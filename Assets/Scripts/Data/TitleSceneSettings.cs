using System;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/0_Title.json 매핑 — 0_Title 씬(TitleFlowController)의 연출 타이밍을 재빌드 없이 조정.
    /// </summary>
    [Serializable]
    public class TitleSceneSettings
    {
        /// <summary> 시작 버튼 클릭 시 인트로 씬으로 전환되는 화면 페이드 시간(초). </summary>
        public float sceneFadeDuration = 0.5f;
    }
}
