using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game.Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
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
    /// 디버그 액션 PlayLevel4Simulation(DebugInputActions, 기본 스페이스바)을 누르면 디자인 윈도우에 확정된 "반복하기/이동하기" 명령대로 로봇 아이콘이 셀 단위로 순차 이동하는
    /// 검증용 시뮬레이션을 재생함('코딩완료' 버튼은 결과 씬으로 바로 전환되므로 개발/플레이 확인용으로 둠).
    /// 이동 판정·배치 후보·정답 탐색 규칙은 Level4Rules에 있고, 이 클래스는 화면 배치와 연출을 맡음.
    /// </summary>
    public class Level4BoardController : MonoBehaviour
    {
        [SerializeField] private RectTransform robotIcon;
        [SerializeField] private RectTransform resourceIcon;
        [SerializeField] private RectTransform trapIcon;
        [SerializeField] private RectTransform hqIcon;
        [SerializeField] private CanvasGroup gamePanel; // 게임 패널이 활성(상호작용 가능)일 때만 스페이스 입력을 받음

        private const int MaxCommands = Constants.Level4Board.MaxCards; // 플레이어가 입력 가능한 카드 최대 개수. 반복하기 없이는 이 장수 안에 못 푸는 배치만 고르는 기준으로 씀.
        private const int RequiredRepeatCount = Constants.Level4Board.RequiredRepeatCount; // 배치가 전제로 하는 반복하기 횟수
        private const float DebugMarkerHeight = 28f; // CellMarkers 디버그 라벨(TMP, sizeDelta 80x28, pivot 0.5,0.5)의 높이. 아이콘 정렬 기준점(라벨의 중앙 하단) 계산에 씀.
        private const bool StartFacingLeft = false; // 로봇 기본 이미지는 왼쪽을 보고 있으나, 시작 시에는 오른쪽을 보도록 함
        private const float GridWidth = 742f; // Image_Grid(Grid.png) sizeDelta.x
        private const float GridHeight = 234f; // Image_Grid(Grid.png) sizeDelta.y
        private const float OutOfBoundsPeekFraction = 0.5f; // 그리드 밖으로 나갈 때, 나가려던 방향으로 한 칸의 이 비율만큼만 더 이동하며 사라짐

        // 3_Game.json(GameSceneSettings) 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private GameSceneSettings _sceneSettings = new GameSceneSettings();
        private float MoveDuration => _sceneSettings.level4MoveDuration;
        private float StepPauseDuration => _sceneSettings.level4StepPauseDuration;
        private float CollisionScaleDuration => _sceneSettings.level4CollisionScaleDuration;

        private const string MoveIngredientId = Constants.RfidIds.Level4.Move;
        private const string RepeatIngredientId = Constants.RfidIds.Level4.Repeat;

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

        /// <summary> 이번 판 배치(행 위치). </summary>
        private Level4Layout Layout => new Level4Layout(RobotRow, ResourceRow, TrapRow, HqRow);

        /// <summary>
        /// 유닛 테스트용: 결정론적 판정 검증을 위해 아이콘 행 위치를 직접 설정함.
        /// </summary>
        internal void SetPlacementForTest(int robotRow, int resourceRow, int trapRow, int hqRow)
        {
            RobotRow = robotRow;
            ResourceRow = resourceRow;
            TrapRow = trapRow;
            HqRow = hqRow;
        }

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
        private DebugInputActions _debugInput;
        private bool _acceptsSimulationInput; // Start에서 정함 — 레벨 4의 에디터·개발 빌드일 때만 이동 시뮬레이션 디버그 입력을 받음

        /// <summary> VContainer 의존성 주입. 선택된 레벨 저장소, 확정 명령을 읽어올 재료 선택 컨트롤러, 로거를 할당함. </summary>
        [Inject]
        public void Construct(SelectedLevelStore selectedLevelStore, IngredientSelectionController ingredientSelection, ILogger<Level4BoardController> logger)
        {
            _selectedLevelStore = selectedLevelStore;
            _ingredientSelection = ingredientSelection;
            _logger = logger;
        }

        /// <summary> 이동 시뮬레이션 디버그 액션을 만들고 시뮬레이션 재생에 연결함. </summary>
        private void Awake()
        {
            _debugInput = new DebugInputActions();
            _debugInput.Debug.PlayLevel4Simulation.performed += OnPlaySimulationInput;
        }

        /// <summary> 다시 활성화되면 Start에서 허용한 경우에만 이동 시뮬레이션 디버그 액션을 켬. </summary>
        private void OnEnable()
        {
            if (_acceptsSimulationInput) _debugInput.Debug.PlayLevel4Simulation.Enable();
        }

        /// <summary> 이동 시뮬레이션 디버그 액션을 끔. </summary>
        private void OnDisable()
        {
            _debugInput.Debug.PlayLevel4Simulation.Disable();
        }

        /// <summary>
        /// 레벨 4일 때만 보드를 무작위로 배치하고 로봇/자원 아이콘의 원래 스케일(좌우 반전, 소멸 연출 복원 기준)을 기억함.
        /// 이동 시뮬레이션 디버그 액션은 레벨 4의 에디터·개발 빌드에서만 켬.
        /// </summary>
        private void Start()
        {
            _acceptsSimulationInput = IsLevel4() && Debug.isDebugBuild;
            if (_acceptsSimulationInput) _debugInput.Debug.PlayLevel4Simulation.Enable();
            if (!IsLevel4()) return;

            if (!Debug.isDebugBuild && _logger != null) _logger.ZLogInformation($"[Level4BoardController] 릴리스 빌드라 스페이스바 이동 시뮬레이션을 끔.");

            if (robotIcon)
            {
                _robotBaseScale = robotIcon.localScale;
                _robotInitialScaleAbsX = Mathf.Abs(robotIcon.localScale.x);
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[Level4BoardController] robotIcon이 null이라 로봇 원래 스케일을 기억할 수 없음.");
            }

            if (resourceIcon) _resourceBaseScale = resourceIcon.localScale;
            else if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] resourceIcon이 null이라 자원 원래 스케일을 기억할 수 없음.");

            RandomizePlacement();
            LoadSceneSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 3_Game.json(GameSceneSettings)을 비동기로 로드함(3_Game 씬 내 다른 컨트롤러와 로드를 공유함). </summary>
        private async UniTaskVoid LoadSceneSettingsAsync(CancellationToken token)
        {
            _sceneSettings = await GameSceneSettingsProvider.GetAsync(token);
        }

        /// <summary>
        /// 게임 패널이 활성 상태일 때 이동 시뮬레이션 디버그 액션(기본 스페이스바)이 눌리면 이동 시뮬레이션을 (재)시작함.
        /// 개발/플레이 중 경로를 눈으로 미리 확인하기 위한 디버그 트리거라 레벨 4의 에디터·개발 빌드일 때만 켜 두며(Start),
        /// '코딩완료' 버튼도 결과 씬으로 넘어가기 전에 동일한 시뮬레이션(PlaySimulationAsync)을 재생함(IngredientSelectionController에서 호출).
        /// </summary>
        private void OnPlaySimulationInput(UnityEngine.InputSystem.InputAction.CallbackContext _)
        {
            if (gamePanel && !gamePanel.interactable)
            {
                if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 게임 패널이 비활성이라 이동 시뮬레이션 디버그 입력을 무시함.");
                return;
            }

            PlaySimulationAsync().Forget();
        }

        /// <summary> 현재 선택된 레벨이 레벨 4인지. </summary>
        private bool IsLevel4() => _selectedLevelStore != null && _selectedLevelStore.SelectedLevel == 4;

        /// <summary>
        /// 열은 로봇=0, 자원=1, 함정=2, 기지=3으로 고정하고 행만 무작위로 뽑되, 반복하기(제어) 블록을 꼭 쓰도록
        /// 이동하기만으로는 카드 MaxCommands장 안에 풀 수 없고 반복하기를 쓰면 풀리는 배치(Level4Rules.PlacementPool) 중 하나를 고름.
        /// </summary>
        private void RandomizePlacement()
        {
            IReadOnlyList<Level4Layout> pool = Level4Rules.PlacementPool;
            if (pool.Count == 0)
            {
                if (_logger != null) _logger.ZLogError($"[Level4BoardController] 반복하기를 써야만 풀리는 배치가 없어 보드를 배치할 수 없음(카드 {MaxCommands}장, 반복 {RequiredRepeatCount}회 기준).");
                return;
            }

            Level4Layout chosen = pool[UnityEngine.Random.Range(0, pool.Count)];

            RobotRow = chosen.RobotRow;
            ResourceRow = chosen.ResourceRow;
            HqRow = chosen.HqRow;
            TrapRow = chosen.TrapRow;

            PlaceAtCellCenter(robotIcon, Level4Rules.RobotColumn, RobotRow);
            PlaceAtCellCenter(resourceIcon, Level4Rules.ResourceColumn, ResourceRow);
            PlaceAtCellCenter(trapIcon, Level4Rules.TrapColumn, TrapRow);
            PlaceAtCellCenter(hqIcon, Level4Rules.HqColumn, HqRow);

            _robotCurrentColumn = Level4Rules.RobotColumn;
            _robotCurrentRow = RobotRow;
            _facingLeft = StartFacingLeft;
            ApplyRobotFacing();

            ApplyRowBasedDrawOrder();
        }

        /// <summary> 이번 판 배치를 가장 적은 카드(최대 MaxCommands장, 반복 RequiredRepeatCount회)로 푸는 경로. 풀 수 없으면 null. </summary>
        internal List<(int moves, string directionId)> FindSolution()
        {
            return Level4Rules.FindShortestSolution(Layout, MaxCommands, RequiredRepeatCount);
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
            if (!icon)
            {
                if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] 배치할 아이콘이 null이라 ({column}, {row}) 셀 배치를 건너뜀.");
                return;
            }

            icon.anchoredPosition = GetIconAnchoredPositionForCell(icon, column, row);
        }

        /// <summary>
        /// 자원 아이콘은 항상 로봇보다 앞에, 함정/기지 아이콘은 항상 로봇보다 뒤에 그려지도록 고정 순서로
        /// sibling을 재배치함(뒤에 그릴 것부터 차례로 맨 뒤 sibling으로 보냄). 행(row) 기준으로 정렬하면 로봇이 자원/함정/기지와
        /// 같은 행을 지날 때 그리기 순서가 뒤집혀(예: 로봇이 함정 셀 위로 지나가면 함정이 로봇을 가림) 요구사항과 어긋나므로 사용하지 않음.
        /// </summary>
        private void ApplyRowBasedDrawOrder()
        {
            MoveToFront(trapIcon, nameof(trapIcon));
            MoveToFront(hqIcon, nameof(hqIcon));
            MoveToFront(robotIcon, nameof(robotIcon));
            MoveToFront(resourceIcon, nameof(resourceIcon));
        }

        /// <summary> 아이콘을 형제 중 맨 마지막(가장 앞에 그려짐)으로 옮김. </summary>
        private void MoveToFront(RectTransform icon, string fieldName)
        {
            if (icon) icon.SetAsLastSibling();
            else if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] {fieldName}이 null이라 그리기 순서를 정할 수 없어 건너뜀.");
        }

        /// <summary>
        /// 이동 시뮬레이션을 재생하고 완료(또는 취소)될 때까지 대기 가능한 UniTask를 반환함. 이미 진행 중인
        /// 시뮬레이션이 있으면 취소하고 로봇을 시작 위치(Column=0, Row=RobotRow)/기본 시선(왼쪽)으로 되돌린 뒤
        /// 처음부터 다시 재생함(연타에 안전함). 스페이스바 디버그 트리거와 '코딩완료' 버튼(재생 후 결과 씬 전환,
        /// IngredientSelectionController) 양쪽에서 공용으로 사용함.
        /// </summary>
        public async UniTask PlaySimulationAsync()
        {
            if (!robotIcon)
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
            finally
            {
                // 더 최신 시뮬레이션이 이미 시작되어 필드가 교체됐다면 그 CTS는 건드리지 않음
                if (_simulationCts == cts)
                {
                    _simulationCts.Dispose();
                    _simulationCts = null;
                }
            }
        }

        /// <summary>
        /// 확정된 이동 명령을 연출 없이 즉시 계산해 성공/실패를 판정함(IngredientSelectionController.EvaluateMission이
        /// '코딩완료' 클릭 시 호출함). 성공 = 자원을 먼저 수집한 뒤 기지 셀에 정확히 도착. 그 외(그리드 밖으로 나감,
        /// 함정 셀 도착, 자원 없이 기지 도착, 스텝을 다 써도 기지에 도착하지 못함)는 전부 실패.
        /// 연출(ExecuteStepAsync)과 같은 Level4Rules.Step으로 한 칸씩 판정해 연출 버전과 판정이 어긋나지 않도록 함.
        /// commands를 넘기면 그 값을 그대로 평가하고(테스트/외부 호출용), null이면 기존처럼 ingredientSelection에서 직접 읽음.
        /// </summary>
        public bool EvaluateOutcome(IReadOnlyList<(string ingredientId, RfidMatter matter)> commands = null)
        {
            List<Level4MoveStep> steps = BuildMoveSteps(commands);
            Level4Layout layout = Layout;

            int column = Level4Rules.RobotColumn;
            int row = RobotRow;
            bool resourceCollected = false;

            foreach (Level4MoveStep step in steps)
            {
                switch (Level4Rules.Step(layout, ref column, ref row, ref resourceCollected, step))
                {
                    case Level4StepResult.OutOfBounds:
                        if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 판정: 그리드 밖(Column={column + step.DeltaColumn}, Row={row + step.DeltaRow})으로 나감. 실패.");
                        return false;

                    case Level4StepResult.Trap:
                        if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 판정: 함정 셀(R{TrapRow}C{Level4Rules.TrapColumn})에 도착함. 실패.");
                        return false;

                    case Level4StepResult.Hq:
                        if (_logger != null)
                        {
                            _logger.ZLogInformation($"[Level4BoardController] 판정: 기지 셀(R{HqRow}C{Level4Rules.HqColumn})에 도착함(자원 수집={resourceCollected}). {(resourceCollected ? "성공" : "실패")}.");
                        }
                        return resourceCollected;
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
            _robotCurrentColumn = Level4Rules.RobotColumn;
            _robotCurrentRow = RobotRow;
            _facingLeft = StartFacingLeft;
            _resourceCollected = false;

            ApplyRobotFacing(); // x/y/z 전체를 _robotBaseScale 기준으로 재설정하므로 이전 소멸 연출(스케일 0)도 함께 복원됨
            if (resourceIcon) resourceIcon.localScale = _resourceBaseScale;
            else if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] resourceIcon이 null이라 자원 스케일을 복원할 수 없음.");

            robotIcon.anchoredPosition = GetIconAnchoredPositionForCell(robotIcon, _robotCurrentColumn, _robotCurrentRow);
            ApplyRowBasedDrawOrder();
        }

        /// <summary>
        /// (재료 id, 물질) 순서를 실제 이동 스텝 목록으로 변환함. commands가 주어지지 않으면 IngredientSelectionController에서
        /// 확정된 명령을 직접 읽어옴(연출 시뮬레이션용). "반복하기(N회)" 바로 다음에 "이동하기(방향)"가 오면 그 방향으로
        /// N번 연속 이동하는 스텝으로 펼치고, "이동하기(방향)"가 단독이면 1번 이동하는 스텝으로 처리함.
        /// 뒤에 이동하기가 없는 반복하기는 무시함.
        /// </summary>
        private List<Level4MoveStep> BuildMoveSteps(IReadOnlyList<(string ingredientId, RfidMatter matter)> commands = null)
        {
            List<Level4MoveStep> steps = new List<Level4MoveStep>();

            if (commands == null)
            {
                if (!_ingredientSelection)
                {
                    if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] ingredientSelection이 null이라 확정된 명령을 읽을 수 없음.");
                    return steps;
                }

                commands = _ingredientSelection.GetConfirmedCommands();
            }

            int i = 0;
            while (i < commands.Count)
            {
                (string ingredientId, RfidMatter matter) current = commands[i];

                if (string.Equals(current.ingredientId, RepeatIngredientId, StringComparison.Ordinal))
                {
                    bool hasFollowingMove = i + 1 < commands.Count &&
                        string.Equals(commands[i + 1].ingredientId, MoveIngredientId, StringComparison.Ordinal);

                    if (!hasFollowingMove)
                    {
                        // 뒤에 이동하기가 없는 반복하기(예: 마지막으로 확정된 명령)는 무시함
                        i += 1;
                        continue;
                    }

                    int repeatCount = GetRepeatCount(current.matter);
                    if (Level4Rules.TryGetMoveStep(commands[i + 1].matter?.id, out Level4MoveStep moveStep))
                    {
                        for (int r = 0; r < repeatCount; r++) steps.Add(moveStep);
                    }
                    else if (_logger != null)
                    {
                        _logger.ZLogWarning($"[Level4BoardController] 알 수 없는 이동 방향 id '{commands[i + 1].matter?.id}'이라 반복 스텝을 건너뜀.");
                    }

                    i += 2;
                }
                else if (string.Equals(current.ingredientId, MoveIngredientId, StringComparison.Ordinal))
                {
                    if (Level4Rules.TryGetMoveStep(current.matter?.id, out Level4MoveStep moveStep)) steps.Add(moveStep);
                    else if (_logger != null)
                    {
                        _logger.ZLogWarning($"[Level4BoardController] 알 수 없는 이동 방향 id '{current.matter?.id}'이라 스텝을 건너뜀.");
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

        /// <summary> 반복하기 물질의 value(RfidMappings.json)를 반복 횟수로 씀. 1 미만이면 경고를 남기고 1회로 처리함. </summary>
        private int GetRepeatCount(RfidMatter matter)
        {
            if (matter != null && matter.value >= 1) return matter.value;

            if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] 반복하기 물질 '{matter?.id}'의 value({matter?.value})가 1 미만이라 1회로 처리함.");
            return 1;
        }

        /// <summary>
        /// 스텝 하나(한 칸 이동)를 실행함: 좌우 이동이면 시선 방향을 바꾸고(위/아래는 기존 시선 유지), Level4Rules.Step으로
        /// 판정함. 그리드 밖이면 로봇 소멸 연출을 재생하고 멈춤. 범위 안이면 DOTween으로 부드럽게 이동하고 z-order를 갱신한 뒤,
        /// 도착한 셀이 자원/함정/기지 셀이면 해당 연출을 재생함(PlayCellArrivalAsync). 마지막으로 다음 스텝 전 짧게 대기함.
        /// 반환값: 그리드 밖으로 나갔거나 함정/기지 셀에 도착해 로봇이 사라져서 남은 스텝을 더 진행하면 안 되면 true.
        /// </summary>
        private async UniTask<bool> ExecuteStepAsync(Level4MoveStep step, CancellationToken token)
        {
            if (step.DeltaColumn > 0) SetFacing(faceLeft: false);
            else if (step.DeltaColumn < 0) SetFacing(faceLeft: true);

            Level4StepResult result = Level4Rules.Step(Layout, ref _robotCurrentColumn, ref _robotCurrentRow, ref _resourceCollected, step);

            if (result == Level4StepResult.OutOfBounds)
            {
                int targetColumn = _robotCurrentColumn + step.DeltaColumn;
                int targetRow = _robotCurrentRow + step.DeltaRow;
                if (_logger != null)
                {
                    _logger.ZLogWarning($"[Level4BoardController] 로봇이 그리드 밖(Column={targetColumn}, Row={targetRow})으로 나가려 함: 실패 처리, 로봇 소멸 연출 재생.");
                }
                await PlayOutOfBoundsExitAsync(targetColumn, targetRow, token);
                return true;
            }

            Vector2 target = GetIconAnchoredPositionForCell(robotIcon, _robotCurrentColumn, _robotCurrentRow);

            await robotIcon.DOAnchorPos(target, MoveDuration).SetEase(Ease.Linear)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);

            ApplyRowBasedDrawOrder();

            bool shouldStop = await PlayCellArrivalAsync(result, token);

            await UniTask.Delay(TimeSpan.FromSeconds(StepPauseDuration), DelayType.UnscaledDeltaTime, cancellationToken: token);

            return shouldStop;
        }

        /// <summary>
        /// 로봇이 방금 도착한 셀의 판정 결과에 맞는 연출을 재생함. 자원 셀에 처음 닿았으면 자원 아이콘이 로봇에 빨려들어가듯 스케일 1->0.
        /// 함정 또는 기지 셀이면 로봇 아이콘 스케일이 1->0으로 사라짐.
        /// 반환값: 함정 또는 기지 셀에 도착해 로봇이 사라졌으면 true(더 이상 이동하면 안 됨).
        /// </summary>
        private async UniTask<bool> PlayCellArrivalAsync(Level4StepResult result, CancellationToken token)
        {
            switch (result)
            {
                case Level4StepResult.CollectedResource:
                    if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 로봇이 자원 셀(R{ResourceRow}C{Level4Rules.ResourceColumn})에 도착함: 자원 흡수 연출 재생.");
                    await AnimateScaleToZeroAsync(resourceIcon, token);
                    return false;

                case Level4StepResult.Trap:
                    if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] 로봇이 함정 셀(R{TrapRow}C{Level4Rules.TrapColumn})에 도착함: 로봇 소멸 연출 재생.");
                    await AnimateScaleToZeroAsync(robotIcon, token);
                    return true;

                case Level4StepResult.Hq:
                    if (_logger != null) _logger.ZLogInformation($"[Level4BoardController] 로봇이 기지 셀(R{HqRow}C{Level4Rules.HqColumn})에 도착함: 로봇 소멸 연출 재생.");
                    await AnimateScaleToZeroAsync(robotIcon, token);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 대상 아이콘의 스케일을 0으로 부드럽게 줄임(살짝 끌려들어가는 느낌을 위해 Ease.InBack 사용).
        /// 아이콘 피벗이 이미 정중앙(0.5, 0.5)이라 별도 보정 없이 그대로 중심 기준으로 줄어듦.
        /// </summary>
        private UniTask AnimateScaleToZeroAsync(RectTransform target, CancellationToken token)
        {
            if (!target)
            {
                if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] 스케일 연출 대상이 null이라 건너뜀.");
                return UniTask.CompletedTask;
            }

            return target.DOScale(Vector3.zero, CollisionScaleDuration).SetEase(Ease.InBack)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
        }

        /// <summary>
        /// 그리드 밖으로 나가는 실패 연출: 나가려던 방향(targetColumn/targetRow, 그리드 범위 밖의 값)으로
        /// 한 칸의 OutOfBoundsPeekFraction만큼만 더 이동하면서 동시에 스케일을 0으로 줄임("바깥으로 몇 발짝 나가다 사라짐").
        /// </summary>
        private UniTask PlayOutOfBoundsExitAsync(int targetColumn, int targetRow, CancellationToken token)
        {
            if (!robotIcon)
            {
                if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] robotIcon이 null이라 그리드 이탈 연출을 건너뜀.");
                return UniTask.CompletedTask;
            }

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
            int clampedColumn = Mathf.Clamp(targetColumn, 0, Level4Rules.Columns - 1);
            int clampedRow = Mathf.Clamp(targetRow, 0, Level4Rules.Rows - 1);
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
            if (!robotIcon)
            {
                if (_logger != null) _logger.ZLogWarning($"[Level4BoardController] robotIcon이 null이라 시선 방향을 적용할 수 없음.");
                return;
            }

            float signedX = _facingLeft ? _robotInitialScaleAbsX : -_robotInitialScaleAbsX;
            robotIcon.localScale = new Vector3(signedX, _robotBaseScale.y, _robotBaseScale.z);
        }

        /// <summary> 오브젝트 파괴 시 진행 중인 시뮬레이션 취소 토큰과 디버그 액션 에셋 사본을 정리함. </summary>
        private void OnDestroy()
        {
            _simulationCts?.Cancel();
            _simulationCts?.Dispose();
            _debugInput.Dispose();
        }
    }
}
