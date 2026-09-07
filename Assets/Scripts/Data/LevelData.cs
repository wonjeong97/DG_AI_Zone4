using UnityEngine;

namespace DGAIZone.Data
{
    /// <summary>
    /// 레벨별 공용 데이터. 2_LevelSelect(스토리 미리보기)와 3_Game(스토리 다시보기)이 같은 에셋을 참조해
    /// 스토리 텍스트를 한 곳에서만 관리함(Zone1의 LevelData와 동일한 방식).
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "DGAIZone/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Tooltip("2_LevelSelect와 3_Game(스토리 다시보기)에서 공통으로 사용하는 레벨 소개 텍스트(TMP 리치 텍스트 태그 포함)")]
        [TextArea(3, 10)]
        public string storyText;
    }
}
