using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.Video;
using VContainer;
using ZLogger;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬 진입 시 성공/실패 영상 중 하나를 무작위로 선택해 재생함. 반복 재생은 하지 않으며,
    /// 재생이 끝나면 컴플리트 패널로 전환함.
    /// </summary>
    public class ResultVideoPanel : MonoBehaviour
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private ResultFlowController flowController;
        [SerializeField] private string videoFolderName = "Videos";
        [SerializeField] private string[] videoFileNames = { "4-1 success.mp4", "4-1 fail.mp4" };

        private ILogger<ResultVideoPanel> _logger;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<ResultVideoPanel> logger)
        {
            _logger = logger;
        }

        /// <summary> 씬 진입 시 무작위 영상 재생을 시작함. </summary>
        private void Start()
        {
            PlayRandomVideoAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 영상 목록 중 하나를 무작위로 골라 준비 후 재생하고, 끝나면 컴플리트 패널을 표시함. </summary>
        private async UniTaskVoid PlayRandomVideoAsync(CancellationToken token)
        {
            if (videoPlayer == null || videoFileNames == null || videoFileNames.Length == 0) return;

            string fileName = videoFileNames[Random.Range(0, videoFileNames.Length)];
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFolderName, fileName);

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = path;
            videoPlayer.isLooping = false;

            videoPlayer.Prepare();
            await UniTask.WaitUntil(() => videoPlayer.isPrepared, cancellationToken: token);
            videoPlayer.Play();

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
