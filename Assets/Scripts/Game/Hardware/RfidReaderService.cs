using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using HuliacDev.Core;
using MessagePipe;
using Microsoft.Extensions.Logging;
using R3;
using Unity.Profiling;
using UnityEngine;
using VContainer;
using HuliacDev.Utils;
using ZLogger;

namespace DGAIZone.Game.Hardware
{
    /// <summary>
    /// 다중 RFID 리더기(최대 5개)의 TCP 통신을 관리하고 태그 인식 시 이벤트를 발행하는 서비스.
    /// 리더기가 클라이언트, PC(이 서비스)가 서버 역할을 함. 한 포트(listenPort)로 모든 리더기의 접속을 받고,
    /// 접속해온 소켓의 IP를 RfidMappings.json의 readers[].ipAddress와 대조해 readerId를 식별함.
    /// </summary>
    public class RfidReaderService : MonoBehaviour
    {
        private class ReaderSession
        {
            public string ReaderId;
            public TcpClient Client;
            public Thread ReadThread;
            public volatile bool IsRunning;
            public string LastUnregisteredUid; // 마지막으로 경고한 미등록 UID — 같은 카드가 올라가 있는 동안 경고를 반복하지 않음(수신 스레드만 씀)
        }

        private IPublisher<RfidTagEvent> _publisher;
        private IPublisher<RfidReaderIdleEvent> _idlePublisher;
        private ILogger<RfidReaderService> _logger;

        // 카드 입력은 Input System을 거치지 않아 비활동 타이머가 활동으로 세지 못하므로, 카드를 올리거나 뗄 때 직접 다시 재게 함
        private InactivityTimer _inactivityTimer;

        private readonly List<ReaderSession> _sessions = new List<ReaderSession>();
        private readonly object _sessionsLock = new object();
        private RfidSettings _settings;
        private RfidMappingItem[] _mappings;

        // 등록된 카드 uid(대소문자 무시). 서버를 열기 전에 메인 스레드에서 한 번 만들고 그 뒤로는 수신 스레드가 읽기만 함.
        // 매핑이 없으면 null이라 거르지 않음(그때는 DispatchTag가 미등록 경고를 남김)
        private HashSet<string> _registeredUids;
        private readonly Subject<(string readerId, string rawData)> _messageSubject = new Subject<(string readerId, string rawData)>();
        private IDisposable _subscription;
        private readonly Subject<string> _idleSubject = new Subject<string>(); // 카드가 떨어진 리더기 id. 수신 스레드에서 받아 메인 스레드에서 발행함
        private IDisposable _idleSubscription;

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _serverRunning;

        // OnDestroy가 시작됐는지. 접속 수락 스레드가 정리 뒤에 세션을 추가해 리더기 연결을 붙든 채 남지 않게 _sessionsLock 안에서 확인함
        private volatile bool _disposed;

        /// <summary> 접속 수락 중 소켓 오류가 나면 다시 받기 전에 쉬는 시간(ms). 같은 오류가 계속될 때 로그와 CPU를 아끼려는 값. </summary>
        private const int AcceptRetryDelayMs = 200;

        /// <summary>
        /// VContainer 의존성 주입. MessagePipe 발행자(카드 인식/카드 떨어짐), 로거, 비활동 타이머를 할당함.
        /// </summary>
        [Inject]
        public void Construct(IPublisher<RfidTagEvent> publisher, IPublisher<RfidReaderIdleEvent> idlePublisher, ILogger<RfidReaderService> logger, InactivityTimer inactivityTimer = null)
        {
            _publisher = publisher;
            _idlePublisher = idlePublisher;
            _logger = logger;
            _inactivityTimer = inactivityTimer;
            if (!_inactivityTimer && _logger != null) _logger.ZLogWarning($"[RfidReaderService] inactivityTimer가 null이라 카드를 올리고 떼도 비활동 시간을 다시 재지 않음.");
        }

        /// <summary>
        /// 씬 시작 시 수신 이벤트 구독을 연결하고 설정 로드를 비동기(UniTask)로 시작함.
        /// </summary>
        private void Start()
        {
            _subscription = _messageSubject.ObserveOnMainThread().Subscribe(OnNetworkDataReceived);
            _idleSubscription = _idleSubject.ObserveOnMainThread().Subscribe(PublishReaderIdle);
            InitializeAsync().Forget();
        }

