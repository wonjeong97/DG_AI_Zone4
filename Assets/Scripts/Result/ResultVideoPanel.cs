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
    /// "{videoFileNamePrefix}-{레벨}-{Success|Fail}.mp4" 규칙을 따름(예: 레벨 2 성공 = "4-2-Success.mp4").
    /// 반복 재생은 하지 않으며, 재생이 끝나면 AI 연출(ResultFlowController.PlayAiSequence)로 넘어감.
    /// </summary>
    public class ResultVideoPanel : MonoBehaviour, ISceneVideoReadiness
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private ResultFlowController flowController;

        private const int MinLevel = 1;
        private const int MaxLevel = 4; // 실제로 레벨별 영상이 존재하는 최대 레벨(4-1~4-4). 레벨이 늘어나면 영상 추가와 함께 이 값도 올려야 함.

        private readonly UniTaskCompletionSource _readySignal = new UniTaskCompletionSource();
        private GameResultStore _resultStore;
        private SelectedLevelStore _selectedLevelStore;
        private ILogger<ResultVideoPanel> _logger;

        /// <summary> 레벨을 영상이 존재하는 범위(MinLevel~MaxLevel)로 맞춤. </summary>
        internal static int ClampLevel(int level) => Mathf.Clamp(level, MinLevel, MaxLevel);

        /// <summary> "{videoFileNamePrefix}-{레벨}-{Success|Fail}.mp4" 규칙의 파일명. level은 ClampLevel을 거친 값이어야 함. </summary>
        internal static string GetVideoFileName(int level, bool success) =>
            ZString.Concat(Constants.Files.ResultVideoPrefix, "-", level, "-", success ? "Success" : "Fail", ".mp4");

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

        /// <summary> 게임 결과에 맞는 영상을 준비 후 재생하고, 끝나면 AI 연출로 넘어감(마지막 프레임은 화면에 남음). </summary>
        private async UniTaskVoid PlayResultVideoAsync(CancellationToken token)
        {
            try
            {
                if (!videoPlayer)
                {
                    if (_logger != null) _logger.ZLogWarning($"[ResultVideoPanel] videoPlayer가 null이라 결과 영상을 재생할 수 없음.");
                    _readySignal.TrySetResult();
                    return;
                }

                bool success = _resultStore != null && _resultStore.Result == MissionResult.Success;

                int level = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : MinLevel;
                int clampedLevel = ClampLevel(level);
                if (clampedLevel != level && _logger != null)
                {
                    _logger.ZLogWarning($"[ResultVideoPanel] SelectedLevel({level})이 영상이 존재하는 범위({MinLevel}~{MaxLevel})를 벗어나 {clampedLevel}로 대체함.");
                }

                string fileName = GetVideoFileName(clampedLevel, success);
                if (_logger != null) _logger.ZLogInformation($"[ResultVideoPanel] 레벨={clampedLevel}, 결과={(success ? "성공" : "실패")}. {fileName} 재생 중.");
                string path = GetVideoPath(fileName);

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

                if (flowController)
                {
                    flowController.PlayAiSequence();
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[ResultVideoPanel] flowController가 null이라 AI 연출과 CompletePanel이 나오지 않음.");
                }
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소 시 예외 무시
            }
        }
    }
}
