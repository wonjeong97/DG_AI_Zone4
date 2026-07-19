namespace DGAIZone.App
{
    /// <summary> 미션 결과 상태. </summary>
    public enum MissionResult
    {
        Success,
        Fail,
    }

    /// <summary>
    /// 게임 결과(성공/실패)를 씬 전환을 넘어 보관하는 루트 스코프 서비스.
    /// 3_Game에서 결과를 기록하고 4_Result에서 읽어 재생할 영상을 결정함. 기본값은 실패.
    /// </summary>
    public class GameResultStore
    {
        public MissionResult Result { get; set; } = MissionResult.Fail;
    }
}