        /// <summary>
        /// StreamingAssets에서 설정을 로드하고 TCP 서버를 시작해 리더기 클라이언트의 접속을 받음.
        /// </summary>
        private async UniTaskVoid InitializeAsync()
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                _settings = await JsonLoader.LoadAsync<RfidSettings>(Constants.Files.RfidMappings, token, _logger);

                // JsonLoader는 취소돼도 기본 설정을 돌려줌 — 읽는 동안 씬이 내려갔으면 OnDestroy 뒤에 서버를 열어 포트를 붙든 채 남지 않게 멈춤
                token.ThrowIfCancellationRequested();
                if (_settings == null)
                {
                    if (_logger != null) _logger.ZLogError($"[RfidReaderService] RfidMappings.json 로드 실패.");
                    return;
                }

                _mappings = _settings.mappings;
                if (_mappings == null || _mappings.Length == 0)
                {
                    if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] RfidMappings.json에 카드 매핑이 없음.");
                }
                else
                {
                    _registeredUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (RfidMappingItem item in _mappings)
                    {
                        if (item != null && !string.IsNullOrEmpty(item.uid)) _registeredUids.Add(item.uid);
                    }
                }

                if (_settings.readers == null || _settings.readers.Length == 0)
                {
                    if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] RfidMappings.json에 설정된 리더기가 없음. 등록되지 않은 IP는 임시 ID로 접속을 받음.");
                }

                StartServer(_settings.listenPort);
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소 시 예외 무시
            }
        }

        /// <summary>
        /// 지정된 포트로 TCP 서버를 열고, 백그라운드 스레드에서 리더기 클라이언트의 접속을 계속 받아들임.
        /// </summary>
        private void StartServer(int port)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _serverRunning = true;

                _acceptThread = new Thread(AcceptLoop) { Priority = System.Threading.ThreadPriority.BelowNormal, IsBackground = true };
                _acceptThread.Start();

                if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] TCP 서버 시작됨. 포트={port}에서 리더기 접속 대기 중.");
            }
            catch (Exception e)
            {
                if (_logger != null) _logger.ZLogError($"[RfidReaderService] TCP 서버를 포트 {port}에서 시작하는 데 실패함: {e.Message}");
            }
        }

        /// <summary>
        /// 백그라운드 스레드에서 실행되는 클라이언트 접속 수락 루프. 리더기가 재접속해도 계속 받아들임.
        /// </summary>
        private void AcceptLoop()
        {
            while (_serverRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    HandleNewClient(client);
                }
                catch (SocketException e)
                {
                    // 서버 종료(_listener.Stop()) 때 나는 예외는 정상이라 남기지 않음. _serverRunning이 false면 루프가 종료됨
                    if (!_serverRunning) continue;

                    if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] 리더기 접속을 받는 중 소켓 오류가 남: {e.Message}");
                    Thread.Sleep(AcceptRetryDelayMs);
                }
                catch (Exception e)
                {
                    if (!_serverRunning) continue;

                    if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] 클라이언트 접속 수락 중 예외 발생: {e.Message}");
                    Thread.Sleep(AcceptRetryDelayMs);
                }
            }
        }

        /// <summary> 현재 동시에 접속을 받을 리더기 대수. RfidMappings.json의 readers 설정 개수를 그대로 따름(최소 1). </summary>
        private int MaxConcurrentReaders => (_settings?.readers != null && _settings.readers.Length > 0) ? _settings.readers.Length : 1;

        /// <summary>
        /// 새로 접속한 리더기 클라이언트의 IP를 설정된 리더기 목록과 대조해 readerId를 식별하고, 수신 스레드를 시작함.
        /// 같은 IP나 같은 리더기 ID의 좀비 세션(리더기가 재부팅·전원 차단되거나 DHCP로 IP가 바뀌어 이전 소켓이 아직 정리되지 않은 경우)이
        /// 남아있으면 그 세션을 먼저 정리하고 새 접속으로 교체함. 그 외에 이미 MaxConcurrentReaders만큼 접속 중이면 거부함.
        /// </summary>
        private void HandleNewClient(TcpClient client)
        {
            string remoteIp;
            try
            {
                remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                client.NoDelay = true;
            }
            catch (Exception e)
            {
                // 접속 직후 끊긴 연결(RST 등)은 주소를 읽거나 설정할 수 없음
                if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] 접속한 리더기의 주소를 읽지 못해 접속을 닫음: {e.Message}");
                try { client.Close(); } catch (Exception) { /* 이미 닫힌 경우 무시 */ }
                return;
            }

            // ARP 조회로 오래 걸릴 수 있어 잠금 밖에서 먼저 식별함
            string readerId = ResolveReaderId(remoteIp);
            ReaderSession session = new ReaderSession
            {
                ReaderId = readerId,
                Client = client,
                IsRunning = true
            };
            session.ReadThread = new Thread(() => ReadLoop(session)) { Priority = System.Threading.ThreadPriority.BelowNormal, IsBackground = true };

            ReaderSession staleSession;
            lock (_sessionsLock)
            {
                if (_disposed)
                {
                    if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] 서버를 닫는 중이라 {remoteIp}의 접속을 받지 않음.");
                    try { client.Close(); } catch (Exception) { /* 이미 닫힌 경우 무시 */ }
                    return;
                }

                staleSession = _sessions.Find(s => string.Equals(s.ReaderId, readerId, StringComparison.Ordinal) || IsSameRemoteIp(s, remoteIp));

                if (staleSession == null && _sessions.Count >= MaxConcurrentReaders)
                {
                    if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] 현재 리더기 {MaxConcurrentReaders}대까지만 받도록 제한되어 있어 {remoteIp}의 접속을 거부함(이미 {_sessions.Count}대 연결됨).");
                    try { client.Close(); } catch (Exception) { /* 이미 닫힌 경우 무시 */ }
                    return;
                }

                if (staleSession != null) _sessions.Remove(staleSession);

                // 수신 스레드가 곧바로 끝나 목록에서 지우더라도 그보다 먼저 들어가 있도록 스레드를 시작하기 전에 추가함
                _sessions.Add(session);
            }

            if (staleSession != null)
            {
                if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] {readerId}({remoteIp})가 다시 접속함. 이전 세션({staleSession.ReaderId})을 정리함.");
                staleSession.IsRunning = false;
                try { staleSession.Client.Close(); } catch (Exception) { /* 이미 닫힌 경우 무시 */ }
            }

            session.ReadThread.Start();

            if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] {readerId} 리더기가 {remoteIp}에서 접속함.");
        }

        /// <summary> 세션의 접속 IP가 주어진 IP와 같은지 확인함(재접속 시 좀비 세션 탐지용). 소켓이 이미 닫혀있으면 false. </summary>
        private static bool IsSameRemoteIp(ReaderSession session, string ip)
        {
            try
            {
                if (session?.Client?.Client?.RemoteEndPoint is IPEndPoint endPoint)
                {
                    return string.Equals(endPoint.Address.ToString(), ip, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception)
            {
                // 소켓이 이미 닫혀 RemoteEndPoint 접근이 실패하는 경우 무시
            }
            return false;
        }

        /// <summary>
        /// 접속해온 IP를 RfidMappings.json의 readers[].ipAddress와 1순위로 대조해 readerId를 찾음.
        /// 일치하는 IP가 없으면(DHCP로 IP가 바뀌었거나 설정이 비어있는 경우 등) ARP 테이블에서 그 IP의 MAC
        /// 주소를 조회해 readers[].macAddress와 2순위로 대조함. 둘 다 실패하면(테스트 중 미등록 리더기 등)
        /// 원시 데이터를 확인할 수 있도록 IP 기반 임시 ID를 부여함.
        /// </summary>
        private string ResolveReaderId(string remoteIp)
        {
            if (_settings?.readers != null)
            {
                foreach (RfidReaderConfig config in _settings.readers)
                {
                    if (config != null && string.Equals(config.ipAddress, remoteIp, StringComparison.OrdinalIgnoreCase))
                    {
                        return config.readerId;
                    }
                }

                string mac = TryGetMacAddressByArp(remoteIp);
                if (!string.IsNullOrEmpty(mac))
                {
                    foreach (RfidReaderConfig config in _settings.readers)
                    {
                        if (config != null && string.Equals(NormalizeMac(config.macAddress), mac, StringComparison.OrdinalIgnoreCase))
                        {
                            if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] {remoteIp}는 등록된 IP와 다르지만 MAC({mac})으로 {config.readerId} 식별됨.");
                            return config.readerId;
                        }
                    }

                    if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] {remoteIp}(MAC={mac})는 등록된 IP/MAC 어느 쪽과도 일치하지 않음. 임시 ID로 접속을 받음.");
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[RfidReaderService] {remoteIp}는 등록되지 않은 IP이고 ARP로 MAC도 조회하지 못함. 임시 ID로 접속을 받음.");
                }
            }

            return $"Unknown_{remoteIp}";
        }

        /// <summary> Windows ARP로 destIp의 MAC 주소를 조회함(iphlpapi.dll, 성공하면 0). </summary>
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref uint macAddrLen);

        /// <summary>
        /// 윈도우 ARP 테이블을 조회(필요 시 ARP 요청 전송)해 지정된 IP의 MAC 주소를 "XX-XX-XX-XX-XX-XX" 형식으로 반환함.
        /// 같은 로컬 네트워크(스위치/공유기 이하)에 있는 장치만 조회 가능하며, 실패하면 null을 반환함.
        /// </summary>
        private static string TryGetMacAddressByArp(string ipString)
        {
            try
            {
                IPAddress ip = IPAddress.Parse(ipString);
                byte[] ipBytes = ip.GetAddressBytes();
                uint destIp = BitConverter.ToUInt32(ipBytes, 0);

                byte[] macBytes = new byte[6];
                uint macLen = (uint)macBytes.Length;

                int result = SendARP(destIp, 0, macBytes, ref macLen);
                if (result != 0 || macLen == 0) return null;

                using (Utf16ValueStringBuilder sb = ZString.CreateStringBuilder())
                {
                    for (int i = 0; i < macLen; i++)
                    {
                        if (i > 0) sb.Append('-');
                        sb.Append(macBytes[i], "X2");
                    }
                    return sb.ToString();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary> MAC 주소 문자열의 구분자(":", " ")를 "-"로 통일하고 대문자로 정규화함. </summary>
        private static string NormalizeMac(string mac)
        {
            if (string.IsNullOrEmpty(mac)) return "";
            return mac.Replace(":", "-").Replace(" ", "-").ToUpperInvariant();
        }

        /// <summary> 연속 읽기 모드에서 리더기가 보내는 UID 하나의 바이트 수(지금 쓰는 카드는 모두 7바이트 UID). </summary>
        private const int UidByteLength = 7;

        /// <summary> UidByteLength가 되기 전에 이 시간(ms) 동안 더 오는 게 없으면 받은 만큼을 UID 하나로 처리함(7바이트가 아닌 카드 대비). </summary>
        private const int FrameIdleGapMs = 50;

        /// <summary> 데이터를 한 번 기다리는 최대 시간(마이크로초). 데이터가 없어도 이 간격마다 카드 떨어짐을 확인함. </summary>
        private const int ReceiveWaitMicroseconds = 10_000;

        private static readonly ProfilerMarker DispatchTagMarker = new ProfilerMarker("RfidReaderService.DispatchTag");

        /// <summary>
        /// 백그라운드 스레드에서 실행되는 수신 루프. 리더기(KA-LAN-754)는 연속 읽기 모드로 설정되어 있어, 명령을 보내지 않아도
        /// 카드가 올라가 있는 동안 같은 UID를 계속 보내고 카드가 없으면 아무것도 보내지 않음.
        /// UID는 구분자 없는 원시 바이트(예: 81 73 69 22 E5 1D 04)로 오고 그 안에 0x0D 같은 값도 들어 있을 수 있어(등록 카드 중 실제로 있음),
        /// 구분 바이트로 자르지 않고 UidByteLength바이트씩 잘라 16진수 문자열(예: "81736922E51D04")로 바꿈.
        /// 같은 UID가 반복되는 동안은 한 번만 발행하고, UID가 cardRemovedDebounceMs 동안 오지 않으면 카드가 떨어진 것으로 보고 한 번 알림.
        /// 접속 뒤 그 시간 안에 읽힌 첫 카드는 접속 전부터 올려져 있던 카드로 보고 발행하지 않음(판정은 CardPresenceTracker).
        /// NetworkStream.ReadTimeout(소켓 레벨 SO_RCVTIMEO) 기반 블로킹 읽기는 Unity의 Mono/IL2CPP 런타임에서 타임아웃 시
        /// 소켓이 끊어지는 것처럼 동작한 적이 있어 쓰지 않고, Socket.Poll로 짧게 기다렸다가 데이터가 왔을 때만 읽음.
        /// </summary>
        private void ReadLoop(ReaderSession session)
        {
            NetworkStream stream = null;
            byte[] readBuffer = new byte[256];
            byte[] frameBuffer = new byte[UidByteLength];
            int frameLength = 0;
            long lastByteMs = 0;
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew(); // 접속 시각을 0으로 재는 단조 시계

            int removedTimeoutMs = _settings?.cardRemovedDebounceMs ?? RfidSettings.DefaultCardRemovedDebounceMs;
            if (removedTimeoutMs <= 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] cardRemovedDebounceMs가 {removedTimeoutMs}라 기본값 {RfidSettings.DefaultCardRemovedDebounceMs}을 씀(0 이하이면 UID를 받을 때마다 카드가 떨어진 것으로 봄).");
                removedTimeoutMs = RfidSettings.DefaultCardRemovedDebounceMs;
            }
            CardPresenceTracker tracker = new CardPresenceTracker(removedTimeoutMs, 0);
            RfidUidDecoder decoder = new RfidUidDecoder(UidByteLength);

            try
            {
                stream = session.Client.GetStream();
                Socket socket = session.Client.Client;

                while (session.IsRunning && session.Client.Connected)
                {
                    if (socket.Poll(ReceiveWaitMicroseconds, SelectMode.SelectRead))
                    {
                        // 읽을 수 있다고 했는데 0바이트면 리더기가 접속을 끊은 것
                        int n = stream.Read(readBuffer, 0, readBuffer.Length);
                        if (n == 0) break;

                        lastByteMs = clock.ElapsedMilliseconds;
                        for (int i = 0; i < n; i++)
                        {
                            frameBuffer[frameLength++] = readBuffer[i];
                            if (frameLength == UidByteLength)
                            {
                                HandleFrame(session, tracker, decoder.Decode(frameBuffer, frameLength), lastByteMs);
                                frameLength = 0;
                            }
                        }
                    }

                    long nowMs = clock.ElapsedMilliseconds;
                    if (frameLength > 0 && nowMs - lastByteMs >= FrameIdleGapMs)
                    {
                        HandleFrame(session, tracker, decoder.Decode(frameBuffer, frameLength), lastByteMs);
                        frameLength = 0;
                    }

                    string removedCard = tracker.CurrentCard;
                    if (tracker.CheckRemoved(nowMs))
                    {
                        if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] {session.ReaderId} 카드가 떨어짐({removedTimeoutMs}ms 동안 UID 없음, 올라가 있던 동안 UID 간격 최대 {tracker.MaxFrameGapMs}ms): {removedCard}");
                        _idleSubject.OnNext(session.ReaderId); // 구독자가 UI를 바꾸므로 이 수신 스레드에서 바로 발행하지 않음
                    }
                }
            }
            catch (Exception e)
            {
                if (session.IsRunning && _logger != null)
                {
                    _logger.ZLogWarning($"[RfidReaderService] {session.ReaderId} TCP 수신 중 예외 발생: {e.Message}");
                }
            }
            finally
            {
                session.IsRunning = false;
                stream?.Dispose();
                try { session.Client.Close(); } catch (Exception) { /* 이미 닫힌 경우 무시 */ }

                lock (_sessionsLock) _sessions.Remove(session);

                if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] {session.ReaderId} 리더기 접속 종료됨.");
            }
        }

        /// <summary>
        /// 받은 UID 문자열로 카드 상태를 갱신하고, 새로 올라온 카드면 메인 스레드로 넘겨 발행되게 함(등록되지 않은 UID는 경고만 남기고 무시함).
        /// 카드 인식은 게임 화면(IngredientSelectionController)이 처리 결과와 함께 행동 로그로 남기므로 여기서는 남기지 않음.
        /// </summary>
        private void HandleFrame(ReaderSession session, CardPresenceTracker tracker, string uid, long receivedAtMs)
        {
            // 등록되지 않은 UID(관람객의 교통카드, 겹쳐 놓은 다른 카드, 어긋나게 묶인 바이트 등)는 카드 판정에 넣지 않음.
            // 넣으면 올라가 있던 카드가 다시 읽힐 때 새 카드로 발행돼 그 단계부터 진행이 지워짐
            if (_registeredUids != null && !_registeredUids.Contains(uid))
            {
                if (!string.Equals(uid, session.LastUnregisteredUid, StringComparison.Ordinal))
                {
                    session.LastUnregisteredUid = uid;
                    if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] {session.ReaderId}에서 등록되지 않은 카드 uid '{uid}'를 받아 무시함.");
                }
                return;
            }

            switch (tracker.OnCardFrame(uid, receivedAtMs))
            {
                case CardPresenceTracker.CardFrameResult.NewCard:
                    _messageSubject.OnNext((session.ReaderId, uid));
                    break;

                case CardPresenceTracker.CardFrameResult.Baseline:
                    if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] {session.ReaderId} 접속 때 이미 올려져 있던 카드라 무시함(떼었다 다시 올리면 인식): {uid}");
                    break;

                // Repeat: 올라가 있는 카드가 반복해서 보낸 UID라 다시 발행하지 않음(매번 로그를 남기면 스팸)
            }
        }

        /// <summary>
        /// 메인 스레드로 전달된 수신 데이터를 프로파일러 마커 구간 안에서 디스패치함.
        /// </summary>
        private void OnNetworkDataReceived((string readerId, string rawData) data)
        {
            ResetInactivityTimer();
            using (DispatchTagMarker.Auto())
            {
                DispatchTag(data);
            }
        }

        /// <summary> 메인 스레드로 넘어온 카드 떨어짐 알림을 RfidReaderIdleEvent로 발행함. </summary>
        private void PublishReaderIdle(string readerId)
        {
            ResetInactivityTimer();
            if (_idlePublisher != null)
            {
                _idlePublisher.Publish(new RfidReaderIdleEvent(readerId));
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[RfidReaderService] idlePublisher가 null이라 {readerId} 카드 떨어짐을 알릴 수 없음.");
            }
        }

        /// <summary> 관람객이 카드를 올리거나 뗐으므로 비활동 시간을 처음부터 다시 잼(타이머 누락은 Construct에서 경고함). </summary>
        private void ResetInactivityTimer()
        {
            if (_inactivityTimer) _inactivityTimer.ResetTimer();
        }

        /// <summary> 수신 uid를 매핑 목록에서 찾아 category를 RfidTagEvent로 발행함. </summary>
        private void DispatchTag((string readerId, string rawData) data)
        {
            if (_mappings == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] 매핑이 로드되지 않아 태그를 처리할 수 없음.");
                return;
            }

            RfidMappingItem matchedItem = null;
            foreach (RfidMappingItem item in _mappings)
            {
                if (item != null && string.Equals(item.uid, data.rawData, StringComparison.OrdinalIgnoreCase))
                {
                    matchedItem = item;
                    break;
                }
            }

            if (matchedItem == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] {data.readerId}에서 등록되지 않은 카드 uid '{data.rawData}' 수신. 무시함.");
                return;
            }

            if (_publisher != null)
            {
                _publisher.Publish(new RfidTagEvent(data.readerId, matchedItem.category));
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[RfidReaderService] publisher가 null이라 RfidTagEvent를 발행할 수 없음.");
            }
        }

        /// <summary>
        /// 씬 종료 및 파괴 시 TCP 서버와 모든 리더기 접속, 스레드, R3 리소스를 해제함.
        /// </summary>
        private void OnDestroy()
        {
            // 접속 수락 스레드가 아래 정리 뒤에 세션을 추가하지 못하게 먼저 표시함(HandleNewClient가 같은 잠금 안에서 확인)
            lock (_sessionsLock) _disposed = true;
            _serverRunning = false;

            try { _listener?.Stop(); } catch (Exception) { /* 이미 정지된 경우 무시 */ }
            if (_acceptThread != null && _acceptThread.IsAlive)
            {
                _acceptThread.Join(500);
            }

            List<ReaderSession> sessionsSnapshot;
            lock (_sessionsLock) sessionsSnapshot = new List<ReaderSession>(_sessions);

            foreach (ReaderSession session in sessionsSnapshot)
            {
                if (session == null) continue;
                session.IsRunning = false;
                try { session.Client.Close(); } catch (Exception) { /* 이미 닫힌 경우 무시 */ }
                if (session.ReadThread != null && session.ReadThread.IsAlive)
                {
                    session.ReadThread.Join(500);
                }
            }

            lock (_sessionsLock) _sessions.Clear();
            _subscription?.Dispose();
            _messageSubject?.Dispose();
            _idleSubscription?.Dispose();
            _idleSubject?.Dispose();
        }

        /// <summary>
        /// 애플리케이션 종료 시 TCP 서버 점유를 해제함.
        /// </summary>
        private void OnApplicationQuit()
        {
            OnDestroy();
        }
    }
}
