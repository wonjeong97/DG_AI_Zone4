using System.Collections.Generic;
using System.Linq;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using HuliacDev.Utils;
using NUnit.Framework;
using UnityEngine;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 실제 StreamingAssets/RfidMappings.json과 LevelData 에셋이 레벨 규칙에 맞는지 확인하는 데이터 무결성 테스트와,
    /// RfidMappingValidator가 잘못된 데이터를 찾아내는지 확인하는 테스트.
    /// </summary>
    public class RfidMappingDataTests
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

        private RfidSettings _settings;
        private readonly List<Object> _createdObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _settings = JsonLoader.Load<RfidSettings>(Constants.Files.RfidMappings);
            Assert.IsNotNull(_settings, "RfidMappings.json을 읽지 못함");
            Assert.IsNotNull(_settings.levelMappings, "RfidMappings.json에 levelMappings가 없음");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _createdObjects)
            {
                if (created) Object.DestroyImmediate(created);
            }
            _createdObjects.Clear();
        }

        /// <summary> 실제 LevelData 에셋(Assets/Data/LevelN.asset)을 읽음. 에셋 경로로 읽으므로 에디터에서만 실행됨. </summary>
        private static LevelData LoadLevelData(int level)
        {
#if UNITY_EDITOR
            LevelData data = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>($"Assets/Data/Level{level}.asset");
            Assert.IsNotNull(data, $"Assets/Data/Level{level}.asset을 찾지 못함");
            return data;
#else
            Assert.Ignore("LevelData 에셋은 에디터에서만 경로로 읽을 수 있음");
            return null;
#endif
        }

        /// <summary> 레벨 정의를 찾고 없으면 실패시킴. </summary>
        private RfidLevelMapping GetMapping(int level)
        {
            RfidLevelMapping mapping = _settings.FindLevelMapping(level);
            Assert.IsNotNull(mapping, $"레벨 {level} 정의가 없음");
            Assert.IsNotNull(mapping.steps, $"레벨 {level} steps가 없음");
            return mapping;
        }

        /// <summary> ingredientId 단계가 참조하는 블록 목록을 찾고, 단계나 블록이 없으면 실패시킴. </summary>
        private static RfidMatter[] GetStepMatters(RfidLevelMapping mapping, string ingredientId)
        {
            RfidStepDefinition step = mapping.steps.FirstOrDefault(s => s != null && s.ingredientId == ingredientId);
            Assert.IsNotNull(step, $"레벨 {mapping.level}에 '{ingredientId}' 단계가 없음");
            RfidMatter[] matters = mapping.FindMatters(step.matterSetId);
            Assert.IsNotNull(matters, $"레벨 {mapping.level} '{ingredientId}' 단계의 블록 목록 '{step.matterSetId}'가 없음");
            Assert.IsNotEmpty(matters, $"레벨 {mapping.level} '{ingredientId}' 단계의 블록 목록이 비어 있음");
            return matters;
        }

        /// <summary> 레벨 4에서 category 카드로 고르는 재료를 찾고, 없으면 실패시킴. </summary>
        private static RfidStepDefinition GetCategoryIngredient(RfidLevelMapping mapping, string category)
        {
            RfidStepDefinition ingredient = mapping.categoryIngredients?.FirstOrDefault(i => i?.categories != null && i.categories.Contains(category));
            Assert.IsNotNull(ingredient, $"레벨 {mapping.level} categoryIngredients에 '{category}' 카드 재료가 없음");
            return ingredient;
        }

        [Test]
        public void 단계와_분류별_재료의_블록_목록에_id가_중복되지_않는다()
        {
            foreach (RfidLevelMapping mapping in _settings.levelMappings)
            {
                IEnumerable<RfidStepDefinition> ingredients = mapping.steps.Concat(mapping.categoryIngredients ?? new RfidStepDefinition[0])
                    .Where(i => i != null && !string.IsNullOrEmpty(i.matterSetId));

                foreach (RfidStepDefinition ingredient in ingredients)
                {
                    RfidMatter[] matters = mapping.FindMatters(ingredient.matterSetId);
                    Assert.IsNotNull(matters, $"레벨 {mapping.level} '{ingredient.ingredientId}'의 블록 목록 '{ingredient.matterSetId}'가 없음");
                    Assert.IsNotEmpty(matters, $"레벨 {mapping.level} '{ingredient.ingredientId}'의 블록 목록이 비어 있음");

                    string[] duplicates = matters.GroupBy(m => m.id).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
                    CollectionAssert.IsEmpty(duplicates, $"레벨 {mapping.level} '{ingredient.ingredientId}'의 블록 id가 중복됨");
                }
            }
        }

        [Test]
        public void 레벨2_블록_id가_발사_순서_상수와_일치한다()
        {
            RfidLevelMapping mapping = GetMapping(2);
            Assert.AreEqual(Level2LaunchIds.Length, mapping.steps.Length, "레벨 2 단계 수는 발사 순서 블록 수와 같아야 함");

            for (int i = 0; i < mapping.steps.Length; i++)
            {
                RfidMatter[] matters = mapping.FindMatters(mapping.steps[i].matterSetId);
                Assert.IsNotNull(matters, $"레벨 2 {i + 1}번째 단계의 블록 목록이 없음");
                CollectionAssert.AreEqual(Level2LaunchIds, matters.Select(m => m.id).ToArray(), $"레벨 2 {i + 1}번째 단계 블록 id가 Constants.RfidIds.Level2 순서와 달라짐");
            }
        }

        [Test]
        public void 레벨1과_레벨3_조건_블록의_value가_0보다_크다()
        {
            RfidLevelMapping level1 = GetMapping(1);
            foreach (string ingredientId in new[] { Constants.RfidIds.Level1.Engine, Constants.RfidIds.Level1.Payload, Constants.RfidIds.Level1.Fuel })
            {
                foreach (RfidMatter matter in GetStepMatters(level1, ingredientId))
                {
                    Assert.Greater(matter.value, 0, $"레벨 1 '{ingredientId}' 블록 '{matter.id}'의 value가 0 이하임");
                }
            }

            RfidLevelMapping level3 = GetMapping(3);
            foreach (string ingredientId in new[] { Constants.RfidIds.Level3.ElectricityCondition, Constants.RfidIds.Level3.OxygenCondition })
            {
                foreach (RfidMatter matter in GetStepMatters(level3, ingredientId))
                {
                    Assert.Greater(matter.value, 0, $"레벨 3 '{ingredientId}' 블록 '{matter.id}'의 value가 0 이하임");
                }
            }
        }

        [Test]
        public void 레벨4가_동작과_제어_재료를_모두_정의한다()
        {
            RfidLevelMapping mapping = GetMapping(4);

            RfidStepDefinition move = GetCategoryIngredient(mapping, Constants.RfidCategories.Action);
            Assert.AreEqual(Constants.RfidIds.Level4.Move, move.ingredientId, "동작 카드는 이동하기 재료여야 함");
            RfidMatter[] moves = mapping.FindMatters(move.matterSetId);
            Assert.IsNotNull(moves, "이동하기 블록 목록이 없음");
            Assert.IsNotEmpty(moves, "이동하기 블록 목록이 비어 있음");
            foreach (RfidMatter matter in moves)
            {
                CollectionAssert.Contains(Level4MoveIds, matter.id, $"이동하기 블록 '{matter.id}'는 알 수 없는 방향임");
            }

            RfidStepDefinition repeat = GetCategoryIngredient(mapping, Constants.RfidCategories.Control);
            Assert.AreEqual(Constants.RfidIds.Level4.Repeat, repeat.ingredientId, "제어 카드는 반복하기 재료여야 함");
            RfidMatter[] repeats = mapping.FindMatters(repeat.matterSetId);
            Assert.IsNotNull(repeats, "반복하기 블록 목록이 없음");
            Assert.IsNotEmpty(repeats, "반복하기 블록 목록이 비어 있음");
            foreach (RfidMatter matter in repeats)
            {
                Assert.GreaterOrEqual(matter.value, 1, $"반복하기 블록 '{matter.id}'의 반복 횟수(value)가 1 미만임");
            }

            // 보드는 이동하기만으로는 카드 MaxCards장 안에 못 풀고 RequiredRepeatCount회 반복하기로 풀리는 배치만 고름
            Assert.AreEqual(Constants.Level4Board.MaxCards, mapping.steps.Length, "레벨 4 단계 수는 보드 배치 기준 카드 수와 같아야 함");
            CollectionAssert.Contains(repeats.Select(m => m.value).ToArray(), Constants.Level4Board.RequiredRepeatCount,
                $"반복하기 블록에 보드 배치가 전제로 하는 {Constants.Level4Board.RequiredRepeatCount}회가 없음");
        }

        [Test]
        public void 레벨3_기준값_범위가_조건_블록_value에_모두_들어_있다()
        {
            LevelData level3Data = LoadLevelData(3);
            RfidLevelMapping mapping = GetMapping(3);
            int[] electricityValues = GetStepMatters(mapping, Constants.RfidIds.Level3.ElectricityCondition).Select(m => m.value).ToArray();
            int[] oxygenValues = GetStepMatters(mapping, Constants.RfidIds.Level3.OxygenCondition).Select(m => m.value).ToArray();

            Vector2Int electricityRange = level3Data.maxElectricityRange;
            Assert.LessOrEqual(electricityRange.x, electricityRange.y, "전기량 상한 범위의 최소가 최대보다 큼");
            for (int value = electricityRange.x; value <= electricityRange.y; value++)
            {
                CollectionAssert.Contains(electricityValues, value, $"전기량 상한 후보 {value}에 맞는 '만약 전기량이' 블록이 없음");
            }

            Vector2Int oxygenRange = level3Data.minOxygenRange;
            Assert.LessOrEqual(oxygenRange.x, oxygenRange.y, "산소량 하한 범위의 최소가 최대보다 큼");
            for (int value = oxygenRange.x; value <= oxygenRange.y; value++)
            {
                CollectionAssert.Contains(oxygenValues, value, $"산소량 하한 후보 {value}에 맞는 '만약 산소량이' 블록이 없음");
            }
        }

        [Test]
        public void 레벨1_목적지_거리를_엔진_연료_탑재_조합으로_만들_수_있다()
        {
            LevelData level1Data = LoadLevelData(1);
            Assert.IsNotNull(level1Data.destinations, "레벨 1 목적지가 없음");
            Assert.IsNotEmpty(level1Data.destinations, "레벨 1 목적지가 비어 있음");

            RfidLevelMapping mapping = GetMapping(1);
            RfidMatter[] engines = GetStepMatters(mapping, Constants.RfidIds.Level1.Engine);
            RfidMatter[] fuels = GetStepMatters(mapping, Constants.RfidIds.Level1.Fuel);
            RfidMatter[] payloads = GetStepMatters(mapping, Constants.RfidIds.Level1.Payload);

            HashSet<int> reachable = new HashSet<int>(
                from engine in engines
                from fuel in fuels
                from payload in payloads
                select engine.value + fuel.value - payload.value);

            foreach (MissionDestination destination in level1Data.destinations)
            {
                Assert.IsTrue(reachable.Contains(destination.targetDistance),
                    $"목적지 '{destination.planetName}'의 거리 {destination.targetDistance}를 엔진 + 연료 - 탑재 조합으로 만들 수 없음");
            }
        }

        [Test]
        public void 검증기는_실제_데이터에서_오류를_보고하지_않는다()
        {
            List<string> errors = RfidMappingValidator.Validate(_settings, LoadLevelData(1), LoadLevelData(3));
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void 검증기는_빈_블록_목록과_없는_참조를_찾아낸다()
        {
            RfidLevelMapping level2 = GetMapping(2);
            level2.matterSets[0].matters = new RfidMatter[0];
            RfidLevelMapping level3 = GetMapping(3);
            level3.steps.First(s => s.ingredientId == Constants.RfidIds.Level3.Electricity).matterSetId = "MissingSet";

            List<string> errors = RfidMappingValidator.Validate(_settings);

            AssertHasError(errors, "레벨 2", "'LaunchSequence'", "비어");
            AssertHasError(errors, "레벨 3", "'MissingSet'");
        }

        [Test]
        public void 검증기는_중복_id와_잘못된_value를_찾아낸다()
        {
            RfidLevelMapping level1 = GetMapping(1);
            RfidMatterSet fuelSet = level1.matterSets.First(s => s.id == Constants.RfidIds.Level1.Fuel);
            fuelSet.matters = fuelSet.matters.Append(new RfidMatter { id = fuelSet.matters[0].id, value = 1 }).ToArray();
            level1.matterSets.First(s => s.id == Constants.RfidIds.Level1.Engine).matters[0].value = 0;

            RfidLevelMapping level3 = GetMapping(3);
            level3.matterSets.First(s => s.id == Constants.RfidIds.Level3.ElectricityCondition).matters[0].value = 0;

            RfidLevelMapping level4 = GetMapping(4);
            level4.matterSets.First(s => s.id == Constants.RfidIds.Level4.Repeat).matters[0].value = 0;

            List<string> errors = RfidMappingValidator.Validate(_settings);

            AssertHasError(errors, "레벨 1", $"'{fuelSet.matters[0].id}'", "중복");
            AssertHasError(errors, "레벨 1 엔진", "value(0)");
            AssertHasError(errors, "레벨 3 전기량 조건", "value(0)");
            AssertHasError(errors, "레벨 4 반복하기", "value(0)");
        }

        [Test]
        public void 검증기는_레벨4_동작_제어_재료가_없으면_찾아낸다()
        {
            GetMapping(4).categoryIngredients = null;

            List<string> errors = RfidMappingValidator.Validate(_settings);

            AssertHasError(errors, "레벨 4", $"'{Constants.RfidCategories.Action}' 카드");
            AssertHasError(errors, "레벨 4", $"'{Constants.RfidCategories.Control}' 카드");
        }

        [Test]
        public void 검증기는_레벨4_카드_수나_반복_횟수가_보드_배치와_맞지_않으면_찾아낸다()
        {
            RfidLevelMapping level4 = GetMapping(4);
            level4.steps = level4.steps.Take(Constants.Level4Board.MaxCards - 1).ToArray();
            RfidMatterSet repeatSet = level4.matterSets.First(s => s.id == Constants.RfidIds.Level4.Repeat);
            repeatSet.matters = repeatSet.matters.Where(m => m.value != Constants.Level4Board.RequiredRepeatCount).ToArray();

            List<string> errors = RfidMappingValidator.Validate(_settings);

            AssertHasError(errors, "레벨 4 단계 수", $"{Constants.Level4Board.MaxCards}장");
            AssertHasError(errors, "레벨 4 반복하기", $"{Constants.Level4Board.RequiredRepeatCount}회");
        }

        [Test]
        public void 검증기는_도달할_수_없는_목적지와_맞지_않는_기준값_범위를_찾아낸다()
        {
            LevelData level1Data = ScriptableObject.CreateInstance<LevelData>();
            level1Data.destinations = new[] { new MissionDestination { planetName = "먼 행성", targetDistance = 100 } };
            LevelData level3Data = ScriptableObject.CreateInstance<LevelData>();
            level3Data.maxElectricityRange = new Vector2Int(3, 6);
            level3Data.minOxygenRange = new Vector2Int(2, 4);
            _createdObjects.Add(level1Data);
            _createdObjects.Add(level3Data);

            List<string> errors = RfidMappingValidator.Validate(_settings, level1Data, level3Data);

            AssertHasError(errors, "'먼 행성'", "거리 100");
            AssertHasError(errors, "maxElectricityRange", "6에 맞는");
            AssertHasError(errors, "minOxygenRange", "2에 맞는");
        }

        /// <summary> 모든 조각을 포함하는 오류가 하나라도 있는지 확인함. </summary>
        private static void AssertHasError(List<string> errors, params string[] fragments)
        {
            bool found = errors.Any(error => fragments.All(error.Contains));
            Assert.IsTrue(found, $"[{string.Join(", ", fragments)}]를 포함하는 오류가 없음. 실제 오류:\n{string.Join("\n", errors)}");
        }
    }
}
