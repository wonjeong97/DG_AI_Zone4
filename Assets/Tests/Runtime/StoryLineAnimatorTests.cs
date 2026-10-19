using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 스토리 줄 연출(StoryLineAnimator) 테스트. 줄과 줄 사이를 기다리는 동안 누른 넘기기도 받아 남은 줄을 바로 보여 주는지 확인함
    /// (예전에는 줄 사이를 Delay로 기다려 그동안 누른 넘기기가 사라짐 — 넘기기 입력을 따로 붙잡아 두지 않는 레벨 선택 화면에서 드러남).
    /// </summary>
    public class StoryLineAnimatorTests
    {
        private GameObject _canvasObject;
        private CancellationTokenSource _cts;

        [SetUp]
        public void SetUp()
        {
            _cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            // 시간 제한에 걸려 실패해도 연출이 파괴된 텍스트를 계속 건드리지 않게 먼저 멈춤
            _cts.Cancel();
            _cts.Dispose();
            if (_canvasObject) Object.DestroyImmediate(_canvasObject);
        }

        /// <summary> Canvas 아래에 여러 줄 텍스트를 만듦. TMP는 Canvas가 없으면 줄을 계산하지 않아 연출이 바로 끝나므로 줄 수부터 확인함. </summary>
        private TextMeshProUGUI CreateText(string content)
        {
            _canvasObject = new GameObject("TestStoryCanvas", typeof(Canvas));
            GameObject textObject = new GameObject("TestStoryText", typeof(RectTransform));
            textObject.transform.SetParent(_canvasObject.transform, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.ForceMeshUpdate();
            Assert.Greater(text.textInfo.lineCount, 1, "줄이 계산되지 않으면 연출이 바로 끝나 이 테스트가 아무것도 확인하지 못함");
            return text;
        }

        [UnityTest]
        public IEnumerator 줄_사이를_기다리는_동안_누른_넘기기도_받는다() => UniTask.ToCoroutine(async () =>
        {
            TextMeshProUGUI text = CreateText("A\nB\nC");

            // 첫 줄은 0.05초면 다 올라오고, 그 뒤 줄 사이에서 10초를 기다리는 동안 넘기기를 누름
            float start = Time.realtimeSinceStartup;
            bool SkipRequested() => Time.realtimeSinceStartup - start > 0.3f;

            await StoryLineAnimator.AnimateAsync(text, 0.05f, 10f, 30f, SkipRequested, _cts.Token)
                .AwaitWithRealtimeTimeout(3f);

            Assert.Less(Time.realtimeSinceStartup - start, 3f, "줄 사이 대기 중에 누른 넘기기를 받지 못해 10초를 다 기다림");
        });

        [UnityTest]
        public IEnumerator 줄_간격이_음수여도_예외_없이_끝난다() => UniTask.ToCoroutine(async () =>
        {
            TextMeshProUGUI text = CreateText("A\nB");

            await StoryLineAnimator.AnimateAsync(text, 0.01f, -1f, 30f, null, _cts.Token)
                .AwaitWithRealtimeTimeout(3f);
        });

        /// <summary> 연출 중인 text에서 character 글자(처음 나오는 것)의 정점 알파. 보이지 않는 글자면 실패. </summary>
        private static byte AlphaOf(TMP_Text text, char character)
        {
            TMP_TextInfo info = text.textInfo;
            for (int c = 0; c < info.characterCount; c++)
            {
                TMP_CharacterInfo ch = info.characterInfo[c];
                if (ch.character != character || !ch.isVisible) continue;
                return info.meshInfo[ch.materialReferenceIndex].colors32[ch.vertexIndex].a;
            }

            Assert.Fail($"'{character}' 글자를 찾지 못함");
            return 0;
        }

        [UnityTest]
        public IEnumerator 글_줄은_시작_시각이_되어야_올라온다() => UniTask.ToCoroutine(async () =>
        {
            // 빈 줄은 글 줄로 세지 않으므로 B가 두 번째 글 줄(시각 1초)
            TextMeshProUGUI text = CreateText("A\n\nB");

            UniTask animation = StoryLineAnimator.AnimateAsync(text, 0.05f, 0f, 30f, null, _cts.Token, null, new[] { 0f, 1f });
            await UniTask.Delay(400, ignoreTimeScale: true);

            Assert.AreEqual(255, AlphaOf(text, 'A'), "첫 글 줄은 0초에 올라와 있어야 함");
            Assert.AreEqual(0, AlphaOf(text, 'B'), "두 번째 글 줄은 1초가 되기 전에 보이면 안 됨");

            await animation.AwaitWithRealtimeTimeout(3f);
        });

        [UnityTest]
        public IEnumerator 자동_줄바꿈된_뒷줄은_같은_글_줄과_이어서_올라온다() => UniTask.ToCoroutine(async () =>
        {
            TextMeshProUGUI text = CreateText("AAAA XXXX\nC");
            ((RectTransform)text.transform).sizeDelta = new Vector2(text.GetPreferredValues("AAAA").x + 1f, 200f);
            text.ForceMeshUpdate();
            Assert.GreaterOrEqual(text.textInfo.lineCount, 3, "첫 글 줄이 자동 줄바꿈되지 않으면 이 테스트가 아무것도 확인하지 못함");

            UniTask animation = StoryLineAnimator.AnimateAsync(text, 0.05f, 0f, 30f, null, _cts.Token, null, new[] { 0f, 1.5f });
            await UniTask.Delay(500, ignoreTimeScale: true);

            Assert.AreEqual(255, AlphaOf(text, 'X'), "자동 줄바꿈된 뒷줄은 다음 글 줄 시각을 기다리지 않고 이어서 올라와야 함");
            Assert.AreEqual(0, AlphaOf(text, 'C'), "다음 글 줄은 그 시각이 되기 전에 보이면 안 됨");

            await animation.AwaitWithRealtimeTimeout(4f);
        });

        [UnityTest]
        public IEnumerator 글_줄_시각이_모자라면_나머지는_줄_간격으로_올라온다() => UniTask.ToCoroutine(async () =>
        {
            TextMeshProUGUI text = CreateText("A\nB\nC");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("글 줄 시작 시각이 1개뿐"));

            // A는 0초, 시각이 없는 B·C는 앞줄 뒤 줄 간격(1초)마다 — B는 약 1초, C는 약 2초에 올라옴(확인 시점 1.5초 양쪽에 0.5초 여유)
            UniTask animation = StoryLineAnimator.AnimateAsync(text, 0.01f, 1f, 30f, null, _cts.Token, null, new[] { 0f });
            await UniTask.Delay(1500, ignoreTimeScale: true);

            Assert.AreEqual(255, AlphaOf(text, 'B'), "시각이 없는 글 줄은 줄 간격 뒤에 올라와야 함");
            Assert.AreEqual(0, AlphaOf(text, 'C'), "그다음 글 줄은 줄 간격을 한 번 더 기다려야 함");

            await animation.AwaitWithRealtimeTimeout(4f);
        });
    }
}
