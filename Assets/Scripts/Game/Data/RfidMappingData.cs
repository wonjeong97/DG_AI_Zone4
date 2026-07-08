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
    /// RFID 리더기 설정 및 전체 카드 매핑 목록을 담는 직렬화 클래스.
    /// </summary>
    [Serializable]
    public class RfidSettings
    {
        public int baudRate = 9600;
        public int[] stageReadCounts = { 3 }; // 스테이지별 찍어야 하는 read 횟수 (인덱스 = 스테이지 번호)
        public RfidReaderConfig[] readers;
        public RfidMappingItem[] mappings;
    }
}
