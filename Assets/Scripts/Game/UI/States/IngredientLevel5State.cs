using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using ZLogger;

namespace DGAIZone.Game.UI.States
{
    /// <summary>
    /// 레벨 5(우주 도시, 함수 블록) 워크플로우를 담당하는 상태 클래스. 함수·동작 카드를 Constants.Level5Cards 장수만큼 어느 순서로든 놓을 수
    /// 있고(다 쓴 분류의 카드는 받지 않음), 5장을 모두 놓으면 코딩 완료를 누를 수 있으며, 동작 블록이 모두 함수 정의 블록 안쪽(함수 카드 뒤)에
    /// 들어가 있으면 성공.
    /// 함수 카드는 설계창에 함수 사용 블록(시작하기 아래 줄)과 함수 정의 블록(오른쪽)으로 쌓이고, 함수 카드 뒤에 놓은 동작 블록은 함수 정의
    /// 블록 안쪽에, 앞에 놓은 블록은 시작하기 아래 줄에 놓은 순서대로 쌓임. 함수 정의 블록 안쪽에 보이는 동작 블록마다 현재 상황 화면에
    /// 맞는 그림(Level5CityView)이 나타남.
    /// </summary>
    public class IngredientLevel5State : IIngredientSelectionLevelState
    {
        private readonly List<string> _cityMatterIds = new List<string>(Constants.Level5Cards.Action); // 그림을 보여 줄 동작 블록 id(갱신마다 재사용)

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

        /// <summary> 이미 정해진 장수만큼 놓은 분류(함수·동작)의 카드는 받지 않고 경고 연출을 표시함. </summary>
        public bool ValidateTagCategory(IngredientSelectionController controller, RfidTagEvent evt)
        {
            RfidStepDefinition ingredient = controller.FindCategoryIngredient(evt.Category);
            if (ingredient == null) return true; // 재료 정의가 없으면 이어지는 ResolveStepCard에서 컨트롤러가 경고를 남기고 무시함

            int limit = CardLimitOf(ingredient.ingredientId);
            if (CountConfirmed(controller.ConfirmedIngredients, controller.CurrentStepIndex, ingredient.ingredientId) < limit) return true;

            controller.LogCardPlaced(evt, ZString.Format("{0} 카드는 {1}장까지라 경고를 띄움", evt.Category, limit));
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
                RfidStepDefinition ingredient = controller.FindCategoryIngredient(category);
                if (ingredient == null ||
                    CountConfirmed(controller.ConfirmedIngredients, controller.CurrentStepIndex, ingredient.ingredientId) < CardLimitOf(ingredient.ingredientId))
                {
                    allowed.Add(category);
                }
            }

            return allowed.ToArray();
        }

        /// <summary> 찍은 카드 분류(함수/동작)에 맞는 재료를 RfidMappings.json의 categoryIngredients에서 찾음. </summary>
        public RfidStepDefinition ResolveStepCard(IngredientSelectionController controller, RfidStepDefinition step, string category)
        {
            return controller.FindCategoryIngredient(category);
        }

        /// <summary> 이미 놓은 동작 블록은 다시 고를 수 없도록 목록에서 제외함. </summary>
        public RfidMatter[] FilterMatters(IngredientSelectionController controller, string ingredientId, RfidMatter[] matters)
        {
            return controller.ExcludeConfirmedMatters(ingredientId, matters);
        }

        /// <summary> 확정한 블록까지 세어, 함수 정의 블록 안쪽에 들어간 동작 블록의 그림을 그 블록이 다 붙은 뒤에 보여 줌. </summary>
        public void OnStepConfirmed(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter chosenMatter)
        {
            RefreshCity(controller, stepIndex + 1, controller.DesignAttachDuration); // 컨트롤러는 이 호출 뒤에 단계 인덱스를 올림
        }

        /// <summary> 단계 취소 시 추가 작업 없음(이어지는 OnMissingCardsRefreshed에서 그림을 갱신함). </summary>
        public void OnStepRolledBack(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter matter)
        {
        }

        /// <summary>
        /// 취소·되돌리기나 카드가 떨어지고 돌아와 함수 정의 블록 안쪽에 보이는 동작 블록이 바뀌었을 수 있으므로 그림을 다시 맞춤.
        /// 새로 보이는 그림은 카드가 돌아와 블록이 다시 붙는 경우뿐이라 다시 붙는 연출이 끝난 뒤에 보여 줌.
        /// </summary>
        public void OnMissingCardsRefreshed(IngredientSelectionController controller)
        {
            RefreshCity(controller, controller.CurrentStepIndex, controller.DesignRestoreDuration);
        }

