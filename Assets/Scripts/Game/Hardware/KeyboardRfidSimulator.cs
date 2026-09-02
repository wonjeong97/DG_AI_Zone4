using DGAIZone.Game.Events;
using MessagePipe;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.Hardware
{
    /// <summary>
    /// RFID 리더기가 없는 환경에서 키보드 숫자키로 카드 인식을 대체하는 개발용 시뮬레이터.
    /// 1번 키는 "동작", 2번 키는 "제어", 3번 키는 "논리", 4번 키는 "함수" 카테고리 카드를 찍은 것처럼 RfidTagEvent를 발행함.
    /// </summary>
    public class KeyboardRfidSimulator : MonoBehaviour
    {
        [SerializeField] private string simulatedReaderId = "Keyboard";

        private const string CategoryAction = "동작";
        private const string CategoryControl = "제어";
        private const string CategoryLogic = "논리";
        private const string CategoryFunc = "함수";

        // 인덱스 = 눌러야 하는 숫자키 순서(1번키부터). Key.Digit1 + i 로 계산됨.
        private static readonly string[] KeyCategories = { CategoryAction, CategoryControl, CategoryLogic, CategoryFunc };

        private IPublisher<RfidTagEvent> _publisher;
        private ILogger<KeyboardRfidSimulator> _logger;

        /// <summary> VContainer 의존성 주입. MessagePipe 발행자와 로거를 할당함. </summary>
        [Inject]
        public void Construct(IPublisher<RfidTagEvent> publisher, ILogger<KeyboardRfidSimulator> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        /// <summary> 매 프레임 1~4번 키 입력을 확인해 대응하는 카테고리 카드 인식을 발행함. </summary>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < KeyCategories.Length; i++)
            {
                Key key = Key.Digit1 + i; // Digit1..Digit9 는 열거형에서 연속됨
                if (keyboard[key].wasPressedThisFrame)
                {
                    PublishCategory(KeyCategories[i]);
                }
            }
        }

        /// <summary> 주어진 category를 실제 리더기와 동일한 형태의 RfidTagEvent로 발행함. </summary>
        private void PublishCategory(string category)
        {
            if (_publisher == null) return;

            _publisher.Publish(new RfidTagEvent(simulatedReaderId, category));
            if (_logger != null) _logger.ZLogInformation($"[KeyboardRfidSimulator] 카드 시뮬레이션됨: category={category}");
        }
    }
}
