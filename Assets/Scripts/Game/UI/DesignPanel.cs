using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary> 설계창 단계 블록의 모양. 레벨 상태가 단계(재료)마다 정함. </summary>
    public enum DesignStepShape
    {
        Command,  // 명령 블록(값이 없으면 값 소켓 없는 명령 블록)
        FlowControl,       // 만약·반복하기 ㄷ자 블록(값 블록은 머리 오른쪽). 바로 뒤따르는 InsideFlowControl 단계를 안쪽에 품음
        InsideFlowControl, // 앞 ㄷ자 블록 안쪽에 쌓이는 명령 블록(앞에 ㄷ자 블록이 없으면 Command처럼 바깥에 쌓임)
        Logic,    // 그리고·또는 논리 블록(초록, 값 블록 없음)
        FunctionCall // 함수 사용 블록(자주, 값 블록 없음). 붙일 때 설계창 오른쪽에 같은 이름의 함수 정의 ㄷ자 블록도 함께 놓고, 뒤따르는 단계는 모두 그 안쪽에 쌓임
    }

    /// <summary> 설계창에 쌓는 단계 블록 하나(모양·명령 블록 문구·값 블록 문구, 값이 없으면 null). 결과 씬에서 플레이어·정답 설계를 다시 그릴 때도 씀. </summary>
    public readonly struct DesignStep
    {
        public readonly DesignStepShape Shape;
        public readonly string Command;
        public readonly string Value;

        /// <summary> 단계 블록의 모양과 명령·값 블록 문구로 설계 단계 하나를 만듦. </summary>
        public DesignStep(DesignStepShape shape, string command, string value)
        {
            Shape = shape;
            Command = command;
            Value = value;
        }
    }

    /// <summary>
    /// 확정된 블록을 쌓아 보여 주는 설계창(Image_DesignWindow). 맨 위에 '시작하기' 블록을 두고, 설정하기로 확정할 때마다 단계 블록이
    /// 아래에서 올라와 맞물린 뒤 값 블록이 오른쪽에서 미끄러져 와 붙으며, 코딩 완료 시 맨 아래에 '완성하기' 블록이 붙음.
    /// ㄷ자(만약·반복하기) 블록 뒤의 InsideFlowControl 단계는 ㄷ자 블록 안쪽에 쌓이고, 함수 사용 단계는 설계창 오른쪽 위에 함수 정의 블록을 함께 놓으며
    /// 그 뒤 단계는 모두 함수 정의 블록 안쪽에 쌓임(완성하기는 시작하기 줄 맨 아래).
    /// 카드가 떨어진 단계부터 뒤쪽 블록을 임시로 떨어뜨렸다가 카드가 돌아오면 다시 붙이는 일도 맡음.
    /// 블록은 정해진 배율(scrollScale)로 두고, 설계창보다 길어지면 새로 쌓인 블록이 보이게 아래로 자동 스크롤함.
    /// 블록 모양과 문구는 호출하는 쪽(레벨 상태)이 정함.
    /// </summary>
    public class DesignPanel : MonoBehaviour
    {
        private const float ScrolledUpEpsilon = 0.5f; // 맨 아래에서 이만큼(UI 단위) 넘게 올라가 있으면 사용자가 올려 둔 것으로 봄
        private const string StartLabel = "시작하기";
        private const string EndLabel = "완성하기";
        private const float DefinitionGap = 16f; // 블록 묶음 오른쪽 끝과 함수 정의 블록 사이 최소 간격(UI 단위)

        [SerializeField] private ScrollRect scrollRect;      // DesignScrollView
        [SerializeField] private RectTransform content;      // DesignScrollView/Viewport/DesignContainer(ScrollRect의 content)
        [SerializeField] private DesignBlockView blockPrefab; // DesignBlock 프리팹

        [Header("Layout")]
        [SerializeField] private float scrollScale = 0.7f; // 블록 배율(설계창보다 길어지면 아래로 자동 스크롤함)
        [SerializeField] private float edgePadding = 8f;   // 블록 묶음 위아래·좌우 최소 여백(UI 단위)
        [SerializeField] private float stackShiftLeft = 30f; // 블록 묶음(레벨 5는 함수 정의 블록도)을 기준 위치에서 왼쪽으로 옮기는 거리(UI 단위). 왼쪽 여백(edgePadding)을 넘어가지는 않음

        [Header("Animation")]
        [SerializeField] private float riseDuration = 0.5f;         // 블록이 아래에서 올라와 맞물리는 시간(스토리 라인 연출과 같은 곡선)
        [SerializeField] private float riseHeight = 40f;            // 블록이 올라오기 시작하는 깊이(블록 원본 크기 기준, 배율을 곱해 화면상 약 20px)
        [SerializeField] private float valueSlideDuration = 0.3f;   // 명령 블록이 붙은 뒤 값 블록이 오른쪽에서 미끄러져 와 붙는 시간
        [SerializeField] private float valueSlideDistance = 120f;   // 값 블록이 미끄러지기 시작하는 오른쪽 거리(블록 원본 크기 기준)
        [SerializeField] private float removeDuration = 0.2f;       // 취소하거나 카드가 떨어졌을 때 블록이 가라앉으며 사라지는 시간
        [SerializeField] private float restoreRiseDuration = 1f;          // 떨어졌던 카드가 돌아와 블록이 다시 올라와 맞물리는 시간(붙일 때보다 천천히)
        [SerializeField] private float restoreValueSlideDuration = 0.6f;  // 다시 붙을 때 값 블록이 오른쪽에서 미끄러져 와 붙는 시간
        [SerializeField] private float scrollDuration = 0.3f;       // 자동 스크롤 시간
        [SerializeField] private float completeHoldDuration = 0.5f; // 완성하기 블록이 붙은 뒤 다음 연출까지 보여 주는 시간

        private readonly List<DesignBlockView> _steps = new List<DesignBlockView>();
        private readonly List<DesignStepShape> _stepShapes = new List<DesignStepShape>(); // _steps와 같은 순서의 단계 모양
        private readonly List<Vector2> _positions = new List<Vector2>(); // 블록별 위치(px, 0 = 시작하기). Layout이 채움
        private readonly List<float> _flowInnerHeights = new List<float>(); // 단계별 ㄷ자 블록 안쪽 높이(px, ㄷ자가 아니면 0). Layout이 채움
        private readonly List<DesignStepShape> _visibleShapes = new List<DesignStepShape>(); // 떨어뜨리지 않은 앞쪽 단계 모양(보이는 블록만으로 높이를 셀 때 씀)
        private readonly List<Vector2> _visiblePositions = new List<Vector2>();
        private readonly List<float> _visibleFlowInnerHeights = new List<float>();
        private DesignBlockView _startBlock;
        private DesignBlockView _endBlock;
        private DesignBlockView _functionDef; // 설계창 오른쪽에 놓인 함수 정의 블록(함수 사용 단계를 붙였을 때만)
        private int _functionDefStep = -1;    // 함수 정의 블록을 함께 놓은 함수 사용 단계 번호
        private bool _plansFunctionDef;       // 이 레벨에 함수 사용 단계가 있어 오른쪽에 함수 정의 블록 자리를 남겨야 하는지
        private int _droppedFrom = int.MaxValue; // 이 번호의 단계 블록부터 끝까지 임시로 떨어뜨린 상태(int.MaxValue = 떨어뜨린 블록 없음)
        private float _stackHeight;   // 지금 놓인 블록 묶음의 높이(px, 마지막 블록의 아래 돌기 포함, 함수 정의 블록이 더 길면 그 높이)
        private bool _withValueBlocks = true;
        private float _scale = 1f;
        private float _offsetX;
        private float _shiftX; // 기준 위치에서 실제로 왼쪽으로 옮긴 거리(stackShiftLeft를 왼쪽 여백에 맞춰 줄인 값)
        private Tween _scrollTween;
        private bool _scrollTweenShrinks; // 진행 중인 스크롤 연출이 범위 줄이기(ShrinkContentToStack)인지 — 사용자가 끌기 시작해 끊으면 손을 뗀 뒤 다시 함
        private ScrollDragTracker _dragTracker; // 사용자가 설계창을 끄는 동안에는 자동 스크롤을 미루고 손을 떼면 처리함
        private PendingScroll _pendingScroll;
        private IObjectResolver _resolver;
        private ILogger<DesignPanel> _logger;

        /// <summary> 사용자가 끄는 동안 미뤄 둔 스크롤 연출. 마지막으로 요청한 것 하나만 손을 뗄 때 처리함. </summary>
        private enum PendingScroll
        {
            None,
            ToBottom, // 새로 붙은 블록이 보이게 맨 아래로(ScrollToBottom)
            Shrink    // 짧아진 묶음에 맞춰 올린 뒤 범위 줄이기(ShrinkContentToStack)
        }

        /// <summary> 사용자가 지금 설계창을 손가락으로 끄는 중인지 여부. </summary>
        private bool IsUserDragging => _dragTracker && _dragTracker.IsDragging;

        /// <summary> 지금 설계창에 쌓인 단계 블록 수(시작하기·완성하기 제외). </summary>
        public int Count => _steps.Count;

        /// <summary> 완성하기 블록이 붙었는지 여부. </summary>
        public bool IsCompleted => _endBlock;

        /// <summary> 블록 배율(레이아웃 검증용). </summary>
        internal float Scale => _scale;

        /// <summary> 설정하기로 단계 블록 하나가 다 붙기까지 걸리는 시간(초). 값 블록을 쓰는 레벨이면 값 블록이 미끄러져 붙는 시간까지. </summary>
        internal float AttachDuration => riseDuration + (_withValueBlocks ? valueSlideDuration : 0f);

        /// <summary> 떨어졌던 카드가 돌아와 블록이 다시 다 붙기까지 걸리는 시간(초). 값 블록을 쓰는 레벨이면 값 블록이 미끄러져 붙는 시간까지. </summary>
        internal float RestoreDuration => restoreRiseDuration + (_withValueBlocks ? restoreValueSlideDuration : 0f);

        /// <summary> 테스트 전용: 인스펙터로 연결하는 블록 content·프리팹과 ScrollRect를 넣음(scroll이 없으면 content의 부모를 보이는 영역으로 쓰고 자동 스크롤은 하지 않음). </summary>
        internal void SetUpForTest(RectTransform contentRoot, DesignBlockView prefab, ScrollRect scroll = null)
        {
            scrollRect = scroll;
            content = contentRoot;
            blockPrefab = prefab;
            AttachDragTracker();
        }

        /// <summary> 인스펙터로 연결한 ScrollRect에 끌기 추적을 붙임. </summary>
        private void Awake()
        {
            AttachDragTracker();
        }

        /// <summary>
        /// ScrollRect 오브젝트에 ScrollDragTracker를 붙이고(없으면 추가) 끌기 시작·끝을 구독함. scrollRect가 없으면 자동 스크롤도 하지 않으므로
        /// 붙이지 않음(자동 스크롤 때 ScrollToBottom이 경고함).
        /// </summary>
        private void AttachDragTracker()
        {
            if (_dragTracker || !scrollRect) return;

            if (!scrollRect.TryGetComponent(out _dragTracker)) _dragTracker = scrollRect.gameObject.AddComponent<ScrollDragTracker>();
            _dragTracker.DragStarted += OnUserDragStarted;
            _dragTracker.DragEnded += OnUserDragEnded;
        }

        /// <summary> 사용자가 끌기 시작하면 진행 중인 자동 스크롤을 멈추고(손가락이 이김) 손을 뗀 뒤 다시 하도록 기억함. </summary>
        private void OnUserDragStarted()
        {
            if (_scrollTween == null || !_scrollTween.IsActive()) return; // 진행 중인 자동 스크롤이 없음(정상)

            _scrollTween.Kill();
            _scrollTween = null;
            _pendingScroll = _scrollTweenShrinks ? PendingScroll.Shrink : PendingScroll.ToBottom;
        }

        /// <summary> 손을 떼면 끄는 동안 미뤄 둔 자동 스크롤을 처리함. </summary>
        private void OnUserDragEnded()
        {
            PendingScroll pending = _pendingScroll;
            _pendingScroll = PendingScroll.None;
            if (pending == PendingScroll.ToBottom)
            {
                UpdateContentHeight(); // 끄는 동안 미뤄 둔 범위 줄이기가 있었으면 함께 맞춤
                ScrollToBottom();
            }
            else if (pending == PendingScroll.Shrink)
            {
                ShrinkContentToStack();
            }
        }

        /// <summary> VContainer 의존성 주입. 블록 생성용 리졸버와 로거를 할당함. </summary>
        [Inject]
        public void Construct(IObjectResolver resolver, ILogger<DesignPanel> logger)
        {
            _resolver = resolver;
            _logger = logger;
        }

        /// <summary>
        /// 설계창을 비우고 맨 위에 시작하기 블록만 놓음. withValueBlocks(값 블록을 쓰는 레벨인지)로 블록 묶음이 화면 폭을 넘지 않게 할 폭을 정함.
        /// 묶음 왼쪽 끝은 레벨과 관계없이 같음. withFunctionDefinition(함수 사용 단계가 있는 레벨인지)이면 오른쪽에 함수 정의 블록이 들어갈 폭도 남김.
        /// </summary>
        public void Initialize(bool withValueBlocks, bool withFunctionDefinition)
        {
            DestroyAll();
            _withValueBlocks = withValueBlocks;
            _plansFunctionDef = withFunctionDefinition;
            UpdateScale();

            _startBlock = CreateBlock(DesignBlockKind.Start, StartLabel, null);
            Relayout();
            if (_startBlock) _startBlock.SnapTo(PositionOf(0)); // 만들지 못하면 CreateBlock이 경고함
            UpdateContentHeight();
        }

        /// <summary>
        /// 단계 블록 하나를 맨 아래(InsideFlowControl이면 앞 ㄷ자 블록 안쪽 맨 아래, 함수 사용 단계 뒤면 함수 정의 블록 안쪽 맨 아래)에 쌓음
        /// (아래에서 올라와 맞물린 뒤 값 블록이 오른쪽에서 붙는 연출).
        /// 명령·ㄷ자 블록은 value가 있으면 값 블록을 붙이며, 명령 블록은 value가 비어 있으면 값 소켓 없는 모양을 씀.
        /// </summary>
        public void AddItem(DesignStepShape shape, string command, string value)
        {
            if (!_startBlock)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] Initialize 전이라 블록을 쌓을 수 없음.");
                return;
            }

            if (shape == DesignStepShape.InsideFlowControl && !HasOpenFlowControl() && _logger != null)
            {
                _logger.ZLogWarning($"[DesignPanel] '{command}' 블록은 ㄷ자 블록 안쪽에 들어가야 하는데 앞에 ㄷ자 블록이 없어 바깥에 쌓음.");
            }

            DesignBlockView block = CreateBlock(KindOf(shape, value), command, value);
            if (!block) return; // 만들지 못하면 CreateBlock이 경고함

            _steps.Add(block);
            _stepShapes.Add(shape);
            Relayout();
            PlayAttach(block, _steps.Count);
            if (shape == DesignStepShape.FunctionCall) AttachFunctionDefinition(command, _steps.Count - 1);
            UpdateContentHeight();
            ScrollToBottom();
        }

        /// <summary> 설계창 오른쪽 위에 함수 정의 블록(이름은 함수 사용 블록과 같음)을 붙임. 이미 놓여 있으면 경고만 남기고 하나만 둠. </summary>
        private void AttachFunctionDefinition(string label, int stepIndex)
        {
            if (_functionDef)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] 함수 정의 블록이 이미 있어 '{label}' 함수 정의 블록을 더 놓지 않음.");
                return;
            }

            _functionDef = CreateBlock(DesignBlockKind.FunctionDef, label, null);
            if (!_functionDef) return;

            _functionDefStep = stepIndex;
            _functionDef.PlayAttach(FunctionDefinitionPosition(), riseHeight * _scale, riseDuration, valueSlideDistance, valueSlideDuration);
        }

        /// <summary>
        /// 함수 정의 블록의 content 안 위치(왼쪽 위 기준): 안쪽 블록까지 합친 폭을 보이는 영역 오른쪽 끝에 붙인 자리에서 블록 묶음을 옮긴 만큼
        /// 왼쪽으로 옮기고, 위쪽은 시작하기 블록과 맞춤.
        /// </summary>
        private Vector2 FunctionDefinitionPosition()
        {
            return new Vector2(ViewportRect().width - edgePadding - _shiftX - FunctionColumnWidth(_withValueBlocks) * _scale, -edgePadding);
        }

        /// <summary> 마지막으로 쌓은 단계 블록을 뺌(가라앉으며 사라지는 연출 뒤 파괴). </summary>
        public void RemoveLastItem()
        {
            if (_steps.Count == 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] 쌓인 단계 블록이 없어 뺄 블록이 없음.");
                return;
            }

            int lastIndex = _steps.Count - 1;
            DesignBlockView last = _steps[lastIndex];
            _steps.RemoveAt(lastIndex);
            _stepShapes.RemoveAt(lastIndex);
            if (last) last.PlayDetachAndDestroy(riseHeight * _scale, removeDuration);
            if (lastIndex == _functionDefStep)
            {
                if (_functionDef) _functionDef.PlayDetachAndDestroy(riseHeight * _scale, removeDuration);
                _functionDef = null;
                _functionDefStep = -1;
            }
            Relayout();
            ShrinkContentToStack(); // 범위를 바로 줄이면 맨 아래로 스크롤된 상태에서 남은 블록이 한 프레임에 튐(떨어뜨릴 때와 같은 처리)
        }

        /// <summary>
        /// 맨 아래에 완성하기 블록을 붙이고, 맞물리는 연출과 잠깐의 대기가 끝날 때까지 기다림. 사용자가 드래그로 위로 올려 둔 상태면 먼저 맨 아래로
        /// 부드럽게 내린 뒤 붙임(아직 손가락으로 끄는 중이면 기다리지 않고 붙이고, 맨 아래로는 손을 뗀 뒤 내림). 이미 붙어 있으면 바로 끝남.
        /// </summary>
        public async UniTask AttachEndBlockAsync(CancellationToken token)
        {
            if (_endBlock) return;
            if (!_startBlock)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] Initialize 전이라 완성하기 블록을 붙일 수 없음.");
                return;
            }

            if (IsScrolledUp())
            {
                Tween scrollBack = ScrollToBottom();
                if (scrollBack != null) await scrollBack.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
            }

            _endBlock = CreateBlock(DesignBlockKind.End, EndLabel, null);
            if (!_endBlock) return;

            Relayout();
            Sequence attach = PlayAttach(_endBlock, _steps.Count + 1);
            UpdateContentHeight();
            _ = ScrollToBottom(); // 붙는 연출과 함께 스크롤하며 기다리지 않음

            await attach.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
            await UniTask.Delay(TimeSpan.FromSeconds(completeHoldDuration), DelayType.UnscaledDeltaTime, cancellationToken: token);
        }

        /// <summary>
        /// fromIndex번째 단계 블록부터 끝까지 임시로 떨어뜨리고(가라앉으며 사라지되 블록은 남겨 둠), 앞서 떨어뜨렸다가 이번에 fromIndex 앞이 된 블록은
        /// 천천히 다시 붙임. fromIndex가 블록 수 이상이면 떨어뜨린 블록을 모두 다시 붙임. 함수 정의 블록은 함께 놓인 함수 사용 단계를 따름.
        /// </summary>
        public void DropFrom(int fromIndex)
        {
            int previous = _droppedFrom;
            _droppedFrom = fromIndex;
            if (fromIndex == previous) return;

            Relayout(); // 묶음 높이와 ㄷ자·함수 정의 블록 높이를 보이는 블록에 맞춤
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i]) SetDropped(_steps[i], i >= previous, i >= fromIndex, PositionOf(i + 1));
            }
            if (_functionDef) SetDropped(_functionDef, _functionDefStep >= previous, _functionDefStep >= fromIndex, FunctionDefinitionPosition());

            if (fromIndex < previous)
            {
                ShrinkContentToStack(); // 더 앞에서부터 떨어뜨림: 남은 블록의 맨 아래가 보이게 올린 뒤 스크롤 범위를 줄임
            }
            else
            {
                UpdateContentHeight(); // 다시 붙음: 블록을 쌓을 때처럼 스크롤 범위를 늘리고 다시 붙는 블록이 보이게 내림
                ScrollToBottom();
            }
        }

        /// <summary> 블록의 떨어뜨림 상태가 바뀌었을 때만 떨어뜨리는 연출이나 천천히 다시 붙는 연출을 시작함. </summary>
        private void SetDropped(DesignBlockView block, bool wasDropped, bool dropped, Vector2 attachedPosition)
        {
            if (wasDropped == dropped) return;

            if (dropped) block.PlayDrop(riseHeight * _scale, removeDuration);
            else block.PlayAttach(attachedPosition, riseHeight * _scale, restoreRiseDuration, valueSlideDistance, restoreValueSlideDuration);
        }

        /// <summary> index번째 자리(0 = 시작하기)에 블록이 붙는 연출을 시작함. 올라오는 깊이는 content 좌표라 배율을 곱하고, 값 블록 거리는 블록 안 좌표라 그대로 넘김. </summary>
        private Sequence PlayAttach(DesignBlockView block, int index)
        {
            return block.PlayAttach(PositionOf(index), riseHeight * _scale, riseDuration, valueSlideDistance, valueSlideDuration);
        }

        /// <summary> 블록 프리팹을 content 아래에 만들고 종류·문구를 적용함. 만들 수 없으면 원인을 로그로 남기고 null을 반환함. </summary>
        private DesignBlockView CreateBlock(DesignBlockKind kind, string label, string value)
        {
            if (!content || !blockPrefab)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] content 또는 blockPrefab이 null이라 {kind} 블록을 만들 수 없음.");
                return null;
            }

            if (_resolver == null)
            {
                if (_logger != null) _logger.ZLogError($"[DesignPanel] IObjectResolver가 주입되지 않아 {kind} 블록을 만들 수 없음.");
                return null;
            }

            DesignBlockView block = _resolver.Instantiate(blockPrefab, content);
            block.transform.localScale = new Vector3(_scale, _scale, 1f);
            block.Setup(kind, label, value);
            return block;
        }

        /// <summary> 위에서 index번째 블록(0 = 시작하기)의 content 안 위치(왼쪽 위 기준). Layout이 계산한 위치에 배율과 여백을 적용함. </summary>
        private Vector2 PositionOf(int index)
        {
            Vector2 position = _positions[index];
            return new Vector2(_offsetX + position.x * _scale, -(edgePadding + position.y * _scale));
        }

        /// <summary> 단계 모양과 값으로 블록 종류를 정함. 명령 블록은 값이 비어 있으면 값 소켓 없는 모양을 씀. </summary>
        private static DesignBlockKind KindOf(DesignStepShape shape, string value)
        {
            switch (shape)
            {
                case DesignStepShape.FlowControl: return DesignBlockKind.FlowControl;
                case DesignStepShape.Logic: return DesignBlockKind.Logic;
                case DesignStepShape.FunctionCall: return DesignBlockKind.Function;
                default: return string.IsNullOrEmpty(value) ? DesignBlockKind.CommandNoValue : DesignBlockKind.Command;
            }
        }

        /// <summary> 다음 InsideFlowControl 단계가 들어갈 ㄷ자 블록이 있는지(맨 아래 바깥 블록이 ㄷ자 블록인지) 여부. </summary>
        private bool HasOpenFlowControl()
        {
            for (int i = _stepShapes.Count - 1; i >= 0; i--)
            {
                if (_stepShapes[i] != DesignStepShape.InsideFlowControl) return _stepShapes[i] == DesignStepShape.FlowControl;
            }
            return false;
        }

        /// <summary>
        /// 지금 놓인 블록으로 위치와 묶음 높이를 다시 계산하고, ㄷ자 블록·함수 정의 블록 높이를 안쪽 블록에 맞춤. 떨어뜨린 블록이 있으면 위치(다시 붙을 자리)는
        /// 모든 블록 기준으로 두고, 묶음 높이(스크롤 범위)와 ㄷ자·함수 정의 블록 높이는 보이는 블록만으로 정함(떨어뜨린 블록이 잠시 빠진 것처럼 보이게).
        /// </summary>
        private void Relayout()
        {
            bool withEnd = _endBlock;
            float functionDefX = (FunctionDefinitionPosition().x - _offsetX) / _scale; // 함수 정의 블록 왼쪽 끝(px, Layout 좌표)
            _stackHeight = Layout(_stepShapes, withEnd, functionDefX, _positions, _flowInnerHeights, out float functionInnerHeight);

            if (_droppedFrom < _stepShapes.Count)
            {
                _visibleShapes.Clear();
                for (int i = 0; i < _droppedFrom; i++) _visibleShapes.Add(_stepShapes[i]);
                _stackHeight = Layout(_visibleShapes, withEnd, functionDefX, _visiblePositions, _visibleFlowInnerHeights, out float visibleFunctionInnerHeight);
                for (int i = 0; i < _visibleFlowInnerHeights.Count; i++) _flowInnerHeights[i] = _visibleFlowInnerHeights[i];
                if (_functionDefStep < _droppedFrom) functionInnerHeight = visibleFunctionInnerHeight;
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i] && _steps[i].Kind == DesignBlockKind.FlowControl) _steps[i].SetFlowInnerHeight(_flowInnerHeights[i]);
            }
            if (_functionDef) _functionDef.SetFlowInnerHeight(functionInnerHeight);
        }

        /// <summary>
        /// 블록 묶음을 위에서부터 따라가며 블록마다 위치(px, 블록 원본 크기·시작하기 왼쪽 위 기준)를 positions에 채우고, 묶음 높이(마지막 블록의
        /// 아래 돌기 포함, 함수 정의 블록이 더 길면 그 높이)를 반환함. positions[0]은 시작하기, [1..n]은 단계 블록, withEnd면 [n+1]은 완성하기.
        /// 블록은 위 홈 중심을 앞 블록의 아래 돌기 중심에 맞춰 앞 블록 몸통 바로 아래에 놓임. InsideFlowControl 단계는 앞 ㄷ자 블록의 머리 아래 안쪽 돌기부터
        /// 쌓이고, ㄷ자 블록은 안쪽 블록 높이만큼(최소 블록 하나) 늘어나며 그 안쪽 높이를 flowInnerHeights[단계 번호]에 채움(ㄷ자가 아니면 0).
        /// 첫 함수 사용 단계 뒤의 단계는 모두 함수 정의 블록(왼쪽 위가 (functionDefX, 0)) 머리 아래 안쪽 돌기부터 같은 규칙으로 쌓이고, 함수 정의 블록은
        /// 안쪽 블록 높이만큼(최소 블록 하나) 늘어나며 그 안쪽 높이를 functionInnerHeight에 채움(함수 사용 단계가 없으면 0). 완성하기는 시작하기 줄에 붙음.
        /// 명령·논리·함수 사용 블록은 홈·돌기 위치와 몸통 높이가 같아 같은 규칙으로 놓음.
        /// </summary>
        private static float Layout(IReadOnlyList<DesignStepShape> shapes, bool withEnd, float functionDefX, List<Vector2> positions, List<float> flowInnerHeights, out float functionInnerHeight)
        {
            positions.Clear();
            flowInnerHeights.Clear();
            positions.Add(Vector2.zero);
            functionInnerHeight = 0f;

            float y = DesignBlockView.BodyHeightOf(DesignBlockKind.Start);    // 다음 바깥 블록이 놓일 높이
            float tabX = DesignBlockView.TabCenterXOf(DesignBlockKind.Start); // 다음 바깥 블록이 맞물릴 아래 돌기 중심
            int openFlow = -1;                                                  // 안쪽을 채우는 중인 ㄷ자 블록의 단계 번호
            float innerY = 0f, innerTabX = 0f;                                // 그 ㄷ자 블록 안쪽에서 다음 블록이 놓일 높이와 맞물릴 돌기 중심
            bool inFunction = false;                                            // 함수 정의 블록 안쪽에 쌓는 중인지
            float mainY = 0f, mainTabX = 0f;                                  // 그동안 맡겨 둔 시작하기 줄의 다음 바깥 블록 자리

            for (int i = 0; i <= shapes.Count; i++) // i == shapes.Count는 마지막 ㄷ자 블록을 닫기 위한 한 바퀴
            {
                bool inside = i < shapes.Count && shapes[i] == DesignStepShape.InsideFlowControl && openFlow >= 0;
                if (inside)
                {
                    float innerX = innerTabX - DesignBlockView.NotchCenterXOf(DesignBlockKind.Command);
                    positions.Add(new Vector2(innerX, innerY));
                    flowInnerHeights.Add(0f);
                    innerY += DesignBlockView.BodyHeightOf(DesignBlockKind.Command);
                    innerTabX = innerX + DesignBlockView.TabCenterXOf(DesignBlockKind.Command);
                    continue;
                }

                if (openFlow >= 0) // 바깥 블록이 오거나 끝났으니 ㄷ자 블록을 닫고, 다음 바깥 블록은 ㄷ자 블록 아래 돌기에 맞물림
                {
                    Vector2 flowPosition = positions[openFlow + 1];
                    float innerHeight = Mathf.Max(DesignBlockView.MinFlowInnerHeight, innerY - (flowPosition.y + DesignBlockView.FlowHeaderBodyHeight));
                    flowInnerHeights[openFlow] = innerHeight;
                    y = flowPosition.y + DesignBlockView.FlowBodyHeight(innerHeight);
                    tabX = flowPosition.x + DesignBlockView.TabCenterXOf(DesignBlockKind.FlowControl);
                    openFlow = -1;
                }

                if (i == shapes.Count) break;

                DesignBlockKind kind = shapes[i] == DesignStepShape.FlowControl ? DesignBlockKind.FlowControl : DesignBlockKind.Command;
                float x = tabX - DesignBlockView.NotchCenterXOf(kind);
                positions.Add(new Vector2(x, y));
                flowInnerHeights.Add(0f);

                if (kind == DesignBlockKind.FlowControl)
                {
                    openFlow = i;
                    innerY = y + DesignBlockView.FlowHeaderBodyHeight;
                    innerTabX = x + DesignBlockView.FlowInnerTabCenterX;
                }
                else
                {
                    y += DesignBlockView.BodyHeightOf(kind);
                    tabX = x + DesignBlockView.TabCenterXOf(kind);
                }

                if (shapes[i] == DesignStepShape.FunctionCall && !inFunction) // 다음 단계부터 함수 정의 블록 안쪽에 쌓음
                {
                    inFunction = true;
                    mainY = y;
                    mainTabX = tabX;
                    y = DesignBlockView.FlowHeaderBodyHeight;
                    tabX = functionDefX + DesignBlockView.FlowInnerTabCenterX;
                }
            }

            float functionHeight = 0f; // 함수 정의 블록 높이(아래 돌기가 없어 몸통 높이가 곧 이미지 높이임)
            if (inFunction) // 함수 정의 블록을 안쪽 블록 높이만큼 늘이고, 완성하기는 시작하기 줄에 붙임
            {
                functionInnerHeight = Mathf.Max(DesignBlockView.MinFlowInnerHeight, y - DesignBlockView.FlowHeaderBodyHeight);
                functionHeight = DesignBlockView.FlowBodyHeight(functionInnerHeight);
                y = mainY;
                tabX = mainTabX;
            }

            if (!withEnd) return Mathf.Max(y + DesignBlockView.BottomTabHeight, functionHeight);

            positions.Add(new Vector2(tabX - DesignBlockView.NotchCenterXOf(DesignBlockKind.End), y));
            return Mathf.Max(y + DesignBlockView.BodyHeightOf(DesignBlockKind.End), functionHeight); // 완성하기는 아래 돌기가 없어 몸통 높이가 곧 이미지 높이임
        }

        /// <summary> 바깥 명령 블록의 가로 위치(px): 위 홈 중심을 시작하기 블록의 아래 돌기 중심에 맞춘 값. </summary>
        private static float CommandStackX()
        {
            return DesignBlockView.TabCenterXOf(DesignBlockKind.Start) - DesignBlockView.NotchCenterXOf(DesignBlockKind.Command);
        }

        /// <summary>
        /// 블록 묶음 전체 폭(px): 바깥 명령 블록 위치 + 명령 블록 폭(값 블록을 쓰는 레벨이면 값 블록까지 합친 폭). ㄷ자 블록 안쪽 명령 블록은
        /// 바깥 명령 블록과 거의 같은 x(0.5px 왼쪽)에 놓이고 ㄷ자 블록은 그보다 좁아 이 폭을 넘지 않음.
        /// </summary>
        private static float StackWidth(bool withValueBlocks)
        {
            return CommandStackX() + (withValueBlocks ? DesignBlockView.CommandWithValueWidth : DesignBlockView.CommandNoValueWidth);
        }

        /// <summary>
        /// 함수 정의 블록과 안쪽 블록을 합친 폭(px). 안쪽 명령 블록은 함수 정의 블록 안쪽 돌기에 맞춰 오른쪽으로 들어가 있어 함수 정의 블록
        /// 오른쪽 끝보다 튀어나옴.
        /// </summary>
        private static float FunctionColumnWidth(bool withValueBlocks)
        {
            float innerX = DesignBlockView.FlowInnerTabCenterX - DesignBlockView.NotchCenterXOf(DesignBlockKind.Command);
            float innerWidth = innerX + (withValueBlocks ? DesignBlockView.CommandWithValueWidth : DesignBlockView.CommandNoValueWidth);
            return Mathf.Max(DesignBlockView.FunctionDefWidth, innerWidth);
        }

        /// <summary> 블록 배율(scrollScale, 화면 폭을 넘지 않도록 폭으로도 제한)과 가로 시작 위치를 정함. </summary>
        private void UpdateScale()
        {
            Rect viewport = ViewportRect();
            float inset = LeftInset(viewport.width);
            _offsetX = Mathf.Max(edgePadding, inset - stackShiftLeft);
            _shiftX = Mathf.Max(0f, inset - _offsetX);
            float widthScale = (viewport.width - edgePadding * 2f) / StackWidth(_withValueBlocks);
            if (_plansFunctionDef) // 오른쪽에 놓이는 함수 정의 블록(안쪽 블록 포함)과 블록 묶음이 겹치지 않게 함
            {
                float available = viewport.width - edgePadding - _shiftX - _offsetX - DefinitionGap; // 함수 정의 블록도 _shiftX만큼 왼쪽에 있음
                widthScale = Mathf.Min(widthScale, available / (StackWidth(_withValueBlocks) + FunctionColumnWidth(_withValueBlocks)));
            }

            _scale = Mathf.Max(0.01f, Mathf.Min(scrollScale, widthScale));
        }

        /// <summary>
        /// 블록 묶음의 기준 왼쪽 여백. 레벨마다 묶음 폭·배율이 달라도 왼쪽 끝이 같도록, 가장 넓은 묶음(값 블록까지 있는 묶음)을 scrollScale
        /// 배율로 가운데 놓았을 때의 왼쪽 끝을 씀(레벨 1의 위치와 같음). 어느 레벨이든 오른쪽으로 넘치지 않음.
        /// 실제 왼쪽 끝은 여기서 stackShiftLeft만큼 왼쪽(UpdateScale).
        /// </summary>
        private float LeftInset(float viewportWidth)
        {
            float widest = StackWidth(true);
            float scale = Mathf.Min(scrollScale, (viewportWidth - edgePadding * 2f) / widest);
            return (viewportWidth - widest * scale) / 2f;
        }

        /// <summary> 블록을 담는 보이는 영역의 크기. ScrollRect의 viewport가 없으면 content의 부모를 씀. </summary>
        private Rect ViewportRect()
        {
            if (scrollRect && scrollRect.viewport) return scrollRect.viewport.rect;
            if (content && content.parent is RectTransform parent) return parent.rect;

            if (_logger != null) _logger.ZLogWarning($"[DesignPanel] scrollRect·content가 없어 설계창 크기를 알 수 없음. 블록을 원본 크기로 둠.");
            return new Rect(0f, 0f, StackWidth(_withValueBlocks), _stackHeight);
        }

        /// <summary>
        /// 지금 놓인 블록(함수 정의 블록 포함)이 모두 들어가도록 content 높이를 맞춤(스크롤 범위). 사용자가 끄는 동안에는 늘리기만 하고,
        /// 줄이기는 손을 뗀 뒤로 미룸(끄는 중에 줄이면 Clamped가 화면을 한 번에 당김).
        /// </summary>
        private void UpdateContentHeight()
        {
            if (!content)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] content가 null이라 스크롤 범위를 맞출 수 없음.");
                return;
            }

            float height = edgePadding * 2f + _stackHeight * _scale;
            if (IsUserDragging && height < content.sizeDelta.y)
            {
                _pendingScroll = PendingScroll.Shrink;
                return;
            }

            content.sizeDelta = new Vector2(content.sizeDelta.x, height);
        }

        /// <summary>
        /// 새로 놓인 블록이 보이도록 맨 아래로 스크롤하는 연출을 시작하고 반환함(scrollRect가 없거나 사용자가 끄는 중이면 null — 끄는 중이면 손을 뗀 뒤 내림).
        /// 사용자가 드래그로 튕겨 둔 관성은 멈춰 연출과 겹치지 않게 함.
        /// </summary>
        private Tween ScrollToBottom()
        {
            if (!scrollRect)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] scrollRect가 null이라 자동 스크롤을 할 수 없음.");
                return null;
            }

            if (IsUserDragging)
            {
                // 끄는 동안 코드가 스크롤을 움직이면 ScrollRect의 드래그 계산과 겹쳐 떨리므로 손을 뗀 뒤 내림
                _pendingScroll = PendingScroll.ToBottom;
                return null;
            }

            scrollRect.StopMovement();
            _scrollTween?.Kill();
            _scrollTweenShrinks = false;
            _scrollTween = scrollRect.DOVerticalNormalizedPos(0f, scrollDuration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
            return _scrollTween;
        }

        /// <summary>
        /// 블록 묶음이 짧아졌을 때(블록을 떨어뜨림) 줄어든 묶음의 맨 아래가 보이는 자리까지 부드럽게 올린 뒤 스크롤 범위(content 높이)를
        /// 줄임. 범위를 먼저 줄이면 ScrollRect(Clamped)가 범위를 벗어난 위치를 한 번에 끌어올려 튐. scrollRect가 없거나 이미 그 자리보다 위에 있으면 바로 줄이고,
        /// 사용자가 끄는 중이면 손을 뗀 뒤에 함.
        /// content는 위쪽 기준이라 anchoredPosition.y가 아래로 내린 거리임.
        /// </summary>
        private void ShrinkContentToStack()
        {
            if (!scrollRect || !content)
            {
                UpdateContentHeight();
                return;
            }

            if (IsUserDragging)
            {
                // 끄는 동안 범위를 줄이면 손을 뗄 때 Clamped가 한 번에 끌어올려 튀므로 손을 뗀 뒤 줄임
                _pendingScroll = PendingScroll.Shrink;
                return;
            }

            float maxScroll = Mathf.Max(0f, edgePadding * 2f + _stackHeight * _scale - ViewportRect().height); // 줄어든 묶음에서 내릴 수 있는 최대 거리
            scrollRect.StopMovement();
            _scrollTween?.Kill(); // 끊긴 연출의 OnComplete는 불리지 않지만, 아래에서 높이를 다시 맞춤
            if (content.anchoredPosition.y <= maxScroll + ScrolledUpEpsilon)
            {
                UpdateContentHeight();
                return;
            }

            _scrollTweenShrinks = true;
            _scrollTween = content.DOAnchorPosY(maxScroll, scrollDuration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject)
                .OnComplete(UpdateContentHeight);
        }

        /// <summary>
        /// 사용자가 드래그로 위로 올려 둬 맨 아래가 보이지 않는지 여부. 블록 묶음이 보이는 영역보다 짧으면 false.
        /// scrollRect가 없으면 false(경고는 이어서 부르는 ScrollToBottom이 남김).
        /// </summary>
        private bool IsScrolledUp()
        {
            if (!scrollRect || !content) return false;

            float overflow = content.rect.height - ViewportRect().height; // 보이는 영역 밖으로 넘친 높이(스크롤 범위)
            return overflow > ScrolledUpEpsilon && scrollRect.verticalNormalizedPosition * overflow > ScrolledUpEpsilon;
        }

        /// <summary> 놓인 블록(시작하기·단계·완성하기)을 모두 즉시 파괴함. </summary>
        private void DestroyAll()
        {
            if (_startBlock) Destroy(_startBlock.gameObject);
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i]) Destroy(_steps[i].gameObject);
            }
            if (_endBlock) Destroy(_endBlock.gameObject);
            if (_functionDef) Destroy(_functionDef.gameObject);

            _startBlock = null;
            _endBlock = null;
            _functionDef = null;
            _functionDefStep = -1;
            _droppedFrom = int.MaxValue;
            _steps.Clear();
            _stepShapes.Clear();
        }

        /// <summary>
        /// 인스펙터에서 배율·여백을 바꾸면 Play 모드 중에도 바로 다시 배치함(현장 조정용). OnValidate 안에서는 RectTransform 크기를
        /// 바꾸면 경고가 나므로 다음 에디터 업데이트로 미룸. 에디터에서만 호출되므로 빌드 동작에는 영향이 없음.
        /// </summary>
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall -= RelayoutAll; // 인스펙터 값을 연달아 바꿔도 다음 업데이트에 한 번만 다시 배치함
            UnityEditor.EditorApplication.delayCall += RelayoutAll;
