using System;
using DGAIZone.Game.Events;
using MessagePipe;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// CodingCategories 하위의 Image_Action/Control/Logic/Func를 평소 흑백으로 표시하다가,
    /// RFID 카드가 인식되면 해당 카드의 category와 일치하는 이미지만 원래 색으로 표시함.
    /// </summary>
    public class CodingCategoryIndicatorController : MonoBehaviour
    {
        private const string CategoryAction = "동작";
        private const string CategoryControl = "제어";
        private const string CategoryLogic = "논리";
        private const string CategoryFunc = "함수";

        [SerializeField] private Image imageAction;  // Image_Action
        [SerializeField] private Image imageControl; // Image_Control
        [SerializeField] private Image imageLogic;   // Image_Logic
        [SerializeField] private Image imageFunc;    // Image_Func
        [SerializeField] private Material grayscaleMaterial;

        private ISubscriber<RfidTagEvent> _subscriber;
        private ILogger<CodingCategoryIndicatorController> _logger;
        private IDisposable _subscription;

        /// <summary> VContainer 의존성 주입. MessagePipe 구독자와 로거를 할당함. </summary>
        [Inject]
        public void Construct(ISubscriber<RfidTagEvent> subscriber, ILogger<CodingCategoryIndicatorController> logger)
        {
            _subscriber = subscriber;
            _logger = logger;
        }

        /// <summary> 시작 시 네 이미지를 모두 흑백으로 두고 RFID 태그 이벤트를 구독함. </summary>
        private void Start()
        {
            HighlightCategory(null);

            if (_subscriber != null)
            {
                _subscription = _subscriber.Subscribe(OnRfidTagReceived);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[CodingCategoryIndicatorController] subscriber is null. Category highlight disabled.");
            }
        }

        /// <summary> RFID 태그 인식 시 해당 카드의 category만 색을 표시하고 나머지는 흑백으로 되돌림. </summary>
        private void OnRfidTagReceived(RfidTagEvent evt)
        {
            HighlightCategory(evt.Category);
        }

        /// <summary> 주어진 category와 일치하는 이미지만 원래 색으로, 나머지는 흑백 머티리얼로 전환함. </summary>
        private void HighlightCategory(string category)
        {
            ApplyState(imageAction, string.Equals(category, CategoryAction, StringComparison.Ordinal));
            ApplyState(imageControl, string.Equals(category, CategoryControl, StringComparison.Ordinal));
            ApplyState(imageLogic, string.Equals(category, CategoryLogic, StringComparison.Ordinal));
            ApplyState(imageFunc, string.Equals(category, CategoryFunc, StringComparison.Ordinal));
        }

        /// <summary> 이미지 하나에 흑백/원색 상태를 적용함. </summary>
        private void ApplyState(Image image, bool highlighted)
        {
            if (image == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[CodingCategoryIndicatorController] target image is null. Skipping.");
                return;
            }

            image.material = highlighted ? null : grayscaleMaterial;
        }

        /// <summary> 오브젝트 파괴 시 MessagePipe 구독을 해제함. </summary>
        private void OnDestroy()
        {
            _subscription?.Dispose();
        }
    }
}
