namespace DGAIZone.Game.Events
{
    /// <summary>
    /// 리더기에 인식되어 있던 카드가 떨어져(카드 없음 응답으로 전환) 더 이상 감지되지 않을 때 발행되는 이벤트.
    /// 카드가 있는 동안에는 발행되지 않고, "있었다가 없어지는" 전환 시점에 1회만 발행됨.
    /// </summary>
    public readonly struct RfidReaderIdleEvent
    {
        public readonly string ReaderId;

        public RfidReaderIdleEvent(string readerId)
        {
            ReaderId = readerId;
        }
    }
}
