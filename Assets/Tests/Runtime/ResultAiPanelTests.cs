using System.Collections;
using DGAIZone.Result;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 결과 화면 AI 패널의 코드보기 ↔ 영상보기 버튼 검증 테스트. 버튼이 영상이 끝나기 전에는 숨어 있고,
    /// 누를 때마다 문구와 보이는 묶음(정답 설계창 / 성공 영상)이 번갈아 바뀌는지 확인함.
    /// </summary>
    public class ResultAiPanelTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator 코드보기_버튼은_영상이_끝난_뒤_나오고_누를_때마다_설계창과_영상을_번갈아_보여준다()
        {
            // Start 전에 묶음과 버튼을 넣도록 꺼 둔 채 만듦
            _root = new GameObject("TestAiPanel");
            _root.SetActive(false);
            ResultAiPanel panel = _root.AddComponent<ResultAiPanel>();
            CanvasGroup design = new GameObject("Design").AddComponent<CanvasGroup>();
            CanvasGroup video = new GameObject("Video").AddComponent<CanvasGroup>();
            Button button = new GameObject("Button_ViewToggle").AddComponent<Button>();
            TMP_Text label = new GameObject("Text").AddComponent<TextMeshProUGUI>();
            design.transform.SetParent(_root.transform);
            video.transform.SetParent(_root.transform);
            button.transform.SetParent(_root.transform);
            label.transform.SetParent(button.transform);
            panel.SetUpViewToggleForTest(design, video, button);
            _root.SetActive(true);

            yield return null; // Start에서 버튼을 숨기고 연결함
            Assert.IsFalse(button.gameObject.activeSelf, "성공 영상이 끝나기 전에는 코드보기 버튼이 숨어 있어야 함.");

            // 성공 영상이 끝난 상태(영상 보임)를 만듦
            video.alpha = 1f;
            design.alpha = 0f;
            panel.ShowViewToggle();
            Assert.IsTrue(button.gameObject.activeSelf, "성공 영상이 끝나면 코드보기 버튼이 보여야 함.");
            Assert.AreEqual(ResultAiPanel.CodeViewText, label.text);

            button.onClick.Invoke();
            Assert.AreEqual(ResultAiPanel.VideoViewText, label.text, "코드보기를 누르면 문구가 영상보기로 바뀌어야 함.");
            for (int i = 0; i < 5; i++) yield return null; // 교차 페이드(시간 0)가 끝나기를 기다림
            Assert.AreEqual(1f, design.alpha, "코드보기를 누르면 정답 설계창이 보여야 함.");
            Assert.AreEqual(0f, video.alpha, "코드보기를 누르면 성공 영상이 숨어야 함.");

            button.onClick.Invoke();
            Assert.AreEqual(ResultAiPanel.CodeViewText, label.text, "영상보기를 누르면 문구가 코드보기로 바뀌어야 함.");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(0f, design.alpha, "영상보기를 누르면 정답 설계창이 숨어야 함.");
            Assert.AreEqual(1f, video.alpha, "영상보기를 누르면 성공 영상이 보여야 함.");
        }
    }
}
