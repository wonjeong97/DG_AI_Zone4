using DGAIZone.Title;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 타이틀 QR 스캐너 입력 모음 검증 — 스캐너는 uid를 한 번에 빠르게 보내므로, 찍기 전에 눌린 키처럼
    /// 0.5초 넘게 떨어진 앞 글자는 uid에 붙지 않아야 함(실제 리더기 테스트에서 한 글자가 붙어 13자로 들어온 경우가 있었음).
    /// </summary>
    public class ScanInputBufferTests
    {
        /// <summary> 간격이 짧은 글자는 그대로 이어 붙음. </summary>
        [Test]
        public void 간격이_짧은_글자는_이어_붙는다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            float t = 10f;
            foreach (char c in "440930W1XWQH")
            {
                Assert.AreEqual(0, buffer.Append(c, t), "스캔 도중 글자가 버려지면 안 됨");
                t += 0.02f;
            }

            Assert.IsFalse(buffer.IsStale(t));
            Assert.AreEqual("440930W1XWQH", buffer.TakeAndClear());
            Assert.AreEqual(0, buffer.Length, "꺼낸 뒤에는 비어 있어야 함");
        }

        /// <summary> 0.5초 넘게 떨어진 글자가 오면 앞에 모은 글자를 버리고 버린 개수를 돌려줌. </summary>
        [Test]
        public void 간격이_0점5초를_넘으면_앞_글자를_버린다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            buffer.Append('a', 1f);

            float t = 1f + ScanInputBuffer.DefaultMaxCharGapSeconds + 0.1f;
            Assert.AreEqual(1, buffer.Append('4', t), "찍기 전에 눌린 키 한 글자를 버려야 함");
            foreach (char c in "40930W1XWQH")
            {
                t += 0.02f;
                buffer.Append(c, t);
            }

            Assert.AreEqual("440930W1XWQH", buffer.TakeAndClear());
        }

        /// <summary> 간격이 정확히 0.5초면 같은 스캔으로 봄(넘을 때만 버림). </summary>
        [Test]
        public void 간격이_정확히_0점5초면_버리지_않는다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            buffer.Append('A', 2f);

            Assert.AreEqual(0, buffer.Append('B', 2f + ScanInputBuffer.DefaultMaxCharGapSeconds));
            Assert.AreEqual("AB", buffer.TakeAndClear());
        }

        /// <summary> 마지막 글자보다 0.5초 넘게 지나면 모은 글자는 낡은 입력이고, 비어 있으면 낡지 않음. </summary>
        [Test]
        public void 마지막_글자_뒤로_0점5초가_지나면_낡은_입력이다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            Assert.IsFalse(buffer.IsStale(100f), "빈 버퍼는 낡은 입력이 아님");

            buffer.Append('x', 5f);
            Assert.IsFalse(buffer.IsStale(5f + ScanInputBuffer.DefaultMaxCharGapSeconds));
            Assert.IsTrue(buffer.IsStale(5f + ScanInputBuffer.DefaultMaxCharGapSeconds + 0.01f));

            buffer.Restart(6f);
            Assert.IsFalse(buffer.IsStale(100f), "다시 받기 시작한 뒤에는 낡은 입력이 아님");
        }

        /// <summary> 간격을 늘리면(0_Title.json scanCharGapSeconds — PC가 느린 현장) 기본값보다 늦게 온 글자도 같은 스캔으로 봄. </summary>
        [Test]
        public void 간격을_늘리면_늦게_온_글자도_이어_붙는다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer { MaxCharGapSeconds = 1.5f };
            buffer.Append('4', 0f);

            Assert.AreEqual(0, buffer.Append('4', 1.2f), "늘린 간격 안이면 버리면 안 됨");
            Assert.IsFalse(buffer.IsStale(2.7f));
            Assert.AreEqual(2, buffer.Append('0', 2.8f), "늘린 간격도 넘으면 버려야 함");
            Assert.AreEqual("0", buffer.TakeAndClear());
        }

        /// <summary> 다시 받기 시작한 직후 쉬지 않고 이어서 오는 글자는 받지 않던 동안 시작된 스캔의 뒷부분이라 모으지 않고 개수만 셈. </summary>
        [Test]
        public void 다시_받기_시작한_직후_이어서_오는_글자는_앞_스캔의_뒷부분으로_버린다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            buffer.Restart(10f);
            float t = 10f;
            foreach (char c in "XWQH")
            {
                t += 0.02f;
                Assert.AreEqual(0, buffer.Append(c, t));
            }

            Assert.AreEqual(0, buffer.Length, "잘린 uid가 되는 뒷부분은 모으지 않아야 함");
            Assert.AreEqual(4, buffer.TakeSkippedCount(), "버린 글자 수를 알려야 함");
            Assert.AreEqual(0, buffer.TakeSkippedCount(), "꺼낸 뒤에는 0이어야 함");

            t += ScanInputBuffer.DefaultMaxCharGapSeconds + 0.1f; // 쉬었다가 다시 찍음
            foreach (char c in "440930W1XWQH")
            {
                buffer.Append(c, t);
                t += 0.02f;
            }

            Assert.AreEqual("440930W1XWQH", buffer.TakeAndClear(), "쉬었다가 새로 찍은 uid는 그대로 모아야 함");
        }

        /// <summary> 버리는 동안에는 마지막으로 버린 글자부터 간격을 재므로, 간격 안으로 계속 이어지면 다시 받은 시각에서 오래 지나도 버림. </summary>
        [Test]
        public void 버리는_동안_간격_안으로_이어지는_글자는_계속_버린다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            buffer.Restart(0f);
            for (int i = 1; i <= 5; i++) buffer.Append('A', i * 0.3f); // 0.3초 간격, 마지막 글자는 다시 받은 뒤 1.5초

            Assert.AreEqual(0, buffer.Length, "간격 안으로 이어진 글자는 모두 버려야 함");
            Assert.AreEqual(5, buffer.TakeSkippedCount());
        }

        /// <summary> Enter(TakeAndClear)가 오면 잘린 스캔이 끝난 것이므로 바로 이어서 찍은 다음 스캔은 버리지 않음. </summary>
        [Test]
        public void Enter가_오면_버리기를_끝내고_바로_이어_찍은_스캔을_모은다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            buffer.Restart(0f);
            buffer.Append('Q', 0.1f);
            buffer.Append('H', 0.12f);
            Assert.AreEqual(string.Empty, buffer.TakeAndClear(), "잘린 뒷부분은 모으지 않아야 함");
            Assert.AreEqual(2, buffer.TakeSkippedCount());

            buffer.Append('4', 0.3f);
            buffer.Append('4', 0.32f);
            Assert.AreEqual("44", buffer.TakeAndClear(), "Enter 뒤에 바로 찍은 스캔은 그대로 모아야 함");
        }

        /// <summary> 간격을 늘린 현장(scanCharGapSeconds)에서는 버리는 기준도 늘린 간격을 씀. </summary>
        [Test]
        public void 간격을_늘리면_다시_받은_뒤_버리는_기준도_늘어난다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer { MaxCharGapSeconds = 1.5f };
            buffer.Restart(0f);
            buffer.Append('A', 1.2f);
            Assert.AreEqual(1, buffer.TakeSkippedCount(), "늘린 간격 안이면 버려야 함");

            buffer.Append('4', 2.8f);
            Assert.AreEqual("4", buffer.TakeAndClear(), "늘린 간격을 넘겨 쉬었다가 온 글자는 모아야 함");
        }

        /// <summary> 다시 받기 시작하면 모은 글자를 비우고, 쉬었다가 온 첫 글자부터 그대로 모음. </summary>
        [Test]
        public void 다시_받기_시작한_뒤_쉬었다가_온_글자는_그대로_모은다()
        {
            ScanInputBuffer buffer = new ScanInputBuffer();
            buffer.Append('Z', 1f);
            buffer.Restart(5f);
            Assert.AreEqual(0, buffer.Length, "다시 받으면 모은 글자를 비워야 함");

            Assert.AreEqual(0, buffer.Append('4', 5f + ScanInputBuffer.DefaultMaxCharGapSeconds + 0.1f));
            Assert.AreEqual("4", buffer.TakeAndClear());
            Assert.AreEqual(0, buffer.TakeSkippedCount(), "버린 글자가 없어야 함");
        }
    }
}
