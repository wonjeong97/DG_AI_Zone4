using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Utils;
using ZLogger;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬의 화면 흐름 제어. 결과 영상 재생이 끝나면(ResultVideoPanel) 컴플리트 패널로 페이드인함.
    /// 컴플리트 패널의 "다음 미션" 버튼은 방금 플레이한 레벨이 마지막 레벨이 아니면 2_LevelSelect로(다음 레벨을
    /// 고를 수 있도록), 마지막 레벨(LastLevel)이면 5_Outro로 전환하며 버튼 문구도 "종료하기"로 바뀜.
    /// </summary>
    public class ResultFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup completePanel;
        [SerializeField] private Button completeNextButton;
        private readonly float panelFadeDuration = 0.4f; // 4_Result.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)
        private readonly float sceneFadeDuration = 0.5f; // 00_Common.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)

        private const int LastLevel = 4; // 이 레벨을 완료하면 다음 미션(LevelSelect) 대신 Outro로 감. 레벨이 늘어나면 이 값만 올리면 됨.
        private const string EndButtonText = "종료하기";

        private SceneTransitionService _sceneTransition;
        private SelectedLevelStore _selectedLevelStore;
        private UnlockedLevelStore _unlockedLevelStore;
        private ILogger<ResultFlowController> _logger;
        private bool _isBusy;

        // 4_Result.json / 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private ResultSceneSettings _sceneSettings;
        private CommonSettings _commonSettings;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 선택/잠금 해제 레벨 저장소, 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, SelectedLevelStore selectedLevelStore, UnlockedLevelStore unlockedLevelStore, ILogger<ResultFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _selectedLevelStore = selectedLevelStore;
            _unlockedLevelStore = unlockedLevelStore;
            _logger = logger;
        }

        /// <summary>
        /// 초기 패널 상태(컴플리트 숨김)를 적용하고 버튼 이벤트를 연결함. 방금 플레이한 레벨(SelectedLevelStore)을
        /// 완료한 것으로 간주해 다음 레벨까지 잠금 해제하고(성공/실패 무관, 체험 자체를 진행도로 인정),
        /// 마지막 레벨이면 버튼 문구를 "종료하기"로 바꾼 뒤 연출 타이밍을 비동기로 불러옴.
        /// </summary>
        private void Start()
        {
            ApplyPanelState(completePanel, false);

            if (completeNextButton) completeNextButton.onClick.AddListener(OnCompleteNextClicked);
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] completeNextButton이 null임.");

            int playedLevel = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;
            _unlockedLevelStore?.UnlockThrough(playedLevel);

            if (playedLevel >= LastLevel) ApplyEndButtonText();

            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> completeNextButton의 자식 텍스트를 "종료하기"로 바꿈(마지막 레벨을 완료했을 때만 호출됨). </summary>
        private void ApplyEndButtonText()
        {
            if (completeNextButton == null) return;

            TMP_Text label = completeNextButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = EndButtonText;
            else if (_logger != null) _logger.ZLogWarning($"[ResultFlowController] completeNextButton에 TMP_Text 자식이 없어 문구를 바꾸지 못함.");
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
                    await FadeCanvasGroupAsync(completePanel, 0f, 1f, duration, token);
                    ApplyPanelState(completePanel, true);
                }
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> DOTween으로 CanvasGroup 알파를 보간하는 페이드 핵심 로직. </summary>
        private async UniTask FadeCanvasGroupAsync(CanvasGroup group, float startAlpha, float endAlpha, float duration, CancellationToken token)
        {
            if (!group) return;
            if (duration <= 0f) duration = 0.4f;

            group.alpha = startAlpha;
            group.interactable = false;
            group.blocksRaycasts = false;

            await group.DOFade(endAlpha, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true) // Zone1과 동일하게 Time.timeScale과 무관하게 동작하도록 함
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
        }

        /// <summary> 패널의 표시 여부에 따라 알파와 상호작용 상태를 설정함. </summary>
        private void ApplyPanelState(CanvasGroup group, bool visible)
        {
            if (!group) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
