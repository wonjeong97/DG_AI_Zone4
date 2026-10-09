using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using HuliacDev.Core;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DGAIZone.Outro
{
    /// <summary>
    /// 아웃트로 스토리 텍스트를 한 줄씩 올라오는 연출(공용 StoryLineAnimator, 인트로·레벨 선택과 같음)로 보여주는 컨트롤러.
    /// 화면 전환(페이드인)이 끝난 뒤 연출을 시작함.
    /// 연출 중 클릭하면 즉시 전체를 출력(스킵)하고, 연출이 끝나면 "처음으로" 버튼을 활성화함.
    /// 전체를 덮는 raycast 오브젝트(BG 등)에 부착하면 홈 버튼을 제외한 모든 클릭을 받음.
    /// </summary>
    public class OutroStoryController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private GameObject homeButton; // 연출이 끝나면 활성화할 "처음으로" 버튼

        private InactivityTimer _inactivityTimer;
        private SceneTransitionService _sceneTransition;
        private ILogger<OutroStoryController> _logger;
        private VisitorInfoProvider _visitorInfoProvider;
        private bool _isAnimating;
        private bool _skipRequested;

        // 00_Common.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private CommonSettings _commonSettings = new CommonSettings();

        /// <summary> VContainer 의존성 주입. 비활동 타이머(연출 중 일시정지·완료 후 재개), 체험자 이름 제공자, 씬 전환 서비스(페이드인이 끝나기를 기다림), 로거를 할당함. </summary>
        [Inject]
        public void Construct(VisitorInfoProvider visitorInfoProvider, SceneTransitionService sceneTransition, ILogger<OutroStoryController> logger, InactivityTimer inactivityTimer = null)
        {
            _visitorInfoProvider = visitorInfoProvider;
            _sceneTransition = sceneTransition;
            _logger = logger;
            _inactivityTimer = inactivityTimer;
        }

        /// <summary> 스토리 텍스트를 한 줄씩 올라오는 연출로 표시함. </summary>
        private void Start()
        {
            if (!storyText)
            {
                if (_logger != null) _logger.ZLogWarning($"[OutroStoryController] storyText가 null이라 스토리 연출을 진행할 수 없음.");
                return;
            }

            // 연출이 끝나기 전까지 "처음으로" 버튼을 숨김
            if (homeButton) homeButton.SetActive(false);
            else if (_logger != null) _logger.ZLogWarning($"[OutroStoryController] homeButton이 null이라 연출 후 처음으로 버튼을 표시할 수 없음.");

            // 페이드인 도중 전체 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠(AnimateAsync가 다시 전체 노출로 되돌린 뒤 진행함)
            storyText.ForceMeshUpdate();
            storyText.maxVisibleCharacters = 0;

            AnimateStoryAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 클릭 시 연출 중이면 즉시 전체를 출력함(스킵). </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isAnimating) _skipRequested = true;
        }

        /// <summary>
        /// 00_Common.json 연출 타이밍과 체험자 이름을 적용하고 화면 전환이 끝나기를 기다린 뒤, 공용 유틸로 스토리 텍스트를 한 줄씩 올리는 연출을 실행하고
        /// 끝나면 홈 버튼을 활성화함. 전환이 끝나기 전에 시작하면 화면이 밝아졌을 때 첫 줄이 이미 다 올라와 있음(인트로와 같은 처리).
        /// </summary>
        private async UniTaskVoid AnimateStoryAsync(CancellationToken token)
        {
            try
            {
                _commonSettings = await CommonSettingsProvider.GetAsync(token);

                await ApplyVisitorNameAsync(token);

                if (_sceneTransition != null) await _sceneTransition.WaitUntilIdleAsync(token);
                else if (_logger != null) _logger.ZLogWarning($"[OutroStoryController] sceneTransition이 null이라 씬 전환이 끝나기를 기다리지 않고 스토리를 시작함.");

                // 넘기기는 연출이 시작된 뒤부터 받음(전환 중 터치로 미리 넘겨지지 않게)
                _skipRequested = false;
                _isAnimating = true;
                await StoryLineAnimator.AnimateAsync(storyText,
                    _commonSettings.storyLineMoveDuration,
                    _commonSettings.storyLineInterval,
                    _commonSettings.storyLineYOffset,
                    () => _skipRequested, token, _inactivityTimer);

                OnFullyShown();
            }
            catch (OperationCanceledException) { }
            finally { _isAnimating = false; }
        }

        /// <summary> 텍스트가 완전히 노출된 시점 처리. "처음으로" 버튼을 활성화함. </summary>
        private void OnFullyShown()
        {
            if (homeButton) homeButton.SetActive(true);
        }

        /// <summary> 체험자 이름(관리자 화면 이름, 서버 모드면 QR로 확인한 이름)을 가져와 storyText의 자리표시자를 교체함(인트로와 동일한 규칙). </summary>
        private async UniTask ApplyVisitorNameAsync(CancellationToken token)
        {
            if (!storyText || _visitorInfoProvider == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[OutroStoryController] storyText 또는 visitorInfoProvider가 null이라 체험자 이름을 적용할 수 없음.");
                return;
            }

            string visitorName = await _visitorInfoProvider.GetNameAsync(token);
            storyText.text = PlaceholderFormatter.ReplaceVisitorName(storyText.text, visitorName);
        }
    }
}
