using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using HuliacDev.Core;
using HuliacDev.UI;
using HuliacDev.Utils;
using ZLogger;

namespace DGAIZone.Intro
{
    /// <summary>
    /// 인트로 씬의 화면 흐름 제어. 인트로 패널에서 스토리 텍스트가 한 줄씩 올라오듯 순차 표시된 뒤 화면을 클릭하면
    /// 튜토리얼 패널로 크로스페이드함. 연출 중 터치 시 즉시 전체 텍스트가 노출되고, 연출 완료/스킵 후 터치 시 크로스페이드함.
    /// 튜토리얼(체험 방법) 마지막 페이지(7/7)에서 &gt; 버튼을 누르면 화면 페이드와 함께 레벨 선택 씬으로 전환함.
    /// 스토리 글이 올라오기 시작할 때 에피소드 나레이션(효과음 키 EpisodeNarration)을 틀고, 튜토리얼로 넘어가거나 씬을 떠나면 멈춤.
    /// 나레이션은 씬에 들어오자마자 미리 불러 두어 스토리 글과 같은 순간에 시작되게 함.
    /// </summary>
    public class IntroFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup introPanel;
        [SerializeField] private CanvasGroup tutorialPanel;
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private TutorialImageSlider tutorialSlider;
        [Tooltip("화면 우상단 홈 버튼 — 이 버튼을 누른 터치는 튜토리얼로 넘기는 화면 터치로 받지 않음")]
        [SerializeField] private HomeButton homeButton;

        // 튜토리얼 패널의 하위 Canvas — 인트로 스토리 동안 패널을 켜 둔 채(이미지 미리 로드) 그리기만 끄려고 씀
        private Canvas _tutorialCanvas;

        private SceneTransitionService _sceneTransition;
        private VisitorInfoProvider _visitorInfoProvider;
        private ILogger<IntroFlowController> _logger;
        private InactivityTimer _inactivityTimer;
        private SoundManager _soundManager;
        private bool _isBusy;
        /// <summary> 씬 전환 중 중복 입력 방지 플래그. </summary>
        public bool IsBusy => _isBusy;
        private bool _isIntroActive;
        private bool _isTextAnimating;
        private bool _isStoryStarted; // 스토리 줄 연출을 시작했는지. 그 전(설정·이름을 불러오는 중)의 터치는 스토리를 건너뛰지 않게 무시함
        private IDisposable _tutorialCompletedSubscription;
        private bool _skipStoryRequested;
        private bool _isNarrationPlaying; // 나레이션을 틀었고 아직 멈추지 않았는지 — 튜토리얼로 넘어가거나 씬을 떠날 때 멈춤
        private Color _originalStoryColor;

