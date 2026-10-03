using System.Collections.Generic;
using System.Linq;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using DGAIZone.Game.UI;
using DGAIZone.Game.UI.States;
using HuliacDev.Utils;
using NUnit.Framework;
using UnityEngine;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 레벨 상태의 BuildSolution(결과 씬 AI 설계창에 쓰는 정답)이 실제 StreamingAssets/RfidMappings.json 블록으로 만들어지고,
    /// 그 정답을 그대로 확정하면 게임의 판정(EvaluateMission/EvaluateOutcome)이 성공을 내는지 검증하는 테스트.
    /// </summary>
    public class SolutionDesignTests
    {
        private GameObject _go;
        private IngredientSelectionController _controller;
        private RfidSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestSolutionDesign");
            _controller = _go.AddComponent<IngredientSelectionController>();
            _settings = JsonLoader.Load<RfidSettings>(Constants.Files.RfidMappings);
            Assert.IsNotNull(_settings, "RfidMappings.json을 읽지 못함");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        /// <summary> 컨트롤러에 실제 JSON의 레벨 정의(블록 목록·단계·분류별 재료)를 넣음. </summary>
        private void UseLevelMapping(int level)
        {
            RfidLevelMapping mapping = _settings.FindLevelMapping(level);
            Assert.IsNotNull(mapping, $"레벨 {level} 정의가 없음");
            _controller.ApplyLevelMapping(mapping);
        }

        /// <summary> 컨트롤러에 미션 보드를 붙여 넣고 반환함. </summary>
        private MissionBoardController UseMissionBoard()
        {
            MissionBoardController board = _go.AddComponent<MissionBoardController>();
            InjectDependencies(board, null);
            return board;
        }

        /// <summary> 테스트에 필요한 의존성(미션 보드, 결과 저장소)만 컨트롤러에 주입하고 나머지는 비워 둠. </summary>
        private void InjectDependencies(MissionBoardController board, GameResultStore resultStore)
        {
            _controller.Construct(null, null, null, null, board, null, resultStore, null, null, null, null);
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

        /// <summary> 정답 블록을 단계 순서대로 확정함(레벨 상태의 OnStepConfirmed). </summary>
        private void ConfirmAll(IIngredientSelectionLevelState state, List<(RfidStepDefinition ingredient, RfidMatter matter)> solution)
        {
            for (int i = 0; i < solution.Count; i++)
            {
                state.OnStepConfirmed(_controller, i, solution[i].ingredient.ingredientId, solution[i].matter);
            }
        }

        [Test]
        public void 레벨1_모든_목적지에서_무작위로_고른_정답을_확정하면_성공한다()
        {
            const int Draws = 30;
            UseLevelMapping(1);
            MissionBoardController board = UseMissionBoard();
            MissionDestination[] destinations = LoadLevelData(1).destinations;
            Assert.IsNotEmpty(destinations, "Level1 LevelData에 목적지가 없음");

            RfidLevelMapping mapping = _settings.FindLevelMapping(1);
            RfidMatter[] engines = mapping.FindMatters("Engine");
            RfidMatter[] payloads = mapping.FindMatters("Payload");
            RfidMatter[] fuels = mapping.FindMatters("Fuel");

            Random.State savedState = Random.state;
            Random.InitState(1234); // 무작위 선택을 매번 같게 재현해 테스트가 흔들리지 않게 함
            try
            {
                foreach (MissionDestination destination in destinations)
                {
                    board.SetTargetDistanceForTest(destination.targetDistance);
                    int combinationCount = engines.Sum(e => payloads.Sum(p => fuels.Count(f => e.value + f.value - p.value == destination.targetDistance)));
                    HashSet<string> seen = new HashSet<string>();

                    for (int i = 0; i < Draws; i++)
                    {
                        IngredientLevel1State state = new IngredientLevel1State();
                        List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = state.BuildSolution(_controller);

                        Assert.AreEqual(3, solution.Count, $"{destination.planetName}(거리 {destination.targetDistance}): 엔진·탑재·연료 3단계 정답이 나와야 함");
                        CollectionAssert.AreEqual(_controller.StepDefinitions.Select(s => s.ingredientId), solution.Select(s => s.ingredient.ingredientId), "정답은 단계 순서대로여야 함");
                        ConfirmAll(state, solution);
                        Assert.IsTrue(state.EvaluateMission(_controller), $"{destination.planetName}(거리 {destination.targetDistance}) 정답을 확정했는데 실패 판정이 남");
                        seen.Add(string.Join(",", solution.Select(s => s.matter.id)));
                    }

                    if (combinationCount > 1)
                    {
                        Assert.Greater(seen.Count, 1, $"{destination.planetName}(거리 {destination.targetDistance}): 정답 조합이 {combinationCount}가지인데 {Draws}번 모두 같은 조합만 나옴");
                    }
                }
            }
            finally
            {
                Random.state = savedState;
            }
        }

        [Test]
        public void 레벨2_정답은_발사_순서이고_확정하면_성공한다()
        {
            UseLevelMapping(2);
            IngredientLevel2State state = new IngredientLevel2State();

            List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = state.BuildSolution(_controller);

            string[] expected =
            {
                Constants.RfidIds.Level2.Ignite,
                Constants.RfidIds.Level2.Ascend,
                Constants.RfidIds.Level2.SeparateStage1,
                Constants.RfidIds.Level2.SeparateStage2,
                Constants.RfidIds.Level2.EnterOrbit
            };
            CollectionAssert.AreEqual(expected, solution.Select(s => s.matter.id), "레벨 2 정답은 올바른 발사 순서여야 함");

            _controller.SetConfirmedMattersForTest(solution.Select(s => s.matter).ToArray());
            Assert.IsTrue(state.EvaluateMission(_controller), "레벨 2 정답을 확정했는데 실패 판정이 남");
        }

        [Test]
        public void 레벨3_모든_기준값에서_정답을_확정하면_성공한다()
        {
            UseLevelMapping(3);
            MissionBoardController board = UseMissionBoard();
            LevelData level3 = LoadLevelData(3);

            for (int maxElectricity = level3.maxElectricityRange.x; maxElectricity <= level3.maxElectricityRange.y; maxElectricity++)
            {
                for (int minOxygen = level3.minOxygenRange.x; minOxygen <= level3.minOxygenRange.y; minOxygen++)
                {
                    board.SetLevel3LimitsForTest(maxElectricity, minOxygen);
                    IngredientLevel3State state = new IngredientLevel3State();

                    List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = state.BuildSolution(_controller);

                    Assert.AreEqual(_controller.StepDefinitions.Length, solution.Count, $"전기량 상한 {maxElectricity}, 산소량 하한 {minOxygen}: 모든 단계의 정답이 나와야 함");
                    ConfirmAll(state, solution);
                    Assert.IsTrue(state.EvaluateMission(_controller), $"전기량 상한 {maxElectricity}, 산소량 하한 {minOxygen} 정답을 확정했는데 실패 판정이 남");
                }
            }
        }

        [Test]
        public void 레벨4_모든_배치에서_정답은_카드_제한_안에서_반복하기를_쓰고_성공한다()
        {
            UseLevelMapping(4);
            Level4BoardController board = _go.AddComponent<Level4BoardController>();
            _controller.SetLevel4BoardForTest(board);
            IngredientLevel4State state = new IngredientLevel4State();

            IReadOnlyList<Level4Layout> pool = Level4Rules.PlacementPool;
            Assert.IsNotEmpty(pool, "레벨 4 배치 후보가 없음");

            foreach (Level4Layout layout in pool)
            {
                board.SetPlacementForTest(layout.RobotRow, layout.ResourceRow, layout.TrapRow, layout.HqRow);

                List<(RfidStepDefinition ingredient, RfidMatter matter)> solution = state.BuildSolution(_controller);

                Assert.IsNotEmpty(solution, $"{layout}: 정답이 나와야 함");
                Assert.LessOrEqual(solution.Count, Constants.Level4Board.MaxCards, $"{layout}: 정답이 카드 {Constants.Level4Board.MaxCards}장을 넘음");
                Assert.IsTrue(solution.Any(s => s.ingredient.ingredientId == Constants.RfidIds.Level4.Repeat), $"{layout}: 반복하기를 써야 풀리는 배치인데 정답에 반복하기가 없음");
                for (int i = 0; i < solution.Count; i++)
                {
                    if (solution[i].ingredient.ingredientId != Constants.RfidIds.Level4.Repeat) continue;
                    Assert.IsTrue(i + 1 < solution.Count && solution[i + 1].ingredient.ingredientId == Constants.RfidIds.Level4.Move, $"{layout}: 반복하기 다음에는 이동하기가 와야 함");
                }

                List<(string, RfidMatter)> commands = solution.Select(s => (s.ingredient.ingredientId, s.matter)).ToList();
                Assert.IsTrue(board.EvaluateOutcome(commands), $"{layout}: 정답을 입력했는데 실패 판정이 남");
            }
        }

        [Test]
        public void 정답_설계는_설계창_형식의_문구로_결과_저장소에_기록된다()
        {
            UseLevelMapping(2);
            GameResultStore store = new GameResultStore();
            InjectDependencies(null, store);
            _controller.ChangeLevelState(2);
            IngredientLevel2State level2State = (IngredientLevel2State)_controller.CurrentLevelState;

            _controller.StoreSolutionDesign();

            Assert.AreEqual(5, store.SolutionDesignItems.Count, "레벨 2 정답 5줄이 기록돼야 함");
            RfidMatter first = _settings.FindLevelMapping(2).FindMatters("LaunchSequence").First(m => m.id == Constants.RfidIds.Level2.Ignite);
            Assert.AreEqual(level2State.FormatDesignItemText(_controller, "발사 코딩 순서", first.label), store.SolutionDesignItems[0], "첫 줄은 게임 설계창과 같은 형식의 '엔진 점화' 블록이어야 함");
        }
    }
}
