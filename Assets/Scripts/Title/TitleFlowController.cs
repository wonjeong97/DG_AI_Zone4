using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Network;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using HuliacDev.Data;
using HuliacDev.Network;
using HuliacDev.UI;
using HuliacDev.Utils;
using ZLogger;

namespace DGAIZone.Title
{
    /// <summary>
    /// 타이틀 씬의 화면 흐름 제어. 시작 버튼을 누르면 화면 페이드와 함께 인트로 씬으로 전환함.
    /// 서버 모드면 "QR 코드를 인식하여 주세요"를 띄우고 시작 버튼을 숨긴 채 QR 입력을 기다렸다가, 인식되면 체험자 서버에
    /// 체험 가능 여부(checkActive)와 진행도(getUser)를 확인해 체험자와 해금 레벨을 기록하고 "{이름}님, 시작하기를 눌러주세요"와 시작 버튼을 보여줌.
    /// 체험할 수 없으면 이유를 잠시 보여 준 뒤 다시 QR을 기다림. 로컬 모드면 QR 단계 없이 바로 시작 안내와 버튼을 보여줌.
    /// 안내는 어느 쪽이든 원래 색과 최소 알파 사이를 오가며 부드럽게 깜빡임(Zone1 TitleSceneManager와 동일한 정책·효과).
    /// </summary>
    public class TitleFlowController : MonoBehaviour
    {
        [SerializeField] private Button startButton;

        [Header("하단 안내")]
        [SerializeField] private CanvasGroup qrCanvasGroup; // Image_QR
        [SerializeField] private TMP_Text guideText; // Image_QR 하위 Text_QR

        private SceneTransitionService _sceneTransition;
        private VisitorInfoProvider _visitorInfoProvider;
        private SelectedLevelStore _selectedLevelStore;
        private UnlockedLevelStore _unlockedLevelStore;
        private AdminLevelJumpStore _levelJumpStore;
        private VisitorApiClient _visitorApiClient;
        private AppSettingsProvider _settingsProvider;
        private ILogger<TitleFlowController> _logger;
        private SoundManager _soundManager;
        private ApiManagerBase _apiManager;
        private bool _isBusy;

        // 무한 반복 깜빡임이라 씬을 떠날 때 직접 Kill함
        private Tween _qrBlinkTween;

        // USB 바코드 스캐너는 키보드처럼 문자를 입력한 뒤 Enter를 보냄 — Enter 전까지 모은 문자열이 QR 값.
        // 글자 사이가 0_Title.json scanCharGapSeconds(기본 0.5초)보다 벌어지면 앞에 모은 글자(찍기 전에 눌린 키 등)는 버림(ScanInputBuffer).
        // 스캐너는 본체 키보드와 별개의 키보드 장치로 잡히므로 연결된 키보드 전부(나중에 꽂힌 것 포함)를 구독함.
        private readonly ScanInputBuffer _scanBuffer = new();
        private readonly List<Keyboard> _scanKeyboards = new();
        private bool _isWaitingForQr;

        // QR로 확인한 체험자가 시작하기를 누르지 않고 기다린 시간 재기 — 시작하기·새 QR·씬 파괴 때 취소함
        private CancellationTokenSource _confirmTimeoutCts;

        // 0_Title.json 튜닝 값 — 로드 전에 QR 확인 결과가 나오면 설정 클래스의 기본값을 씀
        private TitleSceneSettings _sceneSettings = new TitleSceneSettings();

        // 00_Common.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀.
        // 씬 전환 페이드 시간은 다른 씬들과 마찬가지로 00_Common.json의 sceneTransitionFadeDuration을 공유해서 쓰며,
        // 씬별로 값이 갈리지 않도록 함(현장에서 페이드 시간을 한 곳만 바꾸면 전체 씬에 일관되게 반영됨).
        private CommonSettings _commonSettings = new CommonSettings();

