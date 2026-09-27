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
