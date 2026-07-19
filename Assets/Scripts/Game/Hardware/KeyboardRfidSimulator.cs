using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using MessagePipe;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using Wonjeong.Utils;
using ZLogger;

namespace DGAIZone.Game.Hardware
{
    /// <summary>
    /// RFID 리더기가 없는 환경에서 키보드 숫자키(1~9)로 카드 인식을 대체하는 개발용 시뮬레이터.
    /// 숫자키 N을 누르면 RfidMappings.json의 N번째 매핑 값을 실제 리더기와 동일하게 RfidTagEvent로 발행함.
    /// </summary>
    public class KeyboardRfidSimulator : MonoBehaviour
    {
        [SerializeField] private string simulatedReaderId = "Keyboard";

        private IPublisher<RfidTagEvent> _publisher;
        private ILogger<KeyboardRfidSimulator> _logger;
        private RfidMappingItem[] _mappings;

        /// <summary> VContainer 의존성 주입. MessagePipe 발행자와 로거를 할당함. </summary>
        [Inject]
        public void Construct(IPublisher<RfidTagEvent> publisher, ILogger<KeyboardRfidSimulator> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        /// <summary> 씬 시작 시 카드 매핑을 비동기로 로드함. </summary>
        private void Start()
        {
            LoadMappingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 실제 리더기와 동일한 RfidMappings.json에서 카드 매핑 목록을 로드함. </summary>
        private async UniTaskVoid LoadMappingsAsync(CancellationToken token)
        {
            var settings = await JsonLoader.LoadAsync<RfidSettings>(Constants.Files.RfidMappings, token);
            if (settings == null || settings.mappings == null || settings.mappings.Length == 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[KeyboardRfidSimulator] No mappings loaded. Keyboard simulation disabled.");
                return;
            }

            _mappings = settings.mappings;
            if (_logger != null) _logger.ZLogInformation($"[KeyboardRfidSimulator] Loaded {_mappings.Length} card mappings. Press number keys 1-{_mappings.Length} to simulate.");
        }

        /// <summary> 매 프레임 숫자키 입력을 확인해 해당 카드 인식을 발행함. </summary>
        private void Update()
        {
            if (_mappings == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < _mappings.Length; i++)
            {
                Key key = Key.Digit1 + i; // Digit1..Digit9 는 열거형에서 연속됨
                if (key > Key.Digit9) break;

                if (keyboard[key].wasPressedThisFrame)
                {
                    PublishCard(i);
                }
            }
        }

        /// <summary> 지정한 인덱스의 카드 매핑을 실제 리더기와 동일한 형태의 RfidTagEvent로 발행함. </summary>
        private void PublishCard(int index)
        {
            if (_publisher == null || _mappings == null || index < 0 || index >= _mappings.Length) return;

            RfidMappingItem item = _mappings[index];
            if (item == null) return;

            string ingredientName = item.ingredientName;
            string[] matterNames = (item.matterNames != null && item.matterNames.Length > 0)
                ? item.matterNames
                : new string[] { $"{ingredientName}-1", $"{ingredientName}-2", $"{ingredientName}-3" };

            _publisher.Publish(new RfidTagEvent(simulatedReaderId, ingredientName, matterNames));
            if (_logger != null) _logger.ZLogInformation($"[KeyboardRfidSimulator] Simulated card {index + 1}: {ingredientName} ({matterNames.Length} matters)");
        }
    }
}
