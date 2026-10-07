using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using VContainer;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// 운영 모드(서버 연동 여부)와 화면에 쓸 체험자 이름을 알려 주는 제공자.
    /// 모드와 로컬 모드 이름은 VisitorSettings(SO, 관리자 화면에서 변경 — PlayerPrefs 우선)에서 읽음.
    /// </summary>
    public class VisitorInfoProvider
    {
        private readonly VisitorSettings _settings;
        private readonly ILogger<VisitorInfoProvider> _logger;

        /// <summary> VContainer 생성자 주입. 체험자 설정과 로거를 할당함. </summary>
        [Inject]
        public VisitorInfoProvider(VisitorSettings settings, ILogger<VisitorInfoProvider> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        /// <summary> 서버 연동(QR 인식) 모드인지. 설정이 없으면 로컬 모드로 봄. </summary>
        public bool IsServerConnected
        {
            get
            {
                if (_settings) return _settings.IsServerConnected;

                if (_logger != null) _logger.ZLogWarning($"[VisitorInfoProvider] VisitorSettings가 null이라 로컬 모드로 봄.");
                return false;
            }
        }

        /// <summary>
        /// 화면에 표시할 체험자 이름을 반환함. VisitorSettings의 이름이 비어 있거나 설정이 없으면 Constants.DefaultVisitorName.
        /// 소비자(인트로·레벨 선택·게임·아웃트로)가 await하는 형태를 유지하려고 UniTask로 돌려줌.
        /// </summary>
        public UniTask<string> GetNameAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings)
            {
                if (_logger != null) _logger.ZLogWarning($"[VisitorInfoProvider] VisitorSettings가 null이라 기본 이름 '{Constants.DefaultVisitorName}'을 씀.");
                return UniTask.FromResult(Constants.DefaultVisitorName);
            }

            string visitorName = _settings.VisitorName;
            return UniTask.FromResult(string.IsNullOrEmpty(visitorName) ? Constants.DefaultVisitorName : visitorName);
        }
    }
}
