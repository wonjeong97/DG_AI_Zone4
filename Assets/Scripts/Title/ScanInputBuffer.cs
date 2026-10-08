using System.Text;

namespace DGAIZone.Title
{
    /// <summary>
    /// USB QR 스캐너가 키보드처럼 보낸 문자를 Enter 전까지 모음.
    /// 스캐너는 uid 하나를 수십 ms 안에 보내므로, 앞 글자와 MaxCharGapSeconds 넘게 벌어진 글자가 오면 앞에 모은 글자
    /// (찍기 전에 눌린 키 등)를 버리고 새로 모음. Enter가 마지막 글자보다 그만큼 늦게 오면 모은 글자는 낡은 것으로 봄.
    /// 입력을 받지 않던 동안 시작된 스캔이 다시 받기 시작한 뒤 뒷부분만 잘려 들어오지 않도록, Restart 직후 쉬지 않고 이어서 오는 글자는
    /// 쉬거나 Enter(TakeAndClear)가 올 때까지 버림. 그래서 Restart 뒤 MaxCharGapSeconds 안에 시작한 스캔은 다시 찍어야 함.
    /// 간격은 0_Title.json의 scanCharGapSeconds로 바꿀 수 있음(PC가 느려 스캔 글자가 늦게 들어오는 현장 대비).
    /// </summary>
    public class ScanInputBuffer
    {
        /// <summary> 0_Title.json을 읽기 전·잘못된 값일 때 쓰는 글자 사이 최대 간격(초). </summary>
        public const float DefaultMaxCharGapSeconds = 0.5f;

        private readonly StringBuilder _buffer = new();
        private float _lastCharTime;
        private bool _skipUntilGap; // Restart 직후라, MaxCharGapSeconds 넘게 쉬기 전까지 이어서 오는 글자를 버리는 중인지
        private int _skippedCount;  // 그렇게 버린 글자 수(TakeSkippedCount로 꺼냄)

        /// <summary> 한 번의 스캔으로 보는 글자 사이 최대 간격(초). 0보다 커야 함(호출부가 검사). </summary>
        public float MaxCharGapSeconds { get; set; } = DefaultMaxCharGapSeconds;

        /// <summary> 모은 글자 수. </summary>
        public int Length => _buffer.Length;

        /// <summary>
        /// 문자 하나를 now(초) 시각에 받은 것으로 덧붙임. 앞 글자와 간격이 MaxCharGapSeconds를 넘으면 앞에 모은 글자를 버리고 새로 시작하며,
        /// 버린 글자 수를 돌려줌(버리지 않았으면 0). Restart 뒤 쉬지 않고 이어서 온 글자는 덧붙이지 않고 TakeSkippedCount로 셈.
        /// </summary>
        public int Append(char c, float now)
        {
            if (_skipUntilGap)
            {
                if (now - _lastCharTime <= MaxCharGapSeconds)
                {
                    _lastCharTime = now;
                    _skippedCount++;
                    return 0;
                }

                _skipUntilGap = false;
            }

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

        /// <summary> 모은 문자열을 꺼내고 비움(Enter 때 부름). Enter는 잘린 스캔의 끝이기도 하므로 Restart 뒤 버리던 것도 끝냄. </summary>
        public string TakeAndClear()
        {
            string text = _buffer.ToString();
            _buffer.Clear();
            _skipUntilGap = false;
            return text;
        }

        /// <summary>
        /// 모은 글자를 비우고 now(초)부터 다시 받음. 받지 않던 동안(QR 확인·안내 표시 중) 시작된 스캔의 뒷부분이 잘린 uid로 들어오지 않게,
        /// 그 뒤로 MaxCharGapSeconds 넘게 쉬기 전까지 이어서 오는 글자는 모으지 않고 버림.
        /// </summary>
        public void Restart(float now)
        {
            _buffer.Clear();
            _lastCharTime = now;
            _skipUntilGap = true;
            _skippedCount = 0;
        }

        /// <summary> Restart 뒤 앞 스캔의 뒷부분으로 보고 버린 글자 수를 꺼내고 0으로 되돌림. </summary>
        public int TakeSkippedCount()
        {
            int count = _skippedCount;
            _skippedCount = 0;
            return count;
        }
    }
}
