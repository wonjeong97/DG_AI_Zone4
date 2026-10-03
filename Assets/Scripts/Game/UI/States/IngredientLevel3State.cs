using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using DGAIZone.Game.Events;
using UnityEngine;
using UnityEngine.UI;
using ZLogger;

namespace DGAIZone.Game.UI.States
{
    /// <summary>
    /// 레벨 3 (생명유지장치 게이지 및 논리 조건/아이콘 불안정 연출) 워크플로우를 담당하는 상태 클래스.
    /// </summary>
    public class IngredientLevel3State : IIngredientSelectionLevelState
    {
        private float _level3OxygenFill;
        private float _level3ElectricFill;
        private Tween _level3OxygenGaugeTween;
        private Tween _level3ElectricGaugeTween;
        private Tween _level3OxygenIconBlinkTween;
        private Tween _level3ElectricIconBlinkTween;
        private bool _level3InstabilityPending;

        /// <summary> 산소 게이지 충전량 (0.0 ~ 1.0). </summary>
        public float OxygenFill => _level3OxygenFill;

        /// <summary> 전기 게이지 충전량 (0.0 ~ 1.0). </summary>
        public float ElectricFill => _level3ElectricFill;

        /// <summary> 논리 연결어 '또는' 선택으로 인한 시스템 불안정 예약 여부. </summary>
        public bool InstabilityPending => _level3InstabilityPending;

        /// <summary> 상태 진입 시 산소/전기 게이지와 아이콘 상태를 0으로 초기화. </summary>
        public void Enter(IngredientSelectionController context)
        {
            InitializeGauges(context);
        }

        /// <summary> 매 프레임 업데이트. </summary>
        public void Update(IngredientSelectionController context)
        {
        }

