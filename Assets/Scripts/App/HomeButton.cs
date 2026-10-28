using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.Data;
using MessagePipe;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HuliacDev.App;
using HuliacDev.UI;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// 화면 우상단 홈 버튼(인트로·레벨 선택 씬). 누르면 서버에 move_idle을 보내고(MoveIdleEvent) 화면 페이드와 함께 타이틀로 돌아감.
    /// 관리자 레벨 이동으로 시작한 판이면 결과 화면의 다음 버튼처럼 타이틀의 관리자 화면을 다시 열도록 표시함.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HomeButton : MonoBehaviour
    {
        private Button _button;
        private SceneTransitionService _sceneTransition;
        private ILogger<HomeButton> _logger;
        private IPublisher<MoveIdleEvent> _moveIdlePublisher;
        private SoundManager _soundManager;
        private VisitorInfoProvider _visitorInfoProvider; // 행동 로그 주어
        private AdminLevelJumpStore _levelJumpStore;
        private bool _isLeaving; // 이미 눌렀는지 — 연타해도 move_idle이 한 번만 나가도록

        // 00_Common.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private CommonSettings _commonSettings = new CommonSettings();

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 로거, 관람 종료 이벤트 발행자, 효과음 매니저, 체험자 정보, 관리자 레벨 이동 저장소를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, ILogger<HomeButton> logger, IPublisher<MoveIdleEvent> moveIdlePublisher,
            SoundManager soundManager, VisitorInfoProvider visitorInfoProvider, AdminLevelJumpStore levelJumpStore)
        {
            _sceneTransition = sceneTransition;
            _logger = logger;
            _moveIdlePublisher = moveIdlePublisher;
            _soundManager = soundManager;
            _visitorInfoProvider = visitorInfoProvider;
            _levelJumpStore = levelJumpStore;
        }

        /// <summary> 버튼 클릭을 구독하고 00_Common.json 씬 전환 페이드 시간을 비동기로 불러옴. </summary>
        private void Start()
        {
            if (_logger == null) Debug.LogError("[HomeButton] 의존성이 주입되지 않음. 이 씬의 LifetimeScope가 HomeButton을 등록하는지 확인할 것.");

            if (TryGetComponent(out _button)) _button.onClick.AddListener(OnClicked);
            else if (_logger != null) _logger.ZLogWarning($"[HomeButton] Button이 없어 홈 버튼을 연결할 수 없음.");

            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 00_Common.json(CommonSettings)을 비동기로 로드함. </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            _commonSettings = await CommonSettingsProvider.GetAsync(token);
        }

        /// <summary> 버튼 리스너를 해제함. </summary>
        private void OnDestroy()
        {
            if (_button) _button.onClick.RemoveListener(OnClicked);
        }

        /// <summary> 화면 좌표가 이 버튼 위인지 반환함 — 화면 아무 곳 터치로 넘어가는 연출이 홈 버튼 터치까지 받지 않게 함. </summary>
        public bool ContainsScreenPoint(Vector2 screenPosition)
        {
            // Screen Space Overlay 캔버스라 카메라 없이 판정함
            return isActiveAndEnabled && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, screenPosition);
        }

        /// <summary> 체험자가 스스로 처음 화면으로 돌아가므로 서버 통계에 move_idle로 집계하고 타이틀로 감. </summary>
        private void OnClicked()
        {
            if (_isLeaving) return; // 이미 눌러 타이틀로 가는 중(연타) — 정상이라 로그 없음

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[HomeButton] sceneTransition이 null이라 {Constants.Scenes.Title} 씬을 로드할 수 없음.");
                return;
            }

            // 다른 버튼(뒤로·튜토리얼 끝 등)으로 이미 씬을 떠나는 중이면 move_idle을 보내지 않음
            if (_sceneTransition.IsTransitioning)
            {
                if (_logger != null) _logger.ZLogInformation($"[HomeButton] 이미 다른 화면으로 넘어가는 중이라 홈 버튼을 무시함.");
                return;
            }

            _isLeaving = true;
            if (_logger != null) _logger.ZLogInformation($"[HomeButton] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 홈을 누름 — 타이틀로 돌아감.");
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);

            if (_moveIdlePublisher != null) _moveIdlePublisher.Publish(new MoveIdleEvent());
            else if (_logger != null) _logger.ZLogWarning($"[HomeButton] moveIdlePublisher가 null이라 move_idle을 보내지 못함.");

            // 관리자 레벨 이동으로 시작한 판은 결과 화면의 다음 버튼처럼 타이틀의 관리자 화면으로 돌아감
            if (_levelJumpStore == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[HomeButton] levelJumpStore가 null이라 관리자 레벨 이동 판인지 알 수 없어 타이틀로만 돌아감.");
            }
            else if (_levelJumpStore.IsLevelJump)
            {
                _levelJumpStore.OpenAdminOnTitle = true;
            }

            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Title, _commonSettings.sceneTransitionFadeDuration).Forget();
        }
    }
}
