using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
using VContainer.Unity;
using HuliacDev.Core;
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

        [Header("Right Arrow Hint")]
        [SerializeField] private Image[] rightArrowImages; // Arrows/Image_Arrow1..5 순서

        [Header("Workflow Buttons")]
        [SerializeField] private Button buttonConfirm;
        [SerializeField] private Button buttonCancel;
        [SerializeField] private Button buttonCodingComplete;
        [SerializeField] private Button buttonSkip;

        [Header("Design Panel")]
        [SerializeField] private Transform designContent;
        [SerializeField] private TextMeshProUGUI designItemPrefab;

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

        [Header("Warning")]
        [SerializeField] private CanvasGroup warningPanel; // Image_Warning: 레벨/스텝에 맞지 않는 카드 인식 시 표시

        [Header("Level 4 Board")]
        [SerializeField] private Level4BoardController level4Board; // Panel_Level4. Level4BoardController가 이 클래스를 주입받는 순환 의존이라 VContainer 대신 인스펙터로 연결함

        // 3_Game.json / 00_Common.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)
        private readonly float numberFontSize = 45f; // Text_Matter/DesignItem 값이 숫자일 때 강조용 폰트 크기
        private readonly float rightArrowStepFadeDuration = 0.15f;
        private readonly float rightArrowFadeOutDuration = 0.3f;
        private readonly float rightArrowHoldDuration = 0.5f;
        private readonly float level2FillTweenDuration = 0.45f;
        private readonly float level2FillOvershoot = 1.2f; // Ease.OutBack 오버슈트 크기. 기본(1.70158)보다 작게 둬 과하게 튀지 않도록 함
        private readonly float level3GaugeTweenDuration = 0.4f;
        private readonly float level3IconBlinkMinAlpha = 0.25f; // "또는" 선택 시 불안정하게 깜빡이는 최소 알파
        private readonly float level3IconBlinkDuration = 0.12f; // 깜빡임 한쪽 방향 소요 시간(짧을수록 더 불안정해 보임)
        private readonly float sceneFadeDuration = 0.5f;
        private readonly float warningFadeDuration = 0.25f;
        private readonly float warningShakeAmount = 15f;
        private readonly float warningShakeCycleDuration = 0.08f;
        private readonly float warningHoldDuration = 1.0f;

        private StateMachine<IngredientSelectionController> _stateMachine;
        private IngredientLevel1State _level1State;
        private IngredientLevel2State _level2State;
        private IngredientLevel3State _level3State;
        private IngredientLevel4State _level4State;

        /// <summary> 현재 활성화된 레벨 상태 객체. </summary>
        public IIngredientSelectionLevelState CurrentLevelState => _stateMachine?.CurrentState as IIngredientSelectionLevelState;

        internal Image[] StepBallImages => stepBallImages;
        internal Material StepBallGrayscaleMaterial => stepBallGrayscaleMaterial;
        internal Image Level2FillImage => level2FillImage;
        internal float Level2FillTweenDuration => _sceneSettings?.level2FillTweenDuration ?? level2FillTweenDuration;
        internal float Level2FillOvershoot => _sceneSettings?.level2FillOvershoot ?? level2FillOvershoot;

        internal Image Level3OxygenGauge => level3OxygenGauge;
        internal Image Level3ElectricGauge => level3ElectricGauge;
        internal Image Level3OxygenIcon => level3OxygenIcon;
        internal Image Level3ElectricIcon => level3ElectricIcon;
        internal CanvasGroup Level3OxygenIconCanvasGroup => _level3OxygenIconCanvasGroup;
        internal CanvasGroup Level3ElectricIconCanvasGroup => _level3ElectricIconCanvasGroup;
        internal float Level3GaugeTweenDuration => _sceneSettings?.level3GaugeTweenDuration ?? level3GaugeTweenDuration;
        internal float Level3IconBlinkMinAlpha => _sceneSettings?.level3IconBlinkMinAlpha ?? level3IconBlinkMinAlpha;
        internal float Level3IconBlinkDuration => _sceneSettings?.level3IconBlinkDuration ?? level3IconBlinkDuration;

        internal Level4BoardController Level4Board => level4Board;
        internal MissionBoardController MissionBoard => _missionBoard;
        internal ILogger<IngredientSelectionController> Logger => _logger;
        internal RfidMatter[] ConfirmedMatters => _confirmedMatters;
        internal string[] ConfirmedIngredients => _confirmedIngredients;
        internal RfidLevelMapping LevelMapping => _levelMapping;
        internal RfidStepDefinition[] StepDefinitions => _stepDefinitions;
        internal int CurrentStepIndex => _currentStepIndex;
        internal int TotalSteps => _totalSteps;
        internal int ConfirmedEngineValue => _confirmedEngineValue;
        internal int ConfirmedFuelValue => _confirmedFuelValue;
        internal int ConfirmedPayloadValue => _confirmedPayloadValue;

        private ISubscriber<RfidTagEvent> _subscriber;
        private ISubscriber<RfidReaderIdleEvent> _idleSubscriber;
        private SelectedLevelStore _selectedLevelStore;
        private SceneTransitionService _sceneTransition;
        private MissionBoardController _missionBoard;
        private CodingCategoryIndicatorController _codingCategoryIndicator;
        private GameResultStore _resultStore;
        private IObjectResolver _resolver;
        private ILogger<IngredientSelectionController> _logger;
        private bool _isBusy;

        private int _selectedLevel = 1;     // 현재 레벨 (디자인 항목 표시 형식 분기 등에 사용)
        private int _currentStepIndex = 0;  // 현재 read 인덱스 (0 ~ _totalSteps-1)
        private int _totalSteps = 3;        // 현재 스테이지에서 찍어야 하는 총 read 횟수
        private int _currentStageIndex = 0; // 현재 스테이지 (0부터 시작)
        private int[] _stageReadCounts = { 3 }; // 스테이지별 read 횟수 (JSON stageReadCounts, steps 미설정 시 폴백)
        private RfidLevelMapping _levelMapping; // 현재 레벨의 블록 목록(matterSets)·단계 정의. 단계가 고를 블록은 matterSetId로 여기서 찾음
        private RfidStepDefinition[] _stepDefinitions; // "동작" 카드를 찍을 때마다 순서대로 진행되는 재료 목록 (추진체 종류 -> 탑재 종류 -> 연료량)
        private RfidStepDefinition[] _categoryIngredients; // 카드 분류로 재료가 정해지는 레벨(레벨 4)의 분류별 재료 정의
        private RfidMatter[] _confirmedMatters;
        private string[] _confirmedIngredients; // 각 스탭에서 확정된 재료의 ingredientId
        private string _currentIngredientId; // 현재 대기 중인(아직 확정 안 된) 재료의 ingredientId. 화면 표시용 이름은 _currentIngredient
        private string[] _confirmedCategories; // 각 스탭을 확정시킨 카드의 category(동작/제어/논리/함수). 리더기별 스탭 라우팅에서 카드 변경 감지에 사용
        private string _currentCategory; // 현재 대기 중인(아직 확정 안 된) 태그의 category. Confirm 시 _confirmedCategories에 기록됨

        // 리더기별 스탭 라우팅: readerId("Reader_N")가 N번째 스탭에 고정 배정됨. 설정된 리더기가 1대뿐이면(현재)
        // 라우팅을 적용하지 않고 기존처럼 아무 리더기의 태그나 현재 스탭에 적용함. 2대 이상부터 활성화됨.
        private int _readerCount = 1;

        // 이미 확정된 스탭의 카드가 리더기에서 떨어져(RfidReaderIdleEvent) 값이 불확실해진 스탭 인덱스 목록.
        // 여기 포함된 스탭부터 이후 DesignItem이 흐리게(비활성) 표시됨. 카드가 다시 인식되면 해당 인덱스가 제거됨.
        private readonly HashSet<int> _idleReaderStepIndices = new HashSet<int>();
        private const float DesignItemDeactivatedAlpha = 0.35f;

        // 추진력 계산식(엔진 출력량 + 연료량 - 탑재 중량)에 쓰이는 역할별 확정 값. 미확정 상태의 기본값은 0.
        private int _confirmedEngineValue = 0;
        private int _confirmedFuelValue = 0;
        private int _confirmedPayloadValue = 0;

        // 디자인 컨테이너에 동적으로 추가된 확정 항목 텍스트 목록
        private readonly List<TMP_Text> _designItems = new List<TMP_Text>();

        // R3 반응형 상태 관리
        private readonly ReactiveProperty<string> _currentIngredient = new ReactiveProperty<string>("");
        private readonly ReactiveProperty<RfidMatter[]> _currentMatters = new ReactiveProperty<RfidMatter[]>(Array.Empty<RfidMatter>());
        private readonly ReactiveProperty<int> _currentMatterIndex = new ReactiveProperty<int>(0);

        private R3.DisposableBag _disposables = new R3.DisposableBag();
        private Sequence _rightArrowSequence;
        private Sequence _warningSequence;
        private CancellationTokenSource _warningCts;
        private CanvasGroup[] _rightArrowCanvasGroups;
        private CanvasGroup _level3OxygenIconCanvasGroup;
        private CanvasGroup _level3ElectricIconCanvasGroup;

        // 3_Game.json / 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private GameSceneSettings _sceneSettings;
        private CommonSettings _commonSettings;

        /// <summary>
        /// 상태 머신과 레벨별 상태 인스턴스를 사전 생성하고(Zero-GC), 서브 캔버스 및 캔버스 그룹을 초기화함.
        /// </summary>
        private void Awake()
        {
            _level1State = new IngredientLevel1State();
            _level2State = new IngredientLevel2State();
            _level3State = new IngredientLevel3State();
            _level4State = new IngredientLevel4State();
            _stateMachine = new StateMachine<IngredientSelectionController>(this);

            InitSubCanvases();
            InitCanvasGroups();
        }

        /// <summary> fillAmount가 트윈될 때 메인 UI 캔버스의 리빌드를 차단하도록 서브 캔버스를 보장함. </summary>
        private void InitSubCanvases()
        {
            EnsureSubCanvas(level2FillImage);
            EnsureSubCanvas(level3OxygenGauge);
            EnsureSubCanvas(level3ElectricGauge);
        }

        private static void EnsureSubCanvas(Component target)
        {
            if (!target) return;
            if (!target.TryGetComponent<Canvas>(out _))
            {
                target.gameObject.AddComponent<Canvas>();
            }
        }

        /// <summary> 이미지 알파 페이드/깜빡임 시 정점 리빌드 없이 GPU 블렌딩을 사용하도록 CanvasGroup을 구성함. </summary>
        private void InitCanvasGroups()
        {
            if (rightArrowImages != null && rightArrowImages.Length > 0)
            {
                _rightArrowCanvasGroups = new CanvasGroup[rightArrowImages.Length];
                for (int i = 0; i < rightArrowImages.Length; i++)
                {
                    if (rightArrowImages[i])
                    {
                        if (!rightArrowImages[i].TryGetComponent(out _rightArrowCanvasGroups[i]))
                        {
                            _rightArrowCanvasGroups[i] = rightArrowImages[i].gameObject.AddComponent<CanvasGroup>();
                        }
                        _rightArrowCanvasGroups[i].alpha = 0f;
                    }
                }
            }

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
        /// VContainer 의존성 주입. MessagePipe 구독자(카드 인식/카드 떨어짐), 씬 전환 서비스, 디자인 항목 생성용 리졸버, 로거를 할당함.
        /// </summary>
        [Inject]
        public void Construct(ISubscriber<RfidTagEvent> subscriber, ISubscriber<RfidReaderIdleEvent> idleSubscriber, SelectedLevelStore selectedLevelStore, SceneTransitionService sceneTransition, MissionBoardController missionBoard, CodingCategoryIndicatorController codingCategoryIndicator, GameResultStore resultStore, IObjectResolver resolver, ILogger<IngredientSelectionController> logger)
        {
            _subscriber = subscriber;
            _idleSubscriber = idleSubscriber;
            _selectedLevelStore = selectedLevelStore;
            _sceneTransition = sceneTransition;
            _missionBoard = missionBoard;
            _codingCategoryIndicator = codingCategoryIndicator;
            _resultStore = resultStore;
            _resolver = resolver;
            _logger = logger;
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
            ResetRightArrow();
            InitializeWarningPanel();

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
                RfidSettings settings = await JsonLoader.LoadAsync<RfidSettings>(Constants.Files.RfidMappings, token);
                if (settings != null && settings.stageReadCounts != null && settings.stageReadCounts.Length > 0)
                {
                    _stageReadCounts = settings.stageReadCounts;
                }

                _readerCount = (settings?.readers != null && settings.readers.Length > 0) ? settings.readers.Length : 1;

                int level = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;
                _selectedLevel = level;
                IIngredientSelectionLevelState targetState = level switch
                {
                    1 => _level1State,
                    2 => _level2State,
                    3 => _level3State,
                    4 => _level4State,
                    _ => _level1State
                };
                _stateMachine.ChangeState(targetState);
                ValidateMappings(settings);

                _levelMapping = settings != null ? settings.FindLevelMapping(level) : null;
                _stepDefinitions = _levelMapping?.steps;
                _categoryIngredients = _levelMapping?.categoryIngredients;

                _currentStageIndex = Mathf.Clamp(_currentStageIndex, 0, _stageReadCounts.Length - 1);
                _totalSteps = (_stepDefinitions != null && _stepDefinitions.Length > 0)
                    ? _stepDefinitions.Length
                    : _stageReadCounts[_currentStageIndex];

                if ((_stepDefinitions == null || _stepDefinitions.Length == 0) && _logger != null)
                {
                    _logger.ZLogWarning($"[IngredientSelectionController] RfidMappings.json에 {level}레벨 단계(steps) 정의가 없어 stageReadCounts의 {_totalSteps}회로 진행함.");
                }
            }
            catch (Exception e)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] 워크플로우용 RfidMappings.json 로드 실패: {e.Message}");
            }

            _confirmedMatters = new RfidMatter[_totalSteps];
            _confirmedIngredients = new string[_totalSteps];
            _confirmedCategories = new string[_totalSteps];
            _idleReaderStepIndices.Clear();
            ClearDesignItems();
            InitializeStepBalls();
            UpdateCategoryHint();

            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] {_selectedLevel}레벨 워크플로우 초기화 완료: 총 {_totalSteps}회 read 필요.");
        }

        /// <summary>
        /// RfidMappings.json의 레벨 1~4 블록 정의를 검사해 문제마다 오류 로그를 남김. 블록 목록이 비었거나 값이 빠지면 게임 중에는
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
        /// </summary>
        private void OnRfidTagReceived(RfidTagEvent evt)
        {
            // 게임 패널이 활성 상태가 아니면(스토리 화면 등) RFID 입력을 무시함
            if (!gamePanel || !gamePanel.interactable)
            {
                if (_logger != null)
                {
                    _logger.ZLogInformation($"[IngredientSelectionController] 게임 패널이 비활성 상태라 {evt.ReaderId} 태그를 무시함.");
                }
                return;
            }

            // 리더기별 스탭 라우팅: 설정된 리더기가 2대 이상일 때만 적용함(1대뿐이면 기존처럼 어떤 리더기든 현재 스탭에 적용).
            // "Reader_N" 형식이 아닌 readerId(디버그 키보드 시뮬레이터의 "Keyboard", 미등록 리더기의 "Unknown_...")는
            // GetStepIndexForReader가 -1을 반환하므로 라우팅 없이 기존처럼 현재 스탭에 적용됨.
            if (_readerCount > 1)
            {
                int readerStepIndex = GetStepIndexForReader(evt.ReaderId);

                // 카드가 다시 인식됐으므로(어떤 category든) 흐리게 표시돼 있었다면 정상 표시로 되돌림
                bool wasIdle = readerStepIndex >= 0 && _idleReaderStepIndices.Remove(readerStepIndex);
                if (wasIdle)
                {
                    UpdateDesignItemGrayState();
                }

                if (readerStepIndex >= 0 && readerStepIndex < _currentStepIndex)
                {
                    // 카드가 잠깐 떨어졌다가(idle) 같은 category의 카드가 다시 올라온 것뿐이면 확정된 값은
                    // 그대로 유효하므로 되돌리지 않고 복구만 함(파괴적 롤백 방지).
                    string previousCategory = (_confirmedCategories != null && readerStepIndex < _confirmedCategories.Length)
                        ? _confirmedCategories[readerStepIndex] : null;

                    if (wasIdle && string.Equals(previousCategory, evt.Category, StringComparison.Ordinal))
                    {
                        if (_logger != null)
                        {
                            _logger.ZLogInformation($"[IngredientSelectionController] {evt.ReaderId}(스탭 {readerStepIndex + 1})에 동일 카테고리({evt.Category}) 카드가 다시 인식되어 정상 상태로 복구함.");
                        }
                        return;
                    }

                    // 그 외(카드를 떼지 않은 채 다른 카드로 교체했거나, 떨어졌다가 다른 category로 바뀐 경우)는
                    // 이미 확정된 스탭을 담당하는 리더기에서 변경이 감지된 것이므로, 그 스탭부터 되돌린 뒤
                    // 아래 로직에서 이 태그를 그 스탭의 새 입력으로 처리함.
                    HandleConfirmedStepCardChanged(readerStepIndex, evt.Category);
                }
                else if (readerStepIndex > _currentStepIndex)
                {
                    // 아직 도달하지 않은(활성화되지 않은) 스탭의 리더기 -> 무시
                    if (_logger != null)
                    {
                        _logger.ZLogInformation($"[IngredientSelectionController] {evt.ReaderId}(스탭 {readerStepIndex + 1})은 아직 활성화되지 않아 태그를 무시함(현재 {_currentStepIndex + 1}번째 진행 중).");
                    }
                    return;
                }
            }

            // 모든 단계가 완료되면 더 이상 태그를 받지 않음
            if (_currentStepIndex >= _totalSteps)
            {
                if (_logger != null)
                {
                    _logger.ZLogInformation($"[IngredientSelectionController] 모든 단계가 완료되어 {evt.ReaderId} 태그를 무시함.");
                }
                return;
            }

            if (_stepDefinitions == null || _currentStepIndex >= _stepDefinitions.Length || _stepDefinitions[_currentStepIndex] == null)
            {
                if (_logger != null)
                {
                    _logger.ZLogWarning($"[IngredientSelectionController] {_currentStepIndex}번 인덱스에 대한 단계 정의가 없어 {evt.ReaderId} 태그를 무시함.");
                }
                return;
            }

            RfidStepDefinition step = _stepDefinitions[_currentStepIndex];

            if (!IsCategoryAllowedForStep(step, evt.Category))
            {
                if (_logger != null)
                {
                    _logger.ZLogInformation($"[IngredientSelectionController] '{evt.Category}' 카테고리는 {_currentStepIndex + 1}번째 단계({step.ingredientName})에서 허용되지 않아 {evt.ReaderId} 태그를 무시함.");
                }
                ShowInvalidCategoryWarningAsync().Forget();
                return;
            }

            if (CurrentLevelState != null && !CurrentLevelState.ValidateTagCategory(this, evt))
            {
                return;
            }

            RfidStepDefinition ingredient = CurrentLevelState != null
                ? CurrentLevelState.ResolveStepCard(this, step, evt.Category)
                : step;

            if (ingredient == null)
            {
                if (_logger != null)
                {
                    _logger.ZLogWarning($"[IngredientSelectionController] '{evt.Category}' 카드에 해당하는 재료 정의가 RfidMappings.json에 없어 {evt.ReaderId} 태그를 무시함.");
                }
                return;
            }

            RfidMatter[] matters = _levelMapping?.FindMatters(ingredient.matterSetId);
            if (matters == null || matters.Length == 0)
            {
                if (_logger != null)
                {
                    _logger.ZLogWarning($"[IngredientSelectionController] {ingredient.ingredientName}({ingredient.ingredientId})의 블록 목록 '{ingredient.matterSetId}'가 RfidMappings.json에 없거나 비어 있어 {evt.ReaderId} 태그를 무시함.");
                }
                return;
            }

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] {evt.ReaderId} 리더기 태그를 {_currentStepIndex + 1}번째 단계에 적용함: {ingredient.ingredientName}({ingredient.ingredientId})");
            }

            // 현재 단계에 허용된 카드로 확인된 경우에만 CodingCategories 강조를 갱신함(허용되지 않으면 흑백 상태가 그대로 유지됨)
            if (_codingCategoryIndicator) _codingCategoryIndicator.HighlightCategory(evt.Category);
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] codingCategoryIndicator가 null이라 카테고리 강조를 갱신할 수 없음.");

            _currentIngredient.Value = ingredient.ingredientName ?? "";
            _currentIngredientId = ingredient.ingredientId;
            _currentCategory = evt.Category;
            _currentMatters.Value = CurrentLevelState != null
                ? CurrentLevelState.FilterMatters(this, ingredient.ingredientId, matters)
                : ExcludeConfirmedMatters(ingredient.ingredientId, matters);
            _currentMatterIndex.Value = 0;
            UpdateMatterText();
            UpdateProgressPreview();
        }

        /// <summary> "Reader_N" 형식의 readerId에서 0-기반 스탭 인덱스(N-1)를 추출함. 형식이 안 맞으면(디버그/미등록 리더기) -1을 반환함. </summary>
        private static int GetStepIndexForReader(string readerId)
        {
            if (string.IsNullOrEmpty(readerId)) return -1;

            int underscoreIndex = readerId.LastIndexOf('_');
            if (underscoreIndex < 0 || underscoreIndex == readerId.Length - 1) return -1;

            string suffix = readerId.Substring(underscoreIndex + 1);
            return int.TryParse(suffix, out int readerNumber) ? readerNumber - 1 : -1;
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
        /// 이미 확정된 스탭을 담당하는 리더기에서 카드가 떨어졌을(RfidReaderIdleEvent) 때 호출됨. 그 값이 더 이상
        /// 확실하지 않다는 걸 시각적으로 알리기 위해, 해당 스탭 이후의 DesignItem을 흐리게(비활성) 표시함.
        /// 카드가 다시 인식되면(OnRfidTagReceived) 자동으로 정상 표시로 되돌아감. 리더기 1대뿐이면(현재) 그 1대가
        /// 항상 "지금 진행 중인" 스탭이라 카드를 떼는 것 자체가 일반적인 조작 흐름이므로 이 기능을 적용하지 않음.
        /// </summary>
        private void OnRfidReaderIdle(RfidReaderIdleEvent evt)
        {
            if (_readerCount <= 1) return;
            if (_confirmedMatters == null) return;

            int stepIndex = GetStepIndexForReader(evt.ReaderId);
            if (stepIndex < 0 || stepIndex >= _currentStepIndex) return; // 매핑 안 되거나 아직 확정 안 된 스탭은 무시

            if (_idleReaderStepIndices.Add(stepIndex))
            {
                if (_logger != null)
                {
                    _logger.ZLogInformation($"[IngredientSelectionController] {evt.ReaderId}(스탭 {stepIndex + 1})의 카드가 떨어져 이후 항목을 비활성 표시로 전환함.");
                }
                UpdateDesignItemGrayState();
            }
        }

        /// <summary>
        /// _idleReaderStepIndices 중 가장 이른 인덱스부터 끝까지 DesignItem의 알파를 낮춰 흐리게(비활성) 표시하고,
        /// 그 앞쪽은 정상 알파로 되돌림. 값 자체는 바꾸지 않고 시각적 표시만 담당함.
        /// </summary>
        private void UpdateDesignItemGrayState()
        {
            int grayFromIndex = int.MaxValue;
            foreach (int idx in _idleReaderStepIndices)
            {
                if (idx < grayFromIndex) grayFromIndex = idx;
            }

            for (int i = 0; i < _designItems.Count; i++)
            {
                if (!_designItems[i]) continue;
                _designItems[i].alpha = (i >= grayFromIndex) ? DesignItemDeactivatedAlpha : 1f;
            }
        }

        /// <summary> 레벨 4 전용: 바로 이전 단계에서 확정한 재료가 "반복하기"(제어, 횟수 카드)였다면, 이번 단계는 반드시 "이동하기"(동작)여야 함. </summary>
        internal bool IsRepeatFollowUpRequired()
        {
            if (_currentStepIndex <= 0 || _confirmedIngredients == null || _currentStepIndex - 1 >= _confirmedIngredients.Length) return false;
            return string.Equals(_confirmedIngredients[_currentStepIndex - 1], Constants.RfidIds.Level4.Repeat, StringComparison.Ordinal);
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

        /// <summary> 레벨 4 전용: 찍은 카드 분류(동작/제어)로 고를 재료 정의를 categoryIngredients에서 찾음. 없으면 null. </summary>
        internal RfidStepDefinition FindCategoryIngredient(string category)
        {
            if (_categoryIngredients == null) return null;

            foreach (RfidStepDefinition ingredient in _categoryIngredients)
            {
                if (ingredient != null && IsCategoryAllowedForStep(ingredient, category)) return ingredient;
            }

            return null;
        }

        /// <summary> 해당 단계가 허용하는 category 목록에 주어진 category가 포함되는지 검사함. </summary>
        private bool IsCategoryAllowedForStep(RfidStepDefinition step, string category)
        {
            if (step.categories == null || step.categories.Length == 0) return false;

            for (int i = 0; i < step.categories.Length; i++)
            {
                if (string.Equals(step.categories[i], category, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary> Image_Warning의 알파를 0으로 스냅해 시작 시 숨겨진 상태로 만듦. </summary>
        private void InitializeWarningPanel()
        {
            if (!warningPanel)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] warningPanel이 null이라 경고 표시가 비활성화됨.");
                return;
            }

            warningPanel.alpha = 0f;
            warningPanel.interactable = false;
            warningPanel.blocksRaycasts = false;
        }

        /// <summary>
        /// 현재 레벨/스텝에서 허용되지 않는 카드가 인식됐을 때: Image_Warning을 페이드인하고, 게임 패널(GamePanel)을
        /// 좌우로 3회 흔든 뒤, warningHoldDuration(초)만큼 더 붙잡아 보여주고 나서 Image_Warning을 페이드아웃해 숨김.
        /// 연달아 잘못된 카드가 인식되면 진행 중이던 연출을 정지하고 처음부터 다시 시작함.
        /// </summary>
        internal async UniTaskVoid ShowInvalidCategoryWarningAsync()
        {
            if (!warningPanel)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] warningPanel이 null이라 잘못된 카드 경고를 표시할 수 없음.");
                return;
            }

            // 이전 호출의 UniTask.Delay 등 진행 중이던 비동기 흐름을 확실히 취소함(_warningSequence.Kill()만으로는
            // DOTween 트윈만 멈출 뿐, 이전 호출이 대기 중인 await까지 중단시키진 못해 레이스 컨디션이 발생할 수 있음).
            _warningCts?.Cancel();
            _warningCts?.Dispose();
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            _warningCts = cts;
            CancellationToken token = cts.Token;
            float fadeDuration = _sceneSettings?.warningFadeDuration ?? warningFadeDuration;

            try
            {
                _warningSequence?.Kill();
                if (gamePanel) ((RectTransform)gamePanel.transform).anchoredPosition = Vector2.zero;

                warningPanel.interactable = false;
                warningPanel.blocksRaycasts = false;
                await warningPanel.DOFade(1f, fadeDuration).SetUpdate(true)
                    .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);

                await ShakeGamePanelAsync(token);

                float holdDuration = _sceneSettings?.warningHoldDuration ?? warningHoldDuration;
                await UniTask.Delay(TimeSpan.FromSeconds(holdDuration), DelayType.UnscaledDeltaTime, cancellationToken: token);

                await warningPanel.DOFade(0f, fadeDuration).SetUpdate(true)
                    .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                // 더 최신 경고 연출이 이미 시작되어 필드가 교체됐다면 그 CTS는 건드리지 않음
                if (_warningCts == cts)
                {
                    _warningCts.Dispose();
                    _warningCts = null;
                }
            }
        }

        /// <summary> GamePanel의 RectTransform을 좌우로 3회(왕복) 흔들고 원위치로 되돌림. </summary>
        private UniTask ShakeGamePanelAsync(CancellationToken token)
        {
            if (!gamePanel)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] gamePanel이 null이라 흔들기 연출을 건너뜀.");
                return UniTask.CompletedTask;
            }

            RectTransform target = (RectTransform)gamePanel.transform;
            Vector2 originalPos = target.anchoredPosition;
            float amount = _sceneSettings?.warningShakeAmount ?? warningShakeAmount;
            float cycleDuration = _sceneSettings?.warningShakeCycleDuration ?? warningShakeCycleDuration;
            float half = cycleDuration / 2f;

            _warningSequence?.Kill();
            _warningSequence = DOTween.Sequence().SetUpdate(true);
            for (int i = 0; i < 3; i++)
            {
                _warningSequence.Append(target.DOAnchorPos(originalPos + new Vector2(amount, 0f), half).SetEase(Ease.InOutSine));
                _warningSequence.Append(target.DOAnchorPos(originalPos - new Vector2(amount, 0f), half).SetEase(Ease.InOutSine));
            }
            _warningSequence.Append(target.DOAnchorPos(originalPos, half).SetEase(Ease.InOutSine));

            return _warningSequence.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
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
        /// 설정하기(Confirm) 버튼 클릭 시 현재 선택한 물질을 확정하고 다음 단계로 진행함.
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
            RfidMatter[] matters = _currentMatters.Value;
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
                _logger.ZLogInformation($"[IngredientSelectionController] {_currentStepIndex + 1}번째 단계 확정: {ingredientName}({ingredientId}) -> {chosenMatter.label}({chosenMatter.id}, 값={chosenMatter.value})");
            }

            // 디자인 컨테이너에 확정 항목을 자식으로 추가
            AddDesignItem(ingredientName, chosenMatter.label);

            // 레벨별 상태 객체에 확정 처리 위임 (스텝 볼, 게이지, 불안정 깜빡임, 추진력 등)
            CurrentLevelState?.OnStepConfirmed(this, _currentStepIndex, ingredientId, chosenMatter);

            // 확정된 엔진 출력량/연료량/탑재 중량을 계산식에 반영해 진행도(Image_Fill)를 갱신함
            ApplyProgressToMissionBoard();

            ClearPendingSelection();

            // 다음 단계로 인덱스 증가
            _currentStepIndex++;

            // 다음 단계가 남아있으면 그 단계의 카테고리 힌트를 다시 페이드로 안내하고, 없으면 힌트를 멈춤
            // (동작 확정으로 켜졌던 CodingCategories 강조도 여기서 함께 흑백으로 정리됨)
            UpdateCategoryHint();

            if (_currentStepIndex >= _totalSteps && _logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] 총 {_totalSteps}단계 모두 완료됨!");
            }
        }

        /// <summary>
        /// 취소하기(Cancel) 버튼 클릭 시 현재 대기 상태를 비우고 이전 단계로 되돌아감.
        /// </summary>
        private void OnCancelButtonClicked()
        {
            if (_confirmedMatters == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 워크플로우 초기화가 끝나지 않아 취소하기를 처리할 수 없음.");
                return;
            }

            if (_currentStepIndex == 0)
            {
                // 1단계(Reader 1)인 경우 현재 태그된 임시 선택값만 클리어
                ClearPendingSelection();
                UpdateCategoryHint();
                return;
            }

            RollbackOneStep();

            ApplyProgressToMissionBoard();

            ClearPendingSelection();

            // 되돌아간 단계의 카테고리 힌트를 다시 페이드로 안내함
            UpdateCategoryHint();

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] {_currentStepIndex + 1}번째 단계로 되돌림 (Reader_{_currentStepIndex + 1} 대기 중)");
            }
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
            _currentMatters.Value = Array.Empty<RfidMatter>();
            _currentMatterIndex.Value = 0;
            UpdateMatterText();
            UpdateProgressPreview();
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

            _idleReaderStepIndices.Remove(_currentStepIndex); // 해당 스탭의 DesignItem이 곧 제거되므로 흐림 표시 추적 대상에서도 제외
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

            UpdateDesignItemGrayState(); // 남아있는 DesignItem의 흐림 표시 경계를 다시 계산함
            ClearPendingSelection();
        }

        /// <summary>
        /// 확정된 재료/물질을 "· 재료 [물질]" 형태의 Text로 만들어 디자인 컨테이너의 자식으로 추가함. 물질은 노란색으로 표시함.
        /// 레벨 2는 모든 단계의 ingredientName이 "발사 코딩 순서"로 동일해 매번 반복 표시할 필요가 없고,
        /// ingredientName이 빈 문자열인 단계(예: 레벨 3의 논리 연결어)도 재료 이름 없이 "· [물질]" 형태로만 표시함.
        /// </summary>
        private void AddDesignItem(string ingredientName, string matterLabel)
        {
            if (!designContent || !designItemPrefab)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] designContent 또는 designItemPrefab이 null이라 확정 항목을 추가할 수 없음.");
                return;
            }

            if (_resolver == null)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] IObjectResolver가 주입되지 않아 확정 항목을 생성할 수 없음.");
                return;
            }

            TextMeshProUGUI text = _resolver.Instantiate(designItemPrefab, designContent);
            text.text = CurrentLevelState != null
                ? CurrentLevelState.FormatDesignItemText(this, ingredientName, matterLabel)
                : $" · {ingredientName} [<color=yellow>{ApplyNumberSizeTag(matterLabel)}</color>]";

            _designItems.Add(text);
            UpdateCodingCompleteButton();
        }

        /// <summary>
        /// 디자인 컨테이너에 마지막으로 추가된 확정 항목을 제거함.
        /// </summary>
        private void RemoveLastDesignItem()
        {
            if (_designItems.Count == 0) return;

            int lastIndex = _designItems.Count - 1;
            TMP_Text last = _designItems[lastIndex];
            _designItems.RemoveAt(lastIndex);
            if (last) Destroy(last.gameObject);
            UpdateCodingCompleteButton();
        }

        /// <summary>
        /// 디자인 컨테이너에 추가된 모든 확정 항목을 제거함.
        /// </summary>
        private void ClearDesignItems()
        {
            for (int i = 0; i < _designItems.Count; i++)
            {
                if (_designItems[i]) Destroy(_designItems[i].gameObject);
            }
            _designItems.Clear();
            UpdateCodingCompleteButton();
        }

        /// <summary>
        /// 디자인 컨테이너에 확정 항목이 필요한 만큼 채워졌을 때만 코딩완료 버튼을 활성화함.
        /// </summary>
        private void UpdateCodingCompleteButton()
        {
            if (!buttonCodingComplete)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] buttonCodingComplete가 null이라 코딩완료 버튼 상태를 갱신할 수 없음.");
                return;
            }

            buttonCodingComplete.interactable = CurrentLevelState?.IsCodingCompleteInteractable(this, _designItems.Count, _totalSteps) ?? false;
        }

        /// <summary>
        /// 코딩완료 버튼 클릭 시 확정된 추진력(엔진 출력량 + 연료량 - 탑재 중량)을 목적지 조건과 대조해 성공/실패를 기록하고, 화면 페이드와 함께 결과 씬으로 전환함.
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
            CompleteCodingAsync().Forget();
        }

        /// <summary>
        /// 미션을 판정해 결과를 기록한 뒤 결과 씬으로 전환함. 레벨별 추가 시뮬레이션(레벨 4)이 있으면 재생 후 전환함.
        /// </summary>
        private async UniTaskVoid CompleteCodingAsync()
        {
            if (gamePanel)
            {
                gamePanel.interactable = false;
                gamePanel.blocksRaycasts = false;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] gamePanel이 null이라 시뮬레이션 중 입력을 막을 수 없음.");
            }

            bool success = EvaluateMission();
            if (_resultStore != null) _resultStore.Result = success ? MissionResult.Success : MissionResult.Fail;
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] resultStore가 null이라 미션 결과를 기록할 수 없음.");
            StoreSolutionDesign();

            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] 코딩 완료. 결과={(success ? "성공" : "실패")}.");

            if (CurrentLevelState != null)
            {
                await CurrentLevelState.PlayCompletionSimulationAsync(this, this.GetCancellationTokenOnDestroy());
            }

            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] {Constants.Scenes.Result} 씬으로 이동.");
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Result, _commonSettings?.sceneTransitionFadeDuration ?? sceneFadeDuration).Forget();
        }

        /// <summary>
        /// 스킵 버튼 클릭 시 결과를 실패로 기록하고 화면 페이드와 함께 결과 씬으로 전환함.
        /// </summary>
        private void OnSkipButtonClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] sceneTransition이 null이라 {Constants.Scenes.Result} 씬을 로드할 수 없음.");
                return;
            }

            if (_resultStore != null) _resultStore.Result = MissionResult.Fail;
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] resultStore가 null이라 스킵 결과를 기록할 수 없음.");
            StoreSolutionDesign();

            _isBusy = true;
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] 스킵함. 결과=실패. {Constants.Scenes.Result} 씬으로 이동.");
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Result, _commonSettings?.sceneTransitionFadeDuration ?? sceneFadeDuration).Forget();
        }

        /// <summary>
        /// 이번 판 문제의 정답 블록(레벨 상태의 BuildSolution)을 설계창과 같은 형식의 문구로 만들어 결과 저장소에 기록함.
        /// 결과 씬의 AI 설계창이 이 문구를 그대로 보여줌. 정답을 만들지 못하면 빈 목록이 기록됨.
        /// </summary>
        internal void StoreSolutionDesign()
        {
            if (_resultStore == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] resultStore가 null이라 정답 설계를 기록할 수 없음.");
                return;
            }

            IIngredientSelectionLevelState state = CurrentLevelState;
            if (state == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 레벨 상태가 없어 정답 설계를 만들 수 없음. 빈 설계를 기록함.");
                _resultStore.SolutionDesignItems = Array.Empty<string>();
                return;
            }

            List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = state.BuildSolution(this);
            string[] items = new string[solution.Count];
            for (int i = 0; i < solution.Count; i++)
            {
                items[i] = state.FormatDesignItemText(this, solution[i].ingredient.ingredientName, solution[i].matter.label);
            }

            _resultStore.SolutionDesignItems = items;
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] {_selectedLevel}레벨 정답 설계 {items.Length}줄을 기록함.");
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

            int requiredCount = (_selectedLevel == 4) ? 1 : _totalSteps;
            if (_designItems.Count < requiredCount)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 확정된 단계가 부족함 ({_designItems.Count}/{requiredCount}). 실패로 처리함.");
                return false;
            }

            return CurrentLevelState?.EvaluateMission(this) ?? false;
        }

        /// <summary> 확정된 추진력을 미션보드 진행도(Image_Fill)에 반영함. </summary>
        private void ApplyProgressToMissionBoard()
        {
            if (_missionBoard)
            {
                _missionBoard.SetProgress(CalculateTotalThrust());
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[IngredientSelectionController] missionBoard가 null이라 진행도를 갱신할 수 없음.");
            }
        }

        /// <summary>
        /// 확정된 엔진 출력량 + 연료량 - 탑재 중량으로 총 추진력을 계산함.
        /// </summary>
        internal int CalculateTotalThrust() => CalculateThrust(_confirmedEngineValue, _confirmedFuelValue, _confirmedPayloadValue);

        /// <summary> 엔진 출력량 + 연료량 - 탑재 중량 공식을 그대로 계산함. </summary>
        internal static int CalculateThrust(int engine, int fuel, int payload) => engine + fuel - payload;

        /// <summary>
        /// 확정된 역할(엔진/연료/탑재)별 값에, 현재 조절 중인 임시 선택값을 대입해 미리보기용 추진력을 계산함.
        /// </summary>
        private int CalculatePreviewThrust()
        {
            return CurrentLevelState?.CalculatePreviewThrust(this) ?? 0;
        }

        /// <summary>
        /// 레벨 1 전용: 임시 선택값을 반영한 미리보기 추진력을 계산함.
        /// </summary>
        internal int CalculateLevel1PreviewThrust()
        {
            int engine = _confirmedEngineValue;
            int fuel = _confirmedFuelValue;
            int payload = _confirmedPayloadValue;

            RfidMatter selected = CurrentSelectedMatter();
            if (!string.IsNullOrEmpty(_currentIngredientId) && selected != null)
            {
                int tempValue = selected.value;
                if (string.Equals(_currentIngredientId, Constants.RfidIds.Level1.Engine, StringComparison.Ordinal)) engine = tempValue;
                else if (string.Equals(_currentIngredientId, Constants.RfidIds.Level1.Fuel, StringComparison.Ordinal)) fuel = tempValue;
                else if (string.Equals(_currentIngredientId, Constants.RfidIds.Level1.Payload, StringComparison.Ordinal)) payload = tempValue;
            }

            return CalculateThrust(engine, fuel, payload);
        }

        /// <summary> 현재 좌우 버튼으로 선택 중인 물질을 반환함. 선택된 것이 없으면 null. </summary>
        internal RfidMatter CurrentSelectedMatter()
        {
            RfidMatter[] matters = _currentMatters.Value;
            int idx = _currentMatterIndex.Value;
            return (matters != null && idx >= 0 && idx < matters.Length) ? matters[idx] : null;
        }

        /// <summary>
        /// 확정된 값을 재료 역할(엔진 출력량/연료량/탑재 중량)에 맞는 필드에 반영함. 롤백 시 0을 넘겨 해당 역할을 미확정 상태로 되돌리는 데도 사용됨.
        /// 값은 RfidMappings.json 물질의 value(항상 양수 크기)이며, 탑재 중량의 빼기는 계산식이 담당함.
        /// </summary>
        internal void ApplyConfirmedValue(string ingredientId, int value)
        {
            if (string.Equals(ingredientId, Constants.RfidIds.Level1.Engine, StringComparison.Ordinal)) _confirmedEngineValue = value;
            else if (string.Equals(ingredientId, Constants.RfidIds.Level1.Fuel, StringComparison.Ordinal)) _confirmedFuelValue = value;
            else if (string.Equals(ingredientId, Constants.RfidIds.Level1.Payload, StringComparison.Ordinal)) _confirmedPayloadValue = value;
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 알 수 없는 재료 역할 '{ingredientId}'. 추진력 계산식에 값이 반영되지 않음.");
        }

        /// <summary>
        /// 왼쪽 버튼 클릭 시 세부 물질 인덱스를 이전으로 변경함.
        /// </summary>
        private void OnLeftButtonClicked()
        {
            RfidMatter[] matters = _currentMatters.Value;
            if (matters == null || matters.Length == 0) return;

            int len = matters.Length;
            _currentMatterIndex.Value = (_currentMatterIndex.Value - 1 + len) % len;
        }

        /// <summary>
        /// 오른쪽 버튼 클릭 시 세부 물질 인덱스를 다음으로 변경함.
        /// </summary>
        private void OnRightButtonClicked()
        {
            RfidMatter[] matters = _currentMatters.Value;
            if (matters == null || matters.Length == 0) return;

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

            RfidMatter[] matters = _currentMatters.Value;
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
            if (_currentMatters.Value == null || _currentMatters.Value.Length == 0)
            {
                _missionBoard.ResetPreview();
                return;
            }

            int previewThrust = CalculatePreviewThrust();
            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] 미리보기 추진력 조정 중: {previewThrust} (재료={_currentIngredient.Value})");
            }
            _missionBoard.UpdatePreview(previewThrust);
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
            return isNumber ? $"<size={_sceneSettings?.numberFontSize ?? numberFontSize}>{value}</size>" : value;
        }

        /// <summary>
        /// Text_Matter 또는 Text_Material에 값이 있는지(RFID 태그/디버그 키로 재료가 선택된 상태인지)에 따라
        /// Image_RightArrow의 반복 펄스 애니메이션을 시작하거나 멈춤.
        /// </summary>
        private void UpdateRightArrowAnimation()
        {
            if (rightArrowImages == null || rightArrowImages.Length == 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] rightArrowImages가 비어 있어 화살표 안내 연출을 건너뜀.");
                return;
            }

            bool hasValue = (textMatter && !string.IsNullOrEmpty(textMatter.text))
                          || (textIngredient && !string.IsNullOrEmpty(textIngredient.text));

            if (hasValue) StartRightArrowLoop();
            else ResetRightArrow();
        }

        /// <summary>
        /// Image_Arrow1..5를 순서대로 알파 0->1로 페이드인한 뒤(하나씩 차례로 켜짐), 다 켜진 상태로
        /// rightArrowHoldDuration(초)만큼 붙잡아 보여주고, 다섯 개를 동시에 페이드아웃함. 페이드아웃이 끝난 뒤에도
        /// 바로 다음 루프를 시작하지 않고 다시 rightArrowHoldDuration(초)만큼 대기했다가 반복함. 이미 재생 중이면 무시함.
        /// </summary>
        private void StartRightArrowLoop()
        {
            if (_rightArrowSequence != null && _rightArrowSequence.IsActive()) return;

            float stepDuration = _sceneSettings?.rightArrowStepFadeDuration ?? rightArrowStepFadeDuration;
            float fadeOutDuration = _sceneSettings?.rightArrowFadeOutDuration ?? rightArrowFadeOutDuration;
            float holdDuration = _sceneSettings?.rightArrowHoldDuration ?? rightArrowHoldDuration;

            if (_rightArrowCanvasGroups == null || _rightArrowCanvasGroups.Length == 0) InitCanvasGroups();
            if (_rightArrowCanvasGroups == null || _rightArrowCanvasGroups.Length == 0) return;

            for (int i = 0; i < _rightArrowCanvasGroups.Length; i++)
            {
                if (_rightArrowCanvasGroups[i]) _rightArrowCanvasGroups[i].alpha = 0f;
            }

            _rightArrowSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            for (int i = 0; i < _rightArrowCanvasGroups.Length; i++)
            {
                if (_rightArrowCanvasGroups[i])
                {
                    _rightArrowSequence.Append(_rightArrowCanvasGroups[i].DOFade(1f, stepDuration));
                }
            }

            _rightArrowSequence.AppendInterval(holdDuration);

            if (_rightArrowCanvasGroups.Length > 0 && _rightArrowCanvasGroups[0])
            {
                _rightArrowSequence.Append(_rightArrowCanvasGroups[0].DOFade(0f, fadeOutDuration));
                for (int i = 1; i < _rightArrowCanvasGroups.Length; i++)
                {
                    if (_rightArrowCanvasGroups[i])
                    {
                        _rightArrowSequence.Join(_rightArrowCanvasGroups[i].DOFade(0f, fadeOutDuration));
                    }
                }
            }

            _rightArrowSequence.AppendInterval(holdDuration);

            _rightArrowSequence.SetLoops(-1);
        }

        /// <summary>
        /// 반복 애니메이션을 멈추고 Image_Arrow1..5를 전부 알파 0의 시작 상태로 되돌림.
        /// </summary>
        private void ResetRightArrow()
        {
            _rightArrowSequence?.Kill();
            _rightArrowSequence = null;

            if (_rightArrowCanvasGroups == null) return;
            for (int i = 0; i < _rightArrowCanvasGroups.Length; i++)
            {
                if (_rightArrowCanvasGroups[i]) _rightArrowCanvasGroups[i].alpha = 0f;
            }
        }

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
            _currentMatters?.Dispose();
            _currentMatterIndex?.Dispose();

            _rightArrowSequence?.Kill();
            _warningSequence?.Kill();
            _warningCts?.Cancel();
            _warningCts?.Dispose();
        }
    }
}
