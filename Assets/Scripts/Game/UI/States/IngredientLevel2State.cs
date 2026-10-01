using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZLogger;

namespace DGAIZone.Game.UI.States
{
    /// <summary>
    /// 레벨 2 (발사 코딩 순서 및 스텝 볼 표시) 워크플로우를 담당하는 상태 클래스.
    /// </summary>
    public class IngredientLevel2State : IIngredientSelectionLevelState
    {
        // 올바른 발사 순서(물질 id). Level2StepBallTexts와 같은 인덱스끼리 짝을 이룸
        private static readonly string[] Level2LaunchSequence =
        {
            Constants.RfidIds.Level2.Ignite,
            Constants.RfidIds.Level2.Ascend,
            Constants.RfidIds.Level2.SeparateStage1,
            Constants.RfidIds.Level2.SeparateStage2,
            Constants.RfidIds.Level2.EnterOrbit
        };

        private static readonly string[] Level2StepBallTexts =
        {
            "점화 시퀀스\n완료", "상승 시퀀스\n준비 완료", "1차 로켓\n준비 완료", "2차 로켓\n준비 완료", "진입 궤도\n계산 완료"
        };

        private Tween _level2FillTween;

        /// <summary> 상태 진입 시 레벨 2 진행바를 0으로 초기화. </summary>
        public void Enter(IngredientSelectionController context)
        {
            if (context.Level2FillImage)
            {
                context.Level2FillImage.fillAmount = 0f;
            }
            else if (context.Logger != null)
            {
                context.Logger.ZLogWarning($"[IngredientSelectionController] level2FillImage가 null이라 진행바를 초기화할 수 없음.");
            }
        }

        /// <summary> 매 프레임 업데이트. </summary>
        public void Update(IngredientSelectionController context)
        {
        }

        /// <summary> 상태 종료 시 진행바 트윈 정리. </summary>
        public void Exit(IngredientSelectionController context)
        {
            _level2FillTween?.Kill();
        }

        /// <summary> 모든 태그 카테고리를 허용함. </summary>
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

        /// <summary>
        /// 이미 확정된 물질을 제외한 목록을 순서를 섞어 반환함. JSON의 선택지가 정답 순서대로 나열돼 있어 섞지 않으면
        /// 좌우 버튼을 누르지 않고 설정하기만 눌러도 항상 정답 순서가 되므로, 카드를 찍을 때마다 새로 섞음(원본 배열은 건드리지 않음).
        /// </summary>
        public RfidMatter[] FilterMatters(IngredientSelectionController controller, string ingredientId, RfidMatter[] matters)
        {
            RfidMatter[] shuffled = (RfidMatter[])controller.ExcludeConfirmedMatters(ingredientId, matters).Clone();
            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            return shuffled;
        }

        /// <summary> 단계 확정 시 스텝 볼에 완료 색상 및 문구를 표시하고, 확정된 개수(stepIndex + 1)만큼 진행바를 채움. </summary>
        public void OnStepConfirmed(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter chosenMatter)
        {
            UpdateStepBallDisplay(controller, stepIndex, chosenMatter.id, true);
            UpdateLevel2FillAmount(controller, stepIndex + 1);
        }

        /// <summary>
        /// 단계 취소 시 스텝 볼을 흑백으로 되돌리고 문구를 제거하며, 남는 확정 개수(stepIndex)만큼 진행바를 줄임.
        /// 컨트롤러는 이 메서드를 호출한 뒤에 디자인 항목을 지우므로, 디자인 항목 수가 아니라 stepIndex로 계산함.
        /// </summary>
        public void OnStepRolledBack(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter matter)
        {
            UpdateStepBallDisplay(controller, stepIndex, matter.id, false);
            UpdateLevel2FillAmount(controller, stepIndex);
        }

        /// <summary> 레벨 2는 모든 단계의 재료명이 동일하므로 재료명 없이 물질만 표기함. </summary>
        public string FormatDesignItemText(IngredientSelectionController controller, string ingredientName, string matterLabel)
        {
            return $" · [<color=yellow>{controller.ApplyNumberSizeTag(matterLabel)}</color>]";
        }

        /// <summary> 모든 단계가 완료되었을 때만 코딩완료 버튼을 활성화함. </summary>
        public bool IsCodingCompleteInteractable(IngredientSelectionController controller, int designItemCount, int totalSteps)
        {
            return designItemCount >= totalSteps;
        }

        /// <summary> 확정된 5단계의 값이 올바른 발사 시퀀스 순서와 일치하는지 판정함. </summary>
        public bool EvaluateMission(IngredientSelectionController controller)
        {
            RfidMatter[] confirmedMatters = controller.ConfirmedMatters;
            if (confirmedMatters == null || confirmedMatters.Length < Level2LaunchSequence.Length)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 2 확정 단계 수가 부족함. 실패로 처리함.");
                return false;
            }

