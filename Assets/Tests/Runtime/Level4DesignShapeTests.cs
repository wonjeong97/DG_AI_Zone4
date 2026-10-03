using System.Collections.Generic;
using DGAIZone.App;
using DGAIZone.Game.Data;
using DGAIZone.Game.UI;
using DGAIZone.Game.UI.States;
using HuliacDev.Utils;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 레벨 4 설계창 블록 모양 검증 테스트. 반복하기는 ㄷ자 블록, 바로 뒤 이동하기는 그 안쪽, 그 밖의 이동하기는 명령 블록이 되는지와
    /// 실제 StreamingAssets/RfidMappings.json 단계 정의로 계산한 '가장 길게 쌓인 모양'이 카드 규칙과 맞는지 확인함.
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

        [Test]
        public void 가장_길게_쌓인_모양은_반복하기를_가장_많이_쓴_경우다()
        {
            RfidSettings settings = JsonLoader.Load<RfidSettings>(Constants.Files.RfidMappings);
            Assert.IsNotNull(settings, "RfidMappings.json을 읽지 못함");
            RfidLevelMapping mapping = settings.FindLevelMapping(4);
            Assert.IsNotNull(mapping, "레벨 4 정의가 없음");

            List<DesignStepShape> shapes = new List<DesignStepShape>();
            IngredientLevel4State.FillPlannedShapes(mapping.steps, mapping.steps.Length, shapes);

            // 1~4단계는 제어·동작 카드를, 5단계는 동작 카드만 받으므로 '반복+이동, 반복+이동, 이동'이 가장 김
            CollectionAssert.AreEqual(new[]
            {
                DesignStepShape.FlowControl, DesignStepShape.InsideFlowControl,
                DesignStepShape.FlowControl, DesignStepShape.InsideFlowControl,
                DesignStepShape.Command
            }, shapes);
        }
    }
}
