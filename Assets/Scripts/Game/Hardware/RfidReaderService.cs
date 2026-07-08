using System;
using System.IO.Ports;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    /// 다중 RFID 리더기(최대 5개)의 시리얼 통신을 관리하고 태그 인식 시 이벤트를 발행하는 서비스.
    /// </summary>
    public class RfidReaderService : MonoBehaviour
    {
        private class ReaderSession
        {
            public string ReaderId;
            public SerialPort SerialPort;
            public Thread ReadThread;
            public volatile bool IsRunning;
        }

        private IPublisher<RfidTagEvent> _publisher;
        private ILogger<RfidReaderService> _logger;

        private readonly System.Collections.Generic.List<ReaderSession> _sessions = new System.Collections.Generic.List<ReaderSession>();
        private RfidSettings _settings;
        private readonly Subject<(string readerId, string rawData)> _messageSubject = new Subject<(string readerId, string rawData)>();
        private IDisposable _subscription;

        /// <summary>
        /// VContainer 의존성 주입. MessagePipe 발행자와 로거를 할당함.
        /// </summary>
        [Inject]
        public void Construct(IPublisher<RfidTagEvent> publisher, ILogger<RfidReaderService> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        /// <summary>
        /// 씬 시작 시 수신 이벤트 구독을 연결하고 설정 로드를 비동기(UniTask)로 시작함.
        /// </summary>
        private void Start()
        {
            _subscription = _messageSubject.ObserveOnMainThread().Subscribe(OnSerialDataReceived);
            InitializeAsync().Forget();
        }

        /// <summary>
        /// StreamingAssets에서 설정을 로드하고 장치 경로를 기반으로 포트를 찾아 연결함.
        /// </summary>
        private async UniTaskVoid InitializeAsync()
        {
            _settings = await JsonLoader.LoadAsync<RfidSettings>("RfidMappings.json", this.GetCancellationTokenOnDestroy());
            if (_settings == null)
            {
                if (_logger != null) _logger.ZLogError($"[RfidReaderService] Failed to load RfidMappings.json.");
                return;
            }

            if (_settings.readers == null || _settings.readers.Length == 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] No readers configured in RfidMappings.json.");
                return;
            }

            foreach (var readerConfig in _settings.readers)
            {
                if (readerConfig == null) continue;
                string portName = SerialPortFinder.FindPortByInstancePath(readerConfig.deviceInstancePath, _logger);

                if (string.IsNullOrEmpty(portName))
                {
                    portName = readerConfig.fallbackPort;
                    if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] Reader {readerConfig.readerId} port not found by path. Using fallback: {portName}");
                }

                if (string.IsNullOrEmpty(portName))
                {
                    if (_logger != null) _logger.ZLogError($"[RfidReaderService] Reader {readerConfig.readerId} has no valid COM port.");
                    continue;
                }

                ConnectPort(readerConfig.readerId, portName, _settings.baudRate);
            }
        }

        /// <summary>
        /// 지정된 포트와 속도로 시리얼 연결을 열고 백그라운드 수신 스레드를 시작함.
        /// </summary>
        private void ConnectPort(string readerId, string portName, int baudRate)
        {
            try
            {
                var serialPort = new SerialPort(portName, baudRate);
                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;
                serialPort.Open();

                var session = new ReaderSession
                {
                    ReaderId = readerId,
                    SerialPort = serialPort,
                    IsRunning = true
                };

                session.ReadThread = new Thread(() => ReadSerialLoop(session))
                {
                    Priority = System.Threading.ThreadPriority.BelowNormal
                };
                session.ReadThread.Start();

                _sessions.Add(session);

                if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] Reader {readerId} connected to {portName} ({baudRate}bps)");
            }
            catch (Exception e)
            {
                if (_logger != null) _logger.ZLogError($"[RfidReaderService] Reader {readerId} failed to open port {portName}: {e.Message}");
            }
        }

        /// <summary>
        /// 백그라운드 스레드에서 실행되는 시리얼 데이터 수신 루프.
        /// </summary>
        private void ReadSerialLoop(ReaderSession session)
        {
            var port = session.SerialPort;
            while (session.IsRunning && port != null && port.IsOpen)
            {
                try
                {
                    string line = port.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string cleaned = CleanRawData(line);
                        if (!string.IsNullOrEmpty(cleaned))
                        {
                            _messageSubject.OnNext((session.ReaderId, cleaned));
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // 정상적인 타임아웃
                }
                catch (Exception e)
                {
                    if (session.IsRunning && _logger != null)
                    {
                        _logger.ZLogWarning($"[RfidReaderService] Serial read exception on {session.ReaderId}: {e.Message}");
                    }
                }
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// 메인 스레드로 전달된 시리얼 데이터를 분석하여 매핑 정보를 찾고 이벤트를 발행함.
        /// </summary>
        private void OnSerialDataReceived((string readerId, string rawData) data)
        {
            if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] Raw Tag from {data.readerId}: {data.rawData}");

            if (_settings == null || _settings.mappings == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[RfidReaderService] Mappings not loaded. Cannot process tag.");
                return;
            }

            RfidMappingItem matchedItem = null;
            foreach (var item in _settings.mappings)
            {
                if (item != null && string.Equals(item.uid, data.rawData, StringComparison.OrdinalIgnoreCase))
                {
                    matchedItem = item;
                    break;
                }
            }

            string ingredientName = matchedItem != null ? matchedItem.ingredientName : data.rawData;
            string[] matterNames = (matchedItem != null && matchedItem.matterNames != null && matchedItem.matterNames.Length > 0)
                ? matchedItem.matterNames
                : new string[] { $"{ingredientName}-1", $"{ingredientName}-2", $"{ingredientName}-3" };

            if (_publisher != null)
            {
                _publisher.Publish(new RfidTagEvent(data.readerId, ingredientName, matterNames));
                if (_logger != null) _logger.ZLogInformation($"[RfidReaderService] Published RfidTagEvent: {data.readerId} -> {ingredientName} ({matterNames.Length} matters)");
            }
        }

        private string CleanRawData(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 씬 종료 및 파괴 시 모든 시리얼 포트와 스레드, R3 리소스를 해제함.
        /// </summary>
        private void OnDestroy()
        {
            foreach (var session in _sessions)
            {
                if (session == null) continue;
                session.IsRunning = false;
                if (session.ReadThread != null && session.ReadThread.IsAlive)
                {
                    session.ReadThread.Join(500);
                }
                if (session.SerialPort != null && session.SerialPort.IsOpen)
                {
                    session.SerialPort.Close();
                    session.SerialPort.Dispose();
                }
            }
            _sessions.Clear();
            _subscription?.Dispose();
            _messageSubject?.Dispose();
        }

        /// <summary>
        /// 애플리케이션 종료 시 시리얼 포트 점유를 해제함.
        /// </summary>
        private void OnApplicationQuit()
        {
            OnDestroy();
        }
    }
}
