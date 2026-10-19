using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// CodingCategories 하위 칸(Slot_1~Slot_5)으로 이번 판 단계마다 받을 카드 분류를 순서대로 보여 줌(레벨 1은 동작 3칸, 레벨 3은 만약 > 동작 > 논리 > 만약 > 동작 등).
    /// 칸 수와 분류는 IngredientSelectionController가 레벨 시작 때 SetSlots로 정하고, 남는 칸은 숨김(CodingCategories의 HorizontalLayoutGroup이 보이는 칸만 나란히 둠).
    /// 칸마다 분류 색 이미지 위에 흑백 머티리얼을 입힌 오버레이 이미지(씬에 미리 배치됨)를 겹쳐 두고 그 알파만 조절해 색/흑백을 전환함:
    /// 설정을 마친 앞 칸과 카드가 인식된 지금 칸은 색(오버레이 알파 0), 나머지는 흑백(알파 1).
    /// 지금 칸에 idleHintDelay(초) 동안 카드가 올라오지 않으면 그 칸 오버레이 알파를 0~1로 반복시켜 색상과 흑백 사이를 부드럽게 오가며 안내함.
    /// 그 전에 카드가 올라오면 대기 자체가 취소됨.
    /// RFID 태그를 직접 구독하지 않고, IngredientSelectionController가 현재 단계에 허용된 카드로 판정했을 때만
    /// HighlightStep을 호출해줌(허용되지 않는 카드는 경고만 뜨고 흑백 상태가 그대로 유지되도록 하기 위함).
    /// </summary>
    public class CodingCategoryIndicatorController : MonoBehaviour
    {
        /// <summary> 칸 하나(Slot_N)의 화면 요소. </summary>
        [Serializable]
        internal class StepSlot
        {
            public GameObject root;        // Slot_N
            public Image colorImage;       // Slot_N/Image_Color — 분류 색
            public TMP_Text label;         // Slot_N/Image_Color/Text_Label — 분류 이름
            public Image grayscaleOverlay; // Slot_N/Image_GrayscaleOverlay — 흑백 머티리얼, 알파 1이면 칸을 흑백으로 덮음
        }

        [SerializeField] private StepSlot[] slots; // Slot_1..Slot_5 순서(단계 순서)

        [Header("Category Colors")]
        [SerializeField] private Color actionColor = new Color(0f, 0f, 1f, 1f);
        [SerializeField] private Color controlColor = new Color(1f, 0.7607843f, 0f, 1f);
        [SerializeField] private Color logicColor = new Color(0.11764706f, 0.70980394f, 0.03137255f, 1f);
        [SerializeField] private Color funcColor = new Color(0.7764706f, 0.03529412f, 0.03529412f, 1f);

        private ILogger<CodingCategoryIndicatorController> _logger;
        private readonly List<Tween> _hintTweens = new List<Tween>();
        private readonly Dictionary<Image, CanvasGroup> _overlayCanvasGroups = new Dictionary<Image, CanvasGroup>();
        private int _slotCount; // 이번 판에 보이는 칸 수. SetSlots 전에는 0이라 칸이 보이지 않음

        // 3_Game.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private GameSceneSettings _sceneSettings = new GameSceneSettings();

        /// <summary> 이번 판에 보이는 칸 수. </summary>
        internal int VisibleSlotCount => _slotCount;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<CodingCategoryIndicatorController> logger)
        {
            _logger = logger;
        }

        /// <summary> 단계별 분류가 정해지기 전(RfidMappings.json 로드 전)에는 칸을 모두 숨김. </summary>
        private void Awake()
        {
            SetVisibleSlotCount(0);
        }

        /// <summary> 3_Game.json 연출 타이밍을 비동기로 불러옴. </summary>
        private void Start()
        {
            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 3_Game.json(GameSceneSettings)을 GameSceneSettingsProvider를 통해 비동기로 불러옴(씬 내 다른 컨트롤러와 로드를 공유함). </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            _sceneSettings = await GameSceneSettingsProvider.GetAsync(token);
        }

        /// <summary> 테스트 전용: 씬 없이 만든 칸을 넣음. </summary>
        internal void SetSlotViewsForTest(StepSlot[] views)
        {
            slots = views;
        }

        /// <summary>
        /// 이번 판 단계별 분류로 칸을 정함: 앞에서부터 분류 수만큼 칸을 보여 주고 분류 색·이름(제어 칸은 controlLabel)을 입혀 모두 흑백으로 두며, 남는 칸은 숨김.
        /// categories가 비었으면(레벨 상태가 분류를 정하지 못함) 칸을 모두 숨기고, 칸보다 단계가 많으면 앞 단계만 보여 줌.
        /// </summary>
        public void SetSlots(IReadOnlyList<string> categories, string controlLabel)
        {
            int count = categories?.Count ?? 0;
            if (count == 0 && _logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] 단계별 카드 분류가 없어 칸을 모두 숨김.");

            int slotLength = slots?.Length ?? 0;
            if (count > slotLength)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] 단계가 {count}개인데 칸이 {slotLength}개라 앞 {slotLength}단계만 보여 줌.");
                count = slotLength;
            }

            for (int i = 0; i < count; i++)
            {
                ApplyCategory(i, categories[i], controlLabel);
            }

            SetVisibleSlotCount(count);
            ApplyColoredCount(0);
        }

        /// <summary>
        /// 지금 단계(currentStep)에 맞춰 칸을 둠: 설정을 마친 앞 칸은 색, 지금 칸과 뒤 칸은 흑백. 지금 칸은 idleHintDelay(초) 동안 카드가 올라오지 않으면
        /// 색과 흑백을 부드럽게 오가며 안내함. 모든 단계를 마쳤으면(currentStep이 칸 수 이상) 안내 없이 모든 칸을 색으로 둠.
        /// </summary>
        public void ShowStep(int currentStep)
        {
            ApplyColoredCount(currentStep);
            if (currentStep >= 0 && currentStep < _slotCount) StartBreathing(currentStep);
        }

        /// <summary> 지금 단계(currentStep)에 허용된 카드가 인식됐을 때 부름: 설정을 마친 앞 칸과 지금 칸을 색으로, 뒤 칸은 흑백으로 두고 안내를 멈춤. </summary>
        public void HighlightStep(int currentStep)
        {
            ApplyColoredCount(currentStep + 1);
        }

        /// <summary> 진행 중인 안내 페이드를 모두 멈춤(알파는 호출부가 정함). </summary>
        private void StopHint()
        {
            for (int i = 0; i < _hintTweens.Count; i++) _hintTweens[i]?.Kill();
            _hintTweens.Clear();
        }

        /// <summary> 안내 페이드를 멈추고, 보이는 칸 중 앞 coloredCount개는 색(오버레이 알파 0), 나머지는 흑백(알파 1)으로 둠. </summary>
        private void ApplyColoredCount(int coloredCount)
        {
            StopHint();

            for (int i = 0; i < _slotCount; i++)
            {
                CanvasGroup group = GetOverlayGroup(i);
                if (group) group.alpha = i < coloredCount ? 0f : 1f;
            }
        }

        /// <summary> index번째 칸에 분류 색과 이름을 입힘. </summary>
        private void ApplyCategory(int index, string category, string controlLabel)
        {
            StepSlot slot = slots[index];
            if (slot == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] slots[{index}]가 비어 있어 {index + 1}번째 단계 칸을 만들 수 없음.");
                return;
            }

            if (slot.colorImage) slot.colorImage.color = ColorOf(category);
            else if (_logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] slots[{index}].colorImage가 null이라 {index + 1}번째 칸 색을 바꿀 수 없음.");

            if (slot.label) slot.label.text = LabelOf(category, controlLabel);
            else if (_logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] slots[{index}].label이 null이라 {index + 1}번째 칸 이름을 바꿀 수 없음.");
        }

        /// <summary> 분류 색. 알 수 없는 분류(단계 정의 오타 등)면 경고를 남기고 회색. </summary>
        private Color ColorOf(string category)
        {
            if (string.Equals(category, Constants.RfidCategories.Action, StringComparison.Ordinal)) return actionColor;
            if (string.Equals(category, Constants.RfidCategories.Control, StringComparison.Ordinal)) return controlColor;
            if (string.Equals(category, Constants.RfidCategories.Logic, StringComparison.Ordinal)) return logicColor;
            if (string.Equals(category, Constants.RfidCategories.Func, StringComparison.Ordinal)) return funcColor;

            if (_logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] '{category}'는 알 수 없는 분류라 칸을 회색으로 둠.");
            return Color.gray;
        }

        /// <summary> 칸 이름. 제어 칸은 레벨마다 다른 블록 이름(controlLabel, 만약·반복)을 쓰고, 나머지는 분류 이름. </summary>
        internal static string LabelOf(string category, string controlLabel)
        {
            if (string.Equals(category, Constants.RfidCategories.Control, StringComparison.Ordinal) && !string.IsNullOrEmpty(controlLabel)) return controlLabel;
            return category ?? "";
        }

        /// <summary> 앞 count개 칸만 켜고 나머지는 끔. </summary>
        private void SetVisibleSlotCount(int count)
        {
            _slotCount = count;
            if (slots == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] slots가 연결되지 않아 칸을 보여 줄 수 없음.");
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                GameObject root = slots[i]?.root;
                if (root) root.SetActive(i < count);
                else if (_logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] slots[{i}].root가 null이라 {i + 1}번째 칸을 켜거나 끌 수 없음.");
            }
        }

        /// <summary>
        /// index번째 칸 오버레이를 즉시 알파 1(흑백)로 스냅한 뒤, idleHintDelay(초)만큼 기다렸다가 0(원래 색)까지 무한 반복(Yoyo)으로
        /// 부드럽게 오가게 함. 대기 중에 다른 칸 상태로 바뀌면(카드 인식 등) 페이드가 시작되기 전에 트윈째로 취소됨.
        /// CanvasGroup을 사용해 이미지 버텍스 리빌드 없이 GPU 알파 블렌딩으로 연출함.
        /// </summary>
        private void StartBreathing(int index)
        {
            CanvasGroup group = GetOverlayGroup(index);
            if (!group) return; // GetOverlayGroup이 경고함

            group.alpha = 1f;
            Tween tween = group.DOFade(0f, _sceneSettings.hintFadeDuration)
                .SetDelay(_sceneSettings.idleHintDelay)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject);
            _hintTweens.Add(tween);
        }

        /// <summary> index번째 칸 오버레이의 CanvasGroup을 조회하거나 없으면 추가하여 반환함. 오버레이가 없으면 경고를 남기고 null. </summary>
        private CanvasGroup GetOverlayGroup(int index)
        {
            Image overlay = slots != null && index < slots.Length ? slots[index]?.grayscaleOverlay : null;
            if (!overlay)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] slots[{index}].grayscaleOverlay가 null이라 {index + 1}번째 칸 색/흑백을 바꿀 수 없음.");
                return null;
            }

            if (!_overlayCanvasGroups.TryGetValue(overlay, out CanvasGroup group))
            {
                if (!overlay.TryGetComponent(out group))
                {
                    group = overlay.gameObject.AddComponent<CanvasGroup>();
                }
                _overlayCanvasGroups[overlay] = group;
            }
            return group;
        }

        /// <summary> 오브젝트 파괴 시 안내 트윈을 정리함. </summary>
        private void OnDestroy()
        {
            StopHint();
        }
    }
}
