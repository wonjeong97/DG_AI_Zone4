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
        End             // 완성하기(진회색, 위 홈)
    }

    /// <summary>
    /// 설계창에 쌓는 블록 하나(몸통 이미지 + 라벨, 명령 블록은 오른쪽 값 블록 포함)의 표시와 쌓기·빼기 연출을 맡음.
    /// 이미지는 원본 픽셀 크기(1px = UI 1단위)로 두고, 크기 조절은 DesignPanel이 블록 전체 localScale로 함.
    /// 좌표는 모두 블록 왼쪽 위를 기준으로 하며, 아래 값들은 블록 이미지(Zone1 블록 아트)에서 잰 값임.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class DesignBlockView : MonoBehaviour
    {
        // 위 홈·아래 돌기의 가로 중심(px). 다음 블록의 홈 중심을 이전 블록의 돌기 중심에 맞춰 놓아야 맞물림
        private const float StartTabCenterX = 60f;
        private const float CommandSocketCenterX = 40.5f; // 명령 블록 두 종류는 위 홈과 아래 돌기가 같은 위치
        private const float EndNotchCenterX = 60f;

        // 몸통 높이(px). 이미지 높이에서 아래 돌기를 뺀 값으로, 다음 블록이 놓이는 위치가 됨
        private const float StartBodyHeight = 100f;
        private const float CommandBodyHeight = 101f;
        private const float EndBodyHeight = 101f;

        /// <summary> 아래 돌기 높이(px). 명령 블록 이미지 높이 121 = 몸통 101 + 이 값(시작하기는 몸통 100 아래에 같은 높이의 돌기). </summary>
        public const float BottomTabHeight = 20f;

        private const float CommandSocketWidth = 20f; // 명령 블록 오른쪽 값 소켓 돌기 폭(이미지 폭 381 = 몸통 361 + 20)
        private const float ValueNotchWidth = 20f;    // 값 블록 왼쪽 홈 깊이
        private const float LabelPadding = 12f;       // 라벨과 블록 테두리 사이 여백

        [SerializeField] private Image body;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Image valueBody; // 명령 블록 오른쪽 소켓에 끼우는 값 블록
        [SerializeField] private TMP_Text valueLabel;

        [Header("Block Sprites")]
        [SerializeField] private Sprite startSprite;
        [SerializeField] private Sprite commandSprite;
        [SerializeField] private Sprite commandNoValueSprite;
        [SerializeField] private Sprite endSprite;
        [SerializeField] private Sprite valueSprite;

        private CanvasGroup _group;
        private Sequence _motion;
        private DesignBlockKind _kind;

        /// <summary> 이 블록의 종류. </summary>
        public DesignBlockKind Kind => _kind;

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

        /// <summary> 종류에 맞는 이미지와 라벨을 적용하고 이미지 원본 크기로 맞춤. 값 블록은 Command 종류에 값이 있을 때만 보임. </summary>
        public void Setup(DesignBlockKind kind, string label, string value)
        {
            _kind = kind;

            body.sprite = SpriteOf(kind);
            body.SetNativeSize();
            ((RectTransform)transform).sizeDelta = ((RectTransform)body.transform).sizeDelta;

            float imageHeight = ((RectTransform)body.transform).sizeDelta.y;
            float tabHeight = imageHeight - BodyHeightOf(kind);
            float rightInset = LabelPadding + (kind == DesignBlockKind.Command ? CommandSocketWidth : 0f);
            SetLabelArea(bodyLabel, LabelPadding, rightInset, tabHeight + LabelPadding, LabelPadding);
            bodyLabel.text = label;

            bool hasValue = kind == DesignBlockKind.Command && !string.IsNullOrEmpty(value);
            valueBody.gameObject.SetActive(hasValue);
            if (hasValue)
            {
                valueBody.sprite = valueSprite;
                valueBody.SetNativeSize();
                RectTransform valueRect = (RectTransform)valueBody.transform;
                valueRect.anchoredPosition = new Vector2(CommandWidthWithoutSocket, 0f);
                SetLabelArea(valueLabel, ValueNotchWidth + LabelPadding, LabelPadding, LabelPadding, LabelPadding);
                valueLabel.text = value;
            }
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
                default: return endSprite;
            }
        }

        /// <summary> 종류별 몸통 높이(px). 다음 블록은 이 블록 위치에서 이만큼 아래에 놓임. </summary>
        public static float BodyHeightOf(DesignBlockKind kind)
        {
            switch (kind)
            {
                case DesignBlockKind.Start: return StartBodyHeight;
                case DesignBlockKind.End: return EndBodyHeight;
                default: return CommandBodyHeight;
            }
        }

        /// <summary> 종류별 아래 돌기의 가로 중심(px). 완성하기 블록은 아래 돌기가 없어 쓰지 않음. </summary>
        public static float TabCenterXOf(DesignBlockKind kind)
        {
            return kind == DesignBlockKind.Start ? StartTabCenterX : CommandSocketCenterX;
        }

        /// <summary> 종류별 위 홈의 가로 중심(px). 시작하기 블록은 위 홈이 없어 쓰지 않음. </summary>
        public static float NotchCenterXOf(DesignBlockKind kind)
        {
            return kind == DesignBlockKind.End ? EndNotchCenterX : CommandSocketCenterX;
        }

        /// <summary> 목표 위치 위쪽에서 투명하게 시작해 목표 위치로 내려와 맞물리는 쌓기 연출을 시작함. 진행 중이던 연출은 끝 상태로 건너뜀. </summary>
        public Sequence PlayDropIn(Vector2 targetPosition, float dropHeight, float duration)
        {
            CompleteMotion();
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = targetPosition + new Vector2(0f, dropHeight);
            _group.alpha = 0f;

            _motion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Join(rect.DOAnchorPos(targetPosition, duration).SetEase(Ease.OutBack))
                .Join(_group.DOFade(1f, duration * 0.6f));
            return _motion;
        }

        /// <summary> 위로 떠오르며 사라지는 빼기 연출을 재생한 뒤 블록을 파괴함. </summary>
        public void PlayRemoveAndDestroy(float riseHeight, float duration)
        {
            CompleteMotion();
            RectTransform rect = (RectTransform)transform;

            _motion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Join(rect.DOAnchorPos(rect.anchoredPosition + new Vector2(0f, riseHeight), duration).SetEase(Ease.InQuad))
                .Join(_group.DOFade(0f, duration))
                .OnComplete(() => Destroy(gameObject));
        }

        /// <summary> 연출 없이 위치를 바로 정함(배치 방식이 바뀌어 전체를 다시 놓을 때). </summary>
        public void SnapTo(Vector2 position)
        {
            CompleteMotion();
            ((RectTransform)transform).anchoredPosition = position;
        }

        /// <summary> 흐림 표시 여부에 따라 알파를 정함(카드가 떨어져 값이 불확실해진 단계). </summary>
        public void SetDimmed(bool dimmed, float dimmedAlpha)
        {
            CompleteMotion();
            _group.alpha = dimmed ? dimmedAlpha : 1f;
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
