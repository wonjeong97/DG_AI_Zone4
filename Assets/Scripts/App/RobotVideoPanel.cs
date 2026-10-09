using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.Data;
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
    /// 처음엔 숨겨 두는 패널(3_Game의 스토리 다시 보기)은 playOnStart를 꺼 두고, 보여 줄 때 Play·숨길 때 Pause를 부름.
    /// </summary>
    public class RobotVideoPanel : MonoBehaviour, ISceneVideoReadiness
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage rawImage;
        [SerializeField] private RenderTexture targetTexture;

        [Tooltip("씬이 시작되면 바로 재생할지. 끄면 패널을 보여 줄 때 Play()로 시작하고, 씬 전환이 이 영상을 기다리지 않음(숨긴 패널의 영상을 내내 디코딩하지 않게)")]
        [SerializeField] private bool playOnStart = true;

        private readonly UniTaskCompletionSource _readySignal = new UniTaskCompletionSource();
        private ILogger<RobotVideoPanel> _logger;
        private bool _started;       // 준비·재생을 시작했는지
        private bool _wantsPlaying;  // 지금 보여 줘야 해서 재생해야 하는지(준비 중에 숨기면 준비 뒤 멈춤)

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<RobotVideoPanel> logger)
        {
            _logger = logger;
        }

        /// <summary> 씬 진입 시 로봇 영상 재생 준비를 시작함(playOnStart를 끈 패널은 Play를 부를 때까지 기다림). </summary>
        private void Start()
        {
            if (playOnStart) Play();
        }

        /// <summary> 활성화 시 씬 전환 대기 대상으로 레지스트리에 등록함(처음엔 숨겨 두는 패널은 씬 전환이 기다리지 않게 등록하지 않음). </summary>
        private void OnEnable()
        {
            if (playOnStart) VideoReadinessRegistry.Register(this);
        }

        /// <summary> 패널을 보여 줄 때 재생함. 처음이면 준비부터 하고, 멈춰 두었으면 이어서 재생함. </summary>
        public void Play()
        {
            _wantsPlaying = true;
            if (!_started)
            {
                _started = true;
                PlayVideoAsync(this.GetCancellationTokenOnDestroy()).Forget();
                return;
            }

            if (videoPlayer && videoPlayer.isPrepared && !videoPlayer.isPlaying) videoPlayer.Play(); // 아직 준비 중이면 준비 뒤 PlayVideoAsync가 재생함
        }

        /// <summary> 패널을 숨길 때 디코딩을 멈춤(마지막 프레임은 RenderTexture에 남아 다시 보일 때 그대로 이어짐). </summary>
        public void Pause()
        {
            _wantsPlaying = false;
            if (videoPlayer && videoPlayer.isPlaying) videoPlayer.Pause(); // 준비 중이면 준비 뒤 PlayVideoAsync가 멈춤
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
        /// 스트리밍 에셋의 로봇 영상(00_Common.json의 robotVideoPath)을 준비, 재생하고, 첫 프레임이 실제로 그려진 뒤에 RawImage를 노출함.
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

                string relativePath = await GetVideoPathAsync(token);

                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = System.IO.Path.Combine(Application.streamingAssetsPath, relativePath);
                videoPlayer.isLooping = true;

                if (!await VideoReadyGate.PrepareAsync(videoPlayer, VideoReadyGate.DefaultPrepareTimeoutSeconds, token))
                {
                    // 화면은 영상 없이 두되, 씬 전환 페이드인이 영상 준비 대기 타임아웃까지 기다리지 않게 함
                    if (_logger != null) _logger.ZLogError($"[RobotVideoPanel] {relativePath}을(를) 준비하지 못함(파일 없음·재생 오류·{VideoReadyGate.DefaultPrepareTimeoutSeconds}초 초과). 로봇 영상 없이 진행함.");
                    _readySignal.TrySetResult();
                    return;
                }

                videoPlayer.Play();

                if (!await VideoReadyGate.WaitUntilFrameRenderedAsync(videoPlayer, VideoReadyGate.DefaultProgressThreshold, token) && _logger != null)
                {
                    _logger.ZLogWarning($"[RobotVideoPanel] {relativePath}의 첫 화면이 {VideoReadyGate.FirstFrameTimeoutSeconds}초 안에 그려지지 않았지만 그대로 진행함.");
                }

                if (rawImage) rawImage.enabled = true;
                else if (_logger != null) _logger.ZLogWarning($"[RobotVideoPanel] rawImage가 null이라 로봇 영상을 표시할 수 없음.");
                _readySignal.TrySetResult();

                if (!_wantsPlaying) videoPlayer.Pause(); // 준비하는 사이 패널이 다시 숨겨짐 — 첫 프레임만 남기고 멈춤
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소 시 예외 무시
            }
        }

        /// <summary>
        /// 00_Common.json의 robotVideoPath(StreamingAssets 기준)를 돌려줌.
        /// 비었거나 그 파일이 없으면 경고를 남기고 기본 경로(Constants.Files.RobotVideo)를 돌려줌(기본 영상도 없으면 준비 실패로 처리됨).
        /// </summary>
        private async UniTask<string> GetVideoPathAsync(CancellationToken token)
        {
            CommonSettings settings = await CommonSettingsProvider.GetAsync(token);
            string relativePath = settings.robotVideoPath;
            if (StreamingAssetExists(relativePath)) return relativePath;

            if (_logger != null) _logger.ZLogWarning($"[RobotVideoPanel] 00_Common.json의 robotVideoPath '{relativePath}' 파일이 없어 기본 영상 {Constants.Files.RobotVideo}을(를) 재생함.");
            return Constants.Files.RobotVideo;
        }

        /// <summary> StreamingAssets 기준 경로의 파일이 있는지 봄. 비었거나 경로에 쓸 수 없는 문자('|' 등)가 있으면 false(현장에서 JSON을 잘못 고친 경우). </summary>
        private static bool StreamingAssetExists(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return false;

            try
            {
                return System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, relativePath));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
