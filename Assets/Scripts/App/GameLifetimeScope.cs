using VContainer;
using VContainer.Unity;
using Wonjeong.App;

namespace DGAIZone.App
{
    /// <summary>
    /// 게임 전역 LifetimeScope. 템플릿의 RootLifetimeScope가 구성하는 전역 로깅과 MessagePipe를 그대로 사용하고,
    /// 게임 매니저 등 게임 고유 컴포넌트를 컨테이너에 등록함.
    /// </summary>
    public class GameLifetimeScope : RootLifetimeScope
    {
        /// <summary>
        /// 템플릿 기본 구성(로깅, MessagePipe)을 먼저 적용한 뒤 계층에 있는 게임 매니저를 등록하여 주입 대상으로 만듦.
        /// </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponentInHierarchy<GameManager>();
        }
    }
}
