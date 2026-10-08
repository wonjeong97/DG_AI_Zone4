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

        /// <summary> 활성화 시 씬 전환 대기 대상으로 레지스트리에 등록함. </summary>
        private void OnEnable()
        {
            VideoReadinessRegistry.Register(this);
        }

        /// <summary> 비활성화 시 씬 전환 대기 대상에서 제외함. </summary>
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
        /// 영상을 준비하지 못하면(파일 없음·재생 오류·시간 초과) 오류를 남기고 영상 없이 준비 완료로 알림.
        /// </summary>
        private async UniTaskVoid PlayVideoAsync(CancellationToken token)
        {
            try
            {
                if (!videoPlayer)
                {
                    if (_logger != null) _logger.ZLogWarning($"[RobotVideoPanel] videoPlayer가 null이라 로봇 영상을 재생할 수 없음.");
                    _readySignal.TrySetResult();
                    return;
                }

                if (rawImage) rawImage.enabled = false;
                else if (_logger != null) _logger.ZLogWarning($"[RobotVideoPanel] rawImage가 null이라 영상 준비 중 숨김 처리를 할 수 없음.");

                if (targetTexture)
                {
                    RenderTexture previousActive = RenderTexture.active;
                    RenderTexture.active = targetTexture;
                    GL.Clear(true, true, Color.black);
                    RenderTexture.active = previousActive;
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[RobotVideoPanel] targetTexture가 null이라 이전 프레임 잔상을 지울 수 없음.");
                }

                string path = System.IO.Path.Combine(Application.streamingAssetsPath, Constants.ResourcePaths.VideosFolder, Constants.Files.RobotVideo);

                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = path;
                videoPlayer.isLooping = true;

                if (!await VideoReadyGate.PrepareAsync(videoPlayer, VideoReadyGate.DefaultPrepareTimeoutSeconds, token))
                {
                    // 화면은 영상 없이 두되, 씬 전환 페이드인이 영상 준비 대기 타임아웃까지 기다리지 않게 함
                    if (_logger != null) _logger.ZLogError($"[RobotVideoPanel] {Constants.Files.RobotVideo}을(를) 준비하지 못함(파일 없음·재생 오류·{VideoReadyGate.DefaultPrepareTimeoutSeconds}초 초과). 로봇 영상 없이 진행함.");
                    _readySignal.TrySetResult();
                    return;
                }

                videoPlayer.Play();

                if (!await VideoReadyGate.WaitUntilFrameRenderedAsync(videoPlayer, VideoReadyGate.DefaultProgressThreshold, token) && _logger != null)
                {
                    _logger.ZLogWarning($"[RobotVideoPanel] {Constants.Files.RobotVideo}의 첫 화면이 {VideoReadyGate.FirstFrameTimeoutSeconds}초 안에 그려지지 않았지만 그대로 진행함.");
                }

                if (rawImage) rawImage.enabled = true;
                else if (_logger != null) _logger.ZLogWarning($"[RobotVideoPanel] rawImage가 null이라 로봇 영상을 표시할 수 없음.");
                _readySignal.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소 시 예외 무시
            }
        }
    }
}
