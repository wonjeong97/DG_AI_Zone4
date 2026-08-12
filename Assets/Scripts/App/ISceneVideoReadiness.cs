using System.Threading;
using Cysharp.Threading.Tasks;

namespace DGAIZone.App
{
    /// <summary>
    /// 씬 전환 시 페이드인을 시작하기 전에 씬 안의 영상이 실제로 화면에 그려질 때까지 기다리기 위한 신호.
    /// SceneTransitionService가 씬 로드 후 이 인터페이스 구현체를 찾아 대기함.
    /// </summary>
    public interface ISceneVideoReadiness
    {
        UniTask WaitUntilVideoReadyAsync(CancellationToken token);
    }
}
