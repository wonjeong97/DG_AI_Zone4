using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Game
{
    /// <summary>
    /// 게임 씬의 화면 흐름 제어. 씬 진입 시 게임 패널을 보여주고, 스토리 버튼으로 스토리 패널을 열며, 스토리 표시 중 화면을 클릭하면 게임 패널로 돌아옴.
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup storyPanel;
        [SerializeField] private CanvasGroup gamePanel;
        [SerializeField] private Button storyButton;
        [SerializeField] private float panelFadeDuration = 0.4f;

        [Header("Story Level")]
        [SerializeField] private Image storyImage;          // Image_Story
        [SerializeField] private GameObject[] storyLevels;  // Story_Level1..5 순서
        [SerializeField] private int selectedLevel = 1;     // 활성화된 레벨(1부터). 현재는 1레벨만 존재함.

        private ILogger<GameFlowController> _logger;
        private bool _isBusy;
        private AsyncOperationHandle<Sprite> _storyImageHandle;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<GameFlowController> logger)
        {
            _logger = logger;
        }

        /// <summary> 초기 패널 상태(게임 표시, 스토리 숨김)를 적용하고 활성 레벨 스토리를 설정한 뒤 버튼 이벤트를 연결함. </summary>
        private void Start()
        {
            ApplyPanelState(gamePanel, true);
            ApplyPanelState(storyPanel, false);

            SetupStoryLevel();

            if (storyButton) storyButton.onClick.AddListener(OnStoryClicked);
            else if (_logger != null) _logger.ZLogWarning($"[GameFlowController] storyButton is null.");
        }

        /// <summary> 스토리 패널이 표시된 상태에서 화면 아무 곳이나 마우스/터치로 누르면 게임 패널로 전환함. </summary>
        private void Update()
        {
            if (_isBusy) return;
            if (storyPanel == null || !storyPanel.interactable) return;

            if (IsPointerPressed())
            {
                SwitchToGameAsync().Forget();
            }
        }

        /// <summary> 이번 프레임에 마우스 또는 터치 눌림이 있었는지 반환함. </summary>
        private bool IsPointerPressed()
        {
            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        /// <summary> 활성화된 레벨에 맞춰 스토리 이미지와 스토리 텍스트 오브젝트를 설정함. </summary>
        private void SetupStoryLevel()
        {
            int index = selectedLevel - 1;

            LoadStoryImageAsync().Forget();

            if (storyLevels != null)
            {
                for (int i = 0; i < storyLevels.Length; i++)
                {
                    if (storyLevels[i] != null) storyLevels[i].SetActive(i == index);
                }
            }
        }

        /// <summary> Addressables에서 활성화된 레벨의 스토리 이미지를 비동기로 불러와 적용함. </summary>
        private async UniTaskVoid LoadStoryImageAsync()
        {
            if (storyImage == null) return;

            string key = $"Level{selectedLevel}";
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                _storyImageHandle = Addressables.LoadAssetAsync<Sprite>(key);
                Sprite sprite = await _storyImageHandle.Task.AsUniTask().AttachExternalCancellation(token);

                if (_storyImageHandle.Status == AsyncOperationStatus.Succeeded && sprite != null)
                {
                    storyImage.sprite = sprite;
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[GameFlowController] Level image '{key}' not found in Addressables.");
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary> 버튼 리스너를 해제하고 Addressables 핸들을 반환함. </summary>
        private void OnDestroy()
        {
            if (storyButton) storyButton.onClick.RemoveListener(OnStoryClicked);
            if (_storyImageHandle.IsValid()) Addressables.Release(_storyImageHandle);
        }

        /// <summary> 스토리 패널을 페이드아웃한 뒤 게임 패널을 페이드인하는 크로스페이드. 스토리 표시 중 화면 클릭 시 호출됨. </summary>
        private async UniTaskVoid SwitchToGameAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                if (storyPanel)
                {
                    await FadeCanvasGroupAsync(storyPanel, 1f, 0f, panelFadeDuration, token);
                    ApplyPanelState(storyPanel, false);
                }

                if (gamePanel)
                {
                    await FadeCanvasGroupAsync(gamePanel, 0f, 1f, panelFadeDuration, token);
                    ApplyPanelState(gamePanel, true);
                }
            }
            catch (OperationCanceledException) { }
            finally { _isBusy = false; }
        }

        /// <summary> 스토리 버튼 클릭 시 게임에서 스토리 패널로 되돌아감. </summary>
        private void OnStoryClicked()
        {
            if (_isBusy) return;
            SwitchToStoryAsync().Forget();
        }

        /// <summary> 게임 패널을 페이드아웃한 뒤 스토리 패널을 페이드인하는 크로스페이드. </summary>
        private async UniTaskVoid SwitchToStoryAsync()
        {
            _isBusy = true;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                if (gamePanel)
                {
                    await FadeCanvasGroupAsync(gamePanel, 1f, 0f, panelFadeDuration, token);
                    ApplyPanelState(gamePanel, false);
                }

                if (storyPanel)
                {
                    await FadeCanvasGroupAsync(storyPanel, 0f, 1f, panelFadeDuration, token);
                    ApplyPanelState(storyPanel, true);
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
                .SetEase(Ease.InOutQuad)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
        }

        /// <summary> 패널의 표시 여부에 따라 알파와 상호작용 상태를 설정함. 활성 상태는 유지하고 알파로만 제어함. </summary>
        private void ApplyPanelState(CanvasGroup group, bool visible)
        {
            if (!group) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
