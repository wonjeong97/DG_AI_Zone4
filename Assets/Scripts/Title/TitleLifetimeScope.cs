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
        /// 루트 스코프인 GameLifetimeScope를 부모로 찾음. Find&lt;GameLifetimeScope&gt;() 대신 정적
        /// GameLifetimeScope.Instance만 사용하는 ResolveAndEnsureBuilt()를 통해야, 0_Title 재로드로
        /// 생긴(곧 자멸할) 중복 인스턴스를 잘못 부모로 삼는 레이스를 피할 수 있음(자세한 배경은
        /// GameLifetimeScope 클래스 주석 참고).
        /// </summary>
        protected override LifetimeScope FindParent() => GameLifetimeScope.ResolveAndEnsureBuilt();

        /// <summary> 타이틀 흐름 컨트롤러를 계층에서 찾아 등록하여 주입 대상으로 만듦. </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<TitleFlowController>();
        }
    }
}
