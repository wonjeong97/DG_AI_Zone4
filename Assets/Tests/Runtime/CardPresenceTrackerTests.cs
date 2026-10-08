using DGAIZone.Game.Hardware;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 연속 읽기 모드 리더기의 카드 상태 판정 테스트. 같은 UID가 반복되면 한 번만 알리고, UID가 판정 시간 동안 오지 않으면
    /// 떨어짐으로 보며, 접속 때 이미 올려져 있던 카드는 떼었다 다시 올릴 때까지 알리지 않는지 확인함.
    /// </summary>
    public class CardPresenceTrackerTests
    {
        private const string CardA = "81736922500B04";
        private const string CardB = "8061AF3BA7F204";

        /// <summary> 접속(0ms) 뒤 판정 시간이 지나도록 카드가 없어 접속 때 올려진 카드를 가려내는 단계가 끝난 상태로 만듦. </summary>
        private static CardPresenceTracker CreateAfterBaselineWindow()
        {
            CardPresenceTracker tracker = new CardPresenceTracker(1000, 0);
            Assert.IsFalse(tracker.CheckRemoved(1000));
            return tracker;
        }

        [Test]
        public void 같은_UID가_반복되면_처음_한_번만_새_카드로_본다()
        {
            CardPresenceTracker tracker = CreateAfterBaselineWindow();

            Assert.AreEqual(CardPresenceTracker.CardFrameResult.NewCard, tracker.OnCardFrame(CardA, 2000));
            Assert.AreEqual(CardPresenceTracker.CardFrameResult.Repeat, tracker.OnCardFrame(CardA, 2200));
            Assert.AreEqual(CardPresenceTracker.CardFrameResult.Repeat, tracker.OnCardFrame(CardA, 2500));
            Assert.AreEqual(CardA, tracker.CurrentCard);
            Assert.AreEqual(300, tracker.MaxFrameGapMs, "올라가 있는 동안 UID 간격 중 가장 긴 값");
        }

        [Test]
        public void UID가_판정_시간_동안_오지_않으면_한_번만_떨어짐으로_본다()
        {
            CardPresenceTracker tracker = CreateAfterBaselineWindow();
            tracker.OnCardFrame(CardA, 2000);
            tracker.OnCardFrame(CardA, 2300);

            Assert.IsFalse(tracker.CheckRemoved(3299), "마지막 UID부터 판정 시간 직전까지는 떨어짐이 아님");
            Assert.IsTrue(tracker.CheckRemoved(3300), "마지막 UID부터 판정 시간이 지나면 떨어짐");
            Assert.IsNull(tracker.CurrentCard);
            Assert.IsFalse(tracker.CheckRemoved(5000), "떨어짐은 카드 하나에 한 번만 알림");
            Assert.AreEqual(CardPresenceTracker.CardFrameResult.NewCard, tracker.OnCardFrame(CardA, 5100), "떨어진 뒤 다시 올리면 새 카드");
        }

        [Test]
        public void 판정_시간_안에_떼었다_다시_올리면_떨어짐으로_보지_않는다()
        {
            CardPresenceTracker tracker = CreateAfterBaselineWindow();
            tracker.OnCardFrame(CardA, 2000);

            Assert.IsFalse(tracker.CheckRemoved(2800));
            Assert.AreEqual(CardPresenceTracker.CardFrameResult.Repeat, tracker.OnCardFrame(CardA, 2900), "판정 시간 안에 다시 오면 같은 카드가 계속 올라가 있는 것");
            Assert.AreEqual(900, tracker.MaxFrameGapMs, "비어 있던 시간도 UID 간격 최댓값에 들어가야 함");
            Assert.AreEqual(CardPresenceTracker.CardFrameResult.Repeat, tracker.OnCardFrame(CardA, 3000));
            Assert.AreEqual(900, tracker.MaxFrameGapMs, "간격이 짧아져도 최댓값은 유지함");
        }

        [Test]
        public void 다른_카드로_바로_바뀌면_새_카드로_보고_간격_최댓값을_비운다()
        {
            CardPresenceTracker tracker = CreateAfterBaselineWindow();
            tracker.OnCardFrame(CardA, 2000);
            tracker.OnCardFrame(CardA, 2300);

            Assert.AreEqual(CardPresenceTracker.CardFrameResult.NewCard, tracker.OnCardFrame(CardB, 2400));
            Assert.AreEqual(CardB, tracker.CurrentCard);
            Assert.AreEqual(0, tracker.MaxFrameGapMs, "새 카드는 UID 간격 최댓값을 처음부터 잼");
        }

        [Test]
        public void 접속_직후_판정_시간_안에_읽힌_카드는_떼었다_다시_올릴_때까지_알리지_않는다()
        {
            CardPresenceTracker tracker = new CardPresenceTracker(1000, 0);

            Assert.AreEqual(CardPresenceTracker.CardFrameResult.Baseline, tracker.OnCardFrame(CardA, 50));
            Assert.AreEqual(CardPresenceTracker.CardFrameResult.Repeat, tracker.OnCardFrame(CardA, 2000), "계속 올라가 있으면 판정 시간이 지나도 알리지 않음");
            Assert.IsTrue(tracker.CheckRemoved(3000), "접속 때 올려져 있던 카드도 떼면 떨어짐으로 봄");
            Assert.AreEqual(CardPresenceTracker.CardFrameResult.NewCard, tracker.OnCardFrame(CardA, 3500));
        }

        [Test]
        public void 접속_때_올려져_있던_카드_다음에_다른_카드가_오면_바로_새_카드로_본다()
        {
            CardPresenceTracker tracker = new CardPresenceTracker(1000, 0);

            Assert.AreEqual(CardPresenceTracker.CardFrameResult.Baseline, tracker.OnCardFrame(CardA, 50));
            Assert.AreEqual(CardPresenceTracker.CardFrameResult.NewCard, tracker.OnCardFrame(CardB, 200), "무시하는 것은 접속 때 올려져 있던 첫 카드뿐");
        }

        [Test]
        public void 접속_뒤_판정_시간_동안_카드가_없었으면_그_뒤_첫_카드는_새_카드로_본다()
        {
            CardPresenceTracker tracker = new CardPresenceTracker(1000, 0);

            Assert.IsFalse(tracker.CheckRemoved(999));
            Assert.IsFalse(tracker.CheckRemoved(1000));
            Assert.AreEqual(CardPresenceTracker.CardFrameResult.NewCard, tracker.OnCardFrame(CardA, 1010));
        }
    }
}
