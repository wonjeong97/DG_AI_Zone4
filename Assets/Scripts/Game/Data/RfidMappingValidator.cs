using System;
using System.Collections.Generic;
using DGAIZone.App;
using DGAIZone.Data;
using UnityEngine;

namespace DGAIZone.Game.Data
{
    /// <summary>
    /// RfidMappings.json의 레벨 1~5 블록 정의를 레벨별 규칙으로 검사해 문제 목록을 돌려주는 검증기. 블록 목록이 비었거나 값이 빠지면
    /// 게임 중에는 아무 오류 없이 선택지가 비거나 미션을 깰 수 없게 되므로, 로드 직후 호출해 원인을 오류 로그로 남기는 데 씀.
    /// </summary>
    public static class RfidMappingValidator
    {
        private static readonly string[] Level2LaunchIds =
        {
            Constants.RfidIds.Level2.Ignite,
            Constants.RfidIds.Level2.Ascend,
            Constants.RfidIds.Level2.SeparateStage1,
            Constants.RfidIds.Level2.SeparateStage2,
            Constants.RfidIds.Level2.EnterOrbit
        };

        private static readonly string[] Level4MoveIds =
        {
            Constants.RfidIds.Level4.MoveUp,
            Constants.RfidIds.Level4.MoveDown,
            Constants.RfidIds.Level4.MoveRight,
            Constants.RfidIds.Level4.MoveLeft
        };

        /// <summary>
        /// 레벨 1~5 블록 정의를 검사함. level1Data/level3Data를 넘기면 레벨 1 목적지 거리를 블록 조합으로 만들 수 있는지,
        /// 레벨 3 기준값 범위의 모든 값에 맞는 조건 블록이 있는지도 검사함(null이면 그 검사는 건너뜀). 문제가 없으면 빈 목록을 반환함.
        /// </summary>
        public static List<string> Validate(RfidSettings settings, LevelData level1Data = null, LevelData level3Data = null)
        {
            List<string> errors = new List<string>();
            if (settings == null || settings.levelMappings == null || settings.levelMappings.Length == 0)
            {
                errors.Add("levelMappings가 비어 있어 모든 레벨의 블록 정의가 없음.");
                return errors;
            }

            ValidateLevel1(settings.FindLevelMapping(1), level1Data, errors);
            ValidateLevel2(settings.FindLevelMapping(2), errors);
            ValidateLevel3(settings.FindLevelMapping(3), level3Data, errors);
            ValidateLevel4(settings.FindLevelMapping(4), errors);
            ValidateLevel5(settings.FindLevelMapping(5), errors);
            return errors;
        }

        /// <summary> 레벨 1: 엔진·탑재·연료 블록의 value가 모두 0보다 크고, 목적지 거리마다 엔진 + 연료 - 탑재 조합이 있어야 함. </summary>
        private static void ValidateLevel1(RfidLevelMapping mapping, LevelData levelData, List<string> errors)
        {
            if (!ValidateLevelBasics(1, mapping, errors)) return;

            RfidMatter[] engines = GetRequiredMatters(1, mapping, Constants.RfidIds.Level1.Engine, errors);
            RfidMatter[] payloads = GetRequiredMatters(1, mapping, Constants.RfidIds.Level1.Payload, errors);
            RfidMatter[] fuels = GetRequiredMatters(1, mapping, Constants.RfidIds.Level1.Fuel, errors);
            const string thrustReason = "추진력 계산에 쓰이며 빠지면 0으로 계산됨";
            RequireMinValue(engines, "레벨 1 엔진(Engine)", 1, thrustReason, errors);
            RequireMinValue(payloads, "레벨 1 탑재(Payload)", 1, thrustReason, errors);
            RequireMinValue(fuels, "레벨 1 연료(Fuel)", 1, thrustReason, errors);

            if (levelData == null || levelData.destinations == null || engines == null || payloads == null || fuels == null) return;

            HashSet<int> reachable = new HashSet<int>();
            foreach (RfidMatter engine in engines)
            {
                foreach (RfidMatter fuel in fuels)
                {
                    foreach (RfidMatter payload in payloads)
                    {
                        if (engine != null && fuel != null && payload != null) reachable.Add(engine.value + fuel.value - payload.value);
                    }
                }
            }

            foreach (MissionDestination destination in levelData.destinations)
            {
                if (destination == null)
                {
                    errors.Add("레벨 1 LevelData의 목적지(destinations)에 빈 항목이 있음.");
                    continue;
                }

                if (!reachable.Contains(destination.targetDistance))
                {
                    errors.Add($"레벨 1 목적지 '{destination.planetName}'(거리 {destination.targetDistance})는 엔진 + 연료 - 탑재 블록 조합으로 만들 수 없어 성공할 수 없음.");
                }
            }
        }

