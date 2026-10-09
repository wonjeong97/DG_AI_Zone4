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

        /// <summary> 미션에 성공하면 패널 제목이 1존과 같은 "미션 성공!"이어야 함. </summary>
        [Test]
        public void 미션_성공이면_결과_문구가_미션_성공으로_나온다()
        {
            _missionResultText.text = "";
            _resultStore.Result = MissionResult.Success;

            _controller.ApplyMissionResultText();

            Assert.AreEqual("미션 성공!", _missionResultText.text);
        }

        /// <summary> 결과 화면 다음 씬: 레벨 1~4는 레벨 선택, 마지막 레벨은 아웃트로, 관리자 레벨 이동 판은 레벨과 상관없이 타이틀(관리자 화면). </summary>
        [Test]
        public void 다음_씬은_관리자_판이면_타이틀_마지막_레벨이면_아웃트로_아니면_레벨_선택이다()
        {
            for (int level = 1; level < Constants.LastLevel; level++)
                Assert.AreEqual(Constants.Scenes.LevelSelect, ResultFlowController.ResolveNextScene(level, false), $"레벨{level}");

            Assert.AreEqual(Constants.Scenes.Outro, ResultFlowController.ResolveNextScene(Constants.LastLevel, false));
            Assert.AreEqual(Constants.Scenes.Title, ResultFlowController.ResolveNextScene(1, true));
            Assert.AreEqual(Constants.Scenes.Title, ResultFlowController.ResolveNextScene(Constants.LastLevel, true), "관리자 판은 마지막 레벨이어도 아웃트로가 아니라 타이틀로 감");
        }

        /// <summary> 레벨 결과는 서버 모드이고 관리자 레벨 이동 판이 아니며 QR로 확인한 체험자가 있을 때만 올림(관리자 판·로컬 모드 결과가 서버에 섞이지 않게). </summary>
        [Test]
        public void 레벨_결과는_서버_모드에_QR_체험자가_있고_관리자_판이_아닐_때만_올린다()
        {
            Assert.AreEqual(ResultFlowController.UploadDecision.Upload, ResultFlowController.DecideUpload(true, false, true));
            Assert.AreEqual(ResultFlowController.UploadDecision.LocalMode, ResultFlowController.DecideUpload(false, false, true));
            Assert.AreEqual(ResultFlowController.UploadDecision.LevelJump, ResultFlowController.DecideUpload(true, true, true));
            Assert.AreEqual(ResultFlowController.UploadDecision.NoServerVisitor, ResultFlowController.DecideUpload(true, false, false));
            Assert.AreEqual(ResultFlowController.UploadDecision.LocalMode, ResultFlowController.DecideUpload(false, true, false), "로컬 모드는 다른 조건과 상관없이 올리지 않음");
        }
    }
}
