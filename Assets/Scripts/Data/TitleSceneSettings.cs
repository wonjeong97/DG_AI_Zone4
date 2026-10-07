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
        public float qrFadeDuration = 1.0f;

        /// <summary> QR 안내 깜빡임의 최소 알파. </summary>
        public float qrBlinkMinAlpha = 0.3f;

        /// <summary> 서버 모드에서 QR을 찍은 뒤 'QR 코드를 확인하고 있습니다'를 서버가 빨리 답해도 보여 주는 최소 시간(초). </summary>
        public float qrCheckingMinSeconds = 1.0f;

        /// <summary> 서버 모드에서 체험할 수 없는 QR(완료·미등록·확인 불가) 안내를 보여 준 뒤 QR 대기로 돌아가기까지의 시간(초). </summary>
        public float scanResultMessageSeconds = 3.0f;
    }
}
