using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 레벨 4 게임 보드(4x4 사다리꼴 그리드) 컨트롤러. 씬 시작 시 로봇/자원/함정/기지 아이콘을
    /// 열(로봇=C0, 자원=C1, 함정=C2, 기지=C3)은 고정하고 행은 무작위로 배치함. Grid.png가 정사각형이 아닌
    /// 원근 사다리꼴이라 셀 중심 좌표를 픽셀 분석으로 미리 산출해 상수로 둠(Image_Grid의 sizeDelta 기준 로컬 좌표).
    /// robotIcon/resourceIcon/trapIcon/hqIcon은 앵커/피벗이 모두 (0.5, 0.5)(정중앙)라, 자원 흡수/로봇 소멸
    /// 스케일 연출이 피벗 보정 없이 자연스럽게 중심 기준으로 줄어듦. 대신 배치 시(GetIconAnchoredPositionForCell)
    /// 아이콘 바닥이 목표 지점에 닿도록 높이 절반만큼 보정함.
    /// 스페이스바를 누르면 디자인 윈도우에 확정된 "반복하기/이동하기" 명령대로 로봇 아이콘이 셀 단위로 순차 이동하는
    /// 검증용 시뮬레이션을 재생함('코딩완료' 버튼은 결과 씬으로 바로 전환되므로 개발/플레이 확인용으로 둠).
    /// </summary>
    public class Level4BoardController : MonoBehaviour
    {
        [SerializeField] private RectTransform robotIcon;
        [SerializeField] private RectTransform resourceIcon;
        [SerializeField] private RectTransform trapIcon;
        [SerializeField] private RectTransform hqIcon;
        [SerializeField] private CanvasGroup gamePanel; // 게임 패널이 활성(상호작용 가능)일 때만 스페이스 입력을 받음

        private const int Rows = 4;
        private const int Columns = 4;
        private const int MaxCommands = 5; // 플레이어가 입력 가능한 카드 최대 개수. 복잡한 배치를 막기 위한 이동 비용 상한으로 씀.
        private const float MoveDuration = 0.35f; // 한 칸 이동에 걸리는 시간(초)
        private const float StepPauseDuration = 0.12f; // 한 칸 이동 완료 후 다음 이동 전 대기 시간(초)
        private const float DebugMarkerHeight = 28f; // CellMarkers 디버그 라벨(TMP, sizeDelta 80x28, pivot 0.5,0.5)의 높이. 아이콘 정렬 기준점(라벨의 중앙 하단) 계산에 씀.
        private const bool StartFacingLeft = false; // 로봇 기본 이미지는 왼쪽을 보고 있으나, 시작 시에는 오른쪽을 보도록 함
        private const float GridWidth = 742f; // Image_Grid(Grid.png) sizeDelta.x
        private const float GridHeight = 234f; // Image_Grid(Grid.png) sizeDelta.y
        private const float CollisionScaleDuration = 0.25f; // 자원 흡수/로봇 소멸 스케일 연출 시간(초)
        private const float OutOfBoundsPeekFraction = 0.5f; // 그리드 밖으로 나갈 때, 나가려던 방향으로 한 칸의 이 비율만큼만 더 이동하며 사라짐

        private const string MoveIngredientName = "이동하기";
        private const string RepeatIngredientName = "반복하기";
        private const string MoveUp = "위쪽 한칸";
        private const string MoveDown = "아랫쪽 한칸";
        private const string MoveRight = "오른쪽 한칸";
        private const string MoveLeft = "왼쪽 한칸";

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

        /// <summary> 이번 판에 확정된 로봇/자원/함정/기지의 시작 행(R). 열은 항상 0(로봇)/1(자원)/2(함정)/3(기지)으로 고정됨. </summary>
        public int RobotRow { get; private set; }
        public int ResourceRow { get; private set; }
        public int TrapRow { get; private set; }
        public int HqRow { get; private set; }

        // 스페이스바 시뮬레이션 중 로봇의 실시간 위치(보드 시작 위치인 RobotRow/Column=0과는 별개로 매 스텝 갱신됨)
        private int _robotCurrentColumn;
        private int _robotCurrentRow;
        private bool _facingLeft = StartFacingLeft;
        private float _robotInitialScaleAbsX = 1f;
        private Vector3 _robotBaseScale = Vector3.one; // robotIcon의 원래 스케일(y/z 보존용). x는 시선 방향에 따라 부호만 바뀜.
        private Vector3 _resourceBaseScale = Vector3.one; // resourceIcon의 원래 스케일(흡수 연출 후 복원용)
        private bool _resourceCollected; // 이번 시뮬레이션에서 로봇이 자원 셀에 이미 도착해 흡수 연출을 재생했는지

        private SelectedLevelStore _selectedLevelStore;
        private IngredientSelectionController _ingredientSelection;
        private ILogger<Level4BoardController> _logger;
        private CancellationTokenSource _simulationCts;

        /// <summary> VContainer 의존성 주입. 선택된 레벨 저장소, 확정 명령을 읽어올 재료 선택 컨트롤러, 로거를 할당함. </summary>
        [Inject]
        public void Construct(SelectedLevelStore selectedLevelStore, IngredientSelectionController ingredientSelection, ILogger<Level4BoardController> logger)
        {
            _selectedLevelStore = selectedLevelStore;
            _ingredientSelection = ingredientSelection;
            _logger = logger;
        }

        /// <summary> 레벨 4일 때만 보드를 무작위로 배치하고 로봇/자원 아이콘의 원래 스케일(좌우 반전, 소멸 연출 복원 기준)을 기억함. </summary>
        private void Start()
        {
            if (!IsLevel4()) return;

            if (robotIcon != null)
            {
                _robotBaseScale = robotIcon.localScale;
                _robotInitialScaleAbsX = Mathf.Abs(robotIcon.localScale.x);
            }
            if (resourceIcon != null) _resourceBaseScale = resourceIcon.localScale;

            RandomizePlacement();
        }

        /// <summary>
        /// 레벨 4에서 게임 패널이 활성 상태일 때 스페이스바 입력을 감지해 이동 시뮬레이션을 (재)시작함.
        /// 개발/플레이 중 경로를 눈으로 미리 확인하기 위한 디버그 트리거이며, '코딩완료' 버튼도 결과 씬으로
        /// 넘어가기 전에 동일한 시뮬레이션(PlaySimulationAsync)을 재생함(IngredientSelectionController에서 호출).
        /// </summary>
        private void Update()
        {
            if (!IsLevel4()) return;
            if (gamePanel != null && !gamePanel.interactable) return;
            if (Keyboard.current == null) return;

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                PlaySimulationAsync().Forget();
            }
        }

        private bool IsLevel4() => _selectedLevelStore != null && _selectedLevelStore.SelectedLevel == 4;

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
            var chosen = pool[UnityEngine.Random.Range(0, pool.Count)];

            RobotRow = chosen.robotRow;
            ResourceRow = chosen.resourceRow;
            HqRow = chosen.hqRow;
            TrapRow = chosen.trapRow;

            PlaceAtCellCenter(robotIcon, 0, RobotRow);
            PlaceAtCellCenter(resourceIcon, 1, ResourceRow);
            PlaceAtCellCenter(trapIcon, 2, TrapRow);
            PlaceAtCellCenter(hqIcon, 3, HqRow);

            _robotCurrentColumn = 0;
            _robotCurrentRow = RobotRow;
            _facingLeft = StartFacingLeft;
            ApplyRobotFacing();

            ApplyRowBasedDrawOrder();
        }

        /// <summary>
        /// 지정한 열/행 셀의 기하학적 중심 좌표를, Image_Grid 왼쪽 위 모서리를 원점으로 하는 픽셀 좌표계
        /// (x는 오른쪽으로 증가, y는 아래로 갈수록 더 음수)로 반환함. 사다리꼴이라 셀의 네 모서리
        /// (위/아래 경계 x 좌/우 경계) 평균으로 중심을 구함.
        /// </summary>
        private Vector2 GetCellCenterPosition(int column, int row)
        {
            double topY = RowBoundaryY[row];
            double bottomY = RowBoundaryY[row + 1];
            double[] xAtTop = ColumnBoundaryXByRow[row];
            double[] xAtBottom = ColumnBoundaryXByRow[row + 1];

            double centerX = (xAtTop[column] + xAtTop[column + 1] + xAtBottom[column] + xAtBottom[column + 1]) / 4.0;
            double centerY = (topY + bottomY) / 2.0;
            return new Vector2((float)centerX, (float)-centerY);
        }

        /// <summary>
        /// 지정한 셀에서, 아이콘의 바닥이 CellMarkers 디버그 라벨(R#C# 텍스트)의 중앙 하단에 닿도록 하는
        /// anchoredPosition을 계산함. 라벨은 셀 기하학적 중심(GetCellCenterPosition)에 sizeDelta(80, DebugMarkerHeight),
        /// pivot(0.5, 0.5)로 놓여 있으므로, 라벨의 중앙 하단은 셀 중심에서 DebugMarkerHeight/2만큼 아래임.
        /// robotIcon/resourceIcon/trapIcon/hqIcon은 앵커/피벗이 (0.5, 0.5)(정중앙)라, 그 지점에서 아이콘 높이의
        /// 절반만큼 위로 올려야 아이콘의 바닥이 목표 지점에 닿음. (0.5, 0.5) 앵커는 Image_Grid 왼쪽 위 원점 기준으로
        /// (GridWidth/2, -GridHeight/2) 지점이므로, 그 지점을 기준으로 한 오프셋으로 변환함.
        /// </summary>
        private Vector2 GetIconAnchoredPositionForCell(RectTransform icon, int column, int row)
        {
            Vector2 cellCenter = GetCellCenterPosition(column, row);
            Vector2 markerBottomCenter = new Vector2(cellCenter.x, cellCenter.y - DebugMarkerHeight / 2f);

            float x = markerBottomCenter.x - GridWidth / 2f;
            float y = markerBottomCenter.y + icon.sizeDelta.y / 2f + GridHeight / 2f;
            return new Vector2(x, y);
        }

        /// <summary> 지정한 열/행 셀에서 아이콘의 바닥이 목표 지점에 닿도록(앵커/피벗 0.5,0.5 기준) 즉시(트윈 없이) 맞춤. </summary>
        private void PlaceAtCellCenter(RectTransform icon, int column, int row)
        {
            if (icon == null) return;
            icon.anchoredPosition = GetIconAnchoredPositionForCell(icon, column, row);
        }

        /// <summary>
        /// 자원 아이콘은 항상 로봇보다 앞에, 함정/기지 아이콘은 항상 로봇보다 뒤에 그려지도록 고정 우선순위로
        /// sibling을 재배치함. 행(row) 기준으로 정렬하면 로봇이 자원/함정/기지와 같은 행을 지날 때 그리기
        /// 순서가 뒤집혀(예: 로봇이 함정 셀 위로 지나가면 함정이 로봇을 가림) 요구사항과 어긋나므로 사용하지 않음.
        /// </summary>
        private void ApplyRowBasedDrawOrder()
        {
            var order = new List<(RectTransform icon, int priority)>
            {
                (trapIcon, 0),
                (hqIcon, 0),
                (robotIcon, 1),
                (resourceIcon, 2),
            };
            order.Sort((a, b) => a.priority.CompareTo(b.priority));

            foreach (var entry in order)
            {
                if (entry.icon != null) entry.icon.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 이동 시뮬레이션을 재생하고 완료(또는 취소)될 때까지 대기 가능한 UniTask를 반환함. 이미 진행 중인
        /// 시뮬레이션이 있으면 취소하고 로봇을 시작 위치(Column=0, Row=RobotRow)/기본 시선(왼쪽)으로 되돌린 뒤
        /// 처음부터 다시 재생함(연타에 안전함). 스페이스바 디버그 트리거와 '코딩완료' 버튼(재생 후 결과 씬 전환,
        /// IngredientSelectionController) 양쪽에서 공용으로 사용함.
        /// </summary>
        public async UniTask PlaySimulationAsync()
        {
            if (robotIcon == null)
            {
                if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] robotIcon이 null이라 이동 시뮬레이션을 시작할 수 없음.");
                return;
            }

            _simulationCts?.Cancel();
            _simulationCts?.Dispose();
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            _simulationCts = cts;
            CancellationToken token = cts.Token;

            try
            {
                ResetRobotToStart();

                List<Level4MoveStep> steps = BuildMoveSteps();
                if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 이동 시뮬레이션 시작: 총 {steps.Count}스텝.");

                foreach (Level4MoveStep step in steps)
                {
                    bool shouldStop = await ExecuteStepAsync(step, token);
                    if (shouldStop) break; // 함정/기지 셀에 도착해 로봇이 사라졌으면 남은 스텝은 진행하지 않음
                }

                if (_logger != null)
                {
                    _logger.ZLogInformation($"[Level4BoardController] 이동 시뮬레이션 완료: 최종 위치 Row={_robotCurrentRow}, Column={_robotCurrentColumn}.");
                }
            }
            catch (OperationCanceledException)
            {
                if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 이동 시뮬레이션이 취소됨(재시작 또는 씬 전환).");
            }
        }

        /// <summary>
        /// 확정된 이동 명령을 연출 없이 즉시 계산해 성공/실패를 판정함(IngredientSelectionController.EvaluateMission이
        /// '코딩완료' 클릭 시 호출함). 성공 = 자원을 먼저 수집한 뒤 기지 셀에 정확히 도착. 그 외(그리드 밖으로 나감,
        /// 함정 셀 도착, 자원 없이 기지 도착, 스텝을 다 써도 기지에 도착하지 못함)는 전부 실패.
        /// ExecuteStepAsync/HandleCellArrivalAsync가 쓰는 것과 동일한 판정 규칙(IsOutOfBounds/IsTrapCell/
        /// IsResourceCell/IsHqCell)을 그대로 재사용해 연출 버전과 판정이 어긋나지 않도록 함.
        /// commands를 넘기면 그 값을 그대로 평가하고(테스트/외부 호출용), null이면 기존처럼 ingredientSelection에서 직접 읽음.
        /// </summary>
        public bool EvaluateOutcome(IReadOnlyList<(string ingredient, string matter)> commands = null)
        {
            List<Level4MoveStep> steps = BuildMoveSteps(commands);

            int column = 0;
            int row = RobotRow;
            bool resourceCollected = false;

            foreach (Level4MoveStep step in steps)
            {
                int nextColumn = column + step.DeltaColumn;
                int nextRow = row + step.DeltaRow;

                if (IsOutOfBounds(nextColumn, nextRow))
                {
                    if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 판정: 그리드 밖(Column={nextColumn}, Row={nextRow})으로 나감. 실패.");
                    return false;
                }

                column = nextColumn;
                row = nextRow;

                if (IsResourceCell(column, row)) resourceCollected = true;

                if (IsTrapCell(column, row))
                {
                    if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 판정: 함정 셀(R{TrapRow}C2)에 도착함. 실패.");
                    return false;
                }

                if (IsHqCell(column, row))
                {
                    bool success = resourceCollected;
                    if (_logger != null)
                    {
                        _logger.ZLogInformation($"[Level4BoardController] 판정: 기지 셀(R{HqRow}C3)에 도착함(자원 수집={resourceCollected}). {(success ? "성공" : "실패")}.");
                    }
                    return success;
                }
            }

            if (_logger != null)
            {
                _logger.ZLogInformation($"[Level4BoardController] 판정: 스텝을 모두 소진했지만 기지에 도착하지 못함(최종 위치 Column={column}, Row={row}). 실패.");
            }
            return false;
        }

        /// <summary>
        /// 로봇을 시작 위치/기본 시선 방향/원래 스케일로 즉시 되돌리고, 이전 시뮬레이션에서 자원을 흡수했었다면
        /// 자원 아이콘 스케일도 원래대로 복원한 뒤 z-order를 갱신함.
        /// </summary>
        private void ResetRobotToStart()
        {
            _robotCurrentColumn = 0;
            _robotCurrentRow = RobotRow;
            _facingLeft = StartFacingLeft;
            _resourceCollected = false;

            ApplyRobotFacing(); // x/y/z 전체를 _robotBaseScale 기준으로 재설정하므로 이전 소멸 연출(스케일 0)도 함께 복원됨
            if (resourceIcon != null) resourceIcon.localScale = _resourceBaseScale;

            robotIcon.anchoredPosition = GetIconAnchoredPositionForCell(robotIcon, _robotCurrentColumn, _robotCurrentRow);
            ApplyRowBasedDrawOrder();
        }

        /// <summary>
        /// (재료, 물질) 순서를 실제 이동 스텝 목록으로 변환함. commands가 주어지지 않으면 IngredientSelectionController에서
        /// 확정된 명령을 직접 읽어옴(연출 시뮬레이션용). "반복하기(N회)" 바로 다음에 "이동하기(방향)"가 오면 그 방향으로
        /// N번 연속 이동하는 스텝으로 펼치고, "이동하기(방향)"가 단독이면 1번 이동하는 스텝으로 처리함.
        /// 뒤에 이동하기가 없는 반복하기는 무시함.
        /// </summary>
        private List<Level4MoveStep> BuildMoveSteps(IReadOnlyList<(string ingredient, string matter)> commands = null)
        {
            var steps = new List<Level4MoveStep>();

            if (commands == null)
            {
                if (_ingredientSelection == null)
                {
                    if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] ingredientSelection이 null이라 확정된 명령을 읽을 수 없음.");
                    return steps;
                }

                commands = _ingredientSelection.GetConfirmedCommands();
            }

            int i = 0;
            while (i < commands.Count)
            {
                (string ingredient, string matter) current = commands[i];

                if (string.Equals(current.ingredient, RepeatIngredientName, StringComparison.Ordinal))
                {
                    bool hasFollowingMove = i + 1 < commands.Count &&
                        string.Equals(commands[i + 1].ingredient, MoveIngredientName, StringComparison.Ordinal);

                    if (!hasFollowingMove)
                    {
                        // 뒤에 이동하기가 없는 반복하기(예: 마지막으로 확정된 명령)는 무시함
                        i += 1;
                        continue;
                    }

                    int repeatCount = ParseRepeatCount(current.matter);
                    Level4MoveStep? moveStep = ToMoveStep(commands[i + 1].matter);
                    if (moveStep.HasValue)
                    {
                        for (int r = 0; r < repeatCount; r++) steps.Add(moveStep.Value);
                    }
                    else if (_logger != null)
                    {
                        _logger.ZLogWarning($"[Level4BoardController] 알 수 없는 이동 방향 '{commands[i + 1].matter}'이라 반복 스텝을 건너뜀.");
                    }

                    i += 2;
                }
                else if (string.Equals(current.ingredient, MoveIngredientName, StringComparison.Ordinal))
                {
                    Level4MoveStep? moveStep = ToMoveStep(current.matter);
                    if (moveStep.HasValue) steps.Add(moveStep.Value);
                    else if (_logger != null)
                    {
                        _logger.ZLogWarning($"[Level4BoardController] 알 수 없는 이동 방향 '{current.matter}'이라 스텝을 건너뜀.");
                    }

                    i += 1;
                }
                else
                {
                    i += 1;
                }
            }

            return steps;
        }

        /// <summary> "1회"/"2회"/"3회" 문자열에서 반복 횟수를 파싱함. 실패하면 1회로 처리함. </summary>
        private static int ParseRepeatCount(string matter)
        {
            if (!string.IsNullOrEmpty(matter))
            {
                var match = System.Text.RegularExpressions.Regex.Match(matter, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int count)) return count;
            }
            return 1;
        }

        /// <summary> 방향 문자열을 열/행 변화량으로 변환함. 알 수 없는 값이면 null. </summary>
        private static Level4MoveStep? ToMoveStep(string direction)
        {
            switch (direction)
            {
                case MoveUp: return new Level4MoveStep(0, -1);
                case MoveDown: return new Level4MoveStep(0, 1);
                case MoveRight: return new Level4MoveStep(1, 0);
                case MoveLeft: return new Level4MoveStep(-1, 0);
                default: return null;
            }
        }

        /// <summary>
        /// 스텝 하나(한 칸 이동)를 실행함: 좌우 이동이면 시선 방향을 바꾸고(위/아래는 기존 시선 유지),
        /// 목표 열/행이 그리드 범위(0~3)를 벗어나면 성공/실패 판정 전에 즉시 실패 처리하고 로봇 소멸 연출을 재생함
        /// (더 이상 clamp하지 않음). 범위 안이면 DOTween으로 부드럽게 이동하고, 이동 완료 시 z-order를 갱신하고,
        /// 도착한 셀이 자원/함정/기지 셀이면 해당 연출을 재생함(HandleCellArrivalAsync). 마지막으로 다음 스텝 전 짧게 대기함.
        /// </summary>
        /// <returns> 그리드 밖으로 나갔거나 함정/기지 셀에 도착해 로봇이 사라져서 남은 스텝을 더 진행하면 안 되면 true. </returns>
        private async UniTask<bool> ExecuteStepAsync(Level4MoveStep step, CancellationToken token)
        {
            if (step.DeltaColumn > 0) SetFacing(faceLeft: false);
            else if (step.DeltaColumn < 0) SetFacing(faceLeft: true);

            int nextColumn = _robotCurrentColumn + step.DeltaColumn;
            int nextRow = _robotCurrentRow + step.DeltaRow;

            if (IsOutOfBounds(nextColumn, nextRow))
            {
                if (_logger != null)
                {
                    _logger.ZLogWarning($"[Level4BoardController] 로봇이 그리드 밖(Column={nextColumn}, Row={nextRow})으로 나가려 함: 실패 처리, 로봇 소멸 연출 재생.");
                }
                await PlayOutOfBoundsExitAsync(nextColumn, nextRow, token);
                return true;
            }

            _robotCurrentColumn = nextColumn;
            _robotCurrentRow = nextRow;

            Vector2 target = GetIconAnchoredPositionForCell(robotIcon, _robotCurrentColumn, _robotCurrentRow);

            await robotIcon.DOAnchorPos(target, MoveDuration).SetEase(Ease.Linear)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);

            ApplyRowBasedDrawOrder();

            bool shouldStop = await HandleCellArrivalAsync(token);

            await UniTask.Delay(TimeSpan.FromSeconds(StepPauseDuration), DelayType.UnscaledDeltaTime, cancellationToken: token);

            return shouldStop;
        }

        /// <summary>
        /// 로봇이 방금 도착한 셀(_robotCurrentColumn/_robotCurrentRow)이 자원/함정/기지 셀과 겹치는지 검사하고
        /// 해당 연출을 재생함. 자원 셀이면 자원 아이콘이 로봇에 빨려들어가듯 스케일 1->0(한 시뮬레이션당 한 번만).
        /// 함정 또는 기지 셀이면 로봇 아이콘 스케일이 1->0으로 사라짐.
        /// </summary>
        /// <returns> 함정 또는 기지 셀에 도착해 로봇이 사라졌으면 true(더 이상 이동하면 안 됨). </returns>
        private async UniTask<bool> HandleCellArrivalAsync(CancellationToken token)
        {
            if (!_resourceCollected && IsResourceCell(_robotCurrentColumn, _robotCurrentRow))
            {
                _resourceCollected = true;
                if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 로봇이 자원 셀(R{ResourceRow}C1)에 도착함: 자원 흡수 연출 재생.");
                await AnimateScaleToZeroAsync(resourceIcon, token);
            }

            if (IsTrapCell(_robotCurrentColumn, _robotCurrentRow))
            {
                if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] 로봇이 함정 셀(R{TrapRow}C2)에 도착함: 로봇 소멸 연출 재생.");
                await AnimateScaleToZeroAsync(robotIcon, token);
                return true;
            }

            if (IsHqCell(_robotCurrentColumn, _robotCurrentRow))
            {
                if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 로봇이 기지 셀(R{HqRow}C3)에 도착함: 로봇 소멸 연출 재생.");
                await AnimateScaleToZeroAsync(robotIcon, token);
                return true;
            }

            return false;
        }

        /// <summary> 지정한 열/행이 그리드 범위(0~3)를 벗어나는지. </summary>
        private static bool IsOutOfBounds(int column, int row) => column < 0 || column >= Columns || row < 0 || row >= Rows;

        /// <summary> 지정한 열/행이 함정 셀(C2, TrapRow)인지. </summary>
        private bool IsTrapCell(int column, int row) => column == 2 && row == TrapRow;

        /// <summary> 지정한 열/행이 자원 셀(C1, ResourceRow)인지. </summary>
        private bool IsResourceCell(int column, int row) => column == 1 && row == ResourceRow;

        /// <summary> 지정한 열/행이 기지 셀(C3, HqRow)인지. </summary>
        private bool IsHqCell(int column, int row) => column == 3 && row == HqRow;

        /// <summary>
        /// 대상 아이콘의 스케일을 0으로 부드럽게 줄임(살짝 끌려들어가는 느낌을 위해 Ease.InBack 사용).
        /// 아이콘 피벗이 이미 정중앙(0.5, 0.5)이라 별도 보정 없이 그대로 중심 기준으로 줄어듦.
        /// </summary>
        private UniTask AnimateScaleToZeroAsync(RectTransform target, CancellationToken token)
        {
            if (target == null) return UniTask.CompletedTask;

            return target.DOScale(Vector3.zero, CollisionScaleDuration).SetEase(Ease.InBack)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
        }

        /// <summary>
        /// 그리드 밖으로 나가는 실패 연출: 나가려던 방향(targetColumn/targetRow, 그리드 범위 밖의 값)으로
        /// 한 칸의 OutOfBoundsPeekFraction만큼만 더 이동하면서 동시에 스케일을 0으로 줄임("바깥으로 몇 발짝 나가다 사라짐").
        /// </summary>
        private UniTask PlayOutOfBoundsExitAsync(int targetColumn, int targetRow, CancellationToken token)
        {
            if (robotIcon == null) return UniTask.CompletedTask;

            Vector2 peekPosition = GetOutOfBoundsPeekPosition(robotIcon, targetColumn, targetRow);

            UniTask moveTask = robotIcon.DOAnchorPos(peekPosition, CollisionScaleDuration).SetEase(Ease.InQuad)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
            UniTask scaleTask = AnimateScaleToZeroAsync(robotIcon, token);

            return UniTask.WhenAll(moveTask, scaleTask);
        }

        /// <summary>
        /// 그리드 범위를 벗어난 열/행(targetColumn/targetRow 중 하나만 범위 밖이라고 가정, 이동은 항상 상하좌우 한 축뿐이므로)에
        /// 대해, 가장자리 셀과 그 안쪽 이웃 셀 사이의 벡터를 이용해 바깥 방향으로 OutOfBoundsPeekFraction만큼 외삽한
        /// anchoredPosition을 계산함. 사다리꼴이라 칸 간격이 위치마다 달라서, 실제 격자 간격을 그대로 연장하는 방식임.
        /// </summary>
        private Vector2 GetOutOfBoundsPeekPosition(RectTransform icon, int targetColumn, int targetRow)
        {
            int clampedColumn = Mathf.Clamp(targetColumn, 0, Columns - 1);
            int clampedRow = Mathf.Clamp(targetRow, 0, Rows - 1);
            Vector2 edgePosition = GetIconAnchoredPositionForCell(icon, clampedColumn, clampedRow);

            if (targetColumn != clampedColumn)
            {
                int neighborColumn = targetColumn < 0 ? clampedColumn + 1 : clampedColumn - 1;
                Vector2 neighborPosition = GetIconAnchoredPositionForCell(icon, neighborColumn, clampedRow);
                return edgePosition + (edgePosition - neighborPosition) * OutOfBoundsPeekFraction;
            }

            if (targetRow != clampedRow)
            {
                int neighborRow = targetRow < 0 ? clampedRow + 1 : clampedRow - 1;
                Vector2 neighborPosition = GetIconAnchoredPositionForCell(icon, clampedColumn, neighborRow);
                return edgePosition + (edgePosition - neighborPosition) * OutOfBoundsPeekFraction;
            }

            return edgePosition;
        }

        /// <summary> 로봇의 좌우 시선 방향을 localScale.x 반전으로 적용함. 기본(왼쪽)은 양수, 오른쪽은 음수. </summary>
        private void SetFacing(bool faceLeft)
        {
            _facingLeft = faceLeft;
            ApplyRobotFacing();
        }

        /// <summary> 로봇의 좌우 시선 방향을 localScale.x 반전으로 적용함. 기본(왼쪽)은 양수, 오른쪽은 음수.
        /// y/z는 _robotBaseScale을 그대로 사용하므로, 소멸 연출(스케일 0)로 줄어든 상태를 이 호출로 완전히 복원할 수 있음. </summary>
        private void ApplyRobotFacing()
        {
            if (robotIcon == null) return;

            float signedX = _facingLeft ? _robotInitialScaleAbsX : -_robotInitialScaleAbsX;
            robotIcon.localScale = new Vector3(signedX, _robotBaseScale.y, _robotBaseScale.z);
        }

        /// <summary> 오브젝트 파괴 시 진행 중인 시뮬레이션 취소 토큰을 정리함. </summary>
        private void OnDestroy()
        {
            _simulationCts?.Cancel();
            _simulationCts?.Dispose();
        }

        /// <summary> 한 칸 이동을 나타내는 열/행 변화량. </summary>
        private readonly struct Level4MoveStep
        {
            public readonly int DeltaColumn;
            public readonly int DeltaRow;

            public Level4MoveStep(int deltaColumn, int deltaRow)
            {
                DeltaColumn = deltaColumn;
                DeltaRow = deltaRow;
            }
        }
    }
}
