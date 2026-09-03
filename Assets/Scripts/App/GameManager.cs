using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using Microsoft.Extensions.Logging;
using UnityEngine.SceneManagement;
using VContainer;
using Wonjeong.App;
using Wonjeong.Core;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// 게임 전역 매니저. 템플릿 GameManagerBase의 싱글톤/DontDestroyOnLoad, 입력 토글, 설정 비동기 로드를 그대로 사용함.
    /// 일정 시간 입력이 없으면(InactivityTimer가 발행하는 InactivityTimeoutEvent) 타이틀 씬으로 되돌아가도록 연결함.
    /// </summary>
    public class GameManager : GameManagerBase<GameManager>
    {
        private ISubscriber<InactivityTimeoutEvent> _inactivityTimeoutSubscriber;
        private SceneTransitionService _sceneTransition;
        private ILogger<GameManager> _logger;
        private IDisposable _subscription;

        /// <summary> VContainer 의존성 주입. 비활동 타임아웃 구독자와 씬 전환 서비스, 로거를 할당함(GameManagerBase 자체 주입과 별도). </summary>
        [Inject]
        public void Construct(ISubscriber<InactivityTimeoutEvent> inactivityTimeoutSubscriber, SceneTransitionService sceneTransition, ILogger<GameManager> logger)
        {
            _inactivityTimeoutSubscriber = inactivityTimeoutSubscriber;
            _sceneTransition = sceneTransition;
            _logger = logger;
        }

        /// <summary> 베이스 초기화 후 비활동 타임아웃 이벤트를 구독함. </summary>
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
        }

        /// <summary> 오브젝트 파괴 시 비활동 타임아웃 구독을 해제함. </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            _subscription?.Dispose();
        }

        /// <summary>
        /// 일정 시간 입력이 없으면 화면 페이드와 함께 타이틀 씬으로 되돌아감.
        /// 이미 타이틀 씬이면(첫 화면이라 되돌아갈 곳이 없음) 아무 동작도 하지 않음.
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
