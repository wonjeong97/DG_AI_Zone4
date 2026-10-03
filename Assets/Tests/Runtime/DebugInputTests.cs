using System.Collections;
using System.Collections.Generic;
using DGAIZone.App;
using DGAIZone.Game.Events;
using DGAIZone.Game.Hardware;
using DGAIZone.LevelSelect;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 디버그 입력(DebugInputActions) 검증 테스트. InputTestFixture의 가상 키보드로 키를 눌러
    /// 숫자키 카드 시뮬레이션과 레벨 선택 전체 해금이 액션 바인딩대로 동작하는지 확인함.
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

        [UnityTest]
        public IEnumerator 레벨_선택에서_스페이스바를_누르면_모든_레벨이_열리고_설정을_불러온_뒤에도_유지된다()
        {
            UnlockedLevelStore store = new UnlockedLevelStore();
            _go = new GameObject("LevelSelectFlowController");
            _go.SetActive(false);
            LevelSelectFlowController controller = _go.AddComponent<LevelSelectFlowController>();
            controller.Construct(null, new SelectedLevelStore(), store, null);

            Button[] buttons = new Button[Constants.LastLevel];
            for (int i = 0; i < buttons.Length; i++)
            {
                GameObject buttonGo = new GameObject($"Button_Level{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonGo.transform.SetParent(_go.transform, false);
                buttons[i] = buttonGo.GetComponent<Button>();
                buttons[i].image = buttonGo.GetComponent<Image>();
            }

            controller.SetLevelButtonsForTest(buttons);
            _go.SetActive(true);
            yield return null; // Start: 기본값(레벨 1만 열림)을 적용하고 2_LevelSelect.json을 불러오기 시작함

            Assert.IsTrue(buttons[0].interactable, "처음에는 레벨 1이 열려 있어야 함.");
            Assert.IsFalse(buttons[1].interactable, "처음에는 레벨 2가 잠겨 있어야 함.");

            _input.PressAndRelease(_keyboard.spaceKey);
            yield return null; // [UnityTest]에서는 InputTestFixture가 이벤트를 큐에만 넣으므로 다음 프레임 입력 업데이트에서 처리됨

            Assert.AreEqual(Constants.LastLevel, store.UnlockedLevelCount, "세션 진행도가 마지막 레벨까지 열려야 함.");
            AssertAllUnlocked(buttons);

            // 2_LevelSelect.json 로드가 끝나 잠금이 다시 적용돼도 모두 열려 있어야 함
            yield return new WaitForSecondsRealtime(0.5f);
            AssertAllUnlocked(buttons);
        }

        private static void AssertAllUnlocked(Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
                Assert.IsTrue(buttons[i].interactable, $"레벨 {i + 1} 버튼이 열려 있어야 함.");
        }

        /// <summary> 발행된 카드 분류를 순서대로 기록하는 가짜 발행자. </summary>
        private sealed class RecordingPublisher : IPublisher<RfidTagEvent>
        {
            public readonly List<string> Categories = new List<string>();

            public void Publish(RfidTagEvent message) => Categories.Add(message.Category);
        }
    }
}
