using DGAIZone.App;
using VContainer;
using VContainer.Unity;

namespace DGAIZone.Title
{
    /// <summary>
    /// 타이틀 씬 전용 LifetimeScope. VContainerSettings 루트 스코프를 부모로 자동 연결하며, 타이틀 흐름 컨트롤러를 컨테이너에 등록함.
    /// </summary>
    public class TitleLifetimeScope : LifetimeScope
    {
        /// <summary> 타이틀 흐름 컨트롤러를 계층에서 찾아 등록하여 주입 대상으로 만듦. </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<TitleFlowController>();
        }
    }
}
