using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Utils;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 미션 보드 텍스트를 구성하는 컨트롤러. 씬 시작 시 목적지(달/화성/외계 행성)를 무작위로 정하고,
    /// 엔진 출력량 x 연료량 - 탑재 중량으로 계산되는 추진력 및 진행도(Image_Fill)를 함께 표시함.
    /// </summary>
    public class MissionBoardController : MonoBehaviour
    {
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private Image goalImage;
        [SerializeField] private TMP_Text goalPlanetNameText;
        [SerializeField] private Image progressFillImage; // Image_Fill
        [SerializeField] private Image previewFillImage; // Image_Fill_Preview
        [SerializeField] private float fillTweenDuration = 0.5f; // 3_Game.json 로드 전까지의 폴백 기본값
        [SerializeField] private float previewBlinkFadeDuration = 0.8f; // 3_Game.json 로드 전까지의 폴백 기본값
        [SerializeField] private float previewBlinkMinAlpha = 0.5f; // 3_Game.json 로드 전까지의 폴백 기본값
        [SerializeField] private float previewApplyFadeDuration = 0.3f; // 3_Game.json 로드 전까지의 폴백 기본값

        private SelectedLevelStore _selectedLevelStore;
        private ILogger<MissionBoardController> _logger;
        private Constants.Mission.Definition _current = Constants.Mission.Definitions[0];

        // 3_Game.json 튜닝 값 — 로드 완료 전까지는 null이며 위 인스펙터 값을 그대로 사용함
        private GameSceneSettings _sceneSettings;
        private AsyncOperationHandle<Sprite> _goalSpriteHandle;
        private Tween _fillTween;
        private Tween _previewFillTween;
        private Tween _blinkTween;
        private CanvasGroup _previewCanvasGroup;
        private CancellationTokenSource _progressApplyCts;
        private bool _isApplyingProgress;

        // 설정하기 확정 전, 조절 중인 미리보기 fillAmount를 R3로 반응형 관리함
        private readonly ReactiveProperty<float> _previewFillAmount = new ReactiveProperty<float>(0f);
        private R3.DisposableBag _disposables = new R3.DisposableBag();

        /// <summary> 이번 게임의 목적지 이름. </summary>
        public string Destination => _current.Destination;

        /// <summary> 계산된 추진력이 이번 목적지의 목표 거리에 도달(이상)했는지 반환함. </summary>
        public bool IsThrustValid(int totalThrust) => totalThrust >= _current.TargetDistance;

        /// <summary> 레벨 3: 전기량이 이 값을 넘으면 안 됨(3~5 중 무작위로 정해짐). </summary>
        public int MaxElectricity { get; private set; }

        /// <summary> 레벨 3: 산소량이 이 값보다 낮으면 안 됨(3~5 중 무작위로 정해짐). </summary>
        public int MinOxygen { get; private set; }

        /// <summary> VContainer 의존성 주입. 선택된 레벨 저장소와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SelectedLevelStore selectedLevelStore, ILogger<MissionBoardController> logger)
        {
            _selectedLevelStore = selectedLevelStore;
            _logger = logger;
        }

        /// <summary>
        /// 씬 시작 시 레벨에 맞는 미션 보드 텍스트를 구성함. 레벨 1은 무작위 목적지를 골라 목적지/목표 표시까지 구성하고,
        /// 레벨 2는 고정된 코딩 안내 문구만 표시하며, 레벨 3은 전기량 상한/산소량 하한을 무작위로 정해 안내함.
        /// </summary>
        private void Start()
        {
            int level = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;

            if (level == 2)
            {
                ApplyLevel2MissionText();
            }
            else if (level == 3)
            {
                ApplyLevel3MissionText();
            }
            else
            {
                Constants.Mission.Definition[] definitions = Constants.Mission.Definitions;
                _current = definitions[UnityEngine.Random.Range(0, definitions.Length)];

                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 미션 설정됨: {_current.Destination} (목표거리={_current.TargetDistance})");

                ApplyMissionText();
                ApplyGoalDisplay();
            }

            EnsurePreviewCanvasGroup();
            ResetProgress();
            ResetPreview();

            _previewFillAmount.Subscribe(AnimatePreviewFillAmount).AddTo(ref _disposables);

            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 3_Game.json(GameSceneSettings)을 비동기로 로드함. </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Game}";
            _sceneSettings = await JsonLoader.LoadAsync<GameSceneSettings>(path, token);
        }

        private void ApplyMissionText()
        {
            if (missionText == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] missionText가 null이라 미션 텍스트를 설정할 수 없음.");
                return;
            }

            missionText.text =
                $"목적지는 <color=yellow>[{_current.Destination}]</color>입니다.\n" +
                $"<color=yellow>[동작 블럭]</color>을 사용하여,\n" +
                $"추진체와 탑재 종류를 설정해주세요.";
        }

        /// <summary> 레벨 2 전용 고정 미션 텍스트(목적지/목표 개념 없이 5개 동작 블록을 순서대로 코딩하라는 안내)를 적용함. </summary>
        private void ApplyLevel2MissionText()
        {
            if (missionText == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] missionText가 null이라 미션 텍스트를 설정할 수 없음.");
                return;
            }

            missionText.text =
                "설계한 로켓이 우주까지 날아 갈 수 있도록\n" +
                "<color=yellow>[동작 블록]</color> 5개를 사용하여 순서대로 코딩해주세요.";
        }

        /// <summary>
        /// 레벨 3 전용 미션 텍스트를 적용함. 전기량 상한(MaxElectricity)과 산소량 하한(MinOxygen)을 각각 3~5 중 무작위로 정해 안내함.
        /// </summary>
        private void ApplyLevel3MissionText()
        {
            MaxElectricity = UnityEngine.Random.Range(3, 6);
            MinOxygen = UnityEngine.Random.Range(3, 6);

            if (_logger != null)
            {
                _logger.ZLogInformation($"[MissionBoardController] 레벨 3 미션 설정됨: 전기량 상한={MaxElectricity}, 산소량 하한={MinOxygen}");
            }

            if (missionText == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] missionText가 null이라 미션 텍스트를 설정할 수 없음.");
                return;
            }

            missionText.text =
                $"현재 우주정거장은 전기량이 <color=yellow>[{MaxElectricity}]</color>을 넘으면 안되고,\n" +
                $"산소량은 <color=yellow>[{MinOxygen}]</color>보다 낮으면 안돼요!";
        }

        /// <summary> 목적지에 맞는 행성 이름 텍스트를 적용하고, 이미지는 Addressables에서 비동기로 불러와 Image_Goal에 적용함. </summary>
        private void ApplyGoalDisplay()
        {
            if (goalImage == null || goalPlanetNameText == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] goalImage 또는 goalPlanetNameText가 null이라 목표 표시를 적용할 수 없음.");
                return;
            }

            goalPlanetNameText.text = _current.PlanetName;
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
                    _logger.ZLogWarning($"[MissionBoardController] 목적지 '{_current.Destination}'에 대한 목표 이미지 없음 (Addressables 키 '{key}').");
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 설정하기 확정 시 호출됨. 미리보기(Image_Fill_Preview)를 fillAmount 변경 없이 알파 페이드아웃으로 먼저 자연스럽게 없앤 뒤,
        /// 실제 Image_Fill 값을 최종 적용하는 시퀀스(ApplyProgressAsync)를 시작함.
        /// </summary>
        /// <param name="totalThrust">엔진 출력량 x 연료량 - 탑재 중량으로 계산된 확정 추진력 합계.</param>
        public void SetProgress(int totalThrust)
        {
            if (progressFillImage == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] progressFillImage가 null이라 진행도를 설정할 수 없음.");
                return;
            }

            float target = CalculateFillAmount(totalThrust);
            if (_logger != null)
            {
                _logger.ZLogInformation($"[MissionBoardController] 진행도 적용 요청: 총추진력={totalThrust}, 미리보기게이지={_previewFillAmount.Value:F2}, 목표게이지={target:F2}");
            }

            ApplyProgressAsync(totalThrust, target).Forget();
        }

        /// <summary>
        /// 설정하기 확정 시퀀스: 1) 미리보기 깜빡임 강제 중지 -> 2) 미리보기 알파 페이드아웃과 실제 Image_Fill 상승을 동시에 진행 ->
        /// 3) 둘 다 끝나면 미리보기 오브젝트 비활성화. 씬 파괴/재조정 시 CancellationToken으로 안전하게 중단됨.
        /// </summary>
        private async UniTaskVoid ApplyProgressAsync(int totalThrust, float target)
        {
            _progressApplyCts?.Cancel();
            _progressApplyCts?.Dispose();
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            _progressApplyCts = cts;
            _isApplyingProgress = true;
            CancellationToken token = cts.Token;

            try
            {
                // 1. 깜빡임(블링크) 강제 중지
                _blinkTween?.Kill();
                _blinkTween = null;
                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 진행도 적용 시퀀스: 깜빡임 중지됨. 총추진력={totalThrust}");

                // 2. 미리보기는 fillAmount 변경 없이 알파만 페이드아웃하고, 그와 동시에 실제 Image_Fill을 타겟 값까지 부드럽게 채움
                EnsurePreviewCanvasGroup();
                _previewFillTween?.Kill(); // 미리보기 fillAmount가 페이드 중 함께 바뀌어 줄어들며 사라지지 않도록 정지

                UniTask fadeTask = _previewCanvasGroup != null
                    ? _previewCanvasGroup.DOFade(0f, _sceneSettings?.previewApplyFadeDuration ?? previewApplyFadeDuration).ToUniTask(cancellationToken: token)
                    : UniTask.CompletedTask;

                Tween fillTween = AnimateFillAmount(target);
                UniTask fillTask = fillTween.ToUniTask(cancellationToken: token);

                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 진행도 적용 시퀀스: 미리보기 페이드아웃과 게이지 상승을 동시에 시작함, 목표값={target:F2}.");
                await UniTask.WhenAll(fadeTask, fillTask);
                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 진행도 적용 시퀀스: fillAmount={target:F2} 적용됨");

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

                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 진행도 적용 시퀀스: 미리보기 비활성화, 적용된 fillAmount={target:F2}와 동기화 유지함.");
            }
            catch (OperationCanceledException)
            {
                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 진행도 적용 시퀀스 취소됨 (씬 파괴 또는 더 최신 요청으로 대체됨).");
            }
            finally
            {
                // 더 최신 요청이 이미 시작된 경우(=_progressApplyCts가 교체됨) 그 요청의 진행 상태를 덮어쓰지 않음
                if (_progressApplyCts == cts) _isApplyingProgress = false;
            }
        }

        /// <summary> Image_Fill을 시작 상태(0)로 되돌림. </summary>
        public void ResetProgress()
        {
            if (progressFillImage == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] progressFillImage가 null이라 진행도를 초기화할 수 없음.");
                return;
            }

            AnimateFillAmount(0f);
        }

        /// <summary>
        /// 설정하기 확정 전, 사용자가 엔진 출력량/연료량/탑재 중량 중 하나를 조절하는 동안 Image_Fill_Preview의 fillAmount를 갱신함.
        /// _previewFillAmount(ReactiveProperty)를 통해 AnimatePreviewFillAmount 구독자에게 전파되어 DOTween으로 반영됨.
        /// </summary>
        /// <param name="totalThrust">확정된 값 + 현재 조절 중인 임시 값을 결합해 계산한 추진력.</param>
        public void UpdatePreview(int totalThrust)
        {
            if (previewFillImage == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] previewFillImage가 null이라 미리보기를 갱신할 수 없음.");
                return;
            }

            // 설정하기 페이드아웃/적용 시퀀스가 진행 중이면 트윈 충돌을 막기 위해 무시함
            if (_isApplyingProgress) return;

            if (!previewFillImage.gameObject.activeSelf) previewFillImage.gameObject.SetActive(true);

            StartFuelPreviewBlink();

            _previewFillAmount.Value = CalculateFillAmount(totalThrust);
            if (_logger != null)
            {
                _logger.ZLogInformation($"[MissionBoardController] 미리보기 조정 중: 총추진력={totalThrust}, 미리보기게이지={_previewFillAmount.Value:F2}");
            }
        }

        /// <summary> Image_Fill_Preview를 시작 상태(0)로 되돌리고 깜빡임을 멈춤. 조절 대기 중이거나 설정이 확정/취소되었을 때 호출됨. </summary>
        public void ResetPreview()
        {
            // 설정하기 페이드아웃/적용 시퀀스가 진행 중이면 트윈 충돌을 막기 위해 무시함(시퀀스 자체가 리셋까지 책임짐)
            if (_isApplyingProgress) return;

            StopFuelPreviewBlink();
            if (previewFillImage == null) return;
            _previewFillAmount.Value = 0f;
        }

        /// <summary> fillAmount를 즉시 스냅하지 않고 트윈으로 부드럽게 변경함. 생성된 Tween을 반환해 호출부에서 완료를 대기할 수 있게 함. </summary>
        private Tween AnimateFillAmount(float targetFillAmount)
        {
            _fillTween?.Kill();
            _fillTween = progressFillImage.DOFillAmount(targetFillAmount, _sceneSettings?.fillTweenDuration ?? fillTweenDuration).SetEase(Ease.OutQuad);
            return _fillTween;
        }

        /// <summary> _previewFillAmount 변경 구독 콜백. Image_Fill_Preview의 fillAmount를 트윈으로 부드럽게 변경함. </summary>
        private void AnimatePreviewFillAmount(float targetFillAmount)
        {
            _previewFillTween?.Kill();
            _previewFillTween = previewFillImage.DOFillAmount(targetFillAmount, _sceneSettings?.fillTweenDuration ?? fillTweenDuration).SetEase(Ease.OutQuad);
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
            _blinkTween = _previewCanvasGroup.DOFade(_sceneSettings?.previewBlinkMinAlpha ?? previewBlinkMinAlpha, _sceneSettings?.previewBlinkFadeDuration ?? previewBlinkFadeDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);

            if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 연료 미리보기 깜빡임 시작됨.");
        }

        /// <summary> 연료량 조절이 끝나면(설정 확정/취소, 다른 재료로 전환) Image_Fill_Preview 깜빡임을 멈추고 불투명 상태로 되돌림. </summary>
        private void StopFuelPreviewBlink()
        {
            if (_blinkTween == null) return;

            _blinkTween.Kill();
            _blinkTween = null;
            if (_previewCanvasGroup != null) _previewCanvasGroup.alpha = 1f;

            if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 연료 미리보기 깜빡임 중지됨.");
        }

        /// <summary>
        /// 목적지의 목표 거리(TargetDistance) 대비 현재 추진력의 비율로 0~1 fillAmount를 계산함.
        /// 추진력이 목표 거리 이상이면 1.0(100%), 음수(엔진 출력량 x 연료량이 탑재 중량보다 작은 경우)면 0.0,
        /// 그 사이는 (추진력 / 목표 거리) 비율로 표시됨.
        /// </summary>
        private float CalculateFillAmount(int totalThrust)
        {
            int target = _current.TargetDistance;
            if (target <= 0) return 0f;

            return Mathf.Clamp01((float)totalThrust / target);
        }

        /// <summary> Addressables 핸들을 반환하고 진행 중인 트윈 및 R3 구독을 정리함. </summary>
        private void OnDestroy()
        {
            if (_goalSpriteHandle.IsValid()) Addressables.Release(_goalSpriteHandle);
            _fillTween?.Kill();
            _previewFillTween?.Kill();
            _blinkTween?.Kill();
            _progressApplyCts?.Cancel();
            _progressApplyCts?.Dispose();
            _disposables.Dispose();
            _previewFillAmount?.Dispose();
        }
    }
}
