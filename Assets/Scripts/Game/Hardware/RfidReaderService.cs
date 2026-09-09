using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using MessagePipe;
using Microsoft.Extensions.Logging;
using R3;
using UnityEngine;
using VContainer;
using Wonjeong.Utils;
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
        }

        private IPublisher<RfidTagEvent> _publisher;
        private IPublisher<RfidReaderIdleEvent> _idlePublisher;
        private ILogger<RfidReaderService> _logger;

        private readonly List<ReaderSession> _sessions = new List<ReaderSession>();
        private readonly object _sessionsLock = new object();
        private RfidSettings _settings;
        private RfidMappingItem[] _mappings;
        private readonly Subject<(string readerId, string rawData)> _messageSubject = new Subject<(string readerId, string rawData)>();
        private IDisposable _subscription;

        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _serverRunning;

        /// <summary>
        /// VContainer 의존성 주입. MessagePipe 발행자(카드 인식/카드 떨어짐)와 로거를 할당함.
        /// </summary>
        [Inject]
        public void Construct(IPublisher<RfidTagEvent> publisher, IPublisher<RfidReaderIdleEvent> idlePublisher, ILogger<RfidReaderService> logger)
        {
            _publisher = publisher;
            _idlePublisher = idlePublisher;
            _logger = logger;
        }

        /// <summary>
        /// 씬 시작 시 수신 이벤트 구독을 연결하고 설정 로드를 비동기(UniTask)로 시작함.
        /// </summary>
        private void Start()
        {
            _subscription = _messageSubject.ObserveOnMainThread().Subscribe(OnNetworkDataReceived);
            InitializeAsync().Forget();
        }

        /// <summary>
        /// StreamingAssets에서 설정을 로드하고 TCP 서버를 시작해 리더기 클라이언트의 접속을 받음.
        /// </summary>
        private async UniTaskVoid InitializeAsync()
        {
            try
            {
                _settings = await JsonLoader.LoadAsync<RfidSettings>(Constants.Files.RfidMappings, this.GetCancellationTokenOnDestroy());
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
                catch (SocketException)
                {
                    // 서버 종료(_listener.Stop()) 시 발생하는 정상적인 예외. _serverRunning이 false면 루프가 종료됨.
                }
                catch (Exception e)
                {
                    if (_serverRunning && _logger != null)
                    {
                        _logger.ZLogWarning($"[RfidReaderService] 클라이언트 접속 수락 중 예외 발생: {e.Message}");
                    }
                }
            }
        }

        /// <summary> 현재 동시에 접속을 받을 리더기 대수. RfidMappings.json의 readers 설정 개수를 그대로 따름(최소 1). </summary>
        private int MaxConcurrentReaders => (_settings?.readers != null && _settings.readers.Length > 0) ? _settings.readers.Length : 1;

        /// <summary>
        /// 새로 접속한 리더기 클라이언트의 IP를 설정된 리더기 목록과 대조해 readerId를 식별하고, 수신 스레드를 시작함.
        /// 같은 IP의 좀비 세션(리더기가 재부팅되는 등으로 이전 소켓이 아직 정리되지 않은 경우)이 남아있으면
        /// 그 세션을 먼저 정리하고 새 접속으로 교체함. 그 외에 이미 MaxConcurrentReaders만큼 접속 중이면 거부함.
        /// </summary>
        private void HandleNewClient(TcpClient client)
        {
            string remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            ReaderSession staleSession = null;

            lock (_sessionsLock)
            {
                staleSession = _sessions.Find(s => IsSameRemoteIp(s, remoteIp));

                if (staleSession == null && _sessions.Count >= MaxConcurrentReaders)
                {
                    if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] 현재 리더기 {MaxConcurrentReaders}대까지만 받도록 제한되어 있어 {remoteIp}의 접속을 거부함(이미 {_sessions.Count}대 연결됨).");
                    try { client.Close(); } catch (Exception) { /* 이미 닫힌 경우 무시 */ }
                    return;
                }

                if (staleSession != null) _sessions.Remove(staleSession);
            }

            if (staleSession != null)
            {
                if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] {remoteIp}에서 재접속됨. 이전 세션({staleSession.ReaderId})을 정리함.");
                staleSession.IsRunning = false;
                try { staleSession.Client.Close(); } catch (Exception) { /* 이미 닫힌 경우 무시 */ }
            }

            string readerId = ResolveReaderId(remoteIp);

            client.NoDelay = true;

            var session = new ReaderSession
            {
                ReaderId = readerId,
                Client = client,
                IsRunning = true
            };

            session.ReadThread = new Thread(() => ReadLoop(session)) { Priority = System.Threading.ThreadPriority.BelowNormal, IsBackground = true };
            session.ReadThread.Start();

            lock (_sessionsLock) _sessions.Add(session);

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

                var sb = new StringBuilder();
                for (int i = 0; i < macLen; i++)
                {
                    if (i > 0) sb.Append('-');
                    sb.Append(macBytes[i].ToString("X2"));
                }
                return sb.ToString();
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

        /// <summary> 폴링 응답 프레임의 종결 바이트(실측: 0x0D, CR). </summary>
        private const byte FrameTerminatorByte = 0x0D;

        /// <summary> 폴링 응답이 연속으로 이만큼 타임아웃되면 연결 상태 확인 경고를 한 번 남김(매 폴링마다 로그를 남기면 스팸이 되므로). </summary>
        private const int TimeoutWarnThreshold = 20;

        private enum PollReadResult { Success, Timeout, Disconnected }

        /// <summary>
        /// 백그라운드 스레드에서 실행되는 TCP 폴링 루프. 리더기(KA-LAN-754)는 데이터를 먼저 push하지 않음(실측
        /// 확인됨: 카드만 태그해선 아무 데이터도 안 옴). pollCommandHex 명령을 반복 전송해야 하고, 그 응답으로
        /// 카드 없음=짧은 응답, 카드 있음=UID가 포함된 긴 응답을 주므로 응답 길이로 카드 인식 여부를 판별함.
        /// 카드가 리더기 위에 계속 올라가 있으면 폴링마다(pollIntervalMs 간격) 매번 같은 긴 응답이 반복되므로,
        /// 직전에 발행한 값과 같으면 재발행하지 않고(카드를 계속 대고 있는 동안 이벤트가 수십 번 중복되는 것 방지),
        /// 카드가 떨어져 짧은 응답으로 돌아오면 상태를 리셋해 다음 태그 때 다시 발행되도록 함.
        /// 리더기가 유니티 접속 여부와 무관하게 백그라운드에서 계속 스캔을 유지하다 첫 읽기 명령에 쌓아둔 잔여값을
        /// 그대로 돌려주는 경우가 있어, 접속 후 최초 cardReadsToDiscard회의 "새로운 카드 인식"은 발행하지 않고 버림.
        /// </summary>
        private void ReadLoop(ReaderSession session)
        {
            NetworkStream stream = null;
            byte[] pollCommand = ParseHexBytes(_settings?.pollCommandHex);
            int noCardMaxLength = _settings?.noCardResponseMaxLength ?? 7;
            int pollIntervalMs = _settings?.pollIntervalMs ?? 150;
            int cardReadsToDiscard = _settings?.initialCardReadsToDiscard ?? 2;
            byte[] responseBuffer = new byte[256];
            byte[] drainBuffer = new byte[256];
            int consecutiveTimeouts = 0;
            string lastPublishedDecoded = null;
            int discardedCardReads = 0; // 접속 직후 리더기가 백그라운드에서 계속 스캔하다 쌓아둔 잔여 카드 값을 최초 cardReadsToDiscard회만큼 버림

            try
            {
                if (pollCommand.Length == 0)
                {
                    if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] pollCommandHex 설정이 비어 있어 {session.ReaderId} 폴링을 시작할 수 없음.");
                    return;
                }

                stream = session.Client.GetStream();
                int responseTimeoutMs = _settings?.pollResponseTimeoutMs ?? 300;

                while (session.IsRunning && session.Client.Connected)
                {
                    // 이전 주기의 응답이 타임아웃 이후 지연 도착해 버퍼에 남아있으면, 이번 주기의 응답으로
                    // 잘못 읽혀 요청-응답이 한 주기씩 밀리는(desync) 것을 막기 위해 먼저 비움
                    while (stream.DataAvailable)
                    {
                        stream.Read(drainBuffer, 0, drainBuffer.Length);
                    }

                    stream.Write(pollCommand, 0, pollCommand.Length);
                    stream.Flush();

                    PollReadResult result = TryReadResponseFrame(stream, responseBuffer, responseTimeoutMs, out int length);

                    if (result == PollReadResult.Disconnected) break;

                    if (result == PollReadResult.Timeout)
                    {
                        consecutiveTimeouts++;
                        if (consecutiveTimeouts == TimeoutWarnThreshold && _logger != null)
                        {
                            _logger.ZLogWarning($"[RfidReaderService] {session.ReaderId} 폴링 응답이 연속 {TimeoutWarnThreshold}회 없음. 리더기 연결 상태 확인 필요.");
                        }
                    }
                    else if (length > 0)
                    {
                        consecutiveTimeouts = 0;

                        if (length > noCardMaxLength)
                        {
                            string decoded = DecodeTagPayload(responseBuffer, length);
                            if (!string.IsNullOrEmpty(decoded) && !string.Equals(decoded, lastPublishedDecoded, StringComparison.Ordinal))
                            {
                                lastPublishedDecoded = decoded;

                                if (discardedCardReads < cardReadsToDiscard)
                                {
                                    // 리더기가 접속 전부터(유니티와 무관하게) 백그라운드에서 계속 스캔하며 쌓아둔 값을
                                    // 첫 읽기 명령에 그대로 돌려주는 경우가 있어, 접속 후 최초 cardReadsToDiscard회는
                                    // 발행하지 않고 기준값으로만 저장함.
                                    discardedCardReads++;
                                    if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] {session.ReaderId} 접속 초기 잔여값으로 판단해 무시함({discardedCardReads}/{cardReadsToDiscard}): {decoded}");
                                }
                                else
                                {
                                    if (_logger != null)
                                    {
                                        string hexDump = BitConverter.ToString(responseBuffer, 0, length).Replace("-", " ");
                                        _logger.ZLogInformation($"[RfidReaderService] {session.ReaderId} 카드 인식 응답(HEX, {length}바이트)={hexDump}");
                                    }

                                    _messageSubject.OnNext((session.ReaderId, decoded));
                                }
                            }
                            // decoded가 lastPublishedDecoded와 같으면(같은 카드가 계속 올라가 있음) 중복 발행하지 않고 건너뜀
                        }
                        else
                        {
                            if (lastPublishedDecoded != null)
                            {
                                // 직전까지 인식되어 있던 카드가 방금 떨어짐(짧은 응답으로 전환된 순간) -> 1회만 알림
                                if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] {session.ReaderId} 카드가 떨어짐.");
                                _idlePublisher?.Publish(new RfidReaderIdleEvent(session.ReaderId));
                            }
                            lastPublishedDecoded = null; // 카드가 떨어짐(짧은 응답) -> 다음 태그 때 다시 발행 가능하도록 리셋
                        }
                    }

                    Thread.Sleep(pollIntervalMs);
                }
            }
            catch (Exception e)
            {
                if (session.IsRunning && _logger != null)
                {
                    _logger.ZLogWarning($"[RfidReaderService] {session.ReaderId} TCP 폴링 중 예외 발생: {e.Message}");
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
        /// 폴링 응답 프레임을 읽음. FrameTerminatorByte(0x0D)가 나오면 즉시 완료로 처리하고,
        /// timeoutMs 내에 아무 응답도 없으면 Timeout, 상대가 접속을 끊으면(Read가 0 반환) Disconnected를 반환함.
        /// NetworkStream.ReadTimeout(소켓 레벨 SO_RCVTIMEO) 기반 블로킹 읽기는 Unity의 Mono/IL2CPP 런타임에서
        /// 타임아웃 시 소켓이 끊어지는 것처럼 동작하는 문제가 있어(실측: NetAssist로는 끊김 없이 안정적으로 동작,
        /// 우리 쪽만 계속 재접속됨), 대신 DataAvailable을 짧은 간격으로 폴링하는 방식으로 구현함(소켓 타임아웃
        /// API 자체를 사용하지 않음).
        /// </summary>
        private static PollReadResult TryReadResponseFrame(NetworkStream stream, byte[] buffer, int timeoutMs, out int length)
        {
            const int pollStepMs = 10;
            length = 0;
            int elapsedMs = 0;

            try
            {
                while (elapsedMs < timeoutMs)
                {
                    if (stream.DataAvailable)
                    {
                        int n = stream.Read(buffer, length, buffer.Length - length);
                        if (n == 0) return PollReadResult.Disconnected;

                        length += n;
                        if (buffer[length - 1] == FrameTerminatorByte || length >= buffer.Length)
                        {
                            return PollReadResult.Success;
                        }

                        continue; // 더 받을 데이터가 있을 수 있으니 대기시간 소모 없이 바로 이어서 확인
                    }

                    Thread.Sleep(pollStepMs);
                    elapsedMs += pollStepMs;
                }

                return length > 0 ? PollReadResult.Success : PollReadResult.Timeout;
            }
            catch (Exception)
            {
                return PollReadResult.Disconnected; // 소켓 자체에 문제가 생긴 경우(진짜 연결 끊김)
            }
        }

        /// <summary> "09 41 31 47 33 45 0D" 형식의 공백 구분 16진수 문자열을 바이트 배열로 변환함. </summary>
        private static byte[] ParseHexBytes(string hexWithSpaces)
        {
            if (string.IsNullOrWhiteSpace(hexWithSpaces)) return Array.Empty<byte>();

            string[] tokens = hexWithSpaces.Split(new[] { ' ', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] result = new byte[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                result[i] = Convert.ToByte(tokens[i], 16);
            }
            return result;
        }

        /// <summary> 프레임 앞뒤에 붙을 수 있는 제어 바이트(TAB/LF/CR). 질의응답 프로토콜을 쓰는 경우 대비용. </summary>
        private static bool IsFrameControlByte(byte b) => b == 0x09 || b == 0x0A || b == 0x0D;

        /// <summary>
        /// 수신된 바이트 뭉치를 카드 UID 문자열로 변환함. 두 가지 실측 형식을 모두 대응함:
        /// (1) 매뉴얼 4.1절 기본 동작 - 구분자 없는 원시 바이너리 UID(예: 25 16 F9 96) → 16진수 문자열로 변환.
        /// (2) 별도로 확인된 질의응답 프로토콜 - TAB/LF로 시작하고 CR로 끝나는 아스키 프레임(예: "\nA1G...\r")
        /// → 앞뒤 제어 바이트를 잘라내고 아스키 텍스트로 처리.
        /// 앞뒤 제어 바이트를 먼저 잘라낸 뒤 남은 바이트가 전부 출력 가능한 아스키 범위인지로 (1)/(2)를 자동 판별함.
        /// </summary>
        private static string DecodeTagPayload(byte[] buffer, int length)
        {
            int start = 0;
            int end = length;
            while (start < end && IsFrameControlByte(buffer[start])) start++;
            while (end > start && IsFrameControlByte(buffer[end - 1])) end--;

            int trimmedLength = end - start;
            if (trimmedLength <= 0) return null;

            bool isPrintableAscii = true;
            for (int i = start; i < end; i++)
            {
                byte b = buffer[i];
                if (b < 0x20 || b > 0x7E)
                {
                    isPrintableAscii = false;
                    break;
                }
            }

            if (isPrintableAscii)
            {
                return CleanRawData(Encoding.ASCII.GetString(buffer, start, trimmedLength));
            }

            return BitConverter.ToString(buffer, start, trimmedLength).Replace("-", "");
        }

        /// <summary>
        /// 메인 스레드로 전달된 수신 데이터를 분석하여 매핑 정보를 찾고 이벤트를 발행함.
        /// </summary>
        private void OnNetworkDataReceived((string readerId, string rawData) data)
        {
            if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] {data.readerId}에서 받은 원시 태그: {data.rawData}");

            if (_mappings == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] 매핑이 로드되지 않아 태그를 처리할 수 없음.");
                return;
            }

            RfidMappingItem matchedItem = null;
            foreach (var item in _mappings)
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
                if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] RfidTagEvent 발행됨: {data.readerId} -> category={matchedItem.category}");
            }
        }

        private static string CleanRawData(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            using (var sb = ZString.CreateStringBuilder())
            {
                foreach (char c in input)
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// 씬 종료 및 파괴 시 TCP 서버와 모든 리더기 접속, 스레드, R3 리소스를 해제함.
        /// </summary>
        private void OnDestroy()
        {
            _serverRunning = false;

            try { _listener?.Stop(); } catch (Exception) { /* 이미 정지된 경우 무시 */ }
            if (_acceptThread != null && _acceptThread.IsAlive)
            {
                _acceptThread.Join(500);
            }

            List<ReaderSession> sessionsSnapshot;
            lock (_sessionsLock) sessionsSnapshot = new List<ReaderSession>(_sessions);

            foreach (var session in sessionsSnapshot)
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
