using System;
using System.Threading;
using Cysharp.Text;
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
    /// 결과 씬 진입 시 선택된 레벨과 게임 결과(성공/실패)에 맞는 영상을 재생함. 파일명은
    /// "{videoFileNamePrefix}-{레벨}-{Success|Fail}.mp4" 규칙을 따름(예: 레벨 2 성공 = "4-2-Success.mp4"). 레벨 3처럼 실패 원인별 영상이 있으면
    /// GameResultStore.FailVideoSuffix에 따라 "4-3-Fail-O2.mp4"처럼 고름.
    /// 반복 재생은 하지 않으며, 재생이 끝나면 AI 연출(ResultFlowController.PlayAiSequence)로 넘어감.
    /// </summary>
    public class ResultVideoPanel : MonoBehaviour, ISceneVideoReadiness
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private ResultFlowController flowController;

        private const int MinLevel = 1;

        private readonly UniTaskCompletionSource _readySignal = new UniTaskCompletionSource();
        private GameResultStore _resultStore;
        private SelectedLevelStore _selectedLevelStore;
        private ILogger<ResultVideoPanel> _logger;

        /// <summary> 레벨을 영상이 존재하는 범위(MinLevel~Constants.LastLevel)로 맞춤. </summary>
        internal static int ClampLevel(int level) => Mathf.Clamp(level, MinLevel, Constants.LastLevel);

        /// <summary>
        /// "{videoFileNamePrefix}-{레벨}-{Success|Fail}.mp4" 규칙의 파일명. 실패이고 failSuffix가 있으면 실패 원인별 영상
        /// "{videoFileNamePrefix}-{레벨}-Fail-{failSuffix}.mp4"(예: "4-3-Fail-O2.mp4"). level은 ClampLevel을 거친 값이어야 함.
        /// </summary>
        internal static string GetVideoFileName(int level, bool success, string failSuffix = null) =>
            success || string.IsNullOrEmpty(failSuffix)
                ? ZString.Concat(Constants.Files.ResultVideoPrefix, "-", level, "-", success ? "Success" : "Fail", ".mp4")
                : ZString.Concat(Constants.Files.ResultVideoPrefix, "-", level, "-Fail-", failSuffix, ".mp4");

        /// <summary> StreamingAssets/Videos 아래 영상 파일의 전체 경로. </summary>
        internal static string GetVideoPath(string fileName) =>
            System.IO.Path.Combine(Application.streamingAssetsPath, Constants.ResourcePaths.VideosFolder, fileName);

        /// <summary> VContainer 의존성 주입. 게임 결과 저장소, 선택된 레벨 저장소, 로거를 할당함. </summary>
        [Inject]
        public void Construct(GameResultStore resultStore, SelectedLevelStore selectedLevelStore, ILogger<ResultVideoPanel> logger)
        {
            _resultStore = resultStore;
            _selectedLevelStore = selectedLevelStore;
            _logger = logger;
        }

        /// <summary> 씬 진입 시 무작위 영상 재생을 시작함. </summary>
        private void Start()
        {
            PlayResultVideoAsync(this.GetCancellationTokenOnDestroy()).Forget();
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
        /// 게임 결과에 맞는 영상을 끝까지 재생한 뒤 AI 연출로 넘어감(마지막 프레임은 화면에 남음).
        /// 영상을 틀 수 없으면(videoPlayer 없음, 파일 없음, 준비 실패) 결과 영상 없이 바로 AI 연출로 넘어가 화면이 멈추지 않게 함.
        /// </summary>
        private async UniTaskVoid PlayResultVideoAsync(CancellationToken token)
        {
            try
            {
                await PlayVideoToEndAsync(token);
            }
            catch (OperationCanceledException)
            {
                return; // 토큰 취소 시 예외 무시
            }
            finally
            {
                _readySignal.TrySetResult(); // 영상을 못 틀었어도 씬 전환 페이드인이 기다리지 않게 함
            }

            if (flowController)
            {
                flowController.PlayAiSequence();
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[ResultVideoPanel] flowController가 null이라 AI 연출과 CompletePanel이 나오지 않음.");
            }
        }

        /// <summary> 재생할 결과 영상 파일명. 실패 원인별 영상(failSuffix)이 있으면 그 파일을, 그 파일이 없으면 경고를 남기고 기본 실패 영상을 씀. </summary>
        private string ResolveVideoFileName(int level, bool success, string failSuffix)
        {
            if (success || string.IsNullOrEmpty(failSuffix)) return GetVideoFileName(level, success);

            string causeFileName = GetVideoFileName(level, false, failSuffix);
            if (System.IO.File.Exists(GetVideoPath(causeFileName))) return causeFileName;

            string fallback = GetVideoFileName(level, false);
            if (_logger != null) _logger.ZLogWarning($"[ResultVideoPanel] 실패 원인별 영상 {causeFileName}이(가) 없어 {fallback}을(를) 재생함.");
            return fallback;
        }

        /// <summary> 결과 영상을 준비해 끝까지 재생함. videoPlayer가 없거나 준비에 실패하면 로그를 남기고 바로 반환함. </summary>
        private async UniTask PlayVideoToEndAsync(CancellationToken token)
        {
            if (!videoPlayer)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultVideoPanel] videoPlayer가 null이라 결과 영상 없이 AI 연출로 넘어감.");
                return;
            }

            bool success = _resultStore != null && _resultStore.Result == MissionResult.Success;

            int level = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : MinLevel;
            int clampedLevel = ClampLevel(level);
            if (clampedLevel != level && _logger != null)
            {
                _logger.ZLogWarning($"[ResultVideoPanel] SelectedLevel({level})이 영상이 존재하는 범위({MinLevel}~{Constants.LastLevel})를 벗어나 {clampedLevel}로 대체함.");
            }

            string fileName = ResolveVideoFileName(clampedLevel, success, _resultStore != null ? _resultStore.FailVideoSuffix : null);
            if (_logger != null) _logger.ZLogInformation($"[ResultVideoPanel] 레벨={clampedLevel}, 결과={(success ? "성공" : "실패")}. {fileName} 재생 중.");

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = GetVideoPath(fileName);
            videoPlayer.isLooping = false;

            if (!await VideoReadyGate.PrepareAsync(videoPlayer, VideoReadyGate.DefaultPrepareTimeoutSeconds, token))
            {
                if (_logger != null) _logger.ZLogError($"[ResultVideoPanel] {fileName}을(를) 준비하지 못함(파일 없음·재생 오류·{VideoReadyGate.DefaultPrepareTimeoutSeconds}초 초과). 결과 영상 없이 AI 연출로 넘어감.");
                return;
            }

            videoPlayer.Play();

            if (!await VideoReadyGate.WaitUntilFrameRenderedAsync(videoPlayer, VideoReadyGate.DefaultProgressThreshold, token) && _logger != null)
            {
                _logger.ZLogWarning($"[ResultVideoPanel] {fileName}의 첫 화면이 {VideoReadyGate.FirstFrameTimeoutSeconds}초 안에 그려지지 않았지만 그대로 진행함.");
            }
            _readySignal.TrySetResult();

            if (!await VideoReadyGate.WaitUntilPlaybackEndsAsync(videoPlayer, token) && _logger != null)
            {
                _logger.ZLogWarning($"[ResultVideoPanel] {fileName}이(가) 영상 길이({videoPlayer.length:F1}초)보다 {VideoReadyGate.PlaybackEndMarginSeconds}초 넘게 끝나지 않아 AI 연출로 넘어감.");
            }
        }
    }
}
