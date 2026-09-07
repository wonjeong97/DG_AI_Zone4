using Cysharp.Threading.Tasks;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine.SceneManagement;
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
        /// <summary>
        /// 비활동 타임아웃 발생 시 씬별 분기 처리 (Wonjeong.Template ApiManagerBase.OnInactivityTimeout 오버라이드).
        /// 마지막 씬(5_Outro)은 체험 완료 후 자리를 뜬 정상 이탈이므로 move_idle,
        /// 그 외 씬은 체험 중도 이탈이므로 기본 동작(move_idle_timeout)을 전송함.
        /// </summary>
        protected override void OnInactivityTimeout()
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
                base.OnInactivityTimeout();
            }
        }
    }
}