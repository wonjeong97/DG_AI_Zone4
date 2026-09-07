using System;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/1_Intro.json 매핑 — 1_Intro 씬(IntroFlowController)의 연출 타이밍을 재빌드 없이 조정.
    /// </summary>
    [Serializable]
    public class IntroSceneSettings
    {
        /// <summary> 인트로 패널에서 튜토리얼 패널로 넘어갈 때 크로스페이드에 걸리는 시간(초). </summary>
        public float crossFadeDuration = 0.4f;

        /// <summary> 씬 진입 후 스토리 텍스트 등장 연출이 시작되기 전 대기 시간(초). </summary>
        public float storyTextStartDelay = 0f;
    }
}
