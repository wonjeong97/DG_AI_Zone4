using DGAIZone.App;
using DGAIZone.Game.UI.States;
using DGAIZone.Result;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 결과 영상 선택 검증 테스트. 레벨 3 실패 원인(논리 오류·전기 부족·산소 부족)에 따른 영상 접미사와,
    /// 레벨·결과·접미사로 만드는 결과 영상 파일명("4-3-Fail-O2.mp4" 등)을 확인함.
    /// </summary>
    public class ResultVideoTests
    {
        [Test]
        public void 레벨3_전기나_산소_하나만_모자라면_그_원인_영상_접미사이고_그_밖에는_기본_실패_영상이다()
        {
            Assert.AreEqual(Constants.Files.ResultVideoFailElectricity, IngredientLevel3State.FailVideoSuffixOf(false, 0.5f, 1f), "전기만 모자라면 Electricity");
            Assert.AreEqual(Constants.Files.ResultVideoFailO2, IngredientLevel3State.FailVideoSuffixOf(false, 1f, 0f), "산소만 모자라면 O2");
            Assert.IsNull(IngredientLevel3State.FailVideoSuffixOf(false, 0.5f, 0.5f), "둘 다 모자라면 기본 실패 영상");
            Assert.IsNull(IngredientLevel3State.FailVideoSuffixOf(true, 0.5f, 1f), "논리 블록이 '또는'이면 전기가 모자라도 기본 실패 영상");
            Assert.IsNull(IngredientLevel3State.FailVideoSuffixOf(true, 1f, 1f), "논리 블록만 틀려도 기본 실패 영상");
        }

        [Test]
        public void 결과_영상_파일명은_실패이고_접미사가_있을_때만_원인_영상이다()
        {
            Assert.AreEqual("4-3-Fail-O2.mp4", ResultVideoPanel.GetVideoFileName(3, false, Constants.Files.ResultVideoFailO2));
            Assert.AreEqual("4-3-Fail-Electricity.mp4", ResultVideoPanel.GetVideoFileName(3, false, Constants.Files.ResultVideoFailElectricity));
            Assert.AreEqual("4-3-Fail.mp4", ResultVideoPanel.GetVideoFileName(3, false, null), "접미사가 없으면 기본 실패 영상");
            Assert.AreEqual("4-3-Success.mp4", ResultVideoPanel.GetVideoFileName(3, true, Constants.Files.ResultVideoFailO2), "성공이면 접미사를 무시함");
            Assert.AreEqual("4-5-Success.mp4", ResultVideoPanel.GetVideoFileName(ResultVideoPanel.ClampLevel(5), true), "레벨 5는 레벨 5 영상을 씀(마지막 레벨 5)");
        }
    }
}
