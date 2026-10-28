using System.Collections.Generic;
using DGAIZone.App;
using DGAIZone.Network;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 체험자 서버 getUser 응답에서 이 존(4존, D1~D5)의 진행도를 읽고 기록이 있는 레벨(레벨 선택 화면의 V)을 모으는지 검증함 — 응답 형식은 현장 서버 실측값.
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

        /// <summary> 레벨 1~5 중 기록이 있는 레벨을 "1,3"처럼 적음(없으면 빈 문자열). </summary>
        private static string Recorded(GetUserResult result)
        {
            List<string> levels = new List<string>();
            for (int level = 1; level <= Constants.LastLevel; level++)
                if (result.IsRecorded(level)) levels.Add(level.ToString());
            return string.Join(",", levels);
        }

        /// <summary> 기록이 하나도 없으면 찾았지만 기록 있는 레벨이 없음(V 없음). </summary>
        [Test]
        public void 기록이_없으면_기록_있는_레벨이_없다()
        {
            GetUserResult result = GetUserResult.Parse(AllNullBody);

            Assert.IsTrue(result.IsFound);
            Assert.AreEqual("", Recorded(result));
        }

        /// <summary> 성공(1)·실패(0) 모두 기록으로 보고, 값이 있는 레벨만 기록 있는 레벨로 봄(사이가 비어 있어도 그대로). </summary>
        [TestCase("1", "null", "null", "null", "null", "1")]
        [TestCase("1", "0", "null", "null", "null", "1,2")]
        [TestCase("0", "null", "0", "null", "null", "1,3")]
        [TestCase("1", "1", "1", "0", "null", "1,2,3,4")]
        [TestCase("1", "1", "1", "1", "1", "1,2,3,4,5")]
        [TestCase("null", "null", "null", "null", "0", "5")]
        public void 값이_있는_레벨만_기록으로_본다(string d1, string d2, string d3, string d4, string d5, string expected)
        {
            GetUserResult result = GetUserResult.Parse(WithValues(d1, d2, d3, d4, d5));

            Assert.IsTrue(result.IsFound);
            Assert.AreEqual(expected, Recorded(result));
        }

        /// <summary> 다른 존(A~C)의 기록은 이 존의 진행도에 영향을 주지 않음. </summary>
        [Test]
        public void 다른_존의_기록은_무시한다()
        {
            string body = AllNullBody.Replace(@"""A5"": null", @"""A5"": 1").Replace(@"""C3"": null", @"""C3"": 0");

            GetUserResult result = GetUserResult.Parse(body);

            Assert.AreEqual("", Recorded(result));
        }

        /// <summary> 한 줄로 붙은 응답(공백 없음)도 같은 규칙으로 읽음. </summary>
        [Test]
        public void 공백_없는_응답도_읽는다()
        {
            const string body = "{\"result\":true,\"user\":{\"idx_user\":10,\"name\":\"LLL\",\"D1\":1,\"D2\":0,\"D3\":null,\"D4\":null,\"D5\":null}}";

            GetUserResult result = GetUserResult.Parse(body);

            Assert.AreEqual("1,2", Recorded(result));
        }

        /// <summary> 따옴표로 감싼 숫자("1"·"0")도 기록으로 봄. </summary>
        [Test]
        public void 따옴표로_감싼_숫자도_기록으로_본다()
        {
            GetUserResult result = GetUserResult.Parse(WithValues("\"1\"", "\"0\"", "null", "null", "null"));

            Assert.AreEqual("1,2", Recorded(result));
        }

        /// <summary> 이 존에 없는 레벨 키(D0·D6·D10)는 무시해, 없는 레벨을 기록으로 세지 않음. </summary>
        [Test]
        public void 이_존에_없는_레벨_키는_무시한다()
        {
            string body = WithValues("1", "null", "null", "null", "null")
                .Replace(@"""D5"": null", @"""D5"": null, ""D6"": 1, ""D10"": 0, ""D0"": 1");

            GetUserResult result = GetUserResult.Parse(body);

            Assert.AreEqual("1", Recorded(result));
        }

        /// <summary> JSON 앞뒤에 붙은 줄바꿈·글자는 무시함 — 현장 서버는 JSON 끝 } 뒤에 ``` 줄을 붙여 보낸 적이 있음. </summary>
        [TestCase("\r\n", "\r\n```\r\n")]
        [TestCase("", "```")]
        [TestCase("\r\n\r\n", "\r\n<!-- debug -->\r\n")]
        public void JSON_앞뒤에_붙은_글자는_무시한다(string prefix, string suffix)
        {
            GetUserResult result = GetUserResult.Parse(prefix + WithValues("1", "0", "null", "null", "null") + suffix);

            Assert.IsTrue(result.IsFound);
            Assert.AreEqual("1,2", Recorded(result));
        }

        /// <summary> JSON으로 읽지 못하면 실패 사유에 JsonUtility 오류 문구를 함께 남김 — 로그만으로 응답이 어떻게 깨졌는지 짐작할 수 있게. </summary>
        [Test]
        public void JSON_해석_오류_문구를_실패_사유에_남긴다()
        {
            // 서버 수정 전 형식 — 기록 없는 값이 빈칸
            GetUserResult result = GetUserResult.Parse(AllNullBody.Replace(@"""D1"": null", @"""D1"": "));

            Assert.IsFalse(result.IsFound);
            StringAssert.StartsWith("JSON이 아닌 응답 (", result.FailReason);
            StringAssert.Contains("JSON parse error", result.FailReason);
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
            Assert.AreEqual("", Recorded(result));
        }
    }
}
