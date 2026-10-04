using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using HuliacDev.UI;
using HuliacDev.Utils;
using ZLogger;

namespace DGAIZone.Title
{
    /// <summary>
    /// 타이틀 씬의 화면 흐름 제어. 시작 버튼을 누르면 화면 페이드와 함께 인트로 씬으로 전환함.
    /// 서버(QR 스캔) 연동이면 "QR 코드를 인식하여 주세요"를 띄우고 시작 버튼을 숨긴 채 QR 입력을 기다렸다가,
    /// 인식되면 "시작하기를 눌러주세요"와 시작 버튼을 보여줌. 미연동(로컬)이면 QR 단계 없이 바로 시작 안내와 버튼을 보여줌.
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
        private ILogger<TitleFlowController> _logger;
        private SoundManager _soundManager;
        private bool _isBusy;

        // 무한 반복 깜빡임이라 씬을 떠날 때 직접 Kill함
        private Tween _qrBlinkTween;

        // USB 바코드 스캐너는 키보드처럼 문자를 입력한 뒤 Enter를 보냄 — Enter 전까지 모은 문자열이 QR 값.
        // 스캐너는 본체 키보드와 별개의 키보드 장치로 잡히므로 연결된 키보드 전부(나중에 꽂힌 것 포함)를 구독함.
        private readonly StringBuilder _scanBuffer = new();
        private readonly List<Keyboard> _scanKeyboards = new();
        private bool _isWaitingForQr;

        // 00_Common.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀.
        // 씬 전환 페이드 시간은 다른 씬들과 마찬가지로 00_Common.json의 sceneTransitionFadeDuration을 공유해서 쓰며,
        // 씬별로 값이 갈리지 않도록 함(현장에서 페이드 시간을 한 곳만 바꾸면 전체 씬에 일관되게 반영됨).
        private CommonSettings _commonSettings = new CommonSettings();

