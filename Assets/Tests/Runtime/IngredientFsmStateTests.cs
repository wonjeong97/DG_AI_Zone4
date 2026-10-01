using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using DGAIZone.Game.UI;
using DGAIZone.Game.UI.States;
using HuliacDev.Core;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DGAIZone.Tests
{
    /// <summary>
    /// IngredientSelectionController의 FSM 상태 및 전이 검증 테스트.
    /// </summary>
    public class IngredientFsmStateTests
    {
        private GameObject _go;
        private IngredientSelectionController _controller;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestIngredientController");
            _controller = _go.AddComponent<IngredientSelectionController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        /// <summary>
        /// StateMachine을 통해 레벨 1~4 상태 인스턴스가 올바르게 전이되는지 검증.
        /// </summary>
        [Test]
        public void 상태머신_레벨_전이가_정상_동작한다()
        {
            IngredientLevel1State l1 = new IngredientLevel1State();
            IngredientLevel2State l2 = new IngredientLevel2State();
            IngredientLevel3State l3 = new IngredientLevel3State();
            IngredientLevel4State l4 = new IngredientLevel4State();

            StateMachine<IngredientSelectionController> sm = new StateMachine<IngredientSelectionController>(_controller, l1);

            Assert.AreSame(l1, sm.CurrentState);

            sm.ChangeState(l2);
            Assert.AreSame(l2, sm.CurrentState);
            Assert.AreSame(l1, sm.PreviousState);

            sm.ChangeState(l3);
            Assert.AreSame(l3, sm.CurrentState);
            Assert.AreSame(l2, sm.PreviousState);

            sm.ChangeState(l4);
            Assert.AreSame(l4, sm.CurrentState);
            Assert.AreSame(l3, sm.PreviousState);

            sm.Dispose();
        }

        /// <summary>
        /// 동일 상태로의 재진입은 무시되어야 함 (22번 FSM 규칙).
        /// </summary>
        [Test]
        public void 동일_상태_재진입은_무시된다()
        {
            IngredientLevel1State l1 = new IngredientLevel1State();
            StateMachine<IngredientSelectionController> sm = new StateMachine<IngredientSelectionController>(_controller, l1);

            int transitionCount = 0;
            sm.StateChanged.Subscribe(_ => transitionCount++);

            // 동일 상태 재진입 시도
            sm.ChangeState(l1);
            Assert.AreEqual(0, transitionCount, "동일 상태로의 전환은 이벤트를 발생시키지 않아야 함");

            IngredientLevel2State l2 = new IngredientLevel2State();
            sm.ChangeState(l2);
            Assert.AreEqual(1, transitionCount, "다른 상태로의 전환은 이벤트를 발생시켜야 함");

            sm.ChangeState(l2);
            Assert.AreEqual(1, transitionCount, "동일 상태 재진입은 무시되어야 함");

            sm.Dispose();
        }

        /// <summary>
        /// 레벨 4 상태는 최소 1개만 있어도 코딩완료 버튼이 활성화되어야 하고, 중복 제외를 적용하지 않아야 함.
        /// </summary>
        [Test]
        public void 레벨4_상태_규칙_검증()
        {
            IngredientLevel4State l4 = new IngredientLevel4State();

            // 코딩 완료 활성화 규칙: 1개 이상이면 참, 0개면 거짓
            Assert.IsFalse(l4.IsCodingCompleteInteractable(_controller, 0, 5));
            Assert.IsTrue(l4.IsCodingCompleteInteractable(_controller, 1, 5));
            Assert.IsTrue(l4.IsCodingCompleteInteractable(_controller, 3, 5));

            // 중복 제외 없음 규칙: 동일한 물질 목록이 그대로 반환되어야 함
            RfidMatter[] matters =
            {
                new RfidMatter { id = Constants.RfidIds.Level4.MoveUp, label = "위쪽 한 칸" },
                new RfidMatter { id = Constants.RfidIds.Level4.MoveDown, label = "아래쪽 한 칸" },
                new RfidMatter { id = Constants.RfidIds.Level4.MoveRight, label = "오른쪽 한 칸" }
            };
            RfidMatter[] filtered = l4.FilterMatters(_controller, Constants.RfidIds.Level4.Move, matters);
            Assert.AreSame(matters, filtered, "레벨 4는 중복 제외를 하지 않고 원본 배열을 그대로 반환해야 함");
        }

        /// <summary>
        /// 레벨 1 추진력은 화면 이름(label)이 아니라 재료 id와 물질 value로 계산되어야 함(이름을 바꿔도 판정이 유지됨).
        /// </summary>
        [Test]
        public void 레벨1_추진력은_라벨이_아니라_value로_계산된다()
        {
            IngredientLevel1State l1 = new IngredientLevel1State();

            l1.OnStepConfirmed(_controller, 0, Constants.RfidIds.Level1.Engine, new RfidMatter { id = "SolidRocket", label = "이름이 바뀐 엔진", value = 5 });
            l1.OnStepConfirmed(_controller, 1, Constants.RfidIds.Level1.Payload, new RfidMatter { id = "Satellite", label = "괄호 없는 이름", value = 3 });
            l1.OnStepConfirmed(_controller, 2, Constants.RfidIds.Level1.Fuel, new RfidMatter { id = "Fuel4", label = "넷", value = 4 });

            Assert.AreEqual(5 + 4 - 3, _controller.CalculateTotalThrust(), "엔진 출력량 + 연료량 - 탑재 중량이 value 기준으로 계산되어야 함");

            l1.OnStepRolledBack(_controller, 2, Constants.RfidIds.Level1.Fuel, null);
            Assert.AreEqual(5 + 0 - 3, _controller.CalculateTotalThrust(), "연료 단계를 되돌리면 연료량이 0으로 돌아가야 함");
        }

        /// <summary>
        /// 레벨 1은 추진력이 목표 거리와 정확히 같아야 성공하고, 게이지는 목표에서 가장 차며 넘치면 다시 줄어들어야 함.
        /// </summary>
        [Test]
        public void 레벨1_목표거리와_정확히_같아야_성공하고_넘치면_게이지가_줄어든다()
        {
            MissionBoardController board = _go.AddComponent<MissionBoardController>();
            int target = 20; // Start 전 기본 목적지(외계 행성, 거리 20)

            Assert.IsTrue(board.IsThrustValid(target), "목표 거리와 같으면 성공해야 함");
            Assert.IsFalse(board.IsThrustValid(target - 1), "모자라면 실패해야 함");
            Assert.IsFalse(board.IsThrustValid(target + 1), "넘쳐도 실패해야 함");

            Assert.AreEqual(0.5f, board.CalculateFillAmount(10), 0.0001f, "목표의 절반이면 50%");
            Assert.AreEqual(1f, board.CalculateFillAmount(20), 0.0001f, "목표와 같으면 100%");
            Assert.AreEqual(0.75f, board.CalculateFillAmount(25), 0.0001f, "목표를 5 넘으면 75%로 다시 줄어듦");
            Assert.AreEqual(0f, board.CalculateFillAmount(40), 0.0001f, "목표의 2배 이상 넘치면 0%");
            Assert.AreEqual(0f, board.CalculateFillAmount(-3), 0.0001f, "음수면 0%");
        }

        /// <summary>
        /// 레벨 2 상태는 모든 단계가 완료되어야 코딩완료가 활성화되고, 디자인 항목 텍스트에 재료명이 없어야 함.
        /// </summary>
        [Test]
        public void 레벨2_상태_규칙_검증()
        {
            IngredientLevel2State l2 = new IngredientLevel2State();

            // 코딩 완료 활성화 규칙: totalSteps 이상이어야 참
            Assert.IsFalse(l2.IsCodingCompleteInteractable(_controller, 4, 5));
            Assert.IsTrue(l2.IsCodingCompleteInteractable(_controller, 5, 5));

            // 디자인 텍스트 표기 규칙: 재료명 없이 [물질] 형태
            string formatted = l2.FormatDesignItemText(_controller, "발사 코딩 순서", "점화하기");
            Assert.IsTrue(formatted.Contains("점화하기"));
            Assert.IsFalse(formatted.Contains("발사 코딩 순서"), "레벨 2는 재료명이 텍스트에 포함되지 않아야 함");
        }

        /// <summary>
        /// 레벨 2 선택지는 카드를 찍을 때마다 섞여야 하고(정답 순서로 고정되면 설정하기만 눌러도 성공함), 원본 배열 순서는 바뀌지 않아야 함.
        /// </summary>
        [Test]
        public void 레벨2_선택지는_섞이고_원본은_바뀌지_않는다()
        {
            IngredientLevel2State l2 = new IngredientLevel2State();
            string[] ids =
            {
                Constants.RfidIds.Level2.Ignite, Constants.RfidIds.Level2.Ascend, Constants.RfidIds.Level2.SeparateStage1,
                Constants.RfidIds.Level2.SeparateStage2, Constants.RfidIds.Level2.EnterOrbit
            };
            RfidMatter[] original = System.Array.ConvertAll(ids, id => new RfidMatter { id = id });

            bool anyDifferentOrder = false;
            for (int trial = 0; trial < 20; trial++)
            {
                RfidMatter[] result = l2.FilterMatters(_controller, "LaunchSequence", original);
                Assert.AreNotSame(original, result, "원본 배열을 그대로 섞으면 안 됨");
                CollectionAssert.AreEquivalent(original, result, "섞어도 같은 선택지 집합이어야 함");
                for (int i = 0; i < ids.Length; i++)
                {
                    if (result[i].id != ids[i]) { anyDifferentOrder = true; break; }
                }
            }

            for (int i = 0; i < ids.Length; i++) Assert.AreEqual(ids[i], original[i].id, "원본 배열 순서는 그대로여야 함");
            Assert.IsTrue(anyDifferentOrder, "20번 섞는 동안 정답 순서와 다른 순서가 한 번은 나와야 함");
        }

        /// <summary>
        /// 레벨 2에서 취소하면 진행바(Image_Fill)가 남은 확정 개수만큼 줄어들어야 함.
        /// (컨트롤러가 OnStepRolledBack을 디자인 항목 제거보다 먼저 호출하므로, 항목 수 기준이면 줄어들지 않던 버그 회귀 방지)
        /// </summary>
        [UnityTest]
        public IEnumerator 레벨2_취소하면_진행바가_남은_개수만큼_줄어든다()
        {
            IngredientLevel2State l2 = new IngredientLevel2State();
            GameObject fillGo = new GameObject("TestLevel2Fill", typeof(RectTransform));
            try
            {
                Image fill = fillGo.AddComponent<Image>();
                typeof(IngredientSelectionController).GetField("level2FillImage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_controller, fill);
                RfidMatter matter = new RfidMatter { id = Constants.RfidIds.Level2.Ignite };

                for (int step = 0; step < 3; step++) l2.OnStepConfirmed(_controller, step, "LaunchSequence", matter);
                yield return new WaitForSeconds(0.6f); // 진행바 트윈(기본 0.45초)이 끝날 때까지 대기
                Assert.AreEqual(0.5f, fill.fillAmount, 0.001f, "3개 확정 시 진행바는 50%여야 함");

                l2.OnStepRolledBack(_controller, 2, "LaunchSequence", matter);
                yield return new WaitForSeconds(0.6f);
                Assert.AreEqual(0.25f, fill.fillAmount, 0.001f, "하나 취소해 2개가 남으면 25%로 줄어야 함");
            }
            finally
            {
                Object.DestroyImmediate(fillGo);
            }
        }

        /// <summary> 레벨 3 판정에 쓰는 미션 보드(기준값 고정)를 컨트롤러에 연결함. </summary>
        private void SetUpLevel3MissionBoard(int maxElectricity, int minOxygen)
        {
            MissionBoardController board = _go.AddComponent<MissionBoardController>();
            board.SetLevel3LimitsForTest(maxElectricity, minOxygen);
            typeof(IngredientSelectionController).GetField("_missionBoard", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_controller, board);
        }

        /// <summary>
        /// 레벨 3에서 정답 블록을 JSON 단계 순서대로 확정하면, 전기 조건·전기량 낮추기는 전기 게이지를, 산소 조건·산소량 올리기는 산소 게이지를 절반씩 채워야 함.
        /// </summary>
        [Test]
        public void 레벨3_정답을_확정하면_전기와_산소_게이지가_각각_찬다()
        {
            IngredientLevel3State l3 = new IngredientLevel3State();
            SetUpLevel3MissionBoard(maxElectricity: 4, minOxygen: 3);

            l3.OnStepConfirmed(_controller, 0, Constants.RfidIds.Level3.ElectricityCondition, new RfidMatter { id = "Over4", value = 4 });
            Assert.AreEqual(0.5f, l3.ElectricFill, 0.0001f, "전기 조건이 기준값과 같으면 전기 게이지가 절반 차야 함");
            Assert.AreEqual(0f, l3.OxygenFill, 0.0001f, "전기 조건은 산소 게이지를 채우면 안 됨");

            l3.OnStepConfirmed(_controller, 1, Constants.RfidIds.Level3.Electricity, new RfidMatter { id = Constants.RfidIds.Level3.Lower });
            Assert.AreEqual(1f, l3.ElectricFill, 0.0001f, "전기량 낮추기로 전기 게이지가 가득 차야 함");

            l3.OnStepConfirmed(_controller, 2, Constants.RfidIds.Level3.Logic, new RfidMatter { id = "And" });
            Assert.IsFalse(l3.InstabilityPending, "'그리고'는 불안정 상태가 아니어야 함");

            l3.OnStepConfirmed(_controller, 3, Constants.RfidIds.Level3.OxygenCondition, new RfidMatter { id = "Under3", value = 3 });
            Assert.AreEqual(0.5f, l3.OxygenFill, 0.0001f, "산소 조건이 기준값과 같으면 산소 게이지가 절반 차야 함");

            l3.OnStepConfirmed(_controller, 4, Constants.RfidIds.Level3.Oxygen, new RfidMatter { id = Constants.RfidIds.Level3.Raise });
            Assert.AreEqual(1f, l3.OxygenFill, 0.0001f, "산소량 올리기로 산소 게이지가 가득 차야 함");
            Assert.AreEqual(1f, l3.ElectricFill, 0.0001f, "산소 블록은 전기 게이지를 바꾸면 안 됨");
            Assert.IsTrue(l3.EvaluateMission(_controller), "두 게이지가 가득 차고 안정적이면 성공해야 함");
        }

        /// <summary>
        /// 레벨 3 게이지는 단계 순서(stepIndex)가 아니라 재료 id로 정해져야 함. JSON에서 단계 순서를 바꿔(산소 먼저) 확정해도 맞는 게이지가 차야 함.
        /// </summary>
        [Test]
        public void 레벨3_단계_순서를_바꿔도_재료_id에_맞는_게이지가_찬다()
        {
            IngredientLevel3State l3 = new IngredientLevel3State();
            SetUpLevel3MissionBoard(maxElectricity: 5, minOxygen: 4);

            l3.OnStepConfirmed(_controller, 0, Constants.RfidIds.Level3.Oxygen, new RfidMatter { id = Constants.RfidIds.Level3.Raise });
            Assert.AreEqual(0.5f, l3.OxygenFill, 0.0001f, "첫 단계라도 산소량 올리기는 산소 게이지를 채워야 함");
            Assert.AreEqual(0f, l3.ElectricFill, 0.0001f, "산소량 올리기가 전기 게이지를 채우면 안 됨");

            l3.OnStepConfirmed(_controller, 1, Constants.RfidIds.Level3.OxygenCondition, new RfidMatter { id = "Under4", value = 4 });
            l3.OnStepConfirmed(_controller, 2, Constants.RfidIds.Level3.Logic, new RfidMatter { id = "And" });
            l3.OnStepConfirmed(_controller, 3, Constants.RfidIds.Level3.Electricity, new RfidMatter { id = Constants.RfidIds.Level3.Lower });
            l3.OnStepConfirmed(_controller, 4, Constants.RfidIds.Level3.ElectricityCondition, new RfidMatter { id = "Over5", value = 5 });

            Assert.AreEqual(1f, l3.OxygenFill, 0.0001f, "산소 게이지가 가득 차야 함");
            Assert.AreEqual(1f, l3.ElectricFill, 0.0001f, "전기 게이지가 가득 차야 함");
            Assert.IsTrue(l3.EvaluateMission(_controller), "순서와 무관하게 정답이면 성공해야 함");
        }

        /// <summary>
        /// 레벨 3에서 오답 블록은 게이지를 채우지 않고, '또는'은 불안정 상태를 예약하며, 취소하면 적용됐던 효과가 되돌아가야 함.
        /// </summary>
        [Test]
        public void 레벨3_오답과_또는은_게이지를_채우지_않고_취소하면_되돌아간다()
        {
            IngredientLevel3State l3 = new IngredientLevel3State();
            SetUpLevel3MissionBoard(maxElectricity: 4, minOxygen: 3);
            RfidMatter correctCondition = new RfidMatter { id = "Over4", value = 4 };
            RfidMatter or = new RfidMatter { id = Constants.RfidIds.Level3.Or };

            l3.OnStepConfirmed(_controller, 0, Constants.RfidIds.Level3.ElectricityCondition, correctCondition);
            l3.OnStepConfirmed(_controller, 1, Constants.RfidIds.Level3.Electricity, new RfidMatter { id = Constants.RfidIds.Level3.Raise });
            Assert.AreEqual(0.5f, l3.ElectricFill, 0.0001f, "전기량 올리기는 오답이라 전기 게이지가 더 차면 안 됨");

            l3.OnStepConfirmed(_controller, 2, Constants.RfidIds.Level3.Logic, or);
            Assert.IsTrue(l3.InstabilityPending, "'또는'을 고르면 불안정 상태가 예약돼야 함");

            l3.OnStepRolledBack(_controller, 2, Constants.RfidIds.Level3.Logic, or);
            Assert.IsFalse(l3.InstabilityPending, "'또는'을 취소하면 불안정 상태가 풀려야 함");

            l3.OnStepRolledBack(_controller, 0, Constants.RfidIds.Level3.ElectricityCondition, correctCondition);
            Assert.AreEqual(0f, l3.ElectricFill, 0.0001f, "전기 조건을 취소하면 채웠던 전기 게이지가 되돌아가야 함");
        }

        /// <summary>
        /// 레벨 1 상태는 모든 단계가 완료되어야 코딩완료가 활성화되고, 텍스트에 재료명이 포함되어야 함.
        /// </summary>
        [Test]
        public void 레벨1_상태_규칙_검증()
        {
            IngredientLevel1State l1 = new IngredientLevel1State();

            Assert.IsFalse(l1.IsCodingCompleteInteractable(_controller, 2, 3));
            Assert.IsTrue(l1.IsCodingCompleteInteractable(_controller, 3, 3));

            string formatted = l1.FormatDesignItemText(_controller, "연료량", "5");
            Assert.IsTrue(formatted.Contains("연료량"));
            Assert.IsTrue(formatted.Contains("5"));
        }
    }
}