#endif
        }

        /// <summary> 지금 배율·여백으로 놓인 블록을 모두 연출 없이 다시 배치함. </summary>
        private void RelayoutAll()
        {
            if (!this || !_startBlock) return; // 그사이 파괴됐거나 아직 시작하기 블록이 없음

            UpdateScale();
            Relayout(); // 위치 목록도 다시 계산함(Play 중 스크립트가 다시 로드되면 목록이 비어 있음)
            _startBlock.transform.localScale = new Vector3(_scale, _scale, 1f);
            _startBlock.SnapTo(PositionOf(0));
            for (int i = 0; i < _steps.Count; i++)
            {
                if (!_steps[i]) continue;
                _steps[i].transform.localScale = new Vector3(_scale, _scale, 1f);
                _steps[i].SnapTo(PositionOf(i + 1));
            }
            if (_endBlock)
            {
                _endBlock.transform.localScale = new Vector3(_scale, _scale, 1f);
                _endBlock.SnapTo(PositionOf(_steps.Count + 1));
            }
            if (_functionDef)
            {
                _functionDef.transform.localScale = new Vector3(_scale, _scale, 1f);
                _functionDef.SnapTo(FunctionDefinitionPosition());
            }
            UpdateContentHeight();
        }

        /// <summary> 오브젝트 파괴 시 스크롤 연출과 끌기 구독을 정리함. </summary>
        private void OnDestroy()
        {
            _scrollTween?.Kill();
            if (_dragTracker)
            {
                _dragTracker.DragStarted -= OnUserDragStarted;
                _dragTracker.DragEnded -= OnUserDragEnded;
            }
        }
    }
}
