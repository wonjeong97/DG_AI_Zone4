using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Game
{
    /// <summary>
    /// 게임 씬의 화면 흐름 제어. 씬 진입 시 게임 패널을 보여주고, 스토리 버튼으로 스토리 패널을 열며, 스토리 표시 중 화면을 클릭하면 게임 패널로 돌아옴.
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup storyPanel;
        [SerializeField] private CanvasGroup gamePanel;
        [SerializeField] private Button storyButton;

        private readonly float panelFadeDuration = 0.4f; // 00_Common.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)

        [Header("Story Level")]
        [SerializeField] private Image storyImage;          // Image_Story
        [SerializeField] private GameObject[] storyLevels;  // Story_Level1..5 순서
        [SerializeField] private LevelData[] levelDataList;  // Level1..5 순서, 2_LevelSelect와 공유하는 스토리 텍스트 소스

        [Header("Current Situation Panel")]
        [SerializeField] private GameObject[] situationPanels; // Image_CurrentSituation 하위 Panel_Level1..5 순서

        [Header("Debug (Editor Testing)")]
        [Range(0, 5)]
        [SerializeField] private int debugStartLevel = 0; // 0=사용 안 함(2_LevelSelect에서 넘어온 레벨 그대로 사용). 1~5면 이 씬을 바로 실행할 때 해당 레벨로 강제 설정. 에디터 테스트 전용이라 JSON으로 분리하지 않음.

        private SelectedLevelStore _selectedLevelStore;
        private ILogger<GameFlowController> _logger;
        private bool _isBusy;
        private int _selectedLevel = 1; // SelectedLevelStore에서 읽어온 현재 레벨(1부터)
        private AsyncOperationHandle<Sprite> _storyImageHandle;

        // 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private CommonSettings _commonSettings;

        /// <summary>
        /// VContainer 의존성 주입. 선택된 레벨 저장소와 로거를 할당함. debugStartLevel이 설정되어 있으면(1~5)
        /// 다른 컴포넌트들이 레벨을 읽기 전에(모든 컴포넌트의 Start()보다 먼저 실행되는 이 시점에) SelectedLevelStore에 반영해,
        /// 2_LevelSelect를 거치지 않고 3_Game 씬을 바로 실행해도 원하는 레벨로 테스트할 수 있게 함.
        /// </summary>
        [Inject]
        public void Construct(SelectedLevelStore selectedLevelStore, ILogger<GameFlowController> logger)
        {
            _selectedLevelStore = selectedLevelStore;
            _logger = logger;

            if (debugStartLevel > 0 && _selectedLevelStore != null)
            {
                _selectedLevelStore.SelectedLevel = debugStartLevel;
                if (_logger != null) _logger.ZLogInformation($"[GameFlowController] 디버그 시작 레벨 오버라이드 적용됨: {debugStartLevel}");
            }
        }

        /// <summary> 초기 패널 상태(게임 표시, 스토리 숨김)를 적용하고 활성 레벨 스토리/상황 패널을 설정한 뒤 버튼 이벤트를 연결하고 00_Common.json을 비동기로 불러옴. </summary>
        private void Start()
        {
            ApplyPanelState(gamePanel, true);
            ApplyPanelState(storyPanel, false);

            _selectedLevel = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;

            SetupStoryLevel();
            SetupSituationPanel();

            if (storyButton) storyButton.onClick.AddListener(OnStoryClicked);
            else if (_logger != null) _logger.ZLogWarning($"[GameFlowController] storyButton이 null임.");

            LoadCommonSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 00_Common.json(CommonSettings)을 비동기로 로드함. </summary>
        private async UniTaskVoid LoadCommonSettingsAsync(CancellationToken token)
        {
            _commonSettings = await CommonSettingsProvider.GetAsync(token);
        }

        /// <summary> 스토리 패널이 표시된 상태에서 화면 아무 곳이나 마우스/터치로 누르면 게임 패널로 전환함. </summary>
        private void Update()
        {
            if (_isBusy) return;
            if (storyPanel == null || !storyPanel.interactable) return;

            if (IsPointerPressed())
            {
                SwitchToGameAsync().Forget();
            }
        }

        /// <summary> 이번 프레임에 마우스 또는 터치 눌림이 있었는지 반환함. </summary>
        private bool IsPointerPressed()
        {
            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        /// <summary> 활성화된 레벨에 맞춰 스토리 이미지와 스토리 텍스트 오브젝트를 설정함. </summary>
        private void SetupStoryLevel()
        {
            int index = _selectedLevel - 1;

            LoadStoryImageAsync().Forget();

            if (storyLevels != null)
            {
                for (int i = 0; i < storyLevels.Length; i++)
                {
                    if (storyLevels[i] != null) storyLevels[i].SetActive(i == index);
                }

                // levelDataList(LevelData 에셋)에서 스토리 텍스트를 가져옴 — 2_LevelSelect와 같은 에셋을 참조하므로
                // 텍스트를 한 곳만 고치면 두 씬 모두에 반영됨. 할당되지 않았으면 씬에 미리 입력된 텍스트를 그대로 유지함.
                if (index >= 0 && index < storyLevels.Length && storyLevels[index] != null)
                {
                    TMP_Text storyText = storyLevels[index].GetComponentInChildren<TMP_Text>(true);
                    if (storyText != null)
                    {
                        if (levelDataList != null && index < levelDataList.Length && levelDataList[index] != null)
                        {
                            storyText.text = levelDataList[index].storyText;
                        }
                        else if (_logger != null)
                        {
                            _logger.ZLogWarning($"[GameFlowController] levelDataList[{index}]가 비어 있어 씬에 입력된 텍스트를 그대로 사용함.");
                        }
                    }
                }
            }
        }

        /// <summary> 활성화된 레벨에 맞춰 Image_CurrentSituation 하위의 Panel_Level(N)만 표시함. </summary>
        private void SetupSituationPanel()
        {
            if (situationPanels == null) return;

            int index = _selectedLevel - 1;
            for (int i = 0; i < situationPanels.Length; i++)
            {
                if (situationPanels[i] != null) situationPanels[i].SetActive(i == index);
            }
        }

        /// <summary> Addressables에서 활성화된 레벨의 스토리 이미지를 비동기로 불러와 적용함. </summary>
        private async UniTaskVoid LoadStoryImageAsync()
        {
            if (storyImage == null) return;

            string key = $"Level{_selectedLevel}";
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                _storyImageHandle = Addressables.LoadAssetAsync<Sprite>(key);
                Sprite sprite = await _storyImageHandle.Task.AsUniTask().AttachExternalCancellation(token);

                if (_storyImageHandle.Status == AsyncOperationStatus.Succeeded && sprite != null)
                {
                    storyImage.sprite = sprite;
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[GameFlowController] Addressables에서 레벨 이미지 '{key}'를 찾을 수 없음.");
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary> 버튼 리스너를 해제하고 Addressables 핸들을 반환함. </summary>
        private void OnDestroy()
        {
            if (storyButton) storyButton.onClick.RemoveListener(OnStoryClicked);
            if (_storyImageHandle.IsValid()) Addressables.Release(_storyImageHandle);
        }

        /// <summary> 스토리 패널을 페이드아웃한 뒤 게임 패널을 페이드인하는 크로스페이드. 스토리 표시 중 화면 클릭 시 호출됨. </summary>
        private async UniTaskVoid SwitchToGameAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float duration = _commonSettings?.panelFadeDuration ?? panelFadeDuration;
            try
            {
                if (storyPanel)
                {
                    await FadeCanvasGroupAsync(storyPanel, 1f, 0f, duration, token);
                    ApplyPanelState(storyPanel, false);
                }

                if (gamePanel)
                {
                    await FadeCanvasGroupAsync(gamePanel, 0f, 1f, duration, token);
                    ApplyPanelState(gamePanel, true);
                }
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> 스토리 버튼 클릭 시 게임에서 스토리 패널로 되돌아감. </summary>
        private void OnStoryClicked()
        {
            if (_isBusy) return;
            SwitchToStoryAsync().Forget();
        }

        /// <summary> 게임 패널을 페이드아웃한 뒤 스토리 패널을 페이드인하는 크로스페이드. </summary>
        private async UniTaskVoid SwitchToStoryAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float duration = _commonSettings?.panelFadeDuration ?? panelFadeDuration;
            try
            {
                if (gamePanel)
                {
                    await FadeCanvasGroupAsync(gamePanel, 1f, 0f, duration, token);
                    ApplyPanelState(gamePanel, false);
                }

                if (storyPanel)
                {
                    await FadeCanvasGroupAsync(storyPanel, 0f, 1f, duration, token);
                    ApplyPanelState(storyPanel, true);
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
                .SetEase(Ease.Linear)
                .SetUpdate(true) // Zone1과 동일하게 Time.timeScale과 무관하게 동작하도록 함
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
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
