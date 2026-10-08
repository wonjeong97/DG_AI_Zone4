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
    /// 레벨 4 (블록 코딩 로봇 제어 및 경로 시뮬레이션) 워크플로우를 담당하는 상태 클래스.
    /// </summary>
    public class IngredientLevel4State : IIngredientSelectionLevelState
    {
        private static readonly string[] Level4RepeatFollowUpCategories = { Constants.RfidCategories.Action };

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

        /// <summary> 직전 단계가 '반복하기'인 경우 '동작' 카드만 허용하며 위반 시 경고 연출을 표시함. </summary>
        public bool ValidateTagCategory(IngredientSelectionController controller, RfidTagEvent evt)
        {
            if (IsRepeatFollowUpRequired(controller) && !string.Equals(evt.Category, Constants.RfidCategories.Action, StringComparison.Ordinal))
            {
                controller.LogCardPlaced(evt, $"이전 단계가 '반복하기'라 {controller.CurrentStepIndex + 1}번째 단계는 동작 카드만 쓸 수 있어 경고를 띄움");
                controller.ShowInvalidCardWarning();
                return false;
            }

            return true;
        }

        /// <summary> 직전 단계가 '반복하기'이면 '동작'만, 아니면 단계 정의 카테고리를 안내함. </summary>
        public string[] GetAllowedCategories(IngredientSelectionController controller, RfidStepDefinition step)
        {
            return IsRepeatFollowUpRequired(controller) ? Level4RepeatFollowUpCategories : step.categories;
        }

        /// <summary> 바로 이전 단계에서 확정한 재료가 "반복하기"(제어, 횟수 카드)였다면, 이번 단계는 반드시 "이동하기"(동작)여야 함. </summary>
        private static bool IsRepeatFollowUpRequired(IngredientSelectionController controller)
        {
            int previousIndex = controller.CurrentStepIndex - 1;
            string[] confirmed = controller.ConfirmedIngredients;
            if (previousIndex < 0 || confirmed == null || previousIndex >= confirmed.Length) return false;
            return string.Equals(confirmed[previousIndex], Constants.RfidIds.Level4.Repeat, StringComparison.Ordinal);
        }

        /// <summary> 찍은 카드 분류(동작/제어)로 고를 재료 정의를 categoryIngredients에서 찾음. 없으면 null. </summary>
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

        /// <summary> 스캔된 카드의 카테고리(동작/제어)에 맞는 재료(이동하기/반복하기)를 RfidMappings.json의 categoryIngredients에서 찾음. </summary>
        public RfidStepDefinition ResolveStepCard(IngredientSelectionController controller, RfidStepDefinition step, string category)
        {
            return FindCategoryIngredient(controller, category);
        }

        /// <summary> 레벨 4는 동일한 동작을 여러 번 사용할 수 있으므로 중복 제외를 적용하지 않음. </summary>
        public RfidMatter[] FilterMatters(IngredientSelectionController controller, string ingredientId, RfidMatter[] matters)
        {
            return matters;
        }

        /// <summary> 단계 확정 시 레벨 4 추가 작업 없음. </summary>
        public void OnStepConfirmed(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter chosenMatter)
        {
        }

        /// <summary> 단계 취소 시 레벨 4 추가 작업 없음. </summary>
        public void OnStepRolledBack(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter matter)
        {
        }

        /// <summary> 카드 떨어짐 갱신 시 레벨 4 추가 작업 없음. </summary>
        public void OnMissingCardsRefreshed(IngredientSelectionController controller)
        {
        }

        /// <summary> 재료 이름은 명령 블록, 고른 블록 이름은 값 블록에 씀. 재료 이름이 없는 단계는 값 블록 없이 블록 이름만 씀. </summary>
        public (string command, string value) GetDesignBlockTexts(IngredientSelectionController controller, string ingredientName, string matterLabel)
        {
            return string.IsNullOrEmpty(ingredientName) ? (matterLabel, null) : (ingredientName, matterLabel);
        }

        /// <summary> 반복하기는 ㄷ자 블록(횟수는 값 블록), 반복하기 바로 뒤(previousIngredientId가 반복하기) 이동하기는 그 안쪽, 그 밖의 이동하기는 명령 블록으로 쌓음. </summary>
        public DesignStepShape GetDesignStepShape(IngredientSelectionController controller, string ingredientId, string previousIngredientId)
        {
            bool known = string.Equals(ingredientId, Constants.RfidIds.Level4.Move, StringComparison.Ordinal)
                || string.Equals(ingredientId, Constants.RfidIds.Level4.Repeat, StringComparison.Ordinal);
            if (!known && controller.Logger != null)
            {
                controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 4에서 알 수 없는 재료 id '{ingredientId}'라 설계창에 명령 블록으로 쌓음.");
            }

            return DesignShapeOf(ingredientId, string.Equals(previousIngredientId, Constants.RfidIds.Level4.Repeat, StringComparison.Ordinal));
        }

        /// <summary> 반복하기면 ㄷ자 블록, 직전 단계가 반복하기(afterRepeat)면 그 안쪽, 그 밖에는 명령 블록. </summary>
        internal static DesignStepShape DesignShapeOf(string ingredientId, bool afterRepeat)
        {
            if (string.Equals(ingredientId, Constants.RfidIds.Level4.Repeat, StringComparison.Ordinal)) return DesignStepShape.FlowControl;
            return afterRepeat ? DesignStepShape.InsideFlowControl : DesignStepShape.Command;
        }

        /// <summary> 단계 정의(카드 분류)로 설계창이 가장 길어지는 모양을 셈. </summary>
        public void FillPlannedDesignShapes(IngredientSelectionController controller, List<DesignStepShape> shapes)
        {
            FillPlannedShapes(controller.StepDefinitions, controller.TotalSteps, shapes);
        }

        /// <summary>
        /// 제어 카드를 받는 단계마다 반복하기(ㄷ자 블록)를, 그 바로 뒤 단계에는 반복할 이동하기(안쪽)를 놓고, 나머지는 이동하기(명령 블록)로 채움.
        /// ㄷ자 블록은 안쪽까지 몸통 303px로 명령 블록 둘(202px)보다 길어, 반복하기를 가장 많이 쓴 경우가 가장 김. 정의가 없는 단계는 명령 블록으로 셈.
        /// </summary>
        internal static void FillPlannedShapes(RfidStepDefinition[] steps, int totalSteps, List<DesignStepShape> shapes)
        {
            for (int i = 0; i < totalSteps; i++)
            {
                RfidStepDefinition step = steps != null && i < steps.Length ? steps[i] : null;
                if (step == null || !step.AllowsCategory(Constants.RfidCategories.Control))
                {
                    shapes.Add(DesignStepShape.Command);
                    continue;
                }

                shapes.Add(DesignStepShape.FlowControl);
                if (i + 1 < totalSteps) // 반복하기 뒤에는 이동하기만 올 수 있음(마지막 단계의 반복하기는 안쪽이 빈 채로 남음)
                {
                    shapes.Add(DesignStepShape.InsideFlowControl);
                    i++;
                }
            }
        }

        /// <summary> 재료 이름이 있는 단계는 값 블록을 씀. </summary>
        public bool UsesValueBlocks => true;

        /// <summary> 레벨 4는 5단계를 다 채우지 않아도 되므로 최소 1개만 확정되면 코딩완료 버튼을 활성화함. </summary>
        public bool IsCodingCompleteInteractable(IngredientSelectionController controller, int designItemCount, int totalSteps)
        {
            return designItemCount > 0;
        }

        /// <summary> 보드 컨트롤러에 확정된 명령을 전달하여 목적지 도달 및 자원 수집 성공 여부를 판정함. </summary>
        public bool EvaluateMission(IngredientSelectionController controller)
        {
            if (!controller.Level4Board)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] level4Board가 null이라 레벨 4 미션을 판정할 수 없음. 실패로 처리함.");
                return false;
            }

            return controller.Level4Board.EvaluateOutcome(controller.GetConfirmedCommands());
        }

        /// <summary> 실패 원인별 결과 영상이 없어 기본 실패 영상을 씀. </summary>
        public string GetFailVideoSuffix(IngredientSelectionController controller)
        {
            return null;
        }

        /// <summary>
        /// 이번 판 보드 배치를 가장 적은 카드로 푸는 경로(Level4BoardController.FindSolution)를 블록으로 바꿔 반환함.
        /// 한 칸 이동은 '이동하기', 여러 칸 이동은 '반복하기(칸 수) + 이동하기'가 됨.
        /// </summary>
        public List<(RfidStepDefinition ingredient, RfidMatter matter)> BuildSolution(IngredientSelectionController controller)
        {
            List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = new List<(RfidStepDefinition ingredient, RfidMatter matter)>();
            if (!controller.Level4Board || controller.LevelMapping == null)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] level4Board나 레벨 4 블록 정의가 없어 정답 설계를 만들 수 없음.");
                return solution;
            }

            List<(int moves, string directionId)> path = controller.Level4Board.FindSolution();
            if (path == null)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 이번 레벨 4 배치를 카드 {Constants.Level4Board.MaxCards}장 안에 푸는 경로가 없어 정답 설계를 만들 수 없음.");
                return solution;
            }

            RfidStepDefinition move = FindCategoryIngredient(controller, Constants.RfidCategories.Action);
            RfidStepDefinition repeat = FindCategoryIngredient(controller, Constants.RfidCategories.Control);
            RfidMatter[] moveMatters = move != null ? controller.LevelMapping.FindMatters(move.matterSetId) : null;
            RfidMatter[] repeatMatters = repeat != null ? controller.LevelMapping.FindMatters(repeat.matterSetId) : null;

            foreach ((int moves, string directionId) in path)
            {
                if (moves > 1)
                {
                    RfidMatter repeatMatter = repeatMatters != null ? Array.Find(repeatMatters, m => m.value == moves) : null;
                    if (repeatMatter == null)
                    {
                        if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 4 반복하기 블록에 {moves}회가 없어 정답 설계를 만들 수 없음.");
                        solution.Clear();
                        return solution;
                    }
                    solution.Add((repeat, repeatMatter));
                }

                RfidMatter moveMatter = moveMatters != null ? Array.Find(moveMatters, m => m.id == directionId) : null;
                if (moveMatter == null)
                {
                    if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 4 이동하기 블록에 '{directionId}'가 없어 정답 설계를 만들 수 없음.");
                    solution.Clear();
                    return solution;
                }
                solution.Add((move, moveMatter));
            }

            return solution;
        }

        /// <summary> 레벨 4는 추진력 계산식을 사용하지 않음. </summary>
        public int CalculatePreviewThrust(IngredientSelectionController controller)
        {
            return 0;
        }

        /// <summary> 레벨 4는 추진력 계산식을 사용하지 않음. </summary>
        public int CalculateConfirmedThrust(IngredientSelectionController controller)
        {
            return 0;
        }

        /// <summary> 결과 씬 전환 전 보드 컨트롤러의 로봇 이동 시뮬레이션을 재생함. </summary>
        public async UniTask PlayCompletionSimulationAsync(IngredientSelectionController controller, CancellationToken token)
        {
            if (controller.Level4Board)
            {
                await controller.Level4Board.PlaySimulationAsync();
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
            }
            else if (controller.Logger != null)
            {
                controller.Logger.ZLogWarning($"[IngredientSelectionController] level4Board가 null이라 이동 시뮬레이션 없이 바로 결과 씬으로 전환함.");
            }
        }
    }
}
