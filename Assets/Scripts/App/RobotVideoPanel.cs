using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using VContainer;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// 정적 로봇 이미지 대신 로봇 영상을 반복 재생하는 패널. 씬 진입과 동시에 준비를 시작하되,
    /// RenderTexture에 실제 프레임이 그려진 뒤에야 화면에 노출해 잔상/빈 프레임 노출을 방지함.
    /// </summary>
    public class RobotVideoPanel : MonoBehaviour, ISceneVideoReadiness
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage rawImage;
        [SerializeField] private RenderTexture targetTexture;
        [SerializeField] private string videoFolderName = "Videos";
        [SerializeField] private string videoFileName = "robot_0811.webm";

        private readonly UniTaskCompletionSource _readySignal = new UniTaskCompletionSource();
        private ILogger<RobotVideoPanel> _logger;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<RobotVideoPanel> logger)
        {
            _logger = logger;
        }

        /// <summary> 씬 진입 시 로봇 영상 재생 준비를 시작함. </summary>
        private void Start()
        {
            PlayVideoAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private void OnEnable()
        {
            VideoReadinessRegistry.Register(this);
        }

        private void OnDisable()
        {
            VideoReadinessRegistry.Unregister(this);
        }

        /// <summary> 이 영상이 화면에 실제로 그려질 때까지 대기함. 씬 전환 페이드인을 시작하기 전 SceneTransitionService가 호출함. </summary>
        public UniTask WaitUntilVideoReadyAsync(CancellationToken token)
        {
            return _readySignal.Task.AttachExternalCancellation(token);
        }

        /// <summary>
        /// 스트리밍 에셋의 로봇 영상을 준비, 재생하고, 첫 프레임이 실제로 그려진 뒤에 RawImage를 노출함.
        /// RenderTexture는 씬 간 공유되므로 Prepare 전에 검게 초기화해 이전 프레임 잔상을 막음.
        /// </summary>
        private async UniTaskVoid PlayVideoAsync(CancellationToken token)
        {
            try
            {
                if (videoPlayer == null)
                {
                    if (_logger != null) _logger.ZLogWarning($"[RobotVideoPanel] videoPlayer가 null이라 로봇 영상을 재생할 수 없음.");
                    _readySignal.TrySetResult();
                    return;
                }

                if (rawImage != null) rawImage.enabled = false;

                if (targetTexture != null)
                {
                    RenderTexture previousActive = RenderTexture.active;
                    RenderTexture.active = targetTexture;
                    GL.Clear(true, true, Color.black);
                    RenderTexture.active = previousActive;
                }

                string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFolderName, videoFileName);

                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = path;
                videoPlayer.isLooping = true;

                videoPlayer.Prepare();
                await UniTask.WaitUntil(() => videoPlayer.isPrepared, cancellationToken: token);
                videoPlayer.Play();

                await VideoReadyGate.WaitUntilFrameRenderedAsync(videoPlayer, VideoReadyGate.DefaultProgressThreshold, token);

                if (rawImage != null) rawImage.enabled = true;
                _readySignal.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소 시 예외 무시
            }
        }
    }
}
