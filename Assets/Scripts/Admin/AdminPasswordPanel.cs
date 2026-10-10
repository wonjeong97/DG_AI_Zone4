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
using HuliacDev.Utils;
using ZLogger;

namespace DGAIZone.Admin
{
    /// <summary>
    /// 관리자 비밀번호 입력 창 — 키보드·마우스가 없는 전시 환경이라 화면 키패드(789/456/123/확인0←)로 입력받음.
    /// 비밀번호가 맞으면 관리자 화면을 열고, 틀리면 안내 후 입력을 지우며, 닫기 버튼을 누르거나 일정 시간 입력이 없으면 닫힘.
    /// 관리자 화면의 비밀번호 변경도 같은 키패드로 받음 — 새 비밀번호를 두 번 입력해 같으면 Admin.json에 저장함.
    /// </summary>
    public class AdminPasswordPanel : MonoBehaviour
    {
        [Tooltip("숫자 키 0~9 — 배열 인덱스가 그 키가 입력하는 숫자")]
        [SerializeField] private Button[] digitButtons = new Button[10];
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backspaceButton;
        [SerializeField] private Button closeButton;

        [Tooltip("키패드 위 안내 문구 — 비밀번호 확인·새 비밀번호·한 번 더 입력 단계마다 바뀜")]
        [SerializeField] private TMP_Text promptText;
        [Tooltip("입력한 자릿수만큼 ●를 표시하는 텍스트")]
        [SerializeField] private TMP_Text maskedText;
        [Tooltip("자릿수 부족·비밀번호 오류 안내 텍스트")]
        [SerializeField] private TMP_Text messageText;

        [SerializeField] private AdminPanel adminPanel;

        // 이 시간(초) 동안 입력이 없으면 창을 닫음 — 열 때마다 Admin.json(passwordIdleCloseSeconds)에서 다시 읽음
        private float _idleCloseSeconds = Constants.Admin.DefaultPasswordIdleCloseSeconds;

        // 지금 받는 입력 — 관리자 진입 비밀번호 확인, 또는 비밀번호 변경의 새 비밀번호·한 번 더 입력
        private enum Step
        {
            Verify,
            EnterNew,
            ConfirmNew
        }

        // 자릿수별 표시 문자열 — 키를 누를 때마다 문자열을 새로 만들지 않도록 미리 만들어 둠
        private readonly static string[] MaskTexts = CreateMaskTexts();

        private readonly PasswordInput _input = new();
        private readonly IdleCloseTimer _idleTimer = new();
        private UnityAction[] _digitActions;
        private string _password = Constants.Admin.DefaultPassword;
        private Step _step;
        private string _newPassword;

        private ILogger<AdminPasswordPanel> _logger;
        private SoundManager _soundManager;

        /// <summary> VContainer 의존성 주입. 로거와 효과음 매니저를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<AdminPasswordPanel> logger, SoundManager soundManager = null)
        {
            _logger = logger;
            _soundManager = soundManager;
        }

        /// <summary> 키패드 버튼에 입력 동작을 연결함(패널이 처음 켜질 때 한 번 실행). </summary>
        private void Awake()
        {
            _digitActions = new UnityAction[digitButtons.Length];
            for (int i = 0; i < digitButtons.Length; i++)
            {
                if (!digitButtons[i])
                {
                    if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] 숫자 {i} 버튼이 null임.");
                    continue;
                }

                int digit = i;
                _digitActions[i] = () => OnDigitClicked(digit);
                digitButtons[i].onClick.AddListener(_digitActions[i]);
            }

            if (confirmButton) confirmButton.onClick.AddListener(OnConfirmClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] confirmButton이 null임.");

            if (backspaceButton) backspaceButton.onClick.AddListener(OnBackspaceClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] backspaceButton이 null임.");

            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] closeButton이 null임.");

