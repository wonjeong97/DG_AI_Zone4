using DGAIZone.Game.Hardware;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 리더기 카드 떨어짐 판정 테스트. "카드 없음" 응답이 판정 시간 동안 이어져야 떨어짐으로 보고, 그 사이에 카드가 다시 읽히면
    /// 리더기 오응답으로 보고 처음부터 다시 세는지 확인함.
    /// </summary>
    public class CardRemovalDebouncerTests
    {
        [Test]
        public void 무카드_응답이_판정_시간보다_짧으면_떨어짐으로_보지_않고_카드가_다시_읽히면_처음부터_센다()
        {
            CardRemovalDebouncer removal = new CardRemovalDebouncer { DebounceMs = 1000 };

            Assert.IsFalse(removal.OnNoCard(0), "첫 무카드 응답만으로는 떨어짐이 아님");
            Assert.AreEqual(1, removal.OnCardRead(), "다시 읽히면 그동안 받은 무카드 응답 수를 돌려줘야 함(오응답 로그용)");
            Assert.IsFalse(removal.OnNoCard(5000), "다시 읽힌 뒤의 무카드 응답은 처음부터 다시 세야 함");
            Assert.IsFalse(removal.OnNoCard(5999), "판정 시간 직전까지는 떨어짐이 아님");
        }

        [Test]
        public void 무카드_응답이_판정_시간_동안_이어지면_떨어짐으로_보고_상태를_비운다()
        {
            CardRemovalDebouncer removal = new CardRemovalDebouncer { DebounceMs = 1000 };

            Assert.IsFalse(removal.OnNoCard(100));
            Assert.IsFalse(removal.OnNoCard(1099));
            Assert.IsTrue(removal.OnNoCard(1100), "첫 무카드 응답부터 판정 시간이 지나면 떨어짐");
            Assert.AreEqual(0, removal.NoCardReads, "떨어짐으로 확정하면 기다리던 상태를 비워야 함");
            Assert.AreEqual(0, removal.OnCardRead(), "떨어짐을 기다리지 않을 때 카드가 읽히면 0");
        }

        [Test]
        public void 판정_시간이_0이면_첫_무카드_응답에_바로_떨어짐으로_본다()
        {
            CardRemovalDebouncer removal = new CardRemovalDebouncer { DebounceMs = 0 };

            Assert.IsTrue(removal.OnNoCard(42));
        }
    }
}
