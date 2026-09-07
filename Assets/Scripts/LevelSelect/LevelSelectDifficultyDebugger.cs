using UnityEngine;
using UnityEngine.InputSystem;

namespace DGAIZone.LevelSelect
{
    /// <summary>
    /// 2_LevelSelect 씬에서 난이도(별 개수 및 패널 너비)를 실시간으로 조절하며 확인할 수 있는 디버그 컴포넌트.
    /// 인스펙터 슬라이더, 화면 내 GUI 버튼, 키보드 단축키(1~5, -/+)를 통해 즉시 별 개수를 변경할 수 있음.
    /// </summary>
    public class LevelSelectDifficultyDebugger : MonoBehaviour
    {
        [Header("Target Controller")]
        [SerializeField] private LevelSelectFlowController flowController;

        [Header("Debug Star Settings")]
        [Range(1, 5)]
        [SerializeField] private int starCount = 1;

        [Tooltip("체크 시 레벨 버튼(1~5)의 잠금 상태도 별 개수에 맞춰 함께 갱신함")]
        [SerializeField] private bool syncLevelButtonLocks = true;

        [Header("Runtime Controls")]
        [Tooltip("게임 화면 좌측 상단에 별 개수 조절 GUI 창 표시 여부 (F1 또는  키로 토글 가능)")]
        [SerializeField] private bool showOnScreenGui = true;

        [Tooltip("키보드 숫자키(1~5) 및 -/+ 키로 별 개수 조절 가능 여부")]
        [SerializeField] private bool enableKeyboardShortcuts = true;

        private void Reset()
        {
            if (flowController == null)
            {
                flowController = FindAnyObjectByType<LevelSelectFlowController>();
            }
        }

        private void Start()
        {
            if (flowController == null)
            {
                flowController = FindAnyObjectByType<LevelSelectFlowController>();
            }

            // 시작 시 디버그 설정값으로 화면 적용
            if (flowController != null)
            {
                ApplyCurrentStarCount();
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // F1 또는  (Backquote) 키로 화면 GUI 토글
            if (keyboard.f1Key.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame)
            {
                showOnScreenGui = !showOnScreenGui;
            }

            if (!enableKeyboardShortcuts) return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) SetStarCount(1);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) SetStarCount(2);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) SetStarCount(3);
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) SetStarCount(4);
            else if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) SetStarCount(5);
            else if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame) SetStarCount(starCount - 1);
            else if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame) SetStarCount(starCount + 1);
        }

        private void OnValidate()
        {
            if (Application.isPlaying && flowController != null)
            {
                ApplyCurrentStarCount();
            }
        }

        /// <summary> 원하는 별 개수(1~5)로 설정하고 화면을 갱신함. </summary>
        public void SetStarCount(int count)
        {
            starCount = Mathf.Clamp(count, 1, 5);
            ApplyCurrentStarCount();
        }

        private void ApplyCurrentStarCount()
        {
            if (flowController == null) return;

            if (syncLevelButtonLocks)
            {
                flowController.ApplyLevelButtonLocks(starCount);
            }
            else
            {
                flowController.ApplyDifficulty(starCount);
            }
        }

        private void OnGUI()
        {
            if (!showOnScreenGui) return;

            const float width = 320f;
            const float height = 105f;
            GUILayout.BeginArea(new Rect(20f, 20f, width, height), "★ 난이도 디버그 (F1 토글)", GUI.skin.window);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"별 개수: {starCount}", GUILayout.Width(75));
            for (int i = 1; i <= 5; i++)
            {
                Color prevColor = GUI.color;
                if (starCount == i) GUI.color = Color.yellow;
                if (GUILayout.Button($"{i}", GUILayout.Width(35), GUILayout.Height(26)))
                {
                    SetStarCount(i);
                }
                GUI.color = prevColor;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool newSync = GUILayout.Toggle(syncLevelButtonLocks, "버튼 잠금 연동");
            if (newSync != syncLevelButtonLocks)
            {
                syncLevelButtonLocks = newSync;
                ApplyCurrentStarCount();
            }
            if (GUILayout.Button("◀ -1", GUILayout.Width(50), GUILayout.Height(22))) SetStarCount(starCount - 1);
            if (GUILayout.Button("+1 ▶", GUILayout.Width(50), GUILayout.Height(22))) SetStarCount(starCount + 1);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        [ContextMenu("★ 1 Star")] public void Set1() => SetStarCount(1);
        [ContextMenu("★★ 2 Stars")] public void Set2() => SetStarCount(2);
        [ContextMenu("★★★ 3 Stars")] public void Set3() => SetStarCount(3);
        [ContextMenu("★★★★ 4 Stars")] public void Set4() => SetStarCount(4);
        [ContextMenu("★★★★★ 5 Stars")] public void Set5() => SetStarCount(5);
    }
}