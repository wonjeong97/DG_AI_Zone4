using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using HuliacDev.Utils;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DGAIZone.Data
{
    /// <summary>
    /// StreamingAssets/Json/Admin.json 매핑 — 관리자 화면 비밀번호(숫자 4~6자리)와 관리자 창 자동 닫기 시간·진입 클릭 수.
    /// 현장에서 파일을 고쳐 바꾸거나, 잊어버렸을 때 기본값으로 되돌릴 수 있게 JSON에 둠.
    /// 키가 빠진 예전 파일도 읽을 수 있도록 모든 값에 기본값을 둠.
    /// </summary>
    [Serializable]
    public class AdminSettings
    {
        /// <summary> JsonLoader에 넘기는 파일 경로(StreamingAssets 기준, 확장자 없이). </summary>
        public const string FilePath = Constants.ResourcePaths.SceneSettingsFolder + "/" + Constants.Admin.SettingsFileName;

        public string password = Constants.Admin.DefaultPassword;

        /// <summary> 관리자 화면·체험자 이름 입력 창을 입력 없이 두면 저장하지 않고 닫는 시간(초) — 열어 둔 채 자리를 뜨면 관람객이 설정을 바꿀 수 있음. </summary>
        public float idleCloseSeconds = Constants.Admin.DefaultIdleCloseSeconds;

        /// <summary> 비밀번호 창을 입력 없이 두면 닫는 시간(초). </summary>
        public float passwordIdleCloseSeconds = Constants.Admin.DefaultPasswordIdleCloseSeconds;

        /// <summary> 타이틀 왼쪽 위 숨은 버튼을 entryClickWindowSeconds초 안에 이 횟수만큼 누르면 비밀번호 창을 엶. </summary>
        public int entryClickCount = Constants.Admin.DefaultEntryClickCount;

        /// <summary> 연속 클릭으로 인정하는 시간(초) — 첫 클릭부터 이 시간이 지나면 다시 1회부터 셈. </summary>
        public float entryClickWindowSeconds = Constants.Admin.DefaultEntryClickWindowSeconds;

        /// <summary>
        /// Admin.json을 읽음. 시간·횟수가 1보다 작으면 경고를 남기고 기본값으로 바꿈
        /// (비밀번호 검사는 키패드 규칙을 아는 AdminPasswordPanel이 함).
        /// </summary>
        public static async UniTask<AdminSettings> LoadAsync(CancellationToken token, ILogger logger)
        {
            AdminSettings settings = await JsonLoader.LoadAsync<AdminSettings>(FilePath, token, logger);

            // 로드가 끝난 직후 취소된 경우에도 파괴된 창에 비밀번호·시간을 넣지 않도록 취소를 전달함(로드 중 취소는 JsonLoader가 던짐)
            token.ThrowIfCancellationRequested();
            if (settings.ClampToValid() && logger != null)
                logger.ZLogWarning($"[AdminSettings] Admin.json의 시간·횟수 값이 1보다 작아 그 값은 기본값을 씀.");
            return settings;
        }

        /// <summary>
        /// 비밀번호를 저장하기 전에 Admin.json을 보정 없이 읽음 — 파일이 있는데 읽을 수 없거나(잠김 등) 형식이 깨졌으면 false를 돌려 다른 값이 기본값으로 덮어써지지 않게 함.
        /// </summary>
        public static bool TryReadForSave(out AdminSettings settings, ILogger logger)
        {
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath, FilePath + ".json");
            if (!File.Exists(path))
            {
                settings = new AdminSettings();
                return true;
            }

            try
            {
                // 비어 있으면 FromJson이 null을 돌려줌 — 지킬 값이 없으므로 기본값에 비밀번호만 넣어 저장해도 됨
                settings = UnityEngine.JsonUtility.FromJson<AdminSettings>(File.ReadAllText(path)) ?? new AdminSettings();
                return true;
            }
            catch (Exception e)
            {
                // 파일이 잠겼는지(IOException) 형식이 깨졌는지 로그로 구분할 수 있게 원인을 남김(저장 거부는 호출부가 알림)
                if (logger != null) logger.ZLogWarning($"[AdminSettings] 비밀번호를 저장하기 전에 Admin.json을 읽지 못함: {e.Message}");
                settings = null;
                return false;
            }
        }

        /// <summary>
        /// 1보다 작은 시간·횟수를 기본값으로 바꿈 — 창이 열리자마자 닫히거나 숨은 버튼 진입이 동작하지 않는 일을 막음.
        /// 바꾼 값이 있으면 true를 돌려줌.
        /// </summary>
        public bool ClampToValid()
        {
            bool changed = false;
            if (idleCloseSeconds < 1f) { idleCloseSeconds = Constants.Admin.DefaultIdleCloseSeconds; changed = true; }
            if (passwordIdleCloseSeconds < 1f) { passwordIdleCloseSeconds = Constants.Admin.DefaultPasswordIdleCloseSeconds; changed = true; }
            if (entryClickCount < 1) { entryClickCount = Constants.Admin.DefaultEntryClickCount; changed = true; }
            if (entryClickWindowSeconds < 1f) { entryClickWindowSeconds = Constants.Admin.DefaultEntryClickWindowSeconds; changed = true; }
            return changed;
        }
    }
}
