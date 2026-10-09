using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using HuliacDev.UI;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
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

        [Header("Theme Background")]
        [SerializeField] private Image themeBackgroundImage; // Background: 씬 진입 시 선택된 레벨의 테마 스프라이트로 즉시 교체됨
        [SerializeField] private Sprite[] themeBackgroundSprites; // Level1..5 순서. 레벨 1·2는 같은 스프라이트(Background_1)를 지정하면 됨

        [Header("Story Level")]
        [SerializeField] private Image storyImage;          // Image_Story
        [SerializeField] private GameObject[] storyLevels;  // Story_Level1..5 순서
        [SerializeField] private LevelData[] levelDataList;  // Level1..5 순서, 2_LevelSelect와 공유하는 스토리 텍스트 소스
        public LevelData[] LevelDataList => levelDataList;

        [Header("Current Situation Panel")]
        [SerializeField] private GameObject[] situationPanels; // Image_CurrentSituation 하위 Panel_Level1..5 순서

        [Header("Debug (Editor Testing)")]
        [Range(0, 5)]
        [SerializeField] private int debugStartLevel = 0; // 0=사용 안 함(2_LevelSelect에서 넘어온 레벨 그대로 사용). 1~5면 이 씬을 바로 실행할 때 해당 레벨로 강제 설정. 에디터·개발 빌드에서만 적용되는 테스트 전용 값이라 JSON으로 분리하지 않음.

        private SelectedLevelStore _selectedLevelStore;
        private ILogger<GameFlowController> _logger;
        private bool _isBusy;
        private int _selectedLevel = 1; // SelectedLevelStore에서 읽어온 현재 레벨(1부터)
        private AsyncOperationHandle<Sprite> _storyImageHandle;
        private VisitorInfoProvider _visitorInfoProvider;
        private SoundManager _soundManager;
        private string _visitorName = Constants.DefaultVisitorName;
        private string _sceneStoryTemplate; // levelDataList가 비었을 때 쓰는 씬 스토리 텍스트 원본({name} 치환 전)

        // 00_Common.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private CommonSettings _commonSettings = new CommonSettings();

        /// <summary>
        /// VContainer 의존성 주입. 선택된 레벨 저장소와 로거, 체험자 정보 제공자, 효과음 매니저를 할당함. debugStartLevel이 설정되어 있으면(1~5)
        /// 다른 컴포넌트들이 레벨을 읽기 전에(모든 컴포넌트의 Start()보다 먼저 실행되는 이 시점에) SelectedLevelStore에 반영해,
        /// 2_LevelSelect를 거치지 않고 3_Game 씬을 바로 실행해도 원하는 레벨로 테스트할 수 있게 함. 에디터·개발 빌드에서만 적용하고,
        /// 현장용(릴리스) 빌드에서는 테스트 값이 씬에 남아 있어도 무시하고 경고만 남김.
        /// </summary>
        [Inject]
        public void Construct(
            SelectedLevelStore selectedLevelStore,
            ILogger<GameFlowController> logger,
            VisitorInfoProvider visitorInfoProvider = null,
            SoundManager soundManager = null)
        {
            _selectedLevelStore = selectedLevelStore;
            _logger = logger;
            _visitorInfoProvider = visitorInfoProvider;
            _soundManager = soundManager;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugStartLevel > 0 && _selectedLevelStore != null)
            {
                _selectedLevelStore.SelectedLevel = debugStartLevel;
                if (_logger != null) _logger.ZLogInformation($"[GameFlowController] 디버그 시작 레벨 오버라이드 적용됨: {debugStartLevel}");
            }
#else
            if (debugStartLevel > 0 && _logger != null) _logger.ZLogWarning($"[GameFlowController] 릴리스 빌드라 씬에 남은 디버그 시작 레벨({debugStartLevel})을 무시함.");
