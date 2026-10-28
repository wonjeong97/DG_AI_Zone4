using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game.UI;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.Video;
using VContainer;
using HuliacDev.Utils;
using ZLogger;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬 진입 시 선택된 레벨과 게임 결과(성공/실패)에 맞는 영상을 재생함. 파일명은
    /// "{Constants.Files.ResultVideoPrefix}-{레벨}-{Success|Fail}.mp4" 규칙을 따름(예: 레벨 2 성공 = "4-2-Success.mp4"). 레벨 3처럼 실패 원인별 영상이 있으면
    /// GameResultStore.FailVideoSuffix에 따라 "4-3-Fail-O2.mp4"처럼 고름.
    /// 반복 재생은 하지 않으며, 재생이 끝나면 AI 연출(ResultFlowController.PlayAiSequence)로 넘어감.
    /// 레벨 1은 로켓이 다 보이는 동안만 로켓 왼쪽에 체험자가 고른 블록 값('엔진: 고체 로켓 (+7)'처럼, 블록 순서대로 엔진·탑승 물체·연료량)을
    /// 로켓 쪽으로 모이는 선과 함께 띄움(level1RocketLabels) — 성공은 조립이 끝난 뒤부터, 실패는 부서지기 전까지(4_Result.json).
    /// </summary>
    public class ResultVideoPanel : MonoBehaviour, ISceneVideoReadiness
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private ResultFlowController flowController;
        [Tooltip("레벨 1 결과 영상의 로켓 왼쪽 글자·선 묶음 — 로켓이 다 보이는 동안만 알파를 1로 둠")]
        [SerializeField] private CanvasGroup level1RocketLabels;
        [Tooltip("블록 순서(엔진·탑승 물체·연료량)대로, 체험자가 고른 값을 '이름: 값'으로 적을 글자")]
        [SerializeField] private TMP_Text[] level1RocketLabelTexts;

        private const int MinLevel = 1;
        private const int RocketLabelLevel = 1; // 로켓 글자를 띄우는 레벨
        private const string NotChosenText = "없음"; // 그 블록을 고르지 않았을 때(넘어가기·일부만 설정) 값 자리에 적는 말

        // 레벨 1 블록 순서(추진체 종류·탑재 장비·연료량)대로 로켓 글자에 붙이는 이름
        private static readonly string[] RocketLabelNames = { "엔진", "탑승 물체", "연료량" };

        private readonly UniTaskCompletionSource _readySignal = new UniTaskCompletionSource();
        private GameResultStore _resultStore;
        private SelectedLevelStore _selectedLevelStore;
        private ILogger<ResultVideoPanel> _logger;

        /// <summary> 레벨을 영상이 존재하는 범위(MinLevel~Constants.LastLevel)로 맞춤. </summary>
        internal static int ClampLevel(int level) => Mathf.Clamp(level, MinLevel, Constants.LastLevel);

        /// <summary>
        /// "{Constants.Files.ResultVideoPrefix}-{레벨}-{Success|Fail}.mp4" 규칙의 파일명. 실패이고 failSuffix가 있으면 실패 원인별 영상
        /// "{Constants.Files.ResultVideoPrefix}-{레벨}-Fail-{failSuffix}.mp4"(예: "4-3-Fail-O2.mp4"). level은 ClampLevel을 거친 값이어야 함.
        /// </summary>
        internal static string GetVideoFileName(int level, bool success, string failSuffix = null) =>
            success || string.IsNullOrEmpty(failSuffix)
                ? ZString.Concat(Constants.Files.ResultVideoPrefix, "-", level, "-", success ? "Success" : "Fail", ".mp4")
                : ZString.Concat(Constants.Files.ResultVideoPrefix, "-", level, "-Fail-", failSuffix, ".mp4");

        /// <summary>
        /// 레벨 1 '나의 코딩 결과' 블록(설계 단계, 블록 순서 = 추진체 종류·탑재 장비·연료량)에서 로켓 글자 문구를 만듦: '엔진: 고체 로켓 (+7)'처럼
        /// 이름과 값 블록 문구를 붙이고, 그 단계를 설정하지 않았으면 '엔진: 없음'. 값 블록이 없는 단계는 명령 블록 문구를 씀.
        /// </summary>
        internal static string[] BuildRocketLabelTexts(IReadOnlyList<DesignStep> playerDesign)
        {
            string[] texts = new string[RocketLabelNames.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                string value = playerDesign != null && i < playerDesign.Count ? playerDesign[i].Value ?? playerDesign[i].Command : null;
                texts[i] = ZString.Concat(RocketLabelNames[i], ": ", string.IsNullOrEmpty(value) ? NotChosenText : value);
            }
            return texts;
        }

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

        /// <summary> 레벨 1 로켓 글자를 숨긴 채 시작하고, 이번 판 결과(레벨·성공 여부·실패 원인)에 맞는 영상 재생을 시작함. </summary>
        private void Start()
        {
            if (level1RocketLabels) level1RocketLabels.alpha = 0f;
            else if (_logger != null) _logger.ZLogWarning($"[ResultVideoPanel] level1RocketLabels가 null이라 레벨 1 결과 영상에 로켓 글자를 띄울 수 없음.");

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

        /// <summary> VideoPlayer가 그리는 RenderTexture를 검게 지움(RenderTexture로 그리지 않으면 지울 것이 없어 그대로 둠). </summary>
        private void ClearTargetTexture()
        {
            RenderTexture target = videoPlayer.targetTexture;
            if (!target) return; // RenderTexture 모드가 아님(정상)

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previousActive;
        }

        /// <summary> 체험자가 고른 블록 값으로 레벨 1 로켓 글자를 채움. 글자 칸이 모자라면 경고하고 있는 칸만 채움. </summary>
        private void ApplyRocketLabelTexts()
        {
            if (_resultStore == null && _logger != null) _logger.ZLogWarning($"[ResultVideoPanel] resultStore가 null이라 레벨 1 로켓 글자의 값을 모두 '{NotChosenText}'으로 적음.");

            string[] texts = BuildRocketLabelTexts(_resultStore?.PlayerDesign);
            if (level1RocketLabelTexts == null || level1RocketLabelTexts.Length < texts.Length)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultVideoPanel] level1RocketLabelTexts가 {texts.Length}칸만큼 연결되지 않아 레벨 1 로켓 글자 일부를 채우지 못함.");
                if (level1RocketLabelTexts == null) return;
            }

            for (int i = 0; i < texts.Length && i < level1RocketLabelTexts.Length; i++)
            {
                if (level1RocketLabelTexts[i]) level1RocketLabelTexts[i].text = texts[i];
                else if (_logger != null) _logger.ZLogWarning($"[ResultVideoPanel] level1RocketLabelTexts[{i}]가 null이라 '{texts[i]}'을 적지 못함.");
            }
        }

        /// <summary>
        /// 레벨 1 로켓 글자를 로켓이 다 보이는 동안만 띄움. 성공 영상은 조립이 끝나는 시각부터 끝까지(마지막 화면에 로켓이 남음),
        /// 실패 영상은 처음부터 로켓이 부서지기 전 시각까지. 영상이 그 시각 전에 끝나면 끝난 때를 기준으로 함.
        /// </summary>
        private async UniTaskVoid ShowRocketLabelsAsync(bool success, CancellationToken token)
        {
            try
            {
                string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Result}";
                ResultSceneSettings settings = await JsonLoader.LoadAsync<ResultSceneSettings>(path, token, _logger);
                float fadeDuration = Mathf.Max(0f, settings.level1RocketLabelFadeDuration);

                if (success)
                {
                    float showTime = settings.level1RocketLabelSuccessShowTime;
                    await UniTask.WaitUntil(() => !videoPlayer.isPlaying || videoPlayer.time >= showTime, cancellationToken: token);
                    await FadeRocketLabelsAsync(1f, fadeDuration, token);
                }
                else
                {
                    float hideTime = settings.level1RocketLabelFailHideTime;
                    await FadeRocketLabelsAsync(1f, fadeDuration, token);
                    await UniTask.WaitUntil(() => !videoPlayer.isPlaying || videoPlayer.time >= hideTime, cancellationToken: token);
                    await FadeRocketLabelsAsync(0f, fadeDuration, token);
                }
            }
            catch (OperationCanceledException)
            {
                // 씬을 떠남(정상)
            }
        }

        /// <summary> 레벨 1 로켓 글자 묶음의 알파를 alpha까지 duration초 동안 바꿈. </summary>
        private UniTask FadeRocketLabelsAsync(float alpha, float duration, CancellationToken token)
        {
            return level1RocketLabels.DOFade(alpha, duration).SetUpdate(true).SetLink(level1RocketLabels.gameObject)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
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

            int level = SelectedLevelStore.LevelOrFallback(_selectedLevelStore, _logger, nameof(ResultVideoPanel));
            int clampedLevel = ClampLevel(level);
            if (clampedLevel != level && _logger != null)
            {
                _logger.ZLogWarning($"[ResultVideoPanel] SelectedLevel({level})이 영상이 존재하는 범위({MinLevel}~{Constants.LastLevel})를 벗어나 {clampedLevel}로 대체함.");
            }

            string fileName = ResolveVideoFileName(clampedLevel, success, _resultStore != null ? _resultStore.FailVideoSuffix : null);
            if (_logger != null) _logger.ZLogInformation($"[ResultVideoPanel] 레벨={clampedLevel}, 결과={(success ? "성공" : "실패")}. {fileName} 재생 중.");

            // 준비에 실패하거나 늦어져도 앞 체험자 결과 영상의 마지막 프레임이 보이지 않게 먼저 검게 지움
            ClearTargetTexture();

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

            if (clampedLevel == RocketLabelLevel && level1RocketLabels)
            {
                ApplyRocketLabelTexts();
                ShowRocketLabelsAsync(success, token).Forget();
            }

            if (!await VideoReadyGate.WaitUntilPlaybackEndsAsync(videoPlayer, token) && _logger != null)
            {
                _logger.ZLogWarning($"[ResultVideoPanel] {fileName}이(가) 영상 길이({videoPlayer.length:F1}초)보다 {VideoReadyGate.PlaybackEndMarginSeconds}초 넘게 끝나지 않아 AI 연출로 넘어감.");
            }
        }
    }
}
