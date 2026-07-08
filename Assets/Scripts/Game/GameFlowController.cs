using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Game
{
    /// <summary>
    /// 게임 씬의 화면 흐름 제어. 씬 진입 시 스토리 패널을 보여주고, 시작 버튼으로 스토리에서 게임 패널로 크로스페이드함.
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup storyPanel;
        [SerializeField] private CanvasGroup gamePanel;
        [SerializeField] private Button startButton;
        [SerializeField] private float panelFadeDuration = 0.4f;

        private ILogger<GameFlowController> _logger;
        private bool _isBusy;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<GameFlowController> logger)
        {
            _logger = logger;
        }

        /// <summary> 초기 패널 상태(스토리 표시, 게임 숨김)를 적용하고 버튼 이벤트를 연결함. </summary>
        private void Start()
        {
            ApplyPanelState(storyPanel, true);
            ApplyPanelState(gamePanel, false);

            if (startButton) startButton.onClick.AddListener(OnStartClicked);
            else if (_logger != null) _logger.ZLogWarning($"[GameFlowController] startButton is null.");
        }

        /// <summary> 버튼 리스너를 해제함. </summary>
        private void OnDestroy()
        {
            if (startButton) startButton.onClick.RemoveListener(OnStartClicked);
        }

        /// <summary> 시작 버튼 클릭 시 스토리에서 게임 패널로 전환함. </summary>
        private void OnStartClicked()
        {
            if (_isBusy) return;
            SwitchToGameAsync().Forget();
        }

        /// <summary> 스토리 패널을 페이드아웃한 뒤 게임 패널을 페이드인하는 크로스페이드. </summary>
        private async UniTaskVoid SwitchToGameAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                if (storyPanel)
                {
                    await FadeCanvasGroupAsync(storyPanel, 1f, 0f, panelFadeDuration, token);
                    ApplyPanelState(storyPanel, false);
                }

                if (gamePanel)
                {
                    await FadeCanvasGroupAsync(gamePanel, 0f, 1f, panelFadeDuration, token);
                    ApplyPanelState(gamePanel, true);
                }
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> 프레임 단위 보간으로 CanvasGroup 알파를 변경하는 페이드 핵심 로직. </summary>
        private async UniTask FadeCanvasGroupAsync(CanvasGroup group, float startAlpha, float endAlpha, float duration, CancellationToken token)
        {
            if (!group) return;
            if (duration <= 0f) duration = 0.4f;

            group.alpha = startAlpha;
            group.interactable = false;
            group.blocksRaycasts = false;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            group.alpha = endAlpha;
        }

        /// <summary> 패널의 표시 여부에 따라 알파와 상호작용 상태를 설정함. 활성 상태는 유지하고 알파로만 제어함. </summary>
        private void ApplyPanelState(CanvasGroup group, bool visible)
        {
            if (!group) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
