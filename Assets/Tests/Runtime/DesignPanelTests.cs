using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.Game.UI;
using DGAIZone.Result;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
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
        private const float StackShiftLeft = 30f; // DesignPanel.stackShiftLeft 기본값(블록 묶음을 기준 위치에서 왼쪽으로 옮기는 거리)

        // 블록 이미지(Zone1 블록 아트)에서 잰 값(px). 코드의 상수를 그대로 쓰지 않고 따로 적어, 값이 바뀌면 테스트가 알려 주게 함
        private const float StartTabCenterX = 60f;
        private const float CommandSocketCenterX = 40.5f;
        private const float EndNotchCenterX = 60f;
        private const float StartBodyHeight = 100f;
        private const float CommandBodyHeight = 101f;
        private const float FlowSocketCenterX = 61f;     // ㄷ자 블록(If.png) 위 홈·아래 돌기
        private const float FlowInnerTabCenterX = 60.5f; // ㄷ자 블록 머리 아래 안쪽 돌기
        private const float FlowHeaderBodyHeight = 101f;
        private const float FlowFooterBodyHeight = 101f;
        private const float BottomTabHeight = 20f;

        // 레벨 3 단계 모양: 만약 전기량이 → (안쪽) 전기량 → 그리고 → 만약 산소량이 → (안쪽) 산소량
        private static readonly DesignStepShape[] Level3Shapes =
        {
            DesignStepShape.FlowControl, DesignStepShape.InsideFlowControl, DesignStepShape.Logic, DesignStepShape.FlowControl, DesignStepShape.InsideFlowControl
        };

        private GameObject _root;
        private RectTransform _content;
        private DesignPanel _panel;
        private DesignBlockView _prefab;
        private ScrollRect _scrollRect;

        /// <summary> 보이는 영역(Viewport)과 content, DesignPanel을 만들고 실제 블록 프리팹과 빈 리졸버를 넣음. withScrollRect면 3_Game처럼 세로 ScrollRect도 붙임. </summary>
        private void CreatePanel(DesignLayoutMode mode, bool withScrollRect = false)
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

            if (withScrollRect)
            {
                _scrollRect = _root.AddComponent<ScrollRect>();
                _scrollRect.viewport = viewport;
                _scrollRect.content = _content;
                _scrollRect.horizontal = false;
                _scrollRect.vertical = true;
                _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            }

            _panel = _root.AddComponent<DesignPanel>();
            _panel.SetUpForTest(_content, prefab, mode, _scrollRect);
            _panel.Construct(new ContainerBuilder().Build(), null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        /// <summary> 모두 명령 블록인 단계 모양 count개. </summary>
        private static List<DesignStepShape> Commands(int count)
        {
            List<DesignStepShape> shapes = new List<DesignStepShape>();
            for (int i = 0; i < count; i++) shapes.Add(DesignStepShape.Command);
            return shapes;
        }

        /// <summary> content의 index번째 자식 블록. </summary>
        private DesignBlockView BlockAt(int index) => _content.GetChild(index).GetComponent<DesignBlockView>();

        /// <summary> content의 index번째 자식 블록 위치(왼쪽 위 기준). </summary>
        private Vector2 BlockPosition(int index) => ((RectTransform)_content.GetChild(index)).anchoredPosition;

        /// <summary> content의 마지막 자식 블록 위치. 다시 Initialize하면 이전 블록은 프레임 끝에 파괴되므로 새 시작하기 블록은 마지막 자식임. </summary>
        private Vector2 LastBlockPosition() => BlockPosition(_content.childCount - 1);

        [UnityTest]
        public IEnumerator 블록은_앞_블록의_아래_돌기에_위_홈이_맞물리게_놓인다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(Commands(5), true);
            _panel.AddItem(DesignStepShape.Command, "추진체 종류", "고체 로켓");
            _panel.AddItem(DesignStepShape.Command, "엔진 점화하기", null);
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
            _panel.Initialize(Commands(5), true);
            for (int i = 0; i < 5; i++) _panel.AddItem(DesignStepShape.Command, "이동하기", "위쪽 한 칸");
            await _panel.AttachEndBlockAsync(default).AwaitWithRealtimeTimeout(5f);

            Assert.LessOrEqual(_content.sizeDelta.y, ViewportHeight + Tolerance, "블록 7개가 스크롤 없이 보이는 영역 높이 안에 들어가야 함");
            Assert.LessOrEqual(_panel.Scale, 0.8f + Tolerance, "블록 배율은 maxFitScale(0.8)을 넘지 않아야 함");
        });

        [UnityTest]
        public IEnumerator 크게_두고_자동_스크롤_방식은_정해진_배율을_쓰고_넘치면_영역보다_길어진다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.ScrollLarge);
            _panel.Initialize(Commands(5), true);
            for (int i = 0; i < 5; i++) _panel.AddItem(DesignStepShape.Command, "이동하기", "위쪽 한 칸");
            await _panel.AttachEndBlockAsync(default).AwaitWithRealtimeTimeout(5f);

            Assert.AreEqual(0.7f, _panel.Scale, Tolerance, "scrollScale(0.7) 배율을 그대로 써야 함");
            Assert.Greater(_content.sizeDelta.y, ViewportHeight, "블록 7개는 보이는 영역보다 길어 스크롤 범위가 생겨야 함");
        });

        [Test]
        public void 블록_묶음은_단계_수나_값_블록_유무가_달라도_레벨_1과_같은_왼쪽에_놓인다()
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(Commands(3), true); // 레벨 1: 값 블록 있음, 3단계
            float level1X = LastBlockPosition().x;
            float level1Width = (StartTabCenterX - CommandSocketCenterX + 360f + 361f) * _panel.Scale; // 단계 블록 위치 + 명령 몸통 + 값 블록 폭
            Assert.AreEqual((ViewportWidth - level1Width) / 2f - StackShiftLeft, level1X, Tolerance, "레벨 1은 블록 묶음이 보이는 영역 가운데에서 stackShiftLeft만큼 왼쪽에 놓여야 함(기준 위치)");

            _panel.Initialize(Commands(5), false); // 레벨 2: 값 블록 없음, 5단계
            Assert.AreEqual(level1X, LastBlockPosition().x, Tolerance, "값 블록이 없는 레벨도 레벨 1과 같은 왼쪽에 놓여야 함");

            _panel.Initialize(Commands(5), true); // 레벨 4: 값 블록 있음, 5단계라 배율이 더 작음
            Assert.AreEqual(level1X, LastBlockPosition().x, Tolerance, "단계가 많아 작아진 레벨도 레벨 1과 같은 왼쪽에 놓여야 함");

            _panel.Initialize(Level3Shapes, true); // 레벨 3: 만약 블록이 있어 더 길고 배율이 더 작음
            Assert.AreEqual(level1X, LastBlockPosition().x, Tolerance, "만약 블록이 있는 레벨도 레벨 1과 같은 왼쪽에 놓여야 함");
        }

        [UnityTest]
        public IEnumerator 취소하면_마지막_블록이_빠지고_연출이_끝나면_파괴된다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(Commands(3), true);
            _panel.AddItem(DesignStepShape.Command, "추진체 종류", "고체 로켓");
            _panel.AddItem(DesignStepShape.Command, "탑재 종류", "인공위성");

            _panel.RemoveLastItem();
            Assert.AreEqual(1, _panel.Count, "취소하면 단계 블록 수가 바로 줄어야 함");

            await UniTask.Delay(500, DelayType.UnscaledDeltaTime); // 빼기 연출(기본 0.2초) 뒤 파괴됨
            Assert.AreEqual(2, _content.childCount, "빼기 연출이 끝나면 시작하기 + 단계 1개만 남아야 함");
        });

        [UnityTest]
        public IEnumerator 레벨_3은_만약_블록_안쪽에_동작_블록이_맞물리고_논리_블록과_완성하기는_만약_블록_아래_돌기에_맞물린다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(Level3Shapes, true);
            _panel.AddItem(DesignStepShape.FlowControl, "만약 전기량이", "3 넘으면");
            _panel.AddItem(DesignStepShape.InsideFlowControl, "전기량", "낮추기");
            _panel.AddItem(DesignStepShape.Logic, "그리고", null);
            _panel.AddItem(DesignStepShape.FlowControl, "만약 산소량이", "3 낮으면");
            _panel.AddItem(DesignStepShape.InsideFlowControl, "산소량", "올리기");
            await _panel.AttachEndBlockAsync(default).AwaitWithRealtimeTimeout(5f);

            Assert.AreEqual(7, _content.childCount, "시작하기 + 단계 5 + 완성하기 블록이 있어야 함");
            Assert.AreEqual(DesignBlockKind.FlowControl, BlockAt(1).Kind, "조건 단계는 만약 블록이어야 함");
            Assert.AreEqual(DesignBlockKind.Command, BlockAt(2).Kind, "만약 안쪽 동작 단계는 명령 블록이어야 함");
            Assert.AreEqual(DesignBlockKind.Logic, BlockAt(3).Kind, "논리 연결어 단계는 논리 블록이어야 함");

            float s = _panel.Scale;
            float ifBodyHeight = FlowHeaderBodyHeight + CommandBodyHeight + FlowFooterBodyHeight; // 안쪽에 명령 블록 하나
            Vector2 start = BlockPosition(0), if1 = BlockPosition(1), inner1 = BlockPosition(2), logic = BlockPosition(3);
            Vector2 if2 = BlockPosition(4), inner2 = BlockPosition(5), end = BlockPosition(6);

            Assert.AreEqual(start.x + (StartTabCenterX - FlowSocketCenterX) * s, if1.x, Tolerance, "만약 블록의 위 홈이 시작하기 블록의 아래 돌기에 맞아야 함");
            Assert.AreEqual(StartBodyHeight * s, start.y - if1.y, Tolerance, "만약 블록은 시작하기 몸통 바로 아래에 놓여야 함");
            Assert.AreEqual(if1.x + (FlowInnerTabCenterX - CommandSocketCenterX) * s, inner1.x, Tolerance, "안쪽 블록의 위 홈이 만약 블록 머리 아래 안쪽 돌기에 맞아야 함");
            Assert.AreEqual(FlowHeaderBodyHeight * s, if1.y - inner1.y, Tolerance, "안쪽 블록은 만약 블록 머리 바로 아래에 놓여야 함");
            Assert.AreEqual(if1.x + (FlowSocketCenterX - CommandSocketCenterX) * s, logic.x, Tolerance, "논리 블록의 위 홈이 만약 블록의 아래 돌기에 맞아야 함");
            Assert.AreEqual(ifBodyHeight * s, if1.y - logic.y, Tolerance, "논리 블록은 만약 블록 아래 막대 바로 아래에 놓여야 함");
            Assert.AreEqual(logic.x + (CommandSocketCenterX - FlowSocketCenterX) * s, if2.x, Tolerance, "두 번째 만약 블록의 위 홈이 논리 블록의 아래 돌기에 맞아야 함");
            Assert.AreEqual(CommandBodyHeight * s, logic.y - if2.y, Tolerance, "두 번째 만약 블록은 논리 블록 몸통 바로 아래에 놓여야 함");
            Assert.AreEqual(inner1.x - if1.x, inner2.x - if2.x, Tolerance, "두 만약 블록의 안쪽 블록은 같은 자리에 놓여야 함");
            Assert.AreEqual(if2.x + (FlowSocketCenterX - EndNotchCenterX) * s, end.x, Tolerance, "완성하기 블록의 위 홈이 만약 블록의 아래 돌기에 맞아야 함");
            Assert.AreEqual(ifBodyHeight * s, if2.y - end.y, Tolerance, "완성하기 블록은 만약 블록 아래 막대 바로 아래에 놓여야 함");

            Assert.AreEqual(ifBodyHeight + BottomTabHeight, ((RectTransform)BlockAt(1).transform).sizeDelta.y, Tolerance, "만약 블록 이미지는 안쪽 블록 하나 높이로 늘어나야 함");
            Image ifBody = BlockAt(1).GetComponentInChildren<Image>();
            Assert.AreEqual(Image.Type.Sliced, ifBody.type, "만약 블록 이미지는 팔만 늘어나도록 9-slice여야 함");
            Assert.AreNotEqual(Vector4.zero, ifBody.sprite.border, "만약 블록 스프라이트에 9-slice 경계가 있어야 함");
            Assert.LessOrEqual(_content.sizeDelta.y, ViewportHeight + Tolerance, "레벨 3 블록 묶음 전체가 스크롤 없이 보이는 영역 높이 안에 들어가야 함");
        });

        [UnityTest]
        public IEnumerator 만약_블록은_안쪽_블록이_늘면_늘어나고_빼면_줄어든다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(new[] { DesignStepShape.FlowControl, DesignStepShape.InsideFlowControl, DesignStepShape.InsideFlowControl }, true);
            _panel.AddItem(DesignStepShape.FlowControl, "만약 전기량이", "3 넘으면");
            RectTransform ifRect = (RectTransform)BlockAt(1).transform;
            float oneSlot = FlowHeaderBodyHeight + CommandBodyHeight + FlowFooterBodyHeight + BottomTabHeight;
            Assert.AreEqual(oneSlot, ifRect.sizeDelta.y, Tolerance, "안쪽이 비어도 블록 하나 들어갈 자리를 비워 둬야 함");

            _panel.AddItem(DesignStepShape.InsideFlowControl, "전기량", "낮추기");
            _panel.AddItem(DesignStepShape.InsideFlowControl, "산소량", "올리기");
            Assert.AreEqual(oneSlot + CommandBodyHeight, ifRect.sizeDelta.y, Tolerance, "안쪽 블록이 둘이면 블록 하나 높이만큼 늘어나야 함");

            _panel.RemoveLastItem();
            Assert.AreEqual(oneSlot, ifRect.sizeDelta.y, Tolerance, "안쪽 블록을 빼면 다시 줄어들어야 함");

            await _panel.AttachEndBlockAsync(default).AwaitWithRealtimeTimeout(5f);
            float s = _panel.Scale;
            Assert.AreEqual((oneSlot - BottomTabHeight) * s, BlockPosition(1).y - LastBlockPosition().y, Tolerance, "완성하기 블록은 줄어든 만약 블록 바로 아래에 놓여야 함");
        });

        [UnityTest]
        public IEnumerator 위로_올려_둔_상태에서_완성하면_맨_아래로_내린_뒤_완성하기_블록을_붙인다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.ScrollLarge, true);
            _panel.Initialize(Commands(5), true);
            for (int i = 0; i < 5; i++) _panel.AddItem(DesignStepShape.Command, "이동하기", "위쪽 한 칸");
            await UniTask.Delay(1500, DelayType.UnscaledDeltaTime); // 쌓기(0.8초)·자동 스크롤(0.3초) 연출이 끝나길 기다림
            Assert.AreEqual(0f, _scrollRect.verticalNormalizedPosition, Tolerance, "블록이 쌓이면 맨 아래로 스크롤돼 있어야 함");

            _scrollRect.verticalNormalizedPosition = 1f; // 사용자가 드래그로 맨 위까지 올린 상태
            UniTask attach = _panel.AttachEndBlockAsync(default);
            Assert.IsFalse(_panel.IsCompleted, "맨 아래로 내려가는 동안에는 완성하기 블록이 아직 붙지 않아야 함");

            await attach.AwaitWithRealtimeTimeout(5f);
            Assert.IsTrue(_panel.IsCompleted, "맨 아래로 내린 뒤 완성하기 블록이 붙어야 함");
            Assert.AreEqual(0f, _scrollRect.verticalNormalizedPosition, Tolerance, "완성하기 블록까지 보이도록 맨 아래에 있어야 함");
        });

        [UnityTest]
        public IEnumerator 이미_맨_아래면_기다리지_않고_바로_완성하기_블록을_붙인다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.ScrollLarge, true);
            _panel.Initialize(Commands(5), true);
            for (int i = 0; i < 5; i++) _panel.AddItem(DesignStepShape.Command, "이동하기", "위쪽 한 칸");
            await UniTask.Delay(1500, DelayType.UnscaledDeltaTime); // 쌓기·자동 스크롤 연출이 끝나 맨 아래에 있음

            UniTask attach = _panel.AttachEndBlockAsync(default);
            Assert.IsTrue(_panel.IsCompleted, "이미 맨 아래면 스크롤을 기다리지 않고 바로 완성하기 블록이 붙어야 함");
            await attach.AwaitWithRealtimeTimeout(5f);
        });

        [UnityTest]
        public IEnumerator 레벨_4는_반복하기_ㄷ자_안쪽에_바로_뒤_이동하기가_들어가고_그다음_이동하기는_ㄷ자_아래에_붙는다() => UniTask.ToCoroutine(async () =>
        {
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(new[] { DesignStepShape.FlowControl, DesignStepShape.InsideFlowControl, DesignStepShape.Command }, true);
            _panel.AddItem(DesignStepShape.FlowControl, "반복하기", "3회");
            _panel.AddItem(DesignStepShape.InsideFlowControl, "이동하기", "오른쪽 한 칸");
            _panel.AddItem(DesignStepShape.Command, "이동하기", "위쪽 한 칸");
            await _panel.AttachEndBlockAsync(default).AwaitWithRealtimeTimeout(5f);

            Assert.AreEqual(DesignBlockKind.FlowControl, BlockAt(1).Kind, "반복하기는 ㄷ자 블록이어야 함");
            Assert.AreEqual(1f, BlockAt(1).ValueAlpha, Tolerance, "반복 횟수 값 블록이 ㄷ자 블록 머리에 붙어 있어야 함");

            float s = _panel.Scale;
            float flowBodyHeight = FlowHeaderBodyHeight + CommandBodyHeight + FlowFooterBodyHeight; // 안쪽에 이동하기 하나
            Vector2 repeat = BlockPosition(1), inner = BlockPosition(2), after = BlockPosition(3), end = BlockPosition(4);
            Assert.AreEqual(repeat.x + (FlowInnerTabCenterX - CommandSocketCenterX) * s, inner.x, Tolerance, "반복할 이동하기는 ㄷ자 블록 안쪽 돌기에 맞물려야 함");
            Assert.AreEqual(FlowHeaderBodyHeight * s, repeat.y - inner.y, Tolerance, "반복할 이동하기는 ㄷ자 블록 머리 바로 아래에 놓여야 함");
            Assert.AreEqual(repeat.x + (FlowSocketCenterX - CommandSocketCenterX) * s, after.x, Tolerance, "그다음 이동하기는 ㄷ자 블록 아래 돌기에 맞물려야 함");
            Assert.AreEqual(flowBodyHeight * s, repeat.y - after.y, Tolerance, "그다음 이동하기는 ㄷ자 블록 아래 막대 바로 아래에 놓여야 함");
            Assert.AreEqual(CommandBodyHeight * s, after.y - end.y, Tolerance, "완성하기는 마지막 이동하기 바로 아래에 놓여야 함");
        });

        [UnityTest]
        public IEnumerator 함수_사용_블록은_시작하기_아래에_붙고_함수_정의_블록은_오른쪽_위에_함께_놓이며_취소하면_함께_빠진다() => UniTask.ToCoroutine(async () =>
        {
            const float EdgePadding = 8f, FunctionDefWidth = 361f, DefinitionGap = 16f;
            CreatePanel(DesignLayoutMode.FitAll);
            _panel.Initialize(new[] { DesignStepShape.FunctionCall, DesignStepShape.Command, DesignStepShape.Command, DesignStepShape.Command, DesignStepShape.Logic }, false);
            _panel.AddItem(DesignStepShape.FunctionCall, "우주 도시 만들기", null);
            await UniTask.Delay(800, DelayType.UnscaledDeltaTime); // 붙는 연출(0.5초)이 끝나길 기다림

            Assert.AreEqual(3, _content.childCount, "시작하기 + 함수 사용 + 함수 정의 블록이 있어야 함");
            Assert.AreEqual(DesignBlockKind.Function, BlockAt(1).Kind, "시작하기 아래 줄에는 함수 사용 블록이 붙어야 함");
            Assert.AreEqual(DesignBlockKind.FunctionDef, BlockAt(2).Kind, "함수 정의 블록이 함께 놓여야 함");

            float s = _panel.Scale;
            Vector2 start = BlockPosition(0), call = BlockPosition(1), def = BlockPosition(2);
            Assert.AreEqual(start.x + (StartTabCenterX - CommandSocketCenterX) * s, call.x, Tolerance, "함수 사용 블록의 위 홈이 시작하기 블록의 아래 돌기에 맞아야 함");
            Assert.AreEqual(StartBodyHeight * s, start.y - call.y, Tolerance, "함수 사용 블록은 시작하기 몸통 바로 아래에 놓여야 함");
            Assert.AreEqual(ViewportWidth - EdgePadding - StackShiftLeft - FunctionDefWidth * s, def.x, Tolerance, "함수 정의 블록은 보이는 영역 오른쪽 끝에서 블록 묶음을 옮긴 만큼 왼쪽에 놓여야 함");
            Assert.AreEqual(start.y, def.y, Tolerance, "함수 정의 블록 위쪽은 시작하기 블록과 맞아야 함");
            Assert.LessOrEqual(call.x + 361f * s + DefinitionGap, def.x + Tolerance, "블록 줄과 함수 정의 블록이 겹치지 않아야 함");

            RectTransform defRect = (RectTransform)BlockAt(2).transform;
            Assert.AreEqual(FlowHeaderBodyHeight + CommandBodyHeight + FlowFooterBodyHeight, defRect.sizeDelta.y, Tolerance, "함수 정의 블록은 아래 돌기 없이 안쪽 블록 하나 자리만큼의 높이여야 함");
            Image defBody = BlockAt(2).GetComponentInChildren<Image>();
            Assert.AreEqual(Image.Type.Sliced, defBody.type, "함수 정의 블록 이미지는 팔만 늘어나도록 9-slice여야 함");
            Assert.AreNotEqual(Vector4.zero, defBody.sprite.border, "함수 정의 블록 스프라이트에 9-slice 경계가 있어야 함");

            _panel.RemoveLastItem();
            await UniTask.Delay(500, DelayType.UnscaledDeltaTime); // 빼기 연출(기본 0.2초) 뒤 파괴됨
            Assert.AreEqual(1, _content.childCount, "함수 단계를 취소하면 함수 사용·정의 블록이 함께 빠져 시작하기만 남아야 함");
        });

        [Test]
        public void 결과_설계창은_게임_설계창의_배치_방식으로_그린다()
        {
            DesignStep[] steps =
            {
                new DesignStep(DesignStepShape.Command, "이동하기", "위쪽 한 칸"),
                new DesignStep(DesignStepShape.Command, "이동하기", "오른쪽 한 칸")
            };

            CreatePanel(DesignLayoutMode.FitAll);
            ResultDesignPlayback.Prepare(_panel, steps, DesignLayoutMode.ScrollLarge);
            Assert.AreEqual(DesignLayoutMode.ScrollLarge, _panel.LayoutMode, "게임 설계창이 자동 스크롤이면 결과 설계창도 자동 스크롤이어야 함");
            Assert.AreEqual(0.7f, _panel.Scale, Tolerance, "자동 스크롤 방식의 배율(scrollScale 0.7)을 써야 함");

            ResultDesignPlayback.Prepare(_panel, steps, DesignLayoutMode.FitAll);
            Assert.AreEqual(DesignLayoutMode.FitAll, _panel.LayoutMode, "게임 설계창이 화면 맞추기면 결과 설계창도 화면 맞추기여야 함");
            Assert.AreEqual(0.8f, _panel.Scale, Tolerance, "블록이 적으면 화면 맞추기 최대 배율(maxFitScale 0.8)을 써야 함");
        }
    }
}
