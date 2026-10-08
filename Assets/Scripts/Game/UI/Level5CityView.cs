using System;
using System.Collections.Generic;
using DG.Tweening;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 레벨 5 현재 상황 화면(Panel_Level5)의 우주 도시 그림. 동작 블록마다 맞는 그림이 하나씩 있고(우주 정거장 코드 → 돔 기지, 탐사 로봇 코드 → 로버,
    /// 통신 시스템 코드 → 통신탑, 연결 통로 코드 → 연결 통로), 그 블록이 설계창 함수 정의 블록 안쪽에 있는 동안만 보임. 그림은 씬에서 꺼 둔 채로 시작하고,
    /// 나타날 때는 블록이 다 붙은 뒤에 페이드인하며 사라질 때는 바로 꺼짐. 어떤 블록이 함수 정의 블록 안쪽에 있는지와 기다릴 시간은 IngredientLevel5State가 정함.
    /// </summary>
    public class Level5CityView : MonoBehaviour
    {
        [SerializeField] private Image imageDome;     // Image_Dome — 우주 정거장 코드
        [SerializeField] private Image imageRover;    // Image_Rover — 탐사 로봇 코드
        [SerializeField] private Image imageTower;    // Image_Tower — 통신 시스템 코드
        [SerializeField] private Image imageCorridor; // Image_Corridor — 연결 통로 코드
        [SerializeField] private float fadeInDuration = 0.5f; // 그림이 나타날 때 페이드인 시간(초)

        private ILogger<Level5CityView> _logger;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<Level5CityView> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// matterIds에 든 동작 블록의 그림만 보이게 함. 새로 보일 그림은 appearDelay초(블록이 다 붙는 시간) 뒤에 페이드인하고, 빠진 그림은 바로 끔.
        /// 그림이 정해지지 않은 블록 id는 경고를 남김.
        /// </summary>
        public void ShowOnly(IReadOnlyList<string> matterIds, float appearDelay)
        {
            SetVisible(imageDome, nameof(imageDome), Contains(matterIds, Constants.RfidIds.Level5.SpaceStationCode), appearDelay);
            SetVisible(imageRover, nameof(imageRover), Contains(matterIds, Constants.RfidIds.Level5.ExplorerRobotCode), appearDelay);
            SetVisible(imageTower, nameof(imageTower), Contains(matterIds, Constants.RfidIds.Level5.CommunicationCode), appearDelay);
            SetVisible(imageCorridor, nameof(imageCorridor), Contains(matterIds, Constants.RfidIds.Level5.ConnectionPassageCode), appearDelay);

            for (int i = 0; i < matterIds.Count; i++)
            {
                if (!HasImage(matterIds[i]) && _logger != null)
                {
                    _logger.ZLogWarning($"[Level5CityView] '{matterIds[i]}' 동작 블록에 맞는 그림이 정해지지 않아 현재 상황 화면에 보여 줄 수 없음.");
                }
            }
        }

        /// <summary>
        /// 그림을 켜거나 끔. 켤 때는 투명한 채로 켜 두고 appearDelay초 뒤에 페이드인하며(블록 연출과 같이 시간 배율 영향 없음), 끌 때는 기다리거나
        /// 진행 중이던 페이드인을 멈추고 바로 끔. 이미 그 상태면 그대로 둠.
        /// </summary>
        private void SetVisible(Image image, string fieldName, bool visible, float appearDelay)
        {
            if (!image)
            {
                if (_logger != null) _logger.ZLogWarning($"[Level5CityView] {fieldName}이 null이라 그림을 {(visible ? "보여 줄" : "숨길")} 수 없음.");
                return;
            }

            GameObject target = image.gameObject;
            if (target.activeSelf == visible) return;

            image.DOKill();
            target.SetActive(visible);
            if (!visible) return;

            Color color = image.color;
            color.a = 0f;
            image.color = color;
            image.DOFade(1f, fadeInDuration).SetDelay(appearDelay).SetUpdate(true).SetLink(target);
        }

        /// <summary> 테스트 전용: 인스펙터로 연결하는 그림 네 장을 넣음. </summary>
        internal void SetImagesForTest(Image dome, Image rover, Image tower, Image corridor)
        {
            imageDome = dome;
            imageRover = rover;
            imageTower = tower;
            imageCorridor = corridor;
        }

        /// <summary> ids에 id가 들어 있는지 반환함. </summary>
        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary> 동작 블록 id에 맞는 현재 상황 그림이 정해져 있는지 반환함. </summary>
        private static bool HasImage(string matterId)
        {
            switch (matterId)
            {
                case Constants.RfidIds.Level5.SpaceStationCode:
                case Constants.RfidIds.Level5.ExplorerRobotCode:
                case Constants.RfidIds.Level5.CommunicationCode:
                case Constants.RfidIds.Level5.ConnectionPassageCode:
                    return true;
                default:
                    return false;
            }
        }
    }
}