            for (int i = 0; i < Level2LaunchSequence.Length; i++)
            {
                if (!string.Equals(confirmedMatters[i]?.id, Level2LaunchSequence[i], StringComparison.Ordinal))
                {
                    if (controller.Logger != null)
                    {
                        controller.Logger.ZLogInformation($"[IngredientSelectionController] 레벨 2 판정: {i + 1}번째 단계가 '{Level2LaunchSequence[i]}'가 아니라 '{confirmedMatters[i]?.id}'({confirmedMatters[i]?.label})라 순서가 어긋남. 실패로 처리함.");
                    }
                    return false;
                }
            }

            if (controller.Logger != null)
            {
                controller.Logger.ZLogInformation($"[IngredientSelectionController] 레벨 2 판정: 발사 코딩 순서가 정확히 일치함. 성공으로 처리함.");
            }
            return true;
        }

        /// <summary> 단계 순서대로 올바른 발사 시퀀스(Level2LaunchSequence)의 블록을 반환함. </summary>
        public List<(RfidStepDefinition ingredient, RfidMatter matter)> BuildSolution(IngredientSelectionController controller)
        {
            List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = new List<(RfidStepDefinition ingredient, RfidMatter matter)>();
            RfidStepDefinition[] steps = controller.StepDefinitions;
            RfidLevelMapping mapping = controller.LevelMapping;
            if (steps == null || mapping == null || steps.Length < Level2LaunchSequence.Length)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 2 단계 정의가 없거나 발사 순서({Level2LaunchSequence.Length}단계)보다 짧아 정답 설계를 만들 수 없음.");
                return solution;
            }

            for (int i = 0; i < Level2LaunchSequence.Length; i++)
            {
                RfidStepDefinition step = steps[i];
                RfidMatter[] matters = step != null ? mapping.FindMatters(step.matterSetId) : null;
                RfidMatter matter = matters != null ? Array.Find(matters, m => m.id == Level2LaunchSequence[i]) : null;
                if (matter == null)
                {
                    if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 2 {i + 1}번째 단계 블록 목록에 '{Level2LaunchSequence[i]}'가 없어 정답 설계를 만들 수 없음.");
                    solution.Clear();
                    return solution;
                }

                solution.Add((step, matter));
            }

            return solution;
        }

        /// <summary> 레벨 2는 추진력 계산식을 사용하지 않음. </summary>
        public int CalculatePreviewThrust(IngredientSelectionController controller)
        {
            return 0;
        }

        /// <summary> 레벨 2는 추가 시뮬레이션 연출이 없음. </summary>
        public UniTask PlayCompletionSimulationAsync(IngredientSelectionController controller, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 레벨 2 진행바(Image_Fill)를 확정된 항목 개수(confirmedCount)에 맞춰 갱신함. 진행바는 스텝 볼 사이를 잇는 선이라
        /// 1개 확정 시 0, 5개 확정 시 1이 됨.
        /// </summary>
        private void UpdateLevel2FillAmount(IngredientSelectionController controller, int confirmedCount)
        {
            Image fillImage = controller.Level2FillImage;
            if (!fillImage)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] level2FillImage가 null이라 진행바를 갱신할 수 없음.");
                return;
            }

            float target = Mathf.Clamp01((confirmedCount - 1) / 4f);

            _level2FillTween?.Kill();
            _level2FillTween = fillImage.DOFillAmount(target, controller.Level2FillTweenDuration)
                .SetEase(Ease.OutBack, controller.Level2FillOvershoot)
                .SetLink(fillImage.gameObject);
        }

        /// <summary>
        /// 레벨 2에서 확정/취소된 단계에 해당하는 Image_StepN_Ball의 색상과 완료 텍스트를 갱신함.
        /// </summary>
        private void UpdateStepBallDisplay(IngredientSelectionController controller, int stepIndex, string matterId, bool completed)
        {
            Image[] stepBalls = controller.StepBallImages;
            if (stepBalls == null) return;

            if (stepIndex < 0 || stepIndex >= stepBalls.Length)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] stepIndex({stepIndex})가 stepBallImages 범위(0~{stepBalls.Length - 1})를 벗어나 스텝 볼 표시를 건너뜀.");
                return;
            }

            Image ballImage = stepBalls[stepIndex];
            if (!ballImage)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] stepBallImages[{stepIndex}]가 null이라 스텝 볼 표시를 건너뜀.");
                return;
            }

            ballImage.material = completed ? null : controller.StepBallGrayscaleMaterial;

            if (!ChildComponentFinder.TryGetInDirectChildren(ballImage.transform, out TMP_Text ballText))
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] {ballImage.name}의 직계 자식에 TMP_Text가 없어 완료 문구를 표시할 수 없음.");
                return;
            }

            if (!completed)
            {
                ballText.text = "";
                return;
            }

            int textIndex = Array.IndexOf(Level2LaunchSequence, matterId);
            ballText.text = (textIndex >= 0 && textIndex < Level2StepBallTexts.Length) ? Level2StepBallTexts[textIndex] : "";
        }
    }
}
