using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 미션 보드 텍스트를 구성하는 컨트롤러. 씬 시작 시 목적지(달/화성/외계 행성)를 무작위로 정하고,
    /// 목적지에 맞는 연료량 조건을 함께 표시함.
    /// </summary>
    public class MissionBoardController : MonoBehaviour
    {
        private readonly struct Mission
        {
            public readonly string Destination;
            public readonly string FuelRequirement;
            public readonly int MinFuel;
            public readonly int MaxFuel;

            public Mission(string destination, string fuelRequirement, int minFuel, int maxFuel)
            {
                Destination = destination;
                FuelRequirement = fuelRequirement;
                MinFuel = minFuel;
                MaxFuel = maxFuel;
            }
        }

        // 목적지별 연료량 조건(포함 범위)
        private static readonly Mission[] Missions =
        {
            new Mission("달", "3보다 적은 연료량", 0, 2),
            new Mission("화성", "4에서 7 사이의 연료량", 4, 7),
            new Mission("외계 행성", "8에서 10 사이의 연료량", 8, 10),
        };

        [SerializeField] private TMP_Text missionText;
        [SerializeField] private Image goalImage;
        [SerializeField] private TMP_Text goalPlanetNameText;

        private ILogger<MissionBoardController> _logger;
        private Mission _current = Missions[0];

        /// <summary> 이번 게임의 목적지 이름. </summary>
        public string Destination => _current.Destination;

        /// <summary> 입력한 연료량이 이번 목적지의 조건 범위에 드는지 반환함. </summary>
        public bool IsFuelValid(int fuel) => fuel >= _current.MinFuel && fuel <= _current.MaxFuel;

        /// <summary> VContainer 의존성 주입. 로거를 할당함. </summary>
        [Inject]
        public void Construct(ILogger<MissionBoardController> logger)
        {
            _logger = logger;
        }

        /// <summary> 씬 시작 시 무작위 목적지를 골라 미션 보드 텍스트와 목적지 표시를 구성함. </summary>
        private void Start()
        {
            _current = Missions[Random.Range(0, Missions.Length)];

            if (_logger != null) _logger.ZLogInformation($"[MissionBoardController] Mission set: {_current.Destination} / {_current.FuelRequirement}");

            ApplyMissionText();
            ApplyGoalDisplay();
        }

        private void ApplyMissionText()
        {
            if (missionText == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] missionText is null. Cannot set mission text.");
                return;
            }

            missionText.text =
                $"목적지는 [<color=yellow>{_current.Destination}</color>] 입니다.\n" +
                $"[<color=yellow>{_current.FuelRequirement}</color>]을 입력하고,\n" +
                $"추진체와 탑재 종류를 설정해주세요.";
        }

        /// <summary> 목적지에 맞는 이미지(Resources 폴더)와 행성 이름 텍스트를 Image_Goal에 적용함. </summary>
        private void ApplyGoalDisplay()
        {
            if (goalImage == null || goalPlanetNameText == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] goalImage or goalPlanetNameText is null. Cannot apply goal display.");
                return;
            }

            string resourceName = ResolveGoalSpriteResourceName(_current.Destination);
            Sprite sprite = Resources.Load<Sprite>(resourceName);
            if (sprite == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[MissionBoardController] Goal sprite not found for destination '{_current.Destination}' (Resources/{resourceName}).");
            }
            else
            {
                goalImage.sprite = sprite;
                goalImage.SetNativeSize();
            }

            goalPlanetNameText.text = _current.Destination;
        }

        /// <summary> 목적지 이름을 Resources 폴더의 이미지 파일명으로 변환함(예: "외계 행성" -> "외계행성"). </summary>
        private static string ResolveGoalSpriteResourceName(string destination)
        {
            return destination == "외계 행성" ? "외계행성" : destination;
        }
    }
}
