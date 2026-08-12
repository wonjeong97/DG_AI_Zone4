using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.Video;
using VContainer;
using ZLogger;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬 진입 시 게임 결과(성공/실패)에 맞는 영상을 재생함. 반복 재생은 하지 않으며,
    /// 재생이 끝나면 컴플리트 패널로 전환함.
    /// </summary>
    public class ResultVideoPanel : MonoBehaviour, ISceneVideoReadiness
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private ResultFlowController flowController;
        [SerializeField] private string videoFolderName = "Videos";
        [SerializeField] private string successVideoFileName = "4-1 success.mp4";
        [SerializeField] private string failVideoFileName = "4-1 fail.mp4";

        private readonly UniTaskCompletionSource _readySignal = new UniTaskCompletionSource();
        private GameResultStore _resultStore;
        private ILogger<ResultVideoPanel> _logger;

        /// <summary> VContainer 의존성 주입. 게임 결과 저장소와 로거를 할당함. </summary>
        [Inject]
        public void Construct(GameResultStore resultStore, ILogger<ResultVideoPanel> logger)
        {
            _resultStore = resultStore;
            _logger = logger;
        }

        /// <summary> 씬 진입 시 무작위 영상 재생을 시작함. </summary>
        private void Start()
        {
            PlayResultVideoAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 이 영상이 화면에 실제로 그려질 때까지 대기함. 씬 전환 페이드인을 시작하기 전 SceneTransitionService가 호출함. </summary>
        public UniTask WaitUntilVideoReadyAsync(CancellationToken token)
        {
            return _readySignal.Task.AttachExternalCancellation(token);
        }

        /// <summary> 게임 결과에 맞는 영상을 준비 후 재생하고, 끝나면 컴플리트 패널을 표시함. </summary>
        private async UniTaskVoid PlayResultVideoAsync(CancellationToken token)
        {
            if (videoPlayer == null)
            {
                _readySignal.TrySetResult();
                return;
            }

            bool success = _resultStore != null && _resultStore.Result == MissionResult.Success;
            string fileName = success ? successVideoFileName : failVideoFileName;
            if (_logger != null) _logger.ZLogInformation($"[ResultVideoPanel] Result={(success ? "Success" : "Fail")}. Playing {fileName}.");
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFolderName, fileName);

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = path;
            videoPlayer.isLooping = false;

            videoPlayer.Prepare();
            await UniTask.WaitUntil(() => videoPlayer.isPrepared, cancellationToken: token);
            videoPlayer.Play();

            await VideoReadyGate.WaitUntilFrameRenderedAsync(videoPlayer, VideoReadyGate.DefaultProgressThreshold, token);
            _readySignal.TrySetResult();

            // loopPointReached는 일부 인코딩(비표준 타임스탬프)에서 발생하지 않는 경우가 있어
            // isPlaying 상태 전이를 직접 폴링해 재생 종료를 감지함.
            await UniTask.WaitUntil(() => videoPlayer.isPlaying, cancellationToken: token);
            await UniTask.WaitWhile(() => videoPlayer.isPlaying, cancellationToken: token);

            if (flowController != null)
            {
                flowController.ShowCompletePanel();
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[ResultVideoPanel] flowController is null. CompletePanel will not fade in.");
            }
        }
    }
}
