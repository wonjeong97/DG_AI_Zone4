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

        /// <summary>
        /// 스토리 연출(나레이션)이 시작된 뒤 글 줄(빈 줄을 뺀 줄바꿈 단위)마다 올라오기 시작할 시각(초). 나레이션에서 그 줄을 읽기 시작하는 시각에 맞춤
        /// (기본값은 Zone_4_Episode_Narration.mp3의 쉬는 구간으로 잰 값). 비우면([]) 줄마다 00_Common.json의 storyLineInterval 간격으로 올라오고,
        /// 글 줄이 더 많으면 나머지 줄은 그 간격으로 올라옴.
        /// </summary>
        public float[] storyLineStartTimes = { 0.5f, 6.7f, 15.0f, 21.3f, 25.4f };
    }
}
