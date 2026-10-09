using System;
using System.Collections.Generic;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using HuliacDev.UI;
using Microsoft.Extensions.Logging;
using R3;
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
    /// 튜토리얼 이미지를 페이지 단위로 넘겨보는 슬라이더. 씬이 시작되면 Addressables에서 Tutorial1~7 스프라이트를 모두 미리 로드해 캐싱하고,
    /// 이미지의 좌/우 클릭 위치에 따라 이전/다음 페이지로 이동함. 1페이지 좌측 클릭 시 순환하지 않고, 마지막 페이지 우측 클릭 시 완료 이벤트 발생.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TutorialImageSlider : MonoBehaviour, IPointerClickHandler
    {
        private const int TotalPages = 7;
        private const string AddressPrefix = "Tutorial";

        [SerializeField] private TMP_Text pageText;

        private readonly Subject<Unit> _tutorialCompleted = new Subject<Unit>();

        /// <summary> 마지막 페이지(7/7)에서 우측 영역을 터치했을 때 값을 내보내는 완료 스트림. </summary>
        public Observable<Unit> TutorialCompleted => _tutorialCompleted;

        // 페이지별로 1회만 Addressables에서 로드하고 이후에는 캐시된 핸들에서 반환(로드에 실패한 핸들은 해제하고 빼서 다음에 다시 로드). OnDestroy에서 모두 Release함.
        private readonly Dictionary<int, AsyncOperationHandle<Sprite>> _spriteHandles = new();

        private Image _image;
        private ILogger<TutorialImageSlider> _logger;
        private SoundManager _soundManager;
        private RectTransform _rectTransform;
        private int _currentIndex;

        /// <summary> 현재 페이지 인덱스 (0: 1페이지 ~ 6: 7페이지). </summary>
        public int CurrentIndex => _currentIndex;

        /// <summary> VContainer 의존성 주입. 로거와 효과음 매니저를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<TutorialImageSlider> logger, SoundManager soundManager = null)
        {
            _logger = logger;
            _soundManager = soundManager;
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

        /// <summary> 넘길 때 이미지가 늦게 바뀌지 않도록 모든 페이지 로드를 시작하고 첫 페이지를 표시함. </summary>
        private void Start()
        {
            for (int page = 1; page <= TotalPages; page++) GetOrLoadHandle(page);

            _currentIndex = 0;
            ShowPageAsync().Forget();
        }

        /// <summary> page번째 튜토리얼 이미지의 로드 핸들을 캐시에서 꺼내고, 없으면 Addressables 로드를 시작해 캐시에 넣고 반환함. </summary>
        private AsyncOperationHandle<Sprite> GetOrLoadHandle(int page)
        {
            if (_spriteHandles.TryGetValue(page, out AsyncOperationHandle<Sprite> handle)) return handle;

            handle = Addressables.LoadAssetAsync<Sprite>(ZString.Concat(AddressPrefix, page));
            _spriteHandles[page] = handle;
            return handle;
        }

        /// <summary> 완료 스트림과 로드해 둔 Addressables 핸들을 모두 해제함. </summary>
        private void OnDestroy()
        {
            _tutorialCompleted.Dispose();

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

        /// <summary> 클릭음을 내고 다음 페이지로 이동함. 마지막 페이지(7/7)에서 호출 시 TutorialCompleted로 완료를 알림. </summary>
        public void ShowNext()
        {
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);

            if (_currentIndex >= TotalPages - 1)
            {
                _tutorialCompleted.OnNext(Unit.Default);
                return;
            }

            _currentIndex++;
            ShowPageAsync().Forget();
        }

        /// <summary> 클릭음을 내고 이전 페이지로 이동함. 1페이지(1/7)에서는 더 이상 이전으로 가지 않고 소리 없이 머무름. </summary>
        public void ShowPrevious()
        {
            if (_currentIndex <= 0) return;

            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
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
                AsyncOperationHandle<Sprite> handle = GetOrLoadHandle(page);
                Sprite sprite = await handle.Task.AsUniTask().AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

                // 같은 핸들을 기다리던 다른 호출이 로드 실패를 보고 먼저 해제했으면 핸들을 더 읽을 수 없음(경고는 그쪽이 남겨 여기서는 남기지 않음)
                if (!handle.IsValid()) return;

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    if (_logger != null) _logger.ZLogWarning($"[TutorialImageSlider] 튜토리얼 이미지 '{AddressPrefix}{page}'를 불러오지 못함(로드 상태={handle.Status}). 다음에 이 페이지로 오면 다시 불러옴.");

                    // 같은 핸들을 기다리던 다른 호출이 이미 뺐으면 다시 해제하지 않음
                    if (_spriteHandles.TryGetValue(page, out AsyncOperationHandle<Sprite> cached) && cached.Equals(handle))
                    {
                        _spriteHandles.Remove(page);
                        Addressables.Release(handle);
                    }
                    return;
                }

                // 로딩 중 다른 페이지로 이동했다면(연속 클릭) 결과가 최신 페이지를 덮어쓰지 않도록 방지
                if (_currentIndex + 1 != page) return;

                if (_image) _image.sprite = sprite; // null이면 Awake에서 이미 오류를 남김
            }
            catch (OperationCanceledException)
            {
                // 로딩 중 씬 전환 등으로 오브젝트가 파괴되어 취소된 경우 — 정상 종료
            }
        }
    }
}
