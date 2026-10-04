using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using ZLogger;

namespace DGAIZone.Game.UI.States
{
    /// <summary>
    /// 레벨 5(우주 도시, 함수 블록) 워크플로우를 담당하는 상태 클래스. 기획 검토 중이라 임시 규칙으로 동작함:
    /// 함수·동작·논리 카드를 Constants.Level5Cards 장수만큼 순서 없이 놓고(다 쓴 분류의 카드는 받지 않음), 모두 놓으면 성공.
    /// 함수 카드는 설계창에 함수 사용 블록(시작하기 아래 줄)과 함수 정의 블록(오른쪽)으로 쌓이고, 함수 카드 뒤에 놓은 동작·논리 블록은 함수 정의
    /// 블록 안쪽에, 앞에 놓은 블록은 시작하기 아래 줄에 놓은 순서대로 쌓임.
    /// </summary>
    public class IngredientLevel5State : IIngredientSelectionLevelState
    {
        /// <summary> 상태 진입 시 초기화. </summary>
        public void Enter(IngredientSelectionController context)
        {
        }

        /// <summary> 매 프레임 업데이트. </summary>
        public void Update(IngredientSelectionController context)
        {
        }

        /// <summary> 상태 종료 시 정리. </summary>
        public void Exit(IngredientSelectionController context)
        {
        }

        /// <summary> 이미 정해진 장수만큼 놓은 분류(함수·동작·논리)의 카드는 받지 않고 경고 연출을 표시함. </summary>
        public bool ValidateTagCategory(IngredientSelectionController controller, RfidTagEvent evt)
        {
            RfidStepDefinition ingredient = FindCategoryIngredient(controller, evt.Category);
            if (ingredient == null) return true; // 재료 정의가 없으면 이어지는 ResolveStepCard에서 컨트롤러가 경고를 남기고 무시함

            int limit = CardLimitOf(ingredient.ingredientId);
            if (CountConfirmed(controller.ConfirmedIngredients, controller.CurrentStepIndex, ingredient.ingredientId) < limit) return true;

            if (controller.Logger != null)
            {
                controller.Logger.ZLogInformation($"[IngredientSelectionController] 레벨 5 '{evt.Category}' 카드는 {limit}장까지라 더 받지 않아 {evt.ReaderId} 태그를 무시함.");
            }
            controller.ShowInvalidCardWarning();
            return false;
        }

        /// <summary> 단계가 받는 분류 중 아직 정해진 장수를 다 놓지 않은 분류만 안내함. </summary>
        public string[] GetAllowedCategories(IngredientSelectionController controller, RfidStepDefinition step)
        {
            if (step.categories == null) return Array.Empty<string>();

            List<string> allowed = new List<string>(step.categories.Length);
            foreach (string category in step.categories)
            {
                RfidStepDefinition ingredient = FindCategoryIngredient(controller, category);
                if (ingredient == null ||
                    CountConfirmed(controller.ConfirmedIngredients, controller.CurrentStepIndex, ingredient.ingredientId) < CardLimitOf(ingredient.ingredientId))
                {
                    allowed.Add(category);
                }
            }

            return allowed.ToArray();
        }

        /// <summary> 찍은 카드 분류(함수/동작/논리)에 맞는 재료를 RfidMappings.json의 categoryIngredients에서 찾음. </summary>
        public RfidStepDefinition ResolveStepCard(IngredientSelectionController controller, RfidStepDefinition step, string category)
        {
            return FindCategoryIngredient(controller, category);
        }

        /// <summary> 이미 놓은 동작 블록은 다시 고를 수 없도록 목록에서 제외함. </summary>
        public RfidMatter[] FilterMatters(IngredientSelectionController controller, string ingredientId, RfidMatter[] matters)
        {
            return controller.ExcludeConfirmedMatters(ingredientId, matters);
        }

        /// <summary> 단계 확정 시 레벨 5 추가 작업 없음. </summary>
        public void OnStepConfirmed(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter chosenMatter)
        {
        }

        /// <summary> 단계 취소 시 레벨 5 추가 작업 없음. </summary>
        public void OnStepRolledBack(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter matter)
        {
        }

