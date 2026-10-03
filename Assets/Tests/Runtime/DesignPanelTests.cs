using System.Collections;
using Cysharp.Threading.Tasks;
using DGAIZone.Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 설계창 블록 쌓기(DesignPanel) 검증 테스트. 블록이 앞 블록의 돌기에 홈이 맞물리는 위치에 놓이는지(블록 이미지에서 잰 값 기준),
    /// 배치 방식별 배율, 쌓기·빼기·완성하기 동작을 실제 DesignBlock 프리팹으로 확인함.
    /// </summary>
    public class DesignPanelTests
    {
        private const float ViewportWidth = 691f;  // 3_Game 설계창 표시 영역과 같은 크기
        private const float ViewportHeight = 420f;
        private const float Tolerance = 0.01f;

        // 블록 이미지(Zone1 블록 아트)에서 잰 값(px). 코드의 상수를 그대로 쓰지 않고 따로 적어, 값이 바뀌면 테스트가 알려 주게 함
        private const float StartTabCenterX = 60f;
        private const float CommandSocketCenterX = 40.5f;
        private const float EndNotchCenterX = 60f;
        private const float StartBodyHeight = 100f;
        private const float CommandBodyHeight = 101f;

        private GameObject _root;
        private RectTransform _content;
        private DesignPanel _panel;

        /// <summary> 보이는 영역(Viewport)과 content, DesignPanel을 만들고 실제 블록 프리팹과 빈 리졸버를 넣음. </summary>
        private void CreatePanel(DesignLayoutMode mode)
        {
#if UNITY_EDITOR
            DesignBlockView prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<DesignBlockView>("Assets/Prefabs/DesignBlock.prefab");
            Assert.IsNotNull(prefab, "Assets/Prefabs/DesignBlock.prefab을 찾지 못함");
#else
            DesignBlockView prefab = null;
            Assert.Ignore("DesignBlock 프리팹은 에디터에서만 경로로 읽을 수 있음");
#endif
            _root = new GameObject("TestDesignPanel", typeof(RectTransform));

            RectTransform viewport = (RectTransform)new GameObject("Viewport", typeof(RectTransform)).transform;
            viewport.SetParent(_root.transform, false);
            viewport.sizeDelta = new Vector2(ViewportWidth, ViewportHeight);

            _content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            _content.SetParent(viewport, false);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;

            _panel = _root.AddComponent<DesignPanel>();
            _panel.SetUpForTest(_content, prefab, mode);
            _panel.Construct(new ContainerBuilder().Build(), null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        /// <summary> content의 index번째 자식 블록 위치(왼쪽 위 기준). </summary>
        private Vector2 BlockPosition(int index) => ((RectTransform)_content.GetChild(index)).anchoredPosition;

        [UnityTest]
        public IEnumerator 블록은_앞_블록의_아래_돌기에_위_홈이_맞물리게_놓인다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(5, true);
            _panel.AddItem("추진체 종류", "고체 로켓");
            _panel.AddItem("엔진 점화하기", null);
            await _panel.AttachEndBlockAsync(default).AwaitWithRealtimeTimeout(5f); // 앞 블록들의 쌓기 연출도 이 사이에 끝남

            Assert.AreEqual(4, _content.childCount, "시작하기 + 단계 2 + 완성하기 블록이 있어야 함");
            float s = _panel.Scale;
            Vector2 start = BlockPosition(0), step1 = BlockPosition(1), step2 = BlockPosition(2), end = BlockPosition(3);

            Assert.AreEqual(start.x + (StartTabCenterX - CommandSocketCenterX) * s, step1.x, Tolerance, "첫 단계 블록의 위 홈이 시작하기 블록의 아래 돌기에 맞아야 함");
            Assert.AreEqual(step1.x, step2.x, Tolerance, "명령 블록끼리는 홈과 돌기 위치가 같아 같은 x에 놓여야 함");
            Assert.AreEqual(step2.x + (CommandSocketCenterX - EndNotchCenterX) * s, end.x, Tolerance, "완성하기 블록의 위 홈이 마지막 단계 블록의 아래 돌기에 맞아야 함");

            Assert.AreEqual(StartBodyHeight * s, start.y - step1.y, Tolerance, "첫 단계 블록은 시작하기 몸통 바로 아래에 놓여야 함");
            Assert.AreEqual(CommandBodyHeight * s, step1.y - step2.y, Tolerance, "단계 블록은 앞 블록 몸통 바로 아래에 놓여야 함");
            Assert.AreEqual(CommandBodyHeight * s, step2.y - end.y, Tolerance, "완성하기 블록은 마지막 단계 몸통 바로 아래에 놓여야 함");
            Assert.IsTrue(_panel.IsCompleted, "완성하기 블록이 붙은 상태여야 함");
        });

        [UnityTest]
        public IEnumerator 줄여서_한_화면에_방식은_최대_단계와_완성하기까지_보이는_영역에_들어간다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(5, true);
            for (int i = 0; i < 5; i++) _panel.AddItem("이동하기", "위쪽 한 칸");
            await _panel.AttachEndBlockAsync(default).AwaitWithRealtimeTimeout(5f);

            Assert.LessOrEqual(_content.sizeDelta.y, ViewportHeight + Tolerance, "블록 7개가 스크롤 없이 보이는 영역 높이 안에 들어가야 함");
            Assert.LessOrEqual(_panel.Scale, 0.8f + Tolerance, "블록 배율은 maxFitScale(0.8)을 넘지 않아야 함");
        });

        [UnityTest]
        public IEnumerator 크게_두고_자동_스크롤_방식은_정해진_배율을_쓰고_넘치면_영역보다_길어진다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.ScrollLarge);
            _panel.Initialize(5, true);
            for (int i = 0; i < 5; i++) _panel.AddItem("이동하기", "위쪽 한 칸");
            await _panel.AttachEndBlockAsync(default).AwaitWithRealtimeTimeout(5f);

            Assert.AreEqual(0.7f, _panel.Scale, Tolerance, "scrollScale(0.7) 배율을 그대로 써야 함");
            Assert.Greater(_content.sizeDelta.y, ViewportHeight, "블록 7개는 보이는 영역보다 길어 스크롤 범위가 생겨야 함");
        });

        [Test]
        public void 값_블록을_쓰지_않는_레벨은_명령_블록_폭으로_가운데에_놓인다()
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(5, false);

            float s = _panel.Scale;
            float stackWidth = (StartTabCenterX - CommandSocketCenterX + 361f) * s; // 단계 블록 위치 + 값 소켓 없는 명령 블록 폭
            Assert.AreEqual((ViewportWidth - stackWidth) / 2f, BlockPosition(0).x, Tolerance, "블록 묶음이 보이는 영역 가운데에 놓여야 함");
        }

        [UnityTest]
        public IEnumerator 취소하면_마지막_블록이_빠지고_연출이_끝나면_파괴된다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(3, true);
            _panel.AddItem("추진체 종류", "고체 로켓");
            _panel.AddItem("탑재 종류", "인공위성");

            _panel.RemoveLastItem();
            Assert.AreEqual(1, _panel.Count, "취소하면 단계 블록 수가 바로 줄어야 함");

            await UniTask.Delay(500, DelayType.UnscaledDeltaTime); // 빼기 연출(기본 0.2초) 뒤 파괴됨
            Assert.AreEqual(2, _content.childCount, "빼기 연출이 끝나면 시작하기 + 단계 1개만 남아야 함");
        });
    }
}
