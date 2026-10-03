using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        private DesignBlockView _prefab;

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
            _prefab = prefab;
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

        /// <summary> content의 마지막 자식 블록 위치. 다시 Initialize하면 이전 블록은 프레임 끝에 파괴되므로 새 시작하기 블록은 마지막 자식임. </summary>
        private Vector2 LastBlockPosition() => BlockPosition(_content.childCount - 1);

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

        /// <summary>
        /// 붙는 연출을 시점별로 확인함. 실시간으로 기다리면 프레임이 한 번 길게 걸릴 때 연출이 통째로 끝나 버려 결과가 흔들리므로,
        /// 연출 시퀀스를 멈추고 원하는 시점으로 옮겨(Goto) 확인함.
        /// </summary>
        [Test]
        public void 명령_블록이_아래에서_올라와_붙은_뒤_값_블록이_오른쪽에서_미끄러져_와_소켓에_붙는다()
        {
            const float RiseHeight = 40f, RiseDuration = 0.5f, SlideDistance = 120f, SlideDuration = 0.3f;
            const float ValueAttachedX = 360f; // 명령 블록 몸통 오른쪽 끝(값 소켓 시작) 위치
            Vector2 target = new Vector2(10f, -20f);

            CreatePanel(DesignLayoutMode.FitAll);
            DesignBlockView block = Object.Instantiate(_prefab, _content);
            block.Setup(DesignBlockKind.Command, "추진체 종류", "고체 로켓");
            Sequence attach = block.PlayAttach(target, RiseHeight, RiseDuration, SlideDistance, SlideDuration);
            attach.Pause();
            RectTransform rect = (RectTransform)block.transform;

            attach.Goto(RiseDuration * 0.5f);
            Assert.Less(rect.anchoredPosition.y, target.y, "명령 블록은 목표보다 아래에서 올라오는 중이어야 함");
            Assert.AreEqual(0f, block.ValueAlpha, Tolerance, "명령 블록이 올라오는 동안 값 블록은 보이지 않아야 함");
            Assert.Greater(block.ValuePosition.x, ValueAttachedX, "값 블록은 소켓보다 오른쪽에서 기다려야 함");

            attach.Goto(RiseDuration + SlideDuration * 0.5f);
            Assert.AreEqual(target.y, rect.anchoredPosition.y, Tolerance, "값 블록이 움직일 때는 명령 블록이 이미 붙어 있어야 함");
            Assert.Greater(block.ValuePosition.x, ValueAttachedX, "값 블록은 오른쪽에서 왼쪽으로 오는 중이어야 함");
            Assert.Less(block.ValuePosition.x, ValueAttachedX + SlideDistance, "값 블록은 출발 위치보다 왼쪽으로 와 있어야 함");

            attach.Goto(RiseDuration + SlideDuration);
            Assert.AreEqual(ValueAttachedX, block.ValuePosition.x, Tolerance, "값 블록이 명령 블록 소켓에 붙어야 함");
            Assert.AreEqual(0f, block.ValuePosition.y, Tolerance, "값 블록 위쪽이 명령 블록 위쪽과 맞아야 함");
            Assert.AreEqual(1f, block.ValueAlpha, Tolerance, "붙은 뒤에는 값 블록이 보여야 함");
        }

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
        public void 블록_묶음은_단계_수나_값_블록_유무가_달라도_레벨_1과_같은_왼쪽에_놓인다()
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(3, true); // 레벨 1: 값 블록 있음, 3단계
            float level1X = LastBlockPosition().x;
            float level1Width = (StartTabCenterX - CommandSocketCenterX + 360f + 361f) * _panel.Scale; // 단계 블록 위치 + 명령 몸통 + 값 블록 폭
            Assert.AreEqual((ViewportWidth - level1Width) / 2f, level1X, Tolerance, "레벨 1은 블록 묶음이 보이는 영역 가운데에 놓여야 함(기준 위치)");

            _panel.Initialize(5, false); // 레벨 2: 값 블록 없음, 5단계
            Assert.AreEqual(level1X, LastBlockPosition().x, Tolerance, "값 블록이 없는 레벨도 레벨 1과 같은 왼쪽에 놓여야 함");

            _panel.Initialize(5, true); // 레벨 3·4: 값 블록 있음, 5단계라 배율이 더 작음
            Assert.AreEqual(level1X, LastBlockPosition().x, Tolerance, "단계가 많아 작아진 레벨도 레벨 1과 같은 왼쪽에 놓여야 함");
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
