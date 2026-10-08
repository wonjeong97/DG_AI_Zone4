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
    /// 레벨 안에서 단계(또는 카드 분류별 재료)가 좌우 버튼으로 고르는 블록 목록 하나. 같은 목록을 여러 단계가 쓰면
    /// (레벨 2 발사 순서 5단계, 레벨 3 올리기/낮추기 2단계) 한 번만 정의하고 단계마다 matterSetId로 참조함.
    /// JsonUtility가 Dictionary를 지원하지 않아 배열 + id로 둠.
    /// </summary>
    [Serializable]
    public class RfidMatterSet
    {
        public string id; // 단계의 matterSetId가 참조하는 식별자(같은 레벨 안에서만 유일하면 됨)
        public RfidMatter[] matters;
    }

    /// <summary>
    /// 워크플로우 진행 순서상 한 단계에 해당하는 재료 정의 직렬화 클래스. 고를 블록 목록은 같은 레벨의 matterSets에서 matterSetId로 찾음.
    /// RFID 카드는 category만 알려주므로, 실제 재료 순서(추진체 종류 -> 탑재 종류 -> 연료량)는
    /// 카드와 무관하게 이 목록의 순서로 진행됨.
    /// </summary>
    [Serializable]
    public class RfidStepDefinition
    {
        public string ingredientId;   // 코드가 재료 역할을 구분하는 고정 식별자(Constants.RfidIds)
        public string ingredientName; // 화면에 보이는 재료 이름
        public string matterSetId;    // 좌우 버튼으로 고를 블록 목록(같은 레벨 matterSets의 id)
        public string[] categories; // 이 단계를 진행시킬 수 있는 카드 분류 목록(동작/제어/논리/함수). 여럿이면 그중 아무 카드나 인식됨.

        /// <summary> 이 단계(또는 분류별 재료)가 주어진 카드 분류를 받는지 반환함. </summary>
        public bool AllowsCategory(string category)
        {
            if (categories == null) return false;

            for (int i = 0; i < categories.Length; i++)
            {
                if (string.Equals(categories[i], category, StringComparison.Ordinal)) return true;
            }
            return false;
        }
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
    /// 레벨 하나에 대한 블록 목록(matterSets)과 재료 진행 순서(steps) 직렬화 클래스. 물리 카드(uid/category)는 모든 레벨에서 공용이므로
    /// 여기서는 레벨마다 달라지는 블록과 진행 순서만 다룸.
    /// </summary>
    [Serializable]
    public class RfidLevelMapping
    {
        public int level;
        public RfidMatterSet[] matterSets; // 이 레벨의 블록 목록. 단계와 분류별 재료가 matterSetId로 참조함
        public RfidStepDefinition[] steps;

        // 단계가 아니라 찍은 카드 분류로 재료가 정해지는 레벨(레벨 4: 동작=이동하기, 제어=반복하기)의 분류별 재료 정의.
        // 각 항목의 categories에 그 재료를 고르는 카드 분류를 적음. 이 경우 steps에는 단계별 허용 categories만 적으면 됨.
        public RfidStepDefinition[] categoryIngredients;

        /// <summary> matterSetId에 해당하는 블록 목록을 찾아 반환함. 없으면 null. </summary>
        public RfidMatter[] FindMatters(string matterSetId)
        {
            if (matterSets == null || string.IsNullOrEmpty(matterSetId)) return null;

            foreach (RfidMatterSet set in matterSets)
            {
                if (set != null && string.Equals(set.id, matterSetId, StringComparison.Ordinal)) return set.matters;
            }

            return null;
        }
    }

    /// <summary>
    /// RFID 리더기 설정, 전 레벨 공용 카드 목록, 레벨별 재료 진행 순서를 담는 직렬화 클래스.
    /// </summary>
    [Serializable]
    public class RfidSettings
    {
        public int listenPort = 10123; // PC(서버)가 모든 리더기 클라이언트의 접속을 받는 TCP 포트(공용). 리더기(KA-LAN-754) 기본 목적지 포트값과 동일하게 맞춰둠
        public int[] stageReadCounts = { 3 }; // levelMappings에 단계(steps) 정의가 없는 레벨의 단계 수. 첫 값만 씀(예전 스테이지 구분의 흔적)

        // 리더기(KA-LAN-754)는 연속 읽기 모드로 설정함. 명령을 보내지 않아도 카드가 올라가 있는 동안 같은 UID를 계속 보내고,
        // 카드가 없으면 아무것도 보내지 않음. UID는 구분자 없는 원시 7바이트(예: 81 73 69 22 E5 1D 04)로 오며,
        // mappings[].uid에는 이를 공백 없는 16진수 14자리(예: "81736922E51D04")로 적음.
        // 예전 1회 읽기 모드의 아스키 응답 "A1G0" + 16진수 14자리 + 두 글자(예: "A1G081736922500B047C")에서 가운데 14자리가 같은 값임.

        // UID가 이 시간(ms) 동안 오지 않으면 카드가 떨어진 것으로 봄. 리더기가 UID를 다시 보내는 간격보다 길어야 하며,
        // 카드가 떨어질 때 로그에 남는 "UID 간격 최대"를 보고 맞춤. 접속 뒤 이 시간 안에 읽힌 첫 카드는 접속 전부터 올려져 있던
        // 카드로 보고 떼었다 다시 올릴 때까지 무시함. 0 이하이면 기본값을 씀.
        public const int DefaultCardRemovedDebounceMs = 1000;
        public int cardRemovedDebounceMs = DefaultCardRemovedDebounceMs;
        public RfidReaderConfig[] readers;
        public RfidMappingItem[] mappings; // 모든 레벨에서 공용으로 재사용되는 물리 카드 목록 (uid -> category)
        public RfidLevelMapping[] levelMappings;

        /// <summary> 지정한 레벨의 블록 목록·재료 진행 순서 정의를 찾아 반환함. 없으면 null. </summary>
        public RfidLevelMapping FindLevelMapping(int level)
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
