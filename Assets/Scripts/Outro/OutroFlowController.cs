using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Utils;
using ZLogger;

namespace DGAIZone.Outro
{
    /// <summary>
    /// 아웃트로 씬의 화면 흐름 제어. 홈 버튼을 누르면 화면 페이드와 함께 타이틀 씬으로 전환함.
    /// </summary>
    public class OutroFlowController : MonoBehaviour
    {
        [SerializeField] private Button homeButton;
        [SerializeField] private float sceneFadeDuration = 0.5f; // 5_Outro.json 로드 전까지의 폴백 기본값

        private SceneTransitionService _sceneTransition;
        private ILogger<OutroFlowController> _logger;
        private bool _isBusy;

        // 5_Outro.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private OutroSceneSettings _sceneSettings;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, ILogger<OutroFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _logger = logger;
        }

        /// <summary> 버튼 이벤트를 연결하고 5_Outro.json 연출 타이밍을 비동기로 불러옴. </summary>
        private void Start()
        {
            if (homeButton) homeButton.onClick.AddListener(OnHomeClicked);
            else if (_logger != null) _logger.ZLogWarning($"[OutroFlowController] homeButton이 null임.");

            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 5_Outro.json(OutroSceneSettings)을 비동기로 로드함. </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Outro}";
            _sceneSettings = await JsonLoader.LoadAsync<OutroSceneSettings>(path, token);
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
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Title, _sceneSettings?.sceneFadeDuration ?? sceneFadeDuration).Forget();
        }
    }
}
