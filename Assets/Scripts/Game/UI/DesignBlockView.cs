using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DGAIZone.Game.UI
{
    /// <summary> 설계창 블록 종류. 종류마다 블록 이미지와 홈·돌기 위치가 다름. </summary>
    public enum DesignBlockKind
    {
        Start,          // 시작하기(진회색, 아래 돌기)
        Command,        // 재료 이름(살몬, 위 홈·아래 돌기·오른쪽 값 소켓) + 오른쪽에 끼운 값 블록(파랑)
        CommandNoValue, // 재료 이름 없이 값만 있는 단계(살몬, 값 소켓 없음)
        FlowControl,    // 만약·반복하기(주황 ㄷ자 — Zone1 만약 If.png, 위 홈·머리 오른쪽 값 소켓·머리 아래 안쪽 돌기·아래 돌기). 안쪽 높이에 맞춰 팔 부분만 늘어남(9-slice)
        Logic,          // 그리고·또는(초록, 값 소켓 없는 명령 블록과 같은 모양)
        Function,       // 함수 사용(자주 — Zone1 Func.png, 값 소켓 없는 명령 블록과 같은 모양)
        FunctionDef,    // 함수 정의(자주 ㄷ자 — Zone1 FuncBody.png). 시작하기 줄과 잇지 않는 독립 블록이라 위 홈·아래 돌기·값 소켓이 없고, 함수 사용 뒤 단계 블록을 안쪽에 품음(9-slice)
        End             // 완성하기(진회색, 위 홈)
    }

    /// <summary>
    /// 설계창에 쌓는 블록 하나(몸통 이미지 + 라벨, 명령·ㄷ자 블록은 오른쪽 값 블록 포함)의 표시와 쌓기·빼기 연출을 맡음.
    /// 이미지는 원본 픽셀 크기(1px = UI 1단위)로 두고, 크기 조절은 DesignPanel이 블록 전체 localScale로 함.
    /// 좌표는 모두 블록 왼쪽 위를 기준으로 하며, 아래 값들은 블록 이미지(Zone1 블록 아트)에서 잰 값임.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class DesignBlockView : MonoBehaviour
    {
        // 위 홈·아래 돌기의 가로 중심(px). 다음 블록의 홈 중심을 이전 블록의 돌기 중심에 맞춰 놓아야 맞물림
        private const float StartTabCenterX = 60f;
        private const float CommandSocketCenterX = 40.5f; // 명령 블록 두 종류와 논리 블록은 위 홈과 아래 돌기가 같은 위치
        private const float FlowSocketCenterX = 61f;        // ㄷ자 블록의 위 홈과 아래 돌기
        private const float EndNotchCenterX = 60f;

        /// <summary> ㄷ자 블록 머리 아래 안쪽 돌기의 가로 중심(px). 안쪽 첫 블록의 위 홈 중심을 여기에 맞춤. 함수 정의 블록(FuncBody.png)도 같은 위치(차이 0.5px 미만). </summary>
        public const float FlowInnerTabCenterX = 60.5f;

        /// <summary> ㄷ자 블록 머리의 높이(px). 안쪽 첫 블록은 ㄷ자 블록 위쪽에서 이만큼 아래에 놓임. </summary>
        public const float FlowHeaderBodyHeight = 101f;

        /// <summary> ㄷ자 블록 안쪽의 최소 높이(px). 안쪽 블록이 아직 없어도 명령 블록 하나가 들어갈 자리를 비워 둠. </summary>
        public const float MinFlowInnerHeight = CommandBodyHeight;

        private const float FlowFooterBodyHeight = 101f; // ㄷ자 블록 아래 막대 높이(아래 돌기 제외). 함수 정의 블록도 같음

        /// <summary> 함수 정의 블록 폭(px). 설계창 오른쪽에 놓을 때 쓰임. </summary>
        public const float FunctionDefWidth = 361f;

        // 몸통 높이(px). 이미지 높이에서 아래 돌기를 뺀 값으로, 다음 블록이 놓이는 위치가 됨
        private const float StartBodyHeight = 100f;
        private const float CommandBodyHeight = 101f;
        private const float EndBodyHeight = 101f;

        /// <summary> 아래 돌기 높이(px). 명령 블록 이미지 높이 121 = 몸통 101 + 이 값(시작하기는 몸통 100 아래에 같은 높이의 돌기). </summary>
        public const float BottomTabHeight = 20f;

        private const float CommandSocketWidth = 20f; // 명령 블록 오른쪽 값 소켓 돌기 폭(이미지 폭 381 = 몸통 361 + 20)
        private const float LabelPadding = 12f;       // 라벨과 블록 테두리 사이 여백

        [SerializeField] private Image body;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Image valueBody; // 명령 블록 오른쪽 소켓에 끼우는 값 블록
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private CanvasGroup valueGroup; // 값 블록만 따로 나타나게 하는 CanvasGroup

        [Header("Block Sprites")]
        [SerializeField] private Sprite startSprite;
        [SerializeField] private Sprite commandSprite;
        [SerializeField] private Sprite commandNoValueSprite;
        [SerializeField] private Sprite flowControlSprite; // Zone1 만약(If.png). 9-slice 경계(왼 20, 위·아래 121)가 있어야 안쪽 높이만큼 팔이 늘어남
        [SerializeField] private Sprite logicSprite;
        [SerializeField] private Sprite functionSprite;
        [SerializeField] private Sprite functionDefSprite; // 9-slice 경계(왼 20, 아래 101, 위 121)가 있어야 안쪽 높이만큼 팔이 늘어남
        [SerializeField] private Sprite endSprite;
        [SerializeField] private Sprite valueSprite;

        private CanvasGroup _group;
        private Sequence _motion;
        private DesignBlockKind _kind;

        /// <summary> 이 블록의 종류. </summary>
        public DesignBlockKind Kind => _kind;

        /// <summary> 값 블록 위치(검증용). </summary>
        internal Vector2 ValuePosition => ((RectTransform)valueBody.transform).anchoredPosition;

        /// <summary> 값 블록 알파(검증용). </summary>
        internal float ValueAlpha => valueGroup.alpha;

        /// <summary> 값 블록이 명령 블록 소켓에 붙은 위치(블록 왼쪽 위 기준, px). </summary>
        private static Vector2 ValueAttachedPosition => new Vector2(CommandWidthWithoutSocket, 0f);

        /// <summary> 명령 블록과 오른쪽 값 블록을 합친 폭(px). 블록 묶음의 폭 계산에 쓰임. </summary>
        public static float CommandWithValueWidth => CommandWidthWithoutSocket + ValueWidth;

        /// <summary> 값 소켓 없는 명령 블록의 폭(px). 값 블록을 쓰지 않는 레벨의 블록 묶음 폭 계산에 쓰임. </summary>
        public const float CommandNoValueWidth = 361f;

        // 이미지 크기(px). 프리팹 스프라이트와 같아야 하며, 레이아웃 계산을 인스턴스 없이 하려고 상수로 둠
        private const float CommandWidthWithoutSocket = 360f;
        private const float ValueWidth = 361f;

        /// <summary> CanvasGroup을 찾아 둠. </summary>
        private void Awake()
        {
            TryGetComponent(out _group); // RequireComponent로 항상 붙어 있음
        }

        /// <summary>
        /// 종류에 맞는 이미지와 라벨을 적용하고 이미지 원본 크기로 맞춤(ㄷ자 블록은 안쪽 최소 높이로). 값 블록은 값 소켓이 있는 종류(명령·만약·반복하기)에
        /// 값이 있을 때만 보임.
        /// </summary>
        public void Setup(DesignBlockKind kind, string label, string value)
        {
            _kind = kind;

            body.sprite = SpriteOf(kind);
            body.type = IsContainer(kind) ? Image.Type.Sliced : Image.Type.Simple;
            body.SetNativeSize();
            ((RectTransform)transform).sizeDelta = ((RectTransform)body.transform).sizeDelta;
            bodyLabel.text = label;

            if (IsContainer(kind))
            {
                SetFlowInnerHeight(MinFlowInnerHeight); // 이미지 높이와 머리 라벨 영역을 함께 정함
            }
            else
            {
                float imageHeight = ((RectTransform)body.transform).sizeDelta.y;
                float tabHeight = imageHeight - BodyHeightOf(kind);
                float rightInset = LabelPadding + (kind == DesignBlockKind.Command ? CommandSocketWidth : 0f);
                SetLabelArea(bodyLabel, LabelPadding, rightInset, tabHeight + LabelPadding, LabelPadding);
            }

            bool hasValue = (kind == DesignBlockKind.Command || kind == DesignBlockKind.FlowControl) && !string.IsNullOrEmpty(value);
            valueBody.gameObject.SetActive(hasValue);
            if (hasValue)
            {
                valueBody.sprite = valueSprite;
                valueBody.SetNativeSize();
                RectTransform valueRect = (RectTransform)valueBody.transform;
                valueRect.anchoredPosition = ValueAttachedPosition;
                valueGroup.alpha = 1f;
                SetLabelArea(valueLabel, LabelPadding, LabelPadding, LabelPadding, LabelPadding); // Zone1처럼 왼쪽 홈까지 포함한 블록 폭의 가운데
                valueLabel.text = value;
            }
        }

        /// <summary>
        /// ㄷ자 블록(만약·반복하기·함수 정의)의 안쪽 높이(px)를 정함. 이미지는 9-slice라 머리·아래 막대는 그대로 두고 왼쪽 팔만 늘어나며,
        /// 라벨은 머리 부분에 다시 맞춤. 블록 위쪽을 기준으로 놓여 있어 아래로만 늘어남. 함수 정의 블록은 아래 돌기와 머리 오른쪽 값 소켓이 없음.
        /// </summary>
        public void SetFlowInnerHeight(float innerHeight)
        {
            bool isFunctionDef = _kind == DesignBlockKind.FunctionDef;
            RectTransform bodyRect = (RectTransform)body.transform;
            float imageHeight = FlowBodyHeight(innerHeight) + (isFunctionDef ? 0f : BottomTabHeight);
            bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x, imageHeight);
            ((RectTransform)transform).sizeDelta = bodyRect.sizeDelta;
            float rightInset = LabelPadding + (isFunctionDef ? 0f : CommandSocketWidth);
            SetLabelArea(bodyLabel, LabelPadding, rightInset, imageHeight - FlowHeaderBodyHeight + LabelPadding, LabelPadding);
        }

        /// <summary> 안쪽 높이가 늘어나는 ㄷ자 이미지(9-slice)를 쓰는 종류인지 여부. </summary>
        private static bool IsContainer(DesignBlockKind kind)
        {
            return kind == DesignBlockKind.FlowControl || kind == DesignBlockKind.FunctionDef;
        }

        /// <summary> 안쪽 높이가 innerHeight인 ㄷ자 블록의 몸통 높이(px, 아래 돌기 제외). 다음 블록은 ㄷ자 블록 위치에서 이만큼 아래에 놓임. </summary>
        public static float FlowBodyHeight(float innerHeight)
        {
            return FlowHeaderBodyHeight + innerHeight + FlowFooterBodyHeight;
        }

        /// <summary> 라벨이 부모 이미지 안쪽(테두리·돌기를 뺀 영역)을 채우도록 여백을 설정함. </summary>
        private static void SetLabelArea(TMP_Text label, float left, float right, float bottom, float top)
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary> 종류에 맞는 블록 이미지를 반환함. </summary>
        private Sprite SpriteOf(DesignBlockKind kind)
        {
            switch (kind)
            {
                case DesignBlockKind.Start: return startSprite;
                case DesignBlockKind.Command: return commandSprite;
                case DesignBlockKind.CommandNoValue: return commandNoValueSprite;
                case DesignBlockKind.FlowControl: return flowControlSprite;
                case DesignBlockKind.Logic: return logicSprite;
                case DesignBlockKind.Function: return functionSprite;
                case DesignBlockKind.FunctionDef: return functionDefSprite;
                default: return endSprite;
            }
        }

        /// <summary> 종류별 몸통 높이(px). 다음 블록은 이 블록 위치에서 이만큼 아래에 놓임. ㄷ자 블록은 안쪽이 비었을 때(최소) 높이이며(함수 정의 블록은 아래 돌기가 없어 이미지 높이와 같음), 안쪽이 차면 FlowBodyHeight를 씀. </summary>
        public static float BodyHeightOf(DesignBlockKind kind)
        {
            switch (kind)
            {
                case DesignBlockKind.Start: return StartBodyHeight;
                case DesignBlockKind.End: return EndBodyHeight;
                case DesignBlockKind.FlowControl:
                case DesignBlockKind.FunctionDef: return FlowBodyHeight(MinFlowInnerHeight);
                default: return CommandBodyHeight;
            }
        }

        /// <summary> 종류별 아래 돌기의 가로 중심(px). 완성하기 블록은 아래 돌기가 없어 쓰지 않음. </summary>
        public static float TabCenterXOf(DesignBlockKind kind)
        {
            switch (kind)
            {
                case DesignBlockKind.Start: return StartTabCenterX;
                case DesignBlockKind.FlowControl: return FlowSocketCenterX;
                default: return CommandSocketCenterX;
            }
        }

        /// <summary> 종류별 위 홈의 가로 중심(px). 시작하기 블록은 위 홈이 없어 쓰지 않음. </summary>
        public static float NotchCenterXOf(DesignBlockKind kind)
        {
            switch (kind)
            {
                case DesignBlockKind.End: return EndNotchCenterX;
                case DesignBlockKind.FlowControl: return FlowSocketCenterX;
                default: return CommandSocketCenterX;
            }
        }

        /// <summary>
        /// 붙는 연출을 시작함. 블록은 목표 위치 아래에서 투명하게 시작해 스토리 라인 연출처럼 부드럽게(SmoothStep에 가까운 InOutSine) 올라오며
        /// 나타나 앞 블록에 맞물리고, 값 블록이 있으면 그다음에 오른쪽에서 왼쪽으로 미끄러져 와 소켓에 붙음. 진행 중이던 연출은 끝 상태로 건너뜀.
        /// </summary>
        public Sequence PlayAttach(Vector2 targetPosition, float riseHeight, float riseDuration, float valueSlideDistance, float valueSlideDuration)
        {
            CompleteMotion();
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = targetPosition - new Vector2(0f, riseHeight);
            _group.alpha = 0f;

            _motion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(rect.DOAnchorPos(targetPosition, riseDuration).SetEase(Ease.InOutSine))
                .Join(_group.DOFade(1f, riseDuration).SetEase(Ease.InOutSine));

            if (valueBody.gameObject.activeSelf)
            {
                RectTransform valueRect = (RectTransform)valueBody.transform;
                valueRect.anchoredPosition = ValueAttachedPosition + new Vector2(valueSlideDistance, 0f);
                valueGroup.alpha = 0f;

                _motion.Append(valueRect.DOAnchorPos(ValueAttachedPosition, valueSlideDuration).SetEase(Ease.OutCubic))
                    .Join(valueGroup.DOFade(1f, valueSlideDuration).SetEase(Ease.OutCubic));
            }

            return _motion;
        }

        /// <summary>
        /// 붙을 때와 반대로 아래로 가라앉으며 사라지는 빼기 연출을 재생함. 블록은 남겨 두므로 PlayAttach로 다시 붙일 수 있음.
        /// 붙는 연출 도중이면 다 붙은 자리로 건너뛰지 않고 지금 자리·알파에서 바로 가라앉음.
        /// </summary>
        public void PlayDrop(float sinkHeight, float duration)
        {
            _motion?.Kill();
            RectTransform rect = (RectTransform)transform;

            _motion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Join(rect.DOAnchorPos(rect.anchoredPosition - new Vector2(0f, sinkHeight), duration).SetEase(Ease.InOutSine))
                .Join(_group.DOFade(0f, duration).SetEase(Ease.InOutSine));
        }

        /// <summary> 빼기 연출(PlayDrop)을 재생한 뒤 블록을 파괴함. </summary>
        public void PlayDetachAndDestroy(float sinkHeight, float duration)
        {
            PlayDrop(sinkHeight, duration);
            _motion.OnComplete(() => Destroy(gameObject));
        }

        /// <summary> 연출 없이 위치를 바로 정함(처음 놓을 때나 배율·여백이 바뀌어 전체를 다시 놓을 때). </summary>
        public void SnapTo(Vector2 position)
        {
            CompleteMotion();
            ((RectTransform)transform).anchoredPosition = position;
        }

        /// <summary> 진행 중인 연출이 있으면 끝 상태로 건너뜀. </summary>
        private void CompleteMotion()
        {
            if (_motion != null && _motion.IsActive()) _motion.Complete(true);
            _motion = null;
        }

        /// <summary> 오브젝트 파괴 시 남은 연출을 정리함. </summary>
        private void OnDestroy()
        {
            _motion?.Kill();
        }
    }
}
