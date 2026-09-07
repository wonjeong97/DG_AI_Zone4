using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Utils;
using ZLogger;

namespace DGAIZone.Title
{
    /// <summary>
    /// 타이틀 씬의 화면 흐름 제어. 시작 버튼을 누르면 화면 페이드와 함께 인트로 씬으로 전환함.
    /// QR 안내(Image_QR)는 원래 색과 최소 알파 사이를 오가며 부드럽게 깜빡여 시선을 끔(Zone1 TitleSceneManager의 QR 블링크 효과와 동일).
    /// </summary>
    public class TitleFlowController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private float sceneFadeDuration = 0.5f; // 00_Common.json 로드 전까지의 폴백 기본값

        [Header("QR Blink")]
        [SerializeField] private CanvasGroup qrCanvasGroup; // Image_QR
        [SerializeField] private float qrFadeDuration = 1.2f; // 0_Title.json 로드 전까지의 폴백 기본값
        [SerializeField] private float qrBlinkMinAlpha = 0.3f; // 0_Title.json 로드 전까지의 폴백 기본값

        private SceneTransitionService _sceneTransition;
        private ILogger<TitleFlowController> _logger;
        private bool _isBusy;

        // 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함.
        // 씬 전환 페이드 시간은 다른 씬들과 마찬가지로 00_Common.json의 sceneTransitionFadeDuration을 공유해서 쓰며,
        // 씬별로 값이 갈리지 않도록 함(현장에서 페이드 시간을 한 곳만 바꾸면 전체 씬에 일관되게 반영됨).
        private CommonSettings _commonSettings;

        // 0_Title.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private TitleSceneSettings _sceneSettings;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, ILogger<TitleFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _logger = logger;
        }

        /// <summary> 버튼 이벤트를 연결하고, QR 블링크 연출을 시작하고, 00_Common.json/0_Title.json 연출 타이밍을 비동기로 불러옴. </summary>
        private void Start()
        {
            if (startButton) startButton.onClick.AddListener(OnStartClicked);
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] startButton이 null임.");

            StartQrBlink();
            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// QR 안내를 원래 색(알파 1)에서 qrBlinkMinAlpha까지 qrFadeDuration초에 걸쳐 오가며 무한 반복(Yoyo)함.
        /// 0_Title.json 로드가 끝나기 전에는 인스펙터 폴백 값으로 먼저 시작하고, 로드가 끝나면 실제 값으로 다시 시작함.
        /// </summary>
        private void StartQrBlink()
        {
            if (!qrCanvasGroup) return;

            qrCanvasGroup.DOKill();
            qrCanvasGroup.alpha = 1f;
            qrCanvasGroup.DOFade(_sceneSettings?.qrBlinkMinAlpha ?? qrBlinkMinAlpha, _sceneSettings?.qrFadeDuration ?? qrFadeDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(qrCanvasGroup.gameObject);
        }

        /// <summary> 00_Common.json(CommonSettings)과 0_Title.json(TitleSceneSettings)을 비동기로 로드함. </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Title}";
            UniTask<TitleSceneSettings> sceneSettingsTask = JsonLoader.LoadAsync<TitleSceneSettings>(path, token);
            UniTask<CommonSettings> commonTask = CommonSettingsProvider.GetAsync(token);

            (_sceneSettings, _commonSettings) = await UniTask.WhenAll(sceneSettingsTask, commonTask);

            // 로드 전 폴백 값으로 이미 시작된 QR 블링크를 실제 튜닝 값으로 다시 시작함
            StartQrBlink();
        }

        /// <summary> 버튼 리스너 해제. </summary>
        private void OnDestroy()
        {
            if (startButton) startButton.onClick.RemoveListener(OnStartClicked);
        }

        /// <summary> 시작 버튼 클릭 시 화면 페이드와 함께 인트로 씬으로 전환함. </summary>
        private void OnStartClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[TitleFlowController] sceneTransition이 null이라 {Constants.Scenes.Intro} 씬을 로드할 수 없음.");
                return;
            }

            _isBusy = true;
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Intro, _commonSettings?.sceneTransitionFadeDuration ?? sceneFadeDuration).Forget();
        }
    }
}