        /// <summary> 상태 종료 시 모든 게이지 및 깜빡임 트윈 정리. </summary>
        public void Exit(IngredientSelectionController context)
        {
            _level3OxygenGaugeTween?.Kill();
            _level3ElectricGaugeTween?.Kill();
            _level3OxygenIconBlinkTween?.Kill();
            _level3ElectricIconBlinkTween?.Kill();
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

        /// <summary> 이미 확정된 물질을 제외한 목록을 반환함. </summary>
        public RfidMatter[] FilterMatters(IngredientSelectionController controller, string ingredientId, RfidMatter[] matters)
        {
            return controller.ExcludeConfirmedMatters(ingredientId, matters);
        }

        /// <summary> 확정된 단계와 값에 따라 게이지를 채우거나 불안정 상태를 예약하고, 5단계 완료 시 불안정 깜빡임을 시작함. </summary>
        public void OnStepConfirmed(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter chosenMatter)
        {
            UpdateLevel3Effects(controller, ingredientId, chosenMatter);

            if (stepIndex + 1 >= controller.TotalSteps && _level3InstabilityPending)
            {
                StartLevel3IconInstability(controller);
            }
        }

        /// <summary> 취소 시 적용되었던 게이지 충전량과 효과를 반대로 되돌림. </summary>
        public void OnStepRolledBack(IngredientSelectionController controller, int stepIndex, string ingredientId, RfidMatter matter)
        {
            RevertLevel3Effects(controller, ingredientId, matter);
        }

        /// <summary> 재료명이 비어있는 경우(논리 연결어) 물질만 표시하고, 그 외에는 재료와 물질을 함께 표시함. </summary>
        public string FormatDesignItemText(IngredientSelectionController controller, string ingredientName, string matterLabel)
        {
            return string.IsNullOrEmpty(ingredientName)
                ? $" · [<color=yellow>{controller.ApplyNumberSizeTag(matterLabel)}</color>]"
                : $" · {ingredientName} [<color=yellow>{controller.ApplyNumberSizeTag(matterLabel)}</color>]";
        }

        /// <summary> 재료 이름은 명령(만약) 블록, 고른 블록 이름은 값 블록에 씀. 재료 이름이 없는 단계(논리 연결어)는 값 블록 없이 블록 이름만 씀. </summary>
        public (string command, string value) GetDesignBlockTexts(IngredientSelectionController controller, string ingredientName, string matterLabel)
        {
            return string.IsNullOrEmpty(ingredientName) ? (matterLabel, null) : (ingredientName, matterLabel);
        }

        /// <summary> 조건(만약 전기량이/산소량이)은 만약 ㄷ자 블록, 뒤따르는 동작(전기량/산소량)은 그 안쪽, 논리 연결어는 논리 블록으로 쌓음. </summary>
        public DesignStepShape GetDesignStepShape(IngredientSelectionController controller, string ingredientId)
        {
            switch (ingredientId)
            {
                case Constants.RfidIds.Level3.ElectricityCondition:
                case Constants.RfidIds.Level3.OxygenCondition:
                    return DesignStepShape.If;
                case Constants.RfidIds.Level3.Electricity:
                case Constants.RfidIds.Level3.Oxygen:
                    return DesignStepShape.InsideIf;
                case Constants.RfidIds.Level3.Logic:
                    return DesignStepShape.Logic;
                default:
                    if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 3에서 알 수 없는 재료 id '{ingredientId}'라 설계창에 명령 블록으로 쌓음.");
                    return DesignStepShape.Command;
            }
        }

        /// <summary> 재료 이름이 있는 단계는 값 블록을 씀. </summary>
        public bool UsesValueBlocks => true;

        /// <summary> 모든 단계가 완료되었을 때만 코딩완료 버튼을 활성화함. </summary>
        public bool IsCodingCompleteInteractable(IngredientSelectionController controller, int designItemCount, int totalSteps)
        {
            return designItemCount >= totalSteps;
        }

        /// <summary> 산소/전기 게이지가 모두 100% 충전되고 시스템이 안정적인지 판정함. </summary>
        public bool EvaluateMission(IngredientSelectionController controller)
        {
            bool oxygenFull = _level3OxygenFill >= 1f;
            bool electricFull = _level3ElectricFill >= 1f;
            bool stable = !_level3InstabilityPending;
            bool success = oxygenFull && electricFull && stable;

            if (controller.Logger != null)
            {
                controller.Logger.ZLogInformation($"[IngredientSelectionController] 레벨 3 판정: 산소 게이지={_level3OxygenFill:F2}, 전기 게이지={_level3ElectricFill:F2}, 시스템 안정={stable} -> {(success ? "성공" : "실패")}");
            }

            return success;
        }

        /// <summary> 단계마다 이번 판 기준값에 맞는 정답 블록(IsCorrectBlock)을 골라 단계 순서대로 반환함. </summary>
        public List<(RfidStepDefinition ingredient, RfidMatter matter)> BuildSolution(IngredientSelectionController controller)
        {
            List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = new List<(RfidStepDefinition ingredient, RfidMatter matter)>();
            RfidStepDefinition[] steps = controller.StepDefinitions;
            RfidLevelMapping mapping = controller.LevelMapping;
            if (steps == null || mapping == null)
            {
                if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 3 단계 정의가 없어 정답 설계를 만들 수 없음.");
                return solution;
            }

            foreach (RfidStepDefinition step in steps)
            {
                RfidMatter[] matters = step != null ? mapping.FindMatters(step.matterSetId) : null;
                RfidMatter matter = matters != null ? Array.Find(matters, m => IsCorrectBlock(controller, step.ingredientId, m)) : null;
                if (matter == null)
                {
                    if (controller.Logger != null)
                    {
                        string limits = controller.MissionBoard
                            ? $"전기량 상한={controller.MissionBoard.MaxElectricity}, 산소량 하한={controller.MissionBoard.MinOxygen}"
                            : "미션 보드 없음";
                        controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 3 '{step?.ingredientId}' 단계에 이번 기준값({limits})에 맞는 블록이 없어 정답 설계를 만들 수 없음.");
                    }
                    solution.Clear();
                    return solution;
                }

                solution.Add((step, matter));
            }

            return solution;
        }

        /// <summary> 레벨 3은 추진력 계산식을 사용하지 않음. </summary>
        public int CalculatePreviewThrust(IngredientSelectionController controller)
        {
            return 0;
        }

        /// <summary> 추진력 계산식을 쓰지 않으므로 0. </summary>
        public int CalculateConfirmedThrust(IngredientSelectionController controller)
        {
            return 0;
        }

        /// <summary> 레벨 3은 추가 시뮬레이션 연출이 없음. </summary>
        public UniTask PlayCompletionSimulationAsync(IngredientSelectionController controller, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }

        /// <summary> 산소/전기 게이지 및 아이콘 알파를 0%로 되돌리고 깜빡임을 정지함. </summary>
        private void InitializeGauges(IngredientSelectionController controller)
        {
            _level3OxygenFill = 0f;
            _level3ElectricFill = 0f;
            _level3InstabilityPending = false;

            _level3OxygenGaugeTween?.Kill();
            _level3ElectricGaugeTween?.Kill();
            if (controller.Level3OxygenGauge) controller.Level3OxygenGauge.fillAmount = 0f;
            else if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] level3OxygenGauge가 null이라 산소 게이지를 초기화할 수 없음.");

            if (controller.Level3ElectricGauge) controller.Level3ElectricGauge.fillAmount = 0f;
            else if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] level3ElectricGauge가 null이라 전기 게이지를 초기화할 수 없음.");

