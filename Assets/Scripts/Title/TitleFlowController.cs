using System;
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
    /// 서버(QR 스캔) 연동 여부에 따라 QR 안내를 표시하고, 표시할 때만 원래 색과 최소 알파 사이를
    /// 오가며 부드럽게 깜빡임(Zone1 TitleSceneManager와 동일한 정책·효과).
    /// </summary>
    public class TitleFlowController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        private readonly float sceneFadeDuration = 0.5f; // 00_Common.json 로드 전까지의 폴백 기본값(JSON이 값을 결정하므로 인스펙터에는 노출하지 않음)

        [Header("QR Blink")]
        [SerializeField] private CanvasGroup qrCanvasGroup; // Image_QR

        private SceneTransitionService _sceneTransition;
        private VisitorInfoProvider _visitorInfoProvider;
        private ILogger<TitleFlowController> _logger;
        private bool _isBusy;

        // 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함.
        // 씬 전환 페이드 시간은 다른 씬들과 마찬가지로 00_Common.json의 sceneTransitionFadeDuration을 공유해서 쓰며,
        // 씬별로 값이 갈리지 않도록 함(현장에서 페이드 시간을 한 곳만 바꾸면 전체 씬에 일관되게 반영됨).
        private CommonSettings _commonSettings;

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 체험자 정보 제공자, 로거를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, VisitorInfoProvider visitorInfoProvider, ILogger<TitleFlowController> logger)
        {
            _sceneTransition = sceneTransition;
            _visitorInfoProvider = visitorInfoProvider;
            _logger = logger;
        }

        /// <summary> 버튼 이벤트를 연결하고, QR 표시 여부/블링크 연출과 00_Common.json 연출 타이밍을 비동기로 처리함. </summary>
        private void Start()
        {
            if (startButton) startButton.onClick.AddListener(OnStartClicked);
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] startButton이 null임.");

            // 서버 연동 여부 확인이 끝나기 전까지 QR이 잠깐 노출됐다 꺼지는 플리커를 방지하기 위해 먼저 숨겨둠
            if (qrCanvasGroup) qrCanvasGroup.gameObject.SetActive(false);

            CancellationToken token = this.GetCancellationTokenOnDestroy();
            ApplyQrVisibilityAsync(token).Forget();
            LoadCommonSettingsAsync(token).Forget();
        }

        /// <summary>
        /// 서버(QR 스캔) 연동 여부에 따라 QR 안내를 표시하고, 표시할 때만 천천히 깜빡임.
        /// 페이드 시간은 0_Title.json(TitleSceneSettings)에서 읽어와 재빌드 없이 조정 가능.
        /// </summary>
        private async UniTaskVoid ApplyQrVisibilityAsync(CancellationToken token)
        {
            if (!qrCanvasGroup || _visitorInfoProvider == null) return;

            try
            {
                bool isServerConnected = await _visitorInfoProvider.IsServerConnectedAsync(token);
                qrCanvasGroup.gameObject.SetActive(isServerConnected);

                if (!isServerConnected) return;

                string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Title}";
                TitleSceneSettings sceneSettings = await JsonLoader.LoadAsync<TitleSceneSettings>(path, token);

                // 반환된 Tween은 SetLink로 오브젝트 파괴 시 자동 정리되므로 별도 보관 없이 discard함
                _ = qrCanvasGroup.DOFade(sceneSettings.qrBlinkMinAlpha, sceneSettings.qrFadeDuration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetLink(qrCanvasGroup.gameObject);
            }
            catch (OperationCanceledException)
            {
                // 씬 전환 등으로 오브젝트가 파괴되어 취소된 경우 — 정상 종료
            }
        }

        /// <summary> 00_Common.json(CommonSettings)을 비동기로 로드함. </summary>
        private async UniTaskVoid LoadCommonSettingsAsync(CancellationToken token)
        {
            _commonSettings = await CommonSettingsProvider.GetAsync(token);
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
