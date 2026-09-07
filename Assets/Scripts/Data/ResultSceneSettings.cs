using System;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/4_Result.json 매핑 — 4_Result 씬(ResultFlowController)의 연출 타이밍을 재빌드 없이 조정.
    /// </summary>
    [Serializable]
    public class ResultSceneSettings
    {
        /// <summary> 결과 패널 <-> 컴플리트 패널 전환에 걸리는 페이드 시간(초). </summary>
        public float panelFadeDuration = 0.4f;
    }
}
