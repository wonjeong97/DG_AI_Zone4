using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using Microsoft.Extensions.Logging;
using UnityEngine.SceneManagement;
using VContainer;
using HuliacDev.App;
using HuliacDev.Core;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// 게임 전역 매니저. 템플릿 GameManagerBase의 싱글톤/DontDestroyOnLoad, 입력 토글, 설정 비동기 로드를 그대로 사용함.
    /// 일정 시간 입력이 없으면(InactivityTimer가 발행하는 InactivityTimeoutEvent) 타이틀 씬으로 되돌아가도록 연결함.
    /// 타이틀은 이미 대기 화면이라 그 씬에서는 비활동 타이머를 멈춰 두고, 다른 씬에 들어가면 다시 켬(1존 GameManager와 같은 방식).
    /// </summary>
    public class GameManager : GameManagerBase
    {
        private ISubscriber<InactivityTimeoutEvent> _inactivityTimeoutSubscriber;
        private SceneTransitionService _sceneTransition;
        private InactivityTimer _inactivityTimer;
        private ILogger<GameManager> _logger;
        private IDisposable _subscription;

        /// <summary> VContainer 의존성 주입. 비활동 타임아웃 구독자와 씬 전환 서비스, 로거, 비활동 타이머를 할당함(GameManagerBase 자체 주입과 별도). </summary>
        [Inject]
        public void Construct(ISubscriber<InactivityTimeoutEvent> inactivityTimeoutSubscriber, SceneTransitionService sceneTransition, ILogger<GameManager> logger,
            InactivityTimer inactivityTimer = null)
        {
            _inactivityTimeoutSubscriber = inactivityTimeoutSubscriber;
            _sceneTransition = sceneTransition;
            _logger = logger;
            _inactivityTimer = inactivityTimer;
        }

        /// <summary> 씬 로드 이벤트를 구독해 씬마다 비활동 타이머 상태를 맞춤. </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            SceneManager.sceneLoaded += OnGameSceneLoaded;
        }

        /// <summary> 씬 로드 이벤트 구독을 해제함. </summary>
        protected override void OnDisable()
        {
            base.OnDisable();
            SceneManager.sceneLoaded -= OnGameSceneLoaded;
        }

        /// <summary> 베이스 초기화 후 비활동 타임아웃 이벤트를 구독하고 처음 씬의 비활동 타이머 상태를 맞춤. </summary>
        protected override void Start()
        {
            base.Start();

            if (_inactivityTimeoutSubscriber != null)
            {
                _subscription = _inactivityTimeoutSubscriber.Subscribe(_ => OnInactivityTimeout());
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[GameManager] inactivityTimeoutSubscriber가 null이라 비활동 타임아웃 복귀가 비활성화됨.");
            }

            // sceneLoaded는 구독한 뒤의 전환만 알려 주므로, 앱이 켜질 때 이미 로드된 처음 씬은 여기서 맞춤
            UpdateInactivityTimerState(SceneManager.GetActiveScene().name);
        }

        /// <summary> 오브젝트 파괴 시 비활동 타임아웃 구독을 해제함. </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            _subscription?.Dispose();
        }

        /// <summary> 새로 로드된 씬에 맞춰 비활동 타이머를 멈추거나 다시 켬. </summary>
        private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateInactivityTimerState(scene.name);
        }

        /// <summary>
        /// 타이틀(0_Title)에서는 비활동 타이머를 멈추고, 그 외 씬에서는 다시 켬(Resume은 그 시점부터 다시 셈).
        /// 타이틀에서 QR로 확인한 체험자가 시작하기를 누르지 않은 경우는 TitleFlowController가 자기 대기 시간으로 따로 처리함.
        /// </summary>
        private void UpdateInactivityTimerState(string sceneName)
        {
            if (!_inactivityTimer)
            {
                if (_logger != null) _logger.ZLogWarning($"[GameManager] inactivityTimer가 주입되지 않아 {sceneName}의 비활동 타이머 상태를 바꾸지 못함.");
                return;
            }

            if (sceneName == Constants.Scenes.Title) _inactivityTimer.Pause();
            else _inactivityTimer.Resume();
        }

        /// <summary>
        /// 일정 시간 입력이 없으면 화면 페이드와 함께 타이틀 씬으로 되돌아감.
        /// 이미 타이틀 씬이면(타이머를 멈춰 두므로 보통 일어나지 않지만, 첫 화면이라 되돌아갈 곳이 없음) 아무 동작도 하지 않음.
        /// </summary>
        private void OnInactivityTimeout()
        {
            if (_sceneTransition == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[GameManager] sceneTransition이 null이라 비활동 타임아웃 복귀를 건너뜀.");
                return;
            }
            if (SceneManager.GetActiveScene().name == Constants.Scenes.Title) return;

            _sceneTransition.LoadSceneWithFadeAsync(Constants.Scenes.Title).Forget();
        }
    }
}
