using DGAIZone.Game.Hardware;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 리더기 UID 바이트를 16진수 문자열로 바꾸는 RfidUidDecoder 테스트. 카드가 올려진 동안 반복되는 같은 UID는 문자열을 새로 만들지 않고,
    /// 다른 UID나 길이가 다른 UID는 새로 바꾸는지 확인함.
    /// </summary>
    public class RfidUidDecoderTests
    {
        [Test]
        public void 같은_UID가_반복되면_만들어_둔_문자열을_다시_쓴다()
        {
            RfidUidDecoder decoder = new RfidUidDecoder(7);
            byte[] frame = { 0x81, 0x73, 0x69, 0x22, 0x50, 0x0B, 0x04 };

            string first = decoder.Decode(frame, 7);
            string second = decoder.Decode(frame, 7);

            Assert.AreEqual("81736922500B04", first);
            Assert.AreSame(first, second, "반복되는 같은 UID는 문자열을 새로 만들지 않아야 함");
        }

        [Test]
        public void 다른_UID나_길이가_다른_UID는_새로_바꾼다()
        {
            RfidUidDecoder decoder = new RfidUidDecoder(7);
            byte[] frame = { 0x81, 0x73, 0x69, 0x22, 0x50, 0x0B, 0x04 };
            decoder.Decode(frame, 7);

            frame[4] = 0xE5;
            Assert.AreEqual("81736922E50B04", decoder.Decode(frame, 7), "바이트가 하나라도 다르면 새 UID");
            Assert.AreEqual("81736922", decoder.Decode(frame, 4), "앞부분이 같아도 길이가 다르면 새 UID");
            Assert.AreEqual("951E0FDA0D0D04", decoder.Decode(new byte[] { 0x95, 0x1E, 0x0F, 0xDA, 0x0D, 0x0D, 0x04 }, 7), "0x0D가 든 UID도 그대로 바꿈");
        }
    }
}
