using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using HuliacDev.UI;
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

        /// <summary> 현재 씬 전환 진행 중 여부. </summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>
        /// 진행 중인 씬 전환(이 씬으로 들어오는 페이드인 포함)이 끝날 때까지 기다림. 전환 중이 아니면 바로 끝남.
        /// 씬 연출·입력을 화면이 다 밝아진 뒤에 시작할 때 씀.
        /// </summary>
        public UniTask WaitUntilIdleAsync(CancellationToken token)
        {
            return _isTransitioning ? UniTask.WaitWhile(() => _isTransitioning, cancellationToken: token) : UniTask.CompletedTask;
        }

        /// <summary> 테스트 전용: 실제 씬을 로드하지 않고 전환 중 상태를 만듦. </summary>
        internal void SetTransitioningForTest(bool transitioning) => _isTransitioning = transitioning;

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
                if (_logger != null) _logger.ZLogWarning($"[SceneTransitionService] 이미 전환 중이라 {sceneName} 요청을 무시함.");
                return;
            }

            _isTransitioning = true;
            bool fadedOut = false;
            try
            {
                if (_fadeManager)
                {
                    await _fadeManager.FadeOutAsync(fadeDuration);
                    fadedOut = true;
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[SceneTransitionService] fadeManager가 null이라 페이드아웃 없이 {sceneName}을 로드함.");
                }

                AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName);
                if (loading == null)
                {
                    // 빌드 설정에 없는 씬 이름 — 지금 씬에 머문 채 화면만 다시 밝힘
                    if (_logger != null) _logger.ZLogError($"[SceneTransitionService] {sceneName} 씬을 불러올 수 없어(빌드 설정에 없음) 지금 화면에 머묾.");
                }
                else
                {
                    await loading.ToUniTask();
                    await WaitForSceneVideoReadinessAsync();
                }

                if (_fadeManager) await _fadeManager.FadeInAsync(fadeDuration); // 없을 때의 경고는 페이드아웃에서 남김
            }
            catch (Exception e)
            {
                if (_logger != null) _logger.ZLogError($"[SceneTransitionService] {sceneName}(으)로 전환하는 중 오류가 남: {e.Message}");

                // 페이드아웃한 채로 남으면 검은 화면에 터치까지 막혀 비활동 타임아웃으로만(타이틀이면 그것도 없이) 복구되므로 화면을 다시 밝힘
                if (fadedOut && _fadeManager) await _fadeManager.FadeInAsync(fadeDuration);
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
                if (_logger != null) _logger.ZLogWarning($"[SceneTransitionService] 씬 영상 준비 대기 {VideoReadinessTimeoutSeconds}초 초과. 그대로 페이드인함.");
            }
        }
    }
}
