using DGAIZone.Game.Data;
using DGAIZone.Game.Hardware;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 접속한 리더기를 RfidMappings.json readers의 IP·MAC으로 식별하는 규칙 검증. 엇갈리면 MAC을 따르고, 어느 쪽과도 맞지 않으면(옆 PC의 리더기 등)
    /// 식별하지 않아 접속을 거부하게 함.
    /// </summary>
    public class RfidReaderIdentifyTests
    {
        private static readonly RfidReaderConfig[] Readers =
        {
            new RfidReaderConfig { readerId = "Reader_1", ipAddress = "192.168.0.180", macAddress = "34-46-63-D4-33-CD" },
            new RfidReaderConfig { readerId = "Reader_2", ipAddress = "192.168.0.181", macAddress = "34-46-63-D4-38-92" }
        };

        [Test]
        public void IP나_MAC이_맞으면_그_리더기로_식별한다()
        {
            Assert.AreEqual("Reader_1", RfidReaderService.MatchReaderId(Readers, "192.168.0.180", null, out _, out _), "MAC을 조회하지 못하면 IP로");
            Assert.AreEqual("Reader_2", RfidReaderService.MatchReaderId(Readers, "192.168.0.99", "34:46:63:d4:38:92", out _, out _), "IP가 바뀌어도 MAC으로(구분자·대소문자 무관)");
        }

        [Test]
        public void IP와_MAC이_엇갈리면_MAC을_따른다()
        {
            string id = RfidReaderService.MatchReaderId(Readers, "192.168.0.180", "34-46-63-D4-38-92", out string ipMatched, out string macMatched);

            Assert.AreEqual("Reader_2", id);
            Assert.AreEqual("Reader_1", ipMatched);
            Assert.AreEqual("Reader_2", macMatched);
        }

        [Test]
        public void 등록되지_않은_장비는_식별하지_않는다()
        {
            // 옆 PC의 리더기(185~189)가 Target IP를 잘못 가리켜 이 PC에 접속한 경우
            Assert.IsNull(RfidReaderService.MatchReaderId(Readers, "192.168.0.185", "34-46-63-D4-35-35", out _, out _));
            Assert.IsNull(RfidReaderService.MatchReaderId(Readers, "192.168.0.186", null, out _, out _), "MAC 조회 실패 + 미등록 IP");
        }
    }
}
