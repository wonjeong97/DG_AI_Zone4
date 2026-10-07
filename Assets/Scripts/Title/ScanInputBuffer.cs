using System.Text;

namespace DGAIZone.Title
{
    /// <summary>
    /// USB QR 스캐너가 키보드처럼 보낸 문자를 Enter 전까지 모음.
    /// 스캐너는 uid 하나를 수십 ms 안에 보내므로, 앞 글자와 MaxCharGapSeconds 넘게 벌어진 글자가 오면 앞에 모은 글자
    /// (찍기 전에 눌린 키 등)를 버리고 새로 모음. Enter가 마지막 글자보다 그만큼 늦게 오면 모은 글자는 낡은 것으로 봄.
    /// 간격은 0_Title.json의 scanCharGapSeconds로 바꿀 수 있음(PC가 느려 스캔 글자가 늦게 들어오는 현장 대비).
    /// </summary>
    public class ScanInputBuffer
    {
        /// <summary> 0_Title.json을 읽기 전·잘못된 값일 때 쓰는 글자 사이 최대 간격(초). </summary>
        public const float DefaultMaxCharGapSeconds = 0.5f;

        private readonly StringBuilder _buffer = new();
        private float _lastCharTime;

        /// <summary> 한 번의 스캔으로 보는 글자 사이 최대 간격(초). 0보다 커야 함(호출부가 검사). </summary>
        public float MaxCharGapSeconds { get; set; } = DefaultMaxCharGapSeconds;

        public int Length => _buffer.Length;

        /// <summary>
        /// 문자 하나를 now(초) 시각에 받은 것으로 덧붙임. 앞 글자와 간격이 MaxCharGapSeconds를 넘으면 앞에 모은 글자를 버리고 새로 시작하며,
        /// 버린 글자 수를 돌려줌(버리지 않았으면 0).
        /// </summary>
        public int Append(char c, float now)
        {
            int discarded = 0;
            if (IsStale(now))
            {
                discarded = _buffer.Length;
                _buffer.Clear();
            }

            _buffer.Append(c);
            _lastCharTime = now;
            return discarded;
        }

        /// <summary> 모은 글자가 있고 마지막 글자 뒤로 MaxCharGapSeconds가 지났는지(한 번의 스캔이 아닌 낡은 입력인지). </summary>
        public bool IsStale(float now)
        {
            return _buffer.Length > 0 && now - _lastCharTime > MaxCharGapSeconds;
        }

        /// <summary> 모은 문자열을 꺼내고 비움. </summary>
        public string TakeAndClear()
        {
            string text = _buffer.ToString();
            _buffer.Clear();
            return text;
        }

        /// <summary> 모은 글자를 비움. </summary>
        public void Clear()
        {
            _buffer.Clear();
        }
    }
}
