using System;
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
        public const float DefaultPrepareTimeoutSeconds = 5f; // 로컬 파일은 보통 1초 안에 준비됨. SceneTransitionService의 영상 준비 대기와 같은 값
        public const float PlaybackEndMarginSeconds = 5f; // 재생 종료 대기 상한 = 영상 길이 + 이 값
        public const float FirstFrameTimeoutSeconds = 5f; // Play() 뒤 첫 화면이 그려지기를 기다리는 상한

        /// <summary>
        /// videoPlayer.Prepare()를 호출하고 준비가 끝날 때까지 대기함. 준비되면 true, 오류(errorReceived)가 오거나
        /// timeoutSeconds 안에 준비되지 않으면 false를 반환함. 파일이 없으면 Unity가 경고만 남기고 errorReceived 없이
        /// isPrepared가 계속 false로 남으므로(에디터에서 확인), 타임아웃으로도 끊어 연출이 멈추지 않게 함.
        /// token 취소 시에는 OperationCanceledException을 그대로 전파함.
        /// </summary>
        public static async UniTask<bool> PrepareAsync(VideoPlayer videoPlayer, float timeoutSeconds, CancellationToken token)
        {
            bool failed = false;
            VideoPlayer.ErrorEventHandler onError = (_, _) => failed = true;
            videoPlayer.errorReceived += onError;

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            // 타이머 등록을 CTS보다 먼저 해제해야(using은 선언 역순으로 해제됨), 일찍 끝난 뒤 타이머가 해제된 CTS를 Cancel하지 않음
            using IDisposable timeoutTimer = timeoutCts.CancelAfterSlim(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                videoPlayer.Prepare();
                await UniTask.WaitUntil(() => videoPlayer.isPrepared || failed, cancellationToken: timeoutCts.Token);
                return videoPlayer.isPrepared;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return false; // 타임아웃
            }
            finally
            {
                videoPlayer.errorReceived -= onError;
            }
        }

        /// <summary>
        /// videoPlayer.Play() 호출 이후에 사용. frame 진행률이 threshold 이상이 될 때까지 대기하고 한 프레임 더 대기함. 그려졌으면 true.
        /// 준비는 됐는데 디코더가 멈춰 frame이 오르지 않아도 연출이 영구히 멈추지 않도록 FirstFrameTimeoutSeconds가 지나면 false를 반환함.
        /// token 취소 시에는 OperationCanceledException을 그대로 전파함.
        /// </summary>
        public static async UniTask<bool> WaitUntilFrameRenderedAsync(VideoPlayer videoPlayer, float progressThreshold, CancellationToken token)
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            // PrepareAsync와 같은 이유로 타이머 등록을 CTS보다 먼저 해제함
            using IDisposable timeoutTimer = timeoutCts.CancelAfterSlim(TimeSpan.FromSeconds(FirstFrameTimeoutSeconds));
            try
            {
                await UniTask.WaitUntil(() =>
                {
                    if (videoPlayer.frame < 0) return false;
                    ulong frameCount = videoPlayer.frameCount;
                    if (frameCount == 0) return true;
                    return (double)videoPlayer.frame / frameCount >= progressThreshold;
                }, cancellationToken: timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return false; // 상한 초과
            }

            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, token);
            return true;
        }

        /// <summary>
        /// videoPlayer.Play() 이후 재생이 끝날 때까지 대기함. loopPointReached는 일부 인코딩(비표준 타임스탬프)에서 발생하지 않아
        /// isPlaying 상태 전이를 폴링함. 디코더가 멈춰 isPlaying이 바뀌지 않아도 연출이 영구히 멈추지 않도록, 영상 길이(준비 후 알 수 있음)에
        /// PlaybackEndMarginSeconds를 더한 시간이 지나면 false를 반환함. 정상 종료면 true. token 취소 시에는 OperationCanceledException을 그대로 전파함.
        /// </summary>
        public static async UniTask<bool> WaitUntilPlaybackEndsAsync(VideoPlayer videoPlayer, CancellationToken token)
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            // PrepareAsync와 같은 이유로 타이머 등록을 CTS보다 먼저 해제함
            using IDisposable timeoutTimer = timeoutCts.CancelAfterSlim(TimeSpan.FromSeconds(videoPlayer.length + PlaybackEndMarginSeconds));
            try
            {
                await UniTask.WaitUntil(() => videoPlayer.isPlaying, cancellationToken: timeoutCts.Token);
                await UniTask.WaitWhile(() => videoPlayer.isPlaying, cancellationToken: timeoutCts.Token);
                return true;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return false; // 상한 초과
            }
        }
    }
}
