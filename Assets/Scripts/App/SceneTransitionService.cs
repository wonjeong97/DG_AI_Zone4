using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine.SceneManagement;
using VContainer;
using Wonjeong.UI;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// 화면 페이드와 함께 씬을 전환하는 서비스. 템플릿 FadeManager로 화면을 어둡게 한 뒤 씬을 로드하고 다시 밝게 함.
    /// 특정 씬 오브젝트에 종속되지 않는 루트 스코프 서비스라, 씬 언로드 도중에도 전환 흐름이 안전하게 완료됨.
    /// </summary>
    public class SceneTransitionService
    {
        private readonly FadeManager _fadeManager;
        private readonly ILogger<SceneTransitionService> _logger;
        private bool _isTransitioning;

        /// <summary> VContainer 생성자 주입. 페이드 매니저와 로거를 할당함. </summary>
        [Inject]
        public SceneTransitionService(FadeManager fadeManager, ILogger<SceneTransitionService> logger)
        {
            _fadeManager = fadeManager;
            _logger = logger;
        }

        /// <summary> 화면을 페이드아웃하고 지정한 씬을 로드한 뒤 페이드인함. </summary>
        public async UniTask LoadSceneWithFadeAsync(string sceneName, float fadeDuration = 0.5f)
        {
            if (_isTransitioning)
            {
                if (_logger != null) _logger.ZLogWarning($"[SceneTransitionService] Already transitioning. Ignored request for {sceneName}.");
                return;
            }

            _isTransitioning = true;
            try
            {
                if (_fadeManager != null) await _fadeManager.FadeOutAsync(fadeDuration);
                else if (_logger != null) _logger.ZLogWarning($"[SceneTransitionService] fadeManager is null. Loading {sceneName} without fade-out.");

                await SceneManager.LoadSceneAsync(sceneName).ToUniTask();

                if (_fadeManager != null) await _fadeManager.FadeInAsync(fadeDuration);
            }
            finally
            {
                _isTransitioning = false;
            }
        }
    }
}
