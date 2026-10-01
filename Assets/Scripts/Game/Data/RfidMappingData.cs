using System;

namespace DGAIZone.Game.Data
{
    /// <summary>
    /// 개별 RFID 카드와 재료/물질 리스트 매핑 정보 직렬화 클래스.
    /// </summary>
    [Serializable]
    public class RfidMappingItem
    {
        public string uid;
        public string category; // 카드 분류: 동작, 제어, 논리, 함수
    }

    /// <summary>
    /// 좌우 버튼으로 고르는 물질(블록 값) 하나의 직렬화 클래스. 코드는 화면 이름(label)이 아니라 id와 value로 판정하므로,
    /// label은 자유롭게 바꿔도 되지만 id는 Constants.RfidIds와 맞아야 함.
    /// </summary>
    [Serializable]
    public class RfidMatter
    {
        public string id;    // 코드가 판정에 쓰는 고정 식별자
        public string label; // 화면에 보이는 이름
        public int value;    // 계산·판정용 수치(레벨 1: 엔진 출력량/탑재 중량/연료량, 레벨 3: 조건 기준값, 레벨 4: 반복 횟수). 쓰지 않으면 0
    }

    /// <summary>
    /// 워크플로우 진행 순서상 한 단계에 해당하는 재료/물질 목록 직렬화 클래스.
    /// RFID 카드는 category만 알려주므로, 실제 재료 순서(추진체 종류 -> 탑재 종류 -> 연료량)는
    /// 카드와 무관하게 이 목록의 순서로 진행됨.
    /// </summary>
    [Serializable]
    public class RfidStepDefinition
    {
        public string ingredientId;   // 코드가 재료 역할을 구분하는 고정 식별자(Constants.RfidIds)
        public string ingredientName; // 화면에 보이는 재료 이름
        public RfidMatter[] matters;
        public string[] categories; // 이 단계를 진행시킬 수 있는 카드 분류 목록(동작/제어/논리/함수). 여럿이면 그중 아무 카드나 인식됨.
    }

    /// <summary>
    /// 개별 RFID 리더기 연결 설정 직렬화 클래스. 리더기는 네트워크 클라이언트로 PC(서버)에 접속하며,
    /// 접속해온 소켓의 IP를 이 ipAddress와 대조해 readerId를 식별함(1순위). IP가 일치하지 않으면
    /// ARP 테이블로 조회한 MAC 주소를 macAddress와 대조함(2순위 폴백, DHCP 등으로 IP가 바뀌어도 식별 가능).
    /// </summary>
    [Serializable]
    public class RfidReaderConfig
    {
        public string readerId;
        public string ipAddress;
        public string macAddress; // 형식: "34-46-63-D4-33-CD" (구분자는 대조 시 정규화되므로 -, :, 공백 아무거나 가능)
    }

    /// <summary>
    /// 레벨 하나에 대한 재료 진행 순서(steps) 직렬화 클래스. 물리 카드(uid/category)는 모든 레벨에서 공용이므로
    /// 여기서는 레벨마다 달라지는 진행 순서만 다룸.
    /// </summary>
    [Serializable]
    public class RfidLevelMapping
    {
        public int level;
        public RfidStepDefinition[] steps;

        // 단계가 아니라 찍은 카드 분류로 재료가 정해지는 레벨(레벨 4: 동작=이동하기, 제어=반복하기)의 분류별 재료 정의.
        // 각 항목의 categories에 그 재료를 고르는 카드 분류를 적음. 이 경우 steps에는 단계별 허용 categories만 적으면 됨.
        public RfidStepDefinition[] categoryIngredients;
    }

    /// <summary>
    /// RFID 리더기 설정, 전 레벨 공용 카드 목록, 레벨별 재료 진행 순서를 담는 직렬화 클래스.
    /// </summary>
    [Serializable]
    public class RfidSettings
    {
        public int listenPort = 10123; // PC(서버)가 모든 리더기 클라이언트의 접속을 받는 TCP 포트(공용). 리더기(KA-LAN-754) 기본 목적지 포트값과 동일하게 맞춰둠
        public int[] stageReadCounts = { 3 }; // 스테이지별 찍어야 하는 read 횟수 (인덱스 = 스테이지 번호)

        // 리더기(KA-LAN-754)는 데이터를 먼저 push하지 않음(실측 확인됨: 카드만 태그해선 아무 데이터도 안 옴).
        // 아래 명령이 "1회 읽기" 트리거로 추정되며, 이걸 반복 전송해 폴링해야 함.
        // 실측: 카드 없음="09 41 31 47 33 45 0D"(그대로 에코) 또는 "0A 41 31 47 33 44 0D"(7바이트),
        //       카드 있음="0A 41 31 47 30 38 31 37 33 36 39 32 32 35 30 30 42 30 34 37 43 0D"(22바이트, UID 포함).
        public string pollCommandHex = "09 41 31 47 33 45 0D"; // 폴링(1회 읽기 트리거) 명령(공백으로 구분된 16진수 바이트열)
        public int pollIntervalMs = 1000; // 폴링 명령을 반복 전송하는 주기(ms)
        public int pollResponseTimeoutMs = 300; // 폴링 응답을 기다리는 최대 시간(ms). 초과하면 이번 폴링은 건너뜀
        public int noCardResponseMaxLength = 7; // 이 바이트 수 이하의 응답은 "카드 없음"으로 간주하고 무시함(실측 기준 무카드=7바이트, 카드 인식=22바이트)

        // 리더기는 유니티가 실행되지 않는 동안에도 백그라운드에서 계속 스캔을 유지하다가, 접속 후 첫 읽기 명령을
        // 보내는 순간 그동안 쌓여있던(유니티와 무관하게 읽힌) 잔여 카드 값을 그대로 돌려주는 경우가 있음.
        // 접속 후 리더기별로 이 횟수만큼의 "카드 인식" 응답은 발행하지 않고 버림(기준값으로만 저장).
        public int initialCardReadsToDiscard = 2;
        public RfidReaderConfig[] readers;
        public RfidMappingItem[] mappings; // 모든 레벨에서 공용으로 재사용되는 물리 카드 목록 (uid -> category)
        public RfidLevelMapping[] levelMappings;

        /// <summary> 지정한 레벨에 해당하는 재료 진행 순서 목록을 찾아 반환함. 없으면 null. </summary>
        public RfidStepDefinition[] GetStepsForLevel(int level) => FindLevelMapping(level)?.steps;

        /// <summary> 지정한 레벨의 카드 분류별 재료 정의(레벨 4)를 찾아 반환함. 없으면 null. </summary>
        public RfidStepDefinition[] GetCategoryIngredientsForLevel(int level) => FindLevelMapping(level)?.categoryIngredients;

        private RfidLevelMapping FindLevelMapping(int level)
        {
            if (levelMappings == null) return null;

            foreach (RfidLevelMapping entry in levelMappings)
            {
                if (entry != null && entry.level == level) return entry;
            }

            return null;
        }
    }
}
