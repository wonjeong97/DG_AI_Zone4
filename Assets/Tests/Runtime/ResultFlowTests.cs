using DGAIZone.App;
using DGAIZone.Result;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 결과 씬(ResultFlowController)의 미션 결과 문구 검증 테스트.
    /// </summary>
    public class ResultFlowTests
    {
        private GameObject _go;
        private ResultFlowController _controller;
        private TMP_Text _missionResultText;
        private GameResultStore _resultStore;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestResultFlow");
            _controller = _go.AddComponent<ResultFlowController>();

            GameObject textGo = new GameObject("Text_MissionComplete", typeof(RectTransform));
            textGo.transform.SetParent(_go.transform);
            _missionResultText = textGo.AddComponent<TextMeshProUGUI>();
            _missionResultText.text = "미션 완료!";
            _controller.SetMissionResultTextForTest(_missionResultText);

            _resultStore = new GameResultStore();
            _controller.Construct(null, null, null, _resultStore, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        /// <summary> 미션에 실패하면 패널 제목이 "미션 실패!"로 바뀌어야 함(씬에 "미션 완료!"로 고정돼 있던 문제 회귀 방지). </summary>
        [Test]
        public void 미션_실패면_결과_문구가_미션_실패로_바뀐다()
        {
            _resultStore.Result = MissionResult.Fail;

            _controller.ApplyMissionResultText();

            Assert.AreEqual("미션 실패!", _missionResultText.text);
        }

        /// <summary> 미션에 성공하면 패널 제목이 "미션 완료!"여야 함. </summary>
        [Test]
        public void 미션_성공이면_결과_문구가_미션_완료로_나온다()
        {
            _missionResultText.text = "";
            _resultStore.Result = MissionResult.Success;

            _controller.ApplyMissionResultText();

            Assert.AreEqual("미션 완료!", _missionResultText.text);
        }
    }
}
