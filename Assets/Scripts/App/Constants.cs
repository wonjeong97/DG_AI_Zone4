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

        /// <summary> 타이틀 씬 하단 안내 문구. </summary>
        public static class TitleMessages
        {
            public const string QrGuide = "QR 코드를 인식하여 주세요.";
            public const string StartGuide = "시작하기를 눌러주세요.";
        }

        /// <summary> 스토리 텍스트 등에 들어가는 체험자 이름 플레이스홀더. </summary>
        public const string VisitorPlaceholder = "{name}";

        /// <summary> 체험자 이름을 알 수 없을 때(Visitor.json 로드 전·실패, 서버 미연동) {name} 대신 쓰는 기본 이름. </summary>
        public const string DefaultVisitorName = "체험자";

        /// <summary> 미션 텍스트 내 동적 치환용 플레이스홀더. </summary>
        public static class MissionPlaceholders
        {
            public const string Planet = "{planet}";
            public const string Distance = "{distance}";
            public const string MaxElectricity = "{maxElectricity}";
            public const string MinOxygen = "{minOxygen}";

            // 숫자 자리표시자 이름 뒤에 붙이는 조사 접미사. {distance|이에요}, {maxElectricity|이에요}처럼 쓰면 그 숫자를 읽었을 때
            // 받침 유무에 따라 "이에요"(10이에요) 또는 "예요"(5예요)로 치환됨
            public const string CopulaSuffix = "|이에요";
        }

        /// <summary> StreamingAssets 파일 이름. </summary>
        public static class Files
        {
            public const string RfidMappings = "RfidMappings.json";
            public const string Visitor = "Visitor.json";

            /// <summary> 인트로/레벨 선택/게임/아웃트로에서 반복 재생하는 로봇 영상 파일명(Videos 폴더 안). </summary>
            public const string RobotVideo = "robot_0811.webm";

            /// <summary> 결과 영상 파일명 앞자리("{접두어}-{레벨}-{Success|Fail}.mp4"). </summary>
            public const string ResultVideoPrefix = "4";
        }

        /// <summary> 리소스 경로 및 Addressables 주소/라벨 상수. </summary>
        public static class ResourcePaths
        {
            /// <summary> 씬별 연출 타이밍 JSON이 모여 있는 StreamingAssets 하위 폴더. </summary>
            public const string SceneSettingsFolder = "Json";

            /// <summary> 영상 파일이 모여 있는 StreamingAssets 하위 폴더. </summary>
            public const string VideosFolder = "Videos";

            /// <summary> 특정 씬이 아닌 공통 연출 타이밍(씬 전환 페이드 등)을 담는 JSON 파일명. </summary>
            public const string CommonSettingsFileName = "00_Common";

            /// <summary>
            /// Addressables로 관리하는 TMP 폰트(SDF Font Asset)에 붙은 라벨. Assets/AddressableAssets/Fonts 하위 폰트들이
            /// 이 라벨을 가지며, GameLifetimeScope가 부팅 시 이 라벨로 전부 불러와 MaterialReferenceManager에 등록해
            /// TMP의 &lt;font="..."&gt; 태그가 해석되도록 함.
            /// </summary>
            public const string TmpFontLabel = "TMPFont";
        }

        /// <summary> RFID 카드 분류(RfidMappings.json의 category 값과 일치해야 함). </summary>
        public static class RfidCategories
        {
            public const string Action = "동작";
            public const string Control = "제어";
            public const string Logic = "논리";
            public const string Func = "함수";
        }

        /// <summary>
        /// RfidMappings.json의 재료(ingredientId)·물질(matters[].id) 식별자. 코드는 화면 이름(label)이 아니라 이 값으로 판정하므로
        /// JSON의 label은 자유롭게 바꿔도 되지만 id는 여기와 맞아야 함.
        /// </summary>
        public static class RfidIds
        {
            /// <summary> 레벨 1 재료. 추진력 = 엔진 출력량 + 연료량 - 탑재 중량에서 각 값의 역할을 구분함. </summary>
            public static class Level1
            {
                public const string Engine = "Engine";
                public const string Payload = "Payload";
                public const string Fuel = "Fuel";
            }

            /// <summary> 레벨 2 발사 코딩 순서. 이 순서대로 확정해야 성공함. </summary>
            public static class Level2
            {
                public const string Ignite = "Ignite";
                public const string Ascend = "Ascend";
                public const string SeparateStage1 = "SeparateStage1";
                public const string SeparateStage2 = "SeparateStage2";
                public const string EnterOrbit = "EnterOrbit";
            }

            /// <summary> 레벨 3 동작·논리 블록. 조건 블록(만약 전기량이/산소량이)은 id 대신 value를 기준값과 비교함. </summary>
            public static class Level3
            {
                public const string Raise = "Raise";
                public const string Lower = "Lower";
                public const string Or = "Or";
            }

            /// <summary> 레벨 4 재료(이동하기/반복하기)와 이동 방향. IngredientSelectionController가 확정하고 Level4BoardController가 해석함. </summary>
            public static class Level4
            {
                public const string Move = "Move";
                public const string Repeat = "Repeat";
                public const string MoveUp = "MoveUp";
                public const string MoveDown = "MoveDown";
                public const string MoveRight = "MoveRight";
                public const string MoveLeft = "MoveLeft";
            }
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
    }
}
