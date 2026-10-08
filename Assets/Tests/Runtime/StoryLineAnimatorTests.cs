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
    }
}
