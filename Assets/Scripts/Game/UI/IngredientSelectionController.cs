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
using UnityEngine.UI;
using VContainer;
using Wonjeong.Utils;
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

        private const string FuelIngredientName = "연료량";
        private const string EngineIngredientName = "추진체 종류";
        private const string PayloadIngredientName = "탑재 종류";

        // 레벨 4: 스캔된 카드의 category(동작/제어)에 따라 같은 단계라도 재료/물질이 달라짐(JSON의 단계별 고정값이 아님).
        private const string Level4MoveIngredientName = "이동하기";
        private static readonly string[] Level4MoveMatters = { "위쪽 한칸", "아랫쪽 한칸", "오른쪽 한칸", "왼쪽 한칸" };
        private const string Level4RepeatIngredientName = "반복하기";
        private static readonly string[] Level4RepeatMatters = { "1회", "2회", "3회" };

        // 레벨 2 발사 코딩 순서 단계 볼(Image_StepN_Ball)의 완료 표시. 인덱스가 stepBallImages 순서와 매칭됨.
        private static readonly string[] Level2StepBallMatters =
        {
            "점화하기", "상승하기", "1차 로켓 분리하기", "2차 로켓 분리하기", "우주정거장 궤도 진입하기"
        };
        private static readonly string[] Level2StepBallTexts =
        {
            "점화 시퀀스\n완료", "상승 시퀀스\n준비 완료", "1차 로켓\n준비 완료", "2차 로켓\n준비 완료", "진입 궤도\n계산 완료"
        };

        private ISubscriber<RfidTagEvent> _subscriber;
        private SelectedLevelStore _selectedLevelStore;
        private SceneTransitionService _sceneTransition;
        private MissionBoardController _missionBoard;
        private CodingCategoryIndicatorController _codingCategoryIndicator;
        private GameResultStore _resultStore;
        private Level4BoardController _level4Board; // Level4BoardController가 이 클래스를 참조하는 순환 의존이라 VContainer로 주입하지 않고 필요할 때 FindObjectOfType으로 찾음
        private ILogger<IngredientSelectionController> _logger;
        private bool _isBusy;

        private int _selectedLevel = 1;     // 현재 레벨 (디자인 항목 표시 형식 분기 등에 사용)
        private int _currentStepIndex = 0;  // 현재 read 인덱스 (0 ~ _totalSteps-1)
        private int _totalSteps = 3;        // 현재 스테이지에서 찍어야 하는 총 read 횟수
        private int _currentStageIndex = 0; // 현재 스테이지 (0부터 시작)
        private int[] _stageReadCounts = { 3 }; // 스테이지별 read 횟수 (JSON stageReadCounts, steps 미설정 시 폴백)
        private RfidStepDefinition[] _stepDefinitions; // "동작" 카드를 찍을 때마다 순서대로 진행되는 재료 목록 (추진체 종류 -> 탑재 종류 -> 연료량)
        private string[] _confirmedMatters;
        private string[] _confirmedIngredients;

        // 추진력 계산식(엔진 출력량 x 연료량 - 탑재 중량)에 쓰이는 역할별 확정 값. 미확정 상태의 기본값은 0.
        private int _confirmedEngineValue = 0;
        private int _confirmedFuelValue = 0;
        private int _confirmedPayloadValue = 0;

        // 디자인 컨테이너에 동적으로 추가된 확정 항목 텍스트 목록
        private readonly List<TMP_Text> _designItems = new List<TMP_Text>();

        // R3 반응형 상태 관리
        private readonly ReactiveProperty<string> _currentIngredient = new ReactiveProperty<string>("");
        private readonly ReactiveProperty<string[]> _currentMatters = new ReactiveProperty<string[]>(Array.Empty<string>());
        private readonly ReactiveProperty<int> _currentMatterIndex = new ReactiveProperty<int>(0);

        private R3.DisposableBag _disposables = new R3.DisposableBag();
        private Sequence _rightArrowSequence;
        private Tween _level2FillTween;
        private Sequence _warningSequence;
        private CancellationTokenSource _warningCts;

        // 3_Game.json / 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private GameSceneSettings _sceneSettings;
        private CommonSettings _commonSettings;

        // 레벨 3 게이지(전기/산소) 상태. fillAmount는 애니메이션 도중일 수 있어 목표값을 별도로 추적함.
        private float _level3OxygenFill;
        private float _level3ElectricFill;
        private Tween _level3OxygenGaugeTween;
        private Tween _level3ElectricGaugeTween;
        private Tween _level3OxygenIconBlinkTween;
        private Tween _level3ElectricIconBlinkTween;
        private bool _level3InstabilityPending; // "또는"이 선택된 상태. 5단계를 전부 완료해야 실제 깜빡임이 시작됨.

        /// <summary>
        /// VContainer 의존성 주입. MessagePipe 구독자, 씬 전환 서비스, 로거를 할당함.
        /// </summary>
        [Inject]
        public void Construct(ISubscriber<RfidTagEvent> subscriber, SelectedLevelStore selectedLevelStore, SceneTransitionService sceneTransition, MissionBoardController missionBoard, CodingCategoryIndicatorController codingCategoryIndicator, GameResultStore resultStore, ILogger<IngredientSelectionController> logger)
        {
            _subscriber = subscriber;
            _selectedLevelStore = selectedLevelStore;
            _sceneTransition = sceneTransition;
            _missionBoard = missionBoard;
            _codingCategoryIndicator = codingCategoryIndicator;
            _resultStore = resultStore;
            _logger = logger;
        }

        /// <summary>
        /// 버튼 이벤트 리스너 등록 및 R3 구독 관계 설정.
        /// </summary>
        private void Start()
        {
            if (buttonLeft != null) buttonLeft.onClick.AddListener(OnLeftButtonClicked);
            if (buttonRight != null) buttonRight.onClick.AddListener(OnRightButtonClicked);
            if (buttonConfirm != null) buttonConfirm.onClick.AddListener(OnConfirmButtonClicked);
            if (buttonCancel != null) buttonCancel.onClick.AddListener(OnCancelButtonClicked);
            if (buttonCodingComplete != null) buttonCodingComplete.onClick.AddListener(OnCodingCompleteClicked);
            if (buttonSkip != null) buttonSkip.onClick.AddListener(OnSkipButtonClicked);

            if (_subscriber != null)
            {
                _subscriber.Subscribe(OnRfidTagReceived).AddTo(ref _disposables);
            }

            _currentIngredient.Subscribe(UpdateIngredientText).AddTo(ref _disposables);
            _currentMatterIndex.Subscribe(_ => { UpdateMatterText(); UpdateProgressPreview(); }).AddTo(ref _disposables);

            UpdateCodingCompleteButton();
            ResetRightArrow();
            InitializeWarningPanel();

            // 비동기로 설정을 로드하여 워크플로우 단계를 설정함
            InitializeWorkflowAsync().Forget();
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
                var settings = await JsonLoader.LoadAsync<RfidSettings>(Constants.Files.RfidMappings, token);
                if (settings != null && settings.stageReadCounts != null && settings.stageReadCounts.Length > 0)
                {
                    _stageReadCounts = settings.stageReadCounts;
                }

                int level = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;
                _selectedLevel = level;
                _stepDefinitions = settings != null ? settings.GetStepsForLevel(level) : null;

                _currentStageIndex = Mathf.Clamp(_currentStageIndex, 0, _stageReadCounts.Length - 1);
                _totalSteps = (_stepDefinitions != null && _stepDefinitions.Length > 0)
                    ? _stepDefinitions.Length
                    : _stageReadCounts[_currentStageIndex];
            }
            catch (Exception e)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] 워크플로우용 RfidMappings.json 로드 실패: {e.Message}");
            }

            _confirmedMatters = new string[_totalSteps];
            _confirmedIngredients = new string[_totalSteps];
            ClearDesignItems();
            InitializeStepBalls();
            InitializeLevel3Gauges();
            UpdateCategoryHint();

            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] {_currentStageIndex + 1}번째 스테이지 초기화 완료: 총 {_totalSteps}회 read 필요.");
        }

        /// <summary>
        /// 현재 단계(_currentStepIndex)가 허용하는 category 목록을 CodingCategoryIndicatorController에 전달해
        /// 다음에 찍어야 할 카테고리 아이콘이 부드럽게 페이드하며 안내되도록 함. 모든 단계가 끝났으면 힌트를 멈춤.
        /// 레벨 4에서 바로 이전 단계가 "반복하기"였다면(동작 카드만 허용됨) "제어"는 힌트에서 제외함.
        /// </summary>
        private void UpdateCategoryHint()
        {
            if (_codingCategoryIndicator == null) return;

            if (_stepDefinitions != null && _currentStepIndex < _totalSteps && _currentStepIndex < _stepDefinitions.Length && _stepDefinitions[_currentStepIndex] != null)
            {
                string[] categories = _stepDefinitions[_currentStepIndex].categories;
                if (_selectedLevel == 4 && IsRepeatFollowUpRequired()) categories = new[] { "동작" };

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

            foreach (Image ballImage in stepBallImages)
            {
                if (ballImage == null) continue;

                ballImage.material = stepBallGrayscaleMaterial;

                TMP_Text ballText = ballImage.GetComponentInChildren<TMP_Text>(true);
                if (ballText != null) ballText.text = "";
            }

            // 진행바도 시작 상태(0%)로 즉시 스냅함(연출 없이)
            _level2FillTween?.Kill();
            if (level2FillImage != null) level2FillImage.fillAmount = 0f;
        }

        /// <summary>
        /// 레벨 2 진행바(Image_Fill)를 확정된 DesignedItem 개수에 맞춰 갱신함. 0~1개=0%, 2개=25%, 3개=50%, 4개=75%, 5개=100%.
        /// Ease.OutBack으로 살짝 튕기는 "쥬시한" 느낌을 주되, 기본 오버슈트보다 작은 level2FillOvershoot 값을 써서 과하게 튀어나가지 않도록 함.
        /// </summary>
        private void UpdateLevel2FillAmount()
        {
            if (_selectedLevel != 2 || level2FillImage == null) return;

            float target = Mathf.Clamp01((_designItems.Count - 1) / 4f);

            _level2FillTween?.Kill();
            _level2FillTween = level2FillImage.DOFillAmount(target, _sceneSettings?.level2FillTweenDuration ?? level2FillTweenDuration)
                .SetEase(Ease.OutBack, _sceneSettings?.level2FillOvershoot ?? level2FillOvershoot);
        }

        /// <summary>
        /// 레벨 2에서 확정/취소된 단계(stepIndex, 몇 번째로 확정했는지)에 해당하는 Image_StepN_Ball의 색상과 완료 텍스트를 갱신함.
        /// 볼 번호는 어떤 matter를 골랐는지가 아니라 확정 순서(stepIndex)로 정해지고, 표시 문구만 matter 값에 따라 달라짐.
        /// 예: 두 번째로 확정한 값이 "1차 로켓 분리하기"이면 Step2_Ball에 "1차 로켓 준비 완료"가 표시됨.
        /// completed가 true면 원래 색으로 돌아오며 완료 문구를 표시하고, false면 다시 흑백으로 되돌리고 텍스트를 비움.
        /// </summary>
        private void UpdateStepBallDisplay(int stepIndex, string matter, bool completed)
        {
            if (_selectedLevel != 2 || stepBallImages == null) return;
            if (stepIndex < 0 || stepIndex >= stepBallImages.Length) return;

            Image ballImage = stepBallImages[stepIndex];
            if (ballImage == null) return;

            ballImage.material = completed ? null : stepBallGrayscaleMaterial;

            TMP_Text ballText = ballImage.GetComponentInChildren<TMP_Text>(true);
            if (ballText == null) return;

            if (!completed)
            {
                ballText.text = "";
                return;
            }

            int textIndex = Array.IndexOf(Level2StepBallMatters, matter);
            ballText.text = (textIndex >= 0 && textIndex < Level2StepBallTexts.Length) ? Level2StepBallTexts[textIndex] : "";
        }

        /// <summary> 레벨 3의 산소/전기 게이지와 아이콘 알파를 0%로, 깜빡임을 정지 상태로 되돌림. </summary>
        private void InitializeLevel3Gauges()
        {
            _level3OxygenFill = 0f;
            _level3ElectricFill = 0f;
            _level3InstabilityPending = false;

            _level3OxygenGaugeTween?.Kill();
            _level3ElectricGaugeTween?.Kill();
            if (level3OxygenGauge != null) level3OxygenGauge.fillAmount = 0f;
            if (level3ElectricGauge != null) level3ElectricGauge.fillAmount = 0f;

            // 게이지가 이미 0으로 세팅된 상태이므로, 게이지 값을 그대로 따라가는 아이콘 알파도 0이 됨
            StopLevel3IconInstability();
        }

        /// <summary>
        /// 레벨 3에서 확정된 단계(stepIndex, 0부터)와 값(matter)에 따라 게이지/아이콘 연출을 갱신함.
        /// 전기량 조건(0)·전기량 조작(1)이 정답이면 산소 게이지가, 산소량 조건(3)·산소량 조작(4)이 정답이면 전기 게이지가
        /// 각각 50%씩 채워짐(엇갈려 연결된 생명유지장치라는 설정). 논리 연결어(2)에서 "또는"을 고르면 불안정 상태가 예약되고,
        /// 실제 깜빡임은 5단계를 전부 완료한 시점에 OnConfirmButtonClicked에서 시작됨.
        /// </summary>
        private void UpdateLevel3Effects(int stepIndex, string matter)
        {
            if (_selectedLevel != 3) return;

            switch (stepIndex)
            {
                case 0:
                    if (_missionBoard != null && string.Equals(matter, $"{_missionBoard.MaxElectricity} 이상", StringComparison.Ordinal))
                    {
                        AddOxygenGaugeFill(0.5f);
                    }
                    break;
                case 1:
                    if (string.Equals(matter, "낮추기", StringComparison.Ordinal)) AddOxygenGaugeFill(0.5f);
                    break;
                case 2:
                    _level3InstabilityPending = string.Equals(matter, "또는", StringComparison.Ordinal);
                    break;
                case 3:
                    if (_missionBoard != null && string.Equals(matter, $"{_missionBoard.MinOxygen} 이하", StringComparison.Ordinal))
                    {
                        AddElectricGaugeFill(0.5f);
                    }
                    break;
                case 4:
                    if (string.Equals(matter, "올리기", StringComparison.Ordinal)) AddElectricGaugeFill(0.5f);
                    break;
            }
        }

        /// <summary> 취소(되돌리기) 시 UpdateLevel3Effects로 적용됐던 효과를 반대로 되돌림. </summary>
        private void RevertLevel3Effects(int stepIndex, string matter)
        {
            if (_selectedLevel != 3) return;

            // 어떤 단계를 되돌리든 "5단계 완료" 상태가 깨지므로, 예약/진행 중이던 깜빡임은 항상 멈춤
            StopLevel3IconInstability();

            switch (stepIndex)
            {
                case 0:
                    if (_missionBoard != null && string.Equals(matter, $"{_missionBoard.MaxElectricity} 이상", StringComparison.Ordinal))
                    {
                        AddOxygenGaugeFill(-0.5f);
                    }
                    break;
                case 1:
                    if (string.Equals(matter, "낮추기", StringComparison.Ordinal)) AddOxygenGaugeFill(-0.5f);
                    break;
                case 2:
                    _level3InstabilityPending = false;
                    break;
                case 3:
                    if (_missionBoard != null && string.Equals(matter, $"{_missionBoard.MinOxygen} 이하", StringComparison.Ordinal))
                    {
                        AddElectricGaugeFill(-0.5f);
                    }
                    break;
                case 4:
                    if (string.Equals(matter, "올리기", StringComparison.Ordinal)) AddElectricGaugeFill(-0.5f);
                    break;
            }
        }

        /// <summary>
        /// 산소 게이지의 목표 fillAmount를 amount만큼(0~1로 clamp) 조절하고 부드럽게 애니메이션함.
        /// Image_Icon의 알파도 게이지 진행률(0~1)을 그대로 따라가도록 매 프레임 동기화함.
        /// </summary>
        private void AddOxygenGaugeFill(float amount)
        {
            if (level3OxygenGauge == null) return;

            _level3OxygenFill = Mathf.Clamp01(_level3OxygenFill + amount);
            _level3OxygenGaugeTween?.Kill();
            _level3OxygenGaugeTween = level3OxygenGauge.DOFillAmount(_level3OxygenFill, _sceneSettings?.level3GaugeTweenDuration ?? level3GaugeTweenDuration)
                .SetEase(Ease.OutQuad)
                .OnUpdate(() => SetImageAlpha(level3OxygenIcon, level3OxygenGauge.fillAmount))
                .SetLink(level3OxygenGauge.gameObject);
        }

        /// <summary>
        /// 전기 게이지의 목표 fillAmount를 amount만큼(0~1로 clamp) 조절하고 부드럽게 애니메이션함.
        /// Image_Icon의 알파도 게이지 진행률(0~1)을 그대로 따라가도록 매 프레임 동기화함.
        /// </summary>
        private void AddElectricGaugeFill(float amount)
        {
            if (level3ElectricGauge == null) return;

            _level3ElectricFill = Mathf.Clamp01(_level3ElectricFill + amount);
            _level3ElectricGaugeTween?.Kill();
            _level3ElectricGaugeTween = level3ElectricGauge.DOFillAmount(_level3ElectricFill, _sceneSettings?.level3GaugeTweenDuration ?? level3GaugeTweenDuration)
                .SetEase(Ease.OutQuad)
                .OnUpdate(() => SetImageAlpha(level3ElectricIcon, level3ElectricGauge.fillAmount))
                .SetLink(level3ElectricGauge.gameObject);
        }

        /// <summary>
        /// 5단계를 전부 완료했고 "또는"이 선택되어 있었을 때만 호출됨. 산소/전기 두 Image_Icon을
        /// 서로 다른 주기로 어긋나게 깜빡여 불안정한 느낌을 줌.
        /// </summary>
        private void StartLevel3IconInstability()
        {
            float blinkDuration = _sceneSettings?.level3IconBlinkDuration ?? level3IconBlinkDuration;
            StartIconBlink(level3OxygenIcon, ref _level3OxygenIconBlinkTween, blinkDuration);
            StartIconBlink(level3ElectricIcon, ref _level3ElectricIconBlinkTween, blinkDuration * 1.4f);
        }

        /// <summary> 아이콘 하나를 알파 1~level3IconBlinkMinAlpha 사이로 무한 반복(Yoyo) 깜빡이게 함. </summary>
        private void StartIconBlink(Image icon, ref Tween tween, float duration)
        {
            if (icon == null) return;

            tween?.Kill();
            SetImageAlpha(icon, 1f);
            tween = icon.DOFade(_sceneSettings?.level3IconBlinkMinAlpha ?? level3IconBlinkMinAlpha, duration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(icon.gameObject);
        }

        /// <summary>
        /// 두 아이콘의 깜빡임을 멈추고, 알파를 각 게이지의 현재 fillAmount에 맞춰 되돌림(1이 아니라 진행률만큼).
        /// </summary>
        private void StopLevel3IconInstability()
        {
            _level3OxygenIconBlinkTween?.Kill();
            _level3OxygenIconBlinkTween = null;
            _level3ElectricIconBlinkTween?.Kill();
            _level3ElectricIconBlinkTween = null;

            if (level3OxygenGauge != null) SetImageAlpha(level3OxygenIcon, level3OxygenGauge.fillAmount);
            if (level3ElectricGauge != null) SetImageAlpha(level3ElectricIcon, level3ElectricGauge.fillAmount);
        }

        private void SetImageAlpha(Image image, float alpha)
        {
            if (image == null) return;

            Color color = image.color;
            color.a = alpha;
            image.color = color;
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
            if (gamePanel == null || !gamePanel.interactable)
            {
                if (_logger != null)
                {
                    _logger.ZLogInformation($"[IngredientSelectionController] 게임 패널이 비활성 상태라 {evt.ReaderId} 태그를 무시함.");
                }
                return;
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

            // 레벨 4: "반복하기"(제어)는 반드시 "이동하기"(동작)와 세트로 이어져야 하므로, 바로 이전 단계가
            // "반복하기"였다면 이번 단계는 "동작" 카드만 허용함(제어 카드는 이 규칙 위반으로 거부됨).
            if (_selectedLevel == 4 && IsRepeatFollowUpRequired() && !string.Equals(evt.Category, "동작", StringComparison.Ordinal))
            {
                if (_logger != null)
                {
                    _logger.ZLogInformation($"[IngredientSelectionController] 이전 단계가 '반복하기'라 {_currentStepIndex + 1}번째 단계는 '동작' 카드만 허용되는데 '{evt.Category}' 카드가 인식되어 {evt.ReaderId} 태그를 무시함.");
                }
                ShowInvalidCategoryWarningAsync().Forget();
                return;
            }

            (string ingredientName, string[] matterNames) = ResolveStepCard(step, evt.Category);

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] {evt.ReaderId} 리더기 태그를 {_currentStepIndex + 1}번째 단계에 적용함: {ingredientName}");
            }

            // 현재 단계에 허용된 카드로 확인된 경우에만 CodingCategories 강조를 갱신함(허용되지 않으면 흑백 상태가 그대로 유지됨)
            if (_codingCategoryIndicator != null) _codingCategoryIndicator.HighlightCategory(evt.Category);

            _currentIngredient.Value = ingredientName;
            // 레벨 4는 같은 동작(예: "위쪽 한칸")이나 "반복하기"를 경로상 여러 번 다시 써야 하므로 중복 제외를 적용하지 않음
            _currentMatters.Value = _selectedLevel == 4 ? matterNames : ExcludeConfirmedMatters(ingredientName, matterNames);
            _currentMatterIndex.Value = 0;
            UpdateMatterText();
            UpdateProgressPreview();
        }

        /// <summary>
        /// 현재 단계에서 실제로 사용할 재료 이름/물질 목록을 결정함. 레벨 4는 카드의 category(동작/제어)에 따라
        /// 같은 단계라도 다른 값을 써야 하므로(동작="이동하기"+방향 4종, 제어="반복하기"+횟수 3종) JSON의 단계별 고정값 대신
        /// 스캔된 category로 분기함. 다른 레벨은 JSON에 정의된 단계별 고정값을 그대로 사용함.
        /// </summary>
        private (string ingredientName, string[] matterNames) ResolveStepCard(RfidStepDefinition step, string category)
        {
            if (_selectedLevel != 4) return (step.ingredientName, step.matterNames);

            bool isAction = string.Equals(category, "동작", StringComparison.Ordinal);
            return isAction
                ? (Level4MoveIngredientName, Level4MoveMatters)
                : (Level4RepeatIngredientName, Level4RepeatMatters);
        }

        /// <summary> 레벨 4 전용: 바로 이전 단계에서 확정한 재료가 "반복하기"(제어, 횟수 카드)였다면, 이번 단계는 반드시 "이동하기"(동작)여야 함. </summary>
        private bool IsRepeatFollowUpRequired()
        {
            if (_currentStepIndex <= 0 || _confirmedIngredients == null || _currentStepIndex - 1 >= _confirmedIngredients.Length) return false;
            return string.Equals(_confirmedIngredients[_currentStepIndex - 1], Level4RepeatIngredientName, StringComparison.Ordinal);
        }

        /// <summary>
        /// 레벨 4 전용: 지금까지 확정된 (재료, 물질) 순서 목록을 확정된 순서 그대로 반환함.
        /// Level4BoardController가 이 목록으로 로봇 이동 경로(스페이스바 시뮬레이션)를 만드는 데 사용함.
        /// 아직 확정되지 않은 뒤쪽 슬롯은 포함하지 않음(레벨 4는 5단계를 다 채우지 않아도 되므로).
        /// </summary>
        public IReadOnlyList<(string ingredient, string matter)> GetConfirmedCommands()
        {
            var commands = new List<(string ingredient, string matter)>();
            if (_confirmedIngredients == null || _confirmedMatters == null) return commands;

            for (int i = 0; i < _currentStepIndex && i < _confirmedIngredients.Length; i++)
            {
                commands.Add((_confirmedIngredients[i], _confirmedMatters[i]));
            }

            return commands;
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
            if (warningPanel == null)
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
        private async UniTaskVoid ShowInvalidCategoryWarningAsync()
        {
            if (warningPanel == null) return;

            // 이전 호출의 UniTask.Delay 등 진행 중이던 비동기 흐름을 확실히 취소함(_warningSequence.Kill()만으로는
            // DOTween 트윈만 멈출 뿐, 이전 호출이 대기 중인 await까지 중단시키진 못해 레이스 컨디션이 발생할 수 있음).
            _warningCts?.Cancel();
            _warningCts?.Dispose();
            _warningCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            CancellationToken token = _warningCts.Token;
            float fadeDuration = _sceneSettings?.warningFadeDuration ?? warningFadeDuration;

            try
            {
                _warningSequence?.Kill();
                if (gamePanel != null) ((RectTransform)gamePanel.transform).anchoredPosition = Vector2.zero;

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
        }

        /// <summary> GamePanel의 RectTransform을 좌우로 3회(왕복) 흔들고 원위치로 되돌림. </summary>
        private UniTask ShakeGamePanelAsync(CancellationToken token)
        {
            if (gamePanel == null) return UniTask.CompletedTask;

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
        /// 같은 ingredientName을 쓰는 여러 단계가 matterNames 목록을 공유할 때(예: 레벨 2의 발사 코딩 순서), 이미 다른 단계에서
        /// 확정된 값은 다시 고를 수 없도록 목록에서 제외함. ingredientName까지 함께 비교하므로, 서로 다른 ingredient가
        /// 우연히 같은 값 텍스트를 공유해도(예: 레벨 3의 전기량/산소량이 둘 다 "올리기"/"낮추기") 서로 간섭하지 않음.
        /// </summary>
        private string[] ExcludeConfirmedMatters(string ingredientName, string[] matterNames)
        {
            if (matterNames == null || matterNames.Length == 0) return Array.Empty<string>();
            if (_confirmedMatters == null || _confirmedIngredients == null) return matterNames;

            var available = new List<string>(matterNames.Length);
            foreach (string matter in matterNames)
            {
                bool alreadyConfirmed = false;
                for (int i = 0; i < _confirmedMatters.Length; i++)
                {
                    if (string.Equals(_confirmedIngredients[i], ingredientName, StringComparison.Ordinal) &&
                        string.Equals(_confirmedMatters[i], matter, StringComparison.Ordinal))
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
            if (_confirmedMatters == null) return;

            // ingredientName이 빈 문자열인 단계(예: 레벨 3의 논리 연결어)도 있으므로, "스캔된 것이 없음"은
            // ingredient가 아니라 matters 목록의 존재 여부로 판단함.
            var matters = _currentMatters.Value;
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

            string ingredient = _currentIngredient.Value;
            string chosenMatter = matters[_currentMatterIndex.Value];
            _confirmedMatters[_currentStepIndex] = chosenMatter;
            _confirmedIngredients[_currentStepIndex] = ingredient;

            // 추진력 계산식(엔진 출력량 x 연료량 - 탑재 중량)은 레벨 1 전용 재료 이름을 기준으로 하므로,
            // 다른 레벨의 재료 이름에 대해 매번 "알 수 없는 재료" 경고가 찍히지 않도록 레벨 1에서만 반영함.
            int value = 0;
            if (_selectedLevel == 1)
            {
                value = ParseIngredientValue(ingredient, chosenMatter);
                ApplyConfirmedValue(ingredient, value);
            }

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] {_currentStepIndex + 1}번째 단계 확정: {ingredient} -> {chosenMatter} (값={value})");
            }

            // 디자인 컨테이너에 확정 항목을 자식으로 추가
            AddDesignItem(ingredient, chosenMatter);

            // 레벨 2: 확정된 matter에 대응하는 Image_StepN_Ball을 원래 색으로 되돌리고 완료 문구를 표시함
            UpdateStepBallDisplay(_currentStepIndex, chosenMatter, true);

            // 레벨 3: 확정된 단계/값에 따라 산소/전기 게이지와 아이콘 깜빡임을 갱신함
            UpdateLevel3Effects(_currentStepIndex, chosenMatter);

            // 확정된 엔진 출력량/연료량/탑재 중량을 계산식에 반영해 진행도(Image_Fill)를 갱신함
            if (_missionBoard != null)
            {
                _missionBoard.SetProgress(CalculateTotalThrust());
            }

            // 현재 카드 선택 대기 상태 초기화
            _currentIngredient.Value = "";
            _currentMatters.Value = Array.Empty<string>();
            _currentMatterIndex.Value = 0;
            UpdateMatterText();
            UpdateProgressPreview();

            // 다음 단계로 인덱스 증가
            _currentStepIndex++;

            // 다음 단계가 남아있으면 그 단계의 카테고리 힌트를 다시 페이드로 안내하고, 없으면 힌트를 멈춤
            // (동작 확정으로 켜졌던 CodingCategories 강조도 여기서 함께 흑백으로 정리됨)
            UpdateCategoryHint();

            if (_currentStepIndex >= _totalSteps)
            {
                if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] 총 {_totalSteps}단계 모두 완료됨!");

                // 레벨 3: "또는"이 선택된 채로 5단계를 전부 완료한 시점에 비로소 불안정 깜빡임을 시작함
                if (_selectedLevel == 3 && _level3InstabilityPending)
                {
                    StartLevel3IconInstability();
                }
            }
        }

        /// <summary>
        /// 취소하기(Cancel) 버튼 클릭 시 현재 대기 상태를 비우고 이전 단계로 되돌아감.
        /// </summary>
        private void OnCancelButtonClicked()
        {
            if (_confirmedMatters == null) return;

            if (_currentStepIndex == 0)
            {
                // 1단계(Reader 1)인 경우 현재 태그된 임시 선택값만 클리어
                _currentIngredient.Value = "";
                _currentMatters.Value = Array.Empty<string>();
                _currentMatterIndex.Value = 0;
                UpdateMatterText();
                UpdateProgressPreview();
                UpdateCategoryHint();
                return;
            }

            // 이전 단계로 롤백
            _currentStepIndex--;

            // 되돌리는 항목의 확정 값을 계산식에서 제외(0으로 리셋)하고, 남은 확정 값들로 진행도(Image_Fill)를 다시 계산함
            // (추진력 계산식은 레벨 1 전용 재료 이름 기준이라, 다른 레벨은 건너뜀 - "알 수 없는 재료" 경고 방지)
            if (_selectedLevel == 1) ApplyConfirmedValue(_confirmedIngredients[_currentStepIndex], 0);

            // 레벨 2: 되돌리는 matter에 대응하는 Image_StepN_Ball을 다시 흑백으로 되돌리고 완료 문구를 지움
            UpdateStepBallDisplay(_currentStepIndex, _confirmedMatters[_currentStepIndex], false);

            // 레벨 3: 되돌리는 단계/값에 적용됐던 게이지/아이콘 효과를 반대로 되돌림
            RevertLevel3Effects(_currentStepIndex, _confirmedMatters[_currentStepIndex]);

            // 이전 단계의 확정 내역 삭제
            _confirmedMatters[_currentStepIndex] = null;
            _confirmedIngredients[_currentStepIndex] = null;
            RemoveLastDesignItem();

            if (_missionBoard != null)
            {
                _missionBoard.SetProgress(CalculateTotalThrust());
            }

            // 현재 임시 선택 상태 초기화
            _currentIngredient.Value = "";
            _currentMatters.Value = Array.Empty<string>();
            _currentMatterIndex.Value = 0;
            UpdateMatterText();
            UpdateProgressPreview();

            // 되돌아간 단계의 카테고리 힌트를 다시 페이드로 안내함
            UpdateCategoryHint();

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] {_currentStepIndex + 1}번째 단계로 되돌림 (Reader_{_currentStepIndex + 1} 대기 중)");
            }
        }

        /// <summary>
        /// 확정된 재료/물질을 "· 재료 [물질]" 형태의 Text로 만들어 디자인 컨테이너의 자식으로 추가함. 물질은 노란색으로 표시함.
        /// 레벨 2는 모든 단계의 ingredientName이 "발사 코딩 순서"로 동일해 매번 반복 표시할 필요가 없고,
        /// ingredientName이 빈 문자열인 단계(예: 레벨 3의 논리 연결어)도 재료 이름 없이 "· [물질]" 형태로만 표시함.
        /// </summary>
        private void AddDesignItem(string ingredient, string matter)
        {
            if (designContent == null || designItemPrefab == null) return;

            TextMeshProUGUI text = Instantiate(designItemPrefab, designContent);
            text.text = _selectedLevel == 2 || string.IsNullOrEmpty(ingredient)
                ? $" · [<color=yellow>{ApplyNumberSizeTag(matter)}</color>]"
                : $" · {ingredient} [<color=yellow>{ApplyNumberSizeTag(matter)}</color>]";

            _designItems.Add(text);
            UpdateCodingCompleteButton();
            UpdateLevel2FillAmount();
        }

        /// <summary>
        /// 디자인 컨테이너에 마지막으로 추가된 확정 항목을 제거함.
        /// </summary>
        private void RemoveLastDesignItem()
        {
            if (_designItems.Count == 0) return;

            int lastIndex = _designItems.Count - 1;
            var last = _designItems[lastIndex];
            _designItems.RemoveAt(lastIndex);
            if (last != null) Destroy(last.gameObject);
            UpdateCodingCompleteButton();
            UpdateLevel2FillAmount();
        }

        /// <summary>
        /// 디자인 컨테이너에 추가된 모든 확정 항목을 제거함.
        /// </summary>
        private void ClearDesignItems()
        {
            for (int i = 0; i < _designItems.Count; i++)
            {
                if (_designItems[i] != null) Destroy(_designItems[i].gameObject);
            }
            _designItems.Clear();
            UpdateCodingCompleteButton();
        }

        /// <summary>
        /// 디자인 컨테이너에 확정 항목이 필요한 만큼(_totalSteps) 채워졌을 때만 코딩완료 버튼을 활성화함.
        /// 레벨 4는 5단계를 다 채우지 않아도 되므로(경로가 짧아도 됨) 최소 1개만 확정되면 활성화함.
        /// </summary>
        private void UpdateCodingCompleteButton()
        {
            if (buttonCodingComplete == null) return;

            buttonCodingComplete.interactable = _selectedLevel == 4
                ? _designItems.Count > 0
                : _designItems.Count >= _totalSteps;
        }

        /// <summary>
        /// 코딩완료 버튼 클릭 시 확정된 추진력(엔진 출력량 x 연료량 - 탑재 중량)을 목적지 조건과 대조해 성공/실패를 기록하고, 화면 페이드와 함께 결과 씬으로 전환함.
        /// </summary>
        private void OnCodingCompleteClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] sceneTransition이 null이라 {Constants.Scenes.Result} 씬을 로드할 수 없음.");
                return;
            }

            bool success = EvaluateMission();
            if (_resultStore != null) _resultStore.Result = success ? MissionResult.Success : MissionResult.Fail;

            _isBusy = true;
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] 코딩 완료. 결과={(success ? "성공" : "실패")}. {Constants.Scenes.Result} 씬으로 이동.");
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

            _isBusy = true;
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] 스킵함. 결과=실패. {Constants.Scenes.Result} 씬으로 이동.");
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Result, _commonSettings?.sceneTransitionFadeDuration ?? sceneFadeDuration).Forget();
        }

        /// <summary>
        /// 확정된 엔진 출력량 x 연료량 - 탑재 중량 계산 결과가 이번 목적지의 조건 범위를 만족하는지 판정함.
        /// 모든 단계가 확정되지 않았거나 미션보드가 없으면 실패로 처리함.
        /// </summary>
        private bool EvaluateMission()
        {
            if (_missionBoard == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] missionBoard가 없어 미션을 판정할 수 없음. 실패로 처리함.");
                return false;
            }

            // 레벨 4는 5단계를 다 채우지 않아도 되므로 최소 1개만 확정되면 판정을 진행함
            int requiredCount = _selectedLevel == 4 ? 1 : _totalSteps;
            if (_designItems.Count < requiredCount)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 확정된 단계가 부족함 ({_designItems.Count}/{requiredCount}). 실패로 처리함.");
                return false;
            }

            if (_selectedLevel == 2) return EvaluateLevel2Mission();
            if (_selectedLevel == 3) return EvaluateLevel3Mission();
            if (_selectedLevel == 4) return EvaluateLevel4Mission();

            int totalThrust = CalculateTotalThrust();
            bool valid = _missionBoard.IsThrustValid(totalThrust);
            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] 총 추진력 {totalThrust} (엔진={_confirmedEngineValue} x 연료={_confirmedFuelValue} - 탑재={_confirmedPayloadValue}) vs 목적지 '{_missionBoard.Destination}' -> {(valid ? "성공" : "실패")}");
            }
            return valid;
        }

        /// <summary>
        /// 레벨 2 전용 판정: 확정된 5단계의 값이 순서대로 정확히 "점화하기 -> 상승하기 -> 1차 로켓 분리하기 ->
        /// 2차 로켓 분리하기 -> 우주정거장 궤도 진입하기"(Level2StepBallMatters)와 일치해야만 성공으로 처리함.
        /// 순서가 하나라도 어긋나면 실패.
        /// </summary>
        private bool EvaluateLevel2Mission()
        {
            if (_confirmedMatters == null || _confirmedMatters.Length < Level2StepBallMatters.Length)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 레벨 2 확정 단계 수가 부족함. 실패로 처리함.");
                return false;
            }

            for (int i = 0; i < Level2StepBallMatters.Length; i++)
            {
                if (!string.Equals(_confirmedMatters[i], Level2StepBallMatters[i], StringComparison.Ordinal))
                {
                    if (_logger != null)
                    {
                        _logger.ZLogInformation($"[IngredientSelectionController] 레벨 2 판정: {i + 1}번째 단계가 '{Level2StepBallMatters[i]}'가 아니라 '{_confirmedMatters[i]}'라 순서가 어긋남. 실패로 처리함.");
                    }
                    return false;
                }
            }

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] 레벨 2 판정: 발사 코딩 순서가 정확히 일치함. 성공으로 처리함.");
            }
            return true;
        }

        /// <summary>
        /// 레벨 3 전용 판정: 산소/전기 게이지(CircleGage)가 둘 다 100%(1.0)까지 채워지고, 3번째 단계(논리 연결어)에서
        /// "또는"을 선택해 시스템이 불안정(_level3InstabilityPending)해지지 않았어야 성공.
        /// 각 게이지는 관련된 두 단계(전기량 조건/조작, 산소량 조건/조작)에 모두 정답을 골라야 0.5씩 채워져 1.0이 됨
        /// (UpdateLevel3Effects). fillAmount는 트윈으로 서서히 올라가므로, 트윈 완료 여부와 무관하게 확정된
        /// 목표값인 _level3OxygenFill/_level3ElectricFill을 기준으로 판정함.
        /// </summary>
        private bool EvaluateLevel3Mission()
        {
            bool oxygenFull = _level3OxygenFill >= 1f;
            bool electricFull = _level3ElectricFill >= 1f;
            bool stable = !_level3InstabilityPending;
            bool success = oxygenFull && electricFull && stable;

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] 레벨 3 판정: 산소 게이지={_level3OxygenFill:F2}, 전기 게이지={_level3ElectricFill:F2}, 시스템 안정={stable} -> {(success ? "성공" : "실패")}");
            }

            return success;
        }

        /// <summary>
        /// 레벨 4 전용 판정: 확정된 "반복하기/이동하기" 명령을 직접 Level4BoardController.EvaluateOutcome(commands)에
        /// 인자로 넘겨 연출 없이 즉시 재계산함(자원을 먼저 수집한 뒤 기지에 도착해야 성공, 그 외는 전부 실패).
        /// Level4BoardController도 이 클래스를 참조해서 VContainer로 주입받으면 순환 의존이 되므로,
        /// 보드 인스턴스 자체는 필요할 때(코딩완료 클릭 시, 자주 호출되지 않음) FindObjectOfType으로 찾아 캐시하되,
        /// 판정에 쓰는 명령 목록은 GetConfirmedCommands()로 직접 전달해 EvaluateOutcome이 이 클래스에 되묻지 않게 함.
        /// </summary>
        private bool EvaluateLevel4Mission()
        {
            if (_level4Board == null) _level4Board = FindObjectOfType<Level4BoardController>();
            if (_level4Board == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] level4Board를 찾을 수 없어 레벨 4 미션을 판정할 수 없음. 실패로 처리함.");
                return false;
            }

            return _level4Board.EvaluateOutcome(GetConfirmedCommands());
        }

        /// <summary>
        /// 확정된 엔진 출력량 x 연료량 - 탑재 중량으로 총 추진력을 계산함.
        /// </summary>
        private int CalculateTotalThrust() => CalculateThrust(_confirmedEngineValue, _confirmedFuelValue, _confirmedPayloadValue);

        /// <summary> 엔진 출력량 x 연료량 - 탑재 중량 공식을 그대로 계산함. </summary>
        private int CalculateThrust(int engine, int fuel, int payload) => engine * fuel - payload;

        /// <summary>
        /// 확정된 역할(엔진/연료/탑재)별 값에, 현재 조절 중인 임시 선택값을 해당 역할에 대입해 미리보기용 추진력을 계산함.
        /// 조절 중인 항목이 없으면 확정된 값만으로 계산함.
        /// </summary>
        private int CalculatePreviewThrust()
        {
            int engine = _confirmedEngineValue;
            int fuel = _confirmedFuelValue;
            int payload = _confirmedPayloadValue;

            // 추진력 계산식(엔진 출력량 x 연료량 - 탑재 중량)은 레벨 1 전용 재료 이름을 기준으로 하므로,
            // 다른 레벨의 재료 이름("이동하기", "발사 코딩 순서" 등)에 대해 매번 파싱 경고가 찍히지 않도록 레벨 1에서만 계산함.
            string ingredient = _currentIngredient.Value;
            if (_selectedLevel == 1 && !string.IsNullOrEmpty(ingredient))
            {
                int tempValue = ParseIngredientValue(ingredient, CurrentSelectedMatter());
                if (string.Equals(ingredient, EngineIngredientName, StringComparison.Ordinal)) engine = tempValue;
                else if (string.Equals(ingredient, FuelIngredientName, StringComparison.Ordinal)) fuel = tempValue;
                else if (string.Equals(ingredient, PayloadIngredientName, StringComparison.Ordinal)) payload = tempValue;
            }

            return CalculateThrust(engine, fuel, payload);
        }

        /// <summary> 현재 좌우 버튼으로 선택 중인 물질 문자열을 반환함. 선택된 것이 없으면 null. </summary>
        private string CurrentSelectedMatter()
        {
            var matters = _currentMatters.Value;
            int idx = _currentMatterIndex.Value;
            return (matters != null && idx >= 0 && idx < matters.Length) ? matters[idx] : null;
        }

        /// <summary>
        /// 확정된 값을 역할(엔진 출력량/연료량/탑재 중량)에 맞는 필드에 반영함. 롤백 시 0을 넘겨 해당 역할을 미확정 상태로 되돌리는 데도 사용됨.
        /// </summary>
        private void ApplyConfirmedValue(string ingredient, int value)
        {
            if (string.Equals(ingredient, EngineIngredientName, StringComparison.Ordinal)) _confirmedEngineValue = value;
            else if (string.Equals(ingredient, FuelIngredientName, StringComparison.Ordinal)) _confirmedFuelValue = value;
            else if (string.Equals(ingredient, PayloadIngredientName, StringComparison.Ordinal)) _confirmedPayloadValue = value;
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 알 수 없는 재료 역할 '{ingredient}'. 추진력 계산식에 값이 반영되지 않음.");
        }

        /// <summary>
        /// 물질 문자열에서 계산식에 쓸 정수 값(항상 양수 크기)을 파싱함. 연료량은 값 자체가 숫자("0".."10")이고,
        /// 엔진 출력량/탑재 중량은 "고체 로켓 (+5)", "인공위성 (-3)"처럼 괄호 안의 부호 있는 숫자를 파싱함.
        /// 괄호 안 부호는 화면 표시용(플레이어에게 보너스/페널티를 직관적으로 보여주기 위함)이고,
        /// 실제 공식(엔진 출력량 x 연료량 - 탑재 중량)은 연산자 자체가 방향을 담당하므로 크기(절댓값)만 사용함.
        /// 실패 시 0.
        /// </summary>
        private int ParseIngredientValue(string ingredientName, string matterValue)
        {
            if (string.IsNullOrEmpty(matterValue)) return 0;

            if (string.Equals(ingredientName, FuelIngredientName, StringComparison.Ordinal))
            {
                if (int.TryParse(matterValue, out int fuel)) return fuel;
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] 연료량 값 '{matterValue}'이 숫자가 아님. 0으로 처리함.");
                return 0;
            }

            var match = System.Text.RegularExpressions.Regex.Match(matterValue, @"\(([+-]?\d+)\)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed)) return Math.Abs(parsed);

            if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] '{ingredientName}' 재료의 '{matterValue}' 값에서 숫자를 파싱할 수 없음. 0으로 처리함.");
            return 0;
        }

        /// <summary>
        /// 왼쪽 버튼 클릭 시 세부 물질 인덱스를 이전으로 변경함.
        /// </summary>
        private void OnLeftButtonClicked()
        {
            var matters = _currentMatters.Value;
            if (matters == null || matters.Length == 0) return;

            int len = matters.Length;
            _currentMatterIndex.Value = (_currentMatterIndex.Value - 1 + len) % len;
        }

        /// <summary>
        /// 오른쪽 버튼 클릭 시 세부 물질 인덱스를 다음으로 변경함.
        /// </summary>
        private void OnRightButtonClicked()
        {
            var matters = _currentMatters.Value;
            if (matters == null || matters.Length == 0) return;

            int len = matters.Length;
            _currentMatterIndex.Value = (_currentMatterIndex.Value + 1) % len;
        }

        /// <summary>
        /// 재료 표시 텍스트 컴포넌트 값을 변경함.
        /// </summary>
        private void UpdateIngredientText(string ingredientName)
        {
            if (textIngredient != null)
            {
                textIngredient.text = ingredientName;
            }

            UpdateRightArrowAnimation();
        }

        /// <summary>
        /// 현재 인덱스에 따라 물질 표시 텍스트 컴포넌트 값을 변경함.
        /// </summary>
        private void UpdateMatterText()
        {
            if (textMatter == null) return;

            var matters = _currentMatters.Value;
            int idx = _currentMatterIndex.Value;

            if (matters != null && idx >= 0 && idx < matters.Length)
            {
                textMatter.text = ApplyNumberSizeTag(matters[idx]);
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
            if (_missionBoard == null) return;

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
        private string ApplyNumberSizeTag(string value)
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
            if (rightArrowImages == null || rightArrowImages.Length == 0) return;

            bool hasValue = (textMatter != null && !string.IsNullOrEmpty(textMatter.text))
                          || (textIngredient != null && !string.IsNullOrEmpty(textIngredient.text));

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

            for (int i = 0; i < rightArrowImages.Length; i++) SetImageAlpha(rightArrowImages[i], 0f);

            _rightArrowSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            for (int i = 0; i < rightArrowImages.Length; i++)
            {
                _rightArrowSequence.Append(rightArrowImages[i].DOFade(1f, stepDuration));
            }

            _rightArrowSequence.AppendInterval(holdDuration);

            if (rightArrowImages.Length > 0)
            {
                _rightArrowSequence.Append(rightArrowImages[0].DOFade(0f, fadeOutDuration));
                for (int i = 1; i < rightArrowImages.Length; i++)
                {
                    _rightArrowSequence.Join(rightArrowImages[i].DOFade(0f, fadeOutDuration));
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

            if (rightArrowImages == null) return;
            for (int i = 0; i < rightArrowImages.Length; i++) SetImageAlpha(rightArrowImages[i], 0f);
        }

        /// <summary>
        /// 오브젝트 파괴 시 이벤트 구독 해제 및 리스너 정리.
        /// </summary>
        private void OnDestroy()
        {
            if (buttonLeft != null) buttonLeft.onClick.RemoveListener(OnLeftButtonClicked);
            if (buttonRight != null) buttonRight.onClick.RemoveListener(OnRightButtonClicked);
            if (buttonConfirm != null) buttonConfirm.onClick.RemoveListener(OnConfirmButtonClicked);
            if (buttonCancel != null) buttonCancel.onClick.RemoveListener(OnCancelButtonClicked);
            if (buttonCodingComplete != null) buttonCodingComplete.onClick.RemoveListener(OnCodingCompleteClicked);
            if (buttonSkip != null) buttonSkip.onClick.RemoveListener(OnSkipButtonClicked);

            _disposables.Dispose();
            _currentIngredient?.Dispose();
            _currentMatters?.Dispose();
            _currentMatterIndex?.Dispose();

            _rightArrowSequence?.Kill();
            _warningSequence?.Kill();
            _warningCts?.Cancel();
            _warningCts?.Dispose();
            _level2FillTween?.Kill();
            _level3OxygenGaugeTween?.Kill();
            _level3ElectricGaugeTween?.Kill();
            _level3OxygenIconBlinkTween?.Kill();
            _level3ElectricIconBlinkTween?.Kill();
        }
    }
}
