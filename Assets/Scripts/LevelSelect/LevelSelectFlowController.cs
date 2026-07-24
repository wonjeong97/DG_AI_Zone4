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

namespace DGAIZone.LevelSelect
{
    /// <summary>
    /// 레벨 선택 씬의 화면 흐름 제어. 시작 시 잠긴 레벨 버튼을 흑백 처리해 비활성화하고, 열린 레벨 버튼을 누르면
    /// 레벨 선택 패널을 페이드아웃한 뒤 스토리 패널을 페이드인함. 이때 선택한 버튼 이미지를 스토리 이미지로 복사하고
    /// 해당 레벨의 스토리 오브젝트만 활성화함.
    /// </summary>
    public class LevelSelectFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup levelSelectPanel;
        [SerializeField] private CanvasGroup storyPanel;
        [SerializeField] private Button[] levelButtons;      // Button_Level1..5 순서
        [SerializeField] private GameObject[] storyLevels;   // Story_Level1..5 순서
        [SerializeField] private Image storyImage;           // Image_Story
        [SerializeField] private Button startButton;         // Button_Start (타이핑 완료 전까지 비활성)
        [SerializeField] private Material lockedMaterial;    // 잠긴 버튼용 흑백 머티리얼
        [SerializeField] private int unlockedLevelCount = 1; // 앞에서부터 열린 레벨 수
        [SerializeField] private float panelFadeDuration = 0.4f;
        [SerializeField] private float charsPerSecond = 30f; // 스토리 타이핑 속도
        [SerializeField] private float sceneFadeDuration = 0.5f;

        private SceneTransitionService _sceneTransition;
        private ILogger<LevelSelectFlowController> _logger;
        private bool _isBusy;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, ILogger<LevelSelectFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _logger = logger;
        }

        /// <summary> 초기 패널 상태를 적용하고 레벨 버튼 잠금/활성화 및 클릭 이벤트를 설정함. </summary>
        private void Start()
        {
            ApplyPanelState(levelSelectPanel, true);
            ApplyPanelState(storyPanel, false);

            // 시작 버튼은 스토리 타이핑이 끝나기 전까지 누를 수 없음
            if (startButton != null)
            {
                startButton.interactable = false;
                startButton.onClick.AddListener(OnStartClicked);
            }

            if (levelButtons == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] levelButtons is null. No buttons to configure.");
                return;
            }

            for (int i = 0; i < levelButtons.Length; i++)
            {
                Button button = levelButtons[i];
                if (button == null)
                {
                    if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] levelButtons[{i}] is null.");
                    continue;
                }

                bool unlocked = i < unlockedLevelCount;
                ApplyLockState(button, unlocked);

                int index = i;
                button.onClick.AddListener(() => OnLevelClicked(index));
            }
        }

        /// <summary> 버튼 리스너를 해제함. </summary>
        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);

            if (levelButtons == null) return;
            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (levelButtons[i] != null) levelButtons[i].onClick.RemoveAllListeners();
            }
        }

        /// <summary> 시작 버튼 클릭 시 화면 페이드와 함께 게임 씬으로 전환함. </summary>
        private void OnStartClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[LevelSelectFlowController] sceneTransition is null. Cannot load {Constants.Scenes.Game}.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Game, sceneFadeDuration).Forget();
        }

        /// <summary> 버튼의 잠금 여부에 따라 상호작용 가능 상태와 흑백 머티리얼을 적용함. </summary>
        private void ApplyLockState(Button button, bool unlocked)
        {
            button.interactable = unlocked;

            Image image = button.image;
            if (image != null)
            {
                image.material = unlocked ? null : lockedMaterial;
            }

            // 비활성 버튼이 반투명해지지 않도록 disabled 틴트를 불투명 흰색으로 두어 흑백 머티리얼이 그대로 보이게 함.
            if (!unlocked)
            {
                ColorBlock colors = button.colors;
                colors.disabledColor = Color.white;
                button.colors = colors;
            }
        }

        /// <summary> 열린 레벨 버튼 클릭 시 스토리 이미지/레벨을 갱신하고 패널을 크로스페이드함. </summary>
        private void OnLevelClicked(int index)
        {
            if (_isBusy) return;
            if (index < 0 || index >= unlockedLevelCount) return;

            if (startButton != null) startButton.interactable = false;

            if (storyImage != null && levelButtons[index] != null)
            {
                storyImage.sprite = levelButtons[index].image != null ? levelButtons[index].image.sprite : null;
            }

            TMP_Text storyText = null;
            if (storyLevels != null)
            {
                for (int i = 0; i < storyLevels.Length; i++)
                {
                    if (storyLevels[i] != null) storyLevels[i].SetActive(i == index);
                }

                if (index < storyLevels.Length && storyLevels[index] != null)
                {
                    storyText = storyLevels[index].GetComponentInChildren<TMP_Text>(true);
                    // 페이드인 도중 전체 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠
                    if (storyText != null)
                    {
                        storyText.ForceMeshUpdate();
                        storyText.maxVisibleCharacters = 0;
                    }
                }
            }

            SwitchToStoryAsync(storyText).Forget();
        }

        /// <summary> 레벨 선택 패널을 페이드아웃한 뒤 스토리 패널을 페이드인하고, 스토리 텍스트 타이핑이 끝나면 시작 버튼을 활성화함. </summary>
        private async UniTaskVoid SwitchToStoryAsync(TMP_Text storyText)
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                if (levelSelectPanel && levelSelectPanel.gameObject.activeInHierarchy)
                {
                    await FadeCanvasGroupAsync(levelSelectPanel, 1f, 0f, panelFadeDuration, token);
                    ApplyPanelState(levelSelectPanel, false);
                }

                if (storyPanel)
                {
                    await FadeCanvasGroupAsync(storyPanel, 0f, 1f, panelFadeDuration, token);
                    ApplyPanelState(storyPanel, true);
                }

                await TypeStoryAsync(storyText, token);

                if (startButton != null) startButton.interactable = true;
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> DOTween으로 스토리 텍스트를 한 글자씩 노출함. 도중 마우스/터치 클릭이 감지되면 나머지를 한 번에 출력함. </summary>
        private async UniTask TypeStoryAsync(TMP_Text storyText, CancellationToken token)
        {
            if (storyText == null) return;

            storyText.ForceMeshUpdate();
            int total = storyText.textInfo.characterCount;
            storyText.maxVisibleCharacters = 0;
            if (total <= 0) return;

            Tween typingTween = DOTween.To(() => storyText.maxVisibleCharacters,
                    x => storyText.maxVisibleCharacters = x, total, total / Mathf.Max(1f, charsPerSecond))
                .SetEase(Ease.Linear);
            try
            {
                while (typingTween.IsActive() && !typingTween.IsComplete())
                {
                    if (IsSkipRequested()) break; // 클릭 시 나머지를 한 번에 출력
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                storyText.maxVisibleCharacters = total;
            }
            finally
            {
                // 스킵 또는 취소(파괴) 시 남은 트윈을 정리함
                if (typingTween.IsActive()) typingTween.Kill();
            }
        }

        /// <summary> 이번 프레임에 마우스 또는 터치 눌림이 있었는지 반환함(타이핑 스킵용). </summary>
        private bool IsSkipRequested()
        {
            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
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
