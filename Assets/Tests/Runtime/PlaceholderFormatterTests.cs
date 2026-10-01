using DGAIZone.App;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 스토리·미션 문구 자리표시자 치환(PlaceholderFormatter) 검증 테스트.
    /// </summary>
    public class PlaceholderFormatterTests
    {
        /// <summary> 숫자 자리표시자 뒤 조사({이름|이에요})는 어느 숫자 자리표시자에도 쓸 수 있고, 받침에 맞게 "이에요"/"예요"로 바뀌어야 함. </summary>
        [Test]
        public void 숫자_자리표시자마다_받침에_맞는_조사로_치환된다()
        {
            string distance = PlaceholderFormatter.ReplaceNumber("[{distance}]{distance|이에요}", Constants.MissionPlaceholders.Distance, 10);
            Assert.AreEqual("[10]이에요", distance);

            string electricity = PlaceholderFormatter.ReplaceNumber("{maxElectricity}{maxElectricity|이에요}", Constants.MissionPlaceholders.MaxElectricity, 5);
            Assert.AreEqual("5예요", electricity);

            string untouched = PlaceholderFormatter.ReplaceNumber("{minOxygen|이에요}", Constants.MissionPlaceholders.Distance, 3);
            Assert.AreEqual("{minOxygen|이에요}", untouched, "다른 이름의 자리표시자는 그대로 둬야 함");
        }

        /// <summary> {name}은 체험자 이름으로, 이름이 비어 있으면 기본 이름으로 치환되어야 함. </summary>
        [Test]
        public void 이름_자리표시자는_이름이_없으면_기본_이름으로_치환된다()
        {
            Assert.AreEqual("[홍길동]님", PlaceholderFormatter.ReplaceVisitorName("[{name}]님", "홍길동"));
            Assert.AreEqual($"[{Constants.DefaultVisitorName}]님", PlaceholderFormatter.ReplaceVisitorName("[{name}]님", ""));
            Assert.AreEqual(string.Empty, PlaceholderFormatter.ReplaceVisitorName(null, "홍길동"));
        }
    }
}
