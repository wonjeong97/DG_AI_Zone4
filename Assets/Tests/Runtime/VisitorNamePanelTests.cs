using System.Collections;
using System.Collections.Generic;
using DGAIZone.Admin;
using DGAIZone.App;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 관리자 이름 바꾸기 창은 화면 키보드로만 입력받으므로, 입력란을 직접 터치해도 TMP_InputField가 눌리지 않는지 실제 AdminCanvas 프리팹으로 검증함.
    /// 입력란이 켜질 때(OnEnable) 새로 만드는 커서(Caret)까지 막혀야 함.
    /// </summary>
    public class VisitorNamePanelTests
    {
        private GameObject _eventSystemGo;
        private EventSystem _eventSystem;
        private GameObject _adminCanvas;

        /// <summary> 레이캐스트에 필요한 EventSystem을 준비함. </summary>
        [SetUp]
        public void SetUp()
        {
            _eventSystemGo = new GameObject("VisitorNamePanelTests_EventSystem", typeof(EventSystem));
            _eventSystemGo.TryGetComponent(out _eventSystem);
        }

        /// <summary> 테스트가 만든 오브젝트를 모두 파괴함. </summary>
        [TearDown]
        public void TearDown()
        {
            if (_adminCanvas) Object.Destroy(_adminCanvas);
            if (_eventSystemGo) Object.Destroy(_eventSystemGo);
        }

        /// <summary> 이름 창을 열고 입력란 가운데를 누르면, 맨 위에 맞은 UI가 TMP_InputField로 눌림을 넘기지 않아야 함. </summary>
        [UnityTest]
        public IEnumerator 이름_입력란을_직접_터치해도_입력란이_눌리지_않는다()
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AdminCanvas.prefab");
            Assert.IsNotNull(prefab, "AdminCanvas 프리팹을 찾지 못함");
            _adminCanvas = Object.Instantiate(prefab);

            VisitorNamePanel panel = _adminCanvas.GetComponentInChildren<VisitorNamePanel>(true);
            Assert.IsNotNull(panel, "VisitorNamePanel이 없음");
            panel.Open(null, Constants.Admin.DefaultIdleCloseSeconds);
            yield return null; // 레이아웃과 커서 위치가 잡히도록 한 프레임 기다림

            TMP_InputField inputField = panel.GetComponentInChildren<TMP_InputField>(true);
            Assert.IsNotNull(inputField, "이름 입력란이 없음");
            Assert.IsNotNull(inputField.textViewport, "입력란의 Text Area가 없음");

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, inputField.textViewport.position);
            PointerEventData pointer = new PointerEventData(_eventSystem) { position = screen };
            List<RaycastResult> results = new List<RaycastResult>();
            _eventSystem.RaycastAll(pointer, results);

            Assert.IsNotEmpty(results, "입력란 위치에 맞은 UI가 없음(창 뒤를 막는 Dim이 있어야 함)");
            GameObject pressed = ExecuteEvents.GetEventHandler<IPointerDownHandler>(results[0].gameObject);
            Assert.AreNotSame(inputField.gameObject, pressed, $"입력란을 터치하면 '{results[0].gameObject.name}'이(가) 맞아 TMP_InputField가 눌림(커서를 옮기거나 글자를 고를 수 있음)");
        }
    }
}
