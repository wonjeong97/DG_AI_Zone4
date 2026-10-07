using DGAIZone.Game.Data;

namespace DGAIZone.Game.Hardware
{
    /// <summary>
    /// 리더기 위에 인식돼 있던 카드가 떨어졌는지 판정함. 리더기가 카드가 올라가 있는데도 가끔 "카드 없음"으로 잘못 응답하므로,
    /// "카드 없음" 응답이 DebounceMs 동안 이어져야 떨어짐으로 보고, 그 사이에 카드가 다시 읽히면 오판정으로 보고 무시함.
    /// 시간 간격은 RfidMappings.json의 cardRemovedDebounceMs로 바꿀 수 있음(0이면 첫 "카드 없음" 응답에 바로 떨어짐으로 봄).
    /// </summary>
    public class CardRemovalDebouncer
    {
        private long _noCardSinceMs = -1; // 떨어짐을 기다리기 시작한 시각(ms). 기다리는 중이 아니면 -1

        /// <summary> 떨어짐으로 볼 때까지 "카드 없음" 응답이 이어져야 하는 시간(ms). 0 이상이어야 함(호출부가 검사). </summary>
        public long DebounceMs { get; set; } = RfidSettings.DefaultCardRemovedDebounceMs;

        /// <summary> 떨어짐을 기다리는 동안 받은 "카드 없음" 응답 수. </summary>
        public int NoCardReads { get; private set; }

        /// <summary> 인식돼 있던 카드에 대해 now(ms) 시각에 "카드 없음" 응답을 받음. 떨어짐으로 확정되면 true를 돌려주고 기다리던 상태를 비움. </summary>
        public bool OnNoCard(long nowMs)
        {
            if (_noCardSinceMs < 0) _noCardSinceMs = nowMs;
            NoCardReads++;
            if (nowMs - _noCardSinceMs < DebounceMs) return false;

            Reset();
            return true;
        }

        /// <summary> 카드가 읽힘. 떨어짐을 기다리던 중이었으면 그동안 받은 "카드 없음" 응답 수를 돌려주고(오판정 로그용) 기다리던 상태를 비움. 아니면 0. </summary>
        public int OnCardRead()
        {
            int ignored = NoCardReads;
            Reset();
            return ignored;
        }

        private void Reset()
        {
            _noCardSinceMs = -1;
            NoCardReads = 0;
        }
    }
}
