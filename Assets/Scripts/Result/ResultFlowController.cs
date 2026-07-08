using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬의 화면 흐름 제어. 결과 패널의 다음 버튼으로 컴플리트 패널로 크로스페이드하고, 컴플리트 패널의 다음 버튼으로 아웃트로 씬으로 전환함.
    /// </summary>
    public class ResultFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup resultPanel;
        [SerializeField] private CanvasGroup completePanel;
        [SerializeField] private Button resultNextButton;
        [SerializeField] private Button completeNextButton;
        [SerializeField] private string outroSceneName = "4_Outro";
        [SerializeField] private float panelFadeDuration = 0.4f;
        [SerializeField] private float sceneFadeDuration = 0.5f;

        private SceneTransitionService _sceneTransition;
        private ILogger<ResultFlowController> _logger;
        private bool _isBusy;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, ILogger<ResultFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _logger = logger;
        }

        /// <summary> 초기 패널 상태(결과 표시, 컴플리트 숨김)를 적용하고 버튼 이벤트를 연결함. </summary>
        private void Start()
        {
            ApplyPanelState(resultPanel, true);
            ApplyPanelState(completePanel, false);

            if (resultNextButton) resultNextButton.onClick.AddListener(OnResultNextClicked);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] resultNextButton is null.");

            if (completeNextButton) completeNextButton.onClick.AddListener(OnCompleteNextClicked);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] completeNextButton is null.");
        }

        /// <summary> 버튼 리스너를 해제함. </summary>
        private void OnDestroy()
        {
            if (resultNextButton) resultNextButton.onClick.RemoveListener(OnResultNextClicked);
            if (completeNextButton) completeNextButton.onClick.RemoveListener(OnCompleteNextClicked);
        }

        /// <summary> 결과 패널의 다음 버튼 클릭 시 컴플리트 패널로 전환함. </summary>
        private void OnResultNextClicked()
        {
            if (_isBusy) return;
            SwitchToCompleteAsync().Forget();
        }

        /// <summary> 컴플리트 패널의 다음 버튼 클릭 시 화면 페이드와 함께 아웃트로 씬으로 전환함. </summary>
        private void OnCompleteNextClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[ResultFlowController] sceneTransition is null. Cannot load {outroSceneName}.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(outroSceneName, sceneFadeDuration).Forget();
        }

        /// <summary> 결과 패널을 페이드아웃한 뒤 컴플리트 패널을 페이드인하는 크로스페이드. </summary>
        private async UniTaskVoid SwitchToCompleteAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                if (resultPanel)
                {
                    await FadeCanvasGroupAsync(resultPanel, 1f, 0f, panelFadeDuration, token);
                    ApplyPanelState(resultPanel, false);
                }

                if (completePanel)
                {
                    await FadeCanvasGroupAsync(completePanel, 0f, 1f, panelFadeDuration, token);
                    ApplyPanelState(completePanel, true);
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

        /// <summary> 패널의 표시 여부에 따라 알파와 상호작용 상태를 설정함. </summary>
        private void ApplyPanelState(CanvasGroup group, bool visible)
        {
            if (!group) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
