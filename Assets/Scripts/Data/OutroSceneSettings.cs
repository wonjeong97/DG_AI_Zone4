using System;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/5_Outro.json 매핑 — 5_Outro 씬(OutroFlowController)의 연출 타이밍을 재빌드 없이 조정.
    /// </summary>
    [Serializable]
    public class OutroSceneSettings
    {
        /// <summary> 홈 버튼 클릭 시 타이틀 씬으로 전환되는 화면 페이드 시간(초). </summary>
        public float sceneFadeDuration = 0.5f;
    }
}
