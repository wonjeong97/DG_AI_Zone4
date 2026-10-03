using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Intro;
using NUnit.Framework;
using UnityEngine;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 씬 전환 및 플로우 컨트롤러의 중복 호출 방지(Transition Guard) 동작 검증 테스트.
    /// </summary>
    public class TransitionGuardTests
    {
        private GameObject _introGo;
        private IntroFlowController _introController;

        [SetUp]
        public void SetUp()
        {
            _introGo = new GameObject("TestIntroFlowController");
            _introController = _introGo.AddComponent<IntroFlowController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_introGo) Object.DestroyImmediate(_introGo);
        }

        [Test]
        public void SceneTransitionService_초기상태에서는_IsTransitioning이_false이다()
        {
            SceneTransitionService service = new SceneTransitionService(null, null);
            Assert.IsFalse(service.IsTransitioning, "초기 상태에서는 IsTransitioning이 false여야 함.");
        }

        [Test]
        public void SceneTransitionService_전환중일때_추가요청은_즉시무시된다()
        {
            SceneTransitionService service = new SceneTransitionService(null, null);
            service.SetTransitioningForTest(true);

            // 이미 전환 중일 때 유효하지 않은 씬 이름으로 호출해도 씬 로드를 시도하지 않고 즉시 완료되어야 함
            UniTask task = service.LoadSceneWithFadeAsync("NonExistentScene_GuardCheck");
            Assert.IsTrue(task.Status.IsCompleted(), "전환 중일 때의 호출은 즉시 완료(무시)되어야 함.");
        }

        [Test]
        public void IntroFlowController_초기상태에서는_IsBusy가_false이다()
        {
            Assert.IsFalse(_introController.IsBusy, "초기 상태에서는 IsBusy가 false여야 함.");
        }

        [Test]
        public void IntroFlowController_IsBusy상태일때_클릭요청은_무시된다()
        {
            // 첫 클릭으로 전환을 시작해 IsBusy 상태로 만듦(서비스는 전환 중이라 실제 씬을 로드하지 않음)
            SceneTransitionService service = new SceneTransitionService(null, null);
            service.SetTransitioningForTest(true);
            _introController.Construct(service, null, null, null);
            _introController.OnUnderstandClicked();
            Assert.IsTrue(_introController.IsBusy);

            // 전환 서비스를 빼도 IsBusy이면 null 검사 이전에 즉시 리턴되므로 에러가 발생하지 않음
            _introController.Construct(null, null, null, null);
            Assert.DoesNotThrow(() => _introController.OnUnderstandClicked());
            Assert.IsTrue(_introController.IsBusy);
        }

        [Test]
        public void IntroFlowController_전환시작시_IsBusy가_true로_설정된다()
        {
            SceneTransitionService service = new SceneTransitionService(null, null);
            // transition 중으로 미리 만들어두어 LoadSceneWithFadeAsync 내부에서 실제 씬 로드를 실행하지 않도록 설정
            service.SetTransitioningForTest(true);

            _introController.Construct(service, null, null, null);
            _introController.OnUnderstandClicked();

            Assert.IsTrue(_introController.IsBusy, "OnUnderstandClicked 호출 후 IsBusy가 true여야 함.");
        }
    }
}
