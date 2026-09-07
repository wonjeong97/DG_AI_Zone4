using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using MessagePipe;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Wonjeong.App;
using Wonjeong.Network;
using ZLogger;

namespace Network
{
    /// <summary>
    /// Zone4 API 매니저. 시작/종료/idle 로그를 서버에 전송함.
    /// 마지막 씬(5_Outro)에서 타임아웃으로 타이틀로 복귀할 때는
    /// 관람을 마친 사용자의 정상 이탈로 간주하여 move_idle_timeout 대신 move_idle을 전송함.
    /// </summary>
    public class APIManager : ApiManagerBase
    {
        private ISubscriber<InactivityTimeoutEvent> _inactivityTimeoutSubscriber;
        private IDisposable _customTimeoutSubscription;

        /// <summary> VContainer 의존성 주입. 비활동 타임아웃 이벤트 구독자를 할당함. </summary>
        [Inject]
        public void ConstructAPIManager(ISubscriber<InactivityTimeoutEvent> inactivityTimeoutSubscriber)
        {
            _inactivityTimeoutSubscriber = inactivityTimeoutSubscriber;
            UpdateTimeoutSubscription();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateTimeoutSubscription();
        }

        protected override void OnDisable()
        {
            _customTimeoutSubscription?.Dispose();
            _customTimeoutSubscription = null;
            base.OnDisable();
        }

        /// <summary>
        /// base.OnEnable()에서 등록된 기본 InactivityTimeout 구독(_inactivityTimeoutSubscription)을 해제하고,
        /// 현재 활성 씬에 따라 move_idle 또는 move_idle_timeout을 분기 전송하는 커스텀 핸들러로 재구독함.
        /// </summary>
        private void UpdateTimeoutSubscription()
        {
            if (_inactivityTimeoutSubscriber == null) return;

            FieldInfo baseSubField = typeof(ApiManagerBase).GetField("_inactivityTimeoutSubscription", BindingFlags.Instance | BindingFlags.NonPublic);
            if (baseSubField?.GetValue(this) is IDisposable baseSub)
            {
                baseSub.Dispose();
                baseSubField.SetValue(this, null);
            }

            _customTimeoutSubscription?.Dispose();
            _customTimeoutSubscription = _inactivityTimeoutSubscriber.Subscribe(_ => HandleInactivityTimeout());
        }

        /// <summary>
        /// 비활동 타임아웃 발생 시 씬별 분기 처리.
        /// 마지막 씬(5_Outro)은 체험 완료 후 자리를 뜬 정상 이탈이므로 move_idle,
        /// 그 외 씬은 체험 중도 이탈이므로 move_idle_timeout을 전송함.
        /// </summary>
        private void HandleInactivityTimeout()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == Constants.Scenes.Outro)
            {
                if (Logger != null)
                {
                    Logger.ZLogInformation($"[APIManager] Inactivity timeout in {Constants.Scenes.Outro}. Sending move_idle instead of move_idle_timeout.");
                }

                SendMoveIdleLogAsync().Forget();
            }
            else
            {
                if (Logger != null)
                {
                    Logger.ZLogInformation($"[APIManager] Inactivity timeout in {currentScene}. Sending move_idle_timeout.");
                }

                SendMoveIdleTimeoutLogAsync().Forget();
            }
        }
    }
}