            StopLevel3IconInstability(controller);
        }

        /// <summary>
        /// 확정된 재료(ingredientId)와 값에 따라 게이지를 채우거나 불안정 상태를 예약함. 단계 순서가 아니라 재료 id로 정하므로 JSON에서
        /// 단계 순서를 바꿔도 맞는 게이지가 참. '또는'(Or) 논리 블록이면 불안정 상태를 예약함.
        /// </summary>
        private void UpdateLevel3Effects(IngredientSelectionController controller, string ingredientId, RfidMatter matter)
        {
            if (string.Equals(ingredientId, Constants.RfidIds.Level3.Logic, StringComparison.Ordinal))
            {
                _level3InstabilityPending = string.Equals(matter.id, Constants.RfidIds.Level3.Or, StringComparison.Ordinal);
                return;
            }

            ApplyGaugeEffect(controller, ingredientId, matter, 0.5f);
        }

        /// <summary> 취소 시 UpdateLevel3Effects로 적용됐던 효과를 반대로 되돌림. </summary>
        private void RevertLevel3Effects(IngredientSelectionController controller, string ingredientId, RfidMatter matter)
        {
            StopLevel3IconInstability(controller);

            if (string.Equals(ingredientId, Constants.RfidIds.Level3.Logic, StringComparison.Ordinal))
            {
                _level3InstabilityPending = false;
                return;
            }

            ApplyGaugeEffect(controller, ingredientId, matter, -0.5f);
        }

        /// <summary>
        /// 정답 블록이면 재료에 해당하는 게이지를 delta만큼 증감함. 전기량 조건·전기량 동작은 전기 게이지를, 산소량 조건·산소량 동작은 산소 게이지를
        /// 바꾸며, 정답 여부는 IsCorrectBlock이 정함.
        /// </summary>
        private void ApplyGaugeEffect(IngredientSelectionController controller, string ingredientId, RfidMatter matter, float delta)
        {
            switch (ingredientId)
            {
                case Constants.RfidIds.Level3.ElectricityCondition:
                case Constants.RfidIds.Level3.Electricity:
                    if (IsCorrectBlock(controller, ingredientId, matter)) AddElectricGaugeFill(controller, delta);
                    break;
                case Constants.RfidIds.Level3.OxygenCondition:
                case Constants.RfidIds.Level3.Oxygen:
                    if (IsCorrectBlock(controller, ingredientId, matter)) AddOxygenGaugeFill(controller, delta);
                    break;
                default:
                    if (controller.Logger != null) controller.Logger.ZLogWarning($"[IngredientSelectionController] 레벨 3에서 알 수 없는 재료 id '{ingredientId}'라 게이지 효과를 적용하지 않음.");
                    break;
            }
        }

