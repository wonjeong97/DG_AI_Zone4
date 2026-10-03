using DGAIZone.App;
using VContainer;
using VContainer.Unity;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬 전용 LifetimeScope. 0_Title에서부터 DontDestroyOnLoad로 유지되는 GameLifetimeScope(루트)를
    /// 부모로 직접 찾아 연결하며, 결과 흐름 컨트롤러·결과 영상·AI 패널·나의 코딩 결과 패널을 컨테이너에 등록함.
    /// </summary>
    public class ResultLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// 루트 스코프인 GameLifetimeScope를 부모로 찾음. 앱 시작 직후에는 이 Awake가 VContainerSettings의
        /// 루트 자동 생성(첫 씬 sceneLoaded 콜백)보다 먼저 실행되므로, ResolveAndEnsureBuilt()로 루트의
        /// 생성·빌드를 보장받음(자세한 배경은 GameLifetimeScope 클래스 주석 참고).
        /// </summary>
        protected override LifetimeScope FindParent() => GameLifetimeScope.ResolveAndEnsureBuilt();

        /// <summary> 결과 흐름 컨트롤러·결과 영상·AI 패널·나의 코딩 결과 패널을 계층에서 찾아 등록하여 주입 대상으로 만듦(두 패널이 각자 설계창에 리졸버를 주입함). </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<ResultFlowController>();
            builder.RegisterComponentInHierarchy<ResultVideoPanel>();
            builder.RegisterComponentInHierarchy<ResultAiPanel>();
            builder.RegisterComponentInHierarchy<ResultPlayerPanel>();
        }
    }
}
