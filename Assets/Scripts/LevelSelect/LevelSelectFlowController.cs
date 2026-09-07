using System;
using System.Threading;
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
        private readonly int unlockedLevelCount = 1; // 2_LevelSelect.json 로드 전까지의 폴백 기본값(앞에서부터 열린 레벨 수, JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)
        private readonly float panelFadeDuration = 0.4f; // 2_LevelSelect.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)
        private readonly float sceneFadeDuration = 0.5f; // 00_Common.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)
        private readonly float selectedLevelButtonMoveDuration = 1.0f; // 2_LevelSelect.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)
        private readonly float selectedLevelButtonMoveOvershoot = 1.3f; // 2_LevelSelect.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)

        // 선택된 레벨 버튼이 storyPanel 바깥에서 이동해 안착하는 위치 (Zone1 StoryManager와 동일한 방식)
        private static readonly Vector2 SelectedLevelButtonPosition = new(-932f, 224f);

        private SceneTransitionService _sceneTransition;
        private SelectedLevelStore _selectedLevelStore;
        private ILogger<LevelSelectFlowController> _logger;
        private bool _isBusy;

        // 2_LevelSelect.json / 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private LevelSelectSceneSettings _sceneSettings;
        private CommonSettings _commonSettings;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 선택된 레벨 저장소, 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, SelectedLevelStore selectedLevelStore, ILogger<LevelSelectFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _selectedLevelStore = selectedLevelStore;
            _logger = logger;
        }

        /// <summary> 초기 패널 상태를 적용하고 레벨 버튼 잠금/활성화 및 클릭 이벤트를 설정한 뒤, 2_LevelSelect.json/00_Common.json 연출 타이밍을 비동기로 불러옴. </summary>
        private void Start()
        {
            ApplyPanelState(levelSelectPanel, true);
            ApplyPanelState(storyPanel, false);

            // 선택된 레벨 버튼이 날아와서 표시되므로 스토리 이미지 플레이스홀더는 숨겨둠
            if (storyImage != null) storyImage.gameObject.SetActive(false);

            // 시작 버튼은 스토리 타이핑이 끝나기 전까지 누를 수 없음
            if (startButton != null)
            {
                startButton.interactable = false;
                startButton.onClick.AddListener(OnStartClicked);
            }

            if (levelButtons == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] levelButtons가 null이라 설정할 버튼이 없음.");
                return;
            }

            for (int i = 0; i < levelButtons.Length; i++)
            {
                Button button = levelButtons[i];
                if (button == null)
                {
                    if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] levelButtons[{i}]가 null임.");
                    continue;
                }

                int index = i;
                button.onClick.AddListener(() => OnLevelClicked(index));
            }

            // 폴백 unlockedLevelCount로 즉시 잠금 상태를 적용해 JSON 로드 전에도 버튼이 정상 표시되도록 하고,
            // 로드가 끝나면 실제 값으로 다시 적용함
            ApplyLevelButtonLocks(unlockedLevelCount);
            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 2_LevelSelect.json(LevelSelectSceneSettings)과 00_Common.json(CommonSettings)을 비동기로 로드하고, 레벨 잠금 상태를 실제 값으로 다시 적용함. </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.LevelSelect}";
            UniTask<LevelSelectSceneSettings> settingsTask = JsonLoader.LoadAsync<LevelSelectSceneSettings>(path, token);
            UniTask<CommonSettings> commonTask = CommonSettingsProvider.GetAsync(token);

            (_sceneSettings, _commonSettings) = await UniTask.WhenAll(settingsTask, commonTask);

            ApplyLevelButtonLocks(_sceneSettings?.unlockedLevelCount ?? unlockedLevelCount);
        }

        /// <summary> levelButtons를 앞에서부터 count개만 잠금 해제 상태로 적용함. </summary>
        private void ApplyLevelButtonLocks(int count)
        {
            if (levelButtons == null) return;

            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (levelButtons[i] != null) ApplyLockState(levelButtons[i], i < count);
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
                if (_logger != null) _logger.ZLogError($"[LevelSelectFlowController] sceneTransition이 null이라 {Constants.Scenes.Game} 씬을 로드할 수 없음.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Game, _commonSettings?.sceneTransitionFadeDuration ?? sceneFadeDuration).Forget();
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

        /// <summary> 열린 레벨 버튼 클릭 시 선택한 버튼을 분리해 스토리 영역으로 트윈 이동시키고 패널을 전환함 (Zone1과 동일한 연출). </summary>
        private void OnLevelClicked(int index)
        {
            if (_isBusy) return;
            if (index < 0 || index >= (_sceneSettings?.unlockedLevelCount ?? unlockedLevelCount)) return;

            if (_selectedLevelStore != null)
            {
                _selectedLevelStore.SelectedLevel = index + 1;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[LevelSelectFlowController] selectedLevelStore가 null이라 선택한 레벨을 기록할 수 없음.");
            }

            if (startButton != null) startButton.interactable = false;

            // 선택한 레벨 버튼을 클릭 즉시 두 패널(levelSelectPanel·storyPanel) 바깥의 공통 부모로 옮김.
            // 두 패널 모두 CanvasGroup으로 페이드되는데, 그 자식으로 두면 페이드 도중 알파 블렌딩 때문에
            // 이미지가 흐릿하게 보여서, 페이드에 영향받지 않는 위치로 미리 빼둔다.
            // levelSelectPanel·storyPanel은 같은 부모 안에서 정확히 같은 영역을 꽉 채우고 있어 좌표계가 동일하므로
            // 이동해도 시각적으로 튀지 않는다. 실제 이동은 storyPanel이 페이드인되는 시점에 맞춰 트윈으로 처리한다 (Zone1 StoryManager와 동일한 방식).
            RectTransform selectedButtonRect = null;
            if (levelButtons != null && index < levelButtons.Length && levelButtons[index] != null)
            {
                selectedButtonRect = (RectTransform)levelButtons[index].transform;
                selectedButtonRect.SetParent(storyPanel.transform.parent, worldPositionStays: false);
                levelButtons[index].interactable = false;

                // 버튼에 달려있던 별(Image_StarN) 아이콘은 스토리 패널로 넘어갈 땐 필요 없으므로 숨김
                foreach (Transform child in selectedButtonRect)
                {
                    child.gameObject.SetActive(false);
                }
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

            SwitchToStoryAsync(storyText, selectedButtonRect).Forget();
        }

        /// <summary> 레벨 선택 패널을 페이드아웃한 뒤 스토리 패널을 페이드인하고, 선택된 버튼을 목표 위치로 이동시키며, 스토리 텍스트 연출이 끝나면 시작 버튼을 활성화함. </summary>
        private async UniTaskVoid SwitchToStoryAsync(TMP_Text storyText, RectTransform selectedButtonRect)
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float duration = _sceneSettings?.panelFadeDuration ?? panelFadeDuration;
            try
            {
                if (levelSelectPanel && levelSelectPanel.gameObject.activeInHierarchy)
                {
                    await FadeCanvasGroupAsync(levelSelectPanel, 1f, 0f, duration, token);
                    ApplyPanelState(levelSelectPanel, false);
                }

                if (storyPanel)
                {
                    // storyPanel이 페이드인되는 동안 선택된 레벨 버튼도 함께 제자리로 튀어 들어오도록(OutBack) 이동.
                    // 이동 시간·반동 크기는 2_LevelSelect.json의 selectedLevelButtonMoveDuration/selectedLevelButtonMoveOvershoot로 재빌드 없이 조정 가능.
                    // 페이드와 동시에 진행되어야 하므로 의도적으로 await하지 않는다.
                    if (selectedButtonRect != null)
                    {
                        Vector2 targetPos = storyImage != null ? storyImage.rectTransform.anchoredPosition : SelectedLevelButtonPosition;
                        float moveDuration = _sceneSettings?.selectedLevelButtonMoveDuration ?? selectedLevelButtonMoveDuration;
                        float overshoot = _sceneSettings?.selectedLevelButtonMoveOvershoot ?? selectedLevelButtonMoveOvershoot;

                        _ = selectedButtonRect.DOAnchorPos(targetPos, moveDuration)
                            .SetEase(Ease.OutBack, overshoot)
                            .SetUpdate(true)
                            .SetLink(selectedButtonRect.gameObject);
                    }

                    await FadeCanvasGroupAsync(storyPanel, 0f, 1f, duration, token);
                    ApplyPanelState(storyPanel, true);
                }

                await StoryLineAnimator.AnimateAsync(storyText,
                    _commonSettings?.storyLineMoveDuration ?? Constants.StoryLine.StoryLineMoveDuration,
                    _commonSettings?.storyLineInterval ?? Constants.StoryLine.StoryLineInterval,
                    _commonSettings?.storyLineYOffset ?? Constants.StoryLine.StoryLineYOffset,
                    IsSkipRequested, token);

                if (startButton != null) startButton.interactable = true;
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> 이번 프레임에 마우스 또는 터치 눌림이 있었는지 반환함(연출 스킵용). </summary>
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
                .SetEase(Ease.Linear)
                .SetUpdate(true) // Zone1과 동일하게 Time.timeScale과 무관하게 동작하도록 함
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
