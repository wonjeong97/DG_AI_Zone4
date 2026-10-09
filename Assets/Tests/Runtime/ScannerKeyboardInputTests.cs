using System.IO;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// QR 스캐너는 키보드처럼 글자와 Enter를 보내고 전시 내내 꽂혀 있으므로, 모든 씬의 UI가 키보드 이동·확인(Submit)을 받지 않아야 함.
    /// 받으면 터치로 누른 버튼(Navigation이 Automatic인 관리자 화면 버튼, 게임 화면의 설정하기·좌우 버튼 등)이 선택으로 남아
    /// 스캐너의 Enter에 한 번 더 눌리고, uid의 글자 W·A·S·D에 선택이 다른 버튼으로 옮겨 감.
    /// </summary>
    public class ScannerKeyboardInputTests
    {
        /// <summary>
        /// 빌드에 들어가는 씬의 EventSystem이 모두 Send Navigation Events를 끈 채 저장돼 있음
        /// (InputSystemUIInputModule은 이 값이 꺼져 있으면 Move·Submit·Cancel을 처리하지 않고, 터치·마우스 클릭은 그대로 받음).
        /// </summary>
        [Test]
        public void 빌드_씬의_EventSystem은_키보드_이동과_확인을_보내지_않는다()
        {
            UnityEditor.EditorBuildSettingsScene[] scenes = UnityEditor.EditorBuildSettings.scenes;
            Assert.IsNotEmpty(scenes, "빌드 씬 목록이 비어 있음");

            foreach (UnityEditor.EditorBuildSettingsScene buildScene in scenes)
            {
                string scene = File.ReadAllText(buildScene.path);
                StringAssert.Contains("m_sendNavigationEvents: 0", scene, $"{buildScene.path}의 EventSystem은 Send Navigation Events가 꺼져 있어야 함");
                StringAssert.DoesNotContain("m_sendNavigationEvents: 1", scene, $"{buildScene.path}에 키보드 이동·확인을 받는 EventSystem이 있으면 안 됨");
            }
        }
    }
}
