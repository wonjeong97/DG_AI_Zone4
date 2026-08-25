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
    /// 게임 씬 전용 LifetimeScope. VContainerSettings 루트 스코프를 부모로 자동 연결하며, 게임 흐름 컨트롤러와 RFID 관련 서비스 및 UI를 컨테이너에 등록함.
    /// </summary>
    public class GameSceneLifetimeScope : LifetimeScope
    {
        /// <summary> 게임 흐름 컨트롤러와 RFID 리더기 서비스, 재료 선택 UI 컨트롤러 및 이벤트 브로커를 등록함. </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<GameFlowController>();
            builder.RegisterComponentInHierarchy<TopCornerLocationIcon>();
            builder.RegisterComponentInHierarchy<RfidReaderService>();
            builder.RegisterComponentInHierarchy<KeyboardRfidSimulator>();
            builder.RegisterComponentInHierarchy<IngredientSelectionController>();
            builder.RegisterComponentInHierarchy<MissionBoardController>();
            builder.RegisterComponentInHierarchy<RobotVideoPanel>();

            MessagePipeOptions options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<RfidTagEvent>(options);
        }
    }
}
