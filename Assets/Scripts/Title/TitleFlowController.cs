using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Title
{
    /// <summary>
    /// 타이틀 씬의 화면 흐름 제어. 시작 버튼으로 타이틀에서 인트로 패널로 크로스페이드하고, 다음 버튼으로 게임 씬으로 전환함.
    /// </summary>
    public class TitleFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup titlePanel;
        [SerializeField] private CanvasGroup introPanel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private string gameSceneName = "1_Game";
        [SerializeField] private float panelFadeDuration = 0.4f;
        [SerializeField] private float sceneFadeDuration = 0.5f;

        private SceneTransitionService _sceneTransition;
        private ILogger<TitleFlowController> _logger;
        private bool _isBusy;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, ILogger<TitleFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _logger = logger;
        }

        /// <summary> 초기 패널 상태를 설정하고 버튼 이벤트를 연결함. </summary>
        private void Start()
        {
            ApplyPanelState(titlePanel, true);
            ApplyPanelState(introPanel, false);

            if (startButton) startButton.onClick.AddListener(OnStartClicked);
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] startButton is null.");

            if (nextButton) nextButton.onClick.AddListener(OnNextClicked);
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] nextButton is null.");
        }

        /// <summary> 버튼 리스너를 해제함. </summary>
        private void OnDestroy()
        {
            if (startButton) startButton.onClick.RemoveListener(OnStartClicked);
            if (nextButton) nextButton.onClick.RemoveListener(OnNextClicked);
        }

        /// <summary> 시작 버튼 클릭 시 타이틀에서 인트로 패널로 전환함. </summary>
        private void OnStartClicked()
        {
            if (_isBusy) return;
            SwitchToIntroAsync().Forget();
        }

        /// <summary> 다음 버튼 클릭 시 화면 페이드와 함께 게임 씬으로 전환함. </summary>
        private void OnNextClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[TitleFlowController] sceneTransition is null. Cannot load {gameSceneName}.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(gameSceneName, sceneFadeDuration).Forget();
        }

        /// <summary> 타이틀 패널을 페이드아웃한 뒤 인트로 패널을 페이드인하는 크로스페이드. </summary>
        private async UniTaskVoid SwitchToIntroAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                if (titlePanel)
                {
                    await FadeCanvasGroupAsync(titlePanel, 1f, 0f, panelFadeDuration, token);
                    ApplyPanelState(titlePanel, false);
                }

                if (introPanel)
                {
                    introPanel.gameObject.SetActive(true);
                    await FadeCanvasGroupAsync(introPanel, 0f, 1f, panelFadeDuration, token);
                    ApplyPanelState(introPanel, true);
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

        /// <summary> 패널의 표시 여부에 따라 알파, 상호작용, 활성 상태를 설정함. </summary>
        private void ApplyPanelState(CanvasGroup group, bool visible)
        {
            if (!group) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            group.gameObject.SetActive(visible);
        }
    }
}
