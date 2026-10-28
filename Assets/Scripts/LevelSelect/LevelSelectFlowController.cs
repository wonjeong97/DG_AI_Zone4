using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HuliacDev.Core;
using HuliacDev.UI;
using HuliacDev.Utils;
using ZLogger;

namespace DGAIZone.LevelSelect
{
    /// <summary>
    /// 레벨 선택 씬의 화면 흐름 제어. 모든 레벨 버튼은 처음부터 누를 수 있고, 결과 화면까지 간 레벨(QR 체험자의 서버 기록 포함, CompletedLevelStore)의
    /// 버튼에만 V를 켬. 레벨 버튼을 누르면 레벨 선택 패널을 페이드아웃한 뒤 스토리 패널을 페이드인함. 이때 선택한 버튼을 Background로 옮겨
    /// 목표 위치·크기로 튀어 들어오도록(OutBack) 이동시키고, 해당 레벨의 스토리 오브젝트만 활성화함.
    /// 화면 좌상단 &lt; 버튼은 레벨 선택 화면이면 인트로로, 스토리 화면이면 레벨 선택 화면으로(관리자 레벨 이동이면 타이틀의 관리자 화면으로) 돌아감.
    /// 타이틀 관리자 화면의 레벨 이동으로 들어오면(AdminLevelJumpStore) 씬 전환이 끝난 뒤 그 레벨 버튼을 고른 것처럼 스토리를 바로 띄움.
    /// </summary>
    public class LevelSelectFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup levelSelectPanel;
        [SerializeField] private CanvasGroup storyPanel;
        [SerializeField] private Button[] levelButtons;      // Button_Level1..5 순서
        [SerializeField] private GameObject[] storyLevels;   // Story_Level1..5 순서
        [SerializeField] private Image storyImage;           // Image_Story
        [SerializeField] private Button startButton;         // Button_Start (타이핑 완료 전까지 비활성)
        [Tooltip("levelButtons와 같은 순서로, 결과 화면까지 간 레벨 버튼에 켤 V 표시(Button_LevelN/Image_Complete)")]
        [SerializeField] private GameObject[] levelCompleteMarks;
        [Tooltip("화면 좌상단 < 버튼 — 레벨 선택 화면이면 인트로로, 스토리 화면이면 레벨 선택 화면으로(관리자 레벨 이동이면 타이틀의 관리자 화면으로) 돌아감")]
        [SerializeField] private Button backButton;
        [SerializeField] private LevelData[] levelDataList;  // Level1..5 순서, 3_Game(스토리 다시보기)과 공유하는 스토리 텍스트 소스
        [Header("Selected Level Button Move")]
        [SerializeField] private RectTransform selectedLevelButtonParent; // 선택된 버튼이 이동해 들어갈 부모(Background)
        [Header("Theme Background")]
        [SerializeField] private Image themeBackgroundImage; // Background/ThemeBackground: 평소엔 투명, 레벨 선택 시 테마 스프라이트로 페이드인됨
        [SerializeField] private Sprite[] themeBackgroundSprites; // Level1..5 순서. 레벨 1·2는 같은 스프라이트(Background_1)를 지정하면 됨

        private static readonly ProfilerMarker WarmUpThemeBackgroundMarker = new ProfilerMarker("LevelSelectFlowController.WarmUpThemeBackgroundSprites");

        private SceneTransitionService _sceneTransition;
        private SelectedLevelStore _selectedLevelStore;
        private CompletedLevelStore _completedLevelStore;
        private ILogger<LevelSelectFlowController> _logger;
        private InactivityTimer _inactivityTimer;
        private VisitorInfoProvider _visitorInfoProvider;
        private SoundManager _soundManager;
        private AdminLevelJumpStore _levelJumpStore;
        private CanvasGroup _themeBackgroundCanvasGroup;
        private bool _isBusy;
        private bool _isLevelSelected; // 레벨을 이미 골랐는지(관리자 레벨 이동이 씬 전환을 기다리는 사이 버튼으로 먼저 고른 경우 다시 고르지 않음)
        private bool _isLeaving; // 뒤로 버튼으로 이 씬을 떠나는 중인지(연타해도 한 번만 처리)

