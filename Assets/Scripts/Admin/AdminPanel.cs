using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VContainer;
using HuliacDev.UI;
using ZLogger;

namespace DGAIZone.Admin
{
    /// <summary>
    /// 비밀번호를 통과하면 열리는 관리자 화면 — 운영 모드(로컬/서버)·체험자 이름·비밀번호를 바꾸고,
    /// 고른 레벨까지 해금한 뒤 2_LevelSelect에서 그 레벨의 스토리를 바로 띄움(관리자 레벨 이동).
    /// 입력 없이 Admin.json의 idleCloseSeconds(기본 60초)가 지나면 위에 뜬 이름 입력·비밀번호 변경 창과 함께 닫힘.
    /// </summary>
    public class AdminPanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        [Header("운영 모드")]
        [SerializeField] private Button localModeButton;
        [SerializeField] private Button serverModeButton;
        [Tooltip("지금 모드 버튼의 배경색")]
        [SerializeField] private Color selectedModeColor = new(0.55f, 0.75f, 1f, 1f);
        [Tooltip("지금 모드가 아닌 버튼의 배경색")]
        [SerializeField] private Color normalModeColor = new(0.82f, 0.82f, 0.82f, 1f);

        [Header("체험자 이름")]
        [SerializeField] private TMP_Text visitorNameText;
        [SerializeField] private Button changeNameButton;
        [SerializeField] private VisitorNamePanel namePanel;

        [Header("비밀번호")]
        [SerializeField] private Button changePasswordButton;
        [SerializeField] private AdminPasswordPanel passwordPanel;

        [Header("레벨 이동")]
        [Tooltip("레벨1부터 순서대로 — 누르면 그 레벨까지 해금하고 2_LevelSelect에서 그 레벨 스토리를 바로 띄움")]
        [SerializeField] private Button[] levelButtons;

        [Tooltip("변경 결과 안내 문구")]
        [SerializeField] private TMP_Text statusText;

        // 이 시간(초) 동안 입력이 없으면 관리자 화면을 닫음 — 열 때마다 Admin.json(idleCloseSeconds)에서 다시 읽음
        private float _idleCloseSeconds = Constants.Admin.DefaultIdleCloseSeconds;

        private readonly IdleCloseTimer _idleTimer = new();
        private UnityAction[] _levelActions;
        private bool _isLeaving; // 레벨 이동·타이틀 다시 불러오기로 씬을 떠나는 중이면 버튼 입력과 자동 닫기를 무시함

        // 관리자 화면을 열 때의 모드 — 닫을 때 달라졌으면 타이틀을 다시 불러 안내(QR·시작하기)에 반영함
        private bool _modeAtOpen;

        private ILogger<AdminPanel> _logger;
        private VisitorSettings _visitorSettings;
        private UnlockedLevelStore _unlockedLevelStore;
        private SelectedLevelStore _selectedLevelStore;
        private AdminLevelJumpStore _levelJumpStore;
        private SceneTransitionService _sceneTransition;
        private VisitorInfoProvider _visitorInfoProvider;
        private SoundManager _soundManager;

        /// <summary> VContainer 의존성 주입. 로거, 체험자 설정, 해금·선택 레벨 저장소, 관리자 레벨 이동 저장소, 씬 전환 서비스, 체험자 정보 제공자, 효과음 매니저를 할당함. </summary>
        [Inject]
        public void Construct(
            ILogger<AdminPanel> logger,
            VisitorSettings visitorSettings,
            UnlockedLevelStore unlockedLevelStore,
            SelectedLevelStore selectedLevelStore,
            AdminLevelJumpStore levelJumpStore,
            SceneTransitionService sceneTransition,
            VisitorInfoProvider visitorInfoProvider,
            SoundManager soundManager = null)
        {
            _logger = logger;
            _visitorSettings = visitorSettings;
            _unlockedLevelStore = unlockedLevelStore;
            _selectedLevelStore = selectedLevelStore;
            _levelJumpStore = levelJumpStore;
            _sceneTransition = sceneTransition;
            _visitorInfoProvider = visitorInfoProvider;
            _soundManager = soundManager;
        }

