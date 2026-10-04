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

        /// <summary>
        /// 콘텐츠(미션·레벨별 결과 영상)가 있는 마지막 레벨. 레벨이 늘어나면 영상 추가와 함께 이 값만 올리면
        /// 잠금 해제 상한, 결과 후 아웃트로 전환, 결과 영상 레벨 범위가 함께 따라옴.
        /// </summary>
        public const int LastLevel = 5;

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

            /// <summary> 레벨 3 실패 원인별 결과 영상 접미사("{접두어}-3-Fail-{접미사}.mp4"): 전기 게이지만 모자랄 때. </summary>
            public const string ResultVideoFailElectricity = "Electricity";

            /// <summary> 레벨 3 실패 원인별 결과 영상 접미사: 산소 게이지만 모자랄 때. </summary>
            public const string ResultVideoFailO2 = "O2";
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

            /// <summary>
            /// 레벨 3 재료(단계)와 동작·논리 블록. 게이지 효과는 단계 순서가 아니라 재료 id로 정해지며,
            /// 조건 블록(만약 전기량이/산소량이)은 블록 id 대신 value를 기준값과 비교함.
            /// </summary>
            public static class Level3
            {
                public const string ElectricityCondition = "ElectricityCondition";
                public const string Electricity = "Electricity";
                public const string Logic = "Logic";
                public const string OxygenCondition = "OxygenCondition";
                public const string Oxygen = "Oxygen";

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

            /// <summary> 레벨 5 재료(함수 사용·동작·논리)와 판정·정답에 쓰는 블록 id. 카드 분류로 재료가 정해지며 순서는 자유(기획 검토 중, 임시). </summary>
            public static class Level5
            {
                public const string Function = "Function";
                public const string Action = "Action";
                public const string Logic = "Logic";
                public const string And = "And";
            }
        }

        /// <summary> 레벨 4(탐사 로봇) 보드 규칙. 보드 배치(Level4BoardController)와 RfidMappings.json 검증(RfidMappingValidator)이 함께 씀. </summary>
        public static class Level4Board
        {
            /// <summary> 한 판에 놓을 수 있는 카드 수. RfidMappings.json 레벨 4 steps 수와 같아야 함. </summary>
            public const int MaxCards = 5;

            /// <summary>
            /// 보드 배치가 전제로 하는 반복 횟수. 이동하기만으로는 MaxCards장 안에 못 풀고 '반복하기(이 횟수) + 이동하기'를 써야 풀리는 배치만 나오므로,
            /// RfidMappings.json 반복하기 블록에 이 value가 꼭 있어야 함.
            /// </summary>
            public const int RequiredRepeatCount = 3;
        }

        /// <summary>
        /// 레벨 5에서 분류별로 놓을 수 있는 카드 수. 합이 RfidMappings.json 레벨 5 단계 수와 같아야 하며, 다 쓴 분류의 카드는 받지 않음.
        /// 판정(임시)은 모두 놓으면 성공(기획 확정 뒤 수정).
        /// </summary>
        public static class Level5Cards
        {
            public const int Function = 1;
            public const int Action = 3;
            public const int Logic = 1;
            public const int Total = Function + Action + Logic;
        }

        /// <summary> 효과음 키. StreamingAssets/Settings.json의 sounds 키와 같아야 하며, 파일과 키는 1존과 같음. </summary>
        public static class Sounds
        {
            public const string BlockAssembled = "blockAssembled"; // 설정하기로 블록이 설계창에 붙음, 결과 씬 설계창(나의 코딩 결과·AI 패널)에 블록이 붙음
            public const string ButtonClick    = "buttonClick";    // 전용 효과음이 없는 버튼 클릭, 인트로→튜토리얼 터치, 튜토리얼 페이지 넘기기, 스토리 화면 터치
            public const string CodingAlert    = "codingAlert";    // 잘못된 카드 경고
            public const string CodingComplete = "codingComplete"; // 코딩 완료
            public const string GameStart      = "gameStart";      // 타이틀 시작하기 버튼
            public const string HintEpisode    = "hintEpisode";    // 미션 다시 보기 버튼
            public const string MissionFailed  = "missonFailed";   // 결과 완료 화면 '미션 실패!'
            public const string MissionSuccess = "missonSuccess";  // 결과 완료 화면 '미션 완료!'
        }
    }
}
