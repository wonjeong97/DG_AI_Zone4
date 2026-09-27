using System.Collections.Generic;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using DGAIZone.Game.UI;
using DGAIZone.Game.UI.States;
using HuliacDev.Core;
using NUnit.Framework;
using R3;
using UnityEngine;

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
            string[] matters = { "위쪽 한칸", "아래쪽 한칸", "오른쪽 한칸" };
            string[] filtered = l4.FilterMatters(_controller, "이동하기", matters);
            Assert.AreSame(matters, filtered, "레벨 4는 중복 제외를 하지 않고 원본 배열을 그대로 반환해야 함");
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
