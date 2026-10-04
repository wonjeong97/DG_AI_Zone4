using System.Collections.Generic;
using System.Linq;
using DGAIZone.App;
using DGAIZone.Game.Data;
using DGAIZone.Game.UI;
using DGAIZone.Game.UI.States;
using HuliacDev.Utils;
using NUnit.Framework;
using UnityEngine;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 레벨 5(임시 규칙) 검증 테스트. 카드 분류별 설계창 블록 모양과 설계창 크기 계산용 모양, 분류별 장수 세기와 '모두 놓으면 성공' 판정,
    /// 실제 StreamingAssets/RfidMappings.json 레벨 5 정의로 만든 정답 설계와 데이터 검사를 확인함.
    /// </summary>
    public class Level5RuleTests
    {
        private const string F = Constants.RfidIds.Level5.Function;
        private const string A = Constants.RfidIds.Level5.Action;
        private const string L = Constants.RfidIds.Level5.Logic;

        [Test]
        public void 함수_카드는_함수_사용_블록_논리_카드는_논리_블록_동작_카드는_명령_블록이다()
        {
            Assert.AreEqual(DesignStepShape.FunctionCall, IngredientLevel5State.DesignShapeOf(F));
            Assert.AreEqual(DesignStepShape.Logic, IngredientLevel5State.DesignShapeOf(L));
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
        public void 함수_동작_논리를_정해진_장수만큼_모두_놓으면_순서와_관계없이_완성이다()
        {
            Assert.IsTrue(IngredientLevel5State.IsComplete(new[] { F, A, A, A, L }, 5), "함수1·동작3·논리1이면 완성");
            Assert.IsTrue(IngredientLevel5State.IsComplete(new[] { A, L, A, F, A }, 5), "순서가 달라도 완성");
            Assert.IsFalse(IngredientLevel5State.IsComplete(new[] { F, A, A, A, null }, 4), "논리가 빠지면 미완성");
            Assert.IsFalse(IngredientLevel5State.IsComplete(new[] { F, A, A, A, L }, 4), "확정된 앞쪽 4장만 세면 미완성");
            Assert.IsFalse(IngredientLevel5State.IsComplete(new[] { F, A, A, L, L }, 5), "논리가 2장이고 동작이 모자라면 미완성");
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
        public void 실제_JSON으로_만든_정답은_함수_동작_전부_그리고이고_완성이며_데이터_오류가_없다()
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
                Assert.AreEqual(Constants.RfidIds.Level5.And, solution[solution.Count - 1].matter.id, "정답의 논리 블록은 '그리고'여야 함");
                Assert.AreEqual(Constants.Level5Cards.Action, solution.Where(s => s.ingredient.ingredientId == A).Select(s => s.matter.id).Distinct().Count(), "동작 블록은 서로 다른 블록이어야 함");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
