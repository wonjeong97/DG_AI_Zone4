using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DGAIZone.App;
using DGAIZone.Game.Data;
using DGAIZone.Game.UI;
using DGAIZone.Game.UI.States;
using HuliacDev.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 레벨 5(임시 규칙) 검증 테스트. 카드 분류별 설계창 블록 모양과 설계창 크기 계산용 모양, 분류별 장수 세기와 '모두 놓으면 성공' 판정,
    /// 현재 상황 화면 그림을 보여 줄 함수 정의 블록 안쪽 동작 블록, 실제 StreamingAssets/RfidMappings.json 레벨 5 정의로 만든 정답 설계와 데이터 검사를 확인함.
    /// </summary>
    public class Level5RuleTests
    {
        private const string F = Constants.RfidIds.Level5.Function;
        private const string A = Constants.RfidIds.Level5.Action;

        [Test]
        public void 함수_카드는_함수_사용_블록_동작_카드는_명령_블록이다()
        {
            Assert.AreEqual(DesignStepShape.FunctionCall, IngredientLevel5State.DesignShapeOf(F));
            Assert.AreEqual(DesignStepShape.Command, IngredientLevel5State.DesignShapeOf(A));
        }

        [Test]
        public void 설계창_크기는_함수_카드를_마지막에_놓아_시작하기_줄이_가장_길어지는_경우로_센다()
        {
            GameObject go = new GameObject("TestLevel5Plan");
            try
            {
                IngredientSelectionController controller = go.AddComponent<IngredientSelectionController>();
                List<DesignStepShape> shapes = new List<DesignStepShape>();
                new IngredientLevel5State().FillPlannedDesignShapes(controller, shapes);

                Assert.AreEqual(controller.TotalSteps, shapes.Count, "단계 수만큼 세야 함");
                Assert.AreEqual(DesignStepShape.FunctionCall, shapes[shapes.Count - 1], "함수 사용을 마지막 단계로 세야 함(앞 블록이 모두 시작하기 줄에 쌓여 가장 길어짐)");
                Assert.AreEqual(1, shapes.Count(s => s == DesignStepShape.FunctionCall), "함수 사용은 하나만 세야 함");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 함수_동작을_정해진_장수만큼_모두_놓으면_순서와_관계없이_완성이다()
        {
            Assert.IsTrue(IngredientLevel5State.IsComplete(new[] { F, A, A, A, A }, 5), "함수1·동작4이면 완성");
            Assert.IsTrue(IngredientLevel5State.IsComplete(new[] { A, A, F, A, A }, 5), "순서가 달라도 완성");
            Assert.IsFalse(IngredientLevel5State.IsComplete(new[] { A, A, A, A, null }, 4), "함수가 빠지면 미완성");
            Assert.IsFalse(IngredientLevel5State.IsComplete(new[] { F, A, A, A, A }, 4), "확정된 앞쪽 4장만 세면 미완성");
            Assert.IsFalse(IngredientLevel5State.IsComplete(new[] { F, F, A, A, A }, 5), "함수가 2장이고 동작이 모자라면 미완성");
        }

        [Test]
        public void 그림은_함수_카드_뒤에_놓아_함수_정의_블록_안쪽에_들어간_동작_블록만_보여_준다()
        {
            RfidMatter function = new RfidMatter { id = "BuildSpaceCity" };
            RfidMatter station = new RfidMatter { id = Constants.RfidIds.Level5.SpaceStationCode };
            RfidMatter robot = new RfidMatter { id = Constants.RfidIds.Level5.ExplorerRobotCode };
            RfidMatter tower = new RfidMatter { id = Constants.RfidIds.Level5.CommunicationCode };
            string[] ingredients = { A, F, A, A, null };
            RfidMatter[] matters = { station, function, robot, tower, null };
            List<string> result = new List<string>();

            IngredientLevel5State.CollectFunctionBodyActions(ingredients, matters, 4, result);
            CollectionAssert.AreEqual(new[] { robot.id, tower.id }, result, "함수 카드보다 먼저 놓은 블록(시작하기 줄)은 빼고 뒤에 놓은 블록만 놓은 순서대로");

            result.Clear();
            IngredientLevel5State.CollectFunctionBodyActions(ingredients, matters, 3, result);
            CollectionAssert.AreEqual(new[] { robot.id }, result, "확정된 앞쪽 단계(카드가 떨어지지 않은 단계)만 셈");

            result.Clear();
            IngredientLevel5State.CollectFunctionBodyActions(ingredients, matters, 1, result);
            CollectionAssert.IsEmpty(result, "함수 카드를 아직 놓지 않았으면 보여 줄 그림이 없음");

            result.Clear();
            IngredientLevel5State.CollectFunctionBodyActions(null, null, 5, result);
            CollectionAssert.IsEmpty(result, "확정 목록이 없으면 보여 줄 그림이 없음");
        }

        [Test]
        public void 현재_상황_화면은_받은_동작_블록의_그림만_켜고_나머지는_끈다()
        {
            GameObject go = new GameObject("TestLevel5City");
            try
            {
                Image dome = NewHiddenImage(go, "Image_Dome");
                Image rover = NewHiddenImage(go, "Image_Rover");
                Image tower = NewHiddenImage(go, "Image_Tower");
                Image corridor = NewHiddenImage(go, "Image_Corridor");
                Level5CityView view = go.AddComponent<Level5CityView>();
                view.SetImagesForTest(dome, rover, tower, corridor);

                view.ShowOnly(new List<string> { Constants.RfidIds.Level5.ExplorerRobotCode, Constants.RfidIds.Level5.ConnectionPassageCode }, 0f);
                Assert.IsTrue(rover.gameObject.activeSelf, "탐사 로봇 코드면 로버 그림이 보여야 함");
                Assert.IsTrue(corridor.gameObject.activeSelf, "연결 통로 코드면 연결 통로 그림이 보여야 함");
                Assert.IsFalse(dome.gameObject.activeSelf, "받지 않은 블록의 그림(돔 기지)은 꺼져 있어야 함");
                Assert.IsFalse(tower.gameObject.activeSelf, "받지 않은 블록의 그림(통신탑)은 꺼져 있어야 함");
                Assert.AreEqual(0f, rover.color.a, 0.01f, "새로 보이는 그림은 투명에서 페이드인을 시작해야 함");

                view.ShowOnly(new List<string> { Constants.RfidIds.Level5.SpaceStationCode, Constants.RfidIds.Level5.CommunicationCode }, 0f);
                Assert.IsTrue(dome.gameObject.activeSelf, "우주 정거장 코드면 돔 기지 그림이 보여야 함");
                Assert.IsTrue(tower.gameObject.activeSelf, "통신 시스템 코드면 통신탑 그림이 보여야 함");
                Assert.IsFalse(rover.gameObject.activeSelf, "빠진 블록의 그림은 바로 꺼져야 함");

                view.ShowOnly(new List<string>(), 0f);
                Assert.IsFalse(dome.gameObject.activeSelf || rover.gameObject.activeSelf || tower.gameObject.activeSelf || corridor.gameObject.activeSelf, "받은 블록이 없으면 그림이 모두 꺼져야 함");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [UnityTest]
        public IEnumerator 그림은_블록이_다_붙는_시간만큼_기다린_뒤에_페이드인한다()
        {
            GameObject go = new GameObject("TestLevel5CityDelay");
            try
            {
                Image dome = NewHiddenImage(go, "Image_Dome");
                Image rover = NewHiddenImage(go, "Image_Rover");
                Image tower = NewHiddenImage(go, "Image_Tower");
                Image corridor = NewHiddenImage(go, "Image_Corridor");
                Level5CityView view = go.AddComponent<Level5CityView>();
                view.SetImagesForTest(dome, rover, tower, corridor);

                view.ShowOnly(new List<string> { Constants.RfidIds.Level5.ExplorerRobotCode }, 0.5f);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.AreEqual(0f, rover.color.a, 0.01f, "블록이 다 붙기 전(기다리는 0.5초 안)에는 보이지 않아야 함");

                yield return new WaitForSecondsRealtime(0.9f); // 기다림 0.5초 + 페이드인 0.5초가 지나도록
                Assert.AreEqual(1f, rover.color.a, 0.01f, "기다린 뒤 페이드인이 끝나면 다 보여야 함");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static Image NewHiddenImage(GameObject parent, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(parent.transform, false);
            child.SetActive(false);
            return child.GetComponent<Image>();
        }

        [Test]
        public void 장수는_확정된_앞쪽_단계만_센다()
        {
            string[] confirmed = { A, F, A, null, A };
            Assert.AreEqual(2, IngredientLevel5State.CountConfirmed(confirmed, 3, A), "앞쪽 3단계의 동작은 2장");
            Assert.AreEqual(3, IngredientLevel5State.CountConfirmed(confirmed, 5, A), "5단계 전체의 동작은 3장");
            Assert.AreEqual(0, IngredientLevel5State.CountConfirmed(null, 5, A), "확정 목록이 없으면 0장");
        }

        [Test]
        public void 실제_JSON으로_만든_정답은_함수_동작_전부이고_완성이며_데이터_오류가_없다()
        {
            RfidSettings settings = JsonLoader.Load<RfidSettings>(Constants.Files.RfidMappings);
            Assert.IsNotNull(settings, "RfidMappings.json을 읽지 못함");
            List<string> errors = RfidMappingValidator.Validate(settings);
            CollectionAssert.IsEmpty(errors.Where(e => e.Contains("레벨 5")).ToList(), "레벨 5 정의에 데이터 오류가 없어야 함");

            GameObject go = new GameObject("TestLevel5");
            try
            {
                IngredientSelectionController controller = go.AddComponent<IngredientSelectionController>();
                controller.ApplyLevelMapping(settings.FindLevelMapping(5));
                List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = new IngredientLevel5State().BuildSolution(controller);

                Assert.AreEqual(Constants.Level5Cards.Total, solution.Count, "정답은 카드 수만큼이어야 함");
                string[] ingredientIds = solution.Select(s => s.ingredient.ingredientId).ToArray();
                Assert.IsTrue(IngredientLevel5State.IsComplete(ingredientIds, ingredientIds.Length), "정답을 그대로 놓으면 완성이어야 함");
                Assert.AreEqual(F, solution[0].ingredient.ingredientId, "정답은 함수 사용 블록으로 시작해야 함");
                Assert.AreEqual(Constants.Level5Cards.Action, solution.Where(s => s.ingredient.ingredientId == A).Select(s => s.matter.id).Distinct().Count(), "동작 블록은 서로 다른 블록이어야 함");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
