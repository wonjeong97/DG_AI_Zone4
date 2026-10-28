using System.Collections.Generic;
using DGAIZone.App;
using DGAIZone.Game.Events;
using DGAIZone.Game.Hardware;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 디버그 입력(DebugInputActions) 검증 테스트. InputTestFixture의 가상 키보드로 키를 눌러
    /// 숫자키 카드 시뮬레이션이 액션 바인딩대로 동작하는지 확인함.
    /// </summary>
    public class DebugInputTests
    {
        private readonly InputTestFixture _input = new InputTestFixture();
        private Keyboard _keyboard;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _input.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go); // 액션을 끄고 정리한 뒤 입력 시스템을 되돌림
            _input.TearDown();
        }

        [Test]
        public void 숫자키_1부터_4는_동작_제어_논리_함수_카드를_발행한다()
        {
            RecordingPublisher publisher = new RecordingPublisher();
            _go = new GameObject("KeyboardRfidSimulator");
            _go.SetActive(false);
            KeyboardRfidSimulator simulator = _go.AddComponent<KeyboardRfidSimulator>();
            simulator.Construct(publisher, null);
            _go.SetActive(true);

            _input.PressAndRelease(_keyboard.digit1Key);
            _input.PressAndRelease(_keyboard.digit2Key);
            _input.PressAndRelease(_keyboard.digit3Key);
            _input.PressAndRelease(_keyboard.digit4Key);

            CollectionAssert.AreEqual(
                new[] { Constants.RfidCategories.Action, Constants.RfidCategories.Control, Constants.RfidCategories.Logic, Constants.RfidCategories.Func },
                publisher.Categories);
        }

        /// <summary> 발행된 카드 분류를 순서대로 기록하는 가짜 발행자. </summary>
        private sealed class RecordingPublisher : IPublisher<RfidTagEvent>
        {
            public readonly List<string> Categories = new List<string>();

            public void Publish(RfidTagEvent message) => Categories.Add(message.Category);
        }
    }
}