        /// <summary> 함수·동작·논리 모두 고른 블록 이름만 값 블록 없이 씀. </summary>
        public (string command, string value) GetDesignBlockTexts(IngredientSelectionController controller, string ingredientName, string matterLabel)
        {
            return (matterLabel, null);
        }

        /// <summary> 함수 카드는 함수 사용 블록(오른쪽에 함수 정의 블록도 놓이고 뒤 블록은 그 안쪽에 쌓임), 논리 카드는 논리 블록, 동작 카드는 명령 블록으로 쌓음. </summary>
        public DesignStepShape GetDesignStepShape(IngredientSelectionController controller, string ingredientId, string previousIngredientId)
        {
            bool known = string.Equals(ingredientId, Constants.RfidIds.Level5.Function, StringComparison.Ordinal)
                || string.Equals(ingredientId, Constants.RfidIds.Level5.Action, StringComparison.Ordinal)
                || string.Equals(ingredientId, Constants.RfidIds.Level5.Logic, StringComparison.Ordinal);
            if (!known && controller.Logger != null)
            {
                controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 5에서 알 수 없는 재료 id '{ingredientId}'라 설계창에 명령 블록으로 쌓음.");
            }

            return DesignShapeOf(ingredientId);
        }

        /// <summary> 함수 사용이면 FunctionCall, 논리면 Logic, 그 밖에는 명령 블록. </summary>
        internal static DesignStepShape DesignShapeOf(string ingredientId)
        {
            if (string.Equals(ingredientId, Constants.RfidIds.Level5.Function, StringComparison.Ordinal)) return DesignStepShape.FunctionCall;
            if (string.Equals(ingredientId, Constants.RfidIds.Level5.Logic, StringComparison.Ordinal)) return DesignStepShape.Logic;
            return DesignStepShape.Command;
        }

        /// <summary>
        /// 줄의 블록은 모두 높이가 같고 함수 사용 뒤 블록은 오른쪽 함수 정의 블록 안쪽에 쌓이므로, 설계창이 가장 길어지는 경우(함수 카드를 마지막에
        /// 놓아 모든 블록이 시작하기 아래 줄에 쌓임)로 셈. 함수 사용 블록이 있어 오른쪽 함수 정의 블록 자리도 남음.
        /// </summary>
        public void FillPlannedDesignShapes(IngredientSelectionController controller, List<DesignStepShape> shapes)
        {
            for (int i = 0; i < controller.TotalSteps; i++) shapes.Add(i == controller.TotalSteps - 1 ? DesignStepShape.FunctionCall : DesignStepShape.Command);
        }

        /// <summary> 함수·동작·논리 블록 모두 값 블록을 쓰지 않음. </summary>
        public bool UsesValueBlocks => false;

        /// <summary> 다 놓지 않아도 판정(실패)을 볼 수 있도록 1장 이상 놓이면 코딩완료 버튼을 활성화함. </summary>
        public bool IsCodingCompleteInteractable(IngredientSelectionController controller, int designItemCount, int totalSteps)
        {
            return designItemCount > 0;
        }

        /// <summary> 임시 판정: 함수·동작·논리 카드를 정해진 장수만큼 모두 놓았으면 성공(논리 종류는 보지 않음, 기획 확정 뒤 수정). </summary>
        public bool EvaluateMission(IngredientSelectionController controller)
        {
            bool success = IsComplete(controller.ConfirmedIngredients, controller.CurrentStepIndex);
            if (controller.Logger != null)
            {
                controller.Logger.ZLogInformation($"[IngredientSelectionController] 레벨 5 판정(임시: 카드를 모두 놓으면 성공): {controller.CurrentStepIndex}/{Constants.Level5Cards.Total}장 -> {(success ? "성공" : "실패")}");
            }

            return success;
        }

        /// <summary> 실패 원인별 결과 영상이 없어 기본 실패 영상을 씀. </summary>
        public string GetFailVideoSuffix(IngredientSelectionController controller)
        {
            return null;
        }

        /// <summary> 확정된 앞쪽 count개 단계 중 함수·동작·논리 카드가 각각 정해진 장수만큼 있는지 여부. </summary>
        internal static bool IsComplete(string[] confirmedIngredients, int count)
        {
            return CountConfirmed(confirmedIngredients, count, Constants.RfidIds.Level5.Function) == Constants.Level5Cards.Function
                && CountConfirmed(confirmedIngredients, count, Constants.RfidIds.Level5.Action) == Constants.Level5Cards.Action
                && CountConfirmed(confirmedIngredients, count, Constants.RfidIds.Level5.Logic) == Constants.Level5Cards.Logic;
        }