        /// <summary>
        /// 확정된 앞쪽 confirmedCount개 단계 중 카드가 떨어지지 않은 단계에서, 함수 카드 뒤(함수 정의 블록 안쪽)에 놓인 동작 블록의 그림만 보여 줌
        /// (새로 보일 그림은 appearDelay초 뒤에 나타남). 카드가 떨어진 단계부터 뒤 블록은 설계창에서 임시로 떨어져 있으므로 그 그림도 숨김.
        /// </summary>
        private void RefreshCity(IngredientSelectionController controller, int confirmedCount, float appearDelay)
        {
            Level5CityView city = controller.Level5City;
            if (!city)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] level5City가 null이라 레벨 5 현재 상황 화면의 그림을 바꿀 수 없음.");
                return;
            }

            _cityMatterIds.Clear();
            CollectFunctionBodyActions(controller.ConfirmedIngredients, controller.ConfirmedMatters, Math.Min(confirmedCount, controller.FirstMissingCardStep), _cityMatterIds);
            city.ShowOnly(_cityMatterIds, appearDelay);
        }

        /// <summary>
        /// 확정된 앞쪽 count개 단계 중 함수 카드 뒤(함수 정의 블록 안쪽)에 놓인 동작 블록 id를 놓은 순서대로 result에 담음.
        /// 판정(CountFunctionBodyActions)과 같은 기준이어야 하므로 한쪽을 바꾸면 다른 쪽도 함께 바꿈.
        /// </summary>
        internal static void CollectFunctionBodyActions(string[] confirmedIngredients, RfidMatter[] confirmedMatters, int count, List<string> result)
        {
            if (confirmedIngredients == null || confirmedMatters == null) return;

            bool inFunction = false;
            for (int i = 0; i < count && i < confirmedIngredients.Length && i < confirmedMatters.Length; i++)
            {
                if (string.Equals(confirmedIngredients[i], Constants.RfidIds.Level5.Function, StringComparison.Ordinal)) inFunction = true;
                else if (inFunction && confirmedMatters[i] != null && string.Equals(confirmedIngredients[i], Constants.RfidIds.Level5.Action, StringComparison.Ordinal))
                {
                    result.Add(confirmedMatters[i].id);
                }
            }
        }

        /// <summary> 함수·동작 모두 고른 블록 이름만 값 블록 없이 씀. </summary>
        public (string command, string value) GetDesignBlockTexts(IngredientSelectionController controller, string ingredientName, string matterLabel)
        {
            return (matterLabel, null);
        }

        /// <summary> 함수 카드는 함수 사용 블록(오른쪽에 함수 정의 블록도 놓이고 뒤 블록은 그 안쪽에 쌓임), 동작 카드는 명령 블록으로 쌓음. </summary>
        public DesignStepShape GetDesignStepShape(IngredientSelectionController controller, string ingredientId, string previousIngredientId)
        {
            bool known = string.Equals(ingredientId, Constants.RfidIds.Level5.Function, StringComparison.Ordinal)
                || string.Equals(ingredientId, Constants.RfidIds.Level5.Action, StringComparison.Ordinal);
            if (!known && controller.Logger != null)
            {
                controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 5에서 알 수 없는 재료 id '{ingredientId}'라 설계창에 명령 블록으로 쌓음.");
            }

            return DesignShapeOf(ingredientId);
        }

        /// <summary> 함수 사용이면 FunctionCall, 그 밖에는 명령 블록. </summary>
        internal static DesignStepShape DesignShapeOf(string ingredientId)
        {
            if (string.Equals(ingredientId, Constants.RfidIds.Level5.Function, StringComparison.Ordinal)) return DesignStepShape.FunctionCall;
            return DesignStepShape.Command;
        }

        /// <summary> 함수 카드가 함수 사용 블록이 되므로 오른쪽에 함수 정의 블록 자리를 남김. </summary>
        public bool UsesFunctionDefinition => true;

        /// <summary> 추진력 게이지를 쓰지 않음(레벨 1 전용). </summary>
        public bool UsesThrustGauge => false;

        /// <summary> 함수·동작 블록 모두 값 블록을 쓰지 않음. </summary>
        public bool UsesValueBlocks => false;

        /// <summary> 블록 5개(함수 1·동작 4)를 모두 써야 하므로 모든 단계가 확정됐을 때만 코딩완료 버튼을 활성화함. </summary>
        public bool IsCodingCompleteInteractable(IngredientSelectionController controller, int designItemCount, int totalSteps)
        {
            return designItemCount >= totalSteps;
        }

        /// <summary>
        /// 동작 블록이 정해진 장수(Constants.Level5Cards.Action)만큼 모두 함수 정의 블록 안쪽(함수 카드 뒤)에 들어가 있으면 성공.
        /// 함수 카드가 없거나, 함수 카드보다 먼저 놓아 시작하기 줄에 있는 동작 블록이 있거나, 동작 블록을 다 놓지 않았으면 실패.
        /// </summary>
        public bool EvaluateMission(IngredientSelectionController controller)
        {
            int actionsInBody = CountFunctionBodyActions(controller.ConfirmedIngredients, controller.CurrentStepIndex);
            bool success = actionsInBody == Constants.Level5Cards.Action;
            if (controller.Logger != null)
            {
                int functionCards = CountConfirmed(controller.ConfirmedIngredients, controller.CurrentStepIndex, Constants.RfidIds.Level5.Function);
                controller.Logger.ZLogInformation($"[IngredientSelectionController] 레벨 5 판정: 함수 정의 블록 안 동작 블록 {actionsInBody}/{Constants.Level5Cards.Action}개(함수 카드 {functionCards}장, 놓은 카드 {controller.CurrentStepIndex}/{Constants.Level5Cards.Total}장) -> {(success ? "성공" : "실패")}");
            }

            return success;
        }

        /// <summary> 실패 원인별 결과 영상이 없어 기본 실패 영상을 씀. </summary>
        public string GetFailVideoSuffix(IngredientSelectionController controller)
        {
            return null;
        }

        /// <summary>
        /// 확정된 앞쪽 count개 단계 중 첫 함수 카드 뒤(함수 정의 블록 안쪽)에 놓인 동작 카드 수. 함수 카드가 없으면 0.
        /// 설계창 배치(DesignPanel)·현재 상황 그림(CollectFunctionBodyActions)과 같은 기준이어야 하므로 한쪽을 바꾸면 다른 쪽도 함께 바꿈.
        /// </summary>
        internal static int CountFunctionBodyActions(string[] confirmedIngredients, int count)
        {
            if (confirmedIngredients == null) return 0;

            bool inFunction = false;
            int found = 0;
            for (int i = 0; i < count && i < confirmedIngredients.Length; i++)
            {
                if (string.Equals(confirmedIngredients[i], Constants.RfidIds.Level5.Function, StringComparison.Ordinal)) inFunction = true;
                else if (inFunction && string.Equals(confirmedIngredients[i], Constants.RfidIds.Level5.Action, StringComparison.Ordinal)) found++;
            }

            return found;
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
                default: return 0;
            }
        }

        /// <summary> 정답: 함수 사용 → 동작 블록 전부(모든 동작 블록이 함수 정의 블록 안쪽). 블록 정의가 모자라면 경고를 남기고 빈 목록을 반환함. </summary>
        public List<(RfidStepDefinition ingredient, RfidMatter matter)> BuildSolution(IngredientSelectionController controller)
        {
            List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = new List<(RfidStepDefinition ingredient, RfidMatter matter)>();
            RfidStepDefinition function = controller.FindCategoryIngredient(Constants.RfidCategories.Func);
            RfidStepDefinition action = controller.FindCategoryIngredient(Constants.RfidCategories.Action);
            RfidLevelMapping mapping = controller.LevelMapping;
            RfidMatter[] functionMatters = function != null && mapping != null ? mapping.FindMatters(function.matterSetId) : null;
            RfidMatter[] actionMatters = action != null && mapping != null ? mapping.FindMatters(action.matterSetId) : null;

            if (functionMatters == null || functionMatters.Length == 0 || actionMatters == null || actionMatters.Length < Constants.Level5Cards.Action)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 5 함수·동작 블록 정의가 모자라 정답 설계를 만들 수 없음.");
                return solution;
            }

            solution.Add((function, functionMatters[0]));
            for (int i = 0; i < Constants.Level5Cards.Action; i++) solution.Add((action, actionMatters[i]));
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
