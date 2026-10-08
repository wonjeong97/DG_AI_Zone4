using System;

namespace DGAIZone.Game.Hardware
{
    /// <summary>
    /// 리더기가 보낸 UID 바이트를 16진수 문자열(예: "81736922500B04")로 바꿈. 카드가 올려진 동안 같은 UID가 초당 여러 번 반복되므로,
    /// 직전과 바이트가 같으면 만들어 둔 문자열을 그대로 돌려줘 반복될 때마다 문자열을 새로 만들지 않음. 리더기 수신 스레드마다 하나씩 씀.
    /// </summary>
    internal sealed class RfidUidDecoder
    {
        private readonly byte[] _lastBytes;
        private int _lastLength;
        private string _lastUid;

        /// <summary> 한 번에 받을 수 있는 UID의 최대 바이트 수 maxLength로 디코더를 만듦. </summary>
        public RfidUidDecoder(int maxLength)
        {
            _lastBytes = new byte[maxLength];
        }

        /// <summary> frame의 앞 length바이트를 UID 문자열로 바꿈. 직전과 같은 바이트면 직전 문자열을 다시 돌려줌. </summary>
        public string Decode(byte[] frame, int length)
        {
            ReadOnlySpan<byte> bytes = frame.AsSpan(0, length);
            if (_lastUid != null && bytes.SequenceEqual(_lastBytes.AsSpan(0, _lastLength))) return _lastUid;

            bytes.CopyTo(_lastBytes);
            _lastLength = length;
            _lastUid = BitConverter.ToString(frame, 0, length).Replace("-", "");
            return _lastUid;
        }
    }
}
