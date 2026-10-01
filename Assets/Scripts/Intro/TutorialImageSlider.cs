using System;
using System.Collections.Generic;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Intro
{
    /// <summary>
    /// 튜토리얼 이미지를 페이지 단위로 넘겨보는 슬라이더. Addressables에서 Tutorial1~7 스프라이트를 로드해 캐싱하고,
    /// 이미지의 좌/우 클릭 위치에 따라 이전/다음 페이지로 이동함. 1페이지 좌측 클릭 시 순환하지 않고, 마지막 페이지 우측 클릭 시 완료 이벤트 발생.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TutorialImageSlider : MonoBehaviour, IPointerClickHandler
    {
        private const int TotalPages = 7;
        private const string AddressPrefix = "Tutorial";

        [SerializeField] private TMP_Text pageText;

        /// <summary> 마지막 페이지(7/7)에서 우측 영역을 터치했을 때 발생하는 완료 이벤트. </summary>
        public event Action OnTutorialCompleted;

        // 페이지별로 1회만 Addressables에서 로드하고 이후에는 캐시된 핸들에서 반환. OnDestroy에서 모두 Release함.
        private readonly Dictionary<int, AsyncOperationHandle<Sprite>> _spriteHandles = new();

        private Image _image;
        private ILogger<TutorialImageSlider> _logger;
        private RectTransform _rectTransform;
        private int _currentIndex;

        /// <summary> 현재 페이지 인덱스 (0: 1페이지 ~ 6: 7페이지). </summary>
        public int CurrentIndex => _currentIndex;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<TutorialImageSlider> logger)
        {
            _logger = logger;
        }

        /// <summary> 표시 대상 Image와 클릭 좌표 계산용 RectTransform을 캐싱함. </summary>
        private void Awake()
        {
            if (!TryGetComponent(out _image))
            {
                if (_logger != null) _logger.ZLogError($"[TutorialImageSlider] Image 컴포넌트가 없어 튜토리얼 이미지를 표시할 수 없음.");
                else Debug.LogError("[TutorialImageSlider] Image 컴포넌트가 없어 튜토리얼 이미지를 표시할 수 없음.");
            }
            _rectTransform = (RectTransform)transform;
        }

        /// <summary> 첫 페이지를 불러와 표시함. </summary>
        private void Start()
        {
            _currentIndex = 0;
            ShowPageAsync().Forget();
        }

        /// <summary> 로드해 둔 Addressables 핸들을 모두 해제함. </summary>
        private void OnDestroy()
        {
            foreach (AsyncOperationHandle<Sprite> handle in _spriteHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            _spriteHandles.Clear();
        }

        /// <summary> 클릭 위치가 이미지의 좌측 절반이면 이전 페이지, 우측 절반이면 다음 페이지(또는 완료)로 이동함. </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            Rect rect = _rectTransform.rect;
            float normalizedX = (localPoint.x - rect.xMin) / rect.width;

            if (normalizedX >= 0.5f) ShowNext();
            else ShowPrevious();
        }

        /// <summary> 다음 페이지로 이동함. 마지막 페이지(7/7)에서 호출 시 OnTutorialCompleted 이벤트를 발생시킴. </summary>
        public void ShowNext()
        {
            if (_currentIndex >= TotalPages - 1)
            {
                OnTutorialCompleted?.Invoke();
                return;
            }

            _currentIndex++;
            ShowPageAsync().Forget();
        }

        /// <summary> 이전 페이지로 이동함. 1페이지(1/7)에서는 더 이상 이전으로 가지 않고 머무름. </summary>
        public void ShowPrevious()
        {
            if (_currentIndex <= 0) return;

            _currentIndex--;
            ShowPageAsync().Forget();
        }

        /// <summary> 현재 페이지 번호에 맞는 스프라이트를 Addressables에서 로드(또는 캐시에서 반환)해 표시하고, 페이지 텍스트를 갱신함. </summary>
        private async UniTaskVoid ShowPageAsync()
        {
            int page = _currentIndex + 1;
            if (pageText) pageText.text = ZString.Format("체험 방법 ({0}/{1})", page, TotalPages);
            else if (_logger != null) _logger.ZLogWarning($"[TutorialImageSlider] pageText가 null이라 페이지 번호를 표시할 수 없음.");

            try
            {
                if (!_spriteHandles.TryGetValue(page, out AsyncOperationHandle<Sprite> handle))
                {
                    handle = Addressables.LoadAssetAsync<Sprite>(ZString.Concat(AddressPrefix, page));
                    _spriteHandles[page] = handle;
                }

                Sprite sprite = await handle.Task.AsUniTask().AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

                // 로딩 중 다른 페이지로 이동했다면(연속 클릭) 결과가 최신 페이지를 덮어쓰지 않도록 방지
                if (_currentIndex + 1 != page) return;

                if (handle.Status == AsyncOperationStatus.Succeeded && _image) _image.sprite = sprite;
                else if (_logger != null) _logger.ZLogWarning($"[TutorialImageSlider] 튜토리얼 이미지 '{AddressPrefix}{page}'를 표시할 수 없음(로드 상태={handle.Status}).");
            }
            catch (OperationCanceledException) { }
        }
    }
}
