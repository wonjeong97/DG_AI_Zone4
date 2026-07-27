using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
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
        [SerializeField] private float fillTweenDuration = 0.5f;

        private ILogger<MissionBoardController> _logger;
        private Constants.Mission.Definition _current = Constants.Mission.Definitions[0];
        private AsyncOperationHandle<Sprite> _goalSpriteHandle;
        private Tween _fillTween;

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
            ResetFuelProgress();
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

        /// <summary> 연료량 입력에 따라 Image_Fill의 fillAmount를 목적지 조건에 맞춰 갱신함. 정답 범위 안이면 1, 멀어질수록 0에 가까워짐. </summary>
        public void SetFuelProgress(int fuel)
        {
            if (progressFillImage == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] progressFillImage is null. Cannot set fuel progress.");
                return;
            }

            AnimateFillAmount(CalculateFillAmount(fuel));
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

        /// <summary> fillAmount를 즉시 스냅하지 않고 트윈으로 부드럽게 변경함. </summary>
        private void AnimateFillAmount(float targetFillAmount)
        {
            _fillTween?.Kill();
            _fillTween = progressFillImage.DOFillAmount(targetFillAmount, fillTweenDuration).SetEase(Ease.OutQuad);
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

        /// <summary> Addressables 핸들을 반환하고 진행 중인 트윈을 종료함. </summary>
        private void OnDestroy()
        {
            if (_goalSpriteHandle.IsValid()) Addressables.Release(_goalSpriteHandle);
            _fillTween?.Kill();
        }
    }
}
