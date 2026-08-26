using System;
using System.Collections.Generic;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using MessagePipe;
using Microsoft.Extensions.Logging;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
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
        [SerializeField] private float numberFontSize = 45f; // Text_Matter/DesignItem 값이 숫자일 때 강조용 폰트 크기
        [SerializeField] private Button buttonLeft;
        [SerializeField] private Button buttonRight;

        [Header("Right Arrow Hint")]
        [SerializeField] private Image rightArrowImage; // Image_RightArrow
        [SerializeField] private float rightArrowFillDuration = 1.0f;
        [SerializeField] private float rightArrowFadeDuration = 0.5f;

        [Header("Workflow Buttons")]
        [SerializeField] private Button buttonConfirm;
        [SerializeField] private Button buttonCancel;
        [SerializeField] private Button buttonCodingComplete;
        [SerializeField] private Button buttonSkip;

        [Header("Design Panel")]
        [SerializeField] private Transform designContent;
        [SerializeField] private TextMeshProUGUI designItemPrefab;

        [Header("Activation")]
        [SerializeField] private CanvasGroup gamePanel; // 게임 패널이 활성(상호작용 가능)일 때만 RFID를 처리함

        [Header("Scene Transition")]
        [SerializeField] private float sceneFadeDuration = 0.5f;

        private const string FuelIngredientName = "연료량";
        private const string EngineIngredientName = "추진체 종류";
        private const string PayloadIngredientName = "탑재 종류";

        private ISubscriber<RfidTagEvent> _subscriber;
        private SceneTransitionService _sceneTransition;
        private MissionBoardController _missionBoard;
        private GameResultStore _resultStore;
        private ILogger<IngredientSelectionController> _logger;
        private bool _isBusy;

        private int _currentStepIndex = 0;  // 현재 read 인덱스 (0 ~ _totalSteps-1)
        private int _totalSteps = 3;        // 현재 스테이지에서 찍어야 하는 총 read 횟수
        private int _currentStageIndex = 0; // 현재 스테이지 (0부터 시작)
        private int[] _stageReadCounts = { 3 }; // 스테이지별 read 횟수 (JSON stageReadCounts)
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

        /// <summary>
        /// VContainer 의존성 주입. MessagePipe 구독자, 씬 전환 서비스, 로거를 할당함.
        /// </summary>
        [Inject]
        public void Construct(ISubscriber<RfidTagEvent> subscriber, SceneTransitionService sceneTransition, MissionBoardController missionBoard, GameResultStore resultStore, ILogger<IngredientSelectionController> logger)
        {
            _subscriber = subscriber;
            _sceneTransition = sceneTransition;
            _missionBoard = missionBoard;
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

            // 비동기로 설정을 로드하여 워크플로우 단계를 설정함
            InitializeWorkflowAsync().Forget();
        }

        /// <summary>
        /// JSON 설정파일을 로드하여 총 리더기 수(단계 수)를 동적으로 파악하고 슬롯을 준비함.
        /// </summary>
        private async UniTaskVoid InitializeWorkflowAsync()
        {
            try
            {
                var settings = await Wonjeong.Utils.JsonLoader.LoadAsync<RfidSettings>(Constants.Files.RfidMappings, this.GetCancellationTokenOnDestroy());
                if (settings != null && settings.stageReadCounts != null && settings.stageReadCounts.Length > 0)
                {
                    _stageReadCounts = settings.stageReadCounts;
                }

                _currentStageIndex = Mathf.Clamp(_currentStageIndex, 0, _stageReadCounts.Length - 1);
                _totalSteps = _stageReadCounts[_currentStageIndex];
            }
            catch (Exception e)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] Failed to load RfidMappings.json for workflow: {e.Message}");
            }

            _confirmedMatters = new string[_totalSteps];
            _confirmedIngredients = new string[_totalSteps];
            ClearDesignItems();

            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] Stage {_currentStageIndex + 1} initialized: {_totalSteps} reads required.");
        }

        /// <summary>
        /// RFID 태그 이벤트 수신 시 리더기 순서와 무관하게 현재 단계에 적용함. 리더기 하나만으로도 순차 워크플로우를 진행할 수 있음.
        /// </summary>
        private void OnRfidTagReceived(RfidTagEvent evt)
        {
            // 게임 패널이 활성 상태가 아니면(스토리 화면 등) RFID 입력을 무시함
            if (gamePanel == null || !gamePanel.interactable)
            {
                if (_logger != null)
                {
                    _logger.ZLogInformation($"[IngredientSelectionController] Game panel inactive. Ignored tag from {evt.ReaderId}.");
                }
                return;
            }

            // 모든 단계가 완료되면 더 이상 태그를 받지 않음
            if (_currentStepIndex >= _totalSteps)
            {
                if (_logger != null)
                {
                    _logger.ZLogInformation($"[IngredientSelectionController] All steps completed. Ignored tag from {evt.ReaderId}.");
                }
                return;
            }

            // 이미 디자인 컨테이너에 추가된 재료면 무시함
            if (IsIngredientConfirmed(evt.IngredientName))
            {
                if (_logger != null)
                {
                    _logger.ZLogInformation($"[IngredientSelectionController] Duplicate ingredient ignored: {evt.IngredientName}");
                }
                return;
            }

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] Reader {evt.ReaderId} tag applied to step {_currentStepIndex + 1}: {evt.IngredientName}");
            }

            _currentIngredient.Value = evt.IngredientName;
            _currentMatters.Value = evt.MatterNames ?? Array.Empty<string>();
            _currentMatterIndex.Value = 0;
            UpdateMatterText();
            UpdateProgressPreview();
        }

        /// <summary>
        /// 설정하기(Confirm) 버튼 클릭 시 현재 선택한 물질을 확정하고 다음 단계로 진행함.
        /// </summary>
        private void OnConfirmButtonClicked()
        {
            if (_confirmedMatters == null) return;

            var matters = _currentMatters.Value;
            if (string.IsNullOrEmpty(_currentIngredient.Value) || matters == null || matters.Length == 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] No active RFID tag scanned to confirm.");
                return;
            }

            if (_currentStepIndex >= _totalSteps)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] All steps are already completed.");
                return;
            }

            string ingredient = _currentIngredient.Value;
            string chosenMatter = matters[_currentMatterIndex.Value];
            int value = ParseIngredientValue(ingredient, chosenMatter);
            _confirmedMatters[_currentStepIndex] = chosenMatter;
            _confirmedIngredients[_currentStepIndex] = ingredient;
            ApplyConfirmedValue(ingredient, value);

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] Confirmed step {_currentStepIndex + 1}: {ingredient} -> {chosenMatter} (value={value})");
            }

            // 디자인 컨테이너에 확정 항목을 자식으로 추가
            AddDesignItem(ingredient, chosenMatter);

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

            if (_currentStepIndex >= _totalSteps)
            {
                if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] All {_totalSteps} steps completed successfully!");
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
                return;
            }

            // 이전 단계로 롤백
            _currentStepIndex--;

            // 되돌리는 항목의 확정 값을 계산식에서 제외(0으로 리셋)하고, 남은 확정 값들로 진행도(Image_Fill)를 다시 계산함
            ApplyConfirmedValue(_confirmedIngredients[_currentStepIndex], 0);

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

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] Reverted back to step {_currentStepIndex + 1} (Waiting for Reader_{_currentStepIndex + 1})");
            }
        }

        /// <summary>
        /// 확정된 재료/물질을 "- 재료 [물질]" 형태의 Text로 만들어 디자인 컨테이너의 자식으로 추가함. 물질은 노란색으로 표시함.
        /// </summary>
        private void AddDesignItem(string ingredient, string matter)
        {
            if (designContent == null || designItemPrefab == null) return;

            var text = Instantiate(designItemPrefab, designContent);
            text.text = $"- {ingredient} [<color=yellow>{ApplyNumberSizeTag(matter)}</color>]";

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
            var last = _designItems[lastIndex];
            _designItems.RemoveAt(lastIndex);
            if (last != null) Destroy(last.gameObject);
            UpdateCodingCompleteButton();
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
        /// </summary>
        private void UpdateCodingCompleteButton()
        {
            if (buttonCodingComplete != null)
            {
                buttonCodingComplete.interactable = _designItems.Count >= _totalSteps;
            }
        }

        /// <summary>
        /// 코딩완료 버튼 클릭 시 확정된 추진력(엔진 출력량 x 연료량 - 탑재 중량)을 목적지 조건과 대조해 성공/실패를 기록하고, 화면 페이드와 함께 결과 씬으로 전환함.
        /// </summary>
        private void OnCodingCompleteClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] sceneTransition is null. Cannot load {Constants.Scenes.Result}.");
                return;
            }

            bool success = EvaluateMission();
            if (_resultStore != null) _resultStore.Result = success ? MissionResult.Success : MissionResult.Fail;

            _isBusy = true;
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] Coding complete. Result={(success ? "Success" : "Fail")}. Loading {Constants.Scenes.Result}.");
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Result, sceneFadeDuration).Forget();
        }

        /// <summary>
        /// 스킵 버튼 클릭 시 결과를 실패로 기록하고 화면 페이드와 함께 결과 씬으로 전환함.
        /// </summary>
        private void OnSkipButtonClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] sceneTransition is null. Cannot load {Constants.Scenes.Result}.");
                return;
            }

            if (_resultStore != null) _resultStore.Result = MissionResult.Fail;

            _isBusy = true;
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] Skipped. Result=Fail. Loading {Constants.Scenes.Result}.");
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Result, sceneFadeDuration).Forget();
        }

        /// <summary>
        /// 확정된 엔진 출력량 x 연료량 - 탑재 중량 계산 결과가 이번 목적지의 조건 범위를 만족하는지 판정함.
        /// 모든 단계가 확정되지 않았거나 미션보드가 없으면 실패로 처리함.
        /// </summary>
        private bool EvaluateMission()
        {
            if (_missionBoard == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] Cannot evaluate mission (missionBoard missing). Treated as fail.");
                return false;
            }

            if (_designItems.Count < _totalSteps)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] Not all steps confirmed ({_designItems.Count}/{_totalSteps}). Treated as fail.");
                return false;
            }

            int totalThrust = CalculateTotalThrust();
            bool valid = _missionBoard.IsThrustValid(totalThrust);
            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] Total thrust {totalThrust} (engine={_confirmedEngineValue} x fuel={_confirmedFuelValue} - payload={_confirmedPayloadValue}) vs destination '{_missionBoard.Destination}' -> {(valid ? "valid" : "invalid")}");
            }
            return valid;
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

            string ingredient = _currentIngredient.Value;
            if (!string.IsNullOrEmpty(ingredient))
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
            else if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] Unknown ingredient role '{ingredient}'. Value not applied to thrust formula.");
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
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] Fuel value '{matterValue}' is not a number. Treated as 0.");
                return 0;
            }

            var match = System.Text.RegularExpressions.Regex.Match(matterValue, @"\(([+-]?\d+)\)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed)) return Math.Abs(parsed);

            if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] Could not parse numeric value from '{matterValue}' for ingredient '{ingredientName}'. Treated as 0.");
            return 0;
        }

        /// <summary>
        /// 해당 재료가 이미 디자인 컨테이너에 확정되어 있는지 검사함.
        /// </summary>
        private bool IsIngredientConfirmed(string ingredientName)
        {
            if (_confirmedIngredients == null || string.IsNullOrEmpty(ingredientName)) return false;

            for (int i = 0; i < _confirmedIngredients.Length; i++)
            {
                if (string.Equals(_confirmedIngredients[i], ingredientName, StringComparison.Ordinal)) return true;
            }
            return false;
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

            if (string.IsNullOrEmpty(_currentIngredient.Value))
            {
                _missionBoard.ResetPreview();
                return;
            }

            int previewThrust = CalculatePreviewThrust();
            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] Preview thrust adjusting: {previewThrust} (ingredient={_currentIngredient.Value})");
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
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] value is null or empty. Skipping number size tag.");
                return value;
            }

            bool isNumber = float.TryParse(value, out _);
            return isNumber ? $"<size={numberFontSize}>{value}</size>" : value;
        }

        /// <summary>
        /// Text_Matter 또는 Text_Material에 값이 있는지(RFID 태그/디버그 키로 재료가 선택된 상태인지)에 따라
        /// Image_RightArrow의 반복 펄스 애니메이션을 시작하거나 멈춤.
        /// </summary>
        private void UpdateRightArrowAnimation()
        {
            if (rightArrowImage == null) return;

            bool hasValue = (textMatter != null && !string.IsNullOrEmpty(textMatter.text))
                          || (textIngredient != null && !string.IsNullOrEmpty(textIngredient.text));

            if (hasValue) StartRightArrowLoop();
            else ResetRightArrow();
        }

        /// <summary>
        /// FillAmount 0->1로 차오른 뒤 FadeOut하고 다시 FillAmount 0으로 되돌리는 동작을 무한 반복함. 이미 재생 중이면 무시함.
        /// </summary>
        private void StartRightArrowLoop()
        {
            if (_rightArrowSequence != null && _rightArrowSequence.IsActive()) return;

            rightArrowImage.fillAmount = 0f;
            SetRightArrowAlpha(1f);

            _rightArrowSequence = DOTween.Sequence();
            _rightArrowSequence.Append(rightArrowImage.DOFillAmount(1f, rightArrowFillDuration));
            _rightArrowSequence.Append(rightArrowImage.DOFade(0f, rightArrowFadeDuration));
            _rightArrowSequence.AppendCallback(() =>
            {
                rightArrowImage.fillAmount = 0f;
                SetRightArrowAlpha(1f);
            });
            _rightArrowSequence.SetLoops(-1);
        }

        /// <summary>
        /// 반복 애니메이션을 멈추고 Image_RightArrow를 FillAmount 0의 시작 상태로 되돌림.
        /// </summary>
        private void ResetRightArrow()
        {
            _rightArrowSequence?.Kill();
            _rightArrowSequence = null;

            if (rightArrowImage == null) return;
            rightArrowImage.fillAmount = 0f;
            SetRightArrowAlpha(1f);
        }

        private void SetRightArrowAlpha(float alpha)
        {
            Color color = rightArrowImage.color;
            color.a = alpha;
            rightArrowImage.color = color;
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
        }
    }
}
