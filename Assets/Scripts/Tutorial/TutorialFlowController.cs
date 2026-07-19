using Cysharp.Threading.Tasks;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Tutorial
{
    /// <summary>
    /// 튜토리얼 씬의 화면 흐름 제어. "이해했어요" 버튼을 누르면 화면 페이드와 함께 게임 씬으로 전환함.
    /// </summary>
    public class TutorialFlowController : MonoBehaviour
    {
        [SerializeField] private Button understandButton;
        [SerializeField] private float sceneFadeDuration = 0.5f;

        private SceneTransitionService _sceneTransition;
        private ILogger<TutorialFlowController> _logger;
        private bool _isBusy;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, ILogger<TutorialFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _logger = logger;
        }

        /// <summary> 버튼 이벤트를 연결함. </summary>
        private void Start()
        {
            if (understandButton) understandButton.onClick.AddListener(OnUnderstandClicked);
            else if (_logger != null) _logger.ZLogWarning($"[TutorialFlowController] understandButton is null.");
        }

        /// <summary> 버튼 리스너를 해제함. </summary>
        private void OnDestroy()
        {
            if (understandButton) understandButton.onClick.RemoveListener(OnUnderstandClicked);
        }

        /// <summary> "이해했어요" 버튼 클릭 시 화면 페이드와 함께 게임 씬으로 전환함. </summary>
        private void OnUnderstandClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[TutorialFlowController] sceneTransition is null. Cannot load {Constants.Scenes.LevelSelect}.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.LevelSelect, sceneFadeDuration).Forget();
        }
    }
}
