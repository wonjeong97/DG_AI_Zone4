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
    /// <summary> 설계창에 블록이 다 들어가지 않을 때의 배치 방식. 기획 확인 뒤 하나만 남길 예정. </summary>
    public enum DesignLayoutMode
    {
        [InspectorName("줄여서 한 화면에")] FitAll,       // 레벨의 최대 블록 수(시작 + 단계 + 완성)가 스크롤 없이 모두 보이게 줄임
        [InspectorName("크게 두고 자동 스크롤")] ScrollLarge // 정해진 배율로 두고, 넘치면 새로 쌓인 블록이 보이게 아래로 스크롤
    }

    /// <summary> 설계창 단계 블록의 모양. 레벨 상태가 단계(재료)마다 정함. </summary>
    public enum DesignStepShape
    {
        Command,  // 명령 블록(값이 없으면 값 소켓 없는 명령 블록)
        FlowControl,       // 만약·반복하기 ㄷ자 블록(값 블록은 머리 오른쪽). 바로 뒤따르는 InsideFlowControl 단계를 안쪽에 품음
        InsideFlowControl, // 앞 ㄷ자 블록 안쪽에 쌓이는 명령 블록(앞에 ㄷ자 블록이 없으면 Command처럼 바깥에 쌓임)
        Logic,    // 그리고·또는 논리 블록(초록, 값 블록 없음)
        FunctionCall // 함수 사용 블록(자주, 값 블록 없음). 붙일 때 설계창 오른쪽에 같은 이름의 함수 정의 ㄷ자 블록도 함께 놓음
    }

    /// <summary> 설계창에 쌓는 단계 블록 하나(모양·명령 블록 문구·값 블록 문구, 값이 없으면 null). 결과 씬에서 플레이어·정답 설계를 다시 그릴 때도 씀. </summary>
    public readonly struct DesignStep
    {
        public readonly DesignStepShape Shape;
        public readonly string Command;
        public readonly string Value;

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
    /// ㄷ자(만약·반복하기) 블록 뒤의 InsideFlowControl 단계는 ㄷ자 블록 안쪽에 쌓이고, 함수 사용 단계는 설계창 오른쪽 위에 함수 정의 블록을 함께 놓음.
    /// 카드가 떨어진 단계부터 뒤쪽 블록을 흐리게 표시하는 일도 맡음.
    /// 블록 모양과 문구는 호출하는 쪽(레벨 상태)이 정함.
    /// </summary>
    public class DesignPanel : MonoBehaviour
    {
        private const float DeactivatedAlpha = 0.35f;
        private const float ScrolledUpEpsilon = 0.5f; // 맨 아래에서 이만큼(UI 단위) 넘게 올라가 있으면 사용자가 올려 둔 것으로 봄
        private const string StartLabel = "시작하기";
        private const string EndLabel = "완성하기";
        private const float DefinitionGap = 16f; // 블록 묶음 오른쪽 끝과 함수 정의 블록 사이 최소 간격(UI 단위)

        [SerializeField] private ScrollRect scrollRect;      // DesignScrollView
        [SerializeField] private RectTransform content;      // DesignScrollView/Viewport/DesignContainer(ScrollRect의 content)
        [SerializeField] private DesignBlockView blockPrefab; // DesignBlock 프리팹

        [Header("Layout (기획 확인 뒤 하나만 남김)")]
        [SerializeField] private DesignLayoutMode layoutMode = DesignLayoutMode.FitAll;
        [SerializeField] private float maxFitScale = 0.8f; // 줄여서 한 화면에: 블록이 적은 레벨에서도 이 배율보다 크게 키우지 않음
        [SerializeField] private float scrollScale = 0.7f; // 크게 두고 자동 스크롤: 블록 배율
        [SerializeField] private float edgePadding = 8f;   // 블록 묶음 위아래·좌우 최소 여백(UI 단위)
        [SerializeField] private float stackShiftLeft = 30f; // 블록 묶음(레벨 5는 함수 정의 블록도)을 기준 위치에서 왼쪽으로 옮기는 거리(UI 단위). 왼쪽 여백(edgePadding)을 넘어가지는 않음

        [Header("Animation")]
        [SerializeField] private float riseDuration = 0.5f;         // 블록이 아래에서 올라와 맞물리는 시간(스토리 라인 연출과 같은 곡선)
        [SerializeField] private float riseHeight = 40f;            // 블록이 올라오기 시작하는 깊이(블록 원본 크기 기준, 배율을 곱해 화면상 약 20px)
        [SerializeField] private float valueSlideDuration = 0.3f;   // 명령 블록이 붙은 뒤 값 블록이 오른쪽에서 미끄러져 와 붙는 시간
        [SerializeField] private float valueSlideDistance = 120f;   // 값 블록이 미끄러지기 시작하는 오른쪽 거리(블록 원본 크기 기준)
        [SerializeField] private float removeDuration = 0.2f;       // 취소 시 블록이 가라앉으며 사라지는 시간
        [SerializeField] private float scrollDuration = 0.3f;       // 자동 스크롤 시간
        [SerializeField] private float completeHoldDuration = 0.5f; // 완성하기 블록이 붙은 뒤 다음 연출까지 보여 주는 시간

        private readonly List<DesignBlockView> _steps = new List<DesignBlockView>();
        private readonly List<DesignStepShape> _stepShapes = new List<DesignStepShape>(); // _steps와 같은 순서의 단계 모양
        private readonly List<Vector2> _positions = new List<Vector2>(); // 블록별 위치(px, 0 = 시작하기). Layout이 채움
        private readonly List<float> _flowInnerHeights = new List<float>(); // 단계별 ㄷ자 블록 안쪽 높이(px, ㄷ자가 아니면 0). Layout이 채움
        private DesignBlockView _startBlock;
        private DesignBlockView _endBlock;
        private DesignBlockView _functionDef; // 설계창 오른쪽에 놓인 함수 정의 블록(함수 사용 단계를 붙였을 때만)
        private int _functionDefStep = -1;    // 함수 정의 블록을 함께 놓은 함수 사용 단계 번호
        private bool _plansFunctionDef;       // 이 레벨에 함수 사용 단계가 있어 오른쪽에 함수 정의 블록 자리를 남겨야 하는지
        private float _plannedHeight; // 이 레벨의 단계를 모두 쌓고 완성하기까지 붙였을 때의 높이(px)
        private float _stackHeight;   // 지금 놓인 블록 묶음의 높이(px, 마지막 블록의 아래 돌기 포함)
        private bool _withValueBlocks = true;
        private float _scale = 1f;
        private float _offsetX;
        private float _shiftX; // 기준 위치에서 실제로 왼쪽으로 옮긴 거리(stackShiftLeft를 왼쪽 여백에 맞춰 줄인 값)
        private Tween _scrollTween;
        private IObjectResolver _resolver;
        private ILogger<DesignPanel> _logger;

        /// <summary> 지금 설계창에 쌓인 단계 블록 수(시작하기·완성하기 제외). </summary>
        public int Count => _steps.Count;

        /// <summary> 완성하기 블록이 붙었는지 여부. </summary>
        public bool IsCompleted => _endBlock;

        /// <summary> 블록 배율(레이아웃 검증용). </summary>
        internal float Scale => _scale;

        /// <summary> 배치 방식(줄여서 한 화면에/크게 두고 자동 스크롤). 결과 씬이 3_Game 설계창과 같은 방식으로 그릴 때 Initialize 전에 바꿈. </summary>
        public DesignLayoutMode LayoutMode
        {
            get => layoutMode;
            set => layoutMode = value;
        }

        /// <summary> 테스트 전용: 인스펙터로 연결하는 블록 content·프리팹·배치 방식과 ScrollRect를 넣음(scroll이 없으면 content의 부모를 보이는 영역으로 쓰고 자동 스크롤은 하지 않음). </summary>
        internal void SetUpForTest(RectTransform contentRoot, DesignBlockView prefab, DesignLayoutMode mode, ScrollRect scroll = null)
        {
            scrollRect = scroll;
            content = contentRoot;
            blockPrefab = prefab;
            layoutMode = mode;
        }

        /// <summary> VContainer 의존성 주입. 블록 생성용 리졸버와 로거를 할당함. </summary>
        [Inject]
        public void Construct(IObjectResolver resolver, ILogger<DesignPanel> logger)
        {
            _resolver = resolver;
            _logger = logger;
        }

        /// <summary>
        /// 설계창을 비우고 맨 위에 시작하기 블록만 놓음. plannedShapes(이 레벨의 단계를 모두 쌓았을 때의 단계 모양)로 '줄여서 한 화면에' 방식의
        /// 배율을 정하고, withValueBlocks(값 블록을 쓰는 레벨인지)로 블록 묶음이 화면 폭을 넘지 않게 할 폭을 정함. 묶음 왼쪽 끝은 레벨과 관계없이 같음.
        /// 함수 사용 단계가 있으면 오른쪽에 함수 정의 블록이 들어갈 폭도 남김.
        /// </summary>
        public void Initialize(IReadOnlyList<DesignStepShape> plannedShapes, bool withValueBlocks)
        {
            DestroyAll();
            _withValueBlocks = withValueBlocks;
            _plansFunctionDef = false;
            for (int i = 0; i < plannedShapes.Count; i++)
            {
                if (plannedShapes[i] == DesignStepShape.FunctionCall) _plansFunctionDef = true;
            }
            _plannedHeight = Layout(plannedShapes, true, _positions, _flowInnerHeights); // 높이만 쓰고, 위치는 아래 Relayout이 지금 상태로 다시 채움
            UpdateScale();

            _startBlock = CreateBlock(DesignBlockKind.Start, StartLabel, null);
            Relayout();
            if (_startBlock) _startBlock.SnapTo(PositionOf(0));
            UpdateContentHeight();
        }

        /// <summary>
        /// 단계 블록 하나를 맨 아래(InsideFlowControl이면 앞 ㄷ자 블록 안쪽 맨 아래)에 쌓음(아래에서 올라와 맞물린 뒤 값 블록이 오른쪽에서 붙는 연출).
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
            if (!block) return;

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
        /// 함수 정의 블록의 content 안 위치(왼쪽 위 기준): 보이는 영역 오른쪽 끝에 붙인 자리에서 블록 묶음을 옮긴 만큼 왼쪽으로 옮기고,
        /// 위쪽은 시작하기 블록과 맞춤.
        /// </summary>
        private Vector2 FunctionDefinitionPosition()
        {
            return new Vector2(ViewportRect().width - edgePadding - _shiftX - DesignBlockView.FunctionDefWidth * _scale, -edgePadding);
        }

        /// <summary> 마지막으로 쌓은 단계 블록을 뺌(가라앉으며 사라지는 연출 뒤 파괴). </summary>
        public void RemoveLastItem()
        {
            if (_steps.Count == 0) return;

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
            UpdateContentHeight();
        }

        /// <summary>
        /// 맨 아래에 완성하기 블록을 붙이고, 맞물리는 연출과 잠깐의 대기가 끝날 때까지 기다림. 사용자가 드래그로 위로 올려 둔 상태면 먼저 맨 아래로
        /// 부드럽게 내린 뒤 붙임. 이미 붙어 있으면 바로 끝남.
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
        /// fromIndex번째 단계 블록부터 끝까지 흐리게, 그 앞은 원래대로 표시함. fromIndex가 블록 수 이상이면 모두 원래대로 표시함.
        /// 함수 정의 블록은 함께 놓인 함수 사용 단계를 따름.
        /// </summary>
        public void DimFrom(int fromIndex)
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i]) _steps[i].SetDimmed(i >= fromIndex, DeactivatedAlpha);
            }
            if (_functionDef) _functionDef.SetDimmed(_functionDefStep >= fromIndex, DeactivatedAlpha);
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

        /// <summary> 지금 놓인 블록으로 위치와 묶음 높이를 다시 계산하고, ㄷ자 블록 높이를 안쪽 블록에 맞춤. </summary>
        private void Relayout()
        {
            bool withEnd = _endBlock;
            _stackHeight = Layout(_stepShapes, withEnd, _positions, _flowInnerHeights);
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i] && _steps[i].Kind == DesignBlockKind.FlowControl) _steps[i].SetFlowInnerHeight(_flowInnerHeights[i]);
            }
        }

        /// <summary>
        /// 블록 묶음을 위에서부터 따라가며 블록마다 위치(px, 블록 원본 크기·시작하기 왼쪽 위 기준)를 positions에 채우고, 묶음 높이(마지막 블록의
        /// 아래 돌기 포함)를 반환함. positions[0]은 시작하기, [1..n]은 단계 블록, withEnd면 [n+1]은 완성하기.
        /// 블록은 위 홈 중심을 앞 블록의 아래 돌기 중심에 맞춰 앞 블록 몸통 바로 아래에 놓임. InsideFlowControl 단계는 앞 ㄷ자 블록의 머리 아래 안쪽 돌기부터
        /// 쌓이고, ㄷ자 블록은 안쪽 블록 높이만큼(최소 블록 하나) 늘어나며 그 안쪽 높이를 flowInnerHeights[단계 번호]에 채움(ㄷ자가 아니면 0).
        /// 명령·논리 블록은 홈·돌기 위치와 몸통 높이가 같아 같은 규칙으로 놓음.
        /// </summary>
        private static float Layout(IReadOnlyList<DesignStepShape> shapes, bool withEnd, List<Vector2> positions, List<float> flowInnerHeights)
        {
            positions.Clear();
            flowInnerHeights.Clear();
            positions.Add(Vector2.zero);

            float y = DesignBlockView.BodyHeightOf(DesignBlockKind.Start);    // 다음 바깥 블록이 놓일 높이
            float tabX = DesignBlockView.TabCenterXOf(DesignBlockKind.Start); // 다음 바깥 블록이 맞물릴 아래 돌기 중심
            int openFlow = -1;                                                  // 안쪽을 채우는 중인 ㄷ자 블록의 단계 번호
            float innerY = 0f, innerTabX = 0f;                                // 그 ㄷ자 블록 안쪽에서 다음 블록이 놓일 높이와 맞물릴 돌기 중심

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
            }

            if (!withEnd) return y + DesignBlockView.BottomTabHeight;

            positions.Add(new Vector2(tabX - DesignBlockView.NotchCenterXOf(DesignBlockKind.End), y));
            return y + DesignBlockView.BodyHeightOf(DesignBlockKind.End); // 완성하기는 아래 돌기가 없어 몸통 높이가 곧 이미지 높이임
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

        /// <summary> 배치 방식에 맞춰 블록 배율과 가로 시작 위치를 정함. 화면 폭을 넘지 않도록 두 방식 모두 폭으로도 제한함. </summary>
        private void UpdateScale()
        {
            Rect viewport = ViewportRect();
            float inset = LeftInset(viewport.width);
            _offsetX = Mathf.Max(edgePadding, inset - stackShiftLeft);
            _shiftX = Mathf.Max(0f, inset - _offsetX);
            float widthScale = (viewport.width - edgePadding * 2f) / StackWidth(_withValueBlocks);
            if (_plansFunctionDef) // 오른쪽에 놓이는 함수 정의 블록과 블록 묶음이 겹치지 않게 함
            {
                float available = viewport.width - edgePadding - _shiftX - _offsetX - DefinitionGap; // 함수 정의 블록도 _shiftX만큼 왼쪽에 있음
                widthScale = Mathf.Min(widthScale, available / (StackWidth(_withValueBlocks) + DesignBlockView.FunctionDefWidth));
            }

            if (layoutMode == DesignLayoutMode.FitAll)
            {
                float heightScale = (viewport.height - edgePadding * 2f) / _plannedHeight;
                _scale = Mathf.Min(maxFitScale, heightScale, widthScale);
            }
            else
            {
                _scale = Mathf.Min(scrollScale, widthScale);
            }

            _scale = Mathf.Max(0.01f, _scale);
        }

        /// <summary>
        /// 블록 묶음의 기준 왼쪽 여백. 레벨마다 묶음 폭·배율이 달라도 왼쪽 끝이 같도록, 가장 넓은 묶음(값 블록까지 있는 묶음)을 이 배치 방식의
        /// 최대 배율로 가운데 놓았을 때의 왼쪽 끝을 씀(블록이 적어 최대 배율을 쓰는 레벨 1의 위치와 같음). 어느 레벨이든 오른쪽으로 넘치지 않음.
        /// 실제 왼쪽 끝은 여기서 stackShiftLeft만큼 왼쪽(UpdateScale).
        /// </summary>
        private float LeftInset(float viewportWidth)
        {
            float widest = StackWidth(true);
            float maxScale = layoutMode == DesignLayoutMode.FitAll ? maxFitScale : scrollScale;
            float scale = Mathf.Min(maxScale, (viewportWidth - edgePadding * 2f) / widest);
            return (viewportWidth - widest * scale) / 2f;
        }

        /// <summary> 블록을 담는 보이는 영역의 크기. ScrollRect의 viewport가 없으면 content의 부모를 씀. </summary>
        private Rect ViewportRect()
        {
            if (scrollRect && scrollRect.viewport) return scrollRect.viewport.rect;
            if (content && content.parent is RectTransform parent) return parent.rect;

            if (_logger != null) _logger.ZLogWarning($"[DesignPanel] scrollRect·content가 없어 설계창 크기를 알 수 없음. 블록을 원본 크기로 둠.");
            return new Rect(0f, 0f, StackWidth(_withValueBlocks), _plannedHeight);
        }

        /// <summary> 지금 놓인 블록이 모두 들어가도록 content 높이를 맞춤(스크롤 범위). </summary>
        private void UpdateContentHeight()
        {
            if (!content) return;

            float stackHeight = _stackHeight;
            if (_functionDef) stackHeight = Mathf.Max(stackHeight, DesignBlockView.BodyHeightOf(DesignBlockKind.FunctionDef)); // 위쪽에 맞춘 함수 정의 블록이 더 길면 그만큼
            float height = edgePadding * 2f + stackHeight * _scale;
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);
        }

        /// <summary>
        /// '크게 두고 자동 스크롤' 방식에서 새로 놓인 블록이 보이도록 맨 아래로 스크롤하는 연출을 시작하고 반환함(그 방식이 아니거나 scrollRect가
        /// 없으면 null). 사용자가 드래그로 튕겨 둔 관성은 멈춰 연출과 겹치지 않게 함.
        /// </summary>
        private Tween ScrollToBottom()
        {
            if (layoutMode != DesignLayoutMode.ScrollLarge) return null;
            if (!scrollRect)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] scrollRect가 null이라 자동 스크롤을 할 수 없음.");
                return null;
            }

            scrollRect.StopMovement();
            _scrollTween?.Kill();
            _scrollTween = scrollRect.DOVerticalNormalizedPos(0f, scrollDuration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
            return _scrollTween;
        }

        /// <summary>
        /// '크게 두고 자동 스크롤' 방식에서 사용자가 드래그로 위로 올려 둬 맨 아래가 보이지 않는지 여부. 블록 묶음이 보이는 영역보다 짧으면 false.
        /// scrollRect가 없으면 false(경고는 이어서 부르는 ScrollToBottom이 남김).
        /// </summary>
        private bool IsScrolledUp()
        {
            if (layoutMode != DesignLayoutMode.ScrollLarge || !scrollRect || !content) return false;

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
            _steps.Clear();
            _stepShapes.Clear();
        }

        /// <summary>
        /// 인스펙터에서 배치 방식·배율을 바꾸면 Play 모드 중에도 바로 다시 배치함(기획 확인용 비교). OnValidate 안에서는 RectTransform 크기를
        /// 바꾸면 경고가 나므로 다음 에디터 업데이트로 미룸. 에디터에서만 호출되므로 빌드 동작에는 영향이 없음.
        /// </summary>
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += RelayoutAll;
#endif
        }

        /// <summary> 지금 배치 방식·배율로 놓인 블록을 모두 연출 없이 다시 배치함. </summary>
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

        /// <summary> 오브젝트 파괴 시 스크롤 연출을 정리함. </summary>
        private void OnDestroy()
        {
            _scrollTween?.Kill();
        }
    }
}
