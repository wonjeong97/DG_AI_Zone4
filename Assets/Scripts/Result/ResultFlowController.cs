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
using HuliacDev.Utils;
using ZLogger;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬의 화면 흐름 제어. 결과 영상 재생이 끝나면(ResultVideoPanel) 컴플리트 패널로 페이드인함.
    /// 컴플리트 패널 제목은 미션 결과에 따라 "미션 완료!" 또는 "미션 실패!"로 표시함.
    /// 컴플리트 패널의 "다음 미션" 버튼은 방금 플레이한 레벨이 마지막 레벨이 아니면 2_LevelSelect로(다음 레벨을
    /// 고를 수 있도록), 마지막 레벨(LastLevel)이면 5_Outro로 전환하며 버튼 문구도 "종료하기"로 바뀜.
    /// </summary>
    public class ResultFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup completePanel;
        [SerializeField] private Button completeNextButton;
        [SerializeField] private TMP_Text missionResultText; // Text_MissionComplete: 미션 결과에 따라 문구를 바꿈
        private readonly float panelFadeDuration = 0.4f; // 4_Result.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)
        private readonly float sceneFadeDuration = 0.5f; // 00_Common.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)

        private const int LastLevel = 4; // 이 레벨을 완료하면 다음 미션(LevelSelect) 대신 Outro로 감. 레벨이 늘어나면 이 값만 올리면 됨.
        private const string EndButtonText = "종료하기";
        private const string MissionSuccessText = "미션 완료!";
        private const string MissionFailText = "미션 실패!";

        private SceneTransitionService _sceneTransition;
        private SelectedLevelStore _selectedLevelStore;
        private UnlockedLevelStore _unlockedLevelStore;
        private GameResultStore _resultStore;
        private ILogger<ResultFlowController> _logger;
        private bool _isBusy;

        // 4_Result.json / 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private ResultSceneSettings _sceneSettings;
        private CommonSettings _commonSettings;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 선택/잠금 해제 레벨 저장소, 미션 결과 저장소, 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, SelectedLevelStore selectedLevelStore, UnlockedLevelStore unlockedLevelStore, GameResultStore resultStore, ILogger<ResultFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _selectedLevelStore = selectedLevelStore;
            _unlockedLevelStore = unlockedLevelStore;
            _resultStore = resultStore;
            _logger = logger;
        }

        /// <summary>
        /// 초기 패널 상태(컴플리트 숨김)를 적용하고 버튼 이벤트를 연결함. 방금 플레이한 레벨(SelectedLevelStore)을
        /// 완료한 것으로 간주해 다음 레벨까지 잠금 해제하고(성공/실패 무관, 체험 자체를 진행도로 인정),
        /// 패널 제목을 미션 결과에 맞추고, 마지막 레벨이면 버튼 문구를 "종료하기"로 바꾼 뒤 연출 타이밍을 비동기로 불러옴.
        /// </summary>
        private void Start()
        {
            PanelFader.ApplyState(completePanel, false, _logger);
            ApplyMissionResultText();

            if (completeNextButton) completeNextButton.onClick.AddListener(OnCompleteNextClicked);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] completeNextButton이 null임.");

            int playedLevel = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;
            if (_unlockedLevelStore != null) _unlockedLevelStore.UnlockThrough(playedLevel);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] unlockedLevelStore가 null이라 다음 레벨을 잠금 해제할 수 없음.");

            if (playedLevel >= LastLevel) ApplyEndButtonText();

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

        /// <summary> 4_Result.json(ResultSceneSettings)과 00_Common.json(CommonSettings)을 비동기로 로드함. </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Result}";
            UniTask<ResultSceneSettings> settingsTask = JsonLoader.LoadAsync<ResultSceneSettings>(path, token);
            UniTask<CommonSettings> commonTask = CommonSettingsProvider.GetAsync(token);

            (_sceneSettings, _commonSettings) = await UniTask.WhenAll(settingsTask, commonTask);
        }

        /// <summary> 버튼 리스너를 해제함. </summary>
        private void OnDestroy()
        {
            if (completeNextButton) completeNextButton.onClick.RemoveListener(OnCompleteNextClicked);
        }

        /// <summary> 외부 트리거(결과 영상 재생 종료, ResultVideoPanel)에서 컴플리트 패널로 전환함. </summary>
        public void ShowCompletePanel()
        {
            if (_isBusy) return;
            SwitchToCompleteAsync().Forget();
        }

        /// <summary>
        /// 컴플리트 패널의 다음 버튼 클릭 시 화면 페이드와 함께 전환함. 방금 플레이한 레벨이 마지막 레벨(LastLevel)이면
        /// 아웃트로 씬으로, 아니면 다음 레벨을 고를 수 있도록 레벨 선택 씬으로 전환함.
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
            string nextScene = playedLevel >= LastLevel ? Constants.Scenes.Outro : Constants.Scenes.LevelSelect;

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(nextScene, _commonSettings?.sceneTransitionFadeDuration ?? sceneFadeDuration).Forget();
        }

        /// <summary> 컴플리트 패널을 페이드인함. </summary>
        private async UniTaskVoid SwitchToCompleteAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            float duration = _sceneSettings?.panelFadeDuration ?? panelFadeDuration;
            try
            {
                if (completePanel)
                {
                    await PanelFader.FadeAsync(completePanel, 0f, 1f, duration, _logger, token);
                    PanelFader.ApplyState(completePanel, true, _logger);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[ResultFlowController] completePanel이 null이라 패널 전환 연출을 건너뜀.");
                }
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }
    }
}
