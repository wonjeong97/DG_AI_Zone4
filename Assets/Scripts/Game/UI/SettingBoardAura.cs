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
    /// 카드가 인식돼 Text_Matter(또는 Text_Material)에 값이 있는 동안 설정 보드(Image_SettingBoard) 테두리 바깥에 아우라를 띄우고,
    /// 알파를 1과 settingBoardAuraMinAlpha 사이로 부드럽게 오가게 함(값을 고르고 설정하기를 누르라는 안내). 값이 사라지면 짧게 페이드아웃함.
    /// 빛은 UI/PanelAura 셰이더가 그리고, 이 컴포넌트는 이미지 크기(픽셀)를 머티리얼 사본에 넘기고 CanvasGroup 알파만 조절함.
    /// IngredientSelectionController가 오른쪽 화살표 안내와 같은 시점에 Play/Stop을 부름.
    /// </summary>
    [RequireComponent(typeof(Image), typeof(CanvasGroup))]
    public class SettingBoardAura : MonoBehaviour
    {
        private const string AuraShaderName = "UI/PanelAura";
        private static readonly int RectSizeId = Shader.PropertyToID("_RectSize");

        private Image _image;
        private CanvasGroup _canvasGroup;
        private Material _material; // 이 이미지 전용 머티리얼 사본 — 크기 값을 머티리얼 에셋에 쓰지 않으려고 씀
        private Tween _tween;
        private bool _isPlaying;
        private ILogger<SettingBoardAura> _logger;

        // 3_Game.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private GameSceneSettings _sceneSettings = new GameSceneSettings();

        /// <summary> 아우라가 켜져 있는지(Play 뒤 Stop 전). </summary>
        internal bool IsPlaying => _isPlaying;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<SettingBoardAura> logger)
        {
            _logger = logger;
        }

        /// <summary> 아우라를 숨긴 채 시작하고, 이 이미지 전용 머티리얼 사본에 이미지 크기를 넘김. </summary>
        private void Awake()
        {
            TryGetComponent(out _image);
            TryGetComponent(out _canvasGroup);

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _material = new Material(_image.material) { name = "PanelAura (Instance)" };
            _image.material = _material;
            ApplyRectSize();
        }

        /// <summary> 머티리얼이 아우라 셰이더가 아니면 경고하고(씬 주입은 Awake 뒤라 Start에서 확인), 연출 시간(3_Game.json)을 불러옴. </summary>
        private void Start()
        {
            if (_material.shader.name != AuraShaderName && _logger != null)
            {
                _logger.ZLogWarning($"[SettingBoardAura] 이미지 머티리얼의 셰이더가 {AuraShaderName}이 아니라({_material.shader.name}) 아우라 대신 이미지가 그대로 보일 수 있음.");
            }

            LoadSettingsAsync().Forget();
        }

        /// <summary> 씬 안의 다른 컴포넌트와 공유하는 3_Game.json 설정을 불러옴. </summary>
        private async UniTaskVoid LoadSettingsAsync()
        {
            _sceneSettings = await GameSceneSettingsProvider.GetAsync(this.GetCancellationTokenOnDestroy());
        }

        /// <summary> 이미지 크기가 바뀌면(해상도·레이아웃) 셰이더가 쓰는 픽셀 크기도 바꿈. </summary>
        private void OnRectTransformDimensionsChange()
        {
            ApplyRectSize();
        }

        /// <summary> 이미지 사각형 크기(캔버스 픽셀)를 머티리얼 사본에 넘김. Awake 전에 불리면(머티리얼 준비 전) 건너뜀. </summary>
        private void ApplyRectSize()
        {
            if (!_material) return; // Awake에서 다시 부름(정상)

            Vector2 size = ((RectTransform)transform).rect.size;
            _material.SetVector(RectSizeId, new Vector4(size.x, size.y, 0f, 0f));
        }

        /// <summary> 아우라를 페이드인한 뒤 1과 최소 알파 사이를 계속 오가게 함. 이미 켜져 있으면 무시함. </summary>
        public void Play()
        {
            if (_isPlaying) return;
            _isPlaying = true;

            float pulseDuration = Mathf.Max(0f, _sceneSettings.settingBoardAuraPulseDuration);
            float minAlpha = Mathf.Clamp01(_sceneSettings.settingBoardAuraMinAlpha);

            _tween?.Kill();
            _tween = _canvasGroup.DOFade(1f, pulseDuration)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    _tween = _canvasGroup.DOFade(minAlpha, pulseDuration)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetUpdate(true)
                        .SetLink(gameObject);
                });
        }

        /// <summary> 아우라를 짧게 페이드아웃해 끔. 이미 꺼져 있으면 무시함. </summary>
        public void Stop()
        {
            if (!_isPlaying) return;
            _isPlaying = false;

            _tween?.Kill();
            _tween = _canvasGroup.DOFade(0f, Mathf.Max(0f, _sceneSettings.settingBoardAuraFadeOutDuration))
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        /// <summary> 오브젝트 파괴 시 연출과 머티리얼 사본을 정리함. </summary>
        private void OnDestroy()
        {
            _tween?.Kill();
            if (_material) Destroy(_material);
        }
    }
}
