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
    /// 워크플로우 진행 순서상 한 단계에 해당하는 재료/물질 목록 직렬화 클래스.
    /// RFID 카드는 category만 알려주므로, 실제 재료 순서(추진체 종류 -> 탑재 종류 -> 연료량)는
    /// 카드와 무관하게 이 목록의 순서로 진행됨.
    /// </summary>
    [Serializable]
    public class RfidStepDefinition
    {
        public string ingredientName;
        public string[] matterNames;
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
        public RfidReaderConfig[] readers;
        public RfidMappingItem[] mappings; // 모든 레벨에서 공용으로 재사용되는 물리 카드 목록 (uid -> category)
        public RfidLevelMapping[] levelMappings;

        /// <summary> 지정한 레벨에 해당하는 재료 진행 순서 목록을 찾아 반환함. 없으면 null. </summary>
        public RfidStepDefinition[] GetStepsForLevel(int level)
        {
            if (levelMappings == null) return null;

            foreach (RfidLevelMapping entry in levelMappings)
            {
                if (entry != null && entry.level == level) return entry.steps;
            }

            return null;
        }
    }
}
