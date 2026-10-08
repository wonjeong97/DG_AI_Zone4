using DGAIZone.App;
using DGAIZone.Game.Events;
using MessagePipe;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.Hardware
{
    /// <summary>
    /// RFID 리더기가 없는 환경에서 키보드 숫자키로 카드 인식을 대체하는 개발용 시뮬레이터.
    /// DebugInputActions의 Debug 맵 SimulateActionCard·SimulateControlCard·SimulateLogicCard·SimulateFunctionCard(기본 숫자키 1~4)를 누르면
    /// "동작"·"제어"·"논리"·"함수" 카테고리 카드를 찍은 것처럼 RfidTagEvent를 발행함.
    /// 에디터와 개발 빌드에서만 동작함. 릴리스 빌드에서는 현장 키보드·QR 스캐너 입력이 가짜 카드 인식이 되지 않도록 꺼짐.
    /// </summary>
    public class KeyboardRfidSimulator : MonoBehaviour
    {
        [SerializeField] private string simulatedReaderId = "Keyboard";

        private const string CategoryAction = Constants.RfidCategories.Action;
        private const string CategoryControl = Constants.RfidCategories.Control;
        private const string CategoryLogic = Constants.RfidCategories.Logic;
        private const string CategoryFunc = Constants.RfidCategories.Func;

        private IPublisher<RfidTagEvent> _publisher;
        private ILogger<KeyboardRfidSimulator> _logger;
        private DebugInputActions _debugInput;

        /// <summary> VContainer 의존성 주입. MessagePipe 발행자와 로거를 할당함. </summary>
        [Inject]
        public void Construct(IPublisher<RfidTagEvent> publisher, ILogger<KeyboardRfidSimulator> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        /// <summary> 카드 시뮬레이션 디버그 액션을 만들고 각 액션을 대응하는 카테고리 카드 발행에 연결함. </summary>
        private void Awake()
        {
            _debugInput = new DebugInputActions();
            DebugInputActions.DebugActions actions = _debugInput.Debug;
            actions.SimulateActionCard.performed += _ => PublishCategory(CategoryAction);
            actions.SimulateControlCard.performed += _ => PublishCategory(CategoryControl);
            actions.SimulateLogicCard.performed += _ => PublishCategory(CategoryLogic);
            actions.SimulateFunctionCard.performed += _ => PublishCategory(CategoryFunc);
        }

        /// <summary> 에디터·개발 빌드에서만 카드 시뮬레이션 디버그 액션을 켬. </summary>
        private void OnEnable()
        {
            if (!Debug.isDebugBuild) return; // 릴리스 빌드 안내는 Start에서 남김

            DebugInputActions.DebugActions actions = _debugInput.Debug;
            actions.SimulateActionCard.Enable();
            actions.SimulateControlCard.Enable();
            actions.SimulateLogicCard.Enable();
            actions.SimulateFunctionCard.Enable();
        }

        /// <summary> 카드 시뮬레이션 디버그 액션을 끔. </summary>
        private void OnDisable()
        {
            DebugInputActions.DebugActions actions = _debugInput.Debug;
            actions.SimulateActionCard.Disable();
            actions.SimulateControlCard.Disable();
            actions.SimulateLogicCard.Disable();
            actions.SimulateFunctionCard.Disable();
        }

        /// <summary> 릴리스 빌드면 숫자키 카드 시뮬레이션을 끈 상태로 둠. </summary>
        private void Start()
        {
            if (Debug.isDebugBuild) return;

            enabled = false;
        }

        /// <summary> 디버그 액션 에셋 사본을 정리함. </summary>
        private void OnDestroy()
        {
            _debugInput.Dispose();
        }

        /// <summary> 주어진 category를 실제 리더기와 동일한 형태의 RfidTagEvent로 발행함. </summary>
        private void PublishCategory(string category)
        {
            if (_publisher == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[KeyboardRfidSimulator] publisher가 null이라 카드 시뮬레이션을 발행할 수 없음.");
                return;
            }

            _publisher.Publish(new RfidTagEvent(simulatedReaderId, category)); // 처리 결과는 게임 화면이 행동 로그로 남김
        }
    }
}
