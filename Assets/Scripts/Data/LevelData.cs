using System;
using UnityEngine;

namespace DGAIZone.Data
{
    /// <summary> 레벨 1 목적지 하나. 추진력(엔진 출력량 + 연료량 - 탑재 중량)이 targetDistance와 정확히 같으면 미션 성공. </summary>
    [Serializable]
    public class MissionDestination
    {
        [Tooltip("화면 표시용 행성 이름(예: 화성). 미션 문구의 {planet}과 목표 표시(Text_GoalPlanetName)에 쓰임")]
        public string planetName;

        [Tooltip("목표 거리. 미션 문구의 {distance}에 쓰이며, 추진력(엔진 + 연료 - 탑재)이 이 값과 정확히 같으면 성공. 블록 조합으로 만들 수 있는 값이어야 함")]
        public int targetDistance;

        [Tooltip("목표 이미지(Image_Goal)를 불러올 Addressables 주소")]
        public string spriteKey;
    }

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

        [Tooltip("3_Game 씬의 미션 보드(Image_MissionBoard)에 표시되는 미션 안내 텍스트(TMP 리치 텍스트 태그 및 플레이스홀더 포함)")]
        [TextArea(3, 10)]
        public string missionText;

        [Header("레벨 1 전용")]
        [Tooltip("게임마다 이 중 하나를 무작위로 골라 목적지로 씀")]
        public MissionDestination[] destinations;

        [Header("레벨 3 전용")]
        [Tooltip("전기량 상한 후보 범위(x=최소, y=최대, 양끝 포함). 게임마다 이 범위에서 무작위로 정하며, RfidMappings.json '만약 전기량이' 블록의 value 중 하나여야 정답을 고를 수 있음")]
        public Vector2Int maxElectricityRange = new Vector2Int(3, 5);

        [Tooltip("산소량 하한 후보 범위(x=최소, y=최대, 양끝 포함). 게임마다 이 범위에서 무작위로 정하며, RfidMappings.json '만약 산소량이' 블록의 value 중 하나여야 정답을 고를 수 있음")]
        public Vector2Int minOxygenRange = new Vector2Int(3, 5);
    }
}