            // 아래 텍스트는 키를 누를 때마다 갱신하는 자리라 누락 경고는 여기서 한 번만 남김
            if (!promptText && _logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] promptText가 null이라 단계 안내를 표시할 수 없음.");
            if (!maskedText && _logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] maskedText가 null이라 입력한 자릿수를 표시할 수 없음.");
            if (!messageText && _logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] messageText가 null이라 오류 안내를 표시할 수 없음.");
        }

        /// <summary> 키패드 버튼 연결을 해제함. </summary>
        private void OnDestroy()
        {
            if (_digitActions != null)
                for (int i = 0; i < digitButtons.Length; i++)
                    if (digitButtons[i] && _digitActions[i] != null) digitButtons[i].onClick.RemoveListener(_digitActions[i]);

            if (confirmButton) confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (backspaceButton) backspaceButton.onClick.RemoveListener(OnBackspaceClicked);
            if (closeButton) closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        /// <summary> 관리자 진입용으로 창을 열고, 현장에서 바뀌었을 수 있는 비밀번호·자동 닫기 시간을 파일에서 다시 읽음. </summary>
        public void Open()
        {
            OpenAt(Step.Verify);
            LoadSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 비밀번호 변경용으로 창을 엶 — 새 비밀번호를 두 번 입력받음(관리자 화면 위에 뜸). </summary>
        public void OpenForChange()
        {
            // 관리자 레벨 이동에서 돌아와 비밀번호 확인 없이 열린 경우도 있으므로 자동 닫기 시간을 다시 읽음
            OpenAt(Step.EnterNew);
            LoadSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 입력을 비우고 창을 닫음. </summary>
        public void Close()
        {
            _input.Clear();
            _newPassword = null;
            gameObject.SetActive(false);
        }

        /// <summary> 입력을 비운 채 주어진 단계로 창을 엶. </summary>
        private void OpenAt(Step step)
        {
            _newPassword = null;
            ShowStep(step);
            _idleTimer.Restart();
            gameObject.SetActive(true);
        }

        /// <summary> 마지막 입력 뒤 제한 시간이 지나면 창을 닫음(창이 열려 있을 때만 실행됨). </summary>
        private void Update()
        {
            if (!_idleTimer.HasExpired(_idleCloseSeconds)) return;

            if (_logger != null) _logger.ZLogInformation($"[AdminPasswordPanel] {_idleCloseSeconds}초 동안 입력이 없어 비밀번호 창을 닫음.");
            Close();
        }

        /// <summary> Admin.json에서 비밀번호와 자동 닫기 시간을 읽음. 파일이 없거나 키패드로 입력할 수 없는 비밀번호면 기본 비밀번호를 씀. </summary>
        private async UniTaskVoid LoadSettingsAsync(CancellationToken token)
        {
            try
            {
                AdminSettings settings = await AdminSettings.LoadAsync(token, _logger);
                _idleCloseSeconds = settings.passwordIdleCloseSeconds;

                if (PasswordInput.IsValidPassword(settings.password))
                {
                    _password = settings.password;
                    return;
                }

                if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] Admin.json의 비밀번호가 숫자 {Constants.Admin.PasswordMinLength}~{Constants.Admin.PasswordMaxLength}자리가 아니어서 기본 비밀번호를 씀.");
                _password = Constants.Admin.DefaultPassword;
            }
            catch (OperationCanceledException)
            {
                // 읽는 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary> 숫자 한 자리를 입력함. 최대 자릿수를 넘는 입력은 무시함. </summary>
        private void OnDigitClicked(int digit)
        {
            PlayKeySound();
            if (!_input.TryAppend(digit)) return;

            SetMessage(string.Empty);
            RefreshMasked();
        }

        /// <summary> 마지막 한 자리를 지움. </summary>
        private void OnBackspaceClicked()
        {
            PlayKeySound();
            _input.RemoveLast();
            RefreshMasked();
        }

        /// <summary> 자릿수를 확인한 뒤 지금 단계에 맞게 처리함 — 진입 확인, 새 비밀번호 받기, 한 번 더 입력한 값 비교. </summary>
        private void OnConfirmClicked()
        {
            PlayKeySound();

            if (!_input.HasValidLength)
            {
                SetMessage(Constants.Admin.PasswordLength);
                return;
            }

            switch (_step)
            {
                case Step.Verify:
                    ConfirmVerify();
                    break;

                case Step.EnterNew:
                    _newPassword = _input.ToString();
                    ShowStep(Step.ConfirmNew);
                    break;

                case Step.ConfirmNew:
                    ConfirmNewPassword();
                    break;
            }
        }

        /// <summary> 비밀번호가 맞으면 관리자 화면을 열고, 틀리면 안내를 띄우고 입력을 지움. </summary>
        private void ConfirmVerify()
        {
            if (!_input.Matches(_password))
            {
                if (_logger != null) _logger.ZLogInformation($"[AdminPasswordPanel] 관리자 비밀번호가 틀림.");
                _input.Clear();
                RefreshMasked();
                SetMessage(Constants.Admin.WrongPassword);
                return;
            }

            Close();
            if (adminPanel) adminPanel.Open();
            else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] adminPanel이 null이라 관리자 화면을 열 수 없음.");
        }

        /// <summary> 한 번 더 입력한 값이 새 비밀번호와 같으면 창을 닫고 저장함. 다르면 새 비밀번호부터 다시 받음. </summary>
        private void ConfirmNewPassword()
        {
            if (!_input.Matches(_newPassword))
            {
                ShowStep(Step.EnterNew);
                SetMessage(Constants.Admin.PasswordMismatch);
                return;
            }

            string newPassword = _newPassword;
            Close();
            SavePasswordAsync(newPassword, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// 새 비밀번호를 Admin.json에 저장하고 결과를 관리자 화면에 알림.
        /// 저장 성공 여부는 JsonLoader.SaveAsync의 반환값으로 판단함(임시 파일에 쓴 뒤 바꾸므로 실패하면 기존 파일이 그대로 남음).
        /// </summary>
        private async UniTaskVoid SavePasswordAsync(string newPassword, CancellationToken token)
        {
            try
            {
                // 같은 파일의 다른 값(자동 닫기 시간·진입 클릭 수)을 지키도록 파일을 읽어 비밀번호만 바꿔 저장함.
                // 파일이 깨져 있으면 기본값으로 읽혀 그 값들이 사라지므로 저장하지 않음
                if (!AdminSettings.TryReadForSave(out AdminSettings current, _logger))
                {
                    if (_logger != null) _logger.ZLogError($"[AdminPasswordPanel] Admin.json을 읽을 수 없거나 형식이 올바르지 않아 새 비밀번호를 저장하지 않음.");
                    if (adminPanel) adminPanel.ShowStatus(Constants.Admin.PasswordSaveFailed);
                    return;
                }

                current.password = newPassword;
                bool isSaved = await JsonLoader.SaveAsync(AdminSettings.FilePath, current, token, _logger);

                // 파일 입출력 뒤 관리자 화면 UI를 고치므로 메인 스레드로 돌아옴
                await UniTask.SwitchToMainThread(token);

                if (isSaved) _password = newPassword; // 다시 열 때 파일을 읽기 전에 입력을 마쳐도 새 비밀번호로 확인함

                if (_logger != null)
                {
                    if (isSaved) _logger.ZLogInformation($"[AdminPasswordPanel] 관리자 비밀번호를 변경함.");
                    else _logger.ZLogError($"[AdminPasswordPanel] 새 비밀번호를 Admin.json에 저장하지 못함.");
                }

                if (adminPanel) adminPanel.ShowStatus(isSaved ? Constants.Admin.PasswordChanged : Constants.Admin.PasswordSaveFailed);
                else if (_logger != null) _logger.ZLogWarning($"[AdminPasswordPanel] adminPanel이 null이라 비밀번호 저장 결과를 표시할 수 없음.");
            }
            catch (OperationCanceledException)
            {
                // 저장 도중 씬 전환 등으로 오브젝트가 파괴된 경우 — 정상 종료
            }
        }

        /// <summary> 클릭음을 내고 입력을 비운 채 창을 닫음. </summary>
        private void OnCloseClicked()
        {
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
            Close();
        }

        /// <summary> 키 입력 클릭음을 냄(무입력 시간은 IdleCloseTimer가 화면 누르기로 다시 잼). </summary>
        private void PlayKeySound()
        {
            SoundEffects.Play(_soundManager, Constants.Sounds.ButtonClick, _logger);
        }

        /// <summary> 0자리부터 최대 자릿수까지 ●를 이어 붙인 표시 문자열을 만듦. </summary>
        private static string[] CreateMaskTexts()
        {
            string[] texts = new string[Constants.Admin.PasswordMaxLength + 1];
            for (int i = 0; i < texts.Length; i++)
                texts[i] = new string('●', i);
            return texts;
        }

        /// <summary> 입력을 비우고 단계에 맞는 안내 문구를 띄움. </summary>
        private void ShowStep(Step step)
        {
            _step = step;
            _input.Clear();
            RefreshMasked();
            SetMessage(string.Empty);

            if (!promptText) return; // Awake에서 경고함

            promptText.text = step switch
            {
                Step.EnterNew => Constants.Admin.PromptNew,
                Step.ConfirmNew => Constants.Admin.PromptConfirm,
                _ => Constants.Admin.PromptVerify
            };
        }

        /// <summary> 입력한 자릿수만큼 ●를 표시함(maskedText 누락은 Awake에서 경고함). </summary>
        private void RefreshMasked()
        {
            if (maskedText) maskedText.text = MaskTexts[_input.Length];
        }

        /// <summary> 안내 문구를 표시함(빈 문자열이면 지움, messageText 누락은 Awake에서 경고함). </summary>
        private void SetMessage(string message)
        {
            if (messageText) messageText.text = message;
        }
    }
}