        /// <summary>
        /// 이번 판 기준값에서 정답 블록인지 판정함. 조건 블록은 value를 미션 기준값과, 동작 블록은 id(전기량 낮추기/산소량 올리기)를,
        /// 논리 블록은 '그리고'인지('또는'은 시스템 불안정)를 봄.
        /// </summary>
        private static bool IsCorrectBlock(IngredientSelectionController controller, string ingredientId, RfidMatter matter)
        {
            switch (ingredientId)
            {
                case Constants.RfidIds.Level3.ElectricityCondition:
                    return controller.MissionBoard && matter.value == controller.MissionBoard.MaxElectricity;
                case Constants.RfidIds.Level3.Electricity:
                    return string.Equals(matter.id, Constants.RfidIds.Level3.Lower, StringComparison.Ordinal);
                case Constants.RfidIds.Level3.OxygenCondition:
                    return controller.MissionBoard && matter.value == controller.MissionBoard.MinOxygen;
                case Constants.RfidIds.Level3.Oxygen:
                    return string.Equals(matter.id, Constants.RfidIds.Level3.Raise, StringComparison.Ordinal);
                case Constants.RfidIds.Level3.Logic:
                    return !string.Equals(matter.id, Constants.RfidIds.Level3.Or, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        /// <summary> 산소 게이지를 지정된 delta만큼 증감하고 아이콘 알파를 동기화함. </summary>
        private void AddOxygenGaugeFill(IngredientSelectionController controller, float delta)
        {
            _level3OxygenFill = Mathf.Clamp01(_level3OxygenFill + delta);
            Image gauge = controller.Level3OxygenGauge;
            if (gauge)
            {
                _level3OxygenGaugeTween?.Kill();
                _level3OxygenGaugeTween = gauge.DOFillAmount(_level3OxygenFill, controller.Level3GaugeTweenDuration).SetLink(gauge.gameObject);
            }
            SyncLevel3IconAlpha(controller.Level3OxygenIconCanvasGroup, _level3OxygenFill);
        }

        /// <summary> 전기 게이지를 지정된 delta만큼 증감하고 아이콘 알파를 동기화함. </summary>
        private void AddElectricGaugeFill(IngredientSelectionController controller, float delta)
        {
            _level3ElectricFill = Mathf.Clamp01(_level3ElectricFill + delta);
            Image gauge = controller.Level3ElectricGauge;
            if (gauge)
            {
                _level3ElectricGaugeTween?.Kill();
                _level3ElectricGaugeTween = gauge.DOFillAmount(_level3ElectricFill, controller.Level3GaugeTweenDuration).SetLink(gauge.gameObject);
            }
            SyncLevel3IconAlpha(controller.Level3ElectricIconCanvasGroup, _level3ElectricFill);
        }

        /// <summary> 불안정 깜빡임 중이 아닐 때 아이콘 알파를 게이지 충전량에 맞춤. </summary>
        private void SyncLevel3IconAlpha(CanvasGroup group, float gaugeFill)
        {
            if (!group) return;
            if (_level3OxygenIconBlinkTween != null && _level3OxygenIconBlinkTween.IsActive() && _level3OxygenIconBlinkTween.IsPlaying()) return;
            if (_level3ElectricIconBlinkTween != null && _level3ElectricIconBlinkTween.IsActive() && _level3ElectricIconBlinkTween.IsPlaying()) return;

            group.alpha = gaugeFill;
        }

        /// <summary> 게이지 충전량에 맞춰 아이콘 알파를 직접 설정함. </summary>
        private void SyncIconAlphaDirect(CanvasGroup group, float gaugeFill)
        {
            if (group) group.alpha = gaugeFill;
        }

        /// <summary> 시스템 불안정 시 산소/전기 아이콘을 최소 알파까지 깜빡이게 함. CanvasGroup을 사용해 정점 리빌드를 방지함. </summary>
        private void StartLevel3IconInstability(IngredientSelectionController controller)
        {
            StopLevel3IconInstability(controller);

            float minAlpha = controller.Level3IconBlinkMinAlpha;
            float cycleDuration = controller.Level3IconBlinkDuration;

            CanvasGroup oxGroup = controller.Level3OxygenIconCanvasGroup;
            CanvasGroup elGroup = controller.Level3ElectricIconCanvasGroup;

            if (oxGroup)
            {
                oxGroup.alpha = 1f;
                _level3OxygenIconBlinkTween = oxGroup.DOFade(minAlpha, cycleDuration)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetLink(oxGroup.gameObject);
            }

            if (elGroup)
            {
                elGroup.alpha = 1f;
                _level3ElectricIconBlinkTween = elGroup.DOFade(minAlpha, cycleDuration)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetLink(elGroup.gameObject);
            }
        }

        /// <summary> 아이콘 깜빡임을 멈추고 현재 게이지 알파로 복귀시킴. </summary>
        private void StopLevel3IconInstability(IngredientSelectionController controller)
        {
            _level3OxygenIconBlinkTween?.Kill();
            _level3ElectricIconBlinkTween?.Kill();
            _level3OxygenIconBlinkTween = null;
            _level3ElectricIconBlinkTween = null;

            SyncIconAlphaDirect(controller.Level3OxygenIconCanvasGroup, _level3OxygenFill);
            SyncIconAlphaDirect(controller.Level3ElectricIconCanvasGroup, _level3ElectricFill);
        }
    }
}
