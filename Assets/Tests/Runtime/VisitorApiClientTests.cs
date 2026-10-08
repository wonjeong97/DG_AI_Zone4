using DGAIZone.Network;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary> 레벨 결과를 서버에 올릴 때 쓰는 이 존(4존)의 콘텐츠 코드와 저장 응답 판정을 검증함. </summary>
    public class VisitorApiClientTests
    {
        /// <summary> 레벨 번호(1부터)가 D1~D5로 바뀜 — 레벨1은 D1, 레벨5는 D5. </summary>
        [TestCase(1, "D1")]
        [TestCase(2, "D2")]
        [TestCase(3, "D3")]
        [TestCase(4, "D4")]
        [TestCase(5, "D5")]
        public void 레벨_번호는_4존_콘텐츠_코드로_바뀐다(int level, string expected)
        {
            Assert.AreEqual(expected, VisitorApiClient.GetLevelCode(level));
        }

        /// <summary> updateValue 응답의 result가 true면 저장 성공 — 응답 예시는 현장 서버 실측값. </summary>
        [TestCase("{\n    \"result\":true,\n    \"idx_user\":8,\n    \"code\":\"D1\",\n    \"value\":0\n}")]
        [TestCase("\r\n{\"result\":true,\"idx_user\":10,\"code\":\"D5\",\"value\":1}\r\n")]
        public void 결과_저장_응답의_result가_true면_성공이다(string body)
        {
            Assert.IsTrue(UpdateValueResponse.IsSaved(body));
        }

        /// <summary> result가 false이거나 JSON이 아닌 응답은 저장 실패로 봄. </summary>
        [TestCase("{\"result\":false,\"message\":\"ERROR_IDX_USER\"}")]
        [TestCase("{\"result\":false,\"message\":\"NOT_FOUND\"}")]
        [TestCase("{}")]
        [TestCase("OK")]
        [TestCase("<html><body>Error</body></html>")]
        [TestCase("")]
        [TestCase(null)]
        public void 결과_저장_실패_응답은_실패로_본다(string body)
        {
            Assert.IsFalse(UpdateValueResponse.IsSaved(body));
        }

        /// <summary> JSON 뒤에 붙은 글자(현장 서버 getUser에서 본 ``` 줄 등)는 무시하고 result로 판정함. </summary>
        [TestCase("\r\n{\"result\":true,\"idx_user\":10,\"code\":\"D1\",\"value\":1}\r\n```\r\n", true)]
        [TestCase("{\"result\":false,\"message\":\"ERROR_IDX_USER\"}\r\n```", false)]
        public void 결과_저장_응답_뒤에_붙은_글자는_무시한다(string body, bool expected)
        {
            Assert.AreEqual(expected, UpdateValueResponse.IsSaved(body));
        }
    }
}