        // 2_LevelSelect.json / 00_Common.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private LevelSelectSceneSettings _sceneSettings = new LevelSelectSceneSettings();
        private CommonSettings _commonSettings = new CommonSettings();

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 선택된 레벨 저장소, 완료 레벨 저장소, 로거, 체험자 정보 제공자, 비활동 타이머, 효과음 매니저, 관리자 레벨 이동 저장소를 할당함. </summary>
        [Inject]
        public void Construct(
            SceneTransitionService sceneTransition,
            SelectedLevelStore selectedLevelStore,
            CompletedLevelStore completedLevelStore,
            ILogger<LevelSelectFlowController> logger,
            VisitorInfoProvider visitorInfoProvider = null,
            InactivityTimer inactivityTimer = null,
            SoundManager soundManager = null,
            AdminLevelJumpStore levelJumpStore = null)
        {
            _sceneTransition = sceneTransition;
            _selectedLevelStore = selectedLevelStore;
            _completedLevelStore = completedLevelStore;
            _logger = logger;
            _visitorInfoProvider = visitorInfoProvider;
            _inactivityTimer = inactivityTimer;
            _soundManager = soundManager;
            _levelJumpStore = levelJumpStore;
        }

        /// <summary> 유닛 테스트용: 씬 없이 레벨 버튼과 V 표시 목록을 직접 지정함. </summary>
        internal void SetLevelButtonsForTest(Button[] buttons, GameObject[] completeMarks = null)
        {
            levelButtons = buttons;
            levelCompleteMarks = completeMarks;
        }

        /// <summary> 초기 패널 상태를 적용하고 레벨 버튼 클릭 이벤트·V 표시·뒤로 버튼을 설정한 뒤, 2_LevelSelect.json/00_Common.json 연출 타이밍을 비동기로 불러옴. </summary>
        private void Start()
        {
            // DOTween은 씬에서 처음 쓰이는 트윈이 자기 자신을 초기화하는 비용까지 그 자리에서 치르므로, 2_LevelSelect를
            // (이전 씬을 거치지 않고) 단독으로 바로 실행해 테스트할 때 레벨 버튼 클릭이 그 세션의 첫 트윈이 되면서
            // 그 프레임에 히치(순간 멈춤)가 생기고 실제 트윈 이동이 순간이동한 것처럼 보일 수 있어 미리 초기화해둠.
            DOTween.Init();

            PanelFader.ApplyState(levelSelectPanel, true, _logger);
            PanelFader.ApplyState(storyPanel, false, _logger);

            // 선택된 레벨 버튼이 날아와서 표시되므로 스토리 이미지 플레이스홀더는 숨겨둠
            if (storyImage) storyImage.gameObject.SetActive(false);
            else if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] storyImage가 null이라 스토리 이미지 플레이스홀더를 숨길 수 없음.");

            // 테마 배경은 평소엔 투명 상태로 시작하고, 레벨 버튼 클릭 시에만 해당 스프라이트로 페이드인됨
            EnsureThemeBackgroundCanvasGroup();
            using (WarmUpThemeBackgroundMarker.Auto())
            {
                WarmUpThemeBackgroundSprites();
            }

            // 시작 버튼은 스토리 타이핑이 끝나기 전까지 누를 수 없음
            if (startButton)
            {
                startButton.interactable = false;
                startButton.onClick.AddListener(OnStartClicked);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[LevelSelectFlowController] startButton이 null이라 게임 시작 버튼을 연결할 수 없음.");
            }

