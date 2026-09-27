using UnityEngine;

namespace DGAIZone.App
{
    /// <summary>
    /// 이름을 모르는 기존 자식에서 컴포넌트를 찾는 공용 유틸. GetComponentInChildren처럼 계층 전체를 순회하지 않고
    /// 직계 자식만 TryGetComponent로 확인하므로, 프리팹 구조가 바뀌어도 더 깊은 곳의 엉뚱한 컴포넌트를 조용히 잡지 않음.
    /// </summary>
    public static class ChildComponentFinder
    {
        /// <summary> parent의 직계 자식(비활성 포함) 중 T 컴포넌트를 가진 첫 번째 자식에서 컴포넌트를 찾아 반환함. </summary>
        public static bool TryGetInDirectChildren<T>(Transform parent, out T component) where T : Component
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).TryGetComponent(out component)) return true;
            }

            component = null;
            return false;
        }
    }
}
