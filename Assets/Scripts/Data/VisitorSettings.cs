using UnityEngine;

namespace DGAIZone.Data
{
    /// <summary>
    /// 운영 모드(로컬/서버 연동)와 로컬 모드에서 화면에 쓰는 체험자 이름.
    /// 에셋 값은 기본값이고, 관리자 화면에서 바꾼 값은 PlayerPrefs에 저장해 그 값을 우선함
    /// (빌드에서는 런타임에 SO를 바꿔도 앱을 다시 켜면 에셋 값으로 돌아가므로).
    /// Addressables 주소 Constants.ResourcePaths.VisitorSettingsKey로 루트 스코프가 불러와 등록함.
    /// </summary>
    [CreateAssetMenu(fileName = "VisitorSettings", menuName = "DGAIZone/Visitor Settings")]
    public class VisitorSettings : ScriptableObject
    {
        public const string ServerConnectedKey = "VisitorSettings.IsServerConnected";
        public const string VisitorNameKey     = "VisitorSettings.VisitorName";

        [Tooltip("서버 연동(QR 인식) 모드 기본값 — 관리자 화면에서 바꾼 값이 있으면 그 값을 씀")]
        [SerializeField] private bool defaultServerConnected;

        [Tooltip("로컬 모드에서 표시할 체험자 이름 기본값 — 관리자 화면에서 바꾼 값이 있으면 그 값을 씀")]
        [SerializeField] private string defaultVisitorName = "체험자";

        /// <summary> 서버 연동(QR 인식) 모드인지. 바꾸면 PlayerPrefs에 바로 저장함. </summary>
        public bool IsServerConnected
        {
            get => PlayerPrefs.GetInt(ServerConnectedKey, defaultServerConnected ? 1 : 0) != 0;
            set
            {
                PlayerPrefs.SetInt(ServerConnectedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary> 로컬 모드 체험자 이름. 바꾸면 PlayerPrefs에 바로 저장함. </summary>
        public string VisitorName
        {
            get => PlayerPrefs.GetString(VisitorNameKey, defaultVisitorName);
            set
            {
                PlayerPrefs.SetString(VisitorNameKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary> 관리자 화면에서 바꾼 값을 지워 에셋 기본값으로 되돌림 — 에디터에서 기본값을 바꿔도 반영되지 않을 때 씀. </summary>
        [ContextMenu("관리자 화면 변경값 지우기")]
        public void ClearOverrides()
        {
            PlayerPrefs.DeleteKey(ServerConnectedKey);
            PlayerPrefs.DeleteKey(VisitorNameKey);
            PlayerPrefs.Save();
        }
    }
}
