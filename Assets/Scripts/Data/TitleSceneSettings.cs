using System;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/0_Title.json 매핑 — 0_Title 씬(TitleFlowController)의 연출 타이밍을 재빌드 없이 조정.
    /// </summary>
    [Serializable]
    public class TitleSceneSettings
    {
        /// <summary> QR 안내(Image_QR)가 원래 색~최소 알파 사이를 오가는 한쪽 방향 페이드 시간(초). </summary>
        public float qrFadeDuration = 1.2f;

        /// <summary> QR 안내 깜빡임의 최소 알파. </summary>
        public float qrBlinkMinAlpha = 0.3f;
    }
}
