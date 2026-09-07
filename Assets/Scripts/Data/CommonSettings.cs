using System;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/00_Common.json 매핑 — 특정 씬에 속하지 않고 여러 씬에서 공유하는 연출 타이밍.
    /// </summary>
    [Serializable]
    public class CommonSettings
    {
        /// <summary> 씬 전환 시 화면이 어두워졌다 밝아지는 데 걸리는 시간(초). </summary>
        public float sceneTransitionFadeDuration = 0.5f;

        /// <summary> 같은 씬 안에서 패널이 페이드/크로스페이드되는 데 걸리는 시간(초). </summary>
        public float panelFadeDuration = 0.4f;

        /// <summary> 스토리 텍스트 각 줄이 아래에서 위로 올라오는 이동/페이드 연출 시간(초). </summary>
        public float storyLineMoveDuration = 0.7f;

        /// <summary> 스토리 텍스트 다음 줄 연출 시작 전 대기 간격(초). </summary>
        public float storyLineInterval = 0.35f;

        /// <summary> 스토리 텍스트 한 줄이 올라올 때 시작 Y 오프셋 거리(픽셀). </summary>
        public float storyLineYOffset = 22.0f;
    }
}