        /// <summary>
        /// VContainer 의존성 주입. 씬 전환 서비스, 체험자 정보 제공자, 선택/잠금 해제 레벨 저장소, 관리자 레벨 이동 저장소,
        /// 체험자 서버 API, 앱 설정(Settings.json) 제공자, 로거, 효과음 매니저, 서버 로그 매니저를 할당함.
        /// </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, VisitorInfoProvider visitorInfoProvider, SelectedLevelStore selectedLevelStore, UnlockedLevelStore unlockedLevelStore, AdminLevelJumpStore levelJumpStore,
            VisitorApiClient visitorApiClient, AppSettingsProvider settingsProvider, ILogger<TitleFlowController> logger, SoundManager soundManager = null, ApiManagerBase apiManager = null)
        {
            _sceneTransition = sceneTransition;
            _visitorInfoProvider = visitorInfoProvider;
            _selectedLevelStore = selectedLevelStore;
            _unlockedLevelStore = unlockedLevelStore;
            _levelJumpStore = levelJumpStore;
            _visitorApiClient = visitorApiClient;
            _settingsProvider = settingsProvider;
            _logger = logger;
            _soundManager = soundManager;
            _apiManager = apiManager;
        }

        /// <summary>
        /// 버튼 이벤트를 연결하고, QR 표시 여부/블링크 연출과 00_Common.json 연출 타이밍을 비동기로 처리함.
        /// 0_Title은 앱이 처음 켜졌을 때뿐 아니라 아웃트로에서 홈으로 돌아오거나 비활동 타임아웃으로도 진입하므로, 여기서 모드와 상관없이
        /// 레벨 진행도·QR로 확인한 체험자·관리자 레벨 이동 표시를 초기화해 이전 체험자의 상태가 다음 체험자에게 넘어가지 않도록 함.
        /// </summary>
        private void Start()
        {
            if (_unlockedLevelStore != null) _unlockedLevelStore.Reset();
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] unlockedLevelStore가 null이라 레벨 진행도를 초기화할 수 없음.");
            if (_visitorInfoProvider != null) _visitorInfoProvider.ClearServerVisitor();
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] visitorInfoProvider가 null이라 QR로 확인한 체험자 기록을 비울 수 없음.");
            if (_levelJumpStore != null) _levelJumpStore.EndLevelJump();
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] levelJumpStore가 null이라 관리자 레벨 이동 표시를 비울 수 없음.");
            if (_selectedLevelStore != null) _selectedLevelStore.SelectedLevel = 1;
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] selectedLevelStore가 null이라 선택 레벨을 초기화할 수 없음.");

            if (startButton) startButton.onClick.AddListener(OnStartClicked);
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] startButton이 null임.");

            // 서버 연동 여부 확인이 끝나기 전까지 안내·버튼이 잠깐 노출됐다 바뀌는 플리커를 방지하기 위해 먼저 숨겨둠
            if (qrCanvasGroup) qrCanvasGroup.gameObject.SetActive(false);
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] qrCanvasGroup이 null이라 하단 안내를 표시하지 않음.");
            if (!guideText && _logger != null) _logger.ZLogWarning($"[TitleFlowController] guideText가 null이라 안내 문구를 바꿀 수 없음.");
            if (startButton) startButton.gameObject.SetActive(false);

            CancellationToken token = this.GetCancellationTokenOnDestroy();
            ApplyGuideAsync(token).Forget();
            LoadCommonSettingsAsync(token).Forget();
        }

        /// <summary>
        /// 서버 연동(관리자 화면의 운영 모드)이면 QR 안내를 띄우고 시작 버튼을 숨긴 채 QR 입력을 기다리고, 로컬 모드면 시작 안내와 버튼을 바로 보여줌.
        /// 안내는 어느 쪽이든 천천히 깜빡이며, 페이드 시간·QR 확인 시간·스캐너 글자 간격은 0_Title.json(TitleSceneSettings)에서 읽어와 재빌드 없이 조정 가능.
        /// </summary>
        private async UniTaskVoid ApplyGuideAsync(CancellationToken token)
        {
            try
            {
                bool isServerConnected = false;
                if (_visitorInfoProvider != null)
                    isServerConnected = _visitorInfoProvider.IsServerConnected;
                else if (_logger != null)
                    _logger.ZLogWarning($"[TitleFlowController] visitorInfoProvider가 null이라 서버 미연동으로 보고 시작 안내를 표시함.");

                if (isServerConnected) WaitForQr();
                else ShowStartGuide(Constants.TitleMessages.StartGuide);

                if (qrCanvasGroup) qrCanvasGroup.gameObject.SetActive(true); // null이면 Start에서 이미 경고함

                // 안내가 없어도 QR 확인·스캐너 값은 써야 하므로 설정은 항상 읽음
                string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Title}";
                _sceneSettings = await JsonLoader.LoadAsync<TitleSceneSettings>(path, token);
                ApplyScanCharGap();

                if (!qrCanvasGroup) return;

                _qrBlinkTween = qrCanvasGroup.DOFade(_sceneSettings.qrBlinkMinAlpha, _sceneSettings.qrFadeDuration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetLink(qrCanvasGroup.gameObject);
            }
            catch (OperationCanceledException)
            {
                // 씬 전환 등으로 오브젝트가 파괴되어 취소된 경우 — 정상 종료
            }
        }

        /// <summary> 0_Title.json의 스캐너 글자 사이 최대 간격을 적용함. 0 이하면 경고를 남기고 기본값을 씀. </summary>
        private void ApplyScanCharGap()
        {
            float gap = _sceneSettings.scanCharGapSeconds;
            if (gap > 0f)
            {
                _scanBuffer.MaxCharGapSeconds = gap;
                return;
            }

            _scanBuffer.MaxCharGapSeconds = ScanInputBuffer.DefaultMaxCharGapSeconds;
            if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] 0_Title.json의 scanCharGapSeconds({gap})가 0 이하라 기본값 {ScanInputBuffer.DefaultMaxCharGapSeconds}초를 씀.");
        }

        /// <summary> 시작 버튼을 숨기고 QR 안내를 띄운 뒤 키보드(바코드 스캐너) 문자 입력을 받기 시작함. </summary>
        private void WaitForQr()
        {
            if (startButton) startButton.gameObject.SetActive(false);
            if (guideText) guideText.text = Constants.TitleMessages.QrGuide;
            StartScanning();
        }

        /// <summary> 키보드(바코드 스캐너) 문자 입력을 받기 시작함. 이미 받고 있으면 모은 문자만 비움. </summary>
        private void StartScanning()
        {
            _scanBuffer.Clear();
            _isWaitingForQr = true;

            foreach (InputDevice device in InputSystem.devices)
                if (device is Keyboard keyboard) SubscribeScanKeyboard(keyboard);

            // 이미 받고 있는 중에 다시 불려도 장치 연결 이벤트가 두 번 걸리지 않게 뺐다가 검
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;

            if (_scanKeyboards.Count == 0 && _logger != null)
                _logger.ZLogWarning($"[TitleFlowController] 연결된 키보드(바코드 스캐너)가 없음. 장치가 연결되면 QR 입력을 받기 시작함.");
        }

        /// <summary> QR 대기 중 연결·재연결된 키보드(스캐너)는 입력을 받도록 구독하고, 빠진 장치는 목록에서 뺌. </summary>
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is not Keyboard keyboard) return;

            switch (change)
            {
                // 스캐너를 다시 꽂으면 Input System은 같은 장치를 Added가 아니라 Reconnected로 알림
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    SubscribeScanKeyboard(keyboard);
                    break;

                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    UnsubscribeScanKeyboard(keyboard);
                    break;
            }
        }

        /// <summary> 키보드 하나의 문자 입력을 구독함(중복 구독 방지). </summary>
        private void SubscribeScanKeyboard(Keyboard keyboard)
        {
            if (_scanKeyboards.Contains(keyboard)) return;
            keyboard.onTextInput += OnScanTextInput;
            _scanKeyboards.Add(keyboard);
        }

        /// <summary> 키보드 하나의 문자 입력 구독을 해제함(구독하지 않은 장치면 무시). </summary>
        private void UnsubscribeScanKeyboard(Keyboard keyboard)
        {
            if (!_scanKeyboards.Remove(keyboard)) return;
            keyboard.onTextInput -= OnScanTextInput;
        }

        /// <summary>
        /// 스캐너가 보낸 문자를 모음. 스캐너가 Enter를 CR/LF 문자로 보내는 경우 그 자리에서 인식을 끝냄.
        /// 앞 글자와 scanCharGapSeconds(기본 0.5초)보다 벌어진 글자가 오면 앞에 모은 글자는 이번 스캔이 아니라 버림(글자 내용은 uid일 수 있어 개수만 로그에 남김).
        /// </summary>
        private void OnScanTextInput(char c)
        {
            if (!_isWaitingForQr) return;

            if (c == '\r' || c == '\n')
            {
                SubmitScan();
                return;
            }

            if (char.IsControl(c)) return; // Tab 등 제어 문자는 QR 값이 아님(정상)

            int discarded = _scanBuffer.Append(c, Time.realtimeSinceStartup);
            if (discarded > 0 && _logger != null)
                _logger.ZLogInformation($"[TitleFlowController] 글자 사이가 {_scanBuffer.MaxCharGapSeconds}초 넘게 벌어져 앞에 모은 {discarded}글자를 버리고 새로 모음.");
        }

        /// <summary> Enter가 문자로 오지 않는 장치를 위해, QR 대기 중 어느 키보드든 Enter 키가 눌리면 인식을 끝냄. </summary>
        private void Update()
        {
            if (!_isWaitingForQr) return;

            foreach (Keyboard keyboard in _scanKeyboards)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    SubmitScan();
                    return;
                }
            }
        }

        /// <summary>
        /// 모은 문자열을 QR 값으로 처리함 — 비어 있으면(Enter만 들어온 경우) 무시하고 계속 기다림.
        /// 마지막 글자 뒤로 scanCharGapSeconds(기본 0.5초)보다 늦게 온 Enter면 모은 글자는 스캔이 아니라 손으로 누른 키로 보고 버림.
        /// </summary>
        private void SubmitScan()
        {
            bool isStale = _scanBuffer.IsStale(Time.realtimeSinceStartup);
            string code = _scanBuffer.TakeAndClear();
            if (!_isWaitingForQr || string.IsNullOrWhiteSpace(code)) return;

            if (isStale)
            {
                if (_logger != null) _logger.ZLogInformation($"[TitleFlowController] 마지막 글자보다 {_scanBuffer.MaxCharGapSeconds}초 넘게 늦게 Enter가 와서 모은 {code.Length}글자를 QR로 보지 않고 버림.");
                return;
            }

            OnQrScanned(code);
        }

        /// <summary>
        /// QR 인식이 끝나면 입력 대기를 멈추고 서버에 체험자를 확인함. uid에는 생년월일이 들어 있어 로그에는 길이만 남김.
        /// 시작하기가 떠 있는 동안 다음 사람이 찍은 경우에도 시작 버튼을 숨기고 앞사람 기록(체험자·해금)을 비운 뒤 새로 확인함.
        /// </summary>
        private void OnQrScanned(string code)
        {
            StopWaitingForQr();
            CancelConfirmTimeout();
            if (startButton) startButton.gameObject.SetActive(false);
            ClearConfirmedVisitor();
            if (_logger != null) _logger.ZLogInformation($"[TitleFlowController] QR 인식 완료 (길이 {code.Length})");

            CheckVisitorAsync(code, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// QR uid로 서버에 체험자를 확인함. 'QR 코드를 확인하고 있습니다'를 최소 시간만큼은 보여 준 뒤, 확인되면 시작하기 안내로 바꾸고
        /// 아니면(체험 완료·없는 QR·서버 오류) 이유를 잠시 보여 준 뒤 다시 QR을 기다림.
        /// </summary>
        private async UniTaskVoid CheckVisitorAsync(string uid, CancellationToken token)
        {
            if (guideText) guideText.text = Constants.TitleMessages.QrChecking;
            float checkStartTime = Time.realtimeSinceStartup;

            try
            {
                string failMessage = await ConfirmVisitorAsync(uid, token);

                // 서버가 빨리 답해도 '확인하고 있습니다'가 스치듯 지나가지 않게 최소 시간을 채움 — 이미 지났으면 바로 넘어감
                float remainingSeconds = _sceneSettings.qrCheckingMinSeconds - (Time.realtimeSinceStartup - checkStartTime);
                if (remainingSeconds > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(remainingSeconds), DelayType.UnscaledDeltaTime, cancellationToken: token);

                if (failMessage == null)
                {
                    await ShowConfirmedVisitorAsync(token);
                    return;
                }

                if (guideText) guideText.text = failMessage;

                // 0_Title.json에 음수를 적으면 Delay가 예외를 내 QR 대기로 돌아오지 못하므로 0 이상으로 제한함
                float messageSeconds = Mathf.Max(0f, _sceneSettings.scanResultMessageSeconds);
                await UniTask.Delay(TimeSpan.FromSeconds(messageSeconds), DelayType.UnscaledDeltaTime, cancellationToken: token);
                WaitForQr();
            }
            catch (OperationCanceledException)
            {
                // 확인 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary>
        /// 서버에 체험 가능 여부(checkActive)와 진행도(getUser)를 물어 체험자와 해금 레벨을 기록함.
        /// 체험할 수 없으면 하단에 보여 줄 안내 문구를, 확인되면 null을 돌려줌.
        /// </summary>
        private async UniTask<string> ConfirmVisitorAsync(string uid, CancellationToken token)
        {
            if (_visitorApiClient == null)
            {
                if (_logger != null) _logger.ZLogError($"[TitleFlowController] visitorApiClient가 null이라 체험자를 확인할 수 없음.");
                return Constants.TitleMessages.QrCheckFailed;
            }

            CheckActiveResult active = await _visitorApiClient.CheckActiveAsync(uid, token);
            if (active.Status != CheckActiveStatus.Active) return GetScanFailMessage(active.Status);

            GetUserResult progress = await _visitorApiClient.GetUserAsync(uid, token);
            if (!progress.IsFound) return Constants.TitleMessages.QrCheckFailed;

            if (_visitorInfoProvider != null) _visitorInfoProvider.SetServerVisitor(active.IdxUser, active.Name);
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] visitorInfoProvider가 null이라 확인한 체험자를 기록하지 못함.");

            // 성공·실패와 상관없이 기록이 있는 마지막 레벨의 다음 레벨까지 엶 — 로컬 진행 규칙(ResultFlowController의 UnlockThrough)과 같음
            if (_unlockedLevelStore != null) _unlockedLevelStore.UnlockedLevelCount = progress.UnlockedLevelCount;
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] unlockedLevelStore가 null이라 서버 진행도를 반영하지 못함.");

            return null;
        }

        /// <summary> 체험할 수 없는 QR 확인 결과를 하단 안내 문구로 바꿈. </summary>
        private static string GetScanFailMessage(CheckActiveStatus status)
        {
            return status switch
            {
                CheckActiveStatus.Completed => Constants.TitleMessages.QrCompleted,
                CheckActiveStatus.NotFound  => Constants.TitleMessages.QrNotFound,
                _                           => Constants.TitleMessages.QrCheckFailed
            };
        }

        /// <summary> 하단 안내를 message로 바꾸고 시작 버튼을 보여줌. </summary>
        private void ShowStartGuide(string message)
        {
            if (guideText) guideText.text = message;
            if (startButton) startButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// QR로 확인한 체험자에게 이름이 들어간 시작 안내와 시작 버튼을 보여줌.
        /// 다음 사람이 QR을 찍을 수 있게 스캐너 입력을 계속 받고, 시작하기를 기다린 시간을 재기 시작함.
        /// </summary>
        private async UniTask ShowConfirmedVisitorAsync(CancellationToken token)
        {
            string visitorName = _visitorInfoProvider != null ? await _visitorInfoProvider.GetNameAsync(token) : null;
            ShowStartGuide(string.IsNullOrEmpty(visitorName)
                ? Constants.TitleMessages.StartGuide
                : ZString.Format(Constants.TitleMessages.StartGuideWithNameFormat, visitorName));

            StartScanning();
            StartConfirmTimeout();
        }

        /// <summary> 확인했던 체험자와 서버 진행도로 연 해금 레벨을 비움 — 다음 사람의 QR로 다시 확인하거나 대기 시간이 지났을 때. </summary>
        private void ClearConfirmedVisitor()
        {
            if (_visitorInfoProvider != null) _visitorInfoProvider.ClearServerVisitor();
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] visitorInfoProvider가 null이라 확인한 체험자를 비울 수 없음.");
            if (_unlockedLevelStore != null) _unlockedLevelStore.Reset();
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] unlockedLevelStore가 null이라 해금 레벨을 비울 수 없음.");
        }

        /// <summary> 시작하기를 기다린 시간 재기를 새로 시작함 — 이전에 재던 것은 취소함. </summary>
        private void StartConfirmTimeout()
        {
            CancelConfirmTimeout();
            _confirmTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ConfirmTimeoutAsync(_confirmTimeoutCts).Forget();
        }

        /// <summary> 시작하기를 기다린 시간 재기를 취소함 — 시작하기를 눌렀거나 새 QR이 들어왔거나 씬을 떠날 때. 재고 있지 않으면 아무것도 하지 않음. </summary>
        private void CancelConfirmTimeout()
        {
            if (_confirmTimeoutCts == null) return;

            _confirmTimeoutCts.Cancel();
            _confirmTimeoutCts.Dispose();
            _confirmTimeoutCts = null;
        }

        /// <summary>
        /// 비활동 타이머와 같은 설정(Settings.json의 useInactivityTimer·resetTime)으로, 시작하기를 누르지 않은 채
        /// 그 시간이 지나면 서버에 move_idle_timeout을 보내고 확인한 체험자를 비운 뒤 다시 QR을 기다림. 비활동 타이머가 꺼져 있으면 계속 기다림.
        /// 타이틀에서 난 비활동 타임아웃은 APIManager가 보내지 않으므로, 타이틀의 move_idle_timeout은 이 경우에만 남음.
        /// </summary>
        private async UniTaskVoid ConfirmTimeoutAsync(CancellationTokenSource cts)
        {
            // 취소 시 CancelConfirmTimeout이 CTS를 바로 해제하므로 토큰을 먼저 받아 둠
            CancellationToken token = cts.Token;

            try
            {
                if (_settingsProvider == null)
                {
                    if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] settingsProvider가 null이라 시작하기 대기 시간 제한 없이 기다림.");
                    return;
                }

                Settings settings = await _settingsProvider.GetAsync(token);
                if (settings == null || !settings.useInactivityTimer || settings.resetTime <= 0f)
                {
                    if (_logger != null) _logger.ZLogInformation($"[TitleFlowController] 비활동 타이머가 꺼져 있어 시작하기 대기 시간 제한 없이 기다림.");
                    return;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(settings.resetTime), DelayType.UnscaledDeltaTime, cancellationToken: token);

                if (_logger != null) _logger.ZLogInformation($"[TitleFlowController] {settings.resetTime}초 동안 시작하기를 누르지 않아 QR 대기로 돌아감.");
                // 로그 전송은 씬과 상관없이 끝까지 보내도록 이 오브젝트의 토큰을 넘기지 않음
                if (_apiManager != null) _apiManager.SendMoveIdleTimeoutLogAsync().Forget();
                else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] apiManager가 null이라 시작하기 대기 시간 초과 로그(move_idle_timeout)를 보내지 않음.");
                ClearConfirmedVisitor();
                WaitForQr();
            }
            catch (OperationCanceledException)
            {
                // 시작하기·새 QR·씬 파괴로 취소된 정상 흐름
            }
            finally
            {
                // 다른 재기가 이미 새로 시작됐으면 그 CTS는 건드리지 않음
                if (_confirmTimeoutCts == cts)
                {
                    _confirmTimeoutCts.Dispose();
                    _confirmTimeoutCts = null;
                }
            }
        }

        /// <summary> 스캐너 문자 입력·장치 연결 구독을 해제함. </summary>
        private void StopWaitingForQr()
        {
            _isWaitingForQr = false;
            InputSystem.onDeviceChange -= OnDeviceChange;
            foreach (Keyboard keyboard in _scanKeyboards) keyboard.onTextInput -= OnScanTextInput;
            _scanKeyboards.Clear();
        }

        /// <summary> 00_Common.json(CommonSettings)을 비동기로 로드함. </summary>
        private async UniTaskVoid LoadCommonSettingsAsync(CancellationToken token)
        {
            _commonSettings = await CommonSettingsProvider.GetAsync(token);
        }

        /// <summary> 깜빡임 트윈, 스캐너 입력 구독, 시작하기 대기 시간 재기, 버튼 리스너를 정리함. </summary>
        private void OnDestroy()
        {
            if (_qrBlinkTween != null && _qrBlinkTween.IsActive()) _qrBlinkTween.Kill();
            StopWaitingForQr();
            CancelConfirmTimeout();
            if (startButton) startButton.onClick.RemoveListener(OnStartClicked);
        }

        /// <summary>
        /// 시작 버튼 클릭 시 게임 시작 효과음을 내고 화면 페이드와 함께 인트로 씬으로 전환함.
        /// 넘어가는 페이드 동안 QR이 찍혀 체험자가 바뀌거나 대기 시간이 지나 QR 대기로 돌아가지 않게 둘 다 멈춤.
        /// </summary>
        private void OnStartClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[TitleFlowController] sceneTransition이 null이라 {Constants.Scenes.Intro} 씬을 로드할 수 없음.");
                return;
            }

            _isBusy = true;
            StopWaitingForQr();
            CancelConfirmTimeout();
            SoundEffects.Play(_soundManager, Constants.Sounds.GameStart, _logger);
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Intro, _commonSettings.sceneTransitionFadeDuration).Forget();
        }
    }
}
