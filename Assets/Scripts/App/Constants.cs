namespace DGAIZone.App
{
    /// <summary>
    /// 프로젝트 전역 상수. 씬 이름 등 여러 곳에서 참조하는 값을 한곳에서 관리함.
    /// 씬을 리네임하면 여기 한 줄만 바꾸면 됨.
    /// </summary>
    public static class Constants
    {
        /// <summary> 빌드에 등록된 씬 이름. </summary>
        public static class Scenes
        {
            public const string Title = "0_Title";
            public const string Tutorial = "1_Tutorial";
            public const string LevelSelect = "2_LevelSelect";
            public const string Game = "3_Game";
            public const string Result = "4_Result";
            public const string Outro = "5_Outro";
        }

        /// <summary> StreamingAssets 파일 이름. </summary>
        public static class Files
        {
            public const string RfidMappings = "RfidMappings.json";
        }

        /// <summary> 스토리 텍스트가 한 줄씩 올라오는 연출 상수(타이틀/레벨 선택 공용). </summary>
        public static class StoryLine
        {
            /// <summary> 스토리 텍스트 각 줄이 올라오는 이동/페이드 연출 시간 (초, 크면 천천히 올라옴) </summary>
            public const float StoryLineMoveDuration = 0.7f;

            /// <summary> 다음 줄 연출 시작 전 대기 간격 (초) </summary>
            public const float StoryLineInterval = 0.35f;

            /// <summary> 한 줄 올라올 때 시작 Y 오프셋 거리 (픽셀) </summary>
            public const float StoryLineYOffset = 22.0f;
        }
    }
}
