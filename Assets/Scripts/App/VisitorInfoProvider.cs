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
    /// 서버 모드에서는 타이틀이 QR로 확인한 체험자(서버의 idx_user·이름)를 들고 있다가 타이틀로 돌아오면 비움.
    /// </summary>
    public class VisitorInfoProvider
    {
        private const int NoVisitorIdx = -1;

        private readonly VisitorSettings _settings;
        private readonly ILogger<VisitorInfoProvider> _logger;

        /// <summary> 서버 모드에서 QR로 확인한 체험자의 idx_user — 확인 전이거나 체험이 끝나면 -1. </summary>
        public int VisitorIdx { get; private set; } = NoVisitorIdx;

        /// <summary> 서버 모드에서 QR로 확인한 체험자의 서버 이름 — 확인 전이거나 체험이 끝나면 null. </summary>
        public string ServerVisitorName { get; private set; }

        /// <summary> 서버 모드에서 QR로 확인한 체험자가 있는지. </summary>
        public bool HasServerVisitor => VisitorIdx >= 0;

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

        /// <summary> 타이틀에서 QR로 확인한 서버 체험자를 기록함. </summary>
        public void SetServerVisitor(int idxUser, string visitorName)
        {
            VisitorIdx = idxUser;
            ServerVisitorName = visitorName;
        }

        /// <summary> 서버 체험자 기록을 비움 — 타이틀로 돌아오거나 다음 사람의 QR이 들어오면 다시 확인해야 함. </summary>
        public void ClearServerVisitor()
        {
            VisitorIdx = NoVisitorIdx;
            ServerVisitorName = null;
        }

        /// <summary>
        /// 화면에 표시할 체험자 이름을 반환함. 서버 모드면 QR로 확인한 서버 이름, 로컬 모드면 VisitorSettings의 이름이고,
        /// 비어 있으면(관리자 레벨 이동처럼 QR 없이 시작한 서버 모드 판 포함) Constants.DefaultVisitorName.
        /// 소비자(인트로·레벨 선택·게임·아웃트로)가 await하는 형태를 유지하려고 UniTask로 돌려줌.
        /// </summary>
        public UniTask<string> GetNameAsync(CancellationToken cancellationToken = default)
        {
            if (IsServerConnected)
            {
                if (!string.IsNullOrEmpty(ServerVisitorName)) return UniTask.FromResult(ServerVisitorName);

                if (_logger != null) _logger.ZLogWarning($"[VisitorInfoProvider] 서버 모드이지만 QR로 확인한 체험자 이름이 없어 기본 이름 '{Constants.DefaultVisitorName}'을 씀.");
                return UniTask.FromResult(Constants.DefaultVisitorName);
            }

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
