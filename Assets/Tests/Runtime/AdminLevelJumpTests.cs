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
    /// 레벨 선택 화면의 모든 레벨이 처음부터 열려 있고, 완료한 레벨(CompletedLevelStore)에만 V가 켜지며, 다섯 레벨을 모두 마쳤을 때만 종료하기 버튼이 보이는지도 확인함.
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
            SelectedLevelStore selected = new SelectedLevelStore();
            AdminLevelJumpStore jump = new AdminLevelJumpStore();
            jump.Begin(5);

            Button[] buttons = CreateController(new CompletedLevelStore(), selected, jump, out _);
            yield return WaitUntilSettingsLoaded(_controller); // Start → 2_LevelSelect.json 로드 → 레벨 선택

            Assert.AreEqual(5, selected.SelectedLevel, "관리자가 고른 레벨이 선택돼야 함");
            Assert.IsFalse(buttons[4].interactable, "고른 레벨 버튼은 스토리 영역으로 옮겨지며 다시 누를 수 없어야 함");
            Assert.IsFalse(jump.TryTakePendingStoryLevel(out _), "스토리 레벨은 한 번 쓰고 비워져야 함");
        }

        /// <summary> 관리자 레벨 이동이 아니면 레벨을 고르지 않고 레벨 선택 화면에 머물며, 모든 레벨 버튼을 처음부터 누를 수 있음. </summary>
        [UnityTest]
        public IEnumerator 관리자_레벨_이동이_아니면_레벨을_고르지_않고_모든_레벨이_열려_있다()
        {
            SelectedLevelStore selected = new SelectedLevelStore();

            Button[] buttons = CreateController(new CompletedLevelStore(), selected, new AdminLevelJumpStore(), out _);
            yield return WaitUntilSettingsLoaded(_controller); // 설정을 다 불러온 뒤에도 고르지 않았는지 봄

            Assert.AreEqual(1, selected.SelectedLevel, "레벨을 고르지 않았으니 기본값 그대로여야 함");
            for (int i = 0; i < buttons.Length; i++)
                Assert.IsTrue(buttons[i].interactable, $"레벨 {i + 1} 버튼이 열려 있어야 함");
        }

        /// <summary> 결과 화면까지 간 레벨(서버 기록 포함)의 버튼에만 V가 켜짐. </summary>
        [UnityTest]
        public IEnumerator 완료한_레벨의_버튼에만_V가_켜진다()
        {
            CompletedLevelStore completed = new CompletedLevelStore();
            completed.MarkCompleted(2);
            completed.MarkCompleted(4);

            CreateController(completed, new SelectedLevelStore(), new AdminLevelJumpStore(), out GameObject[] marks);
            yield return null; // Start: V 표시 적용

            for (int i = 0; i < marks.Length; i++)
                Assert.AreEqual(i == 1 || i == 3, marks[i].activeSelf, $"레벨 {i + 1} V 표시");
        }

        /// <summary> 종료하기 버튼(아웃트로로 가는 유일한 길)은 다섯 레벨에 모두 V가 붙었을 때만 보임. </summary>
        [UnityTest]
        public IEnumerator 다섯_레벨을_모두_마쳤을_때만_종료하기_버튼이_보인다([Values(4, 5)] int completedCount)
        {
            CompletedLevelStore completed = new CompletedLevelStore();
            for (int level = 1; level <= completedCount; level++) completed.MarkCompleted(level);

            GameObject endGo = new GameObject("Button_End", typeof(RectTransform), typeof(Image), typeof(Button));
            Button end = endGo.GetComponent<Button>();
            endGo.SetActive(completedCount != Constants.LastLevel); // 기대와 반대로 두어 Start가 실제로 바꾸는지 봄
            CreateController(completed, new SelectedLevelStore(), new AdminLevelJumpStore(), out _, end);
            endGo.transform.SetParent(_go.transform, false);
            yield return null; // Start: 종료하기 버튼 표시 결정

            Assert.AreEqual(completedCount == Constants.LastLevel, endGo.activeSelf, $"완료 레벨 {completedCount}개");
        }

        /// <summary> 완료 레벨 저장소는 1~마지막 레벨만 기록하고 Reset으로 모두 비움. </summary>
        [Test]
        public void 완료_레벨은_레벨_범위_안만_기록하고_Reset으로_비운다()
        {
            CompletedLevelStore completed = new CompletedLevelStore();

            Assert.IsTrue(completed.MarkCompleted(1));
            Assert.IsTrue(completed.MarkCompleted(Constants.LastLevel));
            Assert.IsFalse(completed.MarkCompleted(0), "레벨 0은 없음");
            Assert.IsFalse(completed.MarkCompleted(Constants.LastLevel + 1), "마지막 레벨 다음은 없음");
            Assert.IsTrue(completed.IsCompleted(1) && completed.IsCompleted(Constants.LastLevel));
            Assert.IsFalse(completed.IsCompleted(0) || completed.IsCompleted(2));

            completed.Reset();

            Assert.IsFalse(completed.IsCompleted(1), "Reset 뒤에는 V가 없어야 함");
        }

        /// <summary> 레벨 선택 컨트롤러와 레벨 버튼·V 표시를 만들고 켬(씬 전환 서비스 없이 — 전환을 기다리지 않고 바로 고름). </summary>
        private Button[] CreateController(CompletedLevelStore completed, SelectedLevelStore selected, AdminLevelJumpStore jump, out GameObject[] marks, Button end = null)
        {
            _go = new GameObject("LevelSelectFlowController");
            _go.SetActive(false);
            LevelSelectFlowController controller = _go.AddComponent<LevelSelectFlowController>();
            controller.Construct(null, selected, completed, null, levelJumpStore: jump);
            _controller = controller;

            Button[] buttons = new Button[Constants.LastLevel];
            marks = new GameObject[Constants.LastLevel];
            for (int i = 0; i < buttons.Length; i++)
            {
                GameObject buttonGo = new GameObject($"Button_Level{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonGo.transform.SetParent(_go.transform, false);
                buttons[i] = buttonGo.GetComponent<Button>();
                buttons[i].image = buttonGo.GetComponent<Image>();

                marks[i] = new GameObject("Image_Complete", typeof(RectTransform));
                marks[i].transform.SetParent(buttonGo.transform, false);
            }

            controller.SetLevelButtonsForTest(buttons, marks, end);
            _go.SetActive(true);
            return buttons;
        }

        /// <summary> 컨트롤러가 2_LevelSelect.json을 불러와 끝낼 때까지 기다림(최대 5초, 실시간 고정 대기 대신). </summary>
        private static IEnumerator WaitUntilSettingsLoaded(LevelSelectFlowController controller)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!controller.IsSceneSettingsLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(controller.IsSceneSettingsLoaded, "5초 안에 2_LevelSelect.json을 불러오지 못함");
        }
    }
}
