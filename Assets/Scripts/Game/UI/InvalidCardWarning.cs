using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 현재 레벨·단계에서 받지 않는 카드가 인식됐을 때의 경고 연출. 이 오브젝트(Image_Warning)의 CanvasGroup을 페이드인하고
    /// 게임 패널을 좌우로 흔든 뒤 warningHoldDuration만큼 보여 주고 다시 숨김. 연달아 호출되면 진행 중이던 연출을 취소하고 처음부터 다시 시작함.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class InvalidCardWarning : MonoBehaviour
    {
        private const int ShakeCount = 3;

        [SerializeField] private RectTransform shakeTarget; // 경고와 함께 좌우로 흔들 패널(GamePanel)

        private CanvasGroup _panel;
        private Sequence _shakeSequence;
        private CancellationTokenSource _cts;
        private GameSceneSettings _sceneSettings = new GameSceneSettings(); // 3_Game.json 로드 전에는 설정 클래스의 기본값을 씀
        private ILogger<InvalidCardWarning> _logger;

        /// <summary> 경고 연출이 진행 중인지 여부. </summary>
        internal bool IsShowing => _cts != null;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<InvalidCardWarning> logger)
        {
            _logger = logger;
        }

        /// <summary> 경고 패널 CanvasGroup을 찾아 숨긴 상태로 시작함. </summary>
        private void Awake()
        {
            if (TryGetComponent(out _panel))
            {
                _panel.alpha = 0f;
                _panel.interactable = false;
                _panel.blocksRaycasts = false;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[InvalidCardWarning] CanvasGroup이 없어 경고 표시가 비활성화됨.");
            }
        }

        /// <summary> 연출 타이밍(3_Game.json)을 불러옴. </summary>
        private void Start()
        {
            LoadSettingsAsync().Forget();
        }

        /// <summary> 씬 안의 다른 컴포넌트와 공유하는 3_Game.json 설정을 불러옴. </summary>
        private async UniTaskVoid LoadSettingsAsync()
        {
            _sceneSettings = await GameSceneSettingsProvider.GetAsync(this.GetCancellationTokenOnDestroy());
        }

        /// <summary> 경고 연출을 시작함(끝날 때까지 기다리지 않음). </summary>
        public void Show()
        {
            ShowAsync().Forget();
        }

        /// <summary>
        /// 경고를 페이드인하고 패널을 흔든 뒤 잠시 보여 주고 페이드아웃함. 이전 호출이 진행 중이면 그 대기까지 확실히 취소한 뒤 새로 시작함
        /// (트윈만 Kill하면 이전 호출의 UniTask.Delay가 남아 뒤늦게 경고를 숨기는 레이스가 생김). 취소되면 조용히 끝남.
        /// </summary>
        internal async UniTask ShowAsync()
        {
            if (!_panel)
            {
                if (_logger != null) _logger.ZLogWarning($"[InvalidCardWarning] CanvasGroup이 없어 잘못된 카드 경고를 표시할 수 없음.");
                return;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            _cts = cts;
            CancellationToken token = cts.Token;
            float fadeDuration = _sceneSettings.warningFadeDuration;

            try
            {
                _shakeSequence?.Kill();
                if (shakeTarget) shakeTarget.anchoredPosition = Vector2.zero;

                _panel.interactable = false;
                _panel.blocksRaycasts = false;
                await _panel.DOFade(1f, fadeDuration).SetUpdate(true)
                    .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);

                await ShakeAsync(token);

                await UniTask.Delay(TimeSpan.FromSeconds(_sceneSettings.warningHoldDuration), DelayType.UnscaledDeltaTime, cancellationToken: token);

                await _panel.DOFade(0f, fadeDuration).SetUpdate(true)
                    .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                // 더 최신 경고 연출이 이미 시작되어 필드가 교체됐다면 그 CTS는 건드리지 않음
                if (_cts == cts)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }
        }

        /// <summary> shakeTarget을 좌우로 ShakeCount번 왕복시킨 뒤 원위치로 되돌림. </summary>
        private UniTask ShakeAsync(CancellationToken token)
        {
            if (!shakeTarget)
            {
                if (_logger != null) _logger.ZLogWarning($"[InvalidCardWarning] shakeTarget이 null이라 흔들기 연출을 건너뜀.");
                return UniTask.CompletedTask;
            }

            Vector2 originalPos = shakeTarget.anchoredPosition;
            Vector2 offset = new Vector2(_sceneSettings.warningShakeAmount, 0f);
            float half = _sceneSettings.warningShakeCycleDuration / 2f;

            _shakeSequence?.Kill();
            _shakeSequence = DOTween.Sequence().SetUpdate(true);
            for (int i = 0; i < ShakeCount; i++)
            {
                _shakeSequence.Append(shakeTarget.DOAnchorPos(originalPos + offset, half).SetEase(Ease.InOutSine));
                _shakeSequence.Append(shakeTarget.DOAnchorPos(originalPos - offset, half).SetEase(Ease.InOutSine));
            }
            _shakeSequence.Append(shakeTarget.DOAnchorPos(originalPos, half).SetEase(Ease.InOutSine));

            return _shakeSequence.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
        }

        /// <summary> 오브젝트 파괴 시 진행 중인 연출과 취소 토큰을 정리함. </summary>
        private void OnDestroy()
        {
            _shakeSequence?.Kill();
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
