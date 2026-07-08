using Wonjeong.Core;

namespace DGAIZone.App
{
    /// <summary>
    /// 게임 전역 매니저. 템플릿 GameManagerBase의 싱글톤/DontDestroyOnLoad, 입력 토글, 설정 비동기 로드를 그대로 사용함.
    /// 게임 고유 로직이 필요해지면 이 클래스에 추가함.
    /// </summary>
    public class GameManager : GameManagerBase<GameManager>
    {
    }
}
