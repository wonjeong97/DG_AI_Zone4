using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 재료가 선택돼 있는 동안 오른쪽 화살표(Image_Arrow1..5)를 하나씩 켰다가 함께 끄는 안내 연출을 반복함.
    /// 알파는 CanvasGroup으로 바꿔 이미지 정점 리빌드 없이 GPU 블렌딩으로 처리함.
    /// </summary>
    public class RightArrowHint : MonoBehaviour
    {
        [SerializeField] private Image[] arrowImages; // Arrows/Image_Arrow1..5 순서

        private CanvasGroup[] _canvasGroups;
        private Sequence _sequence;
        private GameSceneSettings _sceneSettings = new GameSceneSettings(); // 3_Game.json 로드 전에는 설정 클래스의 기본값을 씀
        private ILogger<RightArrowHint> _logger;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<RightArrowHint> logger)
        {
            _logger = logger;
        }

        /// <summary> 화살표마다 CanvasGroup을 준비하고 숨김. </summary>
        private void Awake()
        {
            InitCanvasGroups();
        }

        /// <summary> 비어 있는 화살표 칸을 경고하고(씬 주입은 Awake 뒤라 로그를 남기도록 Start에서 확인) 연출 타이밍(3_Game.json)을 불러옴. </summary>
        private void Start()
        {
            if (arrowImages != null)
            {
                for (int i = 0; i < arrowImages.Length; i++)
                {
                    if (!arrowImages[i] && _logger != null) _logger.ZLogWarning($"[RightArrowHint] arrowImages[{i}]가 비어 있어 그 화살표는 연출에서 빠짐.");
                }
            }

            LoadSettingsAsync().Forget();
        }

        /// <summary> 씬 안의 다른 컴포넌트와 공유하는 3_Game.json 설정을 불러옴. </summary>
        private async UniTaskVoid LoadSettingsAsync()
        {
            _sceneSettings = await GameSceneSettingsProvider.GetAsync(this.GetCancellationTokenOnDestroy());
        }

        /// <summary> 화살표 이미지마다 CanvasGroup을 찾거나 붙이고 알파를 0으로 맞춤. 비어 있는 칸은 건너뜀. </summary>
        private void InitCanvasGroups()
        {
            if (arrowImages == null) return;

            _canvasGroups = new CanvasGroup[arrowImages.Length];
            for (int i = 0; i < arrowImages.Length; i++)
            {
                if (!arrowImages[i]) continue;

                if (!arrowImages[i].TryGetComponent(out _canvasGroups[i]))
                {
                    _canvasGroups[i] = arrowImages[i].gameObject.AddComponent<CanvasGroup>();
                }
                _canvasGroups[i].alpha = 0f;
            }
        }

        /// <summary>
        /// 화살표를 순서대로 알파 0→1로 켜고, 다 켜진 상태로 rightArrowHoldDuration만큼 보여 준 뒤 모두 함께 끄고,
        /// 다시 같은 시간만큼 쉬었다가 반복함. 이미 재생 중이면 무시함.
        /// </summary>
        public void Play()
        {
            if (_sequence != null && _sequence.IsActive()) return;

            if (_canvasGroups == null || _canvasGroups.Length == 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[RightArrowHint] arrowImages가 비어 있어 화살표 안내 연출을 건너뜀.");
                return;
            }

            float stepDuration = _sceneSettings.rightArrowStepFadeDuration;
            float fadeOutDuration = _sceneSettings.rightArrowFadeOutDuration;
            float holdDuration = _sceneSettings.rightArrowHoldDuration;

            SetAllAlpha(0f);

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            for (int i = 0; i < _canvasGroups.Length; i++)
            {
                if (_canvasGroups[i]) _sequence.Append(_canvasGroups[i].DOFade(1f, stepDuration));
            }

            _sequence.AppendInterval(holdDuration);

            bool isFirstFadeOut = true;
            for (int i = 0; i < _canvasGroups.Length; i++)
            {
                if (!_canvasGroups[i]) continue;

                // 첫 화살표의 페이드아웃 뒤에 나머지를 Join해 모두 동시에 꺼지게 함
                Tween fadeOut = _canvasGroups[i].DOFade(0f, fadeOutDuration);
                if (isFirstFadeOut) _sequence.Append(fadeOut);
                else _sequence.Join(fadeOut);
                isFirstFadeOut = false;
            }

            _sequence.AppendInterval(holdDuration);
            _sequence.SetLoops(-1);
        }

        /// <summary> 반복 연출을 멈추고 화살표를 모두 알파 0으로 되돌림. </summary>
        public void Stop()
        {
            _sequence?.Kill();
            _sequence = null;
            SetAllAlpha(0f);
        }

        /// <summary> 준비된 화살표 CanvasGroup의 알파를 한꺼번에 설정함. </summary>
        private void SetAllAlpha(float alpha)
        {
            if (_canvasGroups == null) return;

            for (int i = 0; i < _canvasGroups.Length; i++)
            {
                if (_canvasGroups[i]) _canvasGroups[i].alpha = alpha;
            }
        }

        /// <summary> 오브젝트 파괴 시 반복 연출을 정리함. </summary>
        private void OnDestroy()
        {
            _sequence?.Kill();
        }
    }
}
