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
    /// 레벨 5 규칙 검증 테스트. 카드 분류별 설계창 블록 모양, 분류별 장수 세기와 '동작 블록이 모두 함수 정의 블록 안에 있으면 성공' 판정,
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
        public void 함수_카드_뒤에_놓인_동작_카드만_함수_정의_블록_안으로_센다()
        {
            Assert.AreEqual(4, IngredientLevel5State.CountFunctionBodyActions(new[] { F, A, A, A, A }, 5), "함수 카드를 먼저 놓으면 동작 4장이 모두 안쪽");
            Assert.AreEqual(2, IngredientLevel5State.CountFunctionBodyActions(new[] { A, A, F, A, A }, 5), "함수 카드보다 먼저 놓은 동작 2장은 시작하기 줄(바깥)");
            Assert.AreEqual(0, IngredientLevel5State.CountFunctionBodyActions(new[] { A, A, A, A, F }, 5), "함수 카드를 마지막에 놓으면 안쪽 동작 없음");
            Assert.AreEqual(0, IngredientLevel5State.CountFunctionBodyActions(new[] { A, A, A, A, null }, 4), "함수 카드가 없으면 0");
            Assert.AreEqual(3, IngredientLevel5State.CountFunctionBodyActions(new[] { F, A, A, A, A }, 4), "확정된 앞쪽 4장만 셈");
            Assert.AreEqual(0, IngredientLevel5State.CountFunctionBodyActions(null, 5), "확정 목록이 없으면 0");
        }

        [Test]
        public void 블록_5개를_모두_놓아야_코딩_완료를_누를_수_있다()
        {
            IngredientLevel5State state = new IngredientLevel5State();
            Assert.IsFalse(state.IsCodingCompleteInteractable(null, 1, 5), "1장만 놓으면 누를 수 없음");
            Assert.IsFalse(state.IsCodingCompleteInteractable(null, 4, 5), "4장만 놓으면 누를 수 없음");
            Assert.IsTrue(state.IsCodingCompleteInteractable(null, 5, 5), "5장을 모두 놓으면 누를 수 있음");
        }

        [Test]
        public void 동작_블록이_모두_함수_정의_블록_안에_있어야_성공이다()
        {
            GameObject go = new GameObject("TestLevel5Evaluate");
            try
            {
                IngredientSelectionController controller = go.AddComponent<IngredientSelectionController>();
                IngredientLevel5State state = new IngredientLevel5State();

                controller.SetConfirmedIngredientsForTest(new[] { F, A, A, A, A }, 5);
                Assert.IsTrue(state.EvaluateMission(controller), "함수 카드 뒤에 동작 4장이면 성공");

                controller.SetConfirmedIngredientsForTest(new[] { A, F, A, A, A }, 5);
                Assert.IsFalse(state.EvaluateMission(controller), "동작 블록 하나라도 함수 밖에 있으면 실패");

                controller.SetConfirmedIngredientsForTest(new[] { F, A, A, A, null }, 4);
                Assert.IsFalse(state.EvaluateMission(controller), "동작 블록을 다 놓지 않으면 실패");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
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
                Assert.AreEqual(Constants.Level5Cards.Action, IngredientLevel5State.CountFunctionBodyActions(ingredientIds, ingredientIds.Length), "정답을 그대로 놓으면 동작 블록이 모두 함수 정의 블록 안에 있어야 함");
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
