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
            public const string Intro = "1_Intro";
            public const string LevelSelect = "2_LevelSelect";
            public const string Game = "3_Game";
            public const string Result = "4_Result";
            public const string Outro = "5_Outro";
        }

        /// <summary> StreamingAssets 파일 이름. </summary>
        public static class Files
        {
            public const string RfidMappings = "RfidMappings.json";
            public const string Visitor = "Visitor.json";
        }

        /// <summary> 리소스 경로 및 Addressables 주소/라벨 상수. </summary>
        public static class ResourcePaths
        {
            /// <summary> 씬별 연출 타이밍 JSON이 모여 있는 StreamingAssets 하위 폴더. </summary>
            public const string SceneSettingsFolder = "Json";

            /// <summary> 특정 씬이 아닌 공통 연출 타이밍(씬 전환 페이드 등)을 담는 JSON 파일명. </summary>
            public const string CommonSettingsFileName = "00_Common";

            /// <summary>
            /// Addressables로 관리하는 TMP 폰트(SDF Font Asset)에 붙은 라벨. Assets/AddressableAssets/Fonts 하위 폰트들이
            /// 이 라벨을 가지며, GameLifetimeScope가 부팅 시 이 라벨로 전부 불러와 MaterialReferenceManager에 등록해
            /// TMP의 &lt;font="..."&gt; 태그가 해석되도록 함.
            /// </summary>
            public const string TmpFontLabel = "TMPFont";
        }

        /// <summary>
        /// 스토리 텍스트가 한 줄씩 올라오는 연출 상수(타이틀/레벨 선택/아웃트로 공용).
        /// 실제 값은 00_Common.json(Data.CommonSettings)에서 재빌드 없이 조정 가능하며, 여기 값은 로드 전/실패 시 폴백으로만 쓰임.
        /// </summary>
        public static class StoryLine
        {
            /// <summary> 스토리 텍스트 각 줄이 올라오는 이동/페이드 연출 시간 (초, 크면 천천히 올라옴) </summary>
            public const float StoryLineMoveDuration = 0.7f;

            /// <summary> 다음 줄 연출 시작 전 대기 간격 (초) </summary>
            public const float StoryLineInterval = 0.35f;

            /// <summary> 한 줄 올라올 때 시작 Y 오프셋 거리 (픽셀) </summary>
            public const float StoryLineYOffset = 22.0f;
        }

        /// <summary> 목적지별 미션(추진력 조건) 상수. MissionBoardController가 참조함. </summary>
        public static class Mission
        {
            /// <summary> 목적지 하나에 대한 추진력 조건 정의. </summary>
            public readonly struct Definition
            {
                /// <summary> 화면 표시용 순수 행성 이름(예: "화성"). Text_GoalPlanetName에 그대로 표시됨. </summary>
                public readonly string PlanetName;

                /// <summary> 미션 보드 안내 문구용 표기(예: "화성 (거리 10)"). </summary>
                public readonly string Destination;

                /// <summary> 목표 거리. 추진력이 이 값에 도달/초과하면 Image_Fill이 100%(1.0)가 되고 미션이 성공함. </summary>
                public readonly int TargetDistance;

                /// <summary> Addressables에서 목적지 이미지를 불러올 때 쓰는 주소(PlanetName과 공백 등 표기가 다를 수 있음). </summary>
                public readonly string SpriteKey;

                public Definition(string planetName, int targetDistance, string spriteKey)
                {
                    PlanetName = planetName;
                    Destination = $"{planetName} (거리 {targetDistance})";
                    TargetDistance = targetDistance;
                    SpriteKey = spriteKey;
                }
            }

            /// <summary>
            /// 목적지별 목표 거리.
            /// 추진력 = 엔진 출력량(RfidMappings.json "추진체 종류": 고체 로켓 5 / 액체 로켓 7 / 핵 추진 엔진 10)
            ///        x 연료량("연료량": 0~10)
            ///        - 탑재 중량("탑재 종류": 인공위성 3 / 탐사 로봇 2 / 우주왕복선 5)
            /// Image_Fill의 fillAmount는 (추진력 / TargetDistance)를 0~1로 clamp한 값이며(MissionBoardController.CalculateFillAmount),
            /// 추진력이 TargetDistance 이상이면 미션 성공으로 판정함(MissionBoardController.IsThrustValid).
            /// </summary>
            public static readonly Definition[] Definitions =
            {
                new Definition("달", 5, "Moon"),
                new Definition("화성", 10, "Mars"),
                new Definition("외계 행성", 20, "ExoPlanet"),
            };
        }
    }
}
