using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Game.UI;
using HuliacDev.UI;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using VContainer;
using ZLogger;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬 우측 상단의 AI 패널(AI_Panel 프레임). 플레이어 결과 영상과 'AI가 코딩중입니다...' 안내가 끝나면 ResultFlowController가 PlayAsync를 호출함.
    /// 패널이 열리면 이번 판 문제의 정답 설계(GameResultStore.SolutionDesign)를 3_Game 설계창과 같은 블록 이미지로 하나씩 쌓아
    /// 완성하기까지 붙인 뒤(블록 간격은 4_Result.json의 designBlockInterval), 같은 레벨의 성공 영상("{Constants.Files.ResultVideoPrefix}-{레벨}-Success.mp4")을
    /// 처음부터 끝까지 재생함. 패널은 끝난 뒤에도 마지막 프레임으로 남음.
    /// 성공 영상이 처음 나타나면 패널 아래 '코드보기' 버튼도 나타나, 누르면(첫 재생 중이면 영상을 멈추고 완료 패널로 넘어감) 이미 쌓아 둔 정답 설계창으로 돌아가고(버튼은 '영상보기'),
    /// '영상보기'를 누르면 준비해 둔 성공 영상을 다시 불러오지 않고 처음부터 재생함.
    /// </summary>
    public class ResultAiPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panelGroup; // 패널 전체(AI_Panel 프레임)
        [SerializeField] private CanvasGroup designGroup; // 정답 설계창
        [SerializeField] private DesignPanel designPanel; // 정답 설계를 블록으로 쌓는 설계창(3_Game과 같은 DesignBlock 프리팹)
        [SerializeField] private CanvasGroup videoGroup; // 성공 영상(RawImage)
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private Button viewToggleButton; // 코드보기 ↔ 영상보기(직계 자식 TMP_Text의 문구를 바꿈)

        private const float OpenStartScale = 0.85f; // 패널이 열릴 때 이 크기에서 원래 크기로 커짐
        internal const string CodeViewText = "코드보기";
        internal const string VideoViewText = "영상보기";

        private GameResultStore _resultStore;
        private SelectedLevelStore _selectedLevelStore;
        private ILogger<ResultAiPanel> _logger;
        private SoundManager _soundManager;
        private VisitorInfoProvider _visitorInfoProvider;
        private TMP_Text _viewToggleLabel;
        private float _fadeDuration;
        private bool _isShowingCode; // 지금 설계창을 보여 주는 중(버튼 문구 '영상보기')
        private bool _isSwitching; // 설계창 ↔ 영상 교차 페이드 중(연타로 두 전환이 겹치지 않게 함)

        /// <summary> VContainer 의존성 주입. 정답 설계를 담은 게임 결과 저장소, 선택된 레벨 저장소, 로거, 효과음 매니저, 체험자 정보 제공자(행동 로그 주어)를 할당하고 설계창에 블록 생성용 리졸버를 주입함. </summary>
        [Inject]
        public void Construct(GameResultStore resultStore, SelectedLevelStore selectedLevelStore, IObjectResolver resolver, ILogger<ResultAiPanel> logger, SoundManager soundManager = null, VisitorInfoProvider visitorInfoProvider = null)
        {
            _resultStore = resultStore;
            _selectedLevelStore = selectedLevelStore;
            _logger = logger;
            _soundManager = soundManager;
            _visitorInfoProvider = visitorInfoProvider;

            if (designPanel) resolver.Inject(designPanel);
            else if (_logger != null) _logger.ZLogWarning($"[ResultAiPanel] designPanel이 null이라 정답 설계를 표시할 수 없음.");
        }

        /// <summary>
        /// 패널·설계창·영상과 코드보기 버튼을 숨긴 상태로 시작하고, 정답 설계에 맞춰 설계창 블록 묶음 폭을 정하고 시작하기 블록만 놓아 둠.
        /// </summary>
        private void Start()
        {
            PanelFader.ApplyState(panelGroup, false, _logger);
            PanelFader.ApplyState(designGroup, false, _logger);
            PanelFader.ApplyState(videoGroup, false, _logger);
            if (designPanel) ResultDesignPlayback.Prepare(designPanel, SolutionDesign()); // null이면 Construct에서 이미 경고함

            if (viewToggleButton)
            {
                viewToggleButton.gameObject.SetActive(false);
                viewToggleButton.onClick.AddListener(OnViewToggleClicked);
                if (!ChildComponentFinder.TryGetInDirectChildren(viewToggleButton.transform, out _viewToggleLabel) && _logger != null)
                    _logger.ZLogWarning($"[ResultAiPanel] viewToggleButton의 직계 자식에 TMP_Text가 없어 코드보기·영상보기 문구를 바꿀 수 없음.");
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[ResultAiPanel] viewToggleButton이 null이라 AI 코딩 결과를 다시 볼 수 없음.");
            }
        }

        /// <summary> 코드보기 버튼 연결을 해제함. </summary>
        private void OnDestroy()
        {
            if (viewToggleButton) viewToggleButton.onClick.RemoveListener(OnViewToggleClicked);
        }

        /// <summary> 결과 저장소의 정답 설계. 없거나 비어 있으면 경고를 남기고 빈 목록(시작하기·완성하기만 보임)을 반환함. </summary>
        private IReadOnlyList<DesignStep> SolutionDesign()
        {
            IReadOnlyList<DesignStep> steps = _resultStore != null ? _resultStore.SolutionDesign : null;
            if (steps != null && steps.Count > 0) return steps;

            if (_logger != null) _logger.ZLogWarning($"[ResultAiPanel] 정답 설계가 비어 있어 설계창을 빈 채로 표시함(3_Game을 거치지 않았거나 정답을 만들지 못함).");
            return Array.Empty<DesignStep>();
        }

        /// <summary>
        /// 패널을 열고 정답 설계를 블록으로 blockInterval초 간격으로 하나씩 쌓아 완성하기까지 붙인 뒤 designHoldDuration초 보여 주고,
        /// 성공 영상으로 교차 페이드해 끝까지 재생함.
        /// 영상은 패널이 열릴 때 미리 준비해 두며, 준비하지 못하면(videoPlayer 없음, 파일 없음, 준비 실패) 설계창까지만 보여 주고 끝냄(코드보기 버튼도 나오지 않음).
        /// 영상이 나타나면 코드보기 버튼을 보여 줌. 첫 재생 중에 코드보기를 누르면 영상이 멈춰 재생이 끝난 것으로 보고 반환함(완료 패널로 넘어감).
        /// </summary>
        public async UniTask PlayAsync(float fadeDuration, float designHoldDuration, float blockInterval, CancellationToken token)
        {
            _fadeDuration = fadeDuration;
            UniTask<bool> prepareTask = PrepareSuccessVideoAsync(token);

            PanelFader.ApplyState(designGroup, true, _logger);
            await OpenPanelAsync(fadeDuration, token);
            PanelFader.ApplyState(panelGroup, true, _logger);

            if (designPanel) await ResultDesignPlayback.StackAsync(designPanel, SolutionDesign(), true, blockInterval, _soundManager, _logger, token);

            // 4_Result.json에 음수를 넣으면 Delay가 예외를 내 완료 패널로 넘어가지 못하므로 0 이상으로 제한함
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, designHoldDuration)), cancellationToken: token);
            if (!await prepareTask) return;

            videoPlayer.Play();
            if (!await VideoReadyGate.WaitUntilFrameRenderedAsync(videoPlayer, VideoReadyGate.DefaultProgressThreshold, token) && _logger != null)
            {
                _logger.ZLogWarning($"[ResultAiPanel] AI 패널 성공 영상의 첫 화면이 {VideoReadyGate.FirstFrameTimeoutSeconds}초 안에 그려지지 않았지만 그대로 진행함.");
            }

            await UniTask.WhenAll(
                PanelFader.FadeAsync(designGroup, 1f, 0f, fadeDuration, _logger, token),
                PanelFader.FadeAsync(videoGroup, 0f, 1f, fadeDuration, _logger, token));
            PanelFader.ApplyState(designGroup, false, _logger);
            PanelFader.ApplyState(videoGroup, true, _logger);
            ShowViewToggle();

            // 코드보기로 영상을 멈춰도 isPlaying이 꺼져 여기서 끝난 것으로 봄
            if (!await VideoReadyGate.WaitUntilPlaybackEndsAsync(videoPlayer, token) && _logger != null)
            {
                _logger.ZLogWarning($"[ResultAiPanel] 성공 영상이 영상 길이({videoPlayer.length:F1}초)보다 {VideoReadyGate.PlaybackEndMarginSeconds}초 넘게 끝나지 않아 완료 패널로 넘어감.");
            }
        }

        /// <summary> 성공 영상을 보여 주는 상태로 코드보기 버튼을 나타냄. </summary>
        internal void ShowViewToggle()
        {
            if (!viewToggleButton) return; // null이면 Start에서 이미 경고함

            _isShowingCode = false;
            SetViewToggleText(CodeViewText);
            viewToggleButton.gameObject.SetActive(true);
        }

        /// <summary> 코드보기 버튼 문구를 바꿈(직계 자식 TMP_Text가 없으면 Start에서 이미 경고함). </summary>
        private void SetViewToggleText(string text)
        {
            if (_viewToggleLabel) _viewToggleLabel.text = text;
        }

        /// <summary> 코드보기면 정답 설계창으로, 영상보기면 성공 영상을 처음부터 재생하도록 전환함. 전환 중에 누른 것은 무시함. </summary>
        private void OnViewToggleClicked()
        {
            if (_isSwitching)
            {
                if (_logger != null) _logger.ZLogInformation($"[ResultAiPanel] 설계창·영상을 바꾸는 중이라 코드보기·영상보기 누름을 무시함.");
                return;
            }

            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            if (_logger != null) _logger.ZLogInformation($"[ResultAiPanel] {VisitorInfoProvider.LogSubjectOf(_visitorInfoProvider)} 결과 화면에서 {(_isShowingCode ? VideoViewText : CodeViewText)}를 누름 — {(_isShowingCode ? "AI 성공 영상을 처음부터 다시 봄" : "AI 정답 코드를 봄")}.");
            ToggleViewAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// 설계창 ↔ 성공 영상을 교차 페이드로 바꾸고 버튼 문구를 바꿈. 설계창으로 갈 때는 영상을 멈추고 다 숨긴 뒤 처음으로 되감아 두고(다음 영상보기 때 마지막 프레임이
        /// 비치지 않게), 영상으로 갈 때는 준비해 둔 영상을 다시 불러오지 않고 처음부터 재생해 첫 화면이 그려진 뒤 보여 줌. 블록은 이미 쌓여 있어 다시 쌓지 않음.
        /// </summary>
        private async UniTaskVoid ToggleViewAsync(CancellationToken token)
        {
            _isSwitching = true;
            try
            {
                if (_isShowingCode)
                {
                    _isShowingCode = false;
                    SetViewToggleText(CodeViewText);
                    if (videoPlayer)
                    {
                        if (!videoPlayer.isPrepared && _logger != null) _logger.ZLogWarning($"[ResultAiPanel] 성공 영상 준비가 풀려 있어 다시 준비하며 재생함.");
                        videoPlayer.time = 0;
                        videoPlayer.Play();
                        if (!await VideoReadyGate.WaitUntilFrameRenderedAsync(videoPlayer, VideoReadyGate.DefaultProgressThreshold, token) && _logger != null)
                            _logger.ZLogWarning($"[ResultAiPanel] 다시 재생한 성공 영상의 첫 화면이 {VideoReadyGate.FirstFrameTimeoutSeconds}초 안에 그려지지 않았지만 그대로 보여 줌.");
                    }
                    else if (_logger != null)
                    {
                        _logger.ZLogWarning($"[ResultAiPanel] videoPlayer가 null이라 성공 영상을 다시 재생할 수 없음.");
                    }

                    await CrossFadeAsync(designGroup, videoGroup, token);
                }
                else
                {
                    _isShowingCode = true;
                    SetViewToggleText(VideoViewText);
                    if (videoPlayer) videoPlayer.Pause();

                    await CrossFadeAsync(videoGroup, designGroup, token);
                    if (videoPlayer) videoPlayer.time = 0; // 페이드아웃 중에 되감으면 사라지는 영상이 첫 장면으로 튀므로 다 숨긴 뒤 되감음
                }
            }
            catch (OperationCanceledException)
            {
                // 전환 중 씬을 떠남 — 정상 종료
            }
            finally
            {
                _isSwitching = false;
            }
        }

        /// <summary> from을 숨기고 to를 보이도록 교차 페이드함. </summary>
        private async UniTask CrossFadeAsync(CanvasGroup from, CanvasGroup to, CancellationToken token)
        {
            await UniTask.WhenAll(
                PanelFader.FadeAsync(from, 1f, 0f, _fadeDuration, _logger, token),
                PanelFader.FadeAsync(to, 0f, 1f, _fadeDuration, _logger, token));
            PanelFader.ApplyState(from, false, _logger);
            PanelFader.ApplyState(to, true, _logger);
        }

        /// <summary> 테스트에서 씬 없이 설계창·영상 묶음과 코드보기 버튼을 넣을 때 씀(Start 전에 불러야 버튼이 연결됨). </summary>
        internal void SetUpViewToggleForTest(CanvasGroup design, CanvasGroup video, Button toggleButton)
        {
            designGroup = design;
            videoGroup = video;
            viewToggleButton = toggleButton;
        }

        /// <summary> 선택된 레벨의 성공 영상을 준비함. 준비되면 true, videoPlayer가 없거나 준비에 실패하면 로그를 남기고 false. </summary>
        private async UniTask<bool> PrepareSuccessVideoAsync(CancellationToken token)
        {
            if (!videoPlayer)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultAiPanel] videoPlayer가 null이라 AI 패널에서 성공 영상을 재생할 수 없음.");
                return false;
            }

            int level = ResultVideoPanel.ClampLevel(SelectedLevelStore.LevelOrFallback(_selectedLevelStore, _logger, nameof(ResultAiPanel)));
            string fileName = ResultVideoPanel.GetVideoFileName(level, true);

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
