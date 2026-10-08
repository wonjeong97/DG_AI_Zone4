using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using MessagePipe;
using Microsoft.Extensions.Logging;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer;
using HuliacDev.Core;
using HuliacDev.UI;
using HuliacDev.Utils;
using DGAIZone.Game.UI.States;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 2_Game 씬의 재료/물질 선택 및 다중 RFID 순차 워크플로우를 제어하는 컨트롤러.
    /// </summary>
    public class IngredientSelectionController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text textIngredient; // Text_Material
        [SerializeField] private TMP_Text textMatter; // Text_Matter
        [SerializeField] private Button buttonLeft;
        [SerializeField] private Button buttonRight;

        [Header("Workflow Buttons")]
        [SerializeField] private Button buttonConfirm;
        [SerializeField] private Button buttonCancel;
        [SerializeField] private Button buttonCodingComplete;
        [SerializeField] private Button buttonSkip;

        [Header("Level 2 Step Balls")]
        [SerializeField] private Image[] stepBallImages; // Image_Step1_Ball..Image_Step5_Ball 순서 (Panel_Level2 하위)
        [SerializeField] private Material stepBallGrayscaleMaterial; // 미완료 상태 흑백 머티리얼

        [Header("Level 2 Progress Bar")]
        [SerializeField] private Image level2FillImage; // Panel_Level2/Image_Bar/Image_Fill

        [Header("Level 3 Gauges")]
        [SerializeField] private Image level3OxygenGauge;   // Panel_Level3/Group_O2/Image_CircleGage
        [SerializeField] private Image level3ElectricGauge; // Panel_Level3/Group_Electric/Image_CircleGage
        [SerializeField] private Image level3OxygenIcon;    // Panel_Level3/Group_O2/Image_Icon
        [SerializeField] private Image level3ElectricIcon;  // Panel_Level3/Group_Electric/Image_Icon

        [Header("Activation")]
        [SerializeField] private CanvasGroup gamePanel; // 게임 패널이 활성(상호작용 가능)일 때만 RFID를 처리함

        [Header("Level 4 Board")]
        [SerializeField] private Level4BoardController level4Board; // Panel_Level4. Level4BoardController가 이 클래스를 주입받는 순환 의존이라 VContainer 대신 인스펙터로 연결함
        [SerializeField] private Level5CityView level5City; // Panel_Level5. 함수 정의 블록 안쪽 동작 블록마다 맞는 그림을 보여 줌

        private StateMachine<IngredientSelectionController> _stateMachine;
        private IngredientLevel1State _level1State;
        private IngredientLevel2State _level2State;
        private IngredientLevel3State _level3State;
        private IngredientLevel4State _level4State;
        private IngredientLevel5State _level5State;

        /// <summary> 현재 활성화된 레벨 상태 객체. </summary>
        public IIngredientSelectionLevelState CurrentLevelState => _stateMachine?.CurrentState as IIngredientSelectionLevelState;

        internal Image[] StepBallImages => stepBallImages;
        internal Material StepBallGrayscaleMaterial => stepBallGrayscaleMaterial;
        internal Image Level2FillImage => level2FillImage;
        internal float Level2FillTweenDuration => _sceneSettings.level2FillTweenDuration;
        internal float Level2FillOvershoot => _sceneSettings.level2FillOvershoot;

        internal Image Level3OxygenGauge => level3OxygenGauge;
        internal Image Level3ElectricGauge => level3ElectricGauge;
        internal CanvasGroup Level3OxygenIconCanvasGroup => _level3OxygenIconCanvasGroup;
        internal CanvasGroup Level3ElectricIconCanvasGroup => _level3ElectricIconCanvasGroup;
        internal float Level3GaugeTweenDuration => _sceneSettings.level3GaugeTweenDuration;
        internal float Level3IconBlinkMinAlpha => _sceneSettings.level3IconBlinkMinAlpha;
        internal float Level3IconBlinkDuration => _sceneSettings.level3IconBlinkDuration;

        internal Level4BoardController Level4Board => level4Board;
        internal Level5CityView Level5City => level5City;
        internal float DesignAttachDuration => _designPanel ? _designPanel.AttachDuration : 0f;   // 설정하기로 블록이 다 붙기까지(초). 설계창이 없으면 0
        internal float DesignRestoreDuration => _designPanel ? _designPanel.RestoreDuration : 0f; // 떨어졌던 블록이 다시 다 붙기까지(초). 설계창이 없으면 0
        internal MissionBoardController MissionBoard => _missionBoard;
        internal ILogger<IngredientSelectionController> Logger => _logger;
        internal RfidMatter[] ConfirmedMatters => _confirmedMatters;
        internal string[] ConfirmedIngredients => _confirmedIngredients;
        internal RfidLevelMapping LevelMapping => _levelMapping;
        internal RfidStepDefinition[] StepDefinitions => _stepDefinitions;
        internal RfidStepDefinition[] CategoryIngredients => _categoryIngredients;
        internal int CurrentStepIndex => _currentStepIndex;
        internal int TotalSteps => _totalSteps;
        internal string CurrentIngredientId => _currentIngredientId;

        private ISubscriber<RfidTagEvent> _subscriber;
        private ISubscriber<RfidReaderIdleEvent> _idleSubscriber;
        private SelectedLevelStore _selectedLevelStore;
        private SceneTransitionService _sceneTransition;
        private MissionBoardController _missionBoard;
        private CodingCategoryIndicatorController _codingCategoryIndicator;
        private GameResultStore _resultStore;
        private RightArrowHint _rightArrowHint;
        private InvalidCardWarning _invalidCardWarning;
        private DesignPanel _designPanel;
        private ILogger<IngredientSelectionController> _logger;
        private SoundManager _soundManager;
        private VisitorInfoProvider _visitorInfoProvider;
        private bool _isBusy;

        private int _selectedLevel = 1;     // 현재 레벨 (디자인 항목 표시 형식 분기 등에 사용)
        private int _currentStepIndex = 0;  // 현재 read 인덱스 (0 ~ _totalSteps-1)
        private int _totalSteps = 3;        // 현재 스테이지에서 찍어야 하는 총 read 횟수
        private const int DefaultFallbackStepCount = 3; // 단계(steps) 정의가 없는 레벨인데 JSON stageReadCounts도 없을 때의 단계 수
        private RfidLevelMapping _levelMapping; // 현재 레벨의 블록 목록(matterSets)·단계 정의. 단계가 고를 블록은 matterSetId로 여기서 찾음
        private RfidStepDefinition[] _stepDefinitions; // "동작" 카드를 찍을 때마다 순서대로 진행되는 재료 목록 (추진체 종류 -> 탑재 종류 -> 연료량)
        private RfidStepDefinition[] _categoryIngredients; // 카드 분류로 재료가 정해지는 레벨(레벨 4)의 분류별 재료 정의
        private RfidMatter[] _confirmedMatters;
        private string[] _confirmedIngredients; // 각 스탭에서 확정된 재료의 ingredientId
        private string _currentIngredientId; // 현재 대기 중인(아직 확정 안 된) 재료의 ingredientId. 화면 표시용 이름은 _currentIngredient
        private string[] _confirmedCategories; // 각 스탭을 확정시킨 카드의 category(동작/제어/논리/함수). 리더기별 스탭 라우팅에서 카드 변경 감지에 사용
        private readonly List<DesignStepShape> _plannedDesignShapes = new List<DesignStepShape>(); // 이번 레벨 단계를 모두 쌓았을 때의 설계창 블록 모양(배율 계산용)
        private readonly List<DesignStep> _designSteps = new List<DesignStep>(); // 플레이어가 설계창에 쌓은 블록(결과 씬 '나의 코딩 결과'용)
        private string _currentCategory; // 현재 대기 중인(아직 확정 안 된) 태그의 category. Confirm 시 _confirmedCategories에 기록됨

        // 리더기별 스탭 라우팅: readerId("Reader_N")가 N번째 스탭에 고정 배정됨. 설정된 리더기가 1대뿐이면(현재)
        // 라우팅을 적용하지 않고 기존처럼 아무 리더기의 태그나 현재 스탭에 적용함. 2대 이상부터 활성화됨.
        private int _readerCount = 1;

        // 이미 확정된 스탭의 카드가 리더기에서 떨어진(RfidReaderIdleEvent) 스탭 인덱스 목록.
        // 여기 포함된 가장 이른 스탭부터 뒤쪽 설계창 블록을 임시로 떨어뜨리고, 하나라도 있으면 설정하기·코딩 완료를 막음.
        // 같은 category 카드가 다시 인식되면 해당 인덱스가 제거되고 블록이 다시 붙음.
        private readonly HashSet<int> _idleReaderStepIndices = new HashSet<int>();

        // R3 반응형 상태 관리
        private readonly ReactiveProperty<string> _currentIngredient = new ReactiveProperty<string>("");
        private RfidMatter[] _currentMatters = Array.Empty<RfidMatter>(); // 지금 단계에서 좌우 버튼으로 고를 블록 목록
        private readonly ReactiveProperty<int> _currentMatterIndex = new ReactiveProperty<int>(0);

        private R3.DisposableBag _disposables = new R3.DisposableBag();
        private CanvasGroup _level3OxygenIconCanvasGroup;
        private CanvasGroup _level3ElectricIconCanvasGroup;

        // 3_Game.json / 00_Common.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private GameSceneSettings _sceneSettings = new GameSceneSettings();
        private CommonSettings _commonSettings = new CommonSettings();

        /// <summary>
        /// 상태 머신과 레벨별 상태 인스턴스를 사전 생성하고(Zero-GC), 서브 캔버스 및 캔버스 그룹을 초기화함.
        /// </summary>
        private void Awake()
        {
            _level1State = new IngredientLevel1State();
            _level2State = new IngredientLevel2State();
            _level3State = new IngredientLevel3State();
            _level4State = new IngredientLevel4State();
            _level5State = new IngredientLevel5State();
            _stateMachine = new StateMachine<IngredientSelectionController>(this);

            InitSubCanvases();
            InitLevel3IconCanvasGroups();
        }

        /// <summary> fillAmount가 트윈될 때 메인 UI 캔버스의 리빌드를 차단하도록 서브 캔버스를 보장함. </summary>
        private void InitSubCanvases()
        {
            EnsureSubCanvas(level2FillImage);
            EnsureSubCanvas(level3OxygenGauge);
            EnsureSubCanvas(level3ElectricGauge);
        }

        /// <summary> target에 Canvas가 없으면 붙여 fillAmount 트윈이 메인 캔버스를 다시 만들지 않게 함. 참조가 없으면 넘김(그 이미지를 쓰는 곳에서 경고함). </summary>
        private static void EnsureSubCanvas(Component target)
        {
            if (!target) return;
            if (!target.TryGetComponent<Canvas>(out _))
            {
                target.gameObject.AddComponent<Canvas>();
            }
        }

        /// <summary> 레벨 3 아이콘 깜빡임이 정점 리빌드 없이 GPU 블렌딩을 사용하도록 CanvasGroup을 구성함. </summary>
        private void InitLevel3IconCanvasGroups()
        {
            if (level3OxygenIcon && !level3OxygenIcon.TryGetComponent(out _level3OxygenIconCanvasGroup))
            {
                _level3OxygenIconCanvasGroup = level3OxygenIcon.gameObject.AddComponent<CanvasGroup>();
            }

            if (level3ElectricIcon && !level3ElectricIcon.TryGetComponent(out _level3ElectricIconCanvasGroup))
            {
                _level3ElectricIconCanvasGroup = level3ElectricIcon.gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// VContainer 의존성 주입. MessagePipe 구독자(카드 인식/카드 떨어짐), 씬 전환 서비스, 미션 보드·카테고리 안내·결과 저장소,
        /// 화살표 안내·잘못된 카드 경고·설계창 컴포넌트, 로거, 효과음 매니저, 행동 로그 주어를 줄 체험자 정보를 할당함.
        /// </summary>
        [Inject]
        public void Construct(ISubscriber<RfidTagEvent> subscriber, ISubscriber<RfidReaderIdleEvent> idleSubscriber, SelectedLevelStore selectedLevelStore, SceneTransitionService sceneTransition, MissionBoardController missionBoard, CodingCategoryIndicatorController codingCategoryIndicator, GameResultStore resultStore, RightArrowHint rightArrowHint, InvalidCardWarning invalidCardWarning, DesignPanel designPanel, ILogger<IngredientSelectionController> logger, SoundManager soundManager = null, VisitorInfoProvider visitorInfoProvider = null)
        {
            _subscriber = subscriber;
            _idleSubscriber = idleSubscriber;
            _selectedLevelStore = selectedLevelStore;
            _sceneTransition = sceneTransition;
            _missionBoard = missionBoard;
            _codingCategoryIndicator = codingCategoryIndicator;
            _resultStore = resultStore;
            _rightArrowHint = rightArrowHint;
            _invalidCardWarning = invalidCardWarning;
            _designPanel = designPanel;
            _logger = logger;
            _soundManager = soundManager;
            _visitorInfoProvider = visitorInfoProvider;
        }

        /// <summary>
        /// 버튼 이벤트 리스너 등록 및 R3 구독 관계 설정.
        /// </summary>
        private void Start()
        {
            AddButtonListener(buttonLeft, OnLeftButtonClicked, nameof(buttonLeft));
            AddButtonListener(buttonRight, OnRightButtonClicked, nameof(buttonRight));
            AddButtonListener(buttonConfirm, OnConfirmButtonClicked, nameof(buttonConfirm));
            AddButtonListener(buttonCancel, OnCancelButtonClicked, nameof(buttonCancel));
            AddButtonListener(buttonCodingComplete, OnCodingCompleteClicked, nameof(buttonCodingComplete));
            AddButtonListener(buttonSkip, OnSkipButtonClicked, nameof(buttonSkip));

            if (_visitorInfoProvider == null && _logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] visitorInfoProvider가 null이라 행동 로그에 체험자 이름 대신 '체험자'를 씀.");
            }

            if (_subscriber != null)
            {
                _subscriber.Subscribe(OnRfidTagReceived).AddTo(ref _disposables);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] RfidTagEvent 구독자가 null이라 카드 인식을 받을 수 없음.");
            }

            if (_idleSubscriber != null)
            {
                _idleSubscriber.Subscribe(OnRfidReaderIdle).AddTo(ref _disposables);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] RfidReaderIdleEvent 구독자가 null이라 카드 떨어짐을 감지할 수 없음.");
            }

            _currentIngredient.Subscribe(UpdateIngredientText).AddTo(ref _disposables);
            _currentMatterIndex.Subscribe(_ => { UpdateMatterText(); UpdateProgressPreview(); }).AddTo(ref _disposables);

            UpdateCodingCompleteButton();

            // 비동기로 설정을 로드하여 워크플로우 단계를 설정함
            InitializeWorkflowAsync().Forget();
        }

        /// <summary> 버튼에 클릭 리스너를 연결하고, 버튼이 연결되지 않았으면 경고를 남김. </summary>
        private void AddButtonListener(Button button, UnityAction onClick, string fieldName)
        {
            if (button)
            {
                button.onClick.AddListener(onClick);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] {fieldName}이 null이라 클릭 이벤트를 연결할 수 없음.");
            }
        }

        /// <summary>
        /// JSON 설정파일을 로드하여 현재 레벨의 재료 진행 순서(steps)와 총 단계 수를 파악하고 슬롯을 준비함.
        /// 3_Game.json(GameSceneSettings) 연출 타이밍도 GameSceneSettingsProvider를 통해 함께 불러옴(씬 내 다른 컨트롤러와 로드를 공유함).
        /// </summary>
        private async UniTaskVoid InitializeWorkflowAsync()
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();

            UniTask<GameSceneSettings> sceneSettingsTask = GameSceneSettingsProvider.GetAsync(token);
            UniTask<CommonSettings> commonTask = CommonSettingsProvider.GetAsync(token);
            (_sceneSettings, _commonSettings) = await UniTask.WhenAll(sceneSettingsTask, commonTask);

            try
            {
                RfidSettings settings = await JsonLoader.LoadAsync<RfidSettings>(Constants.Files.RfidMappings, token, _logger);

                // 로드 중에 씬을 떠나 이 오브젝트가 파괴됨. JsonLoader는 취소돼도 예외 없이 기본값을 돌려주므로 여기서 멈춤
                // (계속하면 이미 해제된 상태 머신·참조를 건드려 ObjectDisposedException과 null 경고가 이어짐)
                if (token.IsCancellationRequested) return;

                ApplyRfidSettings(settings);
            }
            catch (Exception e)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] 워크플로우용 RfidMappings.json 로드 실패: {e.Message}");
            }

            ResetWorkflowProgress();
        }

        /// <summary>
        /// RfidMappings.json 설정으로 리더기 수, 레벨 상태, 이 레벨의 블록 목록·단계 정의, 총 단계 수를 정함.
        /// 단계 정의가 없는 레벨이면 stageReadCounts의 첫 값만큼 진행하고 경고를 남김.
        /// </summary>
        private void ApplyRfidSettings(RfidSettings settings)
        {
            int fallbackStepCount = (settings != null && settings.stageReadCounts != null && settings.stageReadCounts.Length > 0)
                ? settings.stageReadCounts[0]
                : DefaultFallbackStepCount;

            _readerCount = (settings?.readers != null && settings.readers.Length > 0) ? settings.readers.Length : 1;

            int level = SelectedLevelStore.LevelOrFallback(_selectedLevelStore, _logger, nameof(IngredientSelectionController));
            ChangeLevelState(level);
            ValidateMappings(settings);
            ApplyLevelMapping(settings != null ? settings.FindLevelMapping(level) : null);

            bool hasStepDefinitions = _stepDefinitions != null && _stepDefinitions.Length > 0;
            _totalSteps = hasStepDefinitions ? _stepDefinitions.Length : fallbackStepCount;

            if (!hasStepDefinitions && _logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] RfidMappings.json에 {level}레벨 단계(steps) 정의가 없어 stageReadCounts의 {_totalSteps}회로 진행함.");
            }
        }

        /// <summary> 확정 기록과 떨어진 카드 표시를 비우고, 설계창·설정하기/코딩 완료 막힘·스텝 볼·분류 안내를 처음 상태로 둠. </summary>
        private void ResetWorkflowProgress()
        {
            _confirmedMatters = new RfidMatter[_totalSteps];
            _confirmedIngredients = new string[_totalSteps];
            _confirmedCategories = new string[_totalSteps];
            _idleReaderStepIndices.Clear();
            ResetDesignPanel();
            RefreshMissingCards();
            InitializeStepBalls();
            UpdateCategoryHint();
        }

        /// <summary> 선택된 레벨(1~5)에 맞는 레벨 상태로 전환함. 범위를 벗어나면 레벨 1 상태를 씀. </summary>
        internal void ChangeLevelState(int level)
        {
            _selectedLevel = level;
            IIngredientSelectionLevelState targetState = level switch
            {
                1 => _level1State,
                2 => _level2State,
                3 => _level3State,
                4 => _level4State,
                5 => _level5State,
                _ => null
            };

            if (targetState == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 레벨 {level}은 1~{Constants.LastLevel} 밖이라 레벨 1 상태를 씀.");
                targetState = _level1State;
            }

            _stateMachine.ChangeState(targetState);
        }

        /// <summary> 현재 레벨의 블록 목록·단계 정의·분류별 재료 정의를 적용함. mapping이 null이면 모두 비움. </summary>
        internal void ApplyLevelMapping(RfidLevelMapping mapping)
        {
            _levelMapping = mapping;
            _stepDefinitions = mapping?.steps;
            _categoryIngredients = mapping?.categoryIngredients;
        }

        /// <summary>
        /// RfidMappings.json의 레벨 1~5 블록 정의를 검사해 문제마다 오류 로그를 남김. 블록 목록이 비었거나 값이 빠지면 게임 중에는
        /// 선택지가 비거나 미션을 깰 수 없을 뿐 다른 오류가 나지 않으므로, 로드 직후 원인을 바로 알 수 있게 함.
        /// 레벨 1 목적지 거리와 레벨 3 기준값 범위(LevelData)도 블록으로 만들 수 있는지 함께 검사함.
        /// </summary>
        private void ValidateMappings(RfidSettings settings)
        {
            if (_logger == null) return; // 오류를 남길 곳이 없으면 검사할 의미가 없음

            LevelData level1Data = null;
            LevelData level3Data = null;
            if (_missionBoard)
            {
                level1Data = _missionBoard.GetLevelData(1);
                level3Data = _missionBoard.GetLevelData(3);
            }
            else
            {
                _logger.ZLogWarning($"[IngredientSelectionController] missionBoard가 null이라 레벨 1 목적지 거리·레벨 3 기준값 범위 검사를 건너뜀.");
            }

            List<string> errors = RfidMappingValidator.Validate(settings, level1Data, level3Data);
            foreach (string error in errors)
            {
                _logger.ZLogError($"[IngredientSelectionController] RfidMappings.json 검증 실패: {error}");
            }
        }

        /// <summary>
        /// 현재 단계(_currentStepIndex)가 허용하는 category 목록을 CodingCategoryIndicatorController에 전달해
        /// 다음에 찍어야 할 카테고리 아이콘이 부드럽게 페이드하며 안내되도록 함. 모든 단계가 끝났으면 힌트를 멈춤.
        /// 레벨 4에서 바로 이전 단계가 "반복하기"였다면(동작 카드만 허용됨) "제어"는 힌트에서 제외함.
        /// </summary>
        private void UpdateCategoryHint()
        {
            if (!_codingCategoryIndicator)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] codingCategoryIndicator가 null이라 카테고리 힌트를 갱신할 수 없음.");
                return;
            }

            if (_stepDefinitions != null && _currentStepIndex < _totalSteps && _currentStepIndex < _stepDefinitions.Length && _stepDefinitions[_currentStepIndex] != null)
            {
                RfidStepDefinition step = _stepDefinitions[_currentStepIndex];
                string[] categories = CurrentLevelState != null
                    ? CurrentLevelState.GetAllowedCategories(this, step)
                    : step.categories;

                _codingCategoryIndicator.ShowNextHint(categories);
            }
            else
            {
                _codingCategoryIndicator.StopHint();
            }
        }

        /// <summary>
        /// 레벨 2의 Image_StepN_Ball을 전부 흑백 상태로 되돌리고 완료 텍스트를 비움. stepBallImages가 비어 있으면(다른 레벨) 아무것도 하지 않음.
        /// </summary>
        private void InitializeStepBalls()
        {
            if (stepBallImages == null) return;

            for (int i = 0; i < stepBallImages.Length; i++)
            {
                Image ballImage = stepBallImages[i];
                if (!ballImage)
                {
                    if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] stepBallImages[{i}]가 null이라 초기화를 건너뜀.");
                    continue;
                }

                ballImage.material = stepBallGrayscaleMaterial;

                if (ChildComponentFinder.TryGetInDirectChildren(ballImage.transform, out TMP_Text ballText))
                {
                    ballText.text = "";
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[IngredientSelectionController] {ballImage.name}의 직계 자식에 TMP_Text가 없어 완료 문구를 비울 수 없음.");
                }
            }

            if (level2FillImage)
            {
                level2FillImage.fillAmount = 0f;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] level2FillImage가 null이라 진행바를 초기화할 수 없음.");
            }
        }

        /// <summary>
        /// RFID 태그(또는 디버그 키) 이벤트 수신 시, 현재 단계(_currentStepIndex)가 허용하는 category와 일치하면
        /// 해당 단계의 재료로 진행함. 카드는 더 이상 재료 이름을 알려주지 않으므로, 진행 순서(추진체 종류 -> 탑재 종류 -> 연료량)는
        /// _currentStepIndex에 대응하는 _stepDefinitions 항목으로 결정됨. 단계별로 허용 category가 다를 수 있음
        /// (예: 레벨 1은 모든 단계가 "동작"만 허용, 레벨 4는 "동작"/"제어"를 유동적으로 허용).
        /// 리더기 라우팅·무시 판정(TryRouteToCurrentStep) → 단계와 카드 확인(TryResolveStepCard) → 선택 상태 적용(ApplyStepCard) 순서로 처리함.
        /// </summary>
        private void OnRfidTagReceived(RfidTagEvent evt)
        {
            if (!TryRouteToCurrentStep(evt)) return;
            if (!TryResolveStepCard(evt, out RfidStepDefinition ingredient, out RfidMatter[] matters)) return;

            ApplyStepCard(evt, ingredient, matters);
        }

        /// <summary>
        /// 이 태그를 지금 단계의 입력으로 처리할지 정함. 게임 화면이 아니거나, 아직 차례가 아닌 리더기이거나, 모든 단계를 마쳤으면 무시하고(행동 로그),
        /// 떼었던 카드가 같은 분류로 돌아오면 블록만 다시 붙이고 끝냄. 이미 설정한 단계의 리더기에서 카드가 바뀌었으면 그 단계까지 되돌린 뒤 true를 돌려줌.
        /// </summary>
        private bool TryRouteToCurrentStep(RfidTagEvent evt)
        {
            // 리더기별 스탭 라우팅: 설정된 리더기가 2대 이상일 때만 적용함(1대뿐이면 기존처럼 어떤 리더기든 현재 스탭에 적용).
            // "Reader_N" 형식이 아닌 readerId(디버그 키보드 시뮬레이터의 "Keyboard", 미등록 리더기의 "Unknown_...")는
            // GetStepIndexForReader가 -1을 반환하므로 라우팅 없이 기존처럼 현재 스탭에 적용됨.
            int readerStepIndex = _readerCount > 1 ? GetStepIndexForReader(evt.ReaderId) : -1;

            // 카드가 떨어졌던 스탭의 리더기에 카드가 다시 올라온 것이면 스토리 화면 등으로 게임 패널이 잠시 비활성이어도 처리함
            // (무시하면 카드는 놓였는데 블록은 떨어진 채 남고 설정하기·코딩 완료도 계속 막힘). 결과 화면으로 넘어가는 중에는 처리하지 않음.
            bool returnsMissingCard = !_isBusy && readerStepIndex >= 0 && _idleReaderStepIndices.Contains(readerStepIndex);

            // 게임 패널이 활성 상태가 아니면(스토리 화면 등) RFID 입력을 무시함
            if (!gamePanel || (!gamePanel.interactable && !returnsMissingCard))
            {
                LogCardPlaced(evt, "게임 화면이 아니라 무시함");
                return false;
            }

            if (_readerCount > 1)
            {
                // 카드가 떨어졌던 스탭이면 떨어짐 표시에서 뺌. 떨어뜨린 블록은 같은 category일 때만 아래에서 다시 붙이고,
                // 다른 category면 HandleConfirmedStepCardChanged가 그 스탭부터 블록을 지움(다시 붙였다 지우면 깜빡이므로 미리 붙이지 않음).
                bool wasIdle = readerStepIndex >= 0 && _idleReaderStepIndices.Remove(readerStepIndex);

                if (readerStepIndex >= 0 && readerStepIndex < _currentStepIndex)
                {
                    // 카드가 잠깐 떨어졌다가(idle) 같은 category의 카드가 다시 올라온 것뿐이면 확정된 값은
                    // 그대로 유효하므로 되돌리지 않고 복구만 함(파괴적 롤백 방지).
                    string previousCategory = (_confirmedCategories != null && readerStepIndex < _confirmedCategories.Length)
                        ? _confirmedCategories[readerStepIndex] : null;

                    if (wasIdle && string.Equals(previousCategory, evt.Category, StringComparison.Ordinal))
                    {
                        LogCardPlaced(evt, $"{readerStepIndex + 1}번째 단계에서 뗐던 카드와 같은 분류라 블록을 다시 붙임");
                        RefreshMissingCards();
                        return false;
                    }

                    // 그 외(카드를 떼지 않은 채 다른 카드로 교체했거나, 떨어졌다가 다른 category로 바뀐 경우)는
                    // 이미 확정된 스탭을 담당하는 리더기에서 변경이 감지된 것이므로, 그 스탭부터 되돌린 뒤
                    // 아래 로직에서 이 태그를 그 스탭의 새 입력으로 처리함.
                    HandleConfirmedStepCardChanged(readerStepIndex, evt.Category);
                }
                else if (readerStepIndex > _currentStepIndex)
                {
                    // 아직 도달하지 않은(활성화되지 않은) 스탭의 리더기 -> 무시
                    LogCardPlaced(evt, $"아직 {_currentStepIndex + 1}번째 단계를 하는 중이라 무시함");
                    return false;
                }
            }

            // 모든 단계가 완료되면 더 이상 태그를 받지 않음
            if (_currentStepIndex >= _totalSteps)
            {
                LogCardPlaced(evt, "모든 단계를 마쳐 무시함");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 지금 단계의 정의와 카드 분류를 확인해, 이 카드로 고를 재료(ingredient)와 블록 목록(matters)을 찾음. 단계 정의·재료·블록 목록이 없으면 경고를,
        /// 이 단계에서 쓸 수 없는 분류면 잘못된 카드 경고를 띄우고 false. 레벨 상태가 받지 않는 카드(레벨 4·5 규칙)도 false.
        /// </summary>
        private bool TryResolveStepCard(RfidTagEvent evt, out RfidStepDefinition ingredient, out RfidMatter[] matters)
        {
            ingredient = null;
            matters = null;

            if (_stepDefinitions == null || _currentStepIndex >= _stepDefinitions.Length || _stepDefinitions[_currentStepIndex] == null)
            {
                if (_logger != null)
                {
                    _logger.ZLogWarning($"[IngredientSelectionController] {_currentStepIndex}번 인덱스에 대한 단계 정의가 없어 {evt.ReaderId} 태그를 무시함.");
                }
                return false;
            }

            RfidStepDefinition step = _stepDefinitions[_currentStepIndex];

            if (!step.AllowsCategory(evt.Category))
            {
                LogCardPlaced(evt, $"{_currentStepIndex + 1}번째 단계에서 쓸 수 없는 카드라 경고를 띄움");
                ShowInvalidCardWarning();
                return false;
            }

            if (CurrentLevelState != null && !CurrentLevelState.ValidateTagCategory(this, evt))
            {
                return false; // 레벨 상태가 행동 로그와 경고를 남김
            }

            ingredient = CurrentLevelState != null
                ? CurrentLevelState.ResolveStepCard(this, step, evt.Category)
                : step;

            if (ingredient == null)
            {
                if (_logger != null)
                {
                    _logger.ZLogWarning($"[IngredientSelectionController] '{evt.Category}' 카드에 해당하는 재료 정의가 RfidMappings.json에 없어 {evt.ReaderId} 태그를 무시함.");
                }
                return false;
            }

            matters = _levelMapping?.FindMatters(ingredient.matterSetId);
            if (matters == null || matters.Length == 0)
            {
                if (_logger != null)
                {
                    _logger.ZLogWarning($"[IngredientSelectionController] {ingredient.ingredientName}({ingredient.ingredientId})의 블록 목록 '{ingredient.matterSetId}'가 RfidMappings.json에 없거나 비어 있어 {evt.ReaderId} 태그를 무시함.");
                }
                return false;
            }

            return true;
        }

        /// <summary> 확인된 카드로 지금 단계의 재료와 고를 블록 목록을 정하고, 분류 강조·값 표시·미리보기를 갱신함(행동 로그 포함). </summary>
        private void ApplyStepCard(RfidTagEvent evt, RfidStepDefinition ingredient, RfidMatter[] matters)
        {
            LogCardPlaced(evt, string.IsNullOrEmpty(ingredient.ingredientName)
                ? ZString.Concat(_currentStepIndex + 1, "번째 단계에 적용함")
                : ZString.Concat(_currentStepIndex + 1, "번째 단계 '", ingredient.ingredientName, "'에 적용함"));

            // 현재 단계에 허용된 카드로 확인된 경우에만 CodingCategories 강조를 갱신함(허용되지 않으면 흑백 상태가 그대로 유지됨)
            if (_codingCategoryIndicator) _codingCategoryIndicator.HighlightCategory(evt.Category);
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] codingCategoryIndicator가 null이라 카테고리 강조를 갱신할 수 없음.");

            _currentIngredient.Value = ingredient.ingredientName ?? "";
            _currentIngredientId = ingredient.ingredientId;
            _currentCategory = evt.Category;
            _currentMatters = CurrentLevelState != null
                ? CurrentLevelState.FilterMatters(this, ingredient.ingredientId, matters)
                : ExcludeConfirmedMatters(ingredient.ingredientId, matters);
            ResetMatterIndexAndRefresh();
        }

        /// <summary>
        /// 고르던 블록 인덱스를 0으로 되돌리고 값 표시·미리보기를 한 번만 갱신함. 인덱스가 이미 0이면 구독이 불리지 않으므로 직접 갱신하고,
        /// 0이 아니면 바꾸는 순간 구독이 같은 갱신을 함.
        /// </summary>
        private void ResetMatterIndexAndRefresh()
        {
            if (_currentMatterIndex.Value == 0)
            {
                UpdateMatterText();
                UpdateProgressPreview();
            }
            else
            {
                _currentMatterIndex.Value = 0;
            }
        }

        /// <summary> 행동 로그 주어(예: "홍길동이"). </summary>
        private string PlayerSubject => VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider);

        /// <summary> 체험자가 리더기에 카드를 올렸을 때 그 처리 결과를 행동 로그 한 줄로 남김. 레벨 상태가 카드를 받지 않을 때도 씀. </summary>
        internal void LogCardPlaced(RfidTagEvent evt, string outcome)
        {
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] {PlayerSubject} {ReaderLabel(evt.ReaderId)}에 {evt.Category} 카드를 올림 — {outcome}.");
        }

        /// <summary> 체험자가 리더기에서 카드를 뗐을 때 그 처리 결과를 행동 로그 한 줄로 남김. </summary>
        private void LogCardRemoved(string readerId, string outcome)
        {
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] {PlayerSubject} {ReaderLabel(readerId)}에서 카드를 뗌 — {outcome}.");
        }

        /// <summary> 행동 로그에 쓸 리더기 이름. "Reader_N"이면 "N번 리더기", 아니면(디버그 키보드·미등록 리더기) readerId 그대로. </summary>
        private static string ReaderLabel(string readerId)
        {
            int stepIndex = GetStepIndexForReader(readerId);
            return stepIndex >= 0 ? ZString.Concat(stepIndex + 1, "번 리더기") : readerId;
        }

        /// <summary> "Reader_N" 형식의 readerId에서 0-기반 스탭 인덱스(N-1)를 추출함. 형식이 안 맞으면(디버그/미등록 리더기) -1을 반환함. </summary>
        private static int GetStepIndexForReader(string readerId)
        {
            if (string.IsNullOrEmpty(readerId)) return -1;

            int underscoreIndex = readerId.LastIndexOf('_');
            if (underscoreIndex < 0 || underscoreIndex == readerId.Length - 1) return -1;

            return int.TryParse(readerId.AsSpan(underscoreIndex + 1), out int readerNumber) ? readerNumber - 1 : -1;
        }

        /// <summary>
        /// 이미 확정된 stepIndex번째 스탭을 담당하는 리더기에서 카드가 바뀐 것을 감지했을 때 호출됨.
        /// 같은 category든 다른 category든 처리는 동일함: stepIndex까지 되돌리고(그 뒤 확정된 값은 전부 무효화),
        /// 이 메서드를 호출한 OnRfidTagReceived가 이어서 새 태그를 그 스탭의 입력으로 정상 처리하게 함.
        /// (같은 category: 방향/횟수 등 세부 값만 새로 고르면 되므로 사실상 "그대로 재진행"처럼 느껴짐.
        ///  다른 category: 그 스탭 이후 확정 값이 전부 취소되므로 더 크게 되돌아가는 셈.)
        /// </summary>
        private void HandleConfirmedStepCardChanged(int stepIndex, string newCategory)
        {
            string previousCategory = (_confirmedCategories != null && stepIndex < _confirmedCategories.Length) ? _confirmedCategories[stepIndex] : null;
            bool sameCategory = string.Equals(previousCategory, newCategory, StringComparison.Ordinal);

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] 이미 확정된 {stepIndex + 1}번째 단계에서 카드 변경 감지({previousCategory} -> {newCategory}). {(sameCategory ? "같은 카테고리라 그대로 재진행" : "다른 카테고리라 이후 단계 전부 무효화")}함.");
            }

            RollbackToStep(stepIndex);
            UpdateCategoryHint();
        }

        /// <summary>
        /// 리더기에서 카드가 떨어졌을(RfidReaderIdleEvent) 때 호출됨. 마지막으로 확정한 스탭의 카드면 취소하기를 누른 것처럼 그 스탭을 되돌림.
        /// 그보다 앞서 확정된 스탭의 카드면 그 스탭부터 뒤쪽 설계창 블록을 임시로 떨어뜨리고 설정하기·코딩 완료를 막음(같은 category 카드가
        /// 다시 인식되면 OnRfidTagReceived가 되돌림). 값을 고르던(아직 확정 안 된) 현재 스탭의 카드면 고르던 값만 비움. 리더기 1대뿐이면 그 1대가
        /// 항상 "지금 진행 중인" 스탭이라 카드를 떼는 것 자체가 일반적인 조작 흐름이므로 이 기능을 적용하지 않음.
        /// </summary>
        private void OnRfidReaderIdle(RfidReaderIdleEvent evt)
        {
            if (_readerCount <= 1) return;
            if (_confirmedMatters == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 워크플로우 초기화가 끝나지 않아 {evt.ReaderId} 카드 떨어짐을 처리할 수 없음.");
                return;
            }

            // 코딩 완료·건너뛰기로 결과 화면으로 넘어가는 중에는 체험자가 카드를 치워도 완성된 설계창을 그대로 둠
            if (_isBusy)
            {
                LogCardRemoved(evt.ReaderId, "결과 화면으로 넘어가는 중이라 그대로 둠");
                return;
            }

            int stepIndex = GetStepIndexForReader(evt.ReaderId);
            if (stepIndex < 0 || stepIndex > _currentStepIndex)
            {
                LogCardRemoved(evt.ReaderId, $"단계가 없는 리더기이거나 아직 하지 않은 단계라 그대로 둠(지금 {_currentStepIndex + 1}번째 단계)");
                return;
            }

            if (stepIndex == _currentStepIndex)
            {
                if (_currentMatters.Length == 0)
                {
                    LogCardRemoved(evt.ReaderId, "고르던 값이 없어 그대로 둠");
                    return;
                }

                LogCardRemoved(evt.ReaderId, "고르던 값을 비움");
                ClearPendingSelection();
                UpdateCategoryHint();
                return;
            }

            // 다시 올려도 값을 다시 골라 설정해야 함(임시로 떨어뜨렸다가 다시 붙이지 않음)
            if (stepIndex == _currentStepIndex - 1)
            {
                CancelLastStep();
                LogCardRemoved(evt.ReaderId, $"마지막으로 설정한 단계라 취소해 {_currentStepIndex + 1}번째 단계로 되돌림");
                return;
            }

            if (_idleReaderStepIndices.Add(stepIndex))
            {
                LogCardRemoved(evt.ReaderId, $"{stepIndex + 1}번째 단계부터 블록을 잠시 떼고 설정하기·코딩 완료를 막음");
                RefreshMissingCards();
            }
        }

        /// <summary>
        /// 카드가 떨어진 스탭(_idleReaderStepIndices) 중 가장 이른 스탭부터 끝까지 설계창 블록을 임시로 떨어뜨리고(그 앞은 다시 붙임),
        /// 떨어진 카드가 있는 동안 설정하기·코딩 완료 버튼을 막음. 확정 값 자체는 바꾸지 않음. 끝나면 레벨 상태에 알림(레벨 5 현재 상황 그림 갱신).
        /// </summary>
        private void RefreshMissingCards()
        {
            int dropFromIndex = FirstMissingCardStep;

            if (_designPanel) _designPanel.DropFrom(dropFromIndex);
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] designPanel이 null이라 카드가 떨어진 단계의 블록을 떨어뜨리거나 다시 붙일 수 없음.");

            if (buttonConfirm) buttonConfirm.interactable = _idleReaderStepIndices.Count == 0;
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] buttonConfirm이 null이라 카드가 떨어진 동안 설정하기를 막을 수 없음.");

            UpdateCodingCompleteButton();
            CurrentLevelState?.OnMissingCardsRefreshed(this);
        }

        /// <summary> 카드가 떨어진 단계 중 가장 이른 단계(이 단계부터 뒤 설계창 블록이 임시로 떨어져 있음). 떨어진 카드가 없으면 int.MaxValue. </summary>
        internal int FirstMissingCardStep
        {
            get
            {
                int first = int.MaxValue;
                foreach (int idx in _idleReaderStepIndices)
                {
                    if (idx < first) first = idx;
                }

                return first;
            }
        }

        /// <summary>
        /// 레벨 4 전용: 지금까지 확정된 (재료 id, 물질) 순서 목록을 확정된 순서 그대로 반환함.
        /// Level4BoardController가 이 목록으로 로봇 이동 경로(스페이스바 시뮬레이션)를 만드는 데 사용함.
        /// 아직 확정되지 않은 뒤쪽 슬롯은 포함하지 않음(레벨 4는 5단계를 다 채우지 않아도 되므로).
        /// </summary>
        public IReadOnlyList<(string ingredientId, RfidMatter matter)> GetConfirmedCommands()
        {
            List<(string ingredientId, RfidMatter matter)> commands = new List<(string ingredientId, RfidMatter matter)>();
            if (_confirmedIngredients == null || _confirmedMatters == null) return commands;

            for (int i = 0; i < _currentStepIndex && i < _confirmedIngredients.Length; i++)
            {
                commands.Add((_confirmedIngredients[i], _confirmedMatters[i]));
            }

            return commands;
        }

        /// <summary> 현재 레벨·단계에서 받지 않는 카드가 인식됐을 때 경고음을 내고 경고 연출(경고 이미지 + 게임 패널 흔들기)을 보여 줌. </summary>
        internal void ShowInvalidCardWarning()
        {
            SoundEffects.Play(_soundManager, Constants.Sounds.CodingAlert, _logger);

            if (_invalidCardWarning)
            {
                _invalidCardWarning.Show();
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] invalidCardWarning이 null이라 잘못된 카드 경고를 표시할 수 없음.");
            }
        }

        /// <summary>
        /// 같은 ingredientId를 쓰는 여러 단계가 matters 목록을 공유할 때(예: 레벨 2의 발사 코딩 순서), 이미 다른 단계에서
        /// 확정된 값은 다시 고를 수 없도록 목록에서 제외함. ingredientId까지 함께 비교하므로, 서로 다른 ingredient가
        /// 우연히 같은 물질 id를 공유해도(예: 레벨 3의 전기량/산소량이 둘 다 Raise/Lower) 서로 간섭하지 않음.
        /// </summary>
        internal RfidMatter[] ExcludeConfirmedMatters(string ingredientId, RfidMatter[] matters)
        {
            if (matters == null || matters.Length == 0) return Array.Empty<RfidMatter>();
            if (_confirmedMatters == null || _confirmedIngredients == null) return matters;

            List<RfidMatter> available = new List<RfidMatter>(matters.Length);
            foreach (RfidMatter matter in matters)
            {
                bool alreadyConfirmed = false;
                for (int i = 0; i < _confirmedMatters.Length; i++)
                {
                    if (string.Equals(_confirmedIngredients[i], ingredientId, StringComparison.Ordinal) &&
                        string.Equals(_confirmedMatters[i]?.id, matter.id, StringComparison.Ordinal))
                    {
                        alreadyConfirmed = true;
                        break;
                    }
                }

                if (!alreadyConfirmed) available.Add(matter);
            }

            return available.ToArray();
        }

        /// <summary>
        /// 설정하기(Confirm) 버튼 클릭 시 현재 선택한 물질을 확정해 설계창에 블록을 붙이고(블록 장착 효과음) 다음 단계로 진행함.
        /// </summary>
        private void OnConfirmButtonClicked()
        {
            if (_confirmedMatters == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 워크플로우 초기화가 끝나지 않아 설정하기를 처리할 수 없음.");
                return;
            }

            // ingredientName이 빈 문자열인 단계(예: 레벨 3의 논리 연결어)도 있으므로, "스캔된 것이 없음"은
            // ingredient가 아니라 matters 목록의 존재 여부로 판단함.
            RfidMatter[] matters = _currentMatters;
            if (matters == null || matters.Length == 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 확정할 RFID 태그가 스캔되어 있지 않음.");
                return;
            }

            if (_currentStepIndex >= _totalSteps)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 모든 단계가 이미 완료됨.");
                return;
            }

            string ingredientName = _currentIngredient.Value;
            string ingredientId = _currentIngredientId;
            RfidMatter chosenMatter = matters[_currentMatterIndex.Value];
            _confirmedMatters[_currentStepIndex] = chosenMatter;
            _confirmedIngredients[_currentStepIndex] = ingredientId;
            if (_confirmedCategories != null && _currentStepIndex < _confirmedCategories.Length)
            {
                _confirmedCategories[_currentStepIndex] = _currentCategory;
            }

            if (_logger != null)
            {
                string chosen = string.IsNullOrEmpty(ingredientName) ? chosenMatter.label : ZString.Concat(ingredientName, " = ", chosenMatter.label);
                string finished = _currentStepIndex + 1 >= _totalSteps ? ", 모든 단계를 마침" : "";
                _logger.ZLogInformation($"[IngredientSelectionController] {PlayerSubject} 설정하기를 누름 — {_currentStepIndex + 1}번째 단계를 '{chosen}'(으)로 정함{finished}.");
            }

            // 디자인 컨테이너에 확정 항목을 자식으로 추가
            AddDesignItem(ingredientId, ingredientName, chosenMatter.label);
            SoundEffects.Play(_soundManager, Constants.Sounds.BlockAssembled, _logger);

            // 레벨별 상태 객체에 확정 처리 위임 (스텝 볼, 게이지, 불안정 깜빡임, 추진력 등)
            CurrentLevelState?.OnStepConfirmed(this, _currentStepIndex, ingredientId, chosenMatter);

            // 확정된 엔진 출력량/연료량/탑재 중량을 계산식에 반영해 진행도(Image_Fill)를 갱신함
            ApplyProgressToMissionBoard();

            ClearPendingSelection();

            // 다음 단계로 인덱스 증가
            _currentStepIndex++;
            UpdateCodingCompleteButton();

            // 다음 단계가 남아있으면 그 단계의 카테고리 힌트를 다시 페이드로 안내하고, 없으면 힌트를 멈춤
            // (동작 확정으로 켜졌던 CodingCategories 강조도 여기서 함께 흑백으로 정리됨)
            UpdateCategoryHint();
        }

        /// <summary>
        /// 취소하기(Cancel) 버튼 클릭 시 클릭음을 내고 현재 대기 상태를 비우고 이전 단계로 되돌아감.
        /// </summary>
        private void OnCancelButtonClicked()
        {
            if (_confirmedMatters == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 워크플로우 초기화가 끝나지 않아 취소하기를 처리할 수 없음.");
                return;
            }

            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);

            if (_currentStepIndex == 0)
            {
                // 1단계(Reader 1)인 경우 현재 태그된 임시 선택값만 클리어
                ClearPendingSelection();
                UpdateCategoryHint();
                if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] {PlayerSubject} 취소하기를 누름 — 1번째 단계에서 고르던 값을 비움.");
                return;
            }

            CancelLastStep();
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] {PlayerSubject} 취소하기를 누름 — {_currentStepIndex + 1}번째 단계로 되돌림.");
        }

        /// <summary> 확정된 마지막 단계를 되돌리고 고르던 값을 비운 뒤 되돌아간 단계를 안내함. 취소하기 버튼과, 마지막으로 확정한 단계의 카드가 떨어졌을 때 씀. </summary>
        private void CancelLastStep()
        {
            RollbackOneStep();
            RefreshMissingCards(); // 되돌린 단계의 카드가 떨어져 있었다면 막아 둔 설정하기·코딩 완료를 풂

            ApplyProgressToMissionBoard();

            ClearPendingSelection();

            // 되돌아간 단계의 카테고리 힌트를 다시 페이드로 안내함
            UpdateCategoryHint();
        }

        /// <summary>
        /// 현재 대기 중인(아직 확정 안 된) 카드 선택 상태를 비움. Confirm/Cancel/리더기 변경 감지 롤백 등
        /// 단계가 바뀔 때마다 공통으로 호출됨.
        /// </summary>
        private void ClearPendingSelection()
        {
            _currentIngredient.Value = "";
            _currentIngredientId = null;
            _currentCategory = null;
            _currentMatters = Array.Empty<RfidMatter>();
            ResetMatterIndexAndRefresh();
        }

        /// <summary>
        /// 확정된 마지막 한 단계(_currentStepIndex - 1)를 되돌림: 계산식/스텝 볼/레벨3 효과를 반대로 되돌리고,
        /// 확정 배열(matter/ingredient/category)을 비운 뒤 디자인 항목을 제거하고 _currentStepIndex를 하나 감소시킴.
        /// Cancel 버튼과 리더기 변경 감지(RollbackToStep) 양쪽에서 재사용함. 미션보드 진행도 갱신과
        /// 대기 선택 상태 클리어는 호출자가 필요한 시점에 별도로 처리함(여러 단계를 한 번에 되돌릴 때 매번 반복하지 않기 위함).
        /// </summary>
        private void RollbackOneStep()
        {
            _currentStepIndex--;

            CurrentLevelState?.OnStepRolledBack(this, _currentStepIndex, _confirmedIngredients[_currentStepIndex], _confirmedMatters[_currentStepIndex]);

            _confirmedMatters[_currentStepIndex] = null;
            _confirmedIngredients[_currentStepIndex] = null;
            if (_confirmedCategories != null && _currentStepIndex < _confirmedCategories.Length)
            {
                _confirmedCategories[_currentStepIndex] = null;
            }

            _idleReaderStepIndices.Remove(_currentStepIndex); // 해당 스탭의 블록이 곧 제거되므로 카드 떨어짐 추적 대상에서도 제외(표시 갱신은 호출자가 RefreshMissingCards로)
            RemoveLastDesignItem();
        }

        /// <summary>
        /// _currentStepIndex가 targetStepIndex와 같아질 때까지 RollbackOneStep을 반복 호출함(여러 단계를 한 번에 되돌림).
        /// 리더기별 스탭 라우팅에서 이미 확정된 스탭의 카드가 바뀐 것을 감지했을 때 사용함.
        /// </summary>
        private void RollbackToStep(int targetStepIndex)
        {
            while (_currentStepIndex > targetStepIndex)
            {
                RollbackOneStep();
            }

            ApplyProgressToMissionBoard();

            RefreshMissingCards(); // 다 되돌린 뒤 한 번만 갱신함(중간에 갱신하면 곧 지울 블록이 다시 붙었다 사라지며 깜빡임)
            ClearPendingSelection();
        }

        /// <summary>
        /// 확정된 재료/물질을 설계창에 블록으로 쌓음. 모양과 문구는 레벨 상태가 정함(재료 이름은 명령 블록, 고른 블록 이름은 값 블록,
        /// 레벨 3은 조건이 만약 ㄷ자 블록·동작이 그 안쪽·논리 연결어가 논리 블록, 레벨 4는 반복하기가 ㄷ자 블록·바로 뒤 이동하기가 그 안쪽).
        /// 레벨 2(모든 단계의 재료 이름이 같음)와
        /// ingredientName이 빈 단계(예: 레벨 3의 논리 연결어)는 값 블록 없이 블록 이름만 쌓임.
        /// </summary>
        private void AddDesignItem(string ingredientId, string ingredientName, string matterLabel)
        {
            string previousIngredientId = _currentStepIndex > 0 && _confirmedIngredients != null && _currentStepIndex - 1 < _confirmedIngredients.Length
                ? _confirmedIngredients[_currentStepIndex - 1]
                : null;
            DesignStep step = ToDesignStep(ingredientId, ingredientName, matterLabel, previousIngredientId);
            _designSteps.Add(step);

            if (!_designPanel)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] designPanel이 null이라 확정 항목을 추가할 수 없음.");
                return;
            }

            _designPanel.AddItem(step.Shape, step.Command, step.Value);
        }

        /// <summary> 재료·고른 블록을 레벨 상태가 정한 설계창 블록(모양·명령 문구·값 문구)으로 바꿈. 레벨 상태가 없으면 명령+값 블록. </summary>
        private DesignStep ToDesignStep(string ingredientId, string ingredientName, string matterLabel, string previousIngredientId)
        {
            IIngredientSelectionLevelState state = CurrentLevelState;
            if (state == null) return new DesignStep(DesignStepShape.Command, ingredientName, matterLabel);

            (string command, string value) = state.GetDesignBlockTexts(this, ingredientName, matterLabel);
            return new DesignStep(state.GetDesignStepShape(this, ingredientId, previousIngredientId), command, value);
        }

        /// <summary> 설계창에 마지막으로 추가된 확정 항목을 지우고 코딩완료 버튼 상태를 갱신함. </summary>
        private void RemoveLastDesignItem()
        {
            if (_designSteps.Count > 0) _designSteps.RemoveAt(_designSteps.Count - 1);

            if (_designPanel) _designPanel.RemoveLastItem();
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] designPanel이 null이라 마지막 확정 항목을 지울 수 없음.");

            UpdateCodingCompleteButton();
        }

        /// <summary>
        /// 설계창을 시작하기 블록만 놓인 처음 상태로 되돌리고(레벨 상태가 정한, 이번 레벨 단계를 가장 길게 쌓았을 때의 블록 모양으로 블록 크기를 정함)
        /// 코딩완료 버튼 상태를 갱신함.
        /// </summary>
        private void ResetDesignPanel()
        {
            _designSteps.Clear();
            _plannedDesignShapes.Clear();
            if (CurrentLevelState != null) CurrentLevelState.FillPlannedDesignShapes(this, _plannedDesignShapes);
            else
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 레벨 상태가 없어 설계창 단계를 모두 명령 블록으로 셈.");
                for (int i = 0; i < _totalSteps; i++) _plannedDesignShapes.Add(DesignStepShape.Command);
            }

            if (_designPanel) _designPanel.Initialize(_plannedDesignShapes, CurrentLevelState == null || CurrentLevelState.UsesValueBlocks);
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] designPanel이 null이라 설계창을 초기화할 수 없음.");

            UpdateCodingCompleteButton();
        }

        /// <summary> 설계창 맨 아래에 완성하기 블록을 붙이고 연출이 끝날 때까지 기다림. </summary>
        private UniTask AttachEndBlockAsync(CancellationToken token)
        {
            if (_designPanel) return _designPanel.AttachEndBlockAsync(token);

            if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] designPanel이 null이라 완성하기 블록 없이 진행함.");
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 확정된 단계 수가 레벨 상태의 코딩 완료 조건(레벨 4·5는 1단계 이상, 나머지는 모든 단계)을 채웠고 카드가 떨어진 단계가 없을 때만
        /// 코딩완료 버튼을 활성화함.
        /// </summary>
        private void UpdateCodingCompleteButton()
        {
            if (!buttonCodingComplete)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] buttonCodingComplete가 null이라 코딩완료 버튼 상태를 갱신할 수 없음.");
                return;
            }

            bool reachedCompletion = CurrentLevelState?.IsCodingCompleteInteractable(this, _currentStepIndex, _totalSteps) ?? false;
            buttonCodingComplete.interactable = reachedCompletion && _idleReaderStepIndices.Count == 0;
        }

        /// <summary>
        /// 코딩완료 버튼 클릭 시 코딩 완료 효과음을 내고, 확정된 추진력(엔진 출력량 + 연료량 - 탑재 중량)을 목적지 조건과 대조해 성공/실패를 기록한 뒤 화면 페이드와 함께 결과 씬으로 전환함.
        /// </summary>
        private void OnCodingCompleteClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] sceneTransition이 null이라 {Constants.Scenes.Result} 씬을 로드할 수 없음.");
                return;
            }

            _isBusy = true;
            SoundEffects.Play(_soundManager, Constants.Sounds.CodingComplete, _logger);
            CompleteCodingAsync().Forget();
        }

        /// <summary>
        /// 미션을 판정해 결과를 기록한 뒤 결과 씬으로 전환함. 레벨별 추가 시뮬레이션(레벨 4)이 있으면 재생 후 전환함.
        /// </summary>
        private async UniTaskVoid CompleteCodingAsync()
        {
            LockGamePanel();

            bool success = EvaluateMission();
            if (_resultStore != null)
            {
                _resultStore.Result = success ? MissionResult.Success : MissionResult.Fail;
                _resultStore.FailVideoSuffix = !success && CurrentLevelState != null ? CurrentLevelState.GetFailVideoSuffix(this) : null;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] resultStore가 null이라 미션 결과를 기록할 수 없음.");
            }
            StoreResultDesigns(true);

            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] {PlayerSubject} 코딩 완료를 누름 — {_selectedLevel}레벨 {(success ? "성공" : "실패")}.");

            CancellationToken token = this.GetCancellationTokenOnDestroy();
            await AttachEndBlockAsync(token);

            if (CurrentLevelState != null)
            {
                await CurrentLevelState.PlayCompletionSimulationAsync(this, token);
            }

            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Result, _commonSettings.sceneTransitionFadeDuration).Forget();
        }

        /// <summary> 결과 화면으로 넘어가는 동안(레벨 4 시뮬레이션 포함) 게임 패널의 카드·버튼 입력을 막음. </summary>
        private void LockGamePanel()
        {
            if (gamePanel)
            {
                gamePanel.interactable = false;
                gamePanel.blocksRaycasts = false;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] gamePanel이 null이라 결과 화면으로 넘어가는 동안 입력을 막을 수 없음.");
            }
        }

        /// <summary>
        /// 스킵 버튼 클릭 시 클릭음을 내고 결과를 실패로 기록한 뒤 화면 페이드와 함께 결과 씬으로 전환함.
        /// </summary>
        private void OnSkipButtonClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] sceneTransition이 null이라 {Constants.Scenes.Result} 씬을 로드할 수 없음.");
                return;
            }

            if (_resultStore != null)
            {
                _resultStore.Result = MissionResult.Fail;
                _resultStore.FailVideoSuffix = null;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] resultStore가 null이라 스킵 결과를 기록할 수 없음.");
            }
            StoreResultDesigns(false);

            _isBusy = true;
            LockGamePanel();
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] {PlayerSubject} 건너뛰기를 누름 — {_selectedLevel}레벨 실패로 처리함.");
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Result, _commonSettings.sceneTransitionFadeDuration).Forget();
        }

        /// <summary>
        /// 결과 씬에서 보여 줄 설계를 결과 저장소에 기록함: 플레이어가 설계창에 쌓은 블록과 completed(코딩완료로 마쳤는지, 스킵이면 false),
        /// 설계창 배치 방식, 이번 판 문제의 정답(레벨 상태의 BuildSolution)을 설계창과 같은 블록으로 바꾼 것. 정답을 만들지 못하면 정답은 빈 목록이 기록됨.
        /// </summary>
        internal void StoreResultDesigns(bool completed)
        {
            if (_resultStore == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] resultStore가 null이라 플레이어·정답 설계를 기록할 수 없음.");
                return;
            }

            _resultStore.PlayerDesign = _designSteps.ToArray();
            _resultStore.PlayerDesignCompleted = completed;

            IIngredientSelectionLevelState state = CurrentLevelState;
            if (state == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 레벨 상태가 없어 정답 설계를 만들 수 없음. 빈 설계를 기록함.");
                _resultStore.SolutionDesign = Array.Empty<DesignStep>();
                return;
            }

            List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = state.BuildSolution(this);
            DesignStep[] steps = new DesignStep[solution.Count];
            for (int i = 0; i < solution.Count; i++)
            {
                string previousIngredientId = i > 0 ? solution[i - 1].ingredient.ingredientId : null;
                steps[i] = ToDesignStep(solution[i].ingredient.ingredientId, solution[i].ingredient.ingredientName, solution[i].matter.label, previousIngredientId);
            }

            _resultStore.SolutionDesign = steps;
        }

        /// <summary>
        /// 현재 레벨 상태 객체에 미션 판정을 위임함.
        /// </summary>
        private bool EvaluateMission()
        {
            if (!_missionBoard)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] missionBoard가 없어 미션을 판정할 수 없음. 실패로 처리함.");
                return false;
            }

            IIngredientSelectionLevelState state = CurrentLevelState;
            if (state == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 레벨 상태가 없어 미션을 판정할 수 없음. 실패로 처리함.");
                return false;
            }

            // 코딩완료 버튼을 켜는 조건(레벨 4는 1단계 이상, 나머지는 모든 단계)을 채우지 못했으면 실패로 처리함
            if (!state.IsCodingCompleteInteractable(this, _currentStepIndex, _totalSteps))
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 확정된 단계({_currentStepIndex}/{_totalSteps})가 {_selectedLevel}레벨 코딩 완료 조건에 못 미침. 실패로 처리함.");
                return false;
            }

            return state.EvaluateMission(this);
        }

        /// <summary> 확정된 추진력(레벨 1 외에는 0)을 미션보드 진행도(Image_Fill)에 반영함. </summary>
        private void ApplyProgressToMissionBoard()
        {
            if (_missionBoard)
            {
                _missionBoard.SetProgress(CurrentLevelState != null ? CurrentLevelState.CalculateConfirmedThrust(this) : 0);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] missionBoard가 null이라 진행도를 갱신할 수 없음.");
            }
        }

        /// <summary>
        /// 확정된 역할(엔진/연료/탑재)별 값에, 현재 조절 중인 임시 선택값을 대입해 미리보기용 추진력을 계산함.
        /// </summary>
        private int CalculatePreviewThrust()
        {
            return CurrentLevelState?.CalculatePreviewThrust(this) ?? 0;
        }

        /// <summary> 현재 좌우 버튼으로 선택 중인 물질을 반환함. 선택된 것이 없으면 null. </summary>
        internal RfidMatter CurrentSelectedMatter()
        {
            RfidMatter[] matters = _currentMatters;
            int idx = _currentMatterIndex.Value;
            return (matters != null && idx >= 0 && idx < matters.Length) ? matters[idx] : null;
        }

        /// <summary>
        /// 왼쪽 버튼 클릭 시 클릭음을 내고 세부 물질 인덱스를 이전으로 변경함.
        /// </summary>
        private void OnLeftButtonClicked()
        {
            RfidMatter[] matters = _currentMatters;
            if (matters == null || matters.Length == 0) return;

            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            int len = matters.Length;
            _currentMatterIndex.Value = (_currentMatterIndex.Value - 1 + len) % len;
        }

        /// <summary>
        /// 오른쪽 버튼 클릭 시 클릭음을 내고 세부 물질 인덱스를 다음으로 변경함.
        /// </summary>
        private void OnRightButtonClicked()
        {
            RfidMatter[] matters = _currentMatters;
            if (matters == null || matters.Length == 0) return;

            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            int len = matters.Length;
            _currentMatterIndex.Value = (_currentMatterIndex.Value + 1) % len;
        }

        /// <summary>
        /// 재료 표시 텍스트 컴포넌트 값을 변경함.
        /// </summary>
        private void UpdateIngredientText(string ingredientName)
        {
            if (textIngredient)
            {
                textIngredient.text = ingredientName;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] textIngredient가 null이라 재료 이름을 표시할 수 없음.");
            }

            UpdateRightArrowAnimation();
        }

        /// <summary>
        /// 현재 인덱스에 따라 물질 표시 텍스트 컴포넌트 값을 변경함.
        /// </summary>
        private void UpdateMatterText()
        {
            if (!textMatter)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] textMatter가 null이라 물질 값을 표시할 수 없음.");
                return;
            }

            RfidMatter[] matters = _currentMatters;
            int idx = _currentMatterIndex.Value;

            if (matters != null && idx >= 0 && idx < matters.Length)
            {
                textMatter.text = ApplyNumberSizeTag(matters[idx].label);
            }
            else
            {
                textMatter.text = "";
            }

            UpdateRightArrowAnimation();
        }

        /// <summary>
        /// 설정하기로 확정하기 전, 사용자가 좌우 버튼으로 엔진 출력량/연료량/탑재 중량 중 하나를 조절하는 동안
        /// 확정된 값 + 현재 조절 중인 임시 값을 결합한 추진력을 미리보기 게이지(Image_Fill_Preview)에 반영함.
        /// 현재 조절 중인 재료가 없으면 미리보기를 초기 상태로 되돌림.
        /// </summary>
        private void UpdateProgressPreview()
        {
            if (!_missionBoard)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] missionBoard가 null이라 미리보기 게이지를 갱신할 수 없음.");
                return;
            }

            // ingredientName이 빈 문자열인 단계도 있으므로, "조절 중인 재료 없음"은 ingredient가 아니라
            // matters 목록의 존재 여부로 판단함.
            if (_currentMatters == null || _currentMatters.Length == 0)
            {
                _missionBoard.ResetPreview();
                return;
            }

            _missionBoard.UpdatePreview(CalculatePreviewThrust());
        }

        /// <summary>
        /// 값이 숫자로만 구성되어 있으면 <size> 리치 텍스트 태그로 감싸 강조 크기를 적용하고, 아니면 원본 값을 그대로 반환함.
        /// </summary>
        internal string ApplyNumberSizeTag(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] value가 null이거나 비어 있어 숫자 크기 태그를 건너뜀.");
                return value;
            }

            bool isNumber = float.TryParse(value, out _);
            return isNumber ? ZString.Format("<size={0}>{1}</size>", _sceneSettings.numberFontSize, value) : value;
        }

        /// <summary>
        /// Text_Matter 또는 Text_Material에 값이 있는지(RFID 태그/디버그 키로 재료가 선택된 상태인지)에 따라
        /// 오른쪽 화살표 안내 연출을 시작하거나 멈춤.
        /// </summary>
        private void UpdateRightArrowAnimation()
        {
            if (!_rightArrowHint)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] rightArrowHint가 null이라 화살표 안내 연출을 건너뜀.");
                return;
            }

            bool hasValue = (textMatter && !string.IsNullOrEmpty(textMatter.text))
                          || (textIngredient && !string.IsNullOrEmpty(textIngredient.text));

            if (hasValue) _rightArrowHint.Play();
            else _rightArrowHint.Stop();
        }

        /// <summary> 테스트 전용: 인스펙터로 연결하는 레벨 4 보드를 넣음. </summary>
        internal void SetLevel4BoardForTest(Level4BoardController board) => level4Board = board;

        /// <summary> 테스트 전용: 인스펙터로 연결하는 레벨 2 진행바 이미지를 넣음. </summary>
        internal void SetLevel2FillImageForTest(Image fillImage) => level2FillImage = fillImage;

        /// <summary> 테스트 전용: 단계별로 확정된 블록 목록을 넣음. </summary>
        internal void SetConfirmedMattersForTest(RfidMatter[] matters) => _confirmedMatters = matters;

        /// <summary>
        /// 오브젝트 파괴 시 이벤트 구독 해제 및 리스너 정리.
        /// </summary>
        private void OnDestroy()
        {
            if (buttonLeft) buttonLeft.onClick.RemoveListener(OnLeftButtonClicked);
            if (buttonRight) buttonRight.onClick.RemoveListener(OnRightButtonClicked);
            if (buttonConfirm) buttonConfirm.onClick.RemoveListener(OnConfirmButtonClicked);
            if (buttonCancel) buttonCancel.onClick.RemoveListener(OnCancelButtonClicked);
            if (buttonCodingComplete) buttonCodingComplete.onClick.RemoveListener(OnCodingCompleteClicked);
            if (buttonSkip) buttonSkip.onClick.RemoveListener(OnSkipButtonClicked);

            _disposables.Dispose();
            _stateMachine?.Dispose();
            _currentIngredient?.Dispose();
            _currentMatterIndex?.Dispose();
        }
    }
}
