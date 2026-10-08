namespace DGAIZone.Game.Events
{
    /// <summary>
    /// 리더기에 인식되어 있던 카드가 떨어져(UID가 cardRemovedDebounceMs 동안 오지 않음) 더 이상 감지되지 않을 때 발행되는 이벤트.
    /// 카드가 있는 동안에는 발행되지 않고, "있었다가 없어지는" 전환 시점에 1회만 발행됨.
    /// </summary>
    public readonly struct RfidReaderIdleEvent
    {
        public readonly string ReaderId;

        /// <summary> 카드가 떨어진 리더기 ID로 이벤트를 초기화함. </summary>
        public RfidReaderIdleEvent(string readerId)
        {
            ReaderId = readerId;
        }
    }
}
