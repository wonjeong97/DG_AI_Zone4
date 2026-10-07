using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HuliacDev.Core;
using HuliacDev.UI;
using HuliacDev.Utils;
using ZLogger;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬의 화면 흐름 제어. 플레이어 결과 영상 재생이 끝나면(ResultVideoPanel) 화면 중앙에 'AI가 코딩중입니다...'를 띄웠다가 지우고,
    /// 우측 상단 AI 패널(ResultAiPanel)에서 정답 설계창과 성공 영상을 보여 준 뒤 컴플리트 패널로 페이드인함.
    /// 컴플리트 패널 제목은 미션 결과에 따라 "미션 완료!" 또는 "미션 실패!"로 표시함.
    /// 컴플리트 패널의 "다음 미션" 버튼은 방금 플레이한 레벨이 마지막 레벨이 아니면 2_LevelSelect로(다음 레벨을
    /// 고를 수 있도록), 마지막 레벨(Constants.LastLevel)이면 5_Outro로 전환하며 버튼 문구도 "종료하기"로 바뀜.
    /// 관리자 레벨 이동으로 시작한 판이면 레벨과 상관없이 타이틀로 돌아가 관리자 화면을 다시 엶.
    /// </summary>
    public class ResultFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup completePanel;
        [SerializeField] private Button completeNextButton;
        [SerializeField] private TMP_Text missionResultText; // Text_MissionComplete: 미션 결과에 따라 문구를 바꿈
        [SerializeField] private CanvasGroup aiCodingPanel; // 화면 중앙 'AI가 코딩중입니다...' 띠
        [SerializeField] private TMP_Text aiCodingText;
        [SerializeField] private ResultAiPanel aiPanel; // 우측 상단 AI 패널(정답 설계창 -> 성공 영상)
        [SerializeField] private ResultPlayerPanel playerPanel; // 좌측 하단 '나의 코딩 결과' 패널

        private const string EndButtonText = "종료하기";
        private const string MissionSuccessText = "미션 완료!";
        private const string MissionFailText = "미션 실패!";
        private const string AiCodingText = "AI가 코딩중입니다";
        private const string AiCodingDots = "..."; // 점 슬롯 3개 — AiCodingDotCycle과 맞춰야 함
        private const int AiCodingDotCycle = 4; // 점 0~3개 반복

        private SceneTransitionService _sceneTransition;
        private SelectedLevelStore _selectedLevelStore;
        private UnlockedLevelStore _unlockedLevelStore;
        private GameResultStore _resultStore;
        private AdminLevelJumpStore _levelJumpStore;
        private InactivityTimer _inactivityTimer;
        private ILogger<ResultFlowController> _logger;
        private SoundManager _soundManager;
        private bool _isBusy;
        private bool _isTimerPaused; // 이 씬이 비활동 타이머를 멈춰 둔 상태인지(재개를 한 번만 하기 위함)

        // 4_Result.json / 00_Common.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private ResultSceneSettings _sceneSettings = new ResultSceneSettings();
        private CommonSettings _commonSettings = new CommonSettings();

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 선택/잠금 해제 레벨 저장소, 미션 결과 저장소, 로거, 비활동 타이머, 효과음 매니저, 관리자 레벨 이동 저장소를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, SelectedLevelStore selectedLevelStore, UnlockedLevelStore unlockedLevelStore, GameResultStore resultStore, ILogger<ResultFlowController> logger, InactivityTimer inactivityTimer = null, SoundManager soundManager = null, AdminLevelJumpStore levelJumpStore = null)
        {
            _sceneTransition = sceneTransition;
            _selectedLevelStore = selectedLevelStore;
            _unlockedLevelStore = unlockedLevelStore;
            _resultStore = resultStore;
            _logger = logger;
            _inactivityTimer = inactivityTimer;
            _soundManager = soundManager;
            _levelJumpStore = levelJumpStore;
        }

        /// <summary>
        /// 초기 패널 상태(AI 코딩 안내·컴플리트 숨김)를 적용하고 버튼 이벤트를 연결함. 결과 영상과 AI 연출은 입력 없이 보는 구간이라
        /// 미션 결과 문구(컴플리트 패널)가 나올 때까지 비활동 타이머를 멈춤. 방금 플레이한 레벨(SelectedLevelStore)을
        /// 완료한 것으로 간주해 다음 레벨까지 잠금 해제하고(성공/실패 무관, 체험 자체를 진행도로 인정),
        /// 패널 제목을 미션 결과에 맞추고, 마지막 레벨이면 버튼 문구를 "종료하기"로 바꾼 뒤 연출 타이밍을 비동기로 불러옴.
        /// </summary>
        private void Start()
        {
            PauseInactivityTimer();
            PanelFader.ApplyState(completePanel, false, _logger);
            PanelFader.ApplyState(aiCodingPanel, false, _logger);
            ApplyMissionResultText();

            if (completeNextButton) completeNextButton.onClick.AddListener(OnCompleteNextClicked);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] completeNextButton이 null임.");

            int playedLevel = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;
            if (_unlockedLevelStore != null) _unlockedLevelStore.UnlockThrough(playedLevel);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] unlockedLevelStore가 null이라 다음 레벨을 잠금 해제할 수 없음.");

            if (playedLevel >= Constants.LastLevel) ApplyEndButtonText();

            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> completeNextButton의 자식 텍스트를 "종료하기"로 바꿈(마지막 레벨을 완료했을 때만 호출됨). </summary>
        private void ApplyEndButtonText()
        {
            if (!completeNextButton)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] completeNextButton이 null이라 버튼 문구를 바꿀 수 없음.");
                return;
            }

            if (ChildComponentFinder.TryGetInDirectChildren(completeNextButton.transform, out TMP_Text label)) label.text = EndButtonText;
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] completeNextButton의 직계 자식에 TMP_Text가 없어 문구를 바꾸지 못함.");
        }

        /// <summary> 테스트 전용: 인스펙터로 연결하는 컴플리트 패널 제목 텍스트를 넣음. </summary>
        internal void SetMissionResultTextForTest(TMP_Text text) => missionResultText = text;

        /// <summary> 컴플리트 패널(미션 완료/실패 문구)이 나타날 때 미션 결과에 맞는 효과음을 냄. 결과 저장소가 없으면 결과를 알 수 없어 경고만 남김. </summary>
        private void PlayMissionResultSound()
        {
            if (_resultStore == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] resultStore가 null이라 미션 결과 효과음을 낼 수 없음.");
                return;
            }

            string key = _resultStore.Result == MissionResult.Success ? Constants.Sounds.MissionSuccess : Constants.Sounds.MissionFailed;
            SoundEffects.Play(_soundManager, key, _logger);
        }

        /// <summary> 컴플리트 패널 제목(Text_MissionComplete)을 미션 결과에 맞춰 "미션 완료!" 또는 "미션 실패!"로 바꿈. </summary>
        internal void ApplyMissionResultText()
        {
            if (!missionResultText)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] missionResultText가 null이라 미션 결과 문구를 바꿀 수 없음.");
                return;
            }

            if (_resultStore == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] resultStore가 null이라 미션 결과를 알 수 없어 씬에 입력된 문구를 그대로 둠.");
                return;
            }

            missionResultText.text = _resultStore.Result == MissionResult.Success ? MissionSuccessText : MissionFailText;

            // TextHorizontalGradient는 활성화될 때 한 번만 색을 계산하므로, 문구를 바꾼 뒤 다시 적용해야 그라데이션이 유지됨
            if (missionResultText.TryGetComponent(out TextHorizontalGradient gradient)) gradient.Apply();
        }

        /// <summary>
        /// 4_Result.json(ResultSceneSettings)과 00_Common.json(CommonSettings)을 비동기로 로드하고, 씬 전환 페이드인이 끝나 화면이 다 보이면
        /// 좌측 하단 '나의 코딩 결과' 패널에 블록 쌓기를 시작시킴(블록 간격이 4_Result.json 값이라 로드 뒤에, 첫 블록이 검은 화면 뒤에서
        /// 붙어 버리지 않도록 전환이 끝난 뒤에 시작함).
        /// </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Result}";
            UniTask<ResultSceneSettings> settingsTask = JsonLoader.LoadAsync<ResultSceneSettings>(path, token);
            UniTask<CommonSettings> commonTask = CommonSettingsProvider.GetAsync(token);

            (_sceneSettings, _commonSettings) = await UniTask.WhenAll(settingsTask, commonTask);

            if (_sceneTransition != null) await UniTask.WaitWhile(() => _sceneTransition.IsTransitioning, cancellationToken: token);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] sceneTransition이 null이라 씬 전환이 끝나기를 기다리지 않고 나의 코딩 결과를 쌓음.");

            if (playerPanel) playerPanel.Play(_sceneSettings.designBlockInterval);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] playerPanel이 null이라 나의 코딩 결과를 쌓지 않음.");
        }

        /// <summary> 버튼 리스너를 해제함. </summary>
        private void OnDestroy()
        {
            if (completeNextButton) completeNextButton.onClick.RemoveListener(OnCompleteNextClicked);
            ResumeInactivityTimer(); // 연출 도중 씬이 사라져도 전역 타이머가 멈춘 채로 남지 않게 함
        }

        /// <summary> 비활동 타이머를 멈춤. 타이머가 주입되지 않았으면(테스트·디버그) 경고만 남김. </summary>
        private void PauseInactivityTimer()
        {
            if (!_inactivityTimer)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] inactivityTimer가 주입되지 않아 결과 연출 중 비활동 타이머를 멈출 수 없음.");
                return;
            }

            _inactivityTimer.Pause();
            _isTimerPaused = true;
        }

        /// <summary> 이 씬이 멈춰 둔 비활동 타이머를 재개함(재개 시점부터 다시 셈). 멈춘 적이 없거나 이미 재개했으면 아무것도 하지 않음. </summary>
        private void ResumeInactivityTimer()
        {
            if (!_isTimerPaused) return;

            _isTimerPaused = false;
            if (_inactivityTimer) _inactivityTimer.Resume();
        }

        /// <summary> 외부 트리거(플레이어 결과 영상 재생 종료, ResultVideoPanel)에서 AI 연출을 재생한 뒤 컴플리트 패널로 전환함. </summary>
        public void PlayAiSequence()
        {
            if (_isBusy) return;
            PlayAiSequenceAsync().Forget();
        }

        /// <summary>
        /// 컴플리트 패널의 다음 버튼 클릭 시 클릭음을 내고 화면 페이드와 함께 전환함. 방금 플레이한 레벨이 마지막 레벨(Constants.LastLevel)이면
        /// 아웃트로 씬으로, 아니면 다음 레벨을 고를 수 있도록 레벨 선택 씬으로 전환함.
        /// 관리자 레벨 이동으로 시작한 판이면 타이틀로 돌아가 관리자 화면을 다시 열도록 표시함.
        /// </summary>
        private void OnCompleteNextClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[ResultFlowController] sceneTransition이 null이라 씬을 로드할 수 없음.");
                return;
            }

            int playedLevel = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;
            string nextScene = playedLevel >= Constants.LastLevel ? Constants.Scenes.Outro : Constants.Scenes.LevelSelect;

            if (_levelJumpStore == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] levelJumpStore가 null이라 관리자 레벨 이동 판인지 알 수 없어 {nextScene}(으)로 이동함.");
            }
            else if (_levelJumpStore.IsLevelJump)
            {
                _levelJumpStore.OpenAdminOnTitle = true;
                nextScene = Constants.Scenes.Title;
                if (_logger != null) _logger.ZLogInformation($"[ResultFlowController] 관리자 레벨 이동 판이라 타이틀 관리자 화면으로 돌아감.");
            }

            _isBusy = true;
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            _sceneTransition.LoadSceneWithFadeAsync(nextScene, _commonSettings.sceneTransitionFadeDuration).Forget();
        }

        /// <summary> 'AI가 코딩중입니다...' 안내 -> AI 패널(정답 설계창 -> 성공 영상) -> 컴플리트 패널 페이드인(미션 결과 효과음) 순으로 진행함. </summary>
        private async UniTaskVoid PlayAiSequenceAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float duration = _sceneSettings.panelFadeDuration;
            try
            {
                await PlayAiCodingAsync(duration, token);

                if (aiPanel) await aiPanel.PlayAsync(duration, _sceneSettings.aiDesignHoldDuration, _sceneSettings.designBlockInterval, token);
                else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] aiPanel이 null이라 AI 패널 연출을 건너뜀.");

                if (completePanel)
                {
                    PlayMissionResultSound();
                    await PanelFader.FadeAsync(completePanel, 0f, 1f, duration, _logger, token);
                    PanelFader.ApplyState(completePanel, true, _logger);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[ResultFlowController] completePanel이 null이라 패널 전환 연출을 건너뜀.");
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _isBusy = false;
                ResumeInactivityTimer(); // 미션 결과 문구가 나왔으니 이제부터 입력 대기로 봄
            }
        }

        /// <summary> 화면 중앙 'AI가 코딩중입니다...' 띠를 페이드인 -> aiCodingHoldDuration초 유지(점 0~3개 반복) -> 페이드아웃함. </summary>
        private async UniTask PlayAiCodingAsync(float fadeDuration, CancellationToken token)
        {
            if (!aiCodingPanel)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] aiCodingPanel이 null이라 AI 코딩 안내를 건너뜀.");
                return;
            }

            using CancellationTokenSource dotCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            AnimateAiCodingDotsAsync(dotCts.Token).Forget();

            await PanelFader.FadeAsync(aiCodingPanel, 0f, 1f, fadeDuration, _logger, token);
            await UniTask.Delay(TimeSpan.FromSeconds(_sceneSettings.aiCodingHoldDuration), cancellationToken: token);
            await PanelFader.FadeAsync(aiCodingPanel, 1f, 0f, fadeDuration, _logger, token);
            PanelFader.ApplyState(aiCodingPanel, false, _logger);

            dotCts.Cancel();
        }

        /// <summary>
        /// 'AI가 코딩중입니다' 뒤 점 개수를 0 -> 3으로 반복함(취소될 때까지). 문자열은 점 3개를 포함한 채로 두고
        /// 보이는 글자 수만 바꿔 문구가 좌우로 흔들리지 않게 함(Zone1 결과 씬과 같은 방식).
        /// </summary>
        private async UniTaskVoid AnimateAiCodingDotsAsync(CancellationToken token)
        {
            if (!aiCodingText)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] aiCodingText가 null이라 점 애니메이션을 건너뜀.");
                return;
            }

            aiCodingText.text = AiCodingText + AiCodingDots;
            aiCodingText.ForceMeshUpdate();
            int baseLength = Mathf.Max(0, aiCodingText.textInfo.characterCount - AiCodingDots.Length);

            int dotCount = 0;
            try
            {
                while (true)
                {
                    aiCodingText.maxVisibleCharacters = baseLength + dotCount;
                    dotCount = (dotCount + 1) % AiCodingDotCycle;
                    await UniTask.Delay(_sceneSettings.aiCodingDotIntervalMs, cancellationToken: token);
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
