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

    /// <summary>
    /// 확정된 블록을 쌓아 보여 주는 설계창(Image_DesignWindow). 맨 위에 '시작하기' 블록을 두고, 설정하기로 확정할 때마다 명령(+값) 블록이
    /// 위에서 내려와 맞물리며, 코딩 완료 시 맨 아래에 '완성하기' 블록이 붙음. 카드가 떨어진 단계부터 뒤쪽 블록을 흐리게 표시하는 일도 맡음.
    /// 블록 문구는 호출하는 쪽(레벨 상태)이 정함.
    /// </summary>
    public class DesignPanel : MonoBehaviour
    {
        private const float DeactivatedAlpha = 0.35f;
        private const string StartLabel = "시작하기";
        private const string EndLabel = "완성하기";

        [SerializeField] private ScrollRect scrollRect;      // DesignScrollView
        [SerializeField] private RectTransform content;      // DesignScrollView/Viewport/DesignContainer(ScrollRect의 content)
        [SerializeField] private DesignBlockView blockPrefab; // DesignBlock 프리팹

        [Header("Layout (기획 확인 뒤 하나만 남김)")]
        [SerializeField] private DesignLayoutMode layoutMode = DesignLayoutMode.FitAll;
        [SerializeField] private float maxFitScale = 0.8f; // 줄여서 한 화면에: 블록이 적은 레벨에서도 이 배율보다 크게 키우지 않음
        [SerializeField] private float scrollScale = 0.7f; // 크게 두고 자동 스크롤: 블록 배율
        [SerializeField] private float edgePadding = 8f;   // 블록 묶음 위아래·좌우 최소 여백(UI 단위)

        [Header("Animation")]
        [SerializeField] private float dropDuration = 0.35f;        // 블록이 내려와 맞물리는 시간
        [SerializeField] private float dropHeight = 60f;            // 블록이 내려오기 시작하는 높이(블록 원본 크기 기준)
        [SerializeField] private float removeDuration = 0.2f;       // 취소 시 블록이 떠오르며 사라지는 시간
        [SerializeField] private float scrollDuration = 0.3f;       // 자동 스크롤 시간
        [SerializeField] private float completeHoldDuration = 0.5f; // 완성하기 블록이 붙은 뒤 다음 연출까지 보여 주는 시간

        private readonly List<DesignBlockView> _steps = new List<DesignBlockView>();
        private DesignBlockView _startBlock;
        private DesignBlockView _endBlock;
        private int _maxSteps;
        private bool _withValueBlocks = true;
        private float _scale = 1f;
        private float _offsetX;
        private Tween _scrollTween;
        private IObjectResolver _resolver;
        private ILogger<DesignPanel> _logger;

        /// <summary> 지금 설계창에 쌓인 단계 블록 수(시작하기·완성하기 제외). </summary>
        public int Count => _steps.Count;

        /// <summary> 완성하기 블록이 붙었는지 여부. </summary>
        public bool IsCompleted => _endBlock;

        /// <summary> 블록 배율(레이아웃 검증용). </summary>
        internal float Scale => _scale;

        /// <summary> 테스트 전용: 인스펙터로 연결하는 블록 content·프리팹과 배치 방식을 넣음(scrollRect 없이 content의 부모를 보이는 영역으로 씀). </summary>
        internal void SetUpForTest(RectTransform contentRoot, DesignBlockView prefab, DesignLayoutMode mode)
        {
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
        /// 설계창을 비우고 맨 위에 시작하기 블록만 놓음. maxSteps(이 레벨에서 쌓일 수 있는 최대 단계 수)로 '줄여서 한 화면에' 방식의 배율을 정하고,
        /// withValueBlocks(값 블록을 쓰는 레벨인지)로 블록 묶음을 가운데 놓을 폭을 정함.
        /// </summary>
        public void Initialize(int maxSteps, bool withValueBlocks)
        {
            DestroyAll();
            _maxSteps = Mathf.Max(0, maxSteps);
            _withValueBlocks = withValueBlocks;
            UpdateScale();

            _startBlock = CreateBlock(DesignBlockKind.Start, StartLabel, null);
            if (_startBlock) _startBlock.SnapTo(PositionOf(0));
            UpdateContentHeight();
        }

        /// <summary> 단계 블록 하나를 맨 아래에 쌓음(위에서 내려와 맞물리는 연출). value가 비어 있으면 값 블록 없는 명령 블록을 씀. </summary>
        public void AddItem(string command, string value)
        {
            if (!_startBlock)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] Initialize 전이라 블록을 쌓을 수 없음.");
                return;
            }

            DesignBlockKind kind = string.IsNullOrEmpty(value) ? DesignBlockKind.CommandNoValue : DesignBlockKind.Command;
            DesignBlockView block = CreateBlock(kind, command, value);
            if (!block) return;

            _steps.Add(block);
            block.PlayDropIn(PositionOf(_steps.Count), dropHeight * _scale, dropDuration);
            UpdateContentHeight();
            ScrollToBottom();
        }

        /// <summary> 마지막으로 쌓은 단계 블록을 뺌(떠오르며 사라지는 연출 뒤 파괴). </summary>
        public void RemoveLastItem()
        {
            if (_steps.Count == 0) return;

            int lastIndex = _steps.Count - 1;
            DesignBlockView last = _steps[lastIndex];
            _steps.RemoveAt(lastIndex);
            if (last) last.PlayRemoveAndDestroy(dropHeight * _scale, removeDuration);
            UpdateContentHeight();
        }

        /// <summary> 맨 아래에 완성하기 블록을 붙이고, 맞물리는 연출과 잠깐의 대기가 끝날 때까지 기다림. 이미 붙어 있으면 바로 끝남. </summary>
        public async UniTask AttachEndBlockAsync(CancellationToken token)
        {
            if (_endBlock) return;
            if (!_startBlock)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] Initialize 전이라 완성하기 블록을 붙일 수 없음.");
                return;
            }

            _endBlock = CreateBlock(DesignBlockKind.End, EndLabel, null);
            if (!_endBlock) return;

            Sequence drop = _endBlock.PlayDropIn(PositionOf(_steps.Count + 1), dropHeight * _scale, dropDuration);
            UpdateContentHeight();
            ScrollToBottom();

            await drop.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
            await UniTask.Delay(TimeSpan.FromSeconds(completeHoldDuration), DelayType.UnscaledDeltaTime, cancellationToken: token);
        }

        /// <summary> fromIndex번째 단계 블록부터 끝까지 흐리게, 그 앞은 원래대로 표시함. fromIndex가 블록 수 이상이면 모두 원래대로 표시함. </summary>
        public void DimFrom(int fromIndex)
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i]) _steps[i].SetDimmed(i >= fromIndex, DeactivatedAlpha);
            }
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

        /// <summary>
        /// 위에서 index번째 블록(0 = 시작하기)의 content 안 위치(왼쪽 위 기준)를 계산함. 블록은 앞 블록의 몸통 높이만큼 아래에 놓이고,
        /// 가로는 위 홈 중심이 앞 블록의 아래 돌기 중심과 맞도록 옮김. 단계 블록은 모두 명령 블록 계열이라 위치 규칙이 같음.
        /// </summary>
        private Vector2 PositionOf(int index)
        {
            float x = XOf(KindAt(index));
            float y = StackHeightAbove(index);
            return new Vector2(_offsetX + x * _scale, -(edgePadding + y * _scale));
        }

        /// <summary> 종류별 가로 위치(px). 시작하기는 0, 단계 블록은 시작하기 돌기에 맞춘 위치, 완성하기는 단계 블록 돌기에 맞춘 위치. </summary>
        private static float XOf(DesignBlockKind kind)
        {
            switch (kind)
            {
                case DesignBlockKind.Start:
                    return 0f;
                case DesignBlockKind.End:
                    return CommandStackX() + DesignBlockView.TabCenterXOf(DesignBlockKind.Command) - DesignBlockView.NotchCenterXOf(DesignBlockKind.End);
                default:
                    return CommandStackX();
            }
        }

        /// <summary> 위에서 index번째 블록의 종류(0 = 시작하기, 단계 수 다음 = 완성하기, 그 사이 = 단계 블록). </summary>
        private DesignBlockKind KindAt(int index)
        {
            if (index == 0) return DesignBlockKind.Start;
            if (index > _steps.Count) return DesignBlockKind.End;
            DesignBlockView step = _steps[index - 1];
            return step ? step.Kind : DesignBlockKind.Command;
        }

        /// <summary> 단계 블록의 가로 위치(px): 위 홈 중심을 시작하기 블록의 아래 돌기 중심에 맞춘 값. </summary>
        private static float CommandStackX()
        {
            return DesignBlockView.TabCenterXOf(DesignBlockKind.Start) - DesignBlockView.NotchCenterXOf(DesignBlockKind.Command);
        }

        /// <summary> index번째 블록 위쪽에 놓인 블록들의 몸통 높이 합(px). </summary>
        private static float StackHeightAbove(int index)
        {
            if (index <= 0) return 0f;
            return DesignBlockView.BodyHeightOf(DesignBlockKind.Start) + (index - 1) * DesignBlockView.BodyHeightOf(DesignBlockKind.Command);
        }

        /// <summary> 블록 묶음 전체 폭(px): 단계 블록 위치 + 명령 블록 폭(값 블록을 쓰는 레벨이면 값 블록까지 합친 폭). </summary>
        private float StackWidth()
        {
            return CommandStackX() + (_withValueBlocks ? DesignBlockView.CommandWithValueWidth : DesignBlockView.CommandNoValueWidth);
        }

        /// <summary> 시작하기 + stepCount개 단계 + 완성하기가 모두 놓였을 때의 높이(px). 완성하기는 아래 돌기가 없어 몸통 높이가 곧 이미지 높이임. </summary>
        private static float StackHeight(int stepCount)
        {
            return StackHeightAbove(stepCount + 1) + DesignBlockView.BodyHeightOf(DesignBlockKind.End);
        }

        /// <summary> 배치 방식에 맞춰 블록 배율과 가로 시작 위치를 정함. 화면 폭을 넘지 않도록 두 방식 모두 폭으로도 제한함. </summary>
        private void UpdateScale()
        {
            Rect viewport = ViewportRect();
            float widthScale = (viewport.width - edgePadding * 2f) / StackWidth();

            if (layoutMode == DesignLayoutMode.FitAll)
            {
                float heightScale = (viewport.height - edgePadding * 2f) / StackHeight(_maxSteps);
                _scale = Mathf.Min(maxFitScale, heightScale, widthScale);
            }
            else
            {
                _scale = Mathf.Min(scrollScale, widthScale);
            }

            _scale = Mathf.Max(0.01f, _scale);
            _offsetX = (viewport.width - StackWidth() * _scale) / 2f;
        }

        /// <summary> 블록을 담는 보이는 영역의 크기. ScrollRect의 viewport가 없으면 content의 부모를 씀. </summary>
        private Rect ViewportRect()
        {
            if (scrollRect && scrollRect.viewport) return scrollRect.viewport.rect;
            if (content && content.parent is RectTransform parent) return parent.rect;

            if (_logger != null) _logger.ZLogWarning($"[DesignPanel] scrollRect·content가 없어 설계창 크기를 알 수 없음. 블록을 원본 크기로 둠.");
            return new Rect(0f, 0f, StackWidth(), StackHeight(_maxSteps));
        }

        /// <summary> 지금 놓인 블록이 모두 들어가도록 content 높이를 맞춤(스크롤 범위). </summary>
        private void UpdateContentHeight()
        {
            if (!content) return;

            int lastIndex = _steps.Count + (_endBlock ? 1 : 0);
            DesignBlockKind lastKind = KindAt(lastIndex);
            float lastHeight = DesignBlockView.BodyHeightOf(lastKind) + (lastKind == DesignBlockKind.End ? 0f : DesignBlockView.BottomTabHeight); // 아래 돌기까지 포함
            float height = edgePadding * 2f + (StackHeightAbove(lastIndex) + lastHeight) * _scale;
            content.sizeDelta = new Vector2(content.sizeDelta.x, height);
        }

        /// <summary> '크게 두고 자동 스크롤' 방식에서 새로 놓인 블록이 보이도록 맨 아래로 스크롤함. </summary>
        private void ScrollToBottom()
        {
            if (layoutMode != DesignLayoutMode.ScrollLarge) return;
            if (!scrollRect)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] scrollRect가 null이라 자동 스크롤을 할 수 없음.");
                return;
            }

            _scrollTween?.Kill();
            _scrollTween = scrollRect.DOVerticalNormalizedPos(0f, scrollDuration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
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

            _startBlock = null;
            _endBlock = null;
            _steps.Clear();
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
            UpdateContentHeight();
        }

        /// <summary> 오브젝트 파괴 시 스크롤 연출을 정리함. </summary>
        private void OnDestroy()
        {
            _scrollTween?.Kill();
        }
    }
}
