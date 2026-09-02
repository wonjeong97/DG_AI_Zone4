namespace DGAIZone.Game.Events
{
    /// <summary>
    /// RFID 카드가 인식되었을 때 MessagePipe를 통해 발행되는 이벤트 구조체.
    /// </summary>
    public readonly struct RfidTagEvent
    {
        public readonly string ReaderId;
        public readonly string Category;

        /// <summary>
        /// 인식된 리더기 ID와 카드 분류를 초기화함.
        /// </summary>
        public RfidTagEvent(string readerId, string category)
        {
            ReaderId = readerId;
            Category = category;
        }
    }
}
