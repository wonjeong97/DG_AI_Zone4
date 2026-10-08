using DGAIZone.App;
using DGAIZone.Game.UI;
using DGAIZone.Game.UI.States;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 레벨 4 설계창 블록 모양 검증 테스트. 반복하기는 ㄷ자 블록, 바로 뒤 이동하기는 그 안쪽, 그 밖의 이동하기는 명령 블록이 되는지 확인함.
    /// </summary>
    public class Level4DesignShapeTests
    {
        [Test]
        public void 반복하기는_ㄷ자_블록이고_바로_뒤_이동하기만_그_안쪽에_들어간다()
        {
            Assert.AreEqual(DesignStepShape.FlowControl, IngredientLevel4State.DesignShapeOf(Constants.RfidIds.Level4.Repeat, false), "반복하기는 ㄷ자 블록이어야 함");
            Assert.AreEqual(DesignStepShape.InsideFlowControl, IngredientLevel4State.DesignShapeOf(Constants.RfidIds.Level4.Move, true), "반복하기 바로 뒤 이동하기는 ㄷ자 안쪽이어야 함");
            Assert.AreEqual(DesignStepShape.Command, IngredientLevel4State.DesignShapeOf(Constants.RfidIds.Level4.Move, false), "그 밖의 이동하기는 명령 블록이어야 함");
        }
    }
}
