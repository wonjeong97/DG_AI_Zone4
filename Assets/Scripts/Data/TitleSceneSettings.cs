using System;
using DGAIZone.App;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/0_Title.json 매핑 — 0_Title 씬(TitleFlowController)의 연출 타이밍과 하단 안내 문구를 재빌드 없이 조정.
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

        /// <summary>
        /// QR 스캐너 글자 사이 최대 간격(초). 이보다 벌어지면 앞에 모은 글자(찍기 전에 눌린 키 등)를 버리고, Enter가 이보다 늦게 오면 QR로 보지 않음.
        /// PC가 느려 스캔 글자가 늦게 들어오면 늘림. 0 이하면 기본값 0.5초를 씀.
        /// 안내가 바뀌어 QR을 다시 받기 시작한 뒤 이 시간 안에 찍은 QR은 앞 스캔의 뒷부분으로 보고 버리므로, 너무 크게 늘리지 않음.
        /// </summary>
        public float scanCharGapSeconds = 0.5f;

        // 하단 안내 문구 — QR 대기, 시작하기(로컬 모드·이름 없음), QR로 확인한 체험자의 시작하기({name}은 체험자 이름)
        public string qrGuideText = Constants.TitleMessages.QrGuide;
        public string startGuideText = Constants.TitleMessages.StartGuide;
        public string startGuideWithNameText = Constants.TitleMessages.StartGuideWithName;

        // 서버 모드 QR 확인 안내 — 확인 중, 이미 체험 완료, 등록되지 않은 QR, 서버 오류(확인 중을 빼면 잠시 보여 준 뒤 QR 대기로 돌아감)
        public string qrCheckingText = Constants.TitleMessages.QrChecking;
        public string qrCompletedText = Constants.TitleMessages.QrCompleted;
        public string qrNotFoundText = Constants.TitleMessages.QrNotFound;
        public string qrCheckFailedText = Constants.TitleMessages.QrCheckFailed;
    }
}
