using System;

namespace DGAIZone.App
{
    /// <summary>
    /// Visitor.json 직렬화 클래스. 서버(QR 스캔) 연동 전, 로컬 실행 시 체험자 이름을 담아둠.
    /// </summary>
    [Serializable]
    public class VisitorData
    {
        public bool isServerConnected;
        public string defaultUserName;
    }
}
