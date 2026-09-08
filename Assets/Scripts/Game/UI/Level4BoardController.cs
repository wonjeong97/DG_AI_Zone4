using System.Collections.Generic;
using DGAIZone.App;
using UnityEngine;
using VContainer;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 레벨 4 게임 보드(4x4 사다리꼴 그리드) 컨트롤러. 씬 시작 시 로봇/자원/함정/기지 아이콘을
    /// 열(로봇=C0, 자원=C1, 함정=C2, 기지=C3)은 고정하고 행은 무작위로 배치함. Grid.png가 정사각형이 아닌
    /// 원근 사다리꼴이라 셀 중심 좌표를 픽셀 분석으로 미리 산출해 상수로 둠(Image_Grid의 sizeDelta/pivot(0,1) 기준 로컬 좌표).
    /// </summary>
    public class Level4BoardController : MonoBehaviour
    {
        [SerializeField] private RectTransform robotIcon;
        [SerializeField] private RectTransform resourceIcon;
        [SerializeField] private RectTransform trapIcon;
        [SerializeField] private RectTransform hqIcon;

        private const int Rows = 4;
        private const int MaxCommands = 5; // 플레이어가 입력 가능한 카드 최대 개수. 복잡한 배치를 막기 위한 이동 비용 상한으로 씀.

        // Grid.png(742x234) 사다리꼴 그리드의 행 경계 Y좌표 5개(행 4개 = 경계 5개)와,
        // 각 행 경계에서의 열 경계 X좌표 5개(열 4개 = 경계 5개). 이미지 픽셀 분석으로 산출됨.
        private static readonly double[] RowBoundaryY = { 46, 91, 135, 179, 224 };
        private static readonly double[][] ColumnBoundaryXByRow =
        {
            new double[] { 98.5, 230.5, 370.5, 501.5, 642.0 },
            new double[] { 78.5, 222.5, 370.5, 513.5, 662.0 },
            new double[] { 58.25, 214.0, 370.5, 525.75, 682.5 },
            new double[] { 37.5, 206.0, 370.5, 538.0, 702.5 },
            new double[] { 16.3, 197.7, 370.5, 550.3, 723.3 },
        };

        /// <summary> 이번 판에 확정된 로봇/자원/함정/기지의 행(R). 열은 항상 0(로봇)/1(자원)/2(함정)/3(기지)으로 고정됨. </summary>
        public int RobotRow { get; private set; }
        public int ResourceRow { get; private set; }
        public int TrapRow { get; private set; }
        public int HqRow { get; private set; }

        private SelectedLevelStore _selectedLevelStore;

        /// <summary> VContainer 의존성 주입. 선택된 레벨 저장소를 할당함. </summary>
        [Inject]
        public void Construct(SelectedLevelStore selectedLevelStore)
        {
            _selectedLevelStore = selectedLevelStore;
        }

        /// <summary> 레벨 4일 때만 보드를 무작위로 배치함. </summary>
        private void Start()
        {
            int level = _selectedLevelStore != null ? _selectedLevelStore.SelectedLevel : 1;
            if (level != 4) return;

            RandomizePlacement();
        }

        /// <summary>
        /// 열은 로봇=0, 자원=1, 함정=2, 기지=3으로 고정하고 행만 무작위로 뽑되,
        /// (1) 로봇과 기지가 같은 행이면 일자 경로가 되므로 배제하고,
        /// (2) 로봇-자원-기지 이동에 필요한 최소 커맨드 수(함정이 자원-기지 직선 경로를 완전히 막으면 우회 +2 포함)가
        /// MaxCommands를 넘지 않는 조합만 후보로 삼아 그중 하나를 무작위로 고름.
        /// </summary>
        private void RandomizePlacement()
        {
            var candidates = new List<(int robotRow, int resourceRow, int hqRow, int trapRow, int cost)>();

            for (int robotRow = 0; robotRow < Rows; robotRow++)
            {
                for (int resourceRow = 0; resourceRow < Rows; resourceRow++)
                {
                    for (int hqRow = 0; hqRow < Rows; hqRow++)
                    {
                        if (hqRow == robotRow) continue; // 일자 진행 방지

                        for (int trapRow = 0; trapRow < Rows; trapRow++)
                        {
                            int pathToResource = 1 + Mathf.Abs(resourceRow - robotRow);
                            int pathToHq = 2 + Mathf.Abs(hqRow - resourceRow);
                            bool blockedStraightLine = resourceRow == hqRow && trapRow == resourceRow;
                            int cost = pathToResource + pathToHq + (blockedStraightLine ? 2 : 0);
                            candidates.Add((robotRow, resourceRow, hqRow, trapRow, cost));
                        }
                    }
                }
            }

            int minCost = int.MaxValue;
            foreach (var candidate in candidates)
            {
                if (candidate.cost < minCost) minCost = candidate.cost;
            }

            int budget = Mathf.Min(minCost + 1, MaxCommands);
            List<(int robotRow, int resourceRow, int hqRow, int trapRow, int cost)> pool = candidates.FindAll(c => c.cost <= budget);
            var chosen = pool[Random.Range(0, pool.Count)];

            RobotRow = chosen.robotRow;
            ResourceRow = chosen.resourceRow;
            HqRow = chosen.hqRow;
            TrapRow = chosen.trapRow;

            PlaceAtCellBottomCenter(robotIcon, 0, RobotRow);
            PlaceAtCellBottomCenter(resourceIcon, 1, ResourceRow);
            PlaceAtCellBottomCenter(trapIcon, 2, TrapRow);
            PlaceAtCellBottomCenter(hqIcon, 3, HqRow);

            ApplyRowBasedDrawOrder();
        }

        /// <summary> 지정한 열/행 셀의 바닥 중앙에 아이콘의 바닥 중앙(피벗 0,1 기준)을 맞춤. </summary>
        private void PlaceAtCellBottomCenter(RectTransform icon, int column, int row)
        {
            if (icon == null) return;

            double bottomY = RowBoundaryY[row + 1];
            double[] xAtBottom = ColumnBoundaryXByRow[row + 1];
            double bottomCenterX = (xAtBottom[column] + xAtBottom[column + 1]) / 2.0;

            float x = (float)bottomCenterX - icon.sizeDelta.x / 2f;
            float y = (float)-bottomY + icon.sizeDelta.y;
            icon.anchoredPosition = new Vector2(x, y);
        }

        /// <summary> R(행) 오름차순으로 sibling을 재배치해 아래쪽(화면상 가까운) 타일이 항상 앞에 그려지도록 함. </summary>
        private void ApplyRowBasedDrawOrder()
        {
            var order = new List<(RectTransform icon, int row)>
            {
                (robotIcon, RobotRow),
                (resourceIcon, ResourceRow),
                (trapIcon, TrapRow),
                (hqIcon, HqRow),
            };
            order.Sort((a, b) => a.row.CompareTo(b.row));

            foreach (var entry in order)
            {
                if (entry.icon != null) entry.icon.SetAsLastSibling();
            }
        }
    }
}