        /// <summary> 레벨 2: 단계 수가 발사 순서 블록 수와 같고, 단계마다 발사 순서 블록이 모두 있어야 함. </summary>
        private static void ValidateLevel2(RfidLevelMapping mapping, List<string> errors)
        {
            if (!ValidateLevelBasics(2, mapping, errors)) return;

            if (mapping.steps.Length != Level2LaunchIds.Length)
            {
                errors.Add($"레벨 2 단계 수가 {mapping.steps.Length}개라 발사 순서 블록 {Level2LaunchIds.Length}개와 맞지 않음.");
            }

            for (int i = 0; i < mapping.steps.Length; i++)
            {
                if (mapping.steps[i] == null) continue; // ValidateLevelBasics가 이미 오류로 남김

                string where = $"레벨 2 {i + 1}번째 단계";
                RfidMatter[] matters = GetMatters(mapping, mapping.steps[i], where, errors);
                foreach (string launchId in Level2LaunchIds)
                {
                    RequireMatterId(matters, launchId, where, "발사 순서를 완성할 수 없음", errors);
                }
            }
        }

        /// <summary> 레벨 3: 다섯 재료가 모두 있고, 조건 블록 value가 0보다 크며 기준값 범위를 모두 덮고, 정답 동작·논리 블록이 있어야 함. </summary>
        private static void ValidateLevel3(RfidLevelMapping mapping, LevelData levelData, List<string> errors)
        {
            if (!ValidateLevelBasics(3, mapping, errors)) return;

            RfidMatter[] electricityConditions = GetRequiredMatters(3, mapping, Constants.RfidIds.Level3.ElectricityCondition, errors);
            RfidMatter[] electricity = GetRequiredMatters(3, mapping, Constants.RfidIds.Level3.Electricity, errors);
            RfidMatter[] logic = GetRequiredMatters(3, mapping, Constants.RfidIds.Level3.Logic, errors);
            RfidMatter[] oxygenConditions = GetRequiredMatters(3, mapping, Constants.RfidIds.Level3.OxygenCondition, errors);
            RfidMatter[] oxygen = GetRequiredMatters(3, mapping, Constants.RfidIds.Level3.Oxygen, errors);

            RequireMinValue(electricityConditions, "레벨 3 전기량 조건(ElectricityCondition)", 1, "미션 기준값과 비교함", errors);
            RequireMinValue(oxygenConditions, "레벨 3 산소량 조건(OxygenCondition)", 1, "미션 기준값과 비교함", errors);
            RequireMatterId(electricity, Constants.RfidIds.Level3.Lower, "레벨 3 전기량(Electricity) 단계", "전기 게이지를 채울 수 없음", errors);
            RequireMatterId(oxygen, Constants.RfidIds.Level3.Raise, "레벨 3 산소량(Oxygen) 단계", "산소 게이지를 채울 수 없음", errors);

            if (logic != null && Array.TrueForAll(logic, m => m == null || string.Equals(m.id, Constants.RfidIds.Level3.Or, StringComparison.Ordinal)))
            {
                errors.Add($"레벨 3 논리(Logic) 단계에 '{Constants.RfidIds.Level3.Or}' 말고 고를 블록이 없어 항상 불안정해 성공할 수 없음.");
            }

            if (levelData == null) return;
            RequireRangeCovered(levelData.maxElectricityRange, "레벨 3 LevelData의 전기량 상한 범위(maxElectricityRange)", electricityConditions, "전기량 조건", errors);
            RequireRangeCovered(levelData.minOxygenRange, "레벨 3 LevelData의 산소량 하한 범위(minOxygenRange)", oxygenConditions, "산소량 조건", errors);
        }

