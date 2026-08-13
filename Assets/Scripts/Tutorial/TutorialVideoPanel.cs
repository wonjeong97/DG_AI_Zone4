using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.Video;
using VContainer;
using ZLogger;

namespace DGAIZone.Tutorial
{
    /// <summary>
    /// 튜토리얼 씬 진입 시 안내 영상을 반복 재생함. 사용자가 "이해했어요" 버튼을 눌러 씬을 벗어날 때까지 계속 재생함.
    /// </summary>
    public class TutorialVideoPanel : MonoBehaviour, ISceneVideoReadiness
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private string videoFolderName = "Videos";
        [SerializeField] private string videoFileName = "Tutorial.mp4";

        private readonly UniTaskCompletionSource _readySignal = new UniTaskCompletionSource();
        private ILogger<TutorialVideoPanel> _logger;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<TutorialVideoPanel> logger)
        {
            _logger = logger;
        }

        /// <summary> 씬 진입 시 안내 영상 반복 재생을 시작함. </summary>
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

        /// <summary> 스트리밍 에셋의 튜토리얼 영상을 준비 후 반복 재생하고, 첫 프레임이 실제로 그려진 뒤 준비 완료를 알림. </summary>
        private async UniTaskVoid PlayVideoAsync(CancellationToken token)
        {
            try
            {
                if (videoPlayer == null)
                {
                    if (_logger != null) _logger.ZLogWarning($"[TutorialVideoPanel] videoPlayer is null. Cannot play tutorial video.");
                    _readySignal.TrySetResult();
                    return;
                }

                string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFolderName, videoFileName);

                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = path;
                videoPlayer.isLooping = true;

                videoPlayer.Prepare();
                await UniTask.WaitUntil(() => videoPlayer.isPrepared, cancellationToken: token);
                videoPlayer.Play();

                await VideoReadyGate.WaitUntilFrameRenderedAsync(videoPlayer, VideoReadyGate.DefaultProgressThreshold, token);
                _readySignal.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소 시 예외 무시
            }
        }
    }
}
