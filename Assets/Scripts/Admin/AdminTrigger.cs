using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Admin
{
    /// <summary>
    /// 타이틀 왼쪽 위의 보이지 않는 버튼(RaycastArea) — 제한 시간 안에 정해진 횟수만큼 연속으로 누르면 관리자 비밀번호 창을 엶.
    /// 템플릿 GameCloser(앱 종료)와 같은 원리이며, 겹치지 않도록 반대쪽 구석에 둠.
    /// 관리자 레벨 이동으로 플레이하고 타이틀에 돌아온 경우에는 비밀번호 없이 관리자 화면을 바로 다시 엶.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AdminTrigger : MonoBehaviour
    {
        [Tooltip("비밀번호 창을 열기 위해 필요한 연속 클릭 횟수")]
        [SerializeField, Min(1)] private int targetClickCount = 10;

        [Tooltip("연속 클릭으로 인정하는 시간(초) — 첫 클릭부터 이 시간이 지나면 다시 1회부터 셈")]
        [SerializeField, Min(1f)] private float clickTimeWindow = 3f;

        [SerializeField] private AdminPasswordPanel passwordPanel;
        [Tooltip("관리자 레벨 이동에서 돌아왔을 때 바로 열 관리자 화면")]
        [SerializeField] private AdminPanel adminPanel;

        private Button _button;
        private ConsecutiveClickCounter _counter;
        private ILogger<AdminTrigger> _logger;
        private AdminLevelJumpStore _levelJumpStore;

        /// <summary> VContainer 의존성 주입. 로거와 관리자 레벨 이동 저장소를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<AdminTrigger> logger, AdminLevelJumpStore levelJumpStore)
        {
            _logger = logger;
            _levelJumpStore = levelJumpStore;
        }

        /// <summary> 클릭 카운터를 만들고 숨은 버튼에 클릭 동작을 연결함. </summary>
        private void Awake()
        {
            _counter = new ConsecutiveClickCounter(targetClickCount, clickTimeWindow);
            if (TryGetComponent(out _button))
                _button.onClick.AddListener(OnClicked);
        }

        /// <summary>
        /// 연결 누락을 경고하고(씬 주입은 Awake 뒤라 로그를 남기도록 Start에서 확인),
        /// 관리자 레벨 이동에서 돌아온 경우 관리자 화면을 다시 엶.
        /// </summary>
        private void Start()
        {
            if (!_button && _logger != null) _logger.ZLogWarning($"[AdminTrigger] {name}에 Button이 없어 관리자 진입을 받을 수 없음.");
            if (!passwordPanel && _logger != null) _logger.ZLogWarning($"[AdminTrigger] passwordPanel이 null임.");

            if (_levelJumpStore == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[AdminTrigger] levelJumpStore가 null이라 관리자 레벨 이동에서 돌아왔는지 알 수 없음.");
                return;
            }

            if (!_levelJumpStore.OpenAdminOnTitle) return;

            _levelJumpStore.OpenAdminOnTitle = false;
            if (adminPanel) adminPanel.Open();
            else if (_logger != null) _logger.ZLogWarning($"[AdminTrigger] adminPanel이 null이라 관리자 화면을 다시 열 수 없음.");
        }

        /// <summary> 버튼 클릭 연결을 해제함. </summary>
        private void OnDestroy()
        {
            if (_button) _button.onClick.RemoveListener(OnClicked);
        }

        /// <summary> 클릭을 세고, 목표 횟수에 닿으면 비밀번호 창을 엶. </summary>
        private void OnClicked()
        {
            if (!_counter.Register(Time.unscaledTime)) return;

            if (passwordPanel)
            {
                if (_logger != null) _logger.ZLogInformation($"[AdminTrigger] {targetClickCount}회 연속 클릭 — 관리자 비밀번호 창을 엶.");
                passwordPanel.Open();
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[AdminTrigger] passwordPanel이 null이라 비밀번호 창을 열 수 없음.");
            }
        }
    }
}
