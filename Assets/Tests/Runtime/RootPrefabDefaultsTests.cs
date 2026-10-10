using HuliacDev.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 루트 GameLifetimeScope 프리팹의 숨은 종료 버튼(GameCloser) 기본값이 Settings.json의 closeSetting과 같은지 검증함.
    /// Settings.json을 읽지 못하면 템플릿 대체 설정에 closeSetting이 없어 이 기본값이 그대로 쓰이는데, 템플릿 기본값(화면 가운데·흰색)이면
    /// 모든 화면 가운데에 흰 사각형이 떠 터치를 가리고 그 자리를 연타하면 앱이 꺼짐.
    /// </summary>
    public class RootPrefabDefaultsTests
    {
        [Test]
        public void 숨은_종료_버튼은_설정이_없어도_오른쪽_위_구석에_투명하게_있다()
        {
#if UNITY_EDITOR
            GameObject root = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameLifetimeScope.prefab");
            Assert.IsNotNull(root, "Assets/Prefabs/GameLifetimeScope.prefab을 찾지 못함");

            GameCloser[] closers = root.GetComponentsInChildren<GameCloser>(true);
            Assert.AreEqual(1, closers.Length, "루트 프리팹에 숨은 종료 버튼이 하나 있어야 함");

            RectTransform rect = (RectTransform)closers[0].transform;
            Assert.AreEqual(Vector2.one, rect.anchorMin, "앵커가 오른쪽 위여야 함");
            Assert.AreEqual(Vector2.one, rect.anchorMax, "앵커가 오른쪽 위여야 함");
            Assert.AreEqual(Vector2.one, rect.pivot, "피벗이 오른쪽 위여야 함");
            Assert.IsTrue(closers[0].TryGetComponent(out Image image), "종료 버튼에 Image가 있어야 함");
            Assert.AreEqual(0f, image.color.a, "종료 버튼은 보이지 않아야 함");
#else
            Assert.Ignore("프리팹은 에디터에서만 경로로 읽을 수 있음");
#endif
        }
    }
}