            if (backButton) backButton.onClick.AddListener(OnBackClicked);
            else if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] backButton이 null이라 뒤로 버튼을 연결할 수 없음.");

            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();

            if (levelButtons == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] levelButtons가 null이라 설정할 버튼이 없음.");
                return;
            }

            for (int i = 0; i < levelButtons.Length; i++)
            {
                Button button = levelButtons[i];
                if (!button)
                {
                    if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] levelButtons[{i}]가 null임.");
                    continue;
                }

                int index = i;
                button.onClick.AddListener(() => OnLevelClicked(index));
            }

            ApplyCompleteMarks();
        }

        /// <summary> 결과 화면까지 간 레벨(QR 체험자의 서버 기록 포함)의 버튼에만 V를 켬. </summary>
        private void ApplyCompleteMarks()
        {
            if (levelCompleteMarks == null || levelCompleteMarks.Length < levelButtons.Length)
            {
                if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] levelCompleteMarks가 레벨 버튼 수({levelButtons.Length}개)만큼 할당되지 않아 일부 레벨의 V 표시를 켜지 못함.");
                if (levelCompleteMarks == null) return;
            }

            if (_completedLevelStore == null && _logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] completedLevelStore가 null이라 V 표시를 모두 끔.");

            for (int i = 0; i < levelCompleteMarks.Length; i++)
            {
                if (levelCompleteMarks[i]) levelCompleteMarks[i].SetActive(_completedLevelStore != null && _completedLevelStore.IsCompleted(i + 1));
                else if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] levelCompleteMarks[{i}]가 null이라 레벨{i + 1} V 표시를 바꿀 수 없음.");
            }
        }

        /// <summary>
        /// 2_LevelSelect.json(LevelSelectSceneSettings)과 00_Common.json(CommonSettings)을 비동기로 로드하고, 관리자 레벨 이동이면 그 레벨을 고름.
        /// </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.LevelSelect}";
            UniTask<LevelSelectSceneSettings> settingsTask = JsonLoader.LoadAsync<LevelSelectSceneSettings>(path, token, _logger);
            UniTask<CommonSettings> commonTask = CommonSettingsProvider.GetAsync(token);

            (_sceneSettings, _commonSettings) = await UniTask.WhenAll(settingsTask, commonTask);

            if (_levelJumpStore != null && _levelJumpStore.TryTakePendingStoryLevel(out int level))
                await SelectAdminJumpLevelAsync(level, token);

            if (token.IsCancellationRequested) return; // 관리자 레벨 선택을 기다리다 씬을 떠난 경우(정상) — 끝난 것으로 표시하지 않음
            IsSceneSettingsLoaded = true;
        }

        /// <summary> 설정을 불러와(관리자 레벨 이동이면 그 레벨까지 고른 뒤) 끝났는지. 테스트가 실시간 대기 대신 이 시점을 기다림. </summary>
        internal bool IsSceneSettingsLoaded { get; private set; }

        /// <summary>
        /// 관리자 레벨 이동으로 들어온 레벨(1부터)을, 씬 전환 페이드인이 끝나면 그 레벨 버튼을 고른 것처럼 선택해 스토리를 띄움.
        /// </summary>
        private async UniTask SelectAdminJumpLevelAsync(int level, CancellationToken token)
        {
            try
            {
                if (_sceneTransition != null) await _sceneTransition.WaitUntilIdleAsync(token);
                else if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] sceneTransition이 null이라 씬 전환이 끝나기를 기다리지 않고 관리자 레벨 이동 레벨을 고름.");
            }
            catch (OperationCanceledException)
            {
                return; // 기다리는 도중 씬을 떠난 경우 — 정상 종료
            }

            if (_isLevelSelected || _isLeaving)
            {
                if (_logger != null) _logger.ZLogInformation($"[LevelSelectFlowController] 씬 전환 중에 이미 레벨을 골랐거나 뒤로를 눌러 관리자 레벨 이동 레벨{level}은 고르지 않음.");
                return;
            }

            int index = level - 1;
            if (levelButtons == null || index < 0 || index >= levelButtons.Length)
            {
                if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] 관리자 레벨 이동 레벨{level}에 맞는 레벨 버튼이 없어 고르지 못함(버튼 {levelButtons?.Length ?? 0}개).");
                return;
            }

            if (_logger != null) _logger.ZLogInformation($"[LevelSelectFlowController] 관리자 레벨 이동으로 레벨{level} 스토리를 바로 띄움.");
            SelectLevel(index);
        }

        /// <summary> 버튼 리스너를 해제함. </summary>
        private void OnDestroy()
        {
            if (startButton) startButton.onClick.RemoveListener(OnStartClicked);
            if (backButton) backButton.onClick.RemoveListener(OnBackClicked);

            if (levelButtons == null) return;
            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (levelButtons[i]) levelButtons[i].onClick.RemoveAllListeners();
            }
        }

        /// <summary> 시작 버튼 클릭 시 클릭음을 내고 화면 페이드와 함께 게임 씬으로 전환함. </summary>
        private void OnStartClicked()
        {
            if (_isBusy || _isLeaving) return; // 이미 시작했거나 뒤로를 눌러 떠나는 중(연타) — 정상이라 로그 없음

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[LevelSelectFlowController] sceneTransition이 null이라 {Constants.Scenes.Game} 씬을 로드할 수 없음.");
                return;
            }

            // 홈 버튼으로 이미 타이틀로 가는 중이면 받지 않음(시작 로그·효과음이 함께 나가지 않게)
            if (_sceneTransition.IsTransitioning)
            {
                if (_logger != null) _logger.ZLogInformation($"[LevelSelectFlowController] 이미 다른 화면으로 넘어가는 중이라 시작 버튼을 무시함.");
                return;
            }

            _isBusy = true;
            if (_logger != null) _logger.ZLogInformation($"[LevelSelectFlowController] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 스토리를 보고 시작을 누름.");
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Game, _commonSettings.sceneTransitionFadeDuration).Forget();
        }

        /// <summary>
        /// &lt; 버튼 — 관리자 레벨 이동으로 들어온 판이면(그 레벨을 자동으로 고르기 전이어도) 타이틀의 관리자 화면으로, 레벨을 고르기 전(레벨 선택 화면)이면 인트로로,
        /// 스토리 화면이면 이 씬을 다시 불러 레벨 선택 화면으로 돌아감(고른 버튼을 옮기고 별·V를 숨긴 연출을 하나씩 되돌리지 않기 위해).
        /// </summary>
        private void OnBackClicked()
        {
            if (_isLeaving) return; // 이미 눌러 떠나는 중(연타) — 정상이라 로그 없음

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[LevelSelectFlowController] sceneTransition이 null이라 뒤로 갈 씬을 로드할 수 없음.");
                return;
            }

            // 홈·시작 버튼으로 이미 다른 씬으로 가는 중이면 받지 않음
            if (_sceneTransition.IsTransitioning)
            {
                if (_logger != null) _logger.ZLogInformation($"[LevelSelectFlowController] 이미 다른 화면으로 넘어가는 중이라 뒤로 버튼을 무시함.");
                return;
            }

            if (_levelJumpStore == null && _logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] levelJumpStore가 null이라 관리자 레벨 이동 판인지 알 수 없어 일반 판처럼 돌아감.");

            string nextScene;
            if (_levelJumpStore != null && _levelJumpStore.IsLevelJump)
            {
                nextScene = Constants.Scenes.Title;
                _levelJumpStore.OpenAdminOnTitle = true;
                if (_logger != null) _logger.ZLogInformation($"[LevelSelectFlowController] 관리자 레벨 이동 판에서 뒤로를 누름 — 타이틀 관리자 화면으로 돌아감.");
            }
            else if (!_isLevelSelected)
            {
                nextScene = Constants.Scenes.Intro;
                if (_logger != null) _logger.ZLogInformation($"[LevelSelectFlowController] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 레벨 선택 화면에서 뒤로를 누름 — 인트로로 돌아감.");
            }
            else
            {
                nextScene = Constants.Scenes.LevelSelect;
                if (_logger != null) _logger.ZLogInformation($"[LevelSelectFlowController] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 스토리 화면에서 뒤로를 누름 — 레벨 선택 화면으로 돌아감.");
            }

            _isLeaving = true;
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            _sceneTransition.LoadSceneWithFadeAsync(nextScene, _commonSettings.sceneTransitionFadeDuration).Forget();
        }

        /// <summary> 레벨 버튼 클릭 시 클릭음을 내고 그 레벨을 고름. </summary>
        private void OnLevelClicked(int index)
        {
            if (_isBusy || _isLevelSelected || _isLeaving) return; // 이미 레벨을 골랐거나 뒤로를 눌러 떠나는 중(연타) — 정상이라 로그 없음

            // 홈 버튼으로 이미 타이틀로 가는 중이면 고르지 않음
            if (_sceneTransition != null && _sceneTransition.IsTransitioning)
            {
                if (_logger != null) _logger.ZLogInformation($"[LevelSelectFlowController] 이미 다른 화면으로 넘어가는 중이라 {index + 1}레벨 버튼을 무시함.");
                return;
            }

            if (_logger != null) _logger.ZLogInformation($"[LevelSelectFlowController] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} {index + 1}레벨을 고름.");
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            SelectLevel(index);
        }

        /// <summary> 선택한 버튼을 분리해 스토리 영역으로 트윈 이동시키고 패널을 전환함 (Zone1과 동일한 연출). 버튼 클릭과 관리자 레벨 이동이 함께 씀. </summary>
        private void SelectLevel(int index)
        {
            _isLevelSelected = true;

            if (_selectedLevelStore != null)
            {
                _selectedLevelStore.SelectedLevel = index + 1;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[LevelSelectFlowController] selectedLevelStore가 null이라 선택한 레벨을 기록할 수 없음.");
            }

            if (startButton) startButton.interactable = false;

            ApplyThemeBackground(index);

            // 선택한 레벨 버튼을 클릭 즉시 두 패널(levelSelectPanel·storyPanel) 바깥의 Background로 완전히 옮김.
            // 두 패널 모두 CanvasGroup으로 페이드되는데, 그 자식으로 두면 페이드 도중 알파 블렌딩 때문에
            // 이미지가 흐릿하게 보여서, 페이드에 영향받지 않는 위치로 미리 빼둔다.
            // LevelSelectPanel의 실제 상위 부모(Image_Window3)는 전체화면 스트레치가 아니라 고정 크기/오프셋이 있는
            // 박스라 Background(전체화면 기준)와 좌표계가 다름. worldPositionStays: true로 옮겨서 화면상 실제 위치를
            // 그대로 유지한 채(유니티가 새 부모 기준으로 anchoredPosition을 재계산함) 그 위치에서 목표 위치로 트윈함.
            RectTransform selectedButtonRect = null;
            if (levelButtons != null && index < levelButtons.Length && levelButtons[index])
            {
                selectedButtonRect = (RectTransform)levelButtons[index].transform;

                // selectedLevelButtonParent가 인스펙터에 할당되지 않았으면 씬 루트(부모 없음)로 빠져 캔버스 밖으로
                // 이탈할 수 있으므로, storyPanel의 부모를 폴백으로 사용함.
                // 둘 다 없으면 현재 부모에 그대로 둠(패널과 함께 흐려지지만 화면 밖으로 빠지지는 않음).
                Transform targetParent = selectedLevelButtonParent;
                if (!targetParent)
                {
                    targetParent = storyPanel ? storyPanel.transform.parent : selectedButtonRect.parent;
                    if (_logger != null)
                    {
                        _logger.ZLogWarning($"[LevelSelectFlowController] selectedLevelButtonParent가 null이라 {(storyPanel ? "storyPanel의 부모" : "버튼의 현재 부모")}로 대체함.");
                    }
                }

                selectedButtonRect.SetParent(targetParent, worldPositionStays: true);
                levelButtons[index].interactable = false;

                // 버튼에 달려있던 별(Image_StarN)·V(Image_Complete) 아이콘은 스토리 패널로 넘어갈 땐 필요 없으므로 숨김
                foreach (Transform child in selectedButtonRect)
                {
                    child.gameObject.SetActive(false);
                }
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[LevelSelectFlowController] levelButtons[{index}]가 null이라 선택한 레벨 버튼 이동 연출을 건너뜀.");
            }

            TMP_Text storyText = null;
            string storyTemplate = null;
            if (storyLevels != null)
            {
                for (int i = 0; i < storyLevels.Length; i++)
                {
                    if (storyLevels[i]) storyLevels[i].SetActive(i == index);
                    else if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] storyLevels[{i}]가 null이라 활성 상태를 바꿀 수 없음.");
                }

                if (index < storyLevels.Length && storyLevels[index])
                {
                    if (ChildComponentFinder.TryGetInDirectChildren(storyLevels[index].transform, out storyText))
                    {
                        // levelDataList(LevelData 에셋)에서 스토리 텍스트를 가져옴 — 3_Game(스토리 다시보기)과 같은 에셋을 참조하므로
                        // 텍스트를 한 곳만 고치면 두 씬 모두에 반영됨. 할당되지 않았으면 씬에 미리 입력된 텍스트를 그대로 유지함.
                        // {name}은 SwitchToStoryAsync가 체험자 이름을 받은 뒤 이 원본으로 치환함.
                        if (levelDataList != null && index < levelDataList.Length && levelDataList[index])
                        {
                            storyTemplate = levelDataList[index].storyText;
                        }
                        else
                        {
                            storyTemplate = storyText.text;
                            if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] levelDataList[{index}]가 비어 있어 씬에 입력된 텍스트를 그대로 사용함.");
                        }

                        // 페이드인 도중 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠
                        storyText.maxVisibleCharacters = 0;
                    }
                    else if (_logger != null)
                    {
                        _logger.ZLogWarning($"[LevelSelectFlowController] {storyLevels[index].name}의 직계 자식에 TMP_Text가 없어 스토리 텍스트를 표시할 수 없음.");
                    }
                }
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[LevelSelectFlowController] storyLevels가 null이라 고른 레벨의 스토리를 보여 줄 수 없음.");
            }

            SwitchToStoryAsync(storyText, storyTemplate, selectedButtonRect).Forget();
        }

        /// <summary>
        /// 레벨 선택 패널을 페이드아웃한 뒤 스토리 패널을 페이드인하고, 선택된 버튼을 목표 위치로 이동시키며, 체험자 이름 로드를 기다려
        /// storyTemplate의 {name}을 치환한 스토리 텍스트 연출이 끝나면 시작 버튼을 활성화함.
        /// </summary>
        private async UniTaskVoid SwitchToStoryAsync(TMP_Text storyText, string storyTemplate, RectTransform selectedButtonRect)
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float duration = _sceneSettings.panelFadeDuration;
            try
            {
                if (levelSelectPanel && levelSelectPanel.gameObject.activeInHierarchy)
                {
                    await PanelFader.FadeAsync(levelSelectPanel, 1f, 0f, duration, _logger, token);
                    PanelFader.ApplyState(levelSelectPanel, false, _logger);
                }
                else if (!levelSelectPanel && _logger != null)
                {
                    _logger.ZLogWarning($"[LevelSelectFlowController] levelSelectPanel이 null이라 레벨 선택 패널을 숨기지 못함.");
                }

                if (storyPanel)
                {
                    // storyPanel이 페이드인되는 동안 선택된 레벨 버튼도 함께 목표 위치·크기로 튀어 들어오도록(OutBack) 이동.
                    // 목표 위치·크기, 이동 시간·반동 크기는 2_LevelSelect.json(selectedLevelButtonTargetPosition/TargetSize/
                    // MoveDuration/MoveOvershoot)으로 재빌드 없이 조정 가능. 페이드와 동시에 진행되어야 하므로 의도적으로 await하지 않는다.
                    if (selectedButtonRect)
                    {
                        Vector2 targetPos = _sceneSettings.selectedLevelButtonTargetPosition;
                        Vector2 targetSize = _sceneSettings.selectedLevelButtonTargetSize;
                        float moveDuration = _sceneSettings.selectedLevelButtonMoveDuration;
                        float overshoot = _sceneSettings.selectedLevelButtonMoveOvershoot;

                        _ = selectedButtonRect.DOAnchorPos(targetPos, moveDuration)
                            .SetEase(Ease.OutBack, overshoot)
                            .SetUpdate(true)
                            .SetLink(selectedButtonRect.gameObject);

                        _ = selectedButtonRect.DOSizeDelta(targetSize, moveDuration)
                            .SetEase(Ease.OutBack, overshoot)
                            .SetUpdate(true)
                            .SetLink(selectedButtonRect.gameObject);
                    }

                    await PanelFader.FadeAsync(storyPanel, 0f, 1f, duration, _logger, token);
                    PanelFader.ApplyState(storyPanel, true, _logger);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[LevelSelectFlowController] storyPanel이 null이라 패널 전환 연출을 건너뜀.");
                }

                if (storyText)
                {
                    string visitorName = await GetVisitorNameAsync(token);
                    storyText.text = PlaceholderFormatter.ReplaceVisitorName(storyTemplate, visitorName);
                    storyText.ForceMeshUpdate();
                }

                await StoryLineAnimator.AnimateAsync(storyText,
                    _commonSettings.storyLineMoveDuration,
                    _commonSettings.storyLineInterval,
                    _commonSettings.storyLineYOffset,
                    StoryLineAnimator.IsPointerPressedThisFrame, token, _inactivityTimer);

                // 스토리를 넘긴 탭을 시작 버튼 위에서 떼면 그대로 게임이 시작되므로(버튼은 뗄 때 눌림을 판정함), 손을 뗀 그 프레임이 끝난 뒤에 켬
                await UniTask.WaitUntil(static () => !StoryLineAnimator.IsPointerHeld(), cancellationToken: token);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);

                if (startButton) startButton.interactable = true;
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> 체험자 이름을 불러옴(관리자 화면 이름, 서버 모드면 QR로 확인한 이름). 제공자가 없으면 기본 이름을 씀. </summary>
        private UniTask<string> GetVisitorNameAsync(CancellationToken token)
        {
            if (_visitorInfoProvider != null) return _visitorInfoProvider.GetNameAsync(token);

            if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] visitorInfoProvider가 null이라 스토리 텍스트에 기본 이름 '{Constants.DefaultVisitorName}'을 사용함.");
            return UniTask.FromResult(Constants.DefaultVisitorName);
        }

        /// <summary>
        /// themeBackgroundSprites를 씬 시작 시 한 번씩 themeBackgroundImage에 대입해 텍스처 업로드/머티리얼 준비 비용을
        /// 미리 치러둠. 이렇게 하지 않으면 레벨 버튼 클릭 시 처음으로 큰 배경 스프라이트가 대입되면서 그 프레임에
        /// 히치(순간 멈춤)가 발생하고, 그 사이 실제 시간이 크게 흘러 버튼 이동 트윈이 순간이동한 것처럼 보임
        /// (SetUpdate(true)라 unscaledDeltaTime을 그대로 따라가므로, 히치 프레임의 큰 델타를 그대로 반영함).
        /// 알파는 이미 0으로 맞춰둔 상태라 화면에는 아무 변화도 보이지 않음.
        /// </summary>
        private void WarmUpThemeBackgroundSprites()
        {
            if (!themeBackgroundImage || themeBackgroundSprites == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] themeBackgroundImage 또는 themeBackgroundSprites가 비어 있어 배경 워밍업을 건너뜀.");
                return;
            }

            Sprite originalSprite = themeBackgroundImage.sprite;
            foreach (Sprite sprite in themeBackgroundSprites)
            {
                if (!sprite)
                {
                    if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] themeBackgroundSprites에 비어 있는 항목이 있어 워밍업에서 제외함.");
                    continue;
                }
                themeBackgroundImage.sprite = sprite;
                Canvas.ForceUpdateCanvases();
            }
            themeBackgroundImage.sprite = originalSprite;
        }

        /// <summary>
        /// 선택된 레벨(index)에 맞는 테마 배경 스프라이트로 즉시 교체한 뒤 페이드인함. 다른 연출(패널 전환, 버튼 이동)과
        /// 동시에 진행되면 되므로 의도적으로 await하지 않음. themeBackgroundSprites[index]가 비어 있으면 건너뜀.
        /// </summary>
        private void ApplyThemeBackground(int index)
        {
            if (!themeBackgroundImage)
            {
                if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] themeBackgroundImage가 null이라 테마 배경을 바꿀 수 없음.");
                return;
            }

            Sprite sprite = (themeBackgroundSprites != null && index >= 0 && index < themeBackgroundSprites.Length)
                ? themeBackgroundSprites[index]
                : null;

            if (!sprite)
            {
                if (_logger != null) _logger.ZLogWarning($"[LevelSelectFlowController] themeBackgroundSprites[{index}]가 비어 있어 테마 배경을 바꾸지 못함.");
                return;
            }

            themeBackgroundImage.sprite = sprite;

            EnsureThemeBackgroundCanvasGroup();
            if (_themeBackgroundCanvasGroup)
            {
                _themeBackgroundCanvasGroup.alpha = 0f;
                float duration = _sceneSettings.themeBackgroundFadeDuration;
                _ = _themeBackgroundCanvasGroup.DOFade(1f, duration)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true)
                    .SetLink(_themeBackgroundCanvasGroup.gameObject);
            }
        }

        /// <summary> themeBackgroundImage에 CanvasGroup이 없으면 추가하고 초기화함. </summary>
        private void EnsureThemeBackgroundCanvasGroup()
        {
            if (!themeBackgroundImage) return;
            if (!_themeBackgroundCanvasGroup && !themeBackgroundImage.TryGetComponent(out _themeBackgroundCanvasGroup))
            {
                _themeBackgroundCanvasGroup = themeBackgroundImage.gameObject.AddComponent<CanvasGroup>();
                _themeBackgroundCanvasGroup.alpha = 0f;
            }
        }
    }
}
