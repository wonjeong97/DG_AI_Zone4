using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using MessagePipe;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HuliacDev.App;
using HuliacDev.UI;
using ZLogger;

namespace DGAIZone.Outro
{
    /// <summary>
    /// 아웃트로 씬의 화면 흐름 제어. 홈 버튼을 누르면 화면 페이드와 함께 타이틀 씬으로 전환함.
    /// </summary>
    public class OutroFlowController : MonoBehaviour
    {
        [SerializeField] private Button homeButton;

        private SceneTransitionService _sceneTransition;
        private ILogger<OutroFlowController> _logger;
        private IPublisher<MoveIdleEvent> _moveIdlePublisher;
        private SoundManager _soundManager;
        private bool _isBusy;

        // 00_Common.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀.
        // 씬 전환 페이드 시간은 다른 씬들과 마찬가지로 00_Common.json의 sceneTransitionFadeDuration을 공유해서 쓰며,
        // 씬별로 값이 갈리지 않도록 함(현장에서 페이드 시간을 한 곳만 바꾸면 전체 씬에 일관되게 반영됨).
        private CommonSettings _commonSettings = new CommonSettings();

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 로거, idle 전환 이벤트 퍼블리셔, 효과음 매니저를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, ILogger<OutroFlowController> logger, IPublisher<MoveIdleEvent> moveIdlePublisher = null, SoundManager soundManager = null)
        {
            _sceneTransition = sceneTransition;
            _logger = logger;
            _moveIdlePublisher = moveIdlePublisher;
            _soundManager = soundManager;
        }

        /// <summary> 버튼 이벤트를 연결하고 00_Common.json 연출 타이밍을 비동기로 불러옴. </summary>
        private void Start()
        {
            if (homeButton) homeButton.onClick.AddListener(OnHomeClicked);
            else if (_logger != null) _logger.ZLogWarning($"[OutroFlowController] homeButton이 null임.");

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
            if (homeButton) homeButton.onClick.RemoveListener(OnHomeClicked);
        }

        /// <summary> 홈 버튼 클릭 시 클릭음을 내고 화면 페이드와 함께 타이틀 씬으로 전환함. </summary>
        private void OnHomeClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[OutroFlowController] sceneTransition이 null이라 {Constants.Scenes.Title} 씬을 로드할 수 없음.");
                return;
            }

            _isBusy = true;
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            if (_moveIdlePublisher != null) _moveIdlePublisher.Publish(new MoveIdleEvent());
            else if (_logger != null) _logger.ZLogWarning($"[OutroFlowController] moveIdlePublisher가 null이라 idle 전환 이벤트를 발행하지 않음.");
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Title, _commonSettings.sceneTransitionFadeDuration).Forget();
        }
    }
}
