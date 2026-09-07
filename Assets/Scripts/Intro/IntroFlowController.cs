using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Utils;
using ZLogger;

namespace DGAIZone.Intro
{
    /// <summary>
    /// 인트로 씬의 화면 흐름 제어. 인트로 패널에서 스토리 텍스트가 한 줄씩 올라오듯 순차 표시된 뒤 화면을 클릭하면
    /// 튜토리얼 패널로 크로스페이드함. 연출 중 터치 시 즉시 전체 텍스트가 노출되고, 연출 완료/스킵 후 터치 시 크로스페이드함.
    /// 튜토리얼 패널의 "이해했어요" 버튼을 누르면 화면 페이드와 함께 레벨 선택 씬으로 전환함.
    /// </summary>
    public class IntroFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup introPanel;
        [SerializeField] private CanvasGroup tutorialPanel;
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private Button understandButton;
        [SerializeField] private float crossFadeDuration = 0.4f; // 1_Intro.json 로드 전까지의 폴백 기본값
        [SerializeField] private float sceneFadeDuration = 0.5f; // 00_Common.json 로드 전까지의 폴백 기본값

        private const string VisitorPlaceholder = "{name}"; // storyText 안의 이 자리표시자를 실제 체험자 이름으로 교체함

        private SceneTransitionService _sceneTransition;
        private VisitorInfoProvider _visitorInfoProvider;
        private ILogger<IntroFlowController> _logger;
        private bool _isBusy;
        private bool _isIntroActive;
        private bool _isTextAnimating;
        private bool _skipStoryRequested;
        private Color _originalStoryColor;

        // 1_Intro.json / 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private IntroSceneSettings _sceneSettings;
        private CommonSettings _commonSettings;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 체험자 이름 제공자, 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, VisitorInfoProvider visitorInfoProvider, ILogger<IntroFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _visitorInfoProvider = visitorInfoProvider;
            _logger = logger;
        }

        /// <summary> 초기 패널 상태를 설정하고 스토리 연출 및 버튼 이벤트를 시작함. </summary>
        private void Start()
        {
            if (storyText != null)
            {
                _originalStoryColor = storyText.color;
                SetStoryTextAlpha(0f);
            }

            ApplyPanelState(introPanel, true);

            // tutorialPanel은 GameObject를 계속 활성 상태로 유지해 TutorialImageSlider가 씬 진입 시점부터
            // 미리 이미지를 로드해 두도록 함. 크로스페이드 시작 시점에 이미지가 비어 있어 깜빡이는 것을 방지.
            if (tutorialPanel) tutorialPanel.gameObject.SetActive(true);
            ApplyPanelVisibility(tutorialPanel, false);
            _isIntroActive = true;

            if (understandButton) understandButton.onClick.AddListener(OnUnderstandClicked);
            else if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] understandButton이 null임.");

            InitializeStoryTextAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// 1_Intro.json/00_Common.json 연출 타이밍을 불러오고, 체험자 이름을 불러와 storyText 안의 "체험자" 자리표시자를
        /// 실제 이름으로 교체한 뒤, storyTextStartDelay만큼 대기했다가 줄별로 올라오는 등장 연출을 시작함. 알파가 이미
        /// 0으로 설정돼 있어(Start에서) 이름을 불러오는 동안에도 자리표시자 텍스트가 잠깐 보이는 일은 없음.
        /// </summary>
        private async UniTaskVoid InitializeStoryTextAsync(CancellationToken token)
        {
            try
            {
                string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Intro}";
                UniTask<IntroSceneSettings> settingsTask = JsonLoader.LoadAsync<IntroSceneSettings>(path, token);
                UniTask<CommonSettings> commonTask = CommonSettingsProvider.GetAsync(token);

                (_sceneSettings, _commonSettings) = await UniTask.WhenAll(settingsTask, commonTask);

                await ApplyVisitorNameAsync(token);

                float startDelay = _sceneSettings?.storyTextStartDelay ?? 0f;
                if (startDelay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // 씬 전환 등으로 오브젝트가 파괴되어 취소된 경우 — 스토리 연출을 시작하지 않고 종료
                return;
            }

            StartTextAnimation(token);
        }

        /// <summary> Visitor.json(또는 추후 서버/QR)에서 체험자 이름을 가져와 storyText의 자리표시자를 교체함. </summary>
        private async UniTask ApplyVisitorNameAsync(CancellationToken token)
        {
            if (storyText == null || _visitorInfoProvider == null) return;

            string visitorName = await _visitorInfoProvider.GetNameAsync(token);

            using (Utf16ValueStringBuilder sb = ZString.CreateStringBuilder())
            {
                sb.Append(storyText.text);
                sb.Replace(VisitorPlaceholder, visitorName);
                storyText.text = sb.ToString();
            }
        }

        /// <summary> 인트로 패널 활성화 상태에서 연출 중 터치 시 스킵, 연출 종료 또는 스킵 후 터치 시 튜토리얼 패널로 크로스페이드. </summary>
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
                    SwitchToTutorialAsync().Forget();
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
            if (understandButton) understandButton.onClick.RemoveListener(OnUnderstandClicked);
        }

        /// <summary> "이해했어요" 버튼 클릭 시 화면 페이드와 함께 레벨 선택 씬으로 전환함. </summary>
        private void OnUnderstandClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[IntroFlowController] sceneTransition이 null이라 {Constants.Scenes.LevelSelect} 씬을 로드할 수 없음.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.LevelSelect, _commonSettings?.sceneTransitionFadeDuration ?? sceneFadeDuration).Forget();
        }

        /// <summary> 인트로 패널을 페이드아웃한 뒤 튜토리얼 패널을 페이드인하는 크로스페이드 실행. </summary>
        private async UniTaskVoid SwitchToTutorialAsync()
        {
            _isBusy = true;
            _isIntroActive = false;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float duration = _sceneSettings?.crossFadeDuration ?? crossFadeDuration;
            try
            {
                if (introPanel)
                {
                    await FadeCanvasGroupAsync(introPanel, 1f, 0f, duration, token);
                    ApplyPanelState(introPanel, false);
                }

                if (tutorialPanel)
                {
                    await FadeCanvasGroupAsync(tutorialPanel, 0f, 1f, duration, token);
                    ApplyPanelVisibility(tutorialPanel, true);
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
                    _commonSettings?.storyLineMoveDuration ?? Constants.StoryLine.StoryLineMoveDuration,
                    _commonSettings?.storyLineInterval ?? Constants.StoryLine.StoryLineInterval,
                    _commonSettings?.storyLineYOffset ?? Constants.StoryLine.StoryLineYOffset,
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

        /// <summary>
        /// GameObject의 활성 상태는 건드리지 않고 알파/상호작용만 전환함. tutorialPanel처럼 자식 컴포넌트가
        /// 비활성화 이전에 미리 콘텐츠를 로드해 둬야 하는 패널에 사용함.
        /// </summary>
        private void ApplyPanelVisibility(CanvasGroup group, bool visible)
        {
            if (!group) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
