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
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 미션 보드 텍스트를 구성하는 컨트롤러. 씬 시작 시 Level1 LevelData의 목적지 중 하나를 무작위로 정하고,
    /// 엔진 출력량 + 연료량 - 탑재 중량으로 계산되는 추진력 및 진행도(Image_Fill)를 함께 표시함.
    /// 추진력이 목표 거리와 정확히 같아야 성공(IsThrustValid)이며, 진행도는 목표 거리에서 가장 차고 넘치면 다시 줄어듦(CalculateFillAmount).
    /// </summary>
    public class MissionBoardController : MonoBehaviour
    {
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private Image goalImage;
        [SerializeField] private TMP_Text goalPlanetNameText;
        [SerializeField] private Image progressFillImage; // Image_Fill
        [SerializeField] private Image previewFillImage; // Image_Fill_Preview

        private SelectedLevelStore _selectedLevelStore;
        private GameFlowController _gameFlow; // 레벨 데이터(LevelData) 출처. 스토리 다시보기와 같은 배열을 씀
        private ILogger<MissionBoardController> _logger;
        private VisitorInfoProvider _visitorInfoProvider;
        private string _visitorName = Constants.DefaultVisitorName;
        private MissionDestination _current = CreateFallbackDestination();

        // Level1/Level3 LevelData에 값이 없을 때만 오류 로그와 함께 쓰는 폴백.
        // 목적지는 인스턴스마다 새로 만듦: 공유 static 객체를 넣어 두면 에디터 스크립트 리로드 때 _current 복원 값이 그 객체에 덮어써짐
        private static MissionDestination CreateFallbackDestination() => new MissionDestination { planetName = "외계 행성", targetDistance = 20, spriteKey = "ExoPlanet" };
        private static readonly Vector2Int FallbackLevel3Range = new Vector2Int(3, 5);

        // LevelData에 미션 문구가 없을 때 쓰는 기본 문구(레벨 1·3은 이번 판 값이 들어가므로 Start에서 만듦)
        private const string Level2FallbackMissionText =
            "로켓을 우주로 출발시켜 볼까요?\n" +
            "<color=yellow>[동작]</color> 블록 5개를 알맞은 순서로 이어서\n" +
            "로켓을 우주로 출발시켜 주세요.";
        private const string Level4FallbackMissionText =
            "<color=yellow>[동작]</color>과 <color=yellow>[제어]</color> 블록으로 탐사 로봇을 움직여 주세요.\n" +
            "먼저 우주 자원을 모으고, 기지로 안전하게 돌아오세요.\n" +
            "함정은 꼭 피해야 해요.";

        // 3_Game.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private GameSceneSettings _sceneSettings = new GameSceneSettings();
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

        /// <summary> 이번 게임의 목적지 표기(로그용, 예: "화성 (거리 10)"). </summary>
        public string Destination => $"{_current.planetName} (거리 {_current.targetDistance})";

        /// <summary> 이번 목적지의 목표 거리(레벨 1). 추진력이 이 값과 정확히 같아야 성공함. </summary>
        public int TargetDistance => _current.targetDistance;

        /// <summary> 계산된 추진력이 이번 목적지의 목표 거리와 정확히 같은지 반환함(모자라도, 넘쳐도 실패). </summary>
        public bool IsThrustValid(int totalThrust) => totalThrust == _current.targetDistance;

        /// <summary> 테스트 전용: 무작위 대신 레벨 1 목표 거리를 직접 정함. </summary>
        internal void SetTargetDistanceForTest(int targetDistance)
        {
            _current = new MissionDestination { planetName = _current.planetName, targetDistance = targetDistance, spriteKey = _current.spriteKey };
        }

        /// <summary> 레벨 3: 전기량이 이 값을 넘으면 안 됨(Level3 LevelData의 maxElectricityRange에서 무작위로 정해짐). </summary>
        public int MaxElectricity { get; private set; }

        /// <summary> 레벨 3: 산소량이 이 값보다 낮으면 안 됨(Level3 LevelData의 minOxygenRange에서 무작위로 정해짐). </summary>
        public int MinOxygen { get; private set; }

        /// <summary> 테스트 전용: 무작위 대신 레벨 3 기준값(전기량 상한, 산소량 하한)을 직접 정함. </summary>
        internal void SetLevel3LimitsForTest(int maxElectricity, int minOxygen)
        {
            MaxElectricity = maxElectricity;
            MinOxygen = minOxygen;
        }

        /// <summary> VContainer 의존성 주입. 선택된 레벨 저장소, 레벨 데이터를 가진 게임 흐름 컨트롤러, 로거, 체험자 정보 제공자를 할당함. </summary>
        [Inject]
        public void Construct(
            SelectedLevelStore selectedLevelStore,
            GameFlowController gameFlow,
            ILogger<MissionBoardController> logger,
            VisitorInfoProvider visitorInfoProvider = null)
        {
            _selectedLevelStore = selectedLevelStore;
            _gameFlow = gameFlow;
            _logger = logger;
            _visitorInfoProvider = visitorInfoProvider;
        }

        /// <summary>
        /// 씬 시작 시 레벨에 맞는 미션 보드 텍스트를 구성함. LevelData 에셋(ScriptableObject)의 missionText가
        /// 정의되어 있으면 해당 텍스트의 플레이스홀더를 치환하여 적용함.
        /// </summary>
        private void Start()
        {
            if (!_gameFlow && _logger != null)
            {
                _logger.ZLogError($"[MissionBoardController] GameFlowController가 주입되지 않아 레벨 데이터(LevelData)를 읽을 수 없음. 기본 미션 문구를 사용함.");
            }

            int level = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;

            if (level == 2)
            {
                ApplyMissionText(2, Level2FallbackMissionText);
            }
            else if (level == 3)
            {
                PickLevel3Limits();
                ApplyMissionText(3,
                    $"우주정거장을 안전하게 지키려면 어떻게 해야 할까요?\n" +
                    $"전기량은 <color=yellow>[{MaxElectricity}]</color>보다 많으면 안 돼요.\n" +
                    $"<color=yellow>[그리고]</color> 산소량은 <color=yellow>[{MinOxygen}]</color>보다 적으면 안 돼요.\n" +
                    $"두 가지 조건을 모두 지켜 주세요.");
            }
            else if (level == 4)
            {
                ApplyMissionText(4, Level4FallbackMissionText);
            }
            else if (level >= 5)
            {
                ApplyMissionText(5, null);
            }
            else
            {
                PickLevel1Destination();
                ApplyMissionText(1,
                    $"<color=yellow>[{_current.planetName}]</color>까지 거리는 <color=yellow>[{_current.targetDistance}]</color>{PlaceholderFormatter.GetCopula(_current.targetDistance)}.\n" +
                    $"로켓을 날리는 추진체, 탑재할 장비, 연료량을 골라\n" +
                    $"<color=yellow>[동작]</color> 블록으로 로켓의 힘을 <color=yellow>[{_current.targetDistance}]</color>에 맞춰 보세요.");
                ApplyGoalDisplay();
            }

            EnsureSubCanvases();
            EnsurePreviewCanvasGroup();
            ResetProgress();
            ResetPreview();

            _previewFillAmount.Subscribe(AnimatePreviewFillAmount).AddTo(ref _disposables);

            CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();
            LoadSceneSettingsAsync(destroyToken).Forget();
            LoadVisitorNameAsync(destroyToken).Forget();
        }

        /// <summary>
        /// 체험자 이름을 비동기로 불러와 미션 텍스트에 반영함. Start에서 기본 이름으로 이미 치환해 화면 문구에는 {name}이 남아 있지 않으므로,
        /// 원본 템플릿(LevelData의 missionText)에 {name}이 있으면 템플릿으로 다시 포맷함.
        /// </summary>
        private async UniTaskVoid LoadVisitorNameAsync(CancellationToken token)
        {
            if (_visitorInfoProvider == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] visitorInfoProvider가 null이라 미션 텍스트의 체험자 이름을 기본 이름 '{Constants.DefaultVisitorName}'으로 둠.");
                return;
            }

            try
            {
                _visitorName = await _visitorInfoProvider.GetNameAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            int level = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;
            string raw = GetRawMissionTextFromData(level);
            if (missionText && !string.IsNullOrEmpty(raw) && raw.Contains(Constants.VisitorPlaceholder))
            {
                missionText.text = FormatMissionText(raw);
            }
        }

        /// <summary> 해당 레벨(1부터)의 LevelData 에셋을 GameFlowController의 레벨 데이터 배열에서 찾아 반환함. 없으면 null. </summary>
        internal LevelData GetLevelData(int level)
        {
            LevelData[] levelDataList = _gameFlow ? _gameFlow.LevelDataList : null;
            int index = level - 1;
            return (levelDataList != null && index >= 0 && index < levelDataList.Length && levelDataList[index]) ? levelDataList[index] : null;
        }

        /// <summary> LevelData 에셋에서 해당 레벨의 미션 텍스트 원본을 가져옴. </summary>
        private string GetRawMissionTextFromData(int level)
        {
            LevelData data = GetLevelData(level);
            return data ? data.missionText : null;
        }

        /// <summary>
        /// 미션 텍스트 내 플레이스홀더({planet}, 숫자 {distance}/{maxElectricity}/{minOxygen}과 그 조사 {distance|이에요} 등, {name})를 실제 값으로 치환함.
        /// </summary>
        private string FormatMissionText(string rawText)
        {
            if (string.IsNullOrEmpty(rawText)) return string.Empty;

            string formatted = rawText.Replace(Constants.MissionPlaceholders.Planet, _current.planetName);
            formatted = PlaceholderFormatter.ReplaceNumber(formatted, Constants.MissionPlaceholders.Distance, _current.targetDistance);
            formatted = PlaceholderFormatter.ReplaceNumber(formatted, Constants.MissionPlaceholders.MaxElectricity, MaxElectricity);
            formatted = PlaceholderFormatter.ReplaceNumber(formatted, Constants.MissionPlaceholders.MinOxygen, MinOxygen);
            return PlaceholderFormatter.ReplaceVisitorName(formatted, _visitorName);
        }

        /// <summary> 3_Game.json(GameSceneSettings)을 GameSceneSettingsProvider를 통해 비동기로 불러옴(씬 내 다른 컨트롤러와 로드를 공유함). </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            _sceneSettings = await GameSceneSettingsProvider.GetAsync(token);
        }

        /// <summary>
        /// 해당 레벨 LevelData의 미션 문구를 자리표시자를 치환해 적용함. LevelData에 문구가 없으면 fallbackText를 쓰고,
        /// fallbackText도 null이면(레벨 5) 씬에 입력된 문구를 그대로 둔 채 경고를 남김.
        /// </summary>
        private void ApplyMissionText(int level, string fallbackText)
        {
            if (!missionText)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] missionText가 null이라 미션 텍스트를 설정할 수 없음.");
                return;
            }

            string raw = GetRawMissionTextFromData(level);
            if (!string.IsNullOrEmpty(raw))
            {
                missionText.text = FormatMissionText(raw);
            }
            else if (fallbackText != null)
            {
                missionText.text = fallbackText;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[MissionBoardController] Level{level} LevelData에 미션 문구(missionText)가 없어 씬에 입력된 문구를 그대로 둠.");
            }
        }

        /// <summary> 레벨 1 목적지를 Level1 LevelData의 목적지 중 하나로 무작위로 정함. 목적지가 없으면 기본 목적지를 씀. </summary>
        private void PickLevel1Destination()
        {
            LevelData level1 = GetLevelData(1);
            MissionDestination[] destinations = level1 ? level1.destinations : null;
            if (destinations != null && destinations.Length > 0)
            {
                _current = destinations[UnityEngine.Random.Range(0, destinations.Length)];
            }
            else if (_logger != null)
            {
                _logger.ZLogError($"[MissionBoardController] Level1 LevelData에 목적지(destinations)가 없어 기본 목적지 '{_current.planetName}'를 사용함.");
            }

            if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 미션 설정됨: {Destination} (목표거리={_current.targetDistance})");
        }

        /// <summary> 레벨 3 전기량 상한(MaxElectricity)과 산소량 하한(MinOxygen)을 Level3 LevelData의 범위에서 각각 무작위로 정함. </summary>
        private void PickLevel3Limits()
        {
            LevelData level3 = GetLevelData(3);
            if (!level3 && _logger != null)
            {
                _logger.ZLogError($"[MissionBoardController] Level3 LevelData가 없어 기준값 범위를 기본값 {FallbackLevel3Range.x}~{FallbackLevel3Range.y}로 사용함.");
            }

            Vector2Int electricityRange = level3 ? level3.maxElectricityRange : FallbackLevel3Range;
            Vector2Int oxygenRange = level3 ? level3.minOxygenRange : FallbackLevel3Range;
            MaxElectricity = UnityEngine.Random.Range(electricityRange.x, electricityRange.y + 1);
            MinOxygen = UnityEngine.Random.Range(oxygenRange.x, oxygenRange.y + 1);

            if (_logger != null)
            {
                _logger.ZLogInformation($"[MissionBoardController] 레벨 3 미션 설정됨: 전기량 상한={MaxElectricity}, 산소량 하한={MinOxygen}");
            }
        }

        /// <summary> 목적지에 맞는 행성 이름 텍스트를 적용하고, 이미지는 Addressables에서 비동기로 불러와 Image_Goal에 적용함. </summary>
        private void ApplyGoalDisplay()
        {
            if (!goalImage || !goalPlanetNameText)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] goalImage 또는 goalPlanetNameText가 null이라 목표 표시를 적용할 수 없음.");
                return;
            }

            goalPlanetNameText.text = _current.planetName;
            LoadGoalSpriteAsync(_current.spriteKey).Forget();
        }

        /// <summary> Addressables에서 목적지 이미지를 비동기로 불러와 Image_Goal에 적용함. </summary>
        private async UniTaskVoid LoadGoalSpriteAsync(string key)
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            try
            {
                _goalSpriteHandle = Addressables.LoadAssetAsync<Sprite>(key);
                Sprite sprite = await _goalSpriteHandle.Task.AsUniTask().AttachExternalCancellation(token);

                if (_goalSpriteHandle.Status == AsyncOperationStatus.Succeeded && sprite)
                {
                    goalImage.sprite = sprite;
                    goalImage.SetNativeSize();
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[MissionBoardController] 목적지 '{Destination}'에 대한 목표 이미지 없음 (Addressables 키 '{key}').");
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 설정하기 확정 시 호출됨. 미리보기(Image_Fill_Preview)를 fillAmount 변경 없이 알파 페이드아웃으로 먼저 자연스럽게 없앤 뒤,
        /// 실제 Image_Fill 값을 최종 적용하는 시퀀스(ApplyProgressAsync)를 시작함.
        /// totalThrust: 엔진 출력량 + 연료량 - 탑재 중량으로 계산된 확정 추진력 합계.
        /// </summary>
        public void SetProgress(int totalThrust)
        {
            if (!progressFillImage)
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

                UniTask fadeTask = _previewCanvasGroup
                    ? _previewCanvasGroup.DOFade(0f, _sceneSettings.previewApplyFadeDuration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token)
                    : UniTask.CompletedTask;

                Tween fillTween = AnimateFillAmount(target);
                UniTask fillTask = fillTween.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);

                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 진행도 적용 시퀀스: 미리보기 페이드아웃과 게이지 상승을 동시에 시작함, 목표값={target:F2}.");
                await UniTask.WhenAll(fadeTask, fillTask);
                if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 진행도 적용 시퀀스: fillAmount={target:F2} 적용됨");

                // 3. 미리보기 오브젝트 비활성화. 그림자 게이지 컨셉에 맞춰 fillAmount는 0으로 리셋하지 않고
                // 방금 적용된 실제 Image_Fill 값과 동일하게 유지함(다음에 다시 켜졌을 때도 실제 값을 그대로 반영한 상태로 시작함).
                // previewFillImage.fillAmount는 즉시 직접 대입함: _previewFillAmount.Value가 이미 target과 같으면
                // (연속 조절 중 트윈이 중간에 Kill되어 시각값이 target에 못 미친 경우 등) ReactiveProperty가 값 변경 없음으로
                // 판단해 구독 콜백이 실행되지 않을 수 있어 트윈에만 의존하면 그림자가 어긋날 수 있음.
                if (previewFillImage)
                {
                    previewFillImage.fillAmount = target;
                    previewFillImage.gameObject.SetActive(false);
                }
                else if (_logger != null)
                {
                    _logger.ZLogWarning($"[MissionBoardController] previewFillImage가 null이라 미리보기를 비활성화할 수 없음.");
                }

                if (_previewCanvasGroup) _previewCanvasGroup.alpha = 1f;
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
                if (_progressApplyCts == cts)
                {
                    _isApplyingProgress = false;
                    _progressApplyCts.Dispose();
                    _progressApplyCts = null;
                }
            }
        }

        /// <summary> Image_Fill을 시작 상태(0)로 되돌림. </summary>
        public void ResetProgress()
        {
            if (!progressFillImage)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] progressFillImage가 null이라 진행도를 초기화할 수 없음.");
                return;
            }

            AnimateFillAmount(0f);
        }

        /// <summary>
        /// 설정하기 확정 전, 사용자가 엔진 출력량/연료량/탑재 중량 중 하나를 조절하는 동안 Image_Fill_Preview의 fillAmount를 갱신함.
        /// _previewFillAmount(ReactiveProperty)를 통해 AnimatePreviewFillAmount 구독자에게 전파되어 DOTween으로 반영됨.
        /// totalThrust: 확정된 값 + 현재 조절 중인 임시 값을 결합해 계산한 추진력.
        /// </summary>
        public void UpdatePreview(int totalThrust)
        {
            if (!previewFillImage)
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
            if (!previewFillImage)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] previewFillImage가 null이라 미리보기를 초기화할 수 없음.");
                return;
            }
            _previewFillAmount.Value = 0f;
        }

        /// <summary> fillAmount를 즉시 스냅하지 않고 트윈으로 부드럽게 변경함. 생성된 Tween을 반환해 호출부에서 완료를 대기할 수 있게 함. </summary>
        private Tween AnimateFillAmount(float targetFillAmount)
        {
            _fillTween?.Kill();
            _fillTween = progressFillImage.DOFillAmount(targetFillAmount, _sceneSettings.fillTweenDuration).SetEase(Ease.OutQuad)
                .SetLink(progressFillImage.gameObject);
            return _fillTween;
        }

        /// <summary> _previewFillAmount 변경 구독 콜백. Image_Fill_Preview의 fillAmount를 트윈으로 부드럽게 변경함. </summary>
        private void AnimatePreviewFillAmount(float targetFillAmount)
        {
            if (!previewFillImage)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] previewFillImage가 null이라 미리보기 게이지를 갱신할 수 없음.");
                return;
            }

            _previewFillTween?.Kill();
            _previewFillTween = previewFillImage.DOFillAmount(targetFillAmount, _sceneSettings.fillTweenDuration).SetEase(Ease.OutQuad)
                .SetLink(previewFillImage.gameObject);
        }

        /// <summary> 게이지 fillAmount 트윈 시 메인 UI 캔버스의 리빌드를 방지하도록 서브 캔버스를 보장함. </summary>
        private void EnsureSubCanvases()
        {
            EnsureSubCanvas(progressFillImage);
            EnsureSubCanvas(previewFillImage);
        }

        private static void EnsureSubCanvas(Component target)
        {
            if (!target) return;
            if (!target.TryGetComponent<Canvas>(out _))
            {
                target.gameObject.AddComponent<Canvas>();
            }
        }

        /// <summary> Image_Fill_Preview에 CanvasGroup이 없으면 추가해 확보함. 페이드가 이 CanvasGroup에만 적용되어 다른 UI(텍스트/게이지)에 영향을 주지 않음. </summary>
        private void EnsurePreviewCanvasGroup()
        {
            if (_previewCanvasGroup) return;
            if (!previewFillImage)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] previewFillImage가 null이라 미리보기 CanvasGroup을 확보할 수 없음.");
                return;
            }

            if (!previewFillImage.TryGetComponent(out _previewCanvasGroup))
            {
                _previewCanvasGroup = previewFillImage.gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary> 연료량 조절 인터랙션 시작 시 Image_Fill_Preview를 부드럽게 반복 페이드(깜빡임)함. 이미 재생 중이면 무시함. </summary>
        private void StartFuelPreviewBlink()
        {
            EnsurePreviewCanvasGroup();
            if (!_previewCanvasGroup)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] 미리보기 CanvasGroup이 없어 깜빡임을 시작할 수 없음.");
                return;
            }
            if (_blinkTween != null && _blinkTween.IsActive()) return;

            _previewCanvasGroup.alpha = 1f;
            _blinkTween = _previewCanvasGroup.DOFade(_sceneSettings.previewBlinkMinAlpha, _sceneSettings.previewBlinkFadeDuration)
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
            if (_previewCanvasGroup) _previewCanvasGroup.alpha = 1f;

            if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] 연료 미리보기 깜빡임 중지됨.");
        }

        /// <summary>
        /// 목적지의 목표 거리 대비 현재 추진력으로 0~1 fillAmount를 계산함(확정 게이지와 미리보기 게이지 공용).
        /// 목표 거리 이하에서는 (추진력 / 목표 거리) 비율로 차오르고, 정확히 같으면 1.0(100%),
        /// 넘치면 넘친 만큼 같은 비율로 다시 줄어듦(목표 거리의 2배 이상이면 0). 음수(엔진 출력량 + 연료량이 탑재 중량보다 작은 경우)면 0.
        /// </summary>
        internal float CalculateFillAmount(int totalThrust)
        {
            int target = _current.targetDistance;
            if (target <= 0) return 0f;

            float ratio = totalThrust <= target
                ? (float)totalThrust / target
                : 1f - (float)(totalThrust - target) / target;
            return Mathf.Clamp01(ratio);
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
