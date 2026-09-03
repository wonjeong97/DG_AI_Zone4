using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VContainer;
using Wonjeong.Utils;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// 체험자 이름을 가져오는 제공자. Visitor.json의 isServerConnected로 서버(QR 스캔) 연결 여부를 구분함.
    /// false(로컬 실행)면 defaultUserName을 그대로 사용하고, true(서버 연결)면 서버에서 이름을 조회해야 하지만
    /// 서버가 아직 없어 TODO로 남겨두고 우선 기본 이름으로 대체함.
    /// Visitor.json을 단 한 번만 로드하여 모든 소비자에게 공유함(AppSettingsProvider와 동일한 패턴).
    /// </summary>
    public class VisitorInfoProvider : IDisposable
    {
        private const string DefaultName = "체험자";

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly object _lock = new object();
        private readonly ILogger<VisitorInfoProvider> _logger;

        // 공유 소스로 UniTask 대신 Task를 사용함(여러 소비자가 완료 전 동시에 await할 수 있으므로).
        private Task<VisitorData> _loadTask;
        private bool _isLoadStarted;

        /// <summary> VContainer 생성자 주입. 로거를 할당함. </summary>
        [Inject]
        public VisitorInfoProvider(ILogger<VisitorInfoProvider> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 체험자 이름을 비동기로 반환함. 최초 호출 시에만 실제 로드가 발생하고 이후 호출은 같은 결과를 공유함.
        /// isServerConnected가 false면 defaultUserName(없으면 DefaultName)을 반환하고,
        /// true면 서버 조회가 아직 구현되지 않아 경고 로그를 남기고 DefaultName을 반환함(TODO).
        /// </summary>
        public async UniTask<string> GetNameAsync(CancellationToken cancellationToken = default)
        {
            Task<VisitorData> loadTask;

            lock (_lock)
            {
                if (!_isLoadStarted)
                {
                    _isLoadStarted = true;
                    _loadTask = JsonLoader.LoadAsync<VisitorData>(Constants.Files.Visitor, _cts.Token).AsTask();
                }

                loadTask = _loadTask;
            }

            VisitorData data = await loadTask.AsUniTask().AttachExternalCancellation(cancellationToken);
            await UniTask.SwitchToMainThread(cancellationToken);

            if (data != null && data.isServerConnected)
            {
                // TODO: 서버 연동(QR 스캔)으로 체험자 이름을 조회하도록 구현. 서버가 준비되기 전까지는 기본 이름으로 대체함.
                if (_logger != null) _logger.ZLogWarning($"[VisitorInfoProvider] isServerConnected가 true이지만 서버 연동이 아직 구현되지 않아 기본 이름으로 대체함.");
                return DefaultName;
            }

            return (data != null && !string.IsNullOrEmpty(data.defaultUserName)) ? data.defaultUserName : DefaultName;
        }

        /// <summary> 컨테이너 파기 시 진행 중인 로드를 취소하고 리소스를 해제함. </summary>
        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