        /// <summary>
        /// 레벨 4: 동작 카드로 고르는 이동하기, 제어 카드로 고르는 반복하기 재료가 있고, 이동 방향은 아는 id, 반복 횟수는 1 이상이어야 함.
        /// 보드 배치 규칙에 맞게 단계 수는 카드 수(Constants.Level4Board.MaxCards)와 같고, 반복 횟수 블록에 RequiredRepeatCount가 있어야 하며,
        /// 모든 단계가 동작 카드를, 마지막을 뺀 모든 단계가 제어 카드를 받아야 함.
        /// </summary>
        private static void ValidateLevel4(RfidLevelMapping mapping, List<string> errors)
        {
            if (!ValidateLevelBasics(4, mapping, errors)) return;

            RfidMatter[] moves = GetCategoryIngredientMatters(4, mapping, Constants.RfidCategories.Action, Constants.RfidIds.Level4.Move, errors);
            RfidMatter[] repeats = GetCategoryIngredientMatters(4, mapping, Constants.RfidCategories.Control, Constants.RfidIds.Level4.Repeat, errors);

            if (moves != null)
            {
                foreach (RfidMatter move in moves)
                {
                    if (move != null && Array.IndexOf(Level4MoveIds, move.id) < 0)
                    {
                        errors.Add($"레벨 4 이동하기 블록 '{move.id}'는 알 수 없는 방향이라 로봇이 움직이지 않음.");
                    }
                }
            }

            RequireMinValue(repeats, "레벨 4 반복하기(Repeat)", 1, "반복 횟수로 쓰임", errors);

            // 보드는 이동하기만으로는 MaxCards장 안에 못 풀고 '반복하기(RequiredRepeatCount회) + 이동하기'로 풀리는 배치만 고르므로,
            // 카드 수(단계 수)가 다르거나 그 반복 횟수 블록이 없으면 풀 수 없거나 반복하기 없이도 풀리게 됨
            int maxCards = Constants.Level4Board.MaxCards;
            if (mapping.steps.Length != maxCards)
            {
                errors.Add($"레벨 4 단계 수가 {mapping.steps.Length}개라 보드 배치 기준 카드 수 {maxCards}장과 맞지 않음(적으면 풀 수 없고 많으면 반복하기 없이도 풀림).");
            }

            int requiredRepeat = Constants.Level4Board.RequiredRepeatCount;
            if (repeats != null && !Array.Exists(repeats, m => m != null && m.value == requiredRepeat))
            {
                errors.Add($"레벨 4 반복하기 블록에 {requiredRepeat}회(value {requiredRepeat})가 없어 보드를 풀 수 없음(배치가 {requiredRepeat}회 반복을 전제로 함).");
            }

            // 배치 검사는 모든 단계에 이동하기를, 마지막을 뺀 모든 단계에 반복하기를 놓을 수 있다고 보므로 단계별 허용 카드도 그에 맞아야 함
            for (int i = 0; i < mapping.steps.Length; i++)
            {
                string[] categories = mapping.steps[i]?.categories;
                if (categories == null) continue; // ValidateLevelBasics가 이미 오류로 남김

                if (Array.IndexOf(categories, Constants.RfidCategories.Action) < 0)
                {
                    errors.Add($"레벨 4 {i + 1}번째 단계가 '{Constants.RfidCategories.Action}' 카드를 받지 않아 이동하기를 놓을 수 없음.");
                }

                bool isLastStep = i == mapping.steps.Length - 1;
                if (!isLastStep && Array.IndexOf(categories, Constants.RfidCategories.Control) < 0)
                {
                    errors.Add($"레벨 4 {i + 1}번째 단계가 '{Constants.RfidCategories.Control}' 카드를 받지 않아 반복하기를 놓을 수 없는 배치가 생김(마지막 단계를 뺀 모든 단계에 반복하기를 놓을 수 있어야 함).");
                }
            }
        }