        // 1_Intro.json / 00_Common.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private IntroSceneSettings _sceneSettings = new IntroSceneSettings();
        private CommonSettings _commonSettings = new CommonSettings();

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 체험자 이름 제공자, 로거, 비활동 타이머, 효과음 매니저를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, VisitorInfoProvider visitorInfoProvider, ILogger<IntroFlowController> logger, InactivityTimer inactivityTimer = null, SoundManager soundManager = null)
        {
            _sceneTransition = sceneTransition;
            _visitorInfoProvider = visitorInfoProvider;
            _logger = logger;
            _inactivityTimer = inactivityTimer;
            _soundManager = soundManager;
        }

        /// <summary> 초기 패널 상태를 설정하고 스토리 연출 및 버튼 이벤트를 시작함. </summary>
        private void Start()
        {
            if (storyText)
            {
                _originalStoryColor = storyText.color;
                SetStoryTextAlpha(0f);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IntroFlowController] storyText가 null이라 스토리 연출을 진행할 수 없음.");
            }

            ApplyPanelState(introPanel, true);

            // tutorialPanel은 GameObject를 계속 활성 상태로 유지해 TutorialImageSlider가 씬 진입 시점부터
            // 미리 이미지를 로드해 두도록 함. 크로스페이드 시작 시점에 이미지가 비어 있어 깜빡이는 것을 방지.
            if (tutorialPanel) tutorialPanel.gameObject.SetActive(true);
            ApplyPanelVisibility(tutorialPanel, false);
            EnsureTutorialCanvas();
            if (_tutorialCanvas) _tutorialCanvas.enabled = false; // 알파 0이어도 그려지면 인트로 내내 튜토리얼 이미지·글자가 투명하게 겹쳐 그려짐
            _isIntroActive = true;

            if (tutorialSlider)
            {
                _tutorialCompletedSubscription = tutorialSlider.TutorialCompleted.Subscribe(_ => OnTutorialCompleted());
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IntroFlowController] tutorialSlider가 null임.");
            }

            CancellationToken token = this.GetCancellationTokenOnDestroy();
            PreloadNarrationAsync(token).Forget();
            InitializeStoryTextAsync(token).Forget();
        }

        /// <summary>
        /// 나레이션 클립을 미리 불러 두어, 스토리 글이 올라오기 시작할 때 읽기를 기다리지 않고 바로 재생되게 함. 스토리 시작을 막지 않도록
        /// 따로 돌리며, 불러오지 못하면 처음 재생할 때 읽으므로(조금 늦게 시작) 경고만 남김.
        /// </summary>
        private async UniTaskVoid PreloadNarrationAsync(CancellationToken token)
        {
            if (!_soundManager)
            {
                if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] SoundManager가 null이라 나레이션을 미리 불러올 수 없음.");
                return;
            }

            try
            {
                bool loaded = await _soundManager.PreloadAsync(Constants.Sounds.EpisodeNarration, token);
                if (!loaded && _logger != null) _logger.ZLogWarning($"[IntroFlowController] 나레이션을 미리 불러오지 못함(재생할 때 다시 읽어 조금 늦게 시작할 수 있음, 원인은 SoundManager 로그 참고).");
            }
            catch (OperationCanceledException)
            {
                // 씬을 떠나 이 오브젝트가 파괴됨 — 미리 불러오기를 기다리지 않고 끝냄(정상)
            }
        }

        /// <summary>
        /// 1_Intro.json/00_Common.json 연출 타이밍을 불러오고, 체험자 이름을 불러와 storyText 안의 "체험자" 자리표시자를
        /// 실제 이름으로 교체한 뒤, 씬 전환 페이드인이 끝나 화면이 다 보이면 storyTextStartDelay만큼 대기했다가 줄별로 올라오는
        /// 등장 연출을 시작함(첫 줄이 검은 화면 뒤에서 올라와 버리지 않게 함). 알파가 이미 0으로 설정돼 있어(Start에서)
        /// 이름을 불러오는 동안에도 자리표시자 텍스트가 잠깐 보이는 일은 없음.
        /// </summary>
        private async UniTaskVoid InitializeStoryTextAsync(CancellationToken token)
        {
            try
            {
                string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Intro}";
                UniTask<IntroSceneSettings> settingsTask = JsonLoader.LoadAsync<IntroSceneSettings>(path, token, _logger);
                UniTask<CommonSettings> commonTask = CommonSettingsProvider.GetAsync(token);

                (_sceneSettings, _commonSettings) = await UniTask.WhenAll(settingsTask, commonTask);

                await ApplyVisitorNameAsync(token);

                if (_sceneTransition != null) await _sceneTransition.WaitUntilIdleAsync(token);
                else if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] sceneTransition이 null이라 씬 전환이 끝나기를 기다리지 않고 스토리를 시작함.");

                float startDelay = _sceneSettings.storyTextStartDelay;
                if (startDelay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // 씬 전환 등으로 오브젝트가 파괴되어 취소된 경우 — 스토리 연출을 시작하지 않고 종료
                return;
            }

            StartTextAnimation(token);
        }

        /// <summary> 체험자 이름(관리자 화면 이름, 서버 모드면 QR로 확인한 이름)을 가져와 storyText의 자리표시자를 교체함. </summary>
        private async UniTask ApplyVisitorNameAsync(CancellationToken token)
        {
            if (!storyText || _visitorInfoProvider == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] storyText 또는 visitorInfoProvider가 null이라 체험자 이름을 적용할 수 없음.");
                return;
            }

            string visitorName = await _visitorInfoProvider.GetNameAsync(token);
            storyText.text = PlaceholderFormatter.ReplaceVisitorName(storyText.text, visitorName);
        }

        /// <summary> 인트로 패널 활성화 상태에서 연출 중 터치 시 스킵, 연출 종료 또는 스킵 후 터치 시 클릭음을 내고 튜토리얼 패널로 크로스페이드. </summary>
        private void Update()
        {
            if (!_isIntroActive || _isBusy || !_isStoryStarted) return;

            if (StoryLineAnimator.IsPointerPressedThisFrame())
            {
                // 홈 버튼을 누른 터치는 버튼이 타이틀로 보냄 — 스토리를 건너뛰거나 튜토리얼로 넘기지 않음
                if (homeButton && homeButton.ContainsScreenPoint(Pointer.current.position.ReadValue())) return;

                if (_isTextAnimating)
                {
                    _skipStoryRequested = true;
                }
                else
                {
                    StopNarration(); // 클릭음보다 먼저 멈춤(효과음 채널을 함께 멈추므로)
                    SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
                    SwitchToTutorialAsync().Forget();
                }
            }
        }

        /// <summary> 튜토리얼 완료 구독을 해제하고, 튜토리얼로 넘어가지 않고 씬을 떠나면(비활동 등) 나레이션을 멈춤. </summary>
        private void OnDestroy()
        {
            _tutorialCompletedSubscription?.Dispose();

            // 앱 종료로 루트의 SoundManager가 먼저 파괴됐으면 멈출 소리도 없음(정상)
            if (_soundManager) StopNarration();
        }

        /// <summary>
        /// 틀어 둔 나레이션을 멈춤. SoundManager는 효과음 채널 하나로 재생하므로 그 채널을 멈추며, 불러오는 중이던 나레이션도 뒤늦게 재생되지 않음.
        /// 나레이션을 틀지 않았거나 이미 멈췄으면 아무것도 하지 않음(정상).
        /// </summary>
        private void StopNarration()
        {
            if (!_isNarrationPlaying) return;
            _isNarrationPlaying = false;

            if (_soundManager) _soundManager.StopSFX();
            else if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] SoundManager가 null이라 나레이션을 멈출 수 없음.");
        }

        /// <summary> 튜토리얼(체험 방법) 마지막 페이지에서 &gt; 버튼을 누르면 레벨 선택 씬으로 전환함. </summary>
        internal void OnTutorialCompleted()
        {
            OnUnderstandClicked();
        }

        /// <summary> 화면 페이드와 함께 레벨 선택 씬으로 전환함. 기존 테스트 호환성을 위해 유지. </summary>
        internal void OnUnderstandClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[IntroFlowController] sceneTransition이 null이라 {Constants.Scenes.LevelSelect} 씬을 로드할 수 없음.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.LevelSelect, _commonSettings.sceneTransitionFadeDuration).Forget();
        }

        /// <summary> 인트로 패널을 페이드아웃한 뒤 튜토리얼 패널을 페이드인하는 크로스페이드 실행. </summary>
        private async UniTaskVoid SwitchToTutorialAsync()
        {
            _isBusy = true;
            _isIntroActive = false;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float duration = _sceneSettings.crossFadeDuration;
            try
            {
                if (introPanel)
                {
                    await FadeCanvasGroupAsync(introPanel, 1f, 0f, duration, token);
                    ApplyPanelState(introPanel, false);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[IntroFlowController] introPanel이 null이라 패널 전환 연출을 건너뜀.");
                }

                if (tutorialPanel)
                {
                    if (_tutorialCanvas) _tutorialCanvas.enabled = true;
                    await FadeCanvasGroupAsync(tutorialPanel, 0f, 1f, duration, token);
                    ApplyPanelVisibility(tutorialPanel, true);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[IntroFlowController] tutorialPanel이 null이라 패널 전환 연출을 건너뜀.");
                }
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> TMP_Text의 기본 color 알파 및 정점 알파를 동시에 0으로 지정하여 완전히 숨김. </summary>
        private void SetStoryTextAlpha(float alpha)
        {
            if (!storyText)
            {
                if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] storyText가 null이라 알파를 설정할 수 없음.");
                return;
            }

            Color c = _originalStoryColor != default ? _originalStoryColor : storyText.color;
            storyText.color = new Color(c.r, c.g, c.b, alpha);
            storyText.ForceMeshUpdate();

            TMP_TextInfo textInfo = storyText.textInfo;
            if (textInfo == null || textInfo.meshInfo == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] storyText의 메시 정보가 없어 글자 정점 알파를 바꾸지 못함(기본 색 알파만 적용함).");
                return;
            }

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

        /// <summary> 나레이션을 틀고 텍스트 한 줄씩 올라오는 연출을 시작함. </summary>
        private void StartTextAnimation(CancellationToken token)
        {
            _isStoryStarted = true; // 연출을 못 하더라도 터치로 튜토리얼에 넘어갈 수 있게 먼저 표시함

            // 스토리 글과 함께 나레이션을 시작함(글 연출을 건너뛰어도 나레이션은 이어짐)
            SoundEffects.Play(_soundManager, Constants.Sounds.EpisodeNarration, _logger);
            _isNarrationPlaying = _soundManager;

            if (!storyText)
            {
                if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] storyText가 null이라 스토리 연출을 시작할 수 없음.");
                return;
            }

            _skipStoryRequested = false;
            AnimateStoryAsync(token).Forget();
        }

        /// <summary>
        /// 공용 유틸로 스토리 텍스트를 한 줄씩 올리는 연출을 실행함. 글 줄마다 1_Intro.json의 storyLineStartTimes 시각(나레이션과 같은 순간에 시작한 뒤의 시간)에
        /// 올라오게 하고, 시각을 비워 두면 줄 간격으로 올림. 연출 중 화면 클릭 시 즉시 전체 표시함.
        /// </summary>
        private async UniTaskVoid AnimateStoryAsync(CancellationToken token)
        {
            _isTextAnimating = true;
            float[] lineStartTimes = _sceneSettings.storyLineStartTimes;
            try
            {
                await StoryLineAnimator.AnimateAsync(storyText,
                    _commonSettings.storyLineMoveDuration,
                    _commonSettings.storyLineInterval,
                    _commonSettings.storyLineYOffset,
                    () => _skipStoryRequested, token, _inactivityTimer,
                    lineStartTimes != null && lineStartTimes.Length > 0 ? lineStartTimes : null);
            }
            catch (OperationCanceledException) { }
            finally { _isTextAnimating = false; }
        }

        /// <summary> DOTween으로 CanvasGroup 알파를 보간하는 페이드 핵심 로직. </summary>
        private async UniTask FadeCanvasGroupAsync(CanvasGroup group, float startAlpha, float endAlpha, float duration, CancellationToken token)
        {
            if (!group)
            {
                if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] 패널 CanvasGroup이 null이라 페이드를 건너뜀.");
                return;
            }
            if (duration < 0f)
            {
                // 0은 즉시 전환으로 그대로 씀(PanelFader와 같은 규칙, 현장에서 페이드를 끄려고 0을 넣는 경우)
                if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] 패널 페이드 시간이 음수({duration})라 기본값 {PanelFader.FallbackFadeDuration}초를 씀.");
                duration = PanelFader.FallbackFadeDuration;
            }

            group.alpha = startAlpha;
            group.interactable = false;
            group.blocksRaycasts = false;

            await group.DOFade(endAlpha, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true) // Zone1과 동일하게 Time.timeScale과 무관하게 동작하도록 함
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
        }

        /// <summary> 패널의 표시 여부에 따라 알파, 상호작용, 활성 상태를 설정함. </summary>
        private void ApplyPanelState(CanvasGroup group, bool visible)
        {
            if (!group)
            {
                if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] 패널 CanvasGroup이 null이라 표시 상태를 적용할 수 없음.");
                return;
            }
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            group.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 튜토리얼 패널에 하위 Canvas가 없으면 붙여 숨김 동안 그리기만 끌 수 있게 함. 패널 안의 슬라이더·버튼이 터치를 받도록
        /// 같은 오브젝트에 GraphicRaycaster도 붙임(하위 Canvas의 UI는 부모 Canvas의 레이캐스터가 맞히지 않음).
        /// </summary>
        private void EnsureTutorialCanvas()
        {
            if (!tutorialPanel) return; // 누락 경고는 ApplyPanelVisibility가 남김

            if (!tutorialPanel.TryGetComponent(out _tutorialCanvas)) _tutorialCanvas = tutorialPanel.gameObject.AddComponent<Canvas>();
            if (!tutorialPanel.TryGetComponent(out GraphicRaycaster _)) tutorialPanel.gameObject.AddComponent<GraphicRaycaster>();

            // TMP는 글자 메시를 만들 때 자기 Canvas에 셰이더 채널을 켜는데, 숨긴 동안(이 Canvas가 꺼져 있을 때) 만든 글자는 이 Canvas에 켜지 못해
            // 첫 쪽 글자가 흐리게 그려질 수 있으므로 TMP가 쓰는 채널(TexCoord1·Normal·Tangent)을 미리 켬
            _tutorialCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        }

        /// <summary>
        /// GameObject의 활성 상태는 건드리지 않고 알파/상호작용만 전환함. tutorialPanel처럼 자식 컴포넌트가
        /// 비활성화 이전에 미리 콘텐츠를 로드해 둬야 하는 패널에 사용함.
        /// </summary>
        private void ApplyPanelVisibility(CanvasGroup group, bool visible)
        {
            if (!group)
            {
                if (_logger != null) _logger.ZLogWarning($"[IntroFlowController] 패널 CanvasGroup이 null이라 표시 상태를 적용할 수 없음.");
                return;
            }
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
