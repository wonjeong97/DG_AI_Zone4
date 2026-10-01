using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using ZLogger;

namespace DGAIZone.Game.UI.States
{
    /// <summary>
    /// 레벨 1 (기본 추진력 계산 및 재료 선택) 워크플로우를 담당하는 상태 클래스.
    /// </summary>
    public class IngredientLevel1State : IIngredientSelectionLevelState
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

        /// <summary> 레벨 1은 모든 태그 카테고리를 기본 허용함. </summary>
        public bool ValidateTagCategory(IngredientSelectionController controller, RfidTagEvent evt)
        {
            return true;
        }

        /// <summary> 정의된 단계별 허용 카테고리를 그대로 반환함. </summary>
        public string[] GetAllowedCategories(IngredientSelectionController controller, RfidStepDefinition step)
        {
            return step.categories;
        }

        /// <summary> JSON에 정의된 단계별 고정 재료/물질을 그대로 사용함. </summary>
        public RfidStepDefinition ResolveStepCard(IngredientSelectionController controller, RfidStepDefinition step, string category)
        {
            return step;
        }

        /// <summary> 이미 확정된 물질을 제외한 목록을 반환함. </summary>
        public RfidMatter[] FilterMatters(IngredientSelectionController controller, string ingredientId, RfidMatter[] matters)
        {
            return controller.ExcludeConfirmedMatters(ingredientId, matters);
        }

        /// <summary> 선택된 물질의 value를 재료 역할(엔진/탑재/연료)에 맞춰 추진력 계산식에 반영함. </summary>
        public void OnStepConfirmed(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter chosenMatter)
        {
            controller.ApplyConfirmedValue(ingredientId, chosenMatter.value);
        }

        /// <summary> 되돌려진 재료의 확정값을 0으로 리셋함. </summary>
        public void OnStepRolledBack(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter matter)
        {
            controller.ApplyConfirmedValue(ingredientId, 0);
        }

        /// <summary> "· 재료 [물질]" 형식으로 디자인 항목 텍스트를 구성함. </summary>
        public string FormatDesignItemText(IngredientSelectionController controller, string ingredientName, string matterLabel)
        {
            return string.IsNullOrEmpty(ingredientName)
                ? $" · [<color=yellow>{controller.ApplyNumberSizeTag(matterLabel)}</color>]"
                : $" · {ingredientName} [<color=yellow>{controller.ApplyNumberSizeTag(matterLabel)}</color>]";
        }

        /// <summary> 모든 단계가 완료되었을 때만 코딩완료 버튼을 활성화함. </summary>
        public bool IsCodingCompleteInteractable(IngredientSelectionController controller, int designItemCount, int totalSteps)
        {
            return designItemCount >= totalSteps;
        }

        /// <summary> 총 추진력을 계산하고 미션보드의 유효 구간과 대조해 성공 여부를 판정함. </summary>
        public bool EvaluateMission(IngredientSelectionController controller)
        {
            int totalThrust = controller.CalculateTotalThrust();
            bool valid = controller.MissionBoard && controller.MissionBoard.IsThrustValid(totalThrust);
            if (controller.Logger != null)
            {
                controller.Logger.ZLogInformation($"[IngredientSelectionController] 총 추진력 {totalThrust} (엔진={controller.ConfirmedEngineValue} + 연료={controller.ConfirmedFuelValue} - 탑재={controller.ConfirmedPayloadValue}) vs 목적지 '{controller.MissionBoard?.Destination}' -> {(valid ? "성공" : "실패")}");
            }
            return valid;
        }

        /// <summary> 임시 선택값을 포함한 추진력을 계산함. </summary>
        public int CalculatePreviewThrust(IngredientSelectionController controller)
        {
            return controller.CalculateLevel1PreviewThrust();
        }

        /// <summary> 레벨 1은 추가 시뮬레이션 연출이 없음. </summary>
        public UniTask PlayCompletionSimulationAsync(IngredientSelectionController controller, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
    }
}
