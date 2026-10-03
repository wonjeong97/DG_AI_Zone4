using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.Video;
using VContainer;
using ZLogger;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬 우측 상단의 AI 패널(AI_Panel 프레임). 플레이어 결과 영상과 'AI가 코딩중입니다...' 안내가 끝나면 ResultFlowController가 PlayAsync를 호출함.
    /// 패널이 열리면 이번 판 문제의 정답 설계(GameResultStore.SolutionDesignItems, 3_Game 설계창과 같은 형식)를 한 줄씩 아래에서 올려 보여 준 뒤
    /// (StoryLineAnimator, 00_Common.json의 스토리 라인 타이밍 공유), 같은 레벨의 성공 영상("{videoFileNamePrefix}-{레벨}-Success.mp4")을
    /// 처음부터 끝까지 재생함. 패널은 끝난 뒤에도 마지막 프레임으로 남음.
    /// </summary>
    public class ResultAiPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panelGroup; // 패널 전체(AI_Panel 프레임)
        [SerializeField] private CanvasGroup designGroup; // 정답 설계창
        [SerializeField] private TMP_Text designText; // 정답 설계 항목을 줄바꿈으로 이어 한 줄씩 올리는 텍스트
        [SerializeField] private CanvasGroup videoGroup; // 성공 영상(RawImage)
        [SerializeField] private VideoPlayer videoPlayer;

        private const float OpenStartScale = 0.85f; // 패널이 열릴 때 이 크기에서 원래 크기로 커짐

        private GameResultStore _resultStore;
        private SelectedLevelStore _selectedLevelStore;
        private ILogger<ResultAiPanel> _logger;

        /// <summary> VContainer 의존성 주입. 정답 설계를 담은 게임 결과 저장소, 선택된 레벨 저장소, 로거를 할당함. </summary>
        [Inject]
        public void Construct(GameResultStore resultStore, SelectedLevelStore selectedLevelStore, ILogger<ResultAiPanel> logger)
        {
            _resultStore = resultStore;
            _selectedLevelStore = selectedLevelStore;
            _logger = logger;
        }

        /// <summary> 패널·설계창·영상을 숨긴 상태로 시작하고, 정답 설계 문구를 미리 채워 둠(줄 연출 전까지는 글자를 숨김). </summary>
        private void Start()
        {
            PanelFader.ApplyState(panelGroup, false, _logger);
            PanelFader.ApplyState(designGroup, false, _logger);
            PanelFader.ApplyState(videoGroup, false, _logger);
            FillDesignText();
        }

        /// <summary> 정답 설계 문구를 줄바꿈으로 이어 설계 텍스트에 넣고, 패널이 열려 줄 연출이 시작될 때까지 글자를 숨겨 둠. </summary>
        private void FillDesignText()
        {
            if (!designText)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultAiPanel] designText가 null이라 정답 설계를 표시할 수 없음.");
                return;
            }

            IReadOnlyList<string> items = _resultStore?.SolutionDesignItems;
            if (items == null || items.Count == 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultAiPanel] 정답 설계가 비어 있어 설계창을 빈 채로 표시함(3_Game을 거치지 않았거나 정답을 만들지 못함).");
                designText.text = string.Empty;
                return;
            }

            designText.text = string.Join("\n", items);
            designText.maxVisibleCharacters = 0; // StoryLineAnimator가 연출을 시작하며 전체 노출로 되돌림
        }

        /// <summary>
        /// 패널을 열고 정답 설계를 한 줄씩 올린 뒤 designHoldDuration초 보여 주고, 성공 영상으로 교차 페이드해 끝까지 재생함.
        /// 영상은 패널이 열릴 때 미리 준비해 두며, 준비하지 못하면(videoPlayer 없음, 파일 없음, 준비 실패) 설계창까지만 보여 주고 끝냄.
        /// </summary>
        public async UniTask PlayAsync(float fadeDuration, float designHoldDuration, CancellationToken token)
        {
            UniTask<bool> prepareTask = PrepareSuccessVideoAsync(token);

            PanelFader.ApplyState(designGroup, true, _logger);
            await OpenPanelAsync(fadeDuration, token);
            PanelFader.ApplyState(panelGroup, true, _logger);

            CommonSettings common = await CommonSettingsProvider.GetAsync(token);
            await StoryLineAnimator.AnimateAsync(designText,
                common.storyLineMoveDuration,
                common.storyLineInterval,
                common.storyLineYOffset,
                null, token);

            await UniTask.Delay(TimeSpan.FromSeconds(designHoldDuration), cancellationToken: token);
            if (!await prepareTask) return;

            videoPlayer.Play();
            await VideoReadyGate.WaitUntilFrameRenderedAsync(videoPlayer, VideoReadyGate.DefaultProgressThreshold, token);

            await UniTask.WhenAll(
                PanelFader.FadeAsync(designGroup, 1f, 0f, fadeDuration, _logger, token),
                PanelFader.FadeAsync(videoGroup, 0f, 1f, fadeDuration, _logger, token));
            PanelFader.ApplyState(designGroup, false, _logger);
            PanelFader.ApplyState(videoGroup, true, _logger);

            if (!await VideoReadyGate.WaitUntilPlaybackEndsAsync(videoPlayer, token) && _logger != null)
            {
                _logger.ZLogWarning($"[ResultAiPanel] 성공 영상이 영상 길이({videoPlayer.length:F1}초)보다 {VideoReadyGate.PlaybackEndMarginSeconds}초 넘게 끝나지 않아 완료 패널로 넘어감.");
            }
        }

        /// <summary> 선택된 레벨의 성공 영상을 준비함. 준비되면 true, videoPlayer가 없거나 준비에 실패하면 로그를 남기고 false. </summary>
        private async UniTask<bool> PrepareSuccessVideoAsync(CancellationToken token)
        {
            if (!videoPlayer)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultAiPanel] videoPlayer가 null이라 AI 패널에서 성공 영상을 재생할 수 없음.");
                return false;
            }

            int level = ResultVideoPanel.ClampLevel(_selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1);
            string fileName = ResultVideoPanel.GetVideoFileName(level, true);
            if (_logger != null) _logger.ZLogInformation($"[ResultAiPanel] AI 패널 성공 영상 {fileName} 준비 중.");

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = ResultVideoPanel.GetVideoPath(fileName);
            videoPlayer.isLooping = false;

            bool prepared = await VideoReadyGate.PrepareAsync(videoPlayer, VideoReadyGate.DefaultPrepareTimeoutSeconds, token);
            if (!prepared && _logger != null)
            {
                _logger.ZLogError($"[ResultAiPanel] {fileName}을(를) 준비하지 못함(파일 없음·재생 오류·{VideoReadyGate.DefaultPrepareTimeoutSeconds}초 초과). 설계창을 그대로 두고 완료 패널로 넘어감.");
            }
            return prepared;
        }

        /// <summary> 패널을 페이드인하면서 OpenStartScale에서 원래 크기로 살짝 튀듯이 키워 "열리는" 느낌을 줌. </summary>
        private UniTask OpenPanelAsync(float duration, CancellationToken token)
        {
            if (!panelGroup)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultAiPanel] panelGroup이 null이라 패널 열기 연출을 건너뜀.");
                return UniTask.CompletedTask;
            }

            Transform panel = panelGroup.transform;
            panel.localScale = Vector3.one * OpenStartScale;
            UniTask scaleTask = panel.DOScale(1f, duration).SetEase(Ease.OutBack).SetUpdate(true)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);

            return UniTask.WhenAll(PanelFader.FadeAsync(panelGroup, 0f, 1f, duration, _logger, token), scaleTask);
        }
    }
}
