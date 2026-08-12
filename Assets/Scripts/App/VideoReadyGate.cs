using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Video;

namespace DGAIZone.App
{
    /// <summary>
    /// VideoPlayer의 isPrepared는 버퍼링 완료만 의미하고 첫 프레임이 실제로 RenderTexture에
    /// 그려졌다는 보장이 없음. frame&gt;=0과 재생 진행률을 함께 확인한 뒤 한 프레임 더 대기해
    /// 실제로 화면에 그려진 시점을 판단하는 공용 유틸.
    /// </summary>
    public static class VideoReadyGate
    {
        public const float DefaultProgressThreshold = 0.01f;

        /// <summary> videoPlayer.Play() 호출 이후에 사용. frame 진행률이 threshold 이상이 될 때까지 대기하고 한 프레임 더 대기함. </summary>
        public static async UniTask WaitUntilFrameRenderedAsync(VideoPlayer videoPlayer, float progressThreshold, CancellationToken token)
        {
            await UniTask.WaitUntil(() =>
            {
                if (videoPlayer.frame < 0) return false;
                ulong frameCount = videoPlayer.frameCount;
                if (frameCount == 0) return true;
                return (double)videoPlayer.frame / frameCount >= progressThreshold;
            }, cancellationToken: token);

            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, token);
        }
    }
}
