using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Wonjeong.UI;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// 화면 페이드와 함께 씬을 전환하는 서비스. 템플릿 FadeManager로 화면을 어둡게 한 뒤 씬을 로드하고,
    /// 씬 안의 영상이 실제로 화면에 그려질 때까지 기다린 뒤 다시 밝게 함.
    /// 특정 씬 오브젝트에 종속되지 않는 루트 스코프 서비스라, 씬 언로드 도중에도 전환 흐름이 안전하게 완료됨.
    /// </summary>
    public class SceneTransitionService
    {
        private const float VideoReadinessTimeoutSeconds = 5f;

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

        /// <summary> 화면을 페이드아웃하고 지정한 씬을 로드한 뒤, 씬 안의 영상이 실제로 그려질 때까지 기다렸다가 페이드인함. </summary>
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

                await WaitForSceneVideoReadinessAsync();

                if (_fadeManager != null) await _fadeManager.FadeInAsync(fadeDuration);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 레지스트리에 등록된 ISceneVideoReadiness를 구현한 영상 패널들을 찾아, 실제로 화면에
        /// 그려질 때까지 대기함. 해당 패널이 없는 씬은 즉시 통과함. 대기가 지나치게 길어지면
        /// (예: 영상 파일 문제) 씬전환이 영원히 막히지 않도록 타임아웃 후 경고를 남기고 진행함.
        /// </summary>
        private async UniTask WaitForSceneVideoReadinessAsync()
        {
            List<UniTask> readinessTasks = null;
            foreach (ISceneVideoReadiness readiness in VideoReadinessRegistry.ActivePanels)
            {
                readinessTasks ??= new List<UniTask>();
                readinessTasks.Add(readiness.WaitUntilVideoReadyAsync(default));
            }

            if (readinessTasks == null) return;

            try
            {
                await UniTask.WhenAll(readinessTasks).Timeout(TimeSpan.FromSeconds(VideoReadinessTimeoutSeconds));
            }
            catch (TimeoutException)
            {
                if (_logger != null) _logger.ZLogWarning($"[SceneTransitionService] Timed out waiting for scene video readiness after {VideoReadinessTimeoutSeconds}s. Fading in anyway.");
            }
        }
    }
}
