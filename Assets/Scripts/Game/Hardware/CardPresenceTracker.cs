using System;

namespace DGAIZone.Game.Hardware
{
    /// <summary>
    /// 연속 읽기 모드 리더기 하나의 카드 상태를 판정함. 리더기는 카드가 올라가 있는 동안 같은 UID를 계속 보내고 카드가 없으면
    /// 아무것도 보내지 않으므로, UID가 RemovedTimeoutMs 동안 오지 않으면 카드가 떨어진 것으로 봄.
    /// 접속 뒤 RemovedTimeoutMs 안에 읽힌 첫 카드는 접속 전부터 올려져 있던 카드로 보고 알리지 않음(떼었다 다시 올리면 알림).
    /// 시간 간격은 RfidMappings.json의 cardRemovedDebounceMs로 바꿀 수 있으며, 리더기가 UID를 다시 보내는 간격보다 길어야 함.
    /// </summary>
    public class CardPresenceTracker
    {
        /// <summary> 받은 UID를 어떻게 처리할지. </summary>
        public enum CardFrameResult
        {
            NewCard,  // 새로 올라온 카드. 알려야 함
            Baseline, // 접속 때 이미 올려져 있던 카드. 알리지 않음
            Repeat    // 올라가 있는 카드가 반복해서 보낸 UID. 알리지 않음
        }

        private readonly long _connectedAtMs;
        private bool _baselineWindowOpen = true; // 접속 때 이미 올려져 있던 카드를 가려내는 중인지. 첫 카드를 받거나 RemovedTimeoutMs가 지나면 닫힘
        private long _lastCardFrameMs;

        /// <param name="removedTimeoutMs"> UID가 이 시간(ms) 동안 오지 않으면 떨어짐으로 봄. 0보다 커야 함(호출부가 검사). </param>
        /// <param name="connectedAtMs"> 리더기가 접속한 시각(ms). </param>
        public CardPresenceTracker(long removedTimeoutMs, long connectedAtMs)
        {
            RemovedTimeoutMs = removedTimeoutMs;
            _connectedAtMs = connectedAtMs;
        }

        /// <summary> UID가 이 시간(ms) 동안 오지 않으면 떨어짐으로 봄. </summary>
        public long RemovedTimeoutMs { get; }

        /// <summary> 지금 올라가 있다고 보는 카드 UID. 없으면 null. </summary>
        public string CurrentCard { get; private set; }

        /// <summary> 마지막으로 올라온 카드가 올라가 있던 동안 받은 UID 사이 간격 중 가장 긴 값(ms). cardRemovedDebounceMs를 맞출 때 참고함. </summary>
        public long MaxFrameGapMs { get; private set; }

        /// <summary> now(ms) 시각에 UID를 받음. </summary>
        public CardFrameResult OnCardFrame(string uid, long nowMs)
        {
            if (string.Equals(uid, CurrentCard, StringComparison.Ordinal))
            {
                long gap = nowMs - _lastCardFrameMs;
                if (gap > MaxFrameGapMs) MaxFrameGapMs = gap;
                _lastCardFrameMs = nowMs;
                return CardFrameResult.Repeat;
            }

            bool isBaseline = _baselineWindowOpen;
            _baselineWindowOpen = false;
            CurrentCard = uid;
            _lastCardFrameMs = nowMs;
            MaxFrameGapMs = 0;
            return isBaseline ? CardFrameResult.Baseline : CardFrameResult.NewCard;
        }

        /// <summary> now(ms) 시각에 카드가 떨어졌는지 확인함. 떨어짐으로 확정되면 true를 돌려주고 CurrentCard를 비움(카드 하나에 한 번만 true). </summary>
        public bool CheckRemoved(long nowMs)
        {
            if (_baselineWindowOpen && nowMs - _connectedAtMs >= RemovedTimeoutMs) _baselineWindowOpen = false;
            if (CurrentCard == null || nowMs - _lastCardFrameMs < RemovedTimeoutMs) return false;

            CurrentCard = null;
            return true;
        }
    }
}
