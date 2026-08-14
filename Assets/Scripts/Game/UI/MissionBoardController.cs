using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 미션 보드 텍스트를 구성하는 컨트롤러. 씬 시작 시 목적지(달/화성/외계 행성)를 무작위로 정하고,
    /// 목적지에 맞는 연료량 조건 및 진행도(Image_Fill)를 함께 표시함.
    /// </summary>
    public class MissionBoardController : MonoBehaviour
    {
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private Image goalImage;
        [SerializeField] private TMP_Text goalPlanetNameText;
        [SerializeField] private Image progressFillImage; // Image_Fill
        [SerializeField] private Image previewFillImage; // Image_Fill_Preview
        [SerializeField] private float fillTweenDuration = 0.5f;
        [SerializeField] private float previewBlinkFadeDuration = 0.8f;
        [SerializeField] private float previewBlinkMinAlpha = 0.5f;
        [SerializeField] private float previewApplyFadeDuration = 0.3f;

        private ILogger<MissionBoardController> _logger;
        private Constants.Mission.Definition _current = Constants.Mission.Definitions[0];
        private AsyncOperationHandle<Sprite> _goalSpriteHandle;
        private Tween _fillTween;
        private Tween _previewFillTween;
        private Tween _blinkTween;
        private CanvasGroup _previewCanvasGroup;
        private CancellationTokenSource _fuelApplyCts;
        private bool _isApplyingFuel;

        // 설정하기 확정 전, 연료량 조절 중인 미리보기 fillAmount를 R3로 반응형 관리함
        private readonly ReactiveProperty<float> _previewFillAmount = new ReactiveProperty<float>(0f);
        private R3.DisposableBag _disposables = new R3.DisposableBag();

        /// <summary> 이번 게임의 목적지 이름. </summary>
        public string Destination => _current.Destination;

        /// <summary> 입력한 연료량이 이번 목적지의 조건 범위에 드는지 반환함. </summary>
        public bool IsFuelValid(int fuel) => fuel >= _current.MinFuel && fuel <= _current.MaxFuel;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<MissionBoardController> logger)
        {
            _logger = logger;
        }

        /// <summary> 씬 시작 시 무작위 목적지를 골라 미션 보드 텍스트와 목적지 표시를 구성함. </summary>
        private void Start()
        {
            Constants.Mission.Definition[] definitions = Constants.Mission.Definitions;
            _current = definitions[UnityEngine.Random.Range(0, definitions.Length)];

            if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] Mission set: {_current.Destination} / {_current.FuelRequirement}");

            ApplyMissionText();
            ApplyGoalDisplay();
            EnsurePreviewCanvasGroup();
            ResetFuelProgress();
            ResetFuelPreview();

            _previewFillAmount.Subscribe(AnimatePreviewFillAmount).AddTo(ref _disposables);
        }

        private void ApplyMissionText()
        {
            if (missionText == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] missionText is null. Cannot set mission text.");
                return;
            }

            missionText.text =
                $"목적지는 [<color=yellow>{_current.Destination}</color>] 입니다.\n" +
                $"[<color=yellow>{_current.FuelRequirement}</color>]을 입력하고,\n" +
                $"추진체와 탑재 종류를 설정해주세요.";
        }

        /// <summary> 목적지에 맞는 행성 이름 텍스트를 적용하고, 이미지는 Addressables에서 비동기로 불러와 Image_Goal에 적용함. </summary>
        private void ApplyGoalDisplay()
        {
            if (goalImage == null || goalPlanetNameText == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] goalImage or goalPlanetNameText is null. Cannot apply goal display.");
                return;
            }

            goalPlanetNameText.text = _current.Destination;
            LoadGoalSpriteAsync(_current.SpriteKey).Forget();
        }

        /// <summary> Addressables에서 목적지 이미지를 비동기로 불러와 Image_Goal에 적용함. </summary>
        private async UniTaskVoid LoadGoalSpriteAsync(string key)
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                _goalSpriteHandle = Addressables.LoadAssetAsync<Sprite>(key);
                Sprite sprite = await _goalSpriteHandle.Task.AsUniTask().AttachExternalCancellation(token);

                if (_goalSpriteHandle.Status == AsyncOperationStatus.Succeeded && sprite != null)
                {
                    goalImage.sprite = sprite;
                    goalImage.SetNativeSize();
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[MissionBoardController] Goal sprite not found for destination '{_current.Destination}' (Addressables key '{key}').");
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 설정하기 확정 시 호출됨. 미리보기(Image_Fill_Preview)를 fillAmount 변경 없이 알파 페이드아웃으로 먼저 자연스럽게 없앤 뒤,
        /// 실제 Image_Fill 값을 최종 적용하는 시퀀스(ApplyFuelProgressAsync)를 시작함.
        /// </summary>
        public void SetFuelProgress(int fuel)
        {
            if (progressFillImage == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] progressFillImage is null. Cannot set fuel progress.");
                return;
            }

            float target = CalculateFillAmount(fuel);
            if (_logger != null)
            {
                _logger.ZLogInformation($"[MissionBoardController] Fuel apply requested: fuel={fuel}, previewFillAmount={_previewFillAmount.Value:F2}, targetFillAmount={target:F2}");
            }

            ApplyFuelProgressAsync(fuel, target).Forget();
        }

        /// <summary>
        /// 설정하기 확정 시퀀스: 1) 미리보기 깜빡임 강제 중지 -> 2) 미리보기 알파 페이드아웃과 실제 Image_Fill 상승을 동시에 진행 ->
        /// 3) 둘 다 끝나면 미리보기 오브젝트 비활성화. 씬 파괴/재조정 시 CancellationToken으로 안전하게 중단됨.
        /// </summary>
        private async UniTaskVoid ApplyFuelProgressAsync(int fuel, float target)
        {
            _fuelApplyCts?.Cancel();
            _fuelApplyCts?.Dispose();
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            _fuelApplyCts = cts;
            _isApplyingFuel = true;
            CancellationToken token = cts.Token;

            try
            {
                // 1. 깜빡임(블링크) 강제 중지
                _blinkTween?.Kill();
                _blinkTween = null;
                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] Fuel apply sequence: blink stopped. fuel={fuel}");

                // 2. 미리보기는 fillAmount 변경 없이 알파만 페이드아웃하고, 그와 동시에 실제 Image_Fill을 타겟 값까지 부드럽게 채움
                EnsurePreviewCanvasGroup();
                _previewFillTween?.Kill(); // 미리보기 fillAmount가 페이드 중 함께 바뀌어 줄어들며 사라지지 않도록 정지

                UniTask fadeTask = _previewCanvasGroup != null
                    ? _previewCanvasGroup.DOFade(0f, previewApplyFadeDuration).ToUniTask(cancellationToken: token)
                    : UniTask.CompletedTask;

                Tween fillTween = AnimateFillAmount(target);
                UniTask fillTask = fillTween.ToUniTask(cancellationToken: token);

                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] Fuel apply sequence: preview fade-out and fill rise started in parallel, target={target:F2}.");
                await UniTask.WhenAll(fadeTask, fillTask);
                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] Fuel apply sequence: applied fillAmount={target:F2}");

                // 3. 미리보기 오브젝트 비활성화. 그림자 게이지 컨셉에 맞춰 fillAmount는 0으로 리셋하지 않고
                // 방금 적용된 실제 Image_Fill 값과 동일하게 유지함(다음에 다시 켜졌을 때도 실제 값을 그대로 반영한 상태로 시작함).
                // previewFillImage.fillAmount는 즉시 직접 대입함: _previewFillAmount.Value가 이미 target과 같으면
                // (연속 조절 중 트윈이 중간에 Kill되어 시각값이 target에 못 미친 경우 등) ReactiveProperty가 값 변경 없음으로
                // 판단해 구독 콜백이 실행되지 않을 수 있어 트윈에만 의존하면 그림자가 어긋날 수 있음.
                if (previewFillImage != null)
                {
                    previewFillImage.fillAmount = target;
                    previewFillImage.gameObject.SetActive(false);
                }
                if (_previewCanvasGroup != null) _previewCanvasGroup.alpha = 1f;
                _previewFillAmount.Value = target;

                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] Fuel apply sequence: preview deactivated, kept in sync with applied fillAmount={target:F2}.");
            }
            catch (OperationCanceledException)
            {
                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] Fuel apply sequence canceled (scene destroyed or superseded by a newer request).");
            }
            finally
            {
                // 더 최신 요청이 이미 시작된 경우(=_fuelApplyCts가 교체됨) 그 요청의 진행 상태를 덮어쓰지 않음
                if (_fuelApplyCts == cts) _isApplyingFuel = false;
            }
        }

        /// <summary> Image_Fill을 시작 상태(0)로 되돌림. </summary>
        public void ResetFuelProgress()
        {
            if (progressFillImage == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] progressFillImage is null. Cannot reset fuel progress.");
                return;
            }

            AnimateFillAmount(0f);
        }

        /// <summary>
        /// 설정하기 확정 전, 사용자가 연료량을 조절하는 동안 Image_Fill_Preview의 fillAmount를 갱신함.
        /// _previewFillAmount(ReactiveProperty)를 통해 AnimatePreviewFillAmount 구독자에게 전파되어 DOTween으로 반영됨.
        /// </summary>
        public void UpdateFuelPreview(int fuel)
        {
            if (previewFillImage == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] previewFillImage is null. Cannot update fuel preview.");
                return;
            }

            // 설정하기 페이드아웃/적용 시퀀스가 진행 중이면 트윈 충돌을 막기 위해 무시함
            if (_isApplyingFuel) return;

            if (!previewFillImage.gameObject.activeSelf) previewFillImage.gameObject.SetActive(true);

            StartFuelPreviewBlink();

            _previewFillAmount.Value = CalculateFillAmount(fuel);
            if (_logger != null)
            {
                _logger.ZLogInformation($"[MissionBoardController] Fuel preview adjusting: fuel={fuel}, previewFillAmount={_previewFillAmount.Value:F2}");
            }
        }

        /// <summary> Image_Fill_Preview를 시작 상태(0)로 되돌리고 깜빡임을 멈춤. 연료량 이외의 재료를 조절 중이거나 설정이 확정/취소되었을 때 호출됨. </summary>
        public void ResetFuelPreview()
        {
            // 설정하기 페이드아웃/적용 시퀀스가 진행 중이면 트윈 충돌을 막기 위해 무시함(시퀀스 자체가 리셋까지 책임짐)
            if (_isApplyingFuel) return;

            StopFuelPreviewBlink();
            if (previewFillImage == null) return;
            _previewFillAmount.Value = 0f;
        }

        /// <summary> fillAmount를 즉시 스냅하지 않고 트윈으로 부드럽게 변경함. 생성된 Tween을 반환해 호출부에서 완료를 대기할 수 있게 함. </summary>
        private Tween AnimateFillAmount(float targetFillAmount)
        {
            _fillTween?.Kill();
            _fillTween = progressFillImage.DOFillAmount(targetFillAmount, fillTweenDuration).SetEase(Ease.OutQuad);
            return _fillTween;
        }

        /// <summary> _previewFillAmount 변경 구독 콜백. Image_Fill_Preview의 fillAmount를 트윈으로 부드럽게 변경함. </summary>
        private void AnimatePreviewFillAmount(float targetFillAmount)
        {
            _previewFillTween?.Kill();
            _previewFillTween = previewFillImage.DOFillAmount(targetFillAmount, fillTweenDuration).SetEase(Ease.OutQuad);
        }

        /// <summary> Image_Fill_Preview에 CanvasGroup이 없으면 추가해 확보함. 페이드가 이 CanvasGroup에만 적용되어 다른 UI(텍스트/게이지)에 영향을 주지 않음. </summary>
        private void EnsurePreviewCanvasGroup()
        {
            if (previewFillImage == null || _previewCanvasGroup != null) return;

            _previewCanvasGroup = previewFillImage.GetComponent<CanvasGroup>();
            if (_previewCanvasGroup == null) _previewCanvasGroup = previewFillImage.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary> 연료량 조절 인터랙션 시작 시 Image_Fill_Preview를 부드럽게 반복 페이드(깜빡임)함. 이미 재생 중이면 무시함. </summary>
        private void StartFuelPreviewBlink()
        {
            EnsurePreviewCanvasGroup();
            if (_previewCanvasGroup == null) return;
            if (_blinkTween != null && _blinkTween.IsActive()) return;

            _previewCanvasGroup.alpha = 1f;
            _blinkTween = _previewCanvasGroup.DOFade(previewBlinkMinAlpha, previewBlinkFadeDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);

            if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] Fuel preview blink started.");
        }

        /// <summary> 연료량 조절이 끝나면(설정 확정/취소, 다른 재료로 전환) Image_Fill_Preview 깜빡임을 멈추고 불투명 상태로 되돌림. </summary>
        private void StopFuelPreviewBlink()
        {
            if (_blinkTween == null) return;

            _blinkTween.Kill();
            _blinkTween = null;
            if (_previewCanvasGroup != null) _previewCanvasGroup.alpha = 1f;

            if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] Fuel preview blink stopped.");
        }

        /// <summary>
        /// 연료량과 목적지 조건 범위의 거리로 0~1 fillAmount를 계산함.
        /// 범위 안이면 1, 범위 밖이면 목적지별로 가능한 최대 거리(연료량 도메인 0~10 기준) 대비 비율만큼 0에 가까워짐.
        /// </summary>
        private float CalculateFillAmount(int fuel)
        {
            int min = _current.MinFuel;
            int max = _current.MaxFuel;

            if (fuel >= min && fuel <= max) return 1f;

            int distance = fuel < min ? min - fuel : fuel - max;
            int maxDistance = Mathf.Max(min - Constants.Mission.FuelDomainMin, Constants.Mission.FuelDomainMax - max);
            if (maxDistance <= 0) return 0f;

            return Mathf.Clamp01(1f - (float)distance / maxDistance);
        }

        /// <summary> Addressables 핸들을 반환하고 진행 중인 트윈 및 R3 구독을 정리함. </summary>
        private void OnDestroy()
        {
            if (_goalSpriteHandle.IsValid()) Addressables.Release(_goalSpriteHandle);
            _fillTween?.Kill();
            _previewFillTween?.Kill();
            _blinkTween?.Kill();
            _fuelApplyCts?.Cancel();
            _fuelApplyCts?.Dispose();
            _disposables.Dispose();
            _previewFillAmount?.Dispose();
        }
    }
}
