using System;
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
    /// 결과 씬 진입 시 선택된 레벨과 게임 결과(성공/실패)에 맞는 영상을 재생함. 파일명은
    /// "{videoFileNamePrefix}-{레벨}-{Success|Fail}.mp4" 규칙을 따름(예: 레벨 2 성공 = "4-2-Success.mp4").
    /// 반복 재생은 하지 않으며, 재생이 끝나면 컴플리트 패널로 전환함.
    /// </summary>
    public class ResultVideoPanel : MonoBehaviour, ISceneVideoReadiness
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private ResultFlowController flowController;
        [SerializeField] private string videoFolderName = "Videos";
        [SerializeField] private string videoFileNamePrefix = "4"; // 파일명 앞자리("4-{레벨}-Success.mp4"의 "4")

        private const int MinLevel = 1;
        private const int MaxLevel = 4; // 실제로 레벨별 영상이 존재하는 최대 레벨(4-1~4-4). 레벨이 늘어나면 영상 추가와 함께 이 값도 올려야 함.

        private readonly UniTaskCompletionSource _readySignal = new UniTaskCompletionSource();
        private GameResultStore _resultStore;
        private SelectedLevelStore _selectedLevelStore;
        private ILogger<ResultVideoPanel> _logger;

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

        /// <summary> 게임 결과에 맞는 영상을 준비 후 재생하고, 끝나면 컴플리트 패널을 표시함. </summary>
        private async UniTaskVoid PlayResultVideoAsync(CancellationToken token)
        {
            try
            {
                if (videoPlayer == null)
                {
                    _readySignal.TrySetResult();
                    return;
                }

                bool success = _resultStore != null && _resultStore.Result == MissionResult.Success;

                int level = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : MinLevel;
                int clampedLevel = Mathf.Clamp(level, MinLevel, MaxLevel);
                if (clampedLevel != level && _logger != null)
                {
                    _logger.ZLogWarning($"[ResultVideoPanel] SelectedLevel({level})이 영상이 존재하는 범위({MinLevel}~{MaxLevel})를 벗어나 {clampedLevel}로 대체함.");
                }

                string fileName = $"{videoFileNamePrefix}-{clampedLevel}-{(success ? "Success" : "Fail")}.mp4";
                if (_logger != null) _logger.ZLogInformation($"[ResultVideoPanel] 레벨={clampedLevel}, 결과={(success ? "성공" : "실패")}. {fileName} 재생 중.");
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
                    _logger.ZLogWarning($"[ResultVideoPanel] flowController가 null이라 CompletePanel이 페이드인되지 않음.");
                }
            }
            catch (OperationCanceledException)
            {
                // 토큰 취소 시 예외 무시
            }
        }
    }
}
