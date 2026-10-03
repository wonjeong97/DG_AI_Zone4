using System;
using System.Collections.Generic;
using DGAIZone.Game.UI;

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
        /// 레벨 3처럼 실패 원인별 결과 영상이 있을 때 실패 영상 파일명 끝에 붙일 접미사("4-3-Fail-{접미사}.mp4"). null이면 기본 실패 영상.
        /// 3_Game이 코딩완료 시 기록함.
        /// </summary>
        public string FailVideoSuffix { get; set; }

        /// <summary> 플레이어가 설계창에 쌓은 블록(3_Game 설계창과 같은 모양·문구). 3_Game이 코딩완료·스킵 시 기록하고 4_Result의 '나의 코딩 결과' 패널이 보여줌. </summary>
        public IReadOnlyList<DesignStep> PlayerDesign { get; set; } = Array.Empty<DesignStep>();

        /// <summary> 플레이어가 코딩완료로 설계를 마쳤는지 여부(스킵이면 false). '나의 코딩 결과'에 완성하기 블록을 붙일지 정함. </summary>
        public bool PlayerDesignCompleted { get; set; }

        /// <summary> 3_Game 설계창의 배치 방식(줄여서 한 화면에/크게 두고 자동 스크롤). 4_Result의 두 설계창이 같은 방식으로 그림. </summary>
        public DesignLayoutMode DesignLayoutMode { get; set; } = DesignLayoutMode.FitAll;

        /// <summary> 이번 판 문제의 정답 설계(설계창 블록). 3_Game이 코딩완료·스킵 시 기록하고 4_Result의 AI 패널이 "AI가 푼 설계"로 보여줌. </summary>
        public IReadOnlyList<DesignStep> SolutionDesign { get; set; } = Array.Empty<DesignStep>();
    }
}