        /// <summary> 확정된 앞쪽 count개 단계 중 재료가 ingredientId인 단계 수. </summary>
        internal static int CountConfirmed(string[] confirmedIngredients, int count, string ingredientId)
        {
            if (confirmedIngredients == null) return 0;

            int found = 0;
            for (int i = 0; i < count && i < confirmedIngredients.Length; i++)
            {
                if (string.Equals(confirmedIngredients[i], ingredientId, StringComparison.Ordinal)) found++;
            }

            return found;
        }

        /// <summary> 재료별로 놓을 수 있는 카드 수. 레벨 5 재료가 아니면 0. </summary>
        private static int CardLimitOf(string ingredientId)
        {
            switch (ingredientId)
            {
                case Constants.RfidIds.Level5.Function: return Constants.Level5Cards.Function;
                case Constants.RfidIds.Level5.Action: return Constants.Level5Cards.Action;
                case Constants.RfidIds.Level5.Logic: return Constants.Level5Cards.Logic;
                default: return 0;
            }
        }

        /// <summary> 찍은 카드 분류로 고를 재료 정의를 categoryIngredients에서 찾음. 없으면 null. </summary>
        private static RfidStepDefinition FindCategoryIngredient(IngredientSelectionController controller, string category)
        {
            RfidStepDefinition[] ingredients = controller.CategoryIngredients;
            if (ingredients == null) return null;

            foreach (RfidStepDefinition ingredient in ingredients)
            {
                if (ingredient != null && ingredient.AllowsCategory(category)) return ingredient;
            }

            return null;
        }

        /// <summary> 임시 정답: 함수 사용 → 동작 블록 전부 → '그리고'. 블록 정의가 모자라면 경고를 남기고 빈 목록을 반환함. </summary>
        public List<(RfidStepDefinition ingredient, RfidMatter matter)> BuildSolution(IngredientSelectionController controller)
        {
            List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = new List<(RfidStepDefinition ingredient, RfidMatter matter)>();
            RfidStepDefinition function = FindCategoryIngredient(controller, Constants.RfidCategories.Func);
            RfidStepDefinition action = FindCategoryIngredient(controller, Constants.RfidCategories.Action);
            RfidStepDefinition logic = FindCategoryIngredient(controller, Constants.RfidCategories.Logic);
            RfidLevelMapping mapping = controller.LevelMapping;
            RfidMatter[] functionMatters = function != null && mapping != null ? mapping.FindMatters(function.matterSetId) : null;
            RfidMatter[] actionMatters = action != null && mapping != null ? mapping.FindMatters(action.matterSetId) : null;
            RfidMatter[] logicMatters = logic != null && mapping != null ? mapping.FindMatters(logic.matterSetId) : null;
            RfidMatter and = logicMatters != null ? Array.Find(logicMatters, m => m != null && m.id == Constants.RfidIds.Level5.And) : null;

            if (functionMatters == null || functionMatters.Length == 0 || actionMatters == null || actionMatters.Length < Constants.Level5Cards.Action || and == null)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 5 함수·동작·논리('그리고') 블록 정의가 모자라 정답 설계를 만들 수 없음.");
                return solution;
            }

            solution.Add((function, functionMatters[0]));
            for (int i = 0; i < Constants.Level5Cards.Action; i++) solution.Add((action, actionMatters[i]));
            solution.Add((logic, and));
            return solution;
        }

        /// <summary> 레벨 5는 추진력 계산식을 사용하지 않음. </summary>
        public int CalculatePreviewThrust(IngredientSelectionController controller)
        {
            return 0;
        }

        /// <summary> 레벨 5는 추진력 계산식을 사용하지 않음. </summary>
        public int CalculateConfirmedThrust(IngredientSelectionController controller)
        {
            return 0;
        }

        /// <summary> 레벨 5는 추가 시뮬레이션 연출이 없음. </summary>
        public UniTask PlayCompletionSimulationAsync(IngredientSelectionController controller, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
    }
}
