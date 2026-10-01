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
            if (controller.IsRepeatFollowUpRequired() && !string.Equals(evt.Category, Constants.RfidCategories.Action, StringComparison.Ordinal))
            {
                if (controller.Logger != null)
                {
                    controller.Logger.ZLogInformation($"[IngredientSelectionController] 이전 단계가 '반복하기'라 {controller.CurrentStepIndex + 1}번째 단계는 '동작' 카드만 허용되는데 '{evt.Category}' 카드가 인식되어 {evt.ReaderId} 태그를 무시함.");
                }
                controller.ShowInvalidCategoryWarningAsync().Forget();
                return false;
            }

            return true;
        }

        /// <summary> 직전 단계가 '반복하기'이면 '동작'만, 아니면 단계 정의 카테고리를 안내함. </summary>
        public string[] GetAllowedCategories(IngredientSelectionController controller, RfidStepDefinition step)
        {
            return controller.IsRepeatFollowUpRequired() ? Level4RepeatFollowUpCategories : step.categories;
        }

        /// <summary> 스캔된 카드의 카테고리(동작/제어)에 맞는 재료(이동하기/반복하기)를 RfidMappings.json의 categoryIngredients에서 찾음. </summary>
        public RfidStepDefinition ResolveStepCard(IngredientSelectionController controller, RfidStepDefinition step, string category)
        {
            return controller.FindCategoryIngredient(category);
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

        /// <summary> "· 재료 [물질]" 형식으로 디자인 항목 텍스트를 구성함. </summary>
        public string FormatDesignItemText(IngredientSelectionController controller, string ingredientName, string matterLabel)
        {
            return $" · {ingredientName} [<color=yellow>{controller.ApplyNumberSizeTag(matterLabel)}</color>]";
        }

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

        /// <summary> 레벨 4는 추진력 계산식을 사용하지 않음. </summary>
        public int CalculatePreviewThrust(IngredientSelectionController controller)
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
