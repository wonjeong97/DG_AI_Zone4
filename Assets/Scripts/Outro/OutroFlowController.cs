using Cysharp.Threading.Tasks;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Outro
{
    /// <summary>
    /// 아웃트로 씬의 화면 흐름 제어. 홈 버튼을 누르면 화면 페이드와 함께 타이틀 씬으로 전환함.
    /// </summary>
    public class OutroFlowController : MonoBehaviour
    {
        [SerializeField] private Button homeButton;
        [SerializeField] private float sceneFadeDuration = 0.5f;

        private SceneTransitionService _sceneTransition;
        private ILogger<OutroFlowController> _logger;
        private bool _isBusy;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, ILogger<OutroFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _logger = logger;
        }

        /// <summary> 버튼 이벤트를 연결함. </summary>
        private void Start()
        {
            if (homeButton) homeButton.onClick.AddListener(OnHomeClicked);
            else if (_logger != null) _logger.ZLogWarning($"[OutroFlowController] homeButton이 null임.");
        }

        /// <summary> 버튼 리스너를 해제함. </summary>
        private void OnDestroy()
        {
            if (homeButton) homeButton.onClick.RemoveListener(OnHomeClicked);
        }

        /// <summary> 홈 버튼 클릭 시 화면 페이드와 함께 타이틀 씬으로 전환함. </summary>
        private void OnHomeClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[OutroFlowController] sceneTransition이 null이라 {Constants.Scenes.Title} 씬을 로드할 수 없음.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Title, sceneFadeDuration).Forget();
        }
    }
}
