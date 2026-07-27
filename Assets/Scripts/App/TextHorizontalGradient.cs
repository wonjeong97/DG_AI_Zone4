using TMPro;
using UnityEngine;

namespace DGAIZone.App
{
    /// <summary>
    /// TMP 텍스트 전체 가로 범위 기준으로 하나로 이어지는 좌우 그라데이션을 적용하는 컴포넌트.
    /// TMP 기본 Vertex Gradient(Character 모드)는 글자마다 반복 적용되므로, 문자별 정점의 실제 x좌표를
    /// 텍스트 전체 폭 대비 위치로 환산해 직접 정점 색을 계산함.
    /// OnEnable 시점에 한 번만 계산하므로 정적 텍스트 대상이며, 런타임에 텍스트 내용이 바뀌는 대상이라면
    /// 내용이 바뀔 때마다 Apply()를 다시 호출해줘야 함.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextHorizontalGradient : MonoBehaviour
    {
        [SerializeField] private Color leftColor = new Color(224f / 255f, 43f / 255f, 246f / 255f); // #E02BF6
        [SerializeField] private Color rightColor = new Color(21f / 255f, 214f / 255f, 248f / 255f); // #15D6F8

        private TMP_Text _text;

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            Apply();
        }

        /// <summary> 현재 텍스트의 가로 범위를 기준으로 leftColor -> rightColor 정점 그라데이션을 다시 계산해 적용함. </summary>
        public void Apply()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text == null) return;

            _text.ForceMeshUpdate();
            TMP_TextInfo textInfo = _text.textInfo;
            int charCount = textInfo.characterCount;
            if (charCount == 0) return;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            for (int i = 0; i < charCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                minX = Mathf.Min(minX, charInfo.bottomLeft.x);
                maxX = Mathf.Max(maxX, charInfo.topRight.x);
            }

            float width = maxX - minX;
            if (width <= 0f) return;

            for (int i = 0; i < charCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                int matIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;
                Vector3[] vertices = textInfo.meshInfo[matIndex].vertices;
                Color32[] colors = textInfo.meshInfo[matIndex].colors32;

                for (int k = 0; k < 4; k++)
                {
                    float t = Mathf.Clamp01((vertices[vertexIndex + k].x - minX) / width);
                    Color blended = Color.Lerp(leftColor, rightColor, t);
                    byte alpha = colors[vertexIndex + k].a;
                    colors[vertexIndex + k] = new Color32((byte)(blended.r * 255f), (byte)(blended.g * 255f), (byte)(blended.b * 255f), alpha);
                }
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
