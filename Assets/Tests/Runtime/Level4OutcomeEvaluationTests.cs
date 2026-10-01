using System.Collections.Generic;
using DGAIZone.App;
using DGAIZone.Game.Data;
using DGAIZone.Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace DGAIZone.Tests
{
    /// <summary>
    /// Level4BoardController.EvaluateOutcome의 블록 코딩 경로 판정 알고리즘 검증 테스트.
    /// </summary>
    public class Level4OutcomeEvaluationTests
    {
        private GameObject _go;
        private Level4BoardController _board;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestLevel4Board");
            _board = _go.AddComponent<Level4BoardController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        /// <summary> 이동하기 명령 하나를 만듦(방향은 물질 id로 판정됨). </summary>
        private static (string, RfidMatter) Move(string directionId) =>
            (Constants.RfidIds.Level4.Move, new RfidMatter { id = directionId });

        /// <summary> 반복하기 명령 하나를 만듦(반복 횟수는 물질 value로 판정됨). </summary>
        private static (string, RfidMatter) Repeat(int count) =>
            (Constants.RfidIds.Level4.Repeat, new RfidMatter { id = "Repeat" + count, value = count });

        /// <summary>
        /// 자원을 먼저 수집한 뒤 기지에 도착하면 성공(true)을 반환해야 함.
        /// </summary>
        [Test]
        public void 자원수집_후_기지_도착_시_성공한다()
        {
            // 배치: 로봇(R0, C0), 자원(R0, C1), 함정(R2, C2), 기지(R1, C3)
            _board.SetPlacementForTest(robotRow: 0, resourceRow: 0, trapRow: 2, hqRow: 1);

            List<(string, RfidMatter)> commands = new List<(string, RfidMatter)>
            {
                Move(Constants.RfidIds.Level4.MoveRight), // (0,1) 자원 획득
                Move(Constants.RfidIds.Level4.MoveRight), // (0,2)
                Move(Constants.RfidIds.Level4.MoveDown),  // (1,2)
                Move(Constants.RfidIds.Level4.MoveRight), // (1,3) 기지 도착
            };

            bool result = _board.EvaluateOutcome(commands);
            Assert.IsTrue(result, "자원 수집 후 기지에 도착했으나 성공 판정이 나지 않음");
        }

        /// <summary>
        /// 자원을 수집하지 않고 기지에 도착하면 실패(false)해야 함.
        /// </summary>
        [Test]
        public void 자원_미수집_상태로_기지_도착_시_실패한다()
        {
            // 배치: 로봇(R0, C0), 자원(R0, C1), 함정(R2, C2), 기지(R1, C3)
            _board.SetPlacementForTest(robotRow: 0, resourceRow: 0, trapRow: 2, hqRow: 1);

            List<(string, RfidMatter)> commands = new List<(string, RfidMatter)>
            {
                Move(Constants.RfidIds.Level4.MoveDown),  // (1,0)
                Move(Constants.RfidIds.Level4.MoveRight), // (1,1) 자원(0,1) 우회
                Move(Constants.RfidIds.Level4.MoveRight), // (1,2)
                Move(Constants.RfidIds.Level4.MoveRight), // (1,3) 기지 도착
            };

            bool result = _board.EvaluateOutcome(commands);
            Assert.IsFalse(result, "자원을 획득하지 않았는데 성공 처리됨");
        }

        /// <summary>
        /// 이동 중 함정 셀에 도달하면 즉시 실패(false)해야 함.
        /// </summary>
        [Test]
        public void 함정_셀_도착_시_실패한다()
        {
            // 배치: 로봇(R0, C0), 자원(R0, C1), 함정(R2, C2), 기지(R1, C3)
            _board.SetPlacementForTest(robotRow: 0, resourceRow: 0, trapRow: 2, hqRow: 1);

            List<(string, RfidMatter)> commands = new List<(string, RfidMatter)>
            {
                Move(Constants.RfidIds.Level4.MoveDown),  // (1,0)
                Move(Constants.RfidIds.Level4.MoveDown),  // (2,0)
                Move(Constants.RfidIds.Level4.MoveRight), // (2,1)
                Move(Constants.RfidIds.Level4.MoveRight), // (2,2) 함정!
            };

            bool result = _board.EvaluateOutcome(commands);
            Assert.IsFalse(result, "함정에 도착했으나 실패로 판정되지 않음");
        }

        /// <summary>
        /// 그리드 경계를 벗어나면 즉시 실패(false)해야 함.
        /// </summary>
        [Test]
        public void 보드_경계_이탈_시_실패한다()
        {
            // 로봇 시작 행이 0인데 위쪽으로 이동
            _board.SetPlacementForTest(robotRow: 0, resourceRow: 1, trapRow: 2, hqRow: 3);

            List<(string, RfidMatter)> commands = new List<(string, RfidMatter)>
            {
                Move(Constants.RfidIds.Level4.MoveUp) // (-1, 0) 그리드 밖!
            };

            bool result = _board.EvaluateOutcome(commands);
            Assert.IsFalse(result, "경계를 벗어났으나 실패로 판정되지 않음");
        }

        /// <summary>
        /// 반복하기 명령이 다음 이동하기와 정상적으로 연계되어 횟수만큼 이동하는지 검증.
        /// </summary>
        [Test]
        public void 반복하기_명령이_정상_연계된다()
        {
            // 배치: 로봇(R0, C0), 자원(R0, C2 아님 - C1), 기지(R0 불가이므로 R1, C3), 함정(R3, C2)
            _board.SetPlacementForTest(robotRow: 0, resourceRow: 0, trapRow: 3, hqRow: 1);

            List<(string, RfidMatter)> commands = new List<(string, RfidMatter)>
            {
                Repeat(2),
                Move(Constants.RfidIds.Level4.MoveRight), // (0,1)->자원, (0,2)
                Move(Constants.RfidIds.Level4.MoveDown),  // (1,2)
                Move(Constants.RfidIds.Level4.MoveRight), // (1,3)->기지
            };

            bool result = _board.EvaluateOutcome(commands);
            Assert.IsTrue(result, "반복하기(2회)+오른쪽으로 2칸 전진하여 자원 획득 후 기지 도착해야 함");
        }

        /// <summary>
        /// 모든 명령을 수행했으나 기지에 닿지 못하면 실패(false)해야 함.
        /// </summary>
        [Test]
        public void 명령_소진_후_기지_미도착_시_실패한다()
        {
            _board.SetPlacementForTest(robotRow: 0, resourceRow: 0, trapRow: 2, hqRow: 1);

            List<(string, RfidMatter)> commands = new List<(string, RfidMatter)>
            {
                Move(Constants.RfidIds.Level4.MoveRight) // (0,1) 자원만 먹고 정지
            };

            bool result = _board.EvaluateOutcome(commands);
            Assert.IsFalse(result, "기지에 미도착했으나 실패로 판정되지 않음");
        }

        private static readonly string[] Directions =
        {
            Constants.RfidIds.Level4.MoveUp,
            Constants.RfidIds.Level4.MoveDown,
            Constants.RfidIds.Level4.MoveRight,
            Constants.RfidIds.Level4.MoveLeft
        };

        /// <summary>
        /// 보드가 고르는 배치 후보마다 실제 판정(EvaluateOutcome)으로 카드 조합을 모두 따져, 이동하기만으로는 카드 5장 안에 성공할 수 없고
        /// '반복하기(3회) + 이동하기'를 쓰면 5장 안에 성공할 수 있어야 함(제어 블록을 쓰지 않아도 풀리던 문제의 회귀 방지).
        /// </summary>
        [Test]
        public void 모든_배치는_반복하기_없이는_못_풀고_반복하기로는_풀린다()
        {
            List<Level4BoardController.Level4Layout> pool = Level4BoardController.BuildPlacementPool();
            Assert.IsNotEmpty(pool, "반복하기를 써야만 풀리는 배치가 하나도 없음");

            int maxCards = Constants.Level4Board.MaxCards;
            foreach (Level4BoardController.Level4Layout layout in pool)
            {
                _board.SetPlacementForTest(layout.RobotRow, layout.ResourceRow, layout.TrapRow, layout.HqRow);
                Assert.IsFalse(CanClear(new List<(string, RfidMatter)>(), maxCards, allowRepeat: false), $"{layout}: 이동하기만으로 {maxCards}장 안에 풀림");
                Assert.IsTrue(CanClear(new List<(string, RfidMatter)>(), maxCards, allowRepeat: true), $"{layout}: 반복하기를 써도 {maxCards}장 안에 못 풂");
            }
        }

        /// <summary> 지금까지의 명령 뒤에 카드를 cardsLeft장까지 더 붙여 보며, 실제 판정(EvaluateOutcome)으로 성공하는 조합이 있는지 찾음. </summary>
        private bool CanClear(List<(string, RfidMatter)> commands, int cardsLeft, bool allowRepeat)
        {
            if (commands.Count > 0 && _board.EvaluateOutcome(commands)) return true;

            foreach (string direction in Directions)
            {
                if (cardsLeft >= 1)
                {
                    commands.Add(Move(direction));
                    bool found = CanClear(commands, cardsLeft - 1, allowRepeat);
                    commands.RemoveAt(commands.Count - 1);
                    if (found) return true;
                }

                if (allowRepeat && cardsLeft >= 2)
                {
                    commands.Add(Repeat(Constants.Level4Board.RequiredRepeatCount));
                    commands.Add(Move(direction));
                    bool found = CanClear(commands, cardsLeft - 2, allowRepeat);
                    commands.RemoveRange(commands.Count - 2, 2);
                    if (found) return true;
                }
            }

            return false;
        }
    }
}
