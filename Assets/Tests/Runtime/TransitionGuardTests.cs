using System;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Intro;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

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

        /// <summary>
        /// IsBusy이면 두 번째 클릭이 전환 서비스까지 가지 않음. 서비스는 전환 중이라 실제 씬을 로드하지 않고, 받은 요청마다
        /// '이미 전환 중이라 무시함' 경고를 남기므로 그 수로 서비스에 간 요청 수를 셈.
        /// </summary>
        [Test]
        public void IntroFlowController_IsBusy상태일때_클릭요청은_무시된다()
        {
            CountingLogger<SceneTransitionService> logger = new CountingLogger<SceneTransitionService>();
            SceneTransitionService service = new SceneTransitionService(null, logger);
            service.SetTransitioningForTest(true);
            _introController.Construct(service, null, null, null);

            _introController.OnUnderstandClicked();
            Assert.IsTrue(_introController.IsBusy);
            Assert.AreEqual(1, logger.Count, "첫 클릭은 전환 서비스에 요청해야 함");

            _introController.OnUnderstandClicked();
            Assert.AreEqual(1, logger.Count, "IsBusy이면 두 번째 클릭은 전환 서비스에 요청하지 않아야 함");
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

        /// <summary> 받은 로그 수만 세는 테스트용 로거. </summary>
        private sealed class CountingLogger<T> : ILogger<T>
        {
            public int Count { get; private set; }

            /// <summary> 범위를 쓰지 않음. </summary>
            public IDisposable BeginScope<TState>(TState state) => null;

            /// <summary> 모든 수준을 받음. </summary>
            public bool IsEnabled(LogLevel logLevel) => true;

            /// <summary> 로그 한 줄을 셈. </summary>
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) => Count++;
        }
    }
}
