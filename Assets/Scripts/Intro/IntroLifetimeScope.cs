using DGAIZone.App;
using VContainer;
using VContainer.Unity;

namespace DGAIZone.Intro
{
    /// <summary>
    /// 인트로 씬 전용 LifetimeScope. VContainerSettings 루트 스코프를 부모로 자동 연결하며, 인트로 흐름 컨트롤러와 로봇 영상 패널을 컨테이너에 등록함.
    /// </summary>
    public class IntroLifetimeScope : LifetimeScope
    {
        /// <summary> 인트로 흐름 컨트롤러와 로봇 영상 패널을 계층에서 찾아 등록하여 주입 대상으로 만듦. </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<IntroFlowController>();
            builder.RegisterComponentInHierarchy<RobotVideoPanel>();
        }
    }
}