#endif
        }

        /// <summary> 초기 패널 상태(게임 표시, 스토리 숨김)를 적용하고 활성 레벨 스토리/상황 패널을 설정한 뒤 버튼 이벤트를 연결하고 00_Common.json을 비동기로 불러옴. </summary>
        private void Start()
        {
            PanelFader.ApplyState(gamePanel, true, _logger);
            PanelFader.ApplyState(storyPanel, false, _logger);

            _selectedLevel = SelectedLevelStore.LevelOrFallback(_selectedLevelStore, _logger, nameof(GameFlowController));

            ApplyThemeBackground();
            SetupStoryLevel();
            SetupSituationPanel();

            if (storyButton) storyButton.onClick.AddListener(OnStoryClicked);
            else if (_logger != null) _logger.ZLogWarning($"[GameFlowController] storyButton이 null임.");

            LoadCommonSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 00_Common.json(CommonSettings) 및 체험자 이름을 비동기로 로드하고 스토리 텍스트에 체험자 이름을 반영함. </summary>
        private async UniTaskVoid LoadCommonSettingsAsync(CancellationToken token)
        {
            UniTask<CommonSettings> commonTask = CommonSettingsProvider.GetAsync(token);
            if (_visitorInfoProvider == null && _logger != null)
            {
                _logger.ZLogWarning($"[GameFlowController] visitorInfoProvider가 null이라 스토리 텍스트에 기본 이름 '{Constants.DefaultVisitorName}'을 사용함.");
            }
            UniTask<string> visitorTask = _visitorInfoProvider != null
                ? _visitorInfoProvider.GetNameAsync(token)
                : UniTask.FromResult(Constants.DefaultVisitorName);

            (_commonSettings, _visitorName) = await UniTask.WhenAll(commonTask, visitorTask);

            ApplyStoryText();
        }

        /// <summary> 스토리 패널이 표시된 상태에서 화면 아무 곳이나 마우스/터치로 누르면 클릭음을 내고 게임 패널로 전환함. </summary>
        private void Update()
        {
            if (_isBusy) return;
            if (!storyPanel || !storyPanel.interactable) return;

            if (StoryLineAnimator.IsPointerPressedThisFrame())
            {
                SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
                SwitchToGameAsync().Forget();
            }
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
                    if (storyLevels[i]) storyLevels[i].SetActive(i == index);
                    else if (_logger != null) _logger.ZLogWarning($"[GameFlowController] storyLevels[{i}]가 null이라 활성 상태를 바꿀 수 없음.");
                }

                if ((index < 0 || index >= storyLevels.Length) && _logger != null)
                    _logger.ZLogWarning($"[GameFlowController] storyLevels가 {storyLevels.Length}개뿐이라 레벨{_selectedLevel} 스토리를 보여 줄 수 없음.");

                ApplyStoryText();
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[GameFlowController] storyLevels가 null이라 레벨 스토리를 보여 줄 수 없음.");
            }
        }

        /// <summary> 활성화된 레벨의 스토리 텍스트에 levelData 및 체험자 이름을 반영함. </summary>
        private void ApplyStoryText()
        {
            int index = _selectedLevel - 1;
            if (storyLevels == null || index < 0 || index >= storyLevels.Length || !storyLevels[index]) return; // 배열 누락·범위·빈 칸은 SetupStoryLevel이 경고함

            // levelDataList(LevelData 에셋)에서 스토리 텍스트를 가져옴 — 2_LevelSelect와 같은 에셋을 참조하므로
            // 텍스트를 한 곳만 고치면 두 씬 모두에 반영됨. 할당되지 않았으면 씬에 미리 입력된 텍스트를 그대로 유지함.
            // 스토리 텍스트 안의 {name} 자리표시자를 실제 체험자 이름으로 교체함.
            if (ChildComponentFinder.TryGetInDirectChildren(storyLevels[index].transform, out TMP_Text storyText))
            {
                if (levelDataList != null && index < levelDataList.Length && levelDataList[index])
                {
                    storyText.text = PlaceholderFormatter.ReplaceVisitorName(levelDataList[index].storyText, _visitorName);
                }
                else
                {
                    // 씬 텍스트를 제자리에서 치환하면 {name}이 사라져 이름이 늦게 로드됐을 때 다시 반영할 수 없으므로, 처음 읽은 원본을 템플릿으로 보관함
                    _sceneStoryTemplate ??= storyText.text;
                    storyText.text = PlaceholderFormatter.ReplaceVisitorName(_sceneStoryTemplate, _visitorName);
                    if (_logger != null) _logger.ZLogWarning($"[GameFlowController] levelDataList[{index}]가 비어 있어 씬에 입력된 텍스트를 그대로 사용함.");
                }
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[GameFlowController] {storyLevels[index].name}의 직계 자식에 TMP_Text가 없어 스토리 텍스트를 바꿀 수 없음.");
            }
        }

        /// <summary>
        /// 씬 진입 시(2_LevelSelect에서 페이드아웃 중이라 화면엔 안 보이는 시점) Background의 스프라이트를
        /// 선택된 레벨(_selectedLevel)에 맞는 테마로 즉시 교체함. 씬 전환 자체가 이미 페이드를 담당하므로
        /// 별도 페이드인 연출 없이 바로 적용함.
        /// </summary>
        private void ApplyThemeBackground()
        {
            if (!themeBackgroundImage)
            {
                if (_logger != null) _logger.ZLogWarning($"[GameFlowController] themeBackgroundImage가 null이라 테마 배경을 바꿀 수 없음.");
                return;
            }

            int index = _selectedLevel - 1;
            Sprite sprite = (themeBackgroundSprites != null && index >= 0 && index < themeBackgroundSprites.Length)
                ? themeBackgroundSprites[index]
                : null;

            if (!sprite)
            {
                if (_logger != null) _logger.ZLogWarning($"[GameFlowController] themeBackgroundSprites[{index}]가 비어 있어 테마 배경을 바꾸지 못함.");
                return;
            }

            themeBackgroundImage.sprite = sprite;
        }

        /// <summary> 활성화된 레벨에 맞춰 Image_CurrentSituation 하위의 Panel_Level(N)만 표시함. </summary>
        private void SetupSituationPanel()
        {
            if (situationPanels == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[GameFlowController] situationPanels가 null이라 현재 상황 화면을 레벨에 맞출 수 없음.");
                return;
            }

            int index = _selectedLevel - 1;
            for (int i = 0; i < situationPanels.Length; i++)
            {
                if (situationPanels[i]) situationPanels[i].SetActive(i == index);
                else if (_logger != null) _logger.ZLogWarning($"[GameFlowController] situationPanels[{i}]가 null이라 활성 상태를 바꿀 수 없음.");
            }

            if ((index < 0 || index >= situationPanels.Length) && _logger != null)
                _logger.ZLogWarning($"[GameFlowController] situationPanels가 {situationPanels.Length}개뿐이라 레벨{_selectedLevel} 현재 상황 화면을 보여 줄 수 없음.");
        }

        /// <summary> Addressables에서 활성화된 레벨의 스토리 이미지를 비동기로 불러와 적용함. </summary>
        private async UniTaskVoid LoadStoryImageAsync()
        {
            if (!storyImage)
            {
                if (_logger != null) _logger.ZLogWarning($"[GameFlowController] storyImage가 null이라 스토리 이미지를 불러오지 않음.");
                return;
            }

            string key = $"Level{_selectedLevel}";
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                _storyImageHandle = Addressables.LoadAssetAsync<Sprite>(key);
                Sprite sprite = await _storyImageHandle.Task.AsUniTask().AttachExternalCancellation(token);

                if (_storyImageHandle.Status == AsyncOperationStatus.Succeeded && sprite)
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
            float duration = _commonSettings.panelFadeDuration;
            try
            {
                if (storyPanel)
                {
                    await PanelFader.FadeAsync(storyPanel, 1f, 0f, duration, _logger, token);
                    PanelFader.ApplyState(storyPanel, false, _logger);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[GameFlowController] storyPanel이 null이라 패널 전환 연출을 건너뜀.");
                }

                if (gamePanel)
                {
                    await PanelFader.FadeAsync(gamePanel, 0f, 1f, duration, _logger, token);
                    PanelFader.ApplyState(gamePanel, true, _logger);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[GameFlowController] gamePanel이 null이라 패널 전환 연출을 건너뜀.");
                }
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> 스토리 버튼(미션 다시 보기) 클릭 시 미션 다시 보기 효과음을 내고 게임에서 스토리 패널로 되돌아감. </summary>
        private void OnStoryClicked()
        {
            if (_isBusy) return;
            SoundEffects.Play(_soundManager, Constants.Sounds.HintEpisode, _logger);
            SwitchToStoryAsync().Forget();
        }

        /// <summary> 게임 패널을 페이드아웃한 뒤 스토리 패널을 페이드인하는 크로스페이드. </summary>
        private async UniTaskVoid SwitchToStoryAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float duration = _commonSettings.panelFadeDuration;
            try
            {
                if (gamePanel)
                {
                    await PanelFader.FadeAsync(gamePanel, 1f, 0f, duration, _logger, token);
                    PanelFader.ApplyState(gamePanel, false, _logger);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[GameFlowController] gamePanel이 null이라 패널 전환 연출을 건너뜀.");
                }

                if (storyPanel)
                {
                    ApplyStoryText();
                    await PanelFader.FadeAsync(storyPanel, 0f, 1f, duration, _logger, token);
                    PanelFader.ApplyState(storyPanel, true, _logger);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[GameFlowController] storyPanel이 null이라 패널 전환 연출을 건너뜀.");
                }
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }
    }
}
