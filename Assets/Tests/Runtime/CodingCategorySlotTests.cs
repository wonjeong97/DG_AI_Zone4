using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Game.Data;
using DGAIZone.Game.UI;
using DGAIZone.Game.UI.States;
using HuliacDev.Utils;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 게임 화면 CodingCategories 칸 검증: 레벨마다 단계별로 받을 카드 분류(칸 수·순서), 단계 분류에 맞는 카드만 받는 판정, 칸의 색/흑백·이름 표시.
    /// </summary>
    public class CodingCategorySlotTests
    {
        private const string Action = Constants.RfidCategories.Action;
        private const string Control = Constants.RfidCategories.Control;
        private const string Logic = Constants.RfidCategories.Logic;
        private const string Func = Constants.RfidCategories.Func;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _created.Clear();
        }

        /// <summary> 실제 RfidMappings.json의 level 정의를 적용한 컨트롤러를 만듦. </summary>
        private IngredientSelectionController CreateController(int level)
        {
            RfidSettings settings = JsonLoader.Load<RfidSettings>(Constants.Files.RfidMappings);
            Assert.IsNotNull(settings, "RfidMappings.json을 읽지 못함");

            GameObject go = new GameObject("TestCodingCategoryController");
            _created.Add(go);
            IngredientSelectionController controller = go.AddComponent<IngredientSelectionController>();
            controller.ApplyLevelMapping(settings.FindLevelMapping(level));
            return controller;
        }

        [Test]
        public void 레벨_1_2_3은_단계_정의대로_칸을_만든다()
        {
            CollectionAssert.AreEqual(new[] { Action, Action, Action }, new IngredientLevel1State().BuildStepCategories(CreateController(1)), "레벨 1은 동작 3칸");
            CollectionAssert.AreEqual(new[] { Action, Action, Action, Action, Action }, new IngredientLevel2State().BuildStepCategories(CreateController(2)), "레벨 2는 동작 5칸");
            CollectionAssert.AreEqual(new[] { Control, Action, Logic, Control, Action }, new IngredientLevel3State().BuildStepCategories(CreateController(3)), "레벨 3은 만약 > 동작 > 논리 > 만약 > 동작");
        }

        [Test]
        public void 레벨5는_함수_다음_동작_4칸이다()
        {
            CollectionAssert.AreEqual(new[] { Func, Action, Action, Action, Action }, new IngredientLevel5State().BuildStepCategories(CreateController(5)));
        }

        [Test]
        public void 제어_칸_이름은_레벨_3은_만약_레벨_4는_반복이다()
        {
            Assert.AreEqual("만약", CodingCategoryIndicatorController.LabelOf(Control, new IngredientLevel3State().ControlSlotLabel));
            Assert.AreEqual("반복", CodingCategoryIndicatorController.LabelOf(Control, new IngredientLevel4State().ControlSlotLabel));
            Assert.AreEqual(Action, CodingCategoryIndicatorController.LabelOf(Action, "반복"), "제어가 아닌 칸은 분류 이름");
            Assert.AreEqual(Control, CodingCategoryIndicatorController.LabelOf(Control, null), "제어 칸 이름이 없으면 분류 이름");
        }

        /// <summary> 레벨 4는 이번 판 배치의 AI 정답 경로 순서대로 칸을 만들고, 정답 블록(결과 화면 AI 설계창)과 칸이 하나씩 맞아야 함. </summary>
        [TestCase(0, 0, 1, 3, new[] { Control, Action, Control, Action }, TestName = "레벨4_칸은_정답_경로를_따른다_4칸_배치")]
        [TestCase(0, 2, 0, 1, new[] { Control, Action, Control, Action, Action }, TestName = "레벨4_칸은_정답_경로를_따른다_같은_방향_두_칸은_반복_2회")]
        [TestCase(0, 0, 0, 3, new[] { Action, Control, Action, Control, Action }, TestName = "레벨4_칸은_정답_경로를_따른다_반복을_더_쓰는_경로")]
        public void 레벨4_칸은_정답_경로를_따른다(int robotRow, int resourceRow, int trapRow, int hqRow, string[] expected)
        {
            IngredientSelectionController controller = CreateController(4);
            GameObject boardGo = new GameObject("TestLevel4Board");
            _created.Add(boardGo);
            Level4BoardController board = boardGo.AddComponent<Level4BoardController>();
            board.SetPlacementForTest(robotRow, resourceRow, trapRow, hqRow);
            controller.SetLevel4BoardForTest(board);

            IngredientLevel4State state = new IngredientLevel4State();
            string[] categories = state.BuildStepCategories(controller);
            CollectionAssert.AreEqual(expected, categories);

            string[] solutionCategories = state.BuildSolution(controller).Select(s => s.ingredient.categories[0]).ToArray();
            CollectionAssert.AreEqual(solutionCategories, categories, "칸은 결과 화면 AI 정답 블록과 같은 순서여야 함");
        }

        [Test]
        public void 레벨4_정답은_가장_적은_장수_중_반복하기를_가장_많이_쓴다()
        {
            Assert.IsNotEmpty(Level4Rules.PlacementPool, "배치 후보가 비어 있으면 아무것도 확인하지 않고 통과함");
            foreach (Level4Layout layout in Level4Rules.PlacementPool)
            {
                List<(int moves, string directionId)> path = Level4Rules.FindShortestSolution(layout, Constants.Level4Board.MaxCards, Constants.Level4Board.AnswerRepeatCounts);
                Assert.IsNotNull(path, $"{layout}: 정답 경로가 없음");
                Assert.IsTrue(Solves(layout, path), $"{layout}: 정답 경로가 마지막 칸에서 기지에 정확히 닿지 않음");

                // 탐색 코드와 따로, 카드 제한 안의 모든 구간 조합을 걸어 보아 정답인 조합의 (장수, 반복하기 수)를 모음
                List<(int cards, int repeats)> found = new List<(int cards, int repeats)>();
                EnumerateSolutions(layout, new List<(int moves, string directionId)>(), 0, found);
                int fewestCards = found.Min(f => f.cards);
                int mostRepeats = found.Where(f => f.cards == fewestCards).Max(f => f.repeats);

                Assert.AreEqual(fewestCards, path.Sum(s => s.moves > 1 ? 2 : 1), $"{layout}: 가장 적은 장수가 아님");
                Assert.AreEqual(mostRepeats, path.Count(s => s.moves > 1), $"{layout}: 같은 장수 중 반복하기를 가장 많이 쓰지 않음");
            }
        }

        // 이동하기 한 장, AI 정답이 쓰는 반복 횟수마다 반복하기+이동하기 두 장
        private static readonly (int moves, int cost)[] SegmentOptions =
            new[] { (1, 1) }.Concat(Constants.Level4Board.AnswerRepeatCounts.Select(count => (count, 2))).ToArray();

        private static readonly string[] Level4Directions =
        {
            Constants.RfidIds.Level4.MoveUp, Constants.RfidIds.Level4.MoveDown, Constants.RfidIds.Level4.MoveRight, Constants.RfidIds.Level4.MoveLeft
        };

        /// <summary> 한 칸 이동(1장)·반복 이동(2장) 구간을 네 방향으로 카드 제한까지 이어 붙이며, 정답(Solves)인 조합의 장수와 반복하기 수를 found에 담음. </summary>
        private static void EnumerateSolutions(Level4Layout layout, List<(int moves, string directionId)> segments, int cards, List<(int cards, int repeats)> found)
        {
            if (segments.Count > 0 && Solves(layout, segments)) found.Add((cards, segments.Count(s => s.moves > 1)));

            foreach (string direction in Level4Directions)
            {
                foreach ((int moves, int cost) in SegmentOptions)
                {
                    if (cards + cost > Constants.Level4Board.MaxCards) continue;
                    segments.Add((moves, direction));
                    EnumerateSolutions(layout, segments, cards + cost, found);
                    segments.RemoveAt(segments.Count - 1);
                }
            }
        }

        /// <summary> 구간을 Step으로 한 칸씩 걸어, 그리드 밖·함정 없이 마지막 한 칸에서 자원을 모은 채 기지에 닿으면 정답(기지에 더 일찍 닿으면 정답 아님). </summary>
        private static bool Solves(Level4Layout layout, List<(int moves, string directionId)> segments)
        {
            int column = Level4Rules.RobotColumn;
            int row = layout.RobotRow;
            bool collected = false;
            int totalSteps = segments.Sum(s => s.moves);
            int taken = 0;

            foreach ((int moves, string directionId) in segments)
            {
                if (!Level4Rules.TryGetMoveStep(directionId, out Level4MoveStep step)) Assert.Fail($"알 수 없는 방향 {directionId}");
                for (int i = 0; i < moves; i++)
                {
                    taken++;
                    Level4StepResult result = Level4Rules.Step(layout, ref column, ref row, ref collected, step);
                    if (result == Level4StepResult.OutOfBounds || result == Level4StepResult.Trap) return false;
                    if (result == Level4StepResult.Hq) return collected && taken == totalSteps;
                }
            }

            return false;
        }

        [Test]
        public void 레벨4_모든_배치는_4칸이나_5칸이고_반복_다음은_동작이다()
        {
            Assert.IsNotEmpty(Level4Rules.PlacementPool, "배치 후보가 비어 있으면 아무것도 확인하지 않고 통과함");
            foreach (Level4Layout layout in Level4Rules.PlacementPool)
            {
                List<(int moves, string directionId)> path = Level4Rules.FindShortestSolution(layout, Constants.Level4Board.MaxCards, Constants.Level4Board.AnswerRepeatCounts);
                Assert.IsNotNull(path, $"{layout}: 정답 경로가 없음");

                string[] categories = IngredientLevel4State.StepCategoriesOf(path);
                Assert.That(categories.Length, Is.InRange(4, Constants.Level4Board.MaxCards), $"{layout}: 칸 수");
                Assert.Contains(Control, categories, $"{layout}: 반복하기를 꼭 쓰는 배치");
                for (int i = 0; i < categories.Length; i++)
                {
                    if (categories[i] == Control) Assert.AreEqual(Action, i + 1 < categories.Length ? categories[i + 1] : null, $"{layout}: {i + 1}번째 반복 칸 다음은 동작");
                }
            }
        }

        /// <summary> 레벨 4 단계 정의는 1~4단계가 동작·제어를 모두 받으므로, 단계별 분류가 틀린 분류 카드를 막는 유일한 판정임. </summary>
        [Test]
        public void 단계_분류와_같은_카드만_받는다()
        {
            RfidStepDefinition[] steps = JsonLoader.Load<RfidSettings>(Constants.Files.RfidMappings).FindLevelMapping(4).steps;
            string[] categories = { Control, Action, Action, Action, Action };

            Assert.IsTrue(IngredientSelectionController.AcceptsStepCard(steps[0], categories, 0, Control), "1단계 제어 칸에 제어 카드");
            Assert.IsFalse(IngredientSelectionController.AcceptsStepCard(steps[0], categories, 0, Action), "단계 정의는 동작도 받지만 1단계 칸이 제어라 경고");
            Assert.IsFalse(IngredientSelectionController.AcceptsStepCard(steps[1], categories, 1, Control), "2단계 동작 칸에 제어 카드는 경고");
            Assert.IsTrue(IngredientSelectionController.AcceptsStepCard(steps[0], null, 0, Action), "분류를 정하지 못했으면 단계 정의로만 판정");
            Assert.IsFalse(IngredientSelectionController.AcceptsStepCard(steps[4], null, 4, Control), "분류가 없어도 단계 정의가 받지 않는 카드는 경고");
            Assert.IsTrue(IngredientSelectionController.MatchesStepCategory(categories, 5, Logic), "칸이 없는 단계는 단계 정의로만 판정");
        }

        /// <summary> 테스트용 칸 표시기와 선택 레벨을 넣은 컨트롤러에 실제 RfidMappings.json을 적용함(게임 씬이 설정을 읽은 뒤와 같은 경로). </summary>
        private IngredientSelectionController ApplySettings(int level, Level4BoardController board, CodingCategoryIndicatorController indicator)
        {
            GameObject go = new GameObject("TestCodingCategoryController");
            _created.Add(go);
            IngredientSelectionController controller = go.AddComponent<IngredientSelectionController>();
            controller.Construct(null, null, new SelectedLevelStore { SelectedLevel = level }, null, null, indicator, null, null, null, null, null);
            if (board) controller.SetLevel4BoardForTest(board);

            controller.ApplyRfidSettings(JsonLoader.Load<RfidSettings>(Constants.Files.RfidMappings));
            return controller;
        }

        [Test]
        public void 레벨4_정답이_4장인_판은_4단계_4칸으로_진행한다()
        {
            GameObject boardGo = new GameObject("TestLevel4Board");
            _created.Add(boardGo);
            Level4BoardController board = boardGo.AddComponent<Level4BoardController>();
            board.SetPlacementForTest(0, 0, 1, 3);
            CodingCategoryIndicatorController indicator = CreateIndicator(out CodingCategoryIndicatorController.StepSlot[] views);

            IngredientSelectionController controller = ApplySettings(4, board, indicator);

            Assert.AreEqual(4, controller.TotalSteps, "단계 정의는 5개지만 이번 판 정답이 4장");
            CollectionAssert.AreEqual(new[] { Control, Action, Control, Action }, controller.StepCategories);
            Assert.AreEqual(4, indicator.VisibleSlotCount);
            Assert.AreEqual("반복", views[0].label.text);
        }

        [Test]
        public void 레벨4_보드가_없으면_단계_정의대로_5단계이고_칸을_숨긴다()
        {
            CodingCategoryIndicatorController indicator = CreateIndicator(out CodingCategoryIndicatorController.StepSlot[] views);

            IngredientSelectionController controller = ApplySettings(4, null, indicator);

            Assert.IsNull(controller.StepCategories, "정답 경로를 알 수 없음");
            Assert.AreEqual(5, controller.TotalSteps, "단계 정의 개수로 진행");
            Assert.AreEqual(0, indicator.VisibleSlotCount);
            Assert.IsFalse(views.Any(v => v.root.activeSelf), "칸을 모두 끔");
        }

        /// <summary> 칸 5개를 씬 없이 만들어 넣은 칸 표시기를 만듦. </summary>
        private CodingCategoryIndicatorController CreateIndicator(out CodingCategoryIndicatorController.StepSlot[] views)
        {
            GameObject go = new GameObject("TestCodingCategories", typeof(RectTransform));
            _created.Add(go);
            CodingCategoryIndicatorController indicator = go.AddComponent<CodingCategoryIndicatorController>();

            views = new CodingCategoryIndicatorController.StepSlot[5];
            for (int i = 0; i < views.Length; i++)
            {
                GameObject root = new GameObject($"Slot_{i + 1}", typeof(RectTransform));
                root.transform.SetParent(go.transform, false);
                GameObject color = new GameObject("Image_Color", typeof(RectTransform), typeof(Image));
                color.transform.SetParent(root.transform, false);
                GameObject label = new GameObject("Text_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(color.transform, false);
                GameObject overlay = new GameObject("Image_GrayscaleOverlay", typeof(RectTransform), typeof(Image));
                overlay.transform.SetParent(root.transform, false);

                views[i] = new CodingCategoryIndicatorController.StepSlot
                {
                    root = root,
                    colorImage = color.GetComponent<Image>(),
                    label = label.GetComponent<TextMeshProUGUI>(),
                    grayscaleOverlay = overlay.GetComponent<Image>()
                };
            }

            indicator.SetSlotViewsForTest(views);
            return indicator;
        }

        /// <summary> 보이는 칸마다 색(true)/흑백(false)인지. 오버레이 알파 0이면 색. </summary>
        private static bool[] Colored(CodingCategoryIndicatorController.StepSlot[] views, int count)
        {
            return views.Take(count).Select(v => v.grayscaleOverlay.GetComponent<CanvasGroup>().alpha < 0.5f).ToArray();
        }

        /// <summary> 보이는 칸마다 안내 깜빡임 트윈이 걸려 있는지. </summary>
        private static bool[] Hinting(CodingCategoryIndicatorController.StepSlot[] views, int count)
        {
            return views.Take(count).Select(v => DOTween.IsTweening(v.grayscaleOverlay.GetComponent<CanvasGroup>())).ToArray();
        }

        [Test]
        public void 단계_수만큼_칸을_보여_주고_이름을_붙인다()
        {
            CodingCategoryIndicatorController indicator = CreateIndicator(out CodingCategoryIndicatorController.StepSlot[] views);

            indicator.SetSlots(new[] { Control, Action, Logic }, "만약");

            Assert.AreEqual(3, indicator.VisibleSlotCount);
            CollectionAssert.AreEqual(new[] { true, true, true, false, false }, views.Select(v => v.root.activeSelf).ToArray(), "앞 3칸만 보임");
            CollectionAssert.AreEqual(new[] { "만약", Action, Logic }, views.Take(3).Select(v => v.label.text).ToArray());
            Assert.AreNotEqual(views[0].colorImage.color, views[1].colorImage.color, "분류마다 색이 다름");
            CollectionAssert.AreEqual(new[] { false, false, false }, Colored(views, 3), "처음에는 모두 흑백");

            indicator.SetSlots(null, null);
            Assert.AreEqual(0, indicator.VisibleSlotCount);
            Assert.IsFalse(views.Any(v => v.root.activeSelf), "분류를 정하지 못하면 칸을 모두 숨김");
        }

        [Test]
        public void 설정한_칸은_색으로_남고_지금_칸은_카드를_올리면_색이_된다()
        {
            CodingCategoryIndicatorController indicator = CreateIndicator(out CodingCategoryIndicatorController.StepSlot[] views);
            indicator.SetSlots(new[] { Action, Action, Action }, null);

            indicator.ShowStep(0);
            CollectionAssert.AreEqual(new[] { false, false, false }, Colored(views, 3), "1단계 대기");
            CollectionAssert.AreEqual(new[] { true, false, false }, Hinting(views, 3), "1단계 칸만 안내");

            indicator.HighlightStep(0);
            CollectionAssert.AreEqual(new[] { true, false, false }, Colored(views, 3), "1단계 카드 인식");
            CollectionAssert.AreEqual(new[] { false, false, false }, Hinting(views, 3), "카드가 오면 안내를 멈춤");

            indicator.ShowStep(1);
            CollectionAssert.AreEqual(new[] { true, false, false }, Colored(views, 3), "1단계 설정 뒤 2단계 대기");
            CollectionAssert.AreEqual(new[] { false, true, false }, Hinting(views, 3), "2단계 칸만 안내");

            indicator.HighlightStep(1);
            CollectionAssert.AreEqual(new[] { true, true, false }, Colored(views, 3), "2단계 카드 인식");

            indicator.ShowStep(1);
            CollectionAssert.AreEqual(new[] { true, false, false }, Colored(views, 3), "2단계 카드를 떼거나 취소하면 다시 흑백");

            indicator.ShowStep(0);
            CollectionAssert.AreEqual(new[] { false, false, false }, Colored(views, 3), "1단계로 되돌리면 1단계 칸도 흑백");

            indicator.ShowStep(3);
            CollectionAssert.AreEqual(new[] { true, true, true }, Colored(views, 3), "모든 단계를 마치면 모두 색");
            CollectionAssert.AreEqual(new[] { false, false, false }, Hinting(views, 3), "모든 단계를 마치면 안내 없음");
        }
    }
}
