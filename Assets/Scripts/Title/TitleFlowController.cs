using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Title
{
    /// <summary>
    /// 타이틀 씬의 화면 흐름 제어. 시작 버튼으로 타이틀에서 인트로 패널로 크로스페이드하고,
    /// 인트로 패널의 스토리 텍스트가 한 줄씩 천천히 올라오듯 순차 표시된 뒤 화면을 클릭하면 다음 씬으로 전환함.
    /// 연출 중 터치 시 즉시 전체 텍스트가 중앙에 고정되고, 연출 완료/스킵 후 터치 시 다음 씬으로 전환함.
    /// </summary>
    public class TitleFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup titlePanel;
        [SerializeField] private CanvasGroup introPanel;
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private float panelFadeDuration = 0.4f;
        [SerializeField] private float sceneFadeDuration = 0.5f;

        private SceneTransitionService _sceneTransition;
        private ILogger<TitleFlowController> _logger;
        private bool _isBusy;
        private bool _isIntroActive;
        private bool _isTextAnimating;
        private bool _skipStoryRequested;
        private Color _originalStoryColor;

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
            _isIntroActive = false;
            _isTextAnimating = false;

            if (storyText == null && introPanel != null)
            {
                storyText = introPanel.GetComponentInChildren<TMP_Text>(true);
            }

            if (storyText != null)
            {
                _originalStoryColor = storyText.color;
                SetStoryTextAlpha(0f);
            }

            ApplyPanelState(titlePanel, true);
            ApplyPanelState(introPanel, false);

            if (startButton) startButton.onClick.AddListener(OnStartClicked);
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] startButton is null.");

            if (nextButton) nextButton.onClick.AddListener(OnNextClicked);
        }

        /// <summary> 인트로 패널 활성화 상태에서 연출 중 터치 시 스킵, 연출 종료 또는 스킵 후 터치 시 다음 씬 전환. </summary>
        private void Update()
        {
            if (!_isIntroActive || _isBusy) return;

            if (IsClickRequested())
            {
                if (_isTextAnimating)
                {
                    _skipStoryRequested = true;
                }
                else
                {
                    OnNextClicked();
                }
            }
        }

        /// <summary> 이번 프레임에 마우스 또는 터치 클릭이 발생했는지 확인함. </summary>
        private bool IsClickRequested()
        {
            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        /// <summary> 버튼 리스너 해제. </summary>
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

        /// <summary> 인트로 패널 화면 클릭 또는 다음 버튼 클릭 시 화면 페이드와 함께 튜토리얼 씬으로 전환함. </summary>
        private void OnNextClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[TitleFlowController] sceneTransition is null. Cannot load {Constants.Scenes.Tutorial}.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Tutorial, sceneFadeDuration).Forget();
        }

        /// <summary> 타이틀 패널을 페이드아웃한 뒤 인트로 패널을 페이드인하는 크로스페이드 및 한 줄씩 올라오는 연출 실행. </summary>
        private async UniTaskVoid SwitchToIntroAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                // 타이틀 패널 페이드아웃 시작 전에 즉시 스토리 텍스트를 100% 투명화
                SetStoryTextAlpha(0f);

                if (titlePanel)
                {
                    await FadeCanvasGroupAsync(titlePanel, 1f, 0f, panelFadeDuration, token);
                    ApplyPanelState(titlePanel, false);
                }

                if (introPanel)
                {
                    SetStoryTextAlpha(0f);
                    introPanel.gameObject.SetActive(true);
                    await FadeCanvasGroupAsync(introPanel, 0f, 1f, panelFadeDuration, token);
                    ApplyPanelState(introPanel, true);
                    _isIntroActive = true;

                    StartTextAnimation(token);
                }
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> TMP_Text의 기본 color 알파 및 정점 알파를 동시에 0으로 지정하여 완전히 숨김. </summary>
        private void SetStoryTextAlpha(float alpha)
        {
            if (storyText == null) return;
            Color c = _originalStoryColor != default ? _originalStoryColor : storyText.color;
            storyText.color = new Color(c.r, c.g, c.b, alpha);
            storyText.ForceMeshUpdate();

            TMP_TextInfo textInfo = storyText.textInfo;
            if (textInfo == null || textInfo.meshInfo == null) return;

            byte byteAlpha = (byte)Mathf.Clamp((int)(alpha * 255f), 0, 255);
            for (int m = 0; m < textInfo.meshInfo.Length; m++)
            {
                Color32[] colors = textInfo.meshInfo[m].colors32;
                if (colors == null) continue;
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i].a = byteAlpha;
                }
            }
            storyText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary> 텍스트 한 줄씩 올라오는 연출을 시작함. </summary>
        private void StartTextAnimation(CancellationToken token)
        {
            if (storyText == null) return;

            _skipStoryRequested = false;
            AnimateStoryAsync(token).Forget();
        }

        /// <summary> 공용 유틸로 스토리 텍스트를 한 줄씩 올리는 연출을 실행함. 연출 중 화면 클릭 시 즉시 전체 표시함. </summary>
        private async UniTaskVoid AnimateStoryAsync(CancellationToken token)
        {
            _isTextAnimating = true;
            try
            {
                await StoryLineAnimator.AnimateAsync(storyText,
                    Constants.StoryLine.StoryLineMoveDuration,
                    Constants.StoryLine.StoryLineInterval,
                    Constants.StoryLine.StoryLineYOffset,
                    () => _skipStoryRequested, token);
            }
            catch (OperationCanceledException) { }
            finally { _isTextAnimating = false; }
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
