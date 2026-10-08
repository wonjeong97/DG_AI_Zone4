using Microsoft.Extensions.Logging;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// 플레이어가 레벨 선택 씬에서 고른 레벨 번호를 씬 전환 너머로 보관하는 루트 스코프 서비스.
    /// 2_LevelSelect에서 기록하고 3_Game 등에서 읽어 레벨별 리소스를 결정함. 기본값은 1.
    /// </summary>
    public class SelectedLevelStore
    {
        /// <summary> 저장소가 주입되지 않았을 때 대신 쓰는 레벨. </summary>
        public const int FallbackLevel = 1;

        public int SelectedLevel { get; set; } = FallbackLevel;

        /// <summary>
        /// store가 기록한 레벨을 반환함. store가 null이면(주입 누락) FallbackLevel을 쓰고, 그 사실을 owner 태그로 경고 로그에 남김
        /// (로거도 없으면 남길 곳이 없어 그대로 대체함 — 테스트처럼 둘 다 넣지 않은 경우).
        /// </summary>
        public static int LevelOrFallback(SelectedLevelStore store, ILogger logger, string owner)
        {
            if (store != null) return store.SelectedLevel;

            if (logger != null) logger.ZLogWarning($"[{owner}] selectedLevelStore가 null이라 레벨 {FallbackLevel}로 처리함.");
            return FallbackLevel;
        }
    }
}
