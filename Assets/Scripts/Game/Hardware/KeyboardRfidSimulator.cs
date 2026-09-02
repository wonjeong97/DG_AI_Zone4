using System;
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
    /// RFID 리더기가 없는 환경에서 키보드 숫자키 1번으로 "동작" 카드 인식을 대체하는 개발용 시뮬레이터.
    /// 1번 키를 누르면 RfidMappings.json에 등록된 카드의 category를 실제 리더기와 동일하게 RfidTagEvent로 발행함.
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
            try
            {
                var settings = await JsonLoader.LoadAsync<RfidSettings>(Constants.Files.RfidMappings, token);
                var mappings = settings != null ? settings.mappings : null;
                if (mappings == null || mappings.Length == 0)
                {
                    if (_logger != null) _logger.ZLogWarning($"[KeyboardRfidSimulator] 매핑이 로드되지 않아 키보드 시뮬레이션이 비활성화됨.");
                    return;
                }

                _mappings = mappings;
                if (_logger != null) _logger.ZLogInformation($"[KeyboardRfidSimulator] 카드 매핑 {_mappings.Length}개 로드됨. 1번 키를 눌러 동작 카드를 시뮬레이션하세요.");
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소 시 예외 무시
            }
        }

        /// <summary> 매 프레임 1번 키 입력을 확인해 "동작" 카드 인식을 발행함. </summary>
        private void Update()
        {
            if (_mappings == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[Key.Digit1].wasPressedThisFrame)
            {
                PublishActionCard();
            }
        }

        /// <summary> 매핑 목록의 첫 카드 category를 실제 리더기와 동일한 형태의 RfidTagEvent로 발행함. </summary>
        private void PublishActionCard()
        {
            if (_publisher == null || _mappings == null || _mappings.Length == 0) return;

            RfidMappingItem item = _mappings[0];
            if (item == null) return;

            _publisher.Publish(new RfidTagEvent(simulatedReaderId, item.category));
            if (_logger != null) _logger.ZLogInformation($"[KeyboardRfidSimulator] 카드 시뮬레이션됨: category={item.category}");
        }
    }
}
