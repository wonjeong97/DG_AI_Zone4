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
    /// 개별 RFID 리더기 연결 설정 직렬화 클래스.
    /// </summary>
    [Serializable]
    public class RfidReaderConfig
    {
        public string readerId;
        public string deviceInstancePath;
        public string fallbackPort;
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
        public int baudRate = 9600;
        public int[] stageReadCounts = { 3 }; // 스테이지별 찍어야 하는 read 횟수 (인덱스 = 스테이지 번호)
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
