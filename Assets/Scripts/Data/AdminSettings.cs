using System;
using DGAIZone.App;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/Admin.json 매핑 — 관리자 화면 비밀번호(숫자 4~6자리).
    /// 현장에서 파일을 고쳐 바꾸거나, 잊어버렸을 때 기본값으로 되돌릴 수 있게 JSON에 둠.
    /// </summary>
    [Serializable]
    public class AdminSettings
    {
        public string password = Constants.Admin.DefaultPassword;
    }
}
