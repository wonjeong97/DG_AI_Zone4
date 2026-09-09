using DGAIZone.App;
using VContainer;
using VContainer.Unity;

namespace DGAIZone.Title
{
    /// <summary>
    /// 타이틀 씬 전용 LifetimeScope. 0_Title에 배치된 GameLifetimeScope(루트, DontDestroyOnLoad)를
    /// 부모로 직접 찾아 연결하며, 타이틀 흐름 컨트롤러를 컨테이너에 등록함.
    /// </summary>
    public class TitleLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// 루트 스코프인 GameLifetimeScope를 부모로 찾음. Awake 실행 순서는 보장되지 않으므로,
        /// 아직 컨테이너가 빌드되지 않았다면(Container == null) 여기서 직접 Build()를 강제해
        /// 이후 Parent.Container 참조가 항상 유효하도록 함.
        /// </summary>
        protected override LifetimeScope FindParent()
        {
            GameLifetimeScope root = Find<GameLifetimeScope>() as GameLifetimeScope;
            if (root != null && root.Container == null) root.Build();
            return root;
        }

        /// <summary> 타이틀 흐름 컨트롤러를 계층에서 찾아 등록하여 주입 대상으로 만듦. </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<TitleFlowController>();
        }
    }
}
