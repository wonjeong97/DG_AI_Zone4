namespace DGAIZone.App
{
    /// <summary>
    /// 현재 세션에서 잠금 해제된 레벨 수를 씬 전환 너머로 보관하는 루트 스코프 서비스.
    /// 2_LevelSelect 진입 시 난이도 프리셋(JSON)과 이 값 중 더 큰 쪽을 적용하고(진행도가 줄어들지 않도록),
    /// 미션을 완료할 때마다(4_Result) UnlockThrough로 다음 레벨까지 확장됨. 기본값은 1.
    /// </summary>
    public class UnlockedLevelStore
    {
        public int UnlockedLevelCount { get; set; } = 1;

        /// <summary> 레벨 completedLevel(1부터)을 완료했을 때, 다음 레벨까지 잠금 해제되도록 갱신함. 이미 더 넓게 열려있으면 그대로 유지함. </summary>
        public void UnlockThrough(int completedLevel)
        {
            int candidate = completedLevel + 1;
            if (candidate > UnlockedLevelCount) UnlockedLevelCount = candidate;
        }
    }
}
