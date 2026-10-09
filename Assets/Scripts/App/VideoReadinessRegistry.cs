using System.Collections.Generic;

namespace DGAIZone.App
{
    /// <summary>
    /// 씬 안에 활성화된 ISceneVideoReadiness 인스턴스들을 추적하는 정적 레지스트리.
    /// SceneTransitionService가 FindObjectsByType으로 찾는 비용을 없애려고 둠.
    /// </summary>
    public static class VideoReadinessRegistry
    {
        private static readonly HashSet<ISceneVideoReadiness> _activePanels = new HashSet<ISceneVideoReadiness>();

        /// <summary> 현재 활성화되어 있는 영상 패널 목록. </summary>
        public static IEnumerable<ISceneVideoReadiness> ActivePanels => _activePanels;

        /// <summary> 영상 패널을 씬 전환 대기 대상으로 등록함. </summary>
        public static void Register(ISceneVideoReadiness panel)
        {
            if (panel != null)
            {
                _activePanels.Add(panel);
            }
        }

        /// <summary> 영상 패널을 씬 전환 대기 대상에서 제외함. </summary>
        public static void Unregister(ISceneVideoReadiness panel)
        {
            if (panel != null)
            {
                _activePanels.Remove(panel);
            }
        }
    }
}
