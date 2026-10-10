using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 같은 오브젝트의 ScrollRect를 사용자가 손가락으로 끌고 있는지 기억함. 끄는 동안 코드가 스크롤을 움직이면 ScrollRect의 드래그 계산과 겹쳐
    /// 화면이 떨리고 손을 뗄 때 튀므로, 자동 스크롤 쪽(DesignPanel)이 이 값을 보고 손을 뗄 때까지 미룸.
    /// 이벤트는 같은 오브젝트의 ScrollRect와 함께 받음(EventSystem은 대상 오브젝트의 모든 핸들러 컴포넌트에 보냄).
    /// </summary>
    public class ScrollDragTracker : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        /// <summary> 지금 사용자가 끌고 있는지 여부. </summary>
        public bool IsDragging { get; private set; }

        /// <summary> 끌기를 시작했을 때 알림. </summary>
        public event Action DragStarted;

        /// <summary> 끌기가 끝났을 때(손을 떼거나 오브젝트가 꺼짐) 알림. </summary>
        public event Action DragEnded;

        /// <summary> ScrollRect와 같은 조건(왼쪽 버튼·터치)으로 끌기 시작을 기록함. </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return; // ScrollRect도 왼쪽 버튼(터치) 끌기만 스크롤함(정상)

            IsDragging = true;
            DragStarted?.Invoke();
        }

        /// <summary> 손을 떼 끌기가 끝났음을 기록함. </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            EndDrag();
        }

        /// <summary> 끄는 도중 패널이 꺼지면 OnEndDrag가 오지 않을 수 있어 끝난 것으로 처리함. </summary>
        private void OnDisable()
        {
            EndDrag();
        }

        /// <summary> 끄는 중이었으면 끝난 것으로 바꾸고 알림. </summary>
        private void EndDrag()
        {
            if (!IsDragging) return; // 끌고 있지 않았으면 알릴 것이 없음(정상)

            IsDragging = false;
            DragEnded?.Invoke();
        }
    }
}
