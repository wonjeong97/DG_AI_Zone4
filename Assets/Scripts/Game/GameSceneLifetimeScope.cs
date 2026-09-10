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
        /// 루트 스코프인 GameLifetimeScope를 부모로 찾음. Find&lt;GameLifetimeScope&gt;() 대신 정적
        /// GameLifetimeScope.Instance만 사용하는 ResolveAndEnsureBuilt()를 통해야, 0_Title 재로드로
        /// 생긴(곧 자멸할) 중복 인스턴스를 잘못 부모로 삼는 레이스를 피할 수 있음(자세한 배경은
        /// GameLifetimeScope 클래스 주석 참고).
        /// </summary>
        protected override LifetimeScope FindParent() => GameLifetimeScope.ResolveAndEnsureBuilt();

        /// <summary> 게임 흐름 컨트롤러와 RFID 리더기 서비스, 재료 선택 UI 컨트롤러 및 이벤트 브로커를 등록함. </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<GameFlowController>();
            builder.RegisterComponentInHierarchy<RfidReaderService>();
            builder.RegisterComponentInHierarchy<KeyboardRfidSimulator>();
            builder.RegisterComponentInHierarchy<IngredientSelectionController>();
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