        /// <summary> 버튼에 동작을 연결함(패널이 처음 켜질 때 한 번 실행). </summary>
        private void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] closeButton이 null이라 관리자 화면을 닫을 수 없음.");

            if (localModeButton) localModeButton.onClick.AddListener(OnLocalModeClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] localModeButton이 null임.");

            if (serverModeButton) serverModeButton.onClick.AddListener(OnServerModeClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] serverModeButton이 null임.");

            if (changeNameButton) changeNameButton.onClick.AddListener(OnChangeNameClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] changeNameButton이 null임.");

            if (changePasswordButton) changePasswordButton.onClick.AddListener(OnChangePasswordClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] changePasswordButton이 null임.");

            if (!visitorNameText && _logger != null) _logger.ZLogWarning($"[AdminPanel] visitorNameText가 null이라 체험자 이름을 표시할 수 없음.");
            if (!statusText && _logger != null) _logger.ZLogWarning($"[AdminPanel] statusText가 null이라 변경 결과를 표시할 수 없음.");

            if (levelButtons == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[AdminPanel] levelButtons가 null이라 레벨 이동을 쓸 수 없음.");
                return;
            }

            _levelActions = new UnityAction[levelButtons.Length];
            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (!levelButtons[i])
                {
                    if (_logger != null) _logger.ZLogWarning($"[AdminPanel] 레벨{i + 1} 버튼이 null임.");
                    continue;
                }

                int level = i + 1;
                _levelActions[i] = () => OnLevelClicked(level);
                levelButtons[i].onClick.AddListener(_levelActions[i]);
            }
        }

        /// <summary> 버튼 연결을 해제함. </summary>
        private void OnDestroy()
        {
            if (closeButton) closeButton.onClick.RemoveListener(OnCloseClicked);
            if (localModeButton) localModeButton.onClick.RemoveListener(OnLocalModeClicked);
            if (serverModeButton) serverModeButton.onClick.RemoveListener(OnServerModeClicked);
            if (changeNameButton) changeNameButton.onClick.RemoveListener(OnChangeNameClicked);
            if (changePasswordButton) changePasswordButton.onClick.RemoveListener(OnChangePasswordClicked);

            if (_levelActions == null) return;
            for (int i = 0; i < levelButtons.Length; i++)
                if (levelButtons[i] && _levelActions[i] != null) levelButtons[i].onClick.RemoveListener(_levelActions[i]);
        }

        /// <summary> 지금 설정값을 보여 주며 관리자 화면을 엶. </summary>
        public void Open()
        {
            gameObject.SetActive(true);
            _idleTimer.Restart();
            LoadIdleCloseSecondsAsync(this.GetCancellationTokenOnDestroy()).Forget();
            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 관리자 화면을 엶.");

            if (_visitorSettings) _modeAtOpen = _visitorSettings.IsServerConnected;
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] VisitorSettings가 null이라 모드·이름을 바꿀 수 없음.");

            RefreshMode();
            RefreshVisitorName();
            ShowStatus(string.Empty);
        }

        /// <summary> 현장에서 바뀌었을 수 있는 자동 닫기 시간을 Admin.json에서 다시 읽음. </summary>
        private async UniTaskVoid LoadIdleCloseSecondsAsync(CancellationToken token)
        {
            try
            {
                AdminSettings settings = await AdminSettings.LoadAsync(token, _logger);
                _idleCloseSeconds = settings.idleCloseSeconds;
            }
            catch (OperationCanceledException)
            {
                // 읽는 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary> 변경 결과 안내 문구를 표시함(빈 문자열이면 지움, statusText 누락은 Awake에서 경고함). </summary>
        public void ShowStatus(string message)
        {
            if (statusText) statusText.text = message;
        }

        /// <summary> 입력 없이 정해진 시간이 지나면 위에 떠 있는 이름 입력·비밀번호 변경 창까지 함께 닫음(화면이 열려 있을 때만 실행됨). </summary>
        private void Update()
        {
            if (_isLeaving || !_idleTimer.HasExpired(_idleCloseSeconds)) return;

            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] {_idleCloseSeconds}초 동안 입력이 없어 관리자 화면을 닫음.");
            if (namePanel) namePanel.Close();
            if (passwordPanel) passwordPanel.Close();
            Close();
        }

        /// <summary> 클릭음을 내고 관리자 화면을 닫음. </summary>
        private void OnCloseClicked()
        {
            if (_isLeaving)
            {
                if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 이미 화면을 떠나는 중이라 닫기를 무시함.");
                return;
            }

            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            Close();
        }

        /// <summary> 관리자 화면을 닫음. 모드가 바뀌었으면 타이틀을 다시 불러 안내에 반영함. </summary>
        private void Close()
        {
            gameObject.SetActive(false);

            if (!_visitorSettings || _visitorSettings.IsServerConnected == _modeAtOpen) return;

            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 운영 모드가 바뀌어 타이틀을 다시 불러옴.");
            LoadSceneAsync(Constants.Scenes.Title, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 로컬 모드(QR 없이 시작하기)로 바꿈. </summary>
        private void OnLocalModeClicked()
        {
            SetServerConnected(false);
        }

        /// <summary> 서버 모드(QR 인식 후 시작하기)로 바꿈. </summary>
        private void OnServerModeClicked()
        {
            SetServerConnected(true);
        }

        /// <summary> 운영 모드를 저장하고 버튼 표시와 안내를 갱신함. 이미 그 모드면 아무것도 바꾸지 않음. </summary>
        private void SetServerConnected(bool isServerConnected)
        {
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            if (!_visitorSettings || _visitorSettings.IsServerConnected == isServerConnected) return; // 설정 누락은 Open에서 경고함

            _visitorSettings.IsServerConnected = isServerConnected;
            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 운영 모드를 {(isServerConnected ? "서버" : "로컬")}로 바꿈.");

            RefreshMode();
            ShowStatus(isServerConnected ? Constants.Admin.ServerModeSet : Constants.Admin.LocalModeSet);
        }

        /// <summary> 지금 모드 버튼만 강조색으로 칠함. </summary>
        private void RefreshMode()
        {
            if (!_visitorSettings) return; // Open에서 경고함

            bool isServerConnected = _visitorSettings.IsServerConnected;
            if (localModeButton && localModeButton.image) localModeButton.image.color = isServerConnected ? normalModeColor : selectedModeColor;
            if (serverModeButton && serverModeButton.image) serverModeButton.image.color = isServerConnected ? selectedModeColor : normalModeColor;
        }

        /// <summary> 지금 체험자 이름을 표시함. </summary>
        private void RefreshVisitorName()
        {
            if (_visitorSettings && visitorNameText) visitorNameText.text = _visitorSettings.VisitorName; // 누락은 Awake·Open에서 경고함
        }

        /// <summary> 이름 입력 창을 엶. </summary>
        private void OnChangeNameClicked()
        {
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            ShowStatus(string.Empty);

            if (namePanel) namePanel.Open(OnVisitorNameSaved, _idleCloseSeconds);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] namePanel이 null이라 이름 입력 창을 열 수 없음.");
        }

        /// <summary> 이름 입력 창에서 저장한 이름을 체험자 설정에 저장함. </summary>
        private void OnVisitorNameSaved(string visitorName)
        {
            if (!_visitorSettings)
            {
                if (_logger != null) _logger.ZLogWarning($"[AdminPanel] VisitorSettings가 null이라 체험자 이름을 저장할 수 없음.");
                return;
            }

            _visitorSettings.VisitorName = visitorName;
            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 체험자 이름을 '{visitorName}'(으)로 바꿈.");

            RefreshVisitorName();
            ShowStatus(Constants.Admin.VisitorNameChanged);
        }

        /// <summary> 비밀번호 창을 변경용으로 엶 — 저장 결과는 비밀번호 창이 ShowStatus로 알림. </summary>
        private void OnChangePasswordClicked()
        {
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            ShowStatus(string.Empty);

            if (passwordPanel) passwordPanel.OpenForChange();
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] passwordPanel이 null이라 비밀번호를 바꿀 수 없음.");
        }

        /// <summary>
        /// 고른 레벨(1부터)까지 해금하고 그 레벨을 고른 상태로 2_LevelSelect로 이동함. 2_LevelSelect는 레벨 선택 화면 대신 그 레벨의 스토리를 바로 띄움.
        /// 이 판은 결과 화면의 다음 버튼으로 타이틀의 관리자 화면에 돌아오고, 결과를 서버에 올리지 않음.
        /// </summary>
        private void OnLevelClicked(int level)
        {
            if (_isLeaving)
            {
                if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 이미 화면을 떠나는 중이라 레벨 {level} 이동을 무시함.");
                return;
            }

            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            if (_levelJumpStore == null || _unlockedLevelStore == null || _selectedLevelStore == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[AdminPanel] 레벨 이동에 필요한 저장소가 주입되지 않아 레벨{level}로 이동할 수 없음.");
                return;
            }

            _unlockedLevelStore.UnlockedLevelCount = Mathf.Max(_unlockedLevelStore.UnlockedLevelCount, level);
            _selectedLevelStore.SelectedLevel = level;
            _levelJumpStore.Begin(level);

            // 관리자 시험 판이라 QR로 확인해 둔 체험자의 이름이 스토리·게임 화면과 행동 로그에 나오지 않게 비움(결과 업로드는 IsLevelJump로 막혀 있음)
            if (_visitorInfoProvider != null) _visitorInfoProvider.ClearServerVisitor();
            else if (_logger != null) _logger.ZLogWarning($"[AdminPanel] visitorInfoProvider가 null이라 QR로 확인한 체험자를 비우지 못하고 레벨{level}로 이동함.");
            if (_logger != null) _logger.ZLogInformation($"[AdminPanel] 레벨{level}까지 해금하고 레벨{level} 스토리로 이동함.");

            LoadSceneAsync(Constants.Scenes.LevelSelect, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 00_Common.json의 씬 전환 페이드 시간으로 sceneName 씬을 불러옴. 이후 버튼 입력은 무시함. </summary>
        private async UniTaskVoid LoadSceneAsync(string sceneName, CancellationToken token)
        {
            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogError($"[AdminPanel] sceneTransition이 null이라 {sceneName} 씬을 로드할 수 없음.");
                return;
            }

            _isLeaving = true;
            try
            {
                CommonSettings commonSettings = await CommonSettingsProvider.GetAsync(token);
                _sceneTransition.LoadSceneWithFadeAsync(sceneName, commonSettings.sceneTransitionFadeDuration).Forget();
            }
            catch (OperationCanceledException)
            {
                // 설정을 읽는 도중 오브젝트가 파괴된 경우 — 정상 종료
            }
        }
    }
}
