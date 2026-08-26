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
        public string ingredientName;
        public string[] matterNames;
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
    /// 레벨 하나에 대한 카드 매핑 목록 직렬화 클래스. 같은 물리 카드(uid)라도 레벨마다 다른 의미로 매핑될 수 있음.
    /// </summary>
    [Serializable]
    public class RfidLevelMapping
    {
        public int level;
        public RfidMappingItem[] mappings;
    }

    /// <summary>
    /// RFID 리더기 설정 및 레벨별 카드 매핑 목록을 담는 직렬화 클래스.
    /// </summary>
    [Serializable]
    public class RfidSettings
    {
        public int baudRate = 9600;
        public int[] stageReadCounts = { 3 }; // 스테이지별 찍어야 하는 read 횟수 (인덱스 = 스테이지 번호)
        public RfidReaderConfig[] readers;
        public RfidLevelMapping[] levelMappings;

        /// <summary> 지정한 레벨에 해당하는 카드 매핑 목록을 찾아 반환함. 없으면 null. </summary>
        public RfidMappingItem[] GetMappingsForLevel(int level)
        {
            if (levelMappings == null) return null;

            foreach (RfidLevelMapping entry in levelMappings)
            {
                if (entry != null && entry.level == level) return entry.mappings;
            }

            return null;
        }
    }
}
