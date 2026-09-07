using System;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/2_LevelSelect.json 매핑 — 2_LevelSelect 씬(LevelSelectFlowController)의 연출 타이밍을 재빌드 없이 조정.
    /// </summary>
    [Serializable]
    public class LevelSelectSceneSettings
    {
        /// <summary> 앞에서부터 잠금 해제된 상태로 시작하는 레벨 수. </summary>
        public int unlockedLevelCount = 1;

        /// <summary> 레벨 선택 패널 <-> 스토리 패널 전환에 걸리는 페이드 시간(초). </summary>
        public float panelFadeDuration = 0.4f;
    }
}
