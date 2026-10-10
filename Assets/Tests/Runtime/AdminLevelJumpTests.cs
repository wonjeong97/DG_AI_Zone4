using System.Collections;
using DGAIZone.App;
using DGAIZone.LevelSelect;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 관리자 화면 레벨 이동 검증 — 저장소의 스토리 레벨은 한 번만 꺼내지고, 2_LevelSelect는 그 레벨을 고른 것처럼 바로 스토리로 넘어가야 함.
    /// </summary>
    public class AdminLevelJumpTests
    {
        private GameObject _go;
        private LevelSelectFlowController _controller;

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
        }

        /// <summary> Begin한 레벨은 한 번만 꺼내지고, 이동 표시는 EndLevelJump 전까지 유지됨. </summary>
        [Test]
        public void 스토리_레벨은_한_번만_꺼내지고_이동_표시는_타이틀까지_유지된다()
        {
            AdminLevelJumpStore store = new AdminLevelJumpStore();
            Assert.IsFalse(store.TryTakePendingStoryLevel(out _), "Begin 전에는 꺼낼 레벨이 없어야 함");

            store.Begin(3);

            Assert.IsTrue(store.TryTakePendingStoryLevel(out int level));
            Assert.AreEqual(3, level);
            Assert.IsFalse(store.TryTakePendingStoryLevel(out _), "한 번 꺼낸 레벨은 다시 꺼내지면 안 됨");
            Assert.IsTrue(store.IsLevelJump, "결과 화면까지 관리자 판으로 남아 있어야 함");

            store.EndLevelJump();

            Assert.IsFalse(store.IsLevelJump, "타이틀에 돌아오면 관리자 판 표시가 비워져야 함");
        }

        /// <summary> 관리자 레벨 이동으로 들어오면 2_LevelSelect.json을 불러온 뒤 그 레벨을 고른 것처럼 선택해 스토리로 넘어감. </summary>
        [UnityTest]
        public IEnumerator 관리자_레벨_이동으로_들어오면_그_레벨을_바로_고른다()
        {
            UnlockedLevelStore unlocked = new UnlockedLevelStore { UnlockedLevelCount = 3 };
            SelectedLevelStore selected = new SelectedLevelStore();
            AdminLevelJumpStore jump = new AdminLevelJumpStore();
            jump.Begin(3);

            Button[] buttons = CreateController(unlocked, selected, jump);
            yield return WaitUntilSettingsLoaded(_controller); // Start → 2_LevelSelect.json 로드 → 잠금 다시 적용 → 레벨 선택

            Assert.AreEqual(3, selected.SelectedLevel, "관리자가 고른 레벨이 선택돼야 함");
            Assert.IsFalse(buttons[2].interactable, "고른 레벨 버튼은 스토리 영역으로 옮겨지며 다시 누를 수 없어야 함");
            Assert.IsTrue(buttons[0].interactable && buttons[1].interactable, "그 레벨까지는 해금돼 있어야 함");
            Assert.IsFalse(jump.TryTakePendingStoryLevel(out _), "스토리 레벨은 한 번 쓰고 비워져야 함");
        }

        /// <summary> 관리자 레벨 이동이 아니면 레벨을 고르지 않고 레벨 선택 화면에 머묾. </summary>
        [UnityTest]
        public IEnumerator 관리자_레벨_이동이_아니면_레벨을_고르지_않는다()
        {
            UnlockedLevelStore unlocked = new UnlockedLevelStore { UnlockedLevelCount = 3 };
            SelectedLevelStore selected = new SelectedLevelStore();

            Button[] buttons = CreateController(unlocked, selected, new AdminLevelJumpStore());
            yield return WaitUntilSettingsLoaded(_controller); // 로드가 끝나 잠금을 다시 적용한 뒤에도 고르지 않았는지 봄

            Assert.AreEqual(1, selected.SelectedLevel, "레벨을 고르지 않았으니 기본값 그대로여야 함");
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(buttons[i].interactable, $"레벨 {i + 1} 버튼이 열려 있어야 함");
        }

        /// <summary> 레벨 선택 컨트롤러와 레벨 버튼을 만들고 켬(씬 전환 서비스 없이 — 전환을 기다리지 않고 바로 고름). </summary>
        private Button[] CreateController(UnlockedLevelStore unlocked, SelectedLevelStore selected, AdminLevelJumpStore jump)
        {
            _go = new GameObject("LevelSelectFlowController");
            _go.SetActive(false);
            LevelSelectFlowController controller = _go.AddComponent<LevelSelectFlowController>();
            controller.Construct(null, selected, unlocked, null, levelJumpStore: jump);
            _controller = controller;

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
            return buttons;
        }

        /// <summary> 컨트롤러가 2_LevelSelect.json을 불러와 잠금을 다시 적용할 때까지 기다림(최대 5초, 실시간 고정 대기 대신). </summary>
        private static IEnumerator WaitUntilSettingsLoaded(LevelSelectFlowController controller)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!controller.IsSceneSettingsLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(controller.IsSceneSettingsLoaded, "5초 안에 2_LevelSelect.json을 불러와 잠금을 다시 적용하지 못함");
        }
    }
}
