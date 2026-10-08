using Cysharp.Threading.Tasks;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine.SceneManagement;
using HuliacDev.Network;
using ZLogger;

namespace Network
{
    /// <summary>
    /// Zone4 API 매니저. 시작/종료/idle 로그를 서버에 전송함.
    /// 타이틀(0_Title)은 이미 대기 화면이라 비활동 타임아웃이 나도 로그를 보내지 않음
    /// — QR로 확인한 체험자가 시작하기를 누르지 않아 QR 대기로 돌아갈 때는 TitleFlowController가 move_idle_timeout을 직접 보냄.
    /// 마지막 씬(5_Outro)에서 타임아웃으로 타이틀로 복귀할 때는
    /// 관람을 마친 사용자의 정상 이탈로 간주하여 move_idle_timeout 대신 move_idle을 전송함.
    /// </summary>
    public class APIManager : ApiManagerBase
    {
        /// <summary>
        /// 비활동 타임아웃 발생 시 씬별 분기 처리 (HuliacDev.Template ApiManagerBase.OnInactivityTimeout 오버라이드).
        /// 타이틀(0_Title)은 아무도 체험하지 않는 대기 화면이므로 보내지 않고,
        /// 마지막 씬(5_Outro)은 체험 완료 후 자리를 뜬 정상 이탈이므로 move_idle,
        /// 그 외 씬은 체험 중도 이탈이므로 기본 동작(move_idle_timeout)을 전송함.
        /// </summary>
        protected override void OnInactivityTimeout()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == Constants.Scenes.Title)
            {
                if (Logger != null)
                {
                    Logger.ZLogInformation($"[APIManager] {Constants.Scenes.Title}은 이미 대기 화면이라 비활동 타임아웃 로그를 보내지 않음.");
                }
            }
            else if (currentScene == Constants.Scenes.Outro)
            {
                if (Logger != null)
                {
                    Logger.ZLogInformation($"[APIManager] {Constants.Scenes.Outro}에서 비활동 타임아웃이 발생해 move_idle_timeout 대신 move_idle을 전송함.");
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