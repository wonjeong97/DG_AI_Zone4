using System.Collections.Generic;

namespace DGAIZone.App
{
    /// <summary>
    /// 씬 내에 활성화된 ISceneVideoReadiness 인스턴스들을 추적하는 정적 레지스트리입니다.
    /// SceneTransitionService에서 FindObjectsByType을 사용하는 병목을 제거하기 위해 도입되었습니다.
    /// </summary>
    public static class VideoReadinessRegistry
    {
        private static readonly HashSet<ISceneVideoReadiness> _activePanels = new HashSet<ISceneVideoReadiness>();

        public static IEnumerable<ISceneVideoReadiness> ActivePanels => _activePanels;

        public static void Register(ISceneVideoReadiness panel)
        {
            if (panel != null)
            {
                _activePanels.Add(panel);
            }
        }

        public static void Unregister(ISceneVideoReadiness panel)
        {
            if (panel != null)
            {
                _activePanels.Remove(panel);
            }
        }
    }
}
