using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// CanvasGroup 패널의 페이드와 표시 상태를 다루는 공용 유틸(레벨 선택·게임·결과 씬 공용). 게임오브젝트 활성 상태는 유지하되(스크립트는 계속 동작함),
    /// 숨긴 패널은 중첩 Canvas를 꺼 배치·렌더링 대상에서 뺌. 로그 태그에는 넘겨받은 logger의 대상 클래스(T) 이름이 남음.
    /// 인트로는 패널 게임오브젝트를 SetActive로 끄는 방식이라 이 유틸을 쓰지 않음.
    /// </summary>
    public static class PanelFader
    {
        private const float FallbackFadeDuration = 0.4f;

        /// <summary>
        /// DOTween으로 CanvasGroup 알파를 startAlpha에서 endAlpha로 보간함. 페이드 동안에는 상호작용을 막고,
        /// 숨김 상태에서 꺼 둔 중첩 Canvas를 다시 켬. 페이드가 끝난 뒤의 최종 상태는 호출부가 ApplyState로 정함.
        /// </summary>
        public static async UniTask FadeAsync<T>(CanvasGroup group, float startAlpha, float endAlpha, float duration, ILogger<T> logger, CancellationToken token)
        {
            if (!group)
            {
                if (logger != null) logger.ZLogWarning($"[{typeof(T).Name}] 패널 CanvasGroup이 null이라 페이드를 건너뜀.");
                return;
            }

            if (duration < 0f)
            {
                // 0은 즉시 전환으로 그대로 씀(현장에서 페이드를 끄려고 0을 넣는 경우)
                if (logger != null) logger.ZLogWarning($"[{typeof(T).Name}] 페이드 시간이 음수({duration})라 기본값 {FallbackFadeDuration}초를 씀.");
                duration = FallbackFadeDuration;
            }

            SetCanvasEnabled(group, true, logger); // 숨김 상태에서 꺼 둔 패널 캔버스를 페이드 동안 다시 켬
            group.alpha = startAlpha;
            group.interactable = false;
            group.blocksRaycasts = false;

            await group.DOFade(endAlpha, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true) // Zone1과 동일하게 Time.timeScale과 무관하게 동작하도록 함
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
        }

        /// <summary> 패널의 표시 여부에 따라 알파와 상호작용 상태를 설정하고, 숨긴 패널은 중첩 Canvas를 꺼 배치·렌더링 대상에서 뺌. </summary>
        public static void ApplyState<T>(CanvasGroup group, bool visible, ILogger<T> logger)
        {
            if (!group)
            {
                if (logger != null) logger.ZLogWarning($"[{typeof(T).Name}] 패널 CanvasGroup이 null이라 표시 상태를 적용할 수 없음.");
                return;
            }

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            SetCanvasEnabled(group, visible, logger);
        }

        /// <summary> 패널에 붙은 중첩 Canvas를 켜거나 끔. Canvas가 없으면 숨겨도 계속 배치되므로 경고를 남김. </summary>
        private static void SetCanvasEnabled<T>(CanvasGroup group, bool enabled, ILogger<T> logger)
        {
            if (group.TryGetComponent(out Canvas canvas)) canvas.enabled = enabled;
            else if (logger != null) logger.ZLogWarning($"[{typeof(T).Name}] {group.name}에 Canvas가 없어 숨김 상태에서도 배치 대상에 남음.");
        }
    }
}
