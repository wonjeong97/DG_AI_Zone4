using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace DGAIZone.Intro
{
    /// <summary>
    /// 튜토리얼 이미지를 페이지 단위로 넘겨보는 슬라이더. Addressables에서 Tutorial1~7 스프라이트를 로드해 캐싱하고,
    /// 이미지의 좌/우 클릭 위치에 따라 이전/다음 페이지로 순환 이동함.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TutorialImageSlider : MonoBehaviour, IPointerClickHandler
    {
        private const int TotalPages = 7;
        private const string AddressPrefix = "Tutorial";

        [SerializeField] private TMP_Text pageText;

        // 페이지별로 1회만 Addressables에서 로드하고 이후에는 캐시된 핸들에서 반환. OnDestroy에서 모두 Release함.
        private readonly Dictionary<int, AsyncOperationHandle<Sprite>> _spriteHandles = new();

        private Image _image;
        private RectTransform _rectTransform;
        private int _currentIndex;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rectTransform = (RectTransform)transform;
        }

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

        /// <summary> 클릭 위치가 이미지의 좌측 절반이면 이전 페이지, 우측 절반이면 다음 페이지로 이동함. </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            Rect rect = _rectTransform.rect;
            float normalizedX = (localPoint.x - rect.xMin) / rect.width;

            if (normalizedX >= 0.5f) ShowNext();
            else ShowPrevious();
        }

        private void ShowNext()
        {
            _currentIndex = (_currentIndex + 1) % TotalPages;
            ShowPageAsync().Forget();
        }

        private void ShowPrevious()
        {
            _currentIndex = (_currentIndex - 1 + TotalPages) % TotalPages;
            ShowPageAsync().Forget();
        }

        /// <summary> 현재 페이지 번호에 맞는 스프라이트를 Addressables에서 로드(또는 캐시에서 반환)해 표시하고, 페이지 텍스트를 갱신함. </summary>
        private async UniTaskVoid ShowPageAsync()
        {
            int page = _currentIndex + 1;
            if (pageText) pageText.text = $"튜토리얼 ({page}/{TotalPages})";

            try
            {
                if (!_spriteHandles.TryGetValue(page, out AsyncOperationHandle<Sprite> handle))
                {
                    handle = Addressables.LoadAssetAsync<Sprite>($"{AddressPrefix}{page}");
                    _spriteHandles[page] = handle;
                }

                Sprite sprite = await handle.Task.AsUniTask().AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

                // 로딩 중 다른 페이지로 이동했다면(연속 클릭) 결과가 최신 페이지를 덮어쓰지 않도록 방지
                if (_currentIndex + 1 == page && handle.Status == AsyncOperationStatus.Succeeded) _image.sprite = sprite;
            }
            catch (OperationCanceledException) { }
        }
    }
}
