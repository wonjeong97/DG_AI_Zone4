using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DGAIZone.App;
using DGAIZone.Data;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 레벨 4 코딩 중 level4GhostInterval(기본 5초)마다 투명한 로봇(고스트, Image_RobotGhost)이 로봇 시작 칸에서
    /// 이번 판 AI 정답 경로를 따라 기지까지 한 칸씩 움직여 보여 주고 사라짐. 사라진 뒤 다시 그 시간이 지나면 또 보여 줌.
    /// 코딩 완료·넘어가기로 게임 패널이 잠기거나 진짜 로봇의 이동 시뮬레이션이 시작되면 고스트를 페이드아웃하고,
    /// 미션 다시 보기로 게임 패널이 꺼져 있는 동안에도 숨김(돌아오면 처음부터 다시 잼).
    /// 칸 좌표와 정답 경로는 Level4BoardController에서 받음(보드와 같은 배치·같은 경로).
    /// </summary>
    public class Level4GhostHint : MonoBehaviour
    {
        [SerializeField] private Level4BoardController board;  // Panel_Level4
        [SerializeField] private RectTransform ghostIcon;       // Image_Grid/Image_RobotGhost — Image_Robot과 같은 그림·크기, CanvasGroup으로 투명하게 그림
        [SerializeField] private CanvasGroup gamePanel;         // 게임 패널이 활성(상호작용 가능)일 때만 보여 줌

        private const float GhostFadeDuration = 0.25f; // 고스트가 나타나고 사라지는 시간(초)
        private const float GhostHoldDuration = 0.3f;  // 기지에 닿은 뒤 사라지기 전까지 머무는 시간(초)
        private const bool StartFacingLeft = false;    // 로봇은 오른쪽을 보고 시작함(Level4BoardController와 같음)

        private SelectedLevelStore _selectedLevelStore;
        private ILogger<Level4GhostHint> _logger;

        private CanvasGroup _ghostGroup;
        private float _ghostScaleAbsX = 1f;
        private Vector3 _ghostBaseScale = Vector3.one;
        private bool _isActive;        // 레벨 4이고 화면 요소가 모두 연결돼 보여 줄 수 있는지(Start에서 켜고, 정답 경로를 알 수 없으면 ShowGhost가 끔)
        private bool _isShowing;       // 고스트가 지금 움직이는 중인지
        private float _waitSeconds;    // 고스트가 사라진 뒤(처음은 보여 줄 수 있게 된 뒤) 흐른 시간(초)
        private CancellationTokenSource _ghostCts;
        private List<GhostCell> _path; // 시작 칸 다음부터 기지까지 정답 경로 칸(한 번 계산해 둠)

        // 3_Game.json 튜닝 값 — 로드 전에는 설정 클래스의 기본값을 그대로 씀
        private GameSceneSettings _sceneSettings = new GameSceneSettings();

        /// <summary> VContainer 의존성 주입. 선택된 레벨 저장소와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SelectedLevelStore selectedLevelStore, ILogger<Level4GhostHint> logger)
        {
            _selectedLevelStore = selectedLevelStore;
            _logger = logger;
        }

        /// <summary> 고스트를 숨긴 채 시작하고, 원래 스케일(좌우 반전 기준)을 기억함. </summary>
        private void Awake()
        {
            if (!ghostIcon) return; // Start에서 경고함

            _ghostBaseScale = ghostIcon.localScale;
            _ghostScaleAbsX = Mathf.Abs(_ghostBaseScale.x);
            if (!ghostIcon.TryGetComponent(out _ghostGroup)) _ghostGroup = ghostIcon.gameObject.AddComponent<CanvasGroup>();
            _ghostGroup.alpha = 0f;
            _ghostGroup.interactable = false;
            _ghostGroup.blocksRaycasts = false;
            ghostIcon.gameObject.SetActive(false);
        }

        /// <summary> 레벨 4일 때만 보여 줄 준비를 하고 3_Game.json 연출 값을 불러옴. 화면 요소가 빠졌으면 경고하고 보여 주지 않음. </summary>
        private void Start()
        {
            // 다른 레벨은 보여 주지 않음(정상). 저장소가 주입되지 않았으면 LevelOrFallback이 경고를 남김
            if (SelectedLevelStore.LevelOrFallback(_selectedLevelStore, _logger, nameof(Level4GhostHint)) != 4) return;

            if (!board || !ghostIcon)
            {
                if (_logger != null) _logger.ZLogWarning($"[Level4GhostHint] board 또는 ghostIcon이 비어 있어 레벨 4 고스트 안내를 끔.");
                return;
            }

            if (!gamePanel && _logger != null) _logger.ZLogWarning($"[Level4GhostHint] gamePanel이 비어 있어 미션 다시 보기·코딩 완료 중에도 고스트를 숨기지 못할 수 있음(진짜 로봇 시뮬레이션 중에는 숨김).");

            _isActive = true;
            LoadSettingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 씬 안의 다른 컴포넌트와 공유하는 3_Game.json 설정을 불러옴. </summary>
        private async UniTaskVoid LoadSettingsAsync(CancellationToken token)
        {
            _sceneSettings = await GameSceneSettingsProvider.GetAsync(token);
        }

        /// <summary> 보여 줄 수 있는 동안 시간을 재어 level4GhostInterval마다 고스트를 보여 주고, 보여 줄 수 없게 되면 페이드아웃함. </summary>
        private void Update()
        {
            if (!_isActive) return;

            if (!CanShowGhost())
            {
                _waitSeconds = 0f;
                FadeOutGhost();
                return;
            }

            if (_isShowing) return;

            _waitSeconds += Time.unscaledDeltaTime;
            if (_waitSeconds >= Mathf.Max(0f, _sceneSettings.level4GhostInterval)) ShowGhost();
        }

        /// <summary> 게임 화면이 켜져 있고(코딩 완료·넘어가기·미션 다시 보기 중이 아님) 진짜 로봇이 움직이는 중이 아닌지. </summary>
        private bool CanShowGhost()
        {
            if (gamePanel && !gamePanel.interactable) return false;
            return !board.IsSimulating;
        }

        /// <summary> 정답 경로를 따라 고스트를 한 번 움직이기 시작함. 정답 경로를 알 수 없으면 경고를 한 번 남기고 끔. </summary>
        private void ShowGhost()
        {
            _path ??= BuildGhostPath(board.FindSolution(), board.RobotRow);
            if (_path == null || _path.Count == 0)
            {
                if (_logger != null) _logger.ZLogWarning($"[Level4GhostHint] 이번 배치의 정답 경로를 알 수 없어 고스트 안내를 끔.");
                _isActive = false;
                return;
            }

            _isShowing = true;
            _ghostCts?.Dispose();
            _ghostCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            PlayGhostAsync(_ghostCts.Token).Forget();
        }

        /// <summary> 움직이던 고스트를 멈추고 그 자리에서 페이드아웃함. 움직이는 중이 아니면 아무것도 하지 않음(정상). </summary>
        private void FadeOutGhost()
        {
            if (!_isShowing) return;
            _isShowing = false;

            _ghostCts?.Cancel();
            _ghostCts?.Dispose();
            _ghostCts = null;

            DOTween.Kill(ghostIcon);
            DOTween.Kill(_ghostGroup);
            _ghostGroup.DOFade(0f, GhostFadeDuration).SetUpdate(true).SetLink(ghostIcon.gameObject)
                .OnComplete(() => ghostIcon.gameObject.SetActive(false));
        }

        /// <summary> 고스트를 로봇 시작 칸에 띄워 정답 경로를 따라 기지까지 한 칸씩 움직인 뒤 사라지게 함. 다 사라지면 다음 간격을 처음부터 잼. </summary>
        private async UniTaskVoid PlayGhostAsync(CancellationToken token)
        {
            try
            {
                DOTween.Kill(_ghostGroup);
                ghostIcon.anchoredPosition = board.GetIconAnchoredPositionForCell(ghostIcon, Level4Rules.RobotColumn, board.RobotRow);
                ApplyFacing(StartFacingLeft);
                ghostIcon.SetAsLastSibling(); // 로봇·자원·함정·기지 위에 그림
                _ghostGroup.alpha = 0f;
                ghostIcon.gameObject.SetActive(true);

                await _ghostGroup.DOFade(Mathf.Clamp01(_sceneSettings.level4GhostAlpha), GhostFadeDuration).SetUpdate(true)
                    .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);

                float moveDuration = Mathf.Max(0f, _sceneSettings.level4MoveDuration);
                foreach (GhostCell cell in _path)
                {
                    ApplyFacing(cell.FaceLeft);
                    await ghostIcon.DOAnchorPos(board.GetIconAnchoredPositionForCell(ghostIcon, cell.Column, cell.Row), moveDuration)
                        .SetEase(Ease.Linear).SetUpdate(true)
                        .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(GhostHoldDuration), DelayType.UnscaledDeltaTime, cancellationToken: token);
                await _ghostGroup.DOFade(0f, GhostFadeDuration).SetUpdate(true)
                    .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);

                ghostIcon.gameObject.SetActive(false);
                _isShowing = false;
                _waitSeconds = 0f;
            }
            catch (OperationCanceledException)
            {
                // 코딩 완료·미션 다시 보기·씬 전환으로 멈춤(정상) — 사라지는 연출은 FadeOutGhost가 맡음
            }
        }

        /// <summary> 고스트 좌우 시선을 localScale.x 부호로 적용함(그림 기본은 왼쪽, Level4BoardController와 같음). </summary>
        private void ApplyFacing(bool faceLeft)
        {
            ghostIcon.localScale = new Vector3(faceLeft ? _ghostScaleAbsX : -_ghostScaleAbsX, _ghostBaseScale.y, _ghostBaseScale.z);
        }

        /// <summary> 고스트가 지나갈 칸 하나와 그 칸으로 갈 때의 시선. </summary>
        internal readonly struct GhostCell
        {
            public readonly int Column;
            public readonly int Row;
            public readonly bool FaceLeft;

            public GhostCell(int column, int row, bool faceLeft)
            {
                Column = column;
                Row = row;
                FaceLeft = faceLeft;
            }
        }

        /// <summary>
        /// 정답 경로(구간마다 이동 칸 수·방향)를 로봇 시작 칸(열 0, robotRow) 다음부터 기지까지 한 칸씩 펼침. 시선은 마지막 좌우 이동 방향을 따름(위아래 이동은 그대로).
        /// 경로가 없거나 알 수 없는 방향이 있으면 null.
        /// </summary>
        internal static List<GhostCell> BuildGhostPath(IReadOnlyList<(int moves, string directionId)> solution, int robotRow)
        {
            if (solution == null) return null;

            List<GhostCell> cells = new List<GhostCell>();
            int column = Level4Rules.RobotColumn;
            int row = robotRow;
            bool faceLeft = StartFacingLeft;
            foreach ((int moves, string directionId) in solution)
            {
                if (!Level4Rules.TryGetMoveStep(directionId, out Level4MoveStep step)) return null;
                if (step.DeltaColumn != 0) faceLeft = step.DeltaColumn < 0;

                for (int i = 0; i < moves; i++)
                {
                    column += step.DeltaColumn;
                    row += step.DeltaRow;
                    cells.Add(new GhostCell(column, row, faceLeft));
                }
            }

            return cells;
        }

        /// <summary> 진행 중인 고스트 연출을 정리함. </summary>
        private void OnDestroy()
        {
            _ghostCts?.Cancel();
            _ghostCts?.Dispose();
        }
    }
}
