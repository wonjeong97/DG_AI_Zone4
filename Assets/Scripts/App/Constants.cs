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

        /// <summary> 타이틀 씬 하단 안내 기본 문구. 값은 1존과 같고, 현장에서는 0_Title.json(TitleSceneSettings)의 같은 문구를 고쳐 재빌드 없이 바꿈. </summary>
        public static class TitleMessages
        {
            public const string QrGuide = "QR 코드를 인식하여 주세요.";
            public const string StartGuide = "시작하기를 눌러주세요.";

            /// <summary> 서버 모드에서 QR로 확인한 체험자에게 보이는 시작 안내 — {name}에 체험자 이름(VisitorPlaceholder). </summary>
            public const string StartGuideWithName = "{name}님, 시작하기를 눌러주세요.";

            // 서버 모드에서 QR을 찍은 뒤 체험자 확인 결과 안내 — 확인 중을 빼면 잠시 보여 준 뒤 QrGuide로 돌아감
            public const string QrChecking    = "QR 코드를 확인하고 있습니다.";
            public const string QrCompleted   = "이미 체험을 완료한 QR 코드입니다.";
            public const string QrNotFound    = "등록되지 않은 QR 코드입니다.";
            public const string QrCheckFailed = "QR 코드를 확인할 수 없습니다. 잠시 후 다시 시도해 주세요.";
        }

        /// <summary> 스토리 텍스트 등에 들어가는 체험자 이름 플레이스홀더. </summary>
        public const string VisitorPlaceholder = "{name}";

        /// <summary> 체험자 이름이 비어 있을 때(관리자 화면 이름·서버 이름이 없을 때) {name} 대신 쓰는 기본 이름. </summary>
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

            /// <summary>
            /// 인트로/레벨 선택/게임/아웃트로에서 반복 재생하는 로봇 영상의 기본 경로(StreamingAssets 기준).
            /// 00_Common.json의 robotVideoPath가 비었거나 그 파일이 없을 때 씀.
            /// </summary>
            public const string RobotVideo = "Videos/robot_0811.webm";

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

            /// <summary> 운영 모드·체험자 이름 설정(VisitorSettings SO)의 Addressables 주소. 루트 스코프가 부팅 시 동기로 불러와 등록함. </summary>
            public const string VisitorSettingsKey = "VisitorSettings";
        }

        /// <summary> 관리자 화면(타이틀 왼쪽 위 연속 터치 → 비밀번호 키패드). 값과 문구는 1존과 같음. </summary>
        public static class Admin
        {
            /// <summary> 관리자 설정 JSON(StreamingAssets/Json/Admin.json) 파일명 — 비밀번호·자동 닫기 시간·진입 클릭 수. 파일이 없거나 값이 잘못되면 기본값을 씀. </summary>
            public const string SettingsFileName = "Admin";
            public const string DefaultPassword  = "0000";

            public const int PasswordMinLength = 4;
            public const int PasswordMaxLength = 6;

            public const string WrongPassword  = "비밀번호가 올바르지 않습니다.";
            public const string PasswordLength = "비밀번호는 4~6자리입니다.";

            // 비밀번호 창 안내 — 확인 단계마다 바뀜
            public const string PromptVerify     = "비밀번호를 입력하세요";
            public const string PromptNew        = "새 비밀번호를 입력하세요";
            public const string PromptConfirm    = "한 번 더 입력하세요";
            public const string PasswordMismatch = "비밀번호가 서로 다릅니다. 다시 입력하세요.";

            // 관리자 화면 상태 문구
            public const string PasswordChanged    = "비밀번호를 변경했습니다.";
            public const string PasswordSaveFailed = "비밀번호를 저장하지 못했습니다. Admin.json을 확인하세요.";
            public const string LocalModeSet       = "로컬 모드로 바꿨습니다. 관리자 화면을 닫으면 타이틀에 반영됩니다.";
            public const string ServerModeSet      = "서버 모드로 바꿨습니다. 관리자 화면을 닫으면 타이틀에 반영됩니다.";
            public const string VisitorNameChanged = "체험자 이름을 변경했습니다.";

            /// <summary> 체험자 이름 최대 글자 수 — 인트로·아웃트로 문장 안에 들어가므로 한 줄을 넘지 않게 제한함. </summary>
            public const int VisitorNameMaxLength = 8;

            // Admin.json(AdminSettings)에 값이 없거나 1보다 작을 때 쓰는 기본값
            public const float DefaultIdleCloseSeconds         = 60f; // 관리자 화면·이름 입력 창 무입력 자동 닫기
            public const float DefaultPasswordIdleCloseSeconds = 10f; // 비밀번호 창 무입력 자동 닫기
            public const int   DefaultEntryClickCount          = 10;  // 숨은 버튼 연속 클릭 횟수
            public const float DefaultEntryClickWindowSeconds  = 3f;  // 연속 클릭으로 인정하는 시간
        }

        /// <summary>
        /// 체험자 서버 API(서버 모드에서 타이틀 QR uid로 체험자 확인, 결과 화면에서 레벨 결과 저장). 서버는 현장 내부망에 있음.
        /// 경로·응답 문구·기본값은 1존과 같고 콘텐츠 코드만 이 존(D)의 것.
        /// </summary>
        public static class VisitorApi
        {
            /// <summary> 서버 주소 JSON(StreamingAssets/Json/Server.json) 파일명. </summary>
            public const string SettingsFileName = "Server";

            // 요청 실패(연결 실패·시간 초과·HTTP 오류) 시 응답 대기 시간(초)과 최대 시도 횟수(첫 시도 포함).
            // 결과 업로드(updateValue)는 화면을 막지 않아 넉넉히, 타이틀 QR 확인(checkActive·getUser)은 체험자가 화면 앞에서
            // 기다리므로 짧게 둠. 재시도 간격은 공통
            public const int   DefaultUploadTimeoutSeconds  = 5;
            public const int   DefaultUploadMaxAttempts     = 10;
            public const int   DefaultQrCheckTimeoutSeconds = 3;
            public const int   DefaultQrCheckMaxAttempts    = 3;
            public const float DefaultRetryDelaySeconds     = 1f;

            /// <summary> 체험 가능 여부(평문 응답) — 뒤에 uid를 붙임. 체험 가능하면 "idx_user,name"(예: "10,LLL"). </summary>
            public const string CheckActivePath   = "/api/checkActive.cfm?uid=";
            public const string CompletedResponse = "체험을 완료한 유저입니다";
            public const string NotFoundResponse  = "NOT_FOUND";

            /// <summary> 체험자 정보·진행도 — 뒤에 uid를 붙임. 응답 JSON의 user에 A1~D5(성공 1·실패 0·기록 없음 null)가 있음. </summary>
            public const string GetUserPath = "/api/getUser.cfm?uid=";

            /// <summary> 레벨 결과 저장 — {0} idx_user, {1} 콘텐츠 코드, {2} 성공 1·실패 0. </summary>
            public const string UpdateValuePathFormat = "/api/updateValue.cfm?idx_user={0}&code={1}&value={2}";

            /// <summary> 이 존(4존)의 콘텐츠 코드 — 레벨 번호를 붙여 D1~D5(레벨1~5)로 씀. </summary>
            public const string ZoneCode = "D";
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

            /// <summary>
            /// 레벨 5 재료(함수 사용·동작)와 동작 블록 id. 카드 분류로 재료가 정해지며 어느 순서로든 놓을 수 있음(성공하려면 함수 카드를 먼저 놓아야 함).
            /// 동작 블록은 함수 정의 블록 안에 들어가면 현재 상황 화면에 맞는 그림(Level5CityView)이 나타남.
            /// </summary>
            public static class Level5
            {
                public const string Function = "Function";
                public const string Action = "Action";

                public const string SpaceStationCode = "SpaceStationCode";           // 우주 정거장 코드 — 돔 기지
                public const string ExplorerRobotCode = "ExplorerRobotCode";         // 탐사 로봇 코드 — 로버
                public const string CommunicationCode = "CommunicationCode";         // 통신 시스템 코드 — 통신탑
                public const string ConnectionPassageCode = "ConnectionPassageCode"; // 연결 통로 코드 — 연결 통로
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
        /// 판정은 동작 블록이 모두 함수 정의 블록 안(함수 카드 뒤)에 있으면 성공(IngredientLevel5State.EvaluateMission).
        /// </summary>
        public static class Level5Cards
        {
            public const int Function = 1;
            public const int Action = 4;
            public const int Total = Function + Action;
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
            public const string MissionSuccess = "missonSuccess";  // 결과 완료 화면 '미션 성공!'
        }
    }
}
