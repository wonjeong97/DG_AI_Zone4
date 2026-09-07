using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Utils;
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
        private readonly float panelFadeDuration = 0.4f; // 4_Result.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)
        private readonly float sceneFadeDuration = 0.5f; // 00_Common.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)

        private SceneTransitionService _sceneTransition;
        private ILogger<ResultFlowController> _logger;
        private bool _isBusy;

        // 4_Result.json / 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private ResultSceneSettings _sceneSettings;
        private CommonSettings _commonSettings;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, ILogger<ResultFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _logger = logger;
        }

        /// <summary> 초기 패널 상태(결과 표시, 컴플리트 숨김)를 적용하고 버튼 이벤트를 연결한 뒤 연출 타이밍을 비동기로 불러옴. </summary>
        private void Start()
        {
            ApplyPanelState(resultPanel, true);
            ApplyPanelState(completePanel, false);

            if (resultNextButton) resultNextButton.onClick.AddListener(OnResultNextClicked);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] resultNextButton이 null임.");

            if (completeNextButton) completeNextButton.onClick.AddListener(OnCompleteNextClicked);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] completeNextButton이 null임.");

            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 4_Result.json(ResultSceneSettings)과 00_Common.json(CommonSettings)을 비동기로 로드함. </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Result}";
            UniTask<ResultSceneSettings> settingsTask = JsonLoader.LoadAsync<ResultSceneSettings>(path, token);
            UniTask<CommonSettings> commonTask = CommonSettingsProvider.GetAsync(token);

            (_sceneSettings, _commonSettings) = await UniTask.WhenAll(settingsTask, commonTask);
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
            ShowCompletePanel();
        }

        /// <summary> 외부 트리거(예: 결과 영상 재생 종료)에서 컴플리트 패널로 전환함. </summary>
        public void ShowCompletePanel()
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
                if (_logger != null) _logger.ZLogError($"[ResultFlowController] sceneTransition이 null이라 {Constants.Scenes.Outro} 씬을 로드할 수 없음.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Outro, _commonSettings?.sceneTransitionFadeDuration ?? sceneFadeDuration).Forget();
        }

        /// <summary> 결과 패널을 페이드아웃한 뒤 컴플리트 패널을 페이드인하는 크로스페이드. </summary>
        private async UniTaskVoid SwitchToCompleteAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float duration = _sceneSettings?.panelFadeDuration ?? panelFadeDuration;
            try
            {
                if (resultPanel && resultPanel.gameObject.activeInHierarchy)
                {
                    await FadeCanvasGroupAsync(resultPanel, 1f, 0f, duration, token);
                    ApplyPanelState(resultPanel, false);
                }

                if (completePanel)
                {
                    await FadeCanvasGroupAsync(completePanel, 0f, 1f, duration, token);
                    ApplyPanelState(completePanel, true);
                }
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> DOTween으로 CanvasGroup 알파를 보간하는 페이드 핵심 로직. </summary>
        private async UniTask FadeCanvasGroupAsync(CanvasGroup group, float startAlpha, float endAlpha, float duration, CancellationToken token)
        {
            if (!group) return;
            if (duration <= 0f) duration = 0.4f;

            group.alpha = startAlpha;
            group.interactable = false;
            group.blocksRaycasts = false;

            await group.DOFade(endAlpha, duration)
                .SetEase(Ease.InOutQuad)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
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
