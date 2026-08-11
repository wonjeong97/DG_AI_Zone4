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
            _currentMatterIndex.Subscribe(_ => UpdateMatterText()).AddTo(ref _disposables);

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
            _confirmedMatters[_currentStepIndex] = chosenMatter;
            _confirmedIngredients[_currentStepIndex] = ingredient;

            if (_logger != null)
            {
                _logger.ZLogInformation($"[IngredientSelectionController] Confirmed step {_currentStepIndex + 1}: {ingredient} -> {chosenMatter}");
            }

            // 디자인 컨테이너에 확정 항목을 자식으로 추가
            AddDesignItem(ingredient, chosenMatter);

            // 연료량이 확정되면 목적지 조건에 맞춰 진행도(Image_Fill)를 갱신함
            if (string.Equals(ingredient, FuelIngredientName, StringComparison.Ordinal) && int.TryParse(chosenMatter, out int fuelValue) && _missionBoard != null)
            {
                _missionBoard.SetFuelProgress(fuelValue);
            }

            // 현재 카드 선택 대기 상태 초기화
            _currentIngredient.Value = "";
            _currentMatters.Value = Array.Empty<string>();
            _currentMatterIndex.Value = 0;
            UpdateMatterText();

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
                return;
            }

            // 이전 단계로 롤백
            _currentStepIndex--;

            // 되돌리는 항목이 연료량이면 진행도(Image_Fill)를 초기 상태로 되돌림
            if (string.Equals(_confirmedIngredients[_currentStepIndex], FuelIngredientName, StringComparison.Ordinal) && _missionBoard != null)
            {
                _missionBoard.ResetFuelProgress();
            }

            // 이전 단계의 확정 내역 삭제
            _confirmedMatters[_currentStepIndex] = null;
            _confirmedIngredients[_currentStepIndex] = null;
            RemoveLastDesignItem();

            // 현재 임시 선택 상태 초기화
            _currentIngredient.Value = "";
            _currentMatters.Value = Array.Empty<string>();
            _currentMatterIndex.Value = 0;
            UpdateMatterText();

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
        /// 코딩완료 버튼 클릭 시 확정된 연료량을 목적지 조건과 대조해 성공/실패를 기록하고, 화면 페이드와 함께 결과 씬으로 전환함.
        /// </summary>
        private void OnCodingCompleteClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] sceneTransition is null. Cannot load {Constants.Scenes.Result}.");
                return;
            }

            bool success = EvaluateFuel();
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
        /// 확정된 재료 중 연료량 값을 찾아 이번 목적지의 조건 범위를 만족하는지 판정함. 연료량 미설정/파싱 실패/미션보드 부재 시 실패로 처리함.
        /// </summary>
        private bool EvaluateFuel()
        {
            if (_missionBoard == null || _confirmedIngredients == null || _confirmedMatters == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] Cannot evaluate fuel (missionBoard/confirmed data missing). Treated as fail.");
                return false;
            }

            for (int i = 0; i < _confirmedIngredients.Length; i++)
            {
                if (!string.Equals(_confirmedIngredients[i], FuelIngredientName, StringComparison.Ordinal)) continue;

                if (int.TryParse(_confirmedMatters[i], out int fuel))
                {
                    bool valid = _missionBoard.IsFuelValid(fuel);
                    if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] Fuel {fuel} vs destination '{_missionBoard.Destination}' -> {(valid ? "valid" : "invalid")}");
                    return valid;
                }

                if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] Fuel value '{_confirmedMatters[i]}' is not a number. Treated as fail.");
                return false;
            }

            if (_logger != null) _logger.ZLogWarning($"[IngredientSelectionController] No fuel ingredient confirmed. Treated as fail.");
            return false;
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
