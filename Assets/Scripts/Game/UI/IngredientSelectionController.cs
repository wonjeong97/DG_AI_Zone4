using System;
using System.Collections.Generic;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private TMP_Text textIngredient;
        [SerializeField] private TMP_Text textMatter;
        [SerializeField] private Button buttonLeft;
        [SerializeField] private Button buttonRight;

        [Header("Workflow Buttons")]
        [SerializeField] private Button buttonConfirm;
        [SerializeField] private Button buttonCancel;
        [SerializeField] private Button buttonCodingComplete;

        [Header("Design Panel")]
        [SerializeField] private Transform designContent;

        [Header("Activation")]
        [SerializeField] private CanvasGroup gamePanel; // 게임 패널이 활성(상호작용 가능)일 때만 RFID를 처리함

        [Header("Scene Transition")]
        [SerializeField] private string resultSceneName = "3_Result";
        [SerializeField] private float sceneFadeDuration = 0.5f;

        private ISubscriber<RfidTagEvent> _subscriber;
        private SceneTransitionService _sceneTransition;
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

        /// <summary>
        /// VContainer 의존성 주입. MessagePipe 구독자, 씬 전환 서비스, 로거를 할당함.
        /// </summary>
        [Inject]
        public void Construct(ISubscriber<RfidTagEvent> subscriber, SceneTransitionService sceneTransition, ILogger<IngredientSelectionController> logger)
        {
            _subscriber = subscriber;
            _sceneTransition = sceneTransition;
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

            if (_subscriber != null)
            {
                _subscriber.Subscribe(OnRfidTagReceived).AddTo(ref _disposables);
            }

            _currentIngredient.Subscribe(UpdateIngredientText).AddTo(ref _disposables);
            _currentMatterIndex.Subscribe(_ => UpdateMatterText()).AddTo(ref _disposables);

            UpdateCodingCompleteButton();

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
                var settings = await Wonjeong.Utils.JsonLoader.LoadAsync<RfidSettings>("RfidMappings.json", this.GetCancellationTokenOnDestroy());
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
            if (designContent == null) return;

            var go = new GameObject("DesignItem", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = designContent.gameObject.layer;

            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(designContent, false);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 56f);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = textIngredient != null ? textIngredient.font
                      : (textMatter != null ? textMatter.font : null);
            text.fontSize = 44;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.richText = true;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = $"- {ingredient} [<color=yellow>{matter}</color>]";

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
        /// 코딩완료 버튼 클릭 시 화면 페이드와 함께 결과 씬으로 전환함.
        /// </summary>
        private void OnCodingCompleteClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[IngredientSelectionController] sceneTransition is null. Cannot load {resultSceneName}.");
                return;
            }

            _isBusy = true;
            if (_logger != null) _logger.ZLogInformation($"[IngredientSelectionController] Coding complete. Loading {resultSceneName}.");
            _sceneTransition.LoadSceneWithFadeAsync(resultSceneName, sceneFadeDuration).Forget();
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
                textMatter.text = matters[idx];
            }
            else
            {
                textMatter.text = "";
            }
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

            _disposables.Dispose();
            _currentIngredient?.Dispose();
            _currentMatters?.Dispose();
            _currentMatterIndex?.Dispose();
        }
    }
}
