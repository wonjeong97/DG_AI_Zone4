namespace DGAIZone.App
{
    /// <summary>
    /// 플레이어가 레벨 선택 씬에서 고른 레벨 번호를 씬 전환 너머로 보관하는 루트 스코프 서비스.
    /// 2_LevelSelect에서 기록하고 3_Game 등에서 읽어 레벨별 리소스를 결정함. 기본값은 1.
    /// </summary>
    public class SelectedLevelStore
    {
        public int SelectedLevel { get; set; } = 1;
    }
}
