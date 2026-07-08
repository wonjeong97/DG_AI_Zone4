using VContainer;
using VContainer.Unity;

namespace DGAIZone.Outro
{
    /// <summary>
    /// 아웃트로 씬 전용 LifetimeScope. VContainerSettings 루트 스코프를 부모로 자동 연결하며, 아웃트로 흐름 컨트롤러를 컨테이너에 등록함.
    /// </summary>
    public class OutroLifetimeScope : LifetimeScope
    {
        /// <summary> 아웃트로 흐름 컨트롤러를 계층에서 찾아 등록하여 주입 대상으로 만듦. </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<OutroFlowController>();
        }
    }
}
