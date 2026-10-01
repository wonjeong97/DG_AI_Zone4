using System;
using System.Collections.Generic;

namespace DGAIZone.App
{
    /// <summary> 미션 결과 상태. </summary>
    public enum MissionResult
    {
        Success,
        Fail,
    }

    /// <summary>
    /// 게임 결과(성공/실패)를 씬 전환을 넘어 보관하는 루트 스코프 서비스.
    /// 3_Game에서 결과를 기록하고 4_Result에서 읽어 재생할 영상을 결정함. 기본값은 실패.
    /// </summary>
    public class GameResultStore
    {
        public MissionResult Result { get; set; } = MissionResult.Fail;

        /// <summary>
        /// 이번 판 문제의 정답 설계(3_Game 설계창과 같은 형식의 디자인 항목 문구, 리치 텍스트 포함). 3_Game이 코딩완료·스킵 시 기록하고
        /// 4_Result의 AI 패널이 "AI가 푼 설계"로 보여줌.
        /// </summary>
        public IReadOnlyList<string> SolutionDesignItems { get; set; } = Array.Empty<string>();
    }
}
