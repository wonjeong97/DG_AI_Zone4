using DGAIZone.App;
using DGAIZone.Game.Events;
using DGAIZone.Game.Hardware;
using DGAIZone.Game.UI;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace DGAIZone.Game
{
    /// <summary>
    /// 게임 씬 전용 LifetimeScope. 0_Title에서부터 DontDestroyOnLoad로 유지되는 GameLifetimeScope(루트)를
    /// 부모로 직접 찾아 연결하며, 게임 흐름 컨트롤러와 RFID 관련 서비스 및 UI를 컨테이너에 등록함.
    /// </summary>
    public class GameSceneLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// 루트 스코프인 GameLifetimeScope를 부모로 찾음. 앱 시작 직후에는 이 Awake가 VContainerSettings의
        /// 루트 자동 생성(첫 씬 sceneLoaded 콜백)보다 먼저 실행되므로, ResolveAndEnsureBuilt()로 루트의
        /// 생성·빌드를 보장받음(자세한 배경은 GameLifetimeScope 클래스 주석 참고).
        /// </summary>
        protected override LifetimeScope FindParent() => GameLifetimeScope.ResolveAndEnsureBuilt();

        /// <summary> 게임 흐름 컨트롤러와 RFID 리더기 서비스, 재료 선택 UI 컨트롤러 및 이벤트 브로커를 등록함. </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<GameFlowController>();
            builder.RegisterComponentInHierarchy<RfidReaderService>();
            builder.RegisterComponentInHierarchy<KeyboardRfidSimulator>();
            builder.RegisterComponentInHierarchy<IngredientSelectionController>();
            builder.RegisterComponentInHierarchy<RightArrowHint>();
            builder.RegisterComponentInHierarchy<InvalidCardWarning>();
            builder.RegisterComponentInHierarchy<DesignPanel>();
            builder.RegisterComponentInHierarchy<MissionBoardController>();
            builder.RegisterComponentInHierarchy<CodingCategoryIndicatorController>();
            builder.RegisterComponentInHierarchy<RobotVideoPanel>();
            builder.RegisterComponentInHierarchy<Level4BoardController>();

            MessagePipeOptions options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<RfidTagEvent>(options);
            builder.RegisterMessageBroker<RfidReaderIdleEvent>(options);
        }
    }
}
