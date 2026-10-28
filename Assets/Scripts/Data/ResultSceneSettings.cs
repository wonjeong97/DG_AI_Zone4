using System;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/4_Result.json 매핑 — 4_Result 씬(ResultFlowController)의 연출 타이밍을 재빌드 없이 조정.
    /// </summary>
    [Serializable]
    public class ResultSceneSettings
    {
        /// <summary> 패널(AI 코딩 안내, AI 패널, 설계창 ↔ 영상, 컴플리트 패널) 페이드에 걸리는 시간(초). </summary>
        public float panelFadeDuration = 0.4f;

        /// <summary> 'AI가 코딩 중입니다...' 안내를 띄워 두는 시간(초, 페이드 제외). </summary>
        public float aiCodingHoldDuration = 3f;

        /// <summary> 'AI가 코딩 중입니다' 뒤 점(0~3개)이 바뀌는 간격(ms). </summary>
        public int aiCodingDotIntervalMs = 400;

        /// <summary> AI 패널에서 정답 설계가 다 쌓인 뒤 설계창을 보여 주는 시간(초, 페이드·블록 쌓기 제외). 이후 성공 영상으로 넘어감. </summary>
        public float aiDesignHoldDuration = 4f;

        /// <summary> 결과 설계창(나의 코딩 결과·AI 패널)에서 블록이 하나씩 붙는 간격(초). 블록 하나가 다 붙는 시간(설계창 인스펙터의 올라오기 + 값 붙기)보다 길면 하나씩 차례로 붙음. </summary>
        public float designBlockInterval = 1f;

        /// <summary> 레벨 1 성공 영상에서 로켓 조립이 끝나 '엔진·탑승 물체·연료량' 글자(고른 블록 값)를 띄우는 영상 시각(초). 이후 결과 화면이 끝날 때까지(마지막 화면에 로켓이 남음) 둠. </summary>
        public float level1RocketLabelSuccessShowTime = 9.5f;

        /// <summary> 레벨 1 실패 영상에서 로켓이 부서지기 시작하기 전에 '엔진·탑승 물체·연료량' 글자(고른 블록 값)를 지우는 영상 시각(초). 영상 처음부터 이 시각까지 보임. </summary>
        public float level1RocketLabelFailHideTime = 3.6f;

        /// <summary> 레벨 1 로켓 글자가 나타나고 사라지는 시간(초). </summary>
        public float level1RocketLabelFadeDuration = 0.3f;
    }
}
