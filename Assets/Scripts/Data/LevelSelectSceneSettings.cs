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

        /// <summary> 레벨 선택 후 스토리 패널로 전환될 때 선택된 레벨 버튼이 좌측 스토리 영역으로 이동하는 시간(초). </summary>
        public float selectedLevelButtonMoveDuration = 1.0f;

        /// <summary> 버튼 이동 시 Ease.OutBack 오버슈트 계수. </summary>
        public float selectedLevelButtonMoveOvershoot = 1.3f;
    }
}