        /// <summary> VContainer 의존성 주입. 씬 전환 서비스, 체험자 정보 제공자, 선택/잠금 해제 레벨 저장소, 로거, 효과음 매니저를 할당함. </summary>
        [Inject]
        public void Construct(SceneTransitionService sceneTransition, VisitorInfoProvider visitorInfoProvider, SelectedLevelStore selectedLevelStore, UnlockedLevelStore unlockedLevelStore, ILogger<TitleFlowController> logger, SoundManager soundManager = null)
        {
            _sceneTransition = sceneTransition;
            _visitorInfoProvider = visitorInfoProvider;
            _selectedLevelStore = selectedLevelStore;
            _unlockedLevelStore = unlockedLevelStore;
            _logger = logger;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 버튼 이벤트를 연결하고, QR 표시 여부/블링크 연출과 00_Common.json 연출 타이밍을 비동기로 처리함.
        /// 0_Title은 앱이 처음 켜졌을 때뿐 아니라 아웃트로에서 홈으로 돌아오거나 비활동 타임아웃으로도 진입하므로,
        /// 여기서 레벨 진행도를 초기화해 이전 체험자의 잠금 해제 상태가 다음 체험자에게 넘어가지 않도록 함.
        /// </summary>
        private void Start()
        {
            if (_unlockedLevelStore != null) _unlockedLevelStore.Reset();
            else if (_logger != null) _logger.ZLogWarning($"[TitleFlowController] unlockedLevelStore가 null이라 레벨 진행도를 초기화할 수 없음.");
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
        /// 서버 연동이면 QR 안내를 띄우고 시작 버튼을 숨긴 채 QR 입력을 기다리고, 미연동이면 시작 안내와 버튼을 바로 보여줌.
        /// 안내는 어느 쪽이든 천천히 깜빡이며, 페이드 시간은 0_Title.json(TitleSceneSettings)에서 읽어와 재빌드 없이 조정 가능.
        /// </summary>
        private async UniTaskVoid ApplyGuideAsync(CancellationToken token)
        {
            try
            {
                bool isServerConnected = false;
                if (_visitorInfoProvider != null)
                    isServerConnected = await _visitorInfoProvider.IsServerConnectedAsync(token);
                else if (_logger != null)
                    _logger.ZLogWarning($"[TitleFlowController] visitorInfoProvider가 null이라 서버 미연동으로 보고 시작 안내를 표시함.");

                if (isServerConnected) WaitForQr();
                else ShowStartGuide();

                if (!qrCanvasGroup) return; // Start에서 이미 경고함
                qrCanvasGroup.gameObject.SetActive(true);

                string path = $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Title}";
                TitleSceneSettings sceneSettings = await JsonLoader.LoadAsync<TitleSceneSettings>(path, token);

                _qrBlinkTween = qrCanvasGroup.DOFade(sceneSettings.qrBlinkMinAlpha, sceneSettings.qrFadeDuration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetLink(qrCanvasGroup.gameObject);
            }
            catch (OperationCanceledException)
            {
                // 씬 전환 등으로 오브젝트가 파괴되어 취소된 경우 — 정상 종료
            }
        }

        /// <summary> QR 안내를 띄우고 키보드(바코드 스캐너) 문자 입력을 받기 시작함. </summary>
        private void WaitForQr()
        {
            if (guideText) guideText.text = Constants.TitleMessages.QrGuide;

            _scanBuffer.Clear();
            _isWaitingForQr = true;

            foreach (InputDevice device in InputSystem.devices)
                if (device is Keyboard keyboard) SubscribeScanKeyboard(keyboard);
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

        /// <summary> 스캐너가 보낸 문자를 모음. 스캐너가 Enter를 CR/LF 문자로 보내는 경우 그 자리에서 인식을 끝냄. </summary>
        private void OnScanTextInput(char c)
        {
            if (!_isWaitingForQr) return;

            if (c == '\r' || c == '\n') SubmitScan();
            else if (!char.IsControl(c)) _scanBuffer.Append(c);
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

        /// <summary> 모은 문자열을 QR 값으로 처리함 — 비어 있으면(Enter만 들어온 경우) 무시하고 계속 기다림. </summary>
        private void SubmitScan()
        {
            string code = _scanBuffer.ToString();
            _scanBuffer.Clear();
            if (!_isWaitingForQr || string.IsNullOrWhiteSpace(code)) return;

            StopWaitingForQr();
            if (_logger != null) _logger.ZLogInformation($"[TitleFlowController] QR 인식 완료 (길이 {code.Length})");

            // TODO: 서버 연동 시 — code로 체험자 정보·진행도를 조회해 VisitorInfoProvider에 반영할 것.
            ShowStartGuide();
        }

        /// <summary> 하단 안내를 "시작하기를 눌러주세요"로 바꾸고 시작 버튼을 보여줌. </summary>
        private void ShowStartGuide()
        {
            if (guideText) guideText.text = Constants.TitleMessages.StartGuide;
            if (startButton) startButton.gameObject.SetActive(true);
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

        /// <summary> 깜빡임 트윈, 스캐너 입력 구독, 버튼 리스너 해제. </summary>
        private void OnDestroy()
        {
            if (_qrBlinkTween != null && _qrBlinkTween.IsActive()) _qrBlinkTween.Kill();
            StopWaitingForQr();
            if (startButton) startButton.onClick.RemoveListener(OnStartClicked);
        }

        /// <summary> 시작 버튼 클릭 시 게임 시작 효과음을 내고 화면 페이드와 함께 인트로 씬으로 전환함. </summary>
        private void OnStartClicked()
        {
            if (_isBusy) return;

            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[TitleFlowController] sceneTransition이 null이라 {Constants.Scenes.Intro} 씬을 로드할 수 없음.");
                return;
            }

            _isBusy = true;
            SoundEffects.Play(_soundManager, Constants.Sounds.GameStart, _logger);
            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Intro, _commonSettings.sceneTransitionFadeDuration).Forget();
        }
    }
}