        /// <summary>
        /// 레벨 5(임시 규칙): 함수·동작 카드로 고르는 재료가 있어야 하고, 동작 블록은 놓을 동작 카드 수(Constants.Level5Cards.Action) 이상이어야 함
        /// (놓은 블록은 다시 고를 수 없음). 카드를 순서 없이 놓으므로 단계 수는 카드 수 합과 같고 모든 단계가 두 분류 카드를 받아야 함.
        /// </summary>
        private static void ValidateLevel5(RfidLevelMapping mapping, List<string> errors)
        {
            if (!ValidateLevelBasics(5, mapping, errors)) return;

            GetCategoryIngredientMatters(5, mapping, Constants.RfidCategories.Func, Constants.RfidIds.Level5.Function, errors);
            RfidMatter[] actions = GetCategoryIngredientMatters(5, mapping, Constants.RfidCategories.Action, Constants.RfidIds.Level5.Action, errors);

            if (actions != null && actions.Length < Constants.Level5Cards.Action)
            {
                errors.Add($"레벨 5 동작 블록이 {actions.Length}개라 동작 카드 {Constants.Level5Cards.Action}장을 모두 놓을 수 없음(놓은 블록은 다시 고를 수 없음).");
            }

            if (mapping.steps.Length != Constants.Level5Cards.Total)
            {
                errors.Add($"레벨 5 단계 수가 {mapping.steps.Length}개라 놓을 카드 수 합 {Constants.Level5Cards.Total}장과 맞지 않음.");
            }

            string[] required = { Constants.RfidCategories.Func, Constants.RfidCategories.Action };
            for (int i = 0; i < mapping.steps.Length; i++)
            {
                string[] categories = mapping.steps[i]?.categories;
                if (categories == null) continue; // ValidateLevelBasics가 이미 오류로 남김

                foreach (string category in required)
                {
                    if (Array.IndexOf(categories, category) < 0)
                    {
                        errors.Add($"레벨 5 {i + 1}번째 단계가 '{category}' 카드를 받지 않아 카드를 순서 없이 놓을 수 없음.");
                    }
                }
            }
        }

        /// <summary> 레벨 정의와 단계 목록이 있는지, 블록 목록 id·블록 id가 비거나 중복되지 않는지 검사함. 이후 검사를 이어갈 수 없으면 false. </summary>
        private static bool ValidateLevelBasics(int level, RfidLevelMapping mapping, List<string> errors)
        {
            if (mapping == null)
            {
                errors.Add($"레벨 {level} 정의(levelMappings의 level {level})가 없음.");
                return false;
            }

            if (mapping.steps == null || mapping.steps.Length == 0)
            {
                errors.Add($"레벨 {level}의 단계(steps)가 비어 있음.");
                return false;
            }

            if (mapping.matterSets == null || mapping.matterSets.Length == 0)
            {
                errors.Add($"레벨 {level}의 블록 목록(matterSets)이 비어 있음.");
            }
            else
            {
                HashSet<string> setIds = new HashSet<string>();
                foreach (RfidMatterSet set in mapping.matterSets)
                {
                    if (set == null || string.IsNullOrEmpty(set.id))
                    {
                        errors.Add($"레벨 {level}에 id가 비어 있는 블록 목록(matterSets)이 있음.");
                        continue;
                    }

                    if (!setIds.Add(set.id)) errors.Add($"레벨 {level}에 블록 목록 id '{set.id}'가 두 번 정의됨.");

                    HashSet<string> matterIds = new HashSet<string>();
                    foreach (RfidMatter matter in set.matters ?? Array.Empty<RfidMatter>())
                    {
                        if (matter == null || string.IsNullOrEmpty(matter.id)) errors.Add($"레벨 {level} 블록 목록 '{set.id}'에 id가 비어 있는 블록이 있음.");
                        else if (!matterIds.Add(matter.id)) errors.Add($"레벨 {level} 블록 목록 '{set.id}'에 블록 id '{matter.id}'가 중복됨.");
                    }
                }
            }

            for (int i = 0; i < mapping.steps.Length; i++)
            {
                RfidStepDefinition step = mapping.steps[i];
                if (step == null)
                {
                    errors.Add($"레벨 {level} {i + 1}번째 단계가 비어 있음.");
                }
                else if (step.categories == null || step.categories.Length == 0)
                {
                    errors.Add($"레벨 {level} {i + 1}번째 단계({step.ingredientId})의 categories가 비어 있어 어떤 카드로도 진행할 수 없음.");
                }
            }

            return true;
        }

        /// <summary> 재료 정의가 matterSetId로 참조하는 블록 목록을 찾음. 없거나 비어 있으면 오류를 추가하고 null을 반환함. </summary>
        private static RfidMatter[] GetMatters(RfidLevelMapping mapping, RfidStepDefinition ingredient, string where, List<string> errors)
        {
            if (string.IsNullOrEmpty(ingredient.matterSetId))
            {
                errors.Add($"{where}의 matterSetId가 비어 있어 고를 블록이 없음.");
                return null;
            }

            RfidMatter[] matters = mapping.FindMatters(ingredient.matterSetId);
            if (matters == null || matters.Length == 0)
            {
                errors.Add($"{where}가 참조하는 블록 목록 '{ingredient.matterSetId}'가 없거나 비어 있어 고를 블록이 없음.");
                return null;
            }

            return matters;
        }

