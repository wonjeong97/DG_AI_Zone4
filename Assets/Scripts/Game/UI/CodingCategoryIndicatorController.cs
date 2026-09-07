using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game.Events;
using MessagePipe;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Utils;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// CodingCategories 하위의 Image_Action/Control/Logic/Func를 평소 흑백으로 표시하다가,
    /// RFID 카드가 인식되면 해당 카드의 category만 원래 색으로 표시함.
    /// 각 이미지 위에 흑백 머티리얼을 미리 입혀 둔 오버레이 이미지(씬에 미리 배치됨)를 겹쳐두고 그 알파만 조절하는 방식으로 색상/흑백을 전환함.
    /// 대기 중(사용자가 아직 카드를 찍지 않은 상태)에 idleHintDelay(초) 동안 카드가 올라오지 않으면, 다음에 찍어야 할 카테고리의
    /// 오버레이 알파를 0~1로 반복시켜 색상과 흑백 사이를 부드럽게 오가며 숨쉬듯 안내함. 그 전에 카드가 올라오면 대기 자체가 취소됨.
    /// </summary>
    public class CodingCategoryIndicatorController : MonoBehaviour
    {
        private const string CategoryAction = "동작";
        private const string CategoryControl = "제어";
        private const string CategoryLogic = "논리";
        private const string CategoryFunc = "함수";

        [SerializeField] private Image imageAction;  // Image_Action
        [SerializeField] private Image imageActionOverlay; // Image_Action_GrayscaleOverlay (씬에 미리 배치, 흑백 머티리얼 적용됨)
        [SerializeField] private Image imageControl; // Image_Control
        [SerializeField] private Image imageControlOverlay; // Image_Control_GrayscaleOverlay
        [SerializeField] private Image imageLogic;   // Image_Logic
        [SerializeField] private Image imageLogicOverlay; // Image_Logic_GrayscaleOverlay
        [SerializeField] private Image imageFunc;    // Image_Func
        [SerializeField] private Image imageFuncOverlay; // Image_Func_GrayscaleOverlay

        [Header("Next Category Hint")]
        [SerializeField] private float idleHintDelay = 10f;      // 3_Game.json 로드 전까지의 폴백 기본값. 카드를 이 시간(초) 이상 올려놓지 않으면 힌트 페이드를 시작함
        [SerializeField] private float hintFadeDuration = 0.9f;  // 3_Game.json 로드 전까지의 폴백 기본값. 색상 <-> 흑백 한쪽 방향 전환에 걸리는 시간

        private ISubscriber<RfidTagEvent> _subscriber;
        private ILogger<CodingCategoryIndicatorController> _logger;
        private IDisposable _subscription;
        private readonly List<Tween> _hintTweens = new List<Tween>();

        // 3_Game.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private GameSceneSettings _sceneSettings;

        /// <summary> VContainer 의존성 주입. MessagePipe 구독자와 로거를 할당함. </summary>
        [Inject]
        public void Construct(ISubscriber<RfidTagEvent> subscriber, ILogger<CodingCategoryIndicatorController> logger)
        {
            _subscriber = subscriber;
            _logger = logger;
        }

        /// <summary> 시작 시 네 이미지를 모두 흑백으로 두고 RFID 태그 이벤트를 구독한 뒤 3_Game.json 연출 타이밍을 비동기로 불러옴. </summary>
        private void Start()
        {
            HighlightCategory(null);

            if (_subscriber != null)
            {
                _subscription = _subscriber.Subscribe(OnRfidTagReceived);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[CodingCategoryIndicatorController] subscriber가 null이라 카테고리 강조 표시가 비활성화됨.");
            }

            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 3_Game.json(GameSceneSettings)을 비동기로 로드함. </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Game}";
            _sceneSettings = await JsonLoader.LoadAsync<GameSceneSettings>(path, token);
        }

        /// <summary> RFID 태그 인식 시 해당 카드의 category만 색을 표시하고 나머지는 흑백으로 되돌림. 진행 중이던 힌트 페이드는 중단됨. </summary>
        private void OnRfidTagReceived(RfidTagEvent evt)
        {
            HighlightCategory(evt.Category);
        }

        /// <summary>
        /// 다음으로 인식되어야 할 카테고리(들)를 흑백 기준 상태로 되돌린 뒤, idleHintDelay(초) 동안 카드가 올라오지 않으면
        /// 오버레이 알파를 0(원래 색)~1(흑백) 사이로 계속 반복시켜 색상과 흑백을 오가며 부드럽게 페이드함.
        /// 대기 시간 안에 카드가 인식되면(HighlightCategory -> StopHint) 페이드가 시작되기 전에 취소됨.
        /// 확정 직후 등 이전 강조 표시를 정리하는 용도로도 쓰임(categories가 비었으면 힌트 없이 기준 상태만 적용됨).
        /// </summary>
        public void ShowNextHint(string[] categories)
        {
            HighlightCategory(null);

            if (categories == null) return;

            for (int i = 0; i < categories.Length; i++)
            {
                Image overlay = GetOverlayForCategory(categories[i]);
                if (overlay != null) StartBreathing(overlay);
            }
        }

        /// <summary> 진행 중인 힌트 페이드를 모두 멈추고 네 오버레이의 알파를 1(흑백)로 되돌림. </summary>
        public void StopHint()
        {
            for (int i = 0; i < _hintTweens.Count; i++) _hintTweens[i]?.Kill();
            _hintTweens.Clear();

            ResetAlpha(imageActionOverlay);
            ResetAlpha(imageControlOverlay);
            ResetAlpha(imageLogicOverlay);
            ResetAlpha(imageFuncOverlay);
        }

        /// <summary> 카테고리 문자열에 대응하는 오버레이 이미지를 반환함. 일치하는 것이 없으면 null. </summary>
        private Image GetOverlayForCategory(string category)
        {
            if (string.Equals(category, CategoryAction, StringComparison.Ordinal)) return imageActionOverlay;
            if (string.Equals(category, CategoryControl, StringComparison.Ordinal)) return imageControlOverlay;
            if (string.Equals(category, CategoryLogic, StringComparison.Ordinal)) return imageLogicOverlay;
            if (string.Equals(category, CategoryFunc, StringComparison.Ordinal)) return imageFuncOverlay;
            return null;
        }

        /// <summary> 주어진 category와 일치하는 이미지만 원래 색으로(오버레이 알파 0), 나머지는 흑백으로(오버레이 알파 1) 전환함. 힌트 페이드는 항상 먼저 정리됨. </summary>
        private void HighlightCategory(string category)
        {
            StopHint();

            ApplyState(imageActionOverlay, string.Equals(category, CategoryAction, StringComparison.Ordinal));
            ApplyState(imageControlOverlay, string.Equals(category, CategoryControl, StringComparison.Ordinal));
            ApplyState(imageLogicOverlay, string.Equals(category, CategoryLogic, StringComparison.Ordinal));
            ApplyState(imageFuncOverlay, string.Equals(category, CategoryFunc, StringComparison.Ordinal));
        }

        /// <summary> 오버레이 하나의 알파를 강조 여부에 따라 0(원래 색) 또는 1(흑백)로 설정함. </summary>
        private void ApplyState(Image overlay, bool highlighted)
        {
            if (overlay == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] overlay image가 null이라 건너뜀.");
                return;
            }

            SetAlpha(overlay, highlighted ? 0f : 1f);
        }

        /// <summary> 오버레이 알파만 1(흑백)로 스냅함. </summary>
        private void ResetAlpha(Image overlay)
        {
            if (overlay != null) SetAlpha(overlay, 1f);
        }

        /// <summary>
        /// 오버레이를 즉시 알파 1(흑백)로 스냅한 뒤, idleHintDelay(초)만큼 기다렸다가 0(원래 색)까지 무한 반복(Yoyo)으로
        /// 부드럽게 오가게 함. 대기 중에 StopHint가 호출되면(카드 인식 등) 페이드가 시작되기 전에 트윈째로 취소됨.
        /// </summary>
        private void StartBreathing(Image overlay)
        {
            SetAlpha(overlay, 1f);
            Tween tween = overlay.DOFade(0f, _sceneSettings?.hintFadeDuration ?? hintFadeDuration)
                .SetDelay(_sceneSettings?.idleHintDelay ?? idleHintDelay)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject);
            _hintTweens.Add(tween);
        }

        private void SetAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        /// <summary> 오브젝트 파괴 시 MessagePipe 구독 해제 및 힌트 트윈을 정리함. </summary>
        private void OnDestroy()
        {
            _subscription?.Dispose();

            for (int i = 0; i < _hintTweens.Count; i++) _hintTweens[i]?.Kill();
            _hintTweens.Clear();
        }
    }
}
