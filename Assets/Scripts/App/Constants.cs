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

        /// <summary> 목적지별 미션(연료량 조건) 상수. MissionBoardController가 참조함. </summary>
        public static class Mission
        {
            /// <summary> 연료량 입력이 가질 수 있는 값의 범위(RfidMappings.json "연료량" matterNames: "0".."10"). </summary>
            public const int FuelDomainMin = 0;
            public const int FuelDomainMax = 10;

            /// <summary> 목적지 하나에 대한 연료량 조건 정의. </summary>
            public readonly struct Definition
            {
                public readonly string Destination;
                public readonly string FuelRequirement;
                public readonly int MinFuel;
                public readonly int MaxFuel;

                /// <summary> Addressables에서 목적지 이미지를 불러올 때 쓰는 주소(Destination과 공백 등 표기가 다를 수 있음). </summary>
                public readonly string SpriteKey;

                public Definition(string destination, string fuelRequirement, int minFuel, int maxFuel, string spriteKey)
                {
                    Destination = destination;
                    FuelRequirement = fuelRequirement;
                    MinFuel = minFuel;
                    MaxFuel = maxFuel;
                    SpriteKey = spriteKey;
                }
            }

            /// <summary> 목적지별 연료량 조건(포함 범위). </summary>
            public static readonly Definition[] Definitions =
            {
                new Definition("달", "3보다 적은 연료량", 0, 2, "Moon"),
                new Definition("화성", "4에서 7 사이의 연료량", 4, 7, "Mars"),
                new Definition("외계 행성", "8에서 10 사이의 연료량", 8, 10, "ExoPlanet"),
            };
        }
    }
}