        /// <summary> ingredientId 단계를 찾아 그 블록 목록을 반환함. 단계가 없거나 블록 목록이 비어 있으면 오류를 추가하고 null을 반환함. </summary>
        private static RfidMatter[] GetRequiredMatters(int level, RfidLevelMapping mapping, string ingredientId, List<string> errors)
        {
            foreach (RfidStepDefinition step in mapping.steps)
            {
                if (step != null && string.Equals(step.ingredientId, ingredientId, StringComparison.Ordinal))
                {
                    return GetMatters(mapping, step, $"레벨 {level} '{ingredientId}' 단계", errors);
                }
            }

            errors.Add($"레벨 {level}에 '{ingredientId}' 단계가 없음.");
            return null;
        }

        /// <summary> category 카드로 고르는 재료(레벨 4 categoryIngredients)를 찾아 ingredientId를 확인하고 블록 목록을 반환함. 문제가 있으면 오류를 추가하고 null을 반환함. </summary>
        private static RfidMatter[] GetCategoryIngredientMatters(int level, RfidLevelMapping mapping, string category, string expectedIngredientId, List<string> errors)
        {
            if (mapping.categoryIngredients != null)
            {
                foreach (RfidStepDefinition ingredient in mapping.categoryIngredients)
                {
                    if (ingredient == null || ingredient.categories == null || Array.IndexOf(ingredient.categories, category) < 0) continue;

                    if (!string.Equals(ingredient.ingredientId, expectedIngredientId, StringComparison.Ordinal))
                    {
                        errors.Add($"레벨 {level} '{category}' 카드 재료의 ingredientId가 '{ingredient.ingredientId}'라 '{expectedIngredientId}'여야 함.");
                        return null;
                    }

                    return GetMatters(mapping, ingredient, $"레벨 {level} '{category}' 카드 재료({expectedIngredientId})", errors);
                }
            }

            errors.Add($"레벨 {level} categoryIngredients에 '{category}' 카드로 고르는 재료({expectedIngredientId})가 없어 '{category}' 카드를 찍어도 고를 블록이 없음.");
            return null;
        }

        /// <summary> 블록마다 value가 minValue 이상인지 검사함. matters가 null이면(이미 오류로 남김) 건너뜀. </summary>
        private static void RequireMinValue(RfidMatter[] matters, string where, int minValue, string reason, List<string> errors)
        {
            if (matters == null) return;

            foreach (RfidMatter matter in matters)
            {
                if (matter != null && matter.value < minValue)
                {
                    errors.Add($"{where} 블록 '{matter.id}'의 value({matter.value})는 {minValue} 이상이어야 함({reason}).");
                }
            }
        }

        /// <summary> 블록 목록에 정답 블록 id가 있는지 검사함. matters가 null이면(이미 오류로 남김) 건너뜀. </summary>
        private static void RequireMatterId(RfidMatter[] matters, string matterId, string where, string consequence, List<string> errors)
        {
            if (matters == null) return;

            foreach (RfidMatter matter in matters)
            {
                if (matter != null && string.Equals(matter.id, matterId, StringComparison.Ordinal)) return;
            }

            errors.Add($"{where}에 '{matterId}' 블록이 없어 {consequence}.");
        }

        /// <summary> 기준값 범위(양끝 포함)의 모든 값마다 value가 같은 조건 블록이 있는지 검사함. 없으면 그 기준값이 뽑혔을 때 정답을 고를 수 없음. </summary>
        private static void RequireRangeCovered(Vector2Int range, string rangeName, RfidMatter[] conditions, string conditionName, List<string> errors)
        {
            if (range.x > range.y)
            {
                errors.Add($"{rangeName}의 최소({range.x})가 최대({range.y})보다 큼.");
                return;
            }

            if (conditions == null) return;

            for (int value = range.x; value <= range.y; value++)
            {
                int target = value;
                if (!Array.Exists(conditions, m => m != null && m.value == target))
                {
                    errors.Add($"{rangeName} {range.x}~{range.y} 중 {value}에 맞는 {conditionName} 블록이 없어 그 기준값이 나오면 성공할 수 없음.");
                }
            }
        }
    }
}
