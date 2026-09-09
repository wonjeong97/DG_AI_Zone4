using UnityEngine;

namespace DGAIZone.App
{
    /// <summary>
    /// 현재 세션에서 잠금 해제된 레벨 수를 씬 전환 너머로 보관하는 루트 스코프 서비스.
    /// 2_LevelSelect 진입 시 난이도 프리셋(JSON)과 이 값 중 더 큰 쪽을 적용하고(진행도가 줄어들지 않도록),
    /// 미션을 완료할 때마다(4_Result) UnlockThrough로 다음 레벨까지 확장됨. 기본값은 1.
    /// 0_Title 진입 시 Reset()으로 초기화되어, 이전 체험자의 진행도가 다음 체험자에게 넘어가지 않도록 함.
    /// </summary>
    public class UnlockedLevelStore
    {
        private const int MaxLevel = 4; // 실제로 콘텐츠가 있는 최대 레벨. 레벨이 늘어나면 이 값만 올리면 됨.

        public int UnlockedLevelCount { get; set; } = 1;

        /// <summary> 레벨 completedLevel(1부터)을 완료했을 때, 다음 레벨까지 잠금 해제되도록 갱신함(MaxLevel을 넘지 않음). 이미 더 넓게 열려있으면 그대로 유지함. </summary>
        public void UnlockThrough(int completedLevel)
        {
            int candidate = Mathf.Min(completedLevel + 1, MaxLevel);
            if (candidate > UnlockedLevelCount) UnlockedLevelCount = candidate;
        }

        /// <summary> 진행도를 1레벨만 열린 초기 상태로 되돌림. 새 체험자가 시작할 때(0_Title 진입 시) 호출됨. </summary>
        public void Reset()
        {
            UnlockedLevelCount = 1;
        }
    }
}
