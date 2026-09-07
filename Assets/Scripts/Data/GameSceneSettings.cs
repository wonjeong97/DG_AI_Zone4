using System;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/3_Game.json 매핑 — 3_Game 씬의 연출 타이밍과 조작 감도.
    /// CodingCategoryIndicatorController, IngredientSelectionController, MissionBoardController가 함께 참조함.
    /// (GameFlowController의 debugStartLevel은 에디터 테스트 전용이라 JSON으로 분리하지 않고 인스펙터 값만 사용함)
    /// </summary>
    [Serializable]
    public class GameSceneSettings
    {
        /// <summary> 카드를 이 시간(초) 이상 올려놓지 않으면 다음 카테고리 힌트 페이드가 시작됨. </summary>
        public float idleHintDelay = 10.0f;

        /// <summary> 카테고리 힌트의 색상 <-> 흑백 한쪽 방향 전환에 걸리는 시간(초). </summary>
        public float hintFadeDuration = 0.9f;

        /// <summary> Text_Matter/DesignItem 값이 숫자일 때 강조용 폰트 크기. </summary>
        public float numberFontSize = 45f;

        /// <summary> Image_RightArrow가 0->1로 차오르는 데 걸리는 시간(초). </summary>
        public float rightArrowFillDuration = 1.0f;

        /// <summary> Image_RightArrow가 다 차오른 뒤 페이드아웃되는 데 걸리는 시간(초). </summary>
        public float rightArrowFadeDuration = 0.5f;

        /// <summary> 레벨 2 진행바(Image_Fill)가 목표 값까지 채워지는 데 걸리는 시간(초). </summary>
        public float level2FillTweenDuration = 0.45f;

        /// <summary> 레벨 2 진행바 Ease.OutBack 오버슈트 크기(기본값 1.70158보다 작게 두어 과하게 튀지 않도록 함). </summary>
        public float level2FillOvershoot = 1.2f;

        /// <summary> 레벨 3 산소/전기 게이지가 목표 값까지 채워지는 데 걸리는 시간(초). </summary>
        public float level3GaugeTweenDuration = 0.4f;

        /// <summary> 레벨 3 "또는" 선택 시 불안정하게 깜빡이는 아이콘의 최소 알파. </summary>
        public float level3IconBlinkMinAlpha = 0.25f;

        /// <summary> 레벨 3 아이콘 깜빡임 한쪽 방향 소요 시간(초, 짧을수록 더 불안정해 보임). </summary>
        public float level3IconBlinkDuration = 0.12f;

        /// <summary> 미션 보드 진행도(Image_Fill)가 목표 값까지 채워지는 데 걸리는 시간(초). </summary>
        public float fillTweenDuration = 0.5f;

        /// <summary> 미리보기 게이지(Image_Fill_Preview) 깜빡임 한쪽 방향 전환에 걸리는 시간(초). </summary>
        public float previewBlinkFadeDuration = 0.8f;

        /// <summary> 미리보기 게이지 깜빡임 최소 알파. </summary>
        public float previewBlinkMinAlpha = 0.5f;

        /// <summary> 설정하기 확정 시 미리보기 게이지가 페이드아웃되는 데 걸리는 시간(초). </summary>
        public float previewApplyFadeDuration = 0.3f;
    }
}
