using DGAIZone.Network;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 체험자 서버 getUser 응답에서 이 존(4존, D1~D5)의 진행도를 읽고 열 레벨 수로 바꾸는지 검증함 — 응답 형식은 현장 서버 실측값.
    /// </summary>
    public class GetUserResultTests
    {
        // 현장 서버 실측 응답 — 기록이 없으면 null
        private const string AllNullBody = @"{
    ""result"": true,
    ""user"": {
        ""idx_user"": 10,
        ""name"": ""LLL"",
        ""uid"": ""440930W1XWQH"",
        ""dates"": ""2026-10-07 13:27:18"",

        ""A1"": null,
        ""A2"": null,
        ""A3"": null,
        ""A4"": null,
        ""A5"": null,

        ""B1"": null,
        ""B2"": null,
        ""B3"": null,
        ""B4"": null,
        ""B5"": null,

        ""C1"": null,
        ""C2"": null,
        ""C3"": null,
        ""C4"": null,
        ""C5"": null,

        ""D1"": null,
        ""D2"": null,
        ""D3"": null,
        ""D4"": null,
        ""D5"": null
    }
}";

        /// <summary> 이 존의 레벨 값만 바꾼 응답을 만듦. </summary>
        private static string WithValues(string d1, string d2, string d3, string d4, string d5) =>
            AllNullBody
                .Replace(@"""D1"": null", @"""D1"": " + d1)
                .Replace(@"""D2"": null", @"""D2"": " + d2)
                .Replace(@"""D3"": null", @"""D3"": " + d3)
                .Replace(@"""D4"": null", @"""D4"": " + d4)
                .Replace(@"""D5"": null", @"""D5"": " + d5);

        /// <summary> 기록이 하나도 없으면 찾았지만 마지막 기록 레벨은 0이고 레벨 1만 열림. </summary>
        [Test]
        public void 기록이_없으면_레벨_1만_열린다()
        {
            GetUserResult result = GetUserResult.Parse(AllNullBody);

            Assert.IsTrue(result.IsFound);
            Assert.AreEqual(0, result.LastRecordedLevel);
            Assert.AreEqual(1, result.UnlockedLevelCount);
        }

        /// <summary> 성공(1)·실패(0) 모두 기록으로 보고, 값이 있는 마지막 레벨의 다음 레벨까지 엶(최대 5). </summary>
        [TestCase("1", "null", "null", "null", "null", 1, 2)]
        [TestCase("1", "0", "null", "null", "null", 2, 3)]
        [TestCase("0", "null", "0", "null", "null", 3, 4)]
        [TestCase("1", "1", "1", "0", "null", 4, 5)]
        [TestCase("1", "1", "1", "1", "1", 5, 5)]
        [TestCase("null", "null", "null", "null", "0", 5, 5)]
        public void 값이_있는_마지막_레벨의_다음_레벨까지_연다(string d1, string d2, string d3, string d4, string d5, int expectedLast, int expectedUnlocked)
        {
            GetUserResult result = GetUserResult.Parse(WithValues(d1, d2, d3, d4, d5));

            Assert.IsTrue(result.IsFound);
            Assert.AreEqual(expectedLast, result.LastRecordedLevel);
            Assert.AreEqual(expectedUnlocked, result.UnlockedLevelCount);
        }

        /// <summary> 다른 존(A~C)의 기록은 이 존의 진행도에 영향을 주지 않음. </summary>
        [Test]
        public void 다른_존의_기록은_무시한다()
        {
            string body = AllNullBody.Replace(@"""A5"": null", @"""A5"": 1").Replace(@"""C3"": null", @"""C3"": 0");

            GetUserResult result = GetUserResult.Parse(body);

            Assert.AreEqual(0, result.LastRecordedLevel);
            Assert.AreEqual(1, result.UnlockedLevelCount);
        }

        /// <summary> 한 줄로 붙은 응답(공백 없음)도 같은 규칙으로 읽음. </summary>
        [Test]
        public void 공백_없는_응답도_읽는다()
        {
            const string body = "{\"result\":true,\"user\":{\"idx_user\":10,\"name\":\"LLL\",\"D1\":1,\"D2\":0,\"D3\":null,\"D4\":null,\"D5\":null}}";

            GetUserResult result = GetUserResult.Parse(body);

            Assert.AreEqual(2, result.LastRecordedLevel);
            Assert.AreEqual(3, result.UnlockedLevelCount);
        }

        /// <summary> 따옴표로 감싼 숫자("1"·"0")도 기록으로 봄. </summary>
        [Test]
        public void 따옴표로_감싼_숫자도_기록으로_본다()
        {
            GetUserResult result = GetUserResult.Parse(WithValues("\"1\"", "\"0\"", "null", "null", "null"));

            Assert.AreEqual(2, result.LastRecordedLevel);
            Assert.AreEqual(3, result.UnlockedLevelCount);
        }

        /// <summary> 이 존에 없는 레벨 키(D0·D6·D10)는 무시해, 없는 레벨 기록 때문에 모든 레벨이 열리지 않음. </summary>
        [Test]
        public void 이_존에_없는_레벨_키는_무시한다()
        {
            string body = WithValues("1", "null", "null", "null", "null")
                .Replace(@"""D5"": null", @"""D5"": null, ""D6"": 1, ""D10"": 0, ""D0"": 1");

            GetUserResult result = GetUserResult.Parse(body);

            Assert.AreEqual(1, result.LastRecordedLevel);
            Assert.AreEqual(2, result.UnlockedLevelCount);
        }

        /// <summary> result가 false면 찾지 못한 것이고, 서버 메시지를 실패 사유로 남김. </summary>
        [Test]
        public void result가_false면_찾지_못한_것이다()
        {
            GetUserResult result = GetUserResult.Parse("{\"result\":false,\"message\":\"NOT_FOUND\"}");

            Assert.IsFalse(result.IsFound);
            Assert.AreEqual("NOT_FOUND", result.FailReason);
        }

        /// <summary> JSON이 아니거나 빈 응답은 찾지 못한 것으로 봄. </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("NOT_FOUND")]
        [TestCase("<html><body>Error</body></html>")]
        public void 약속하지_않은_응답은_찾지_못한_것이다(string body)
        {
            GetUserResult result = GetUserResult.Parse(body);

            Assert.IsFalse(result.IsFound);
            Assert.AreEqual(0, result.LastRecordedLevel);
        }
    }
}
