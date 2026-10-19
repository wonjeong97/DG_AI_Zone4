using System.Collections.Generic;
using DGAIZone.App;
using Unity.Profiling;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 레벨 4 보드(4x4 그리드)의 순수 규칙. 열은 로봇=0, 자원=1, 함정=2, 기지=3으로 고정되고 행만 배치마다 달라짐.
    /// 판정(Level4BoardController.EvaluateOutcome), 이동 연출(ExecuteStepAsync), 배치 검사·정답 탐색이 모두 Step 하나로
    /// 한 칸 이동을 판정해 규칙이 어긋나지 않게 함. 화면 좌표·연출은 다루지 않음.
    /// </summary>
    internal static class Level4Rules
    {
        internal const int Rows = 4;
        internal const int Columns = 4;
        internal const int RobotColumn = 0;
        internal const int ResourceColumn = 1;
        internal const int TrapColumn = 2;
        internal const int HqColumn = 3;

        // 이동 방향 id와 열/행 변화량. 카드 판정(TryGetMoveStep)과 배치 검사·정답 탐색이 같은 표를 씀
        private static readonly string[] DirectionIds =
        {
            Constants.RfidIds.Level4.MoveUp,
            Constants.RfidIds.Level4.MoveDown,
            Constants.RfidIds.Level4.MoveRight,
            Constants.RfidIds.Level4.MoveLeft
        };

        private static readonly Level4MoveStep[] DirectionSteps =
        {
            new Level4MoveStep(0, -1),
            new Level4MoveStep(0, 1),
            new Level4MoveStep(1, 0),
            new Level4MoveStep(-1, 0)
        };

        private static readonly ProfilerMarker BuildPlacementPoolMarker = new ProfilerMarker("Level4Rules.BuildPlacementPool");

        private static List<Level4Layout> _placementPool;

        /// <summary> 반복하기를 써야만 풀리는 배치 목록. 상수로만 정해지므로 처음 쓸 때 한 번만 만들어 재사용함. </summary>
        internal static IReadOnlyList<Level4Layout> PlacementPool
        {
            get
            {
                if (_placementPool == null)
                {
                    using (BuildPlacementPoolMarker.Auto())
                    {
                        _placementPool = BuildPlacementPool();
                    }
                }

                return _placementPool;
            }
        }

        /// <summary> 이동 방향 id를 열/행 변화량으로 바꿈. 알 수 없는 id면 false. </summary>
        internal static bool TryGetMoveStep(string directionId, out Level4MoveStep step)
        {
            for (int i = 0; i < DirectionIds.Length; i++)
            {
                if (string.Equals(DirectionIds[i], directionId, System.StringComparison.Ordinal))
                {
                    step = DirectionSteps[i];
                    return true;
                }
            }

            step = default;
            return false;
        }

        /// <summary> 지정한 열/행이 그리드 범위(0~3)를 벗어나는지. </summary>
        internal static bool IsOutOfBounds(int column, int row) => column < 0 || column >= Columns || row < 0 || row >= Rows;

        /// <summary>
        /// 로봇을 step만큼 한 칸 옮겨 보고 결과를 돌려줌. 그리드 밖이면 위치를 바꾸지 않음. 자원 칸에 처음 닿으면 collected를 켬.
        /// 기지에 닿으면 성공 여부는 호출부가 collected로 판단함.
        /// </summary>
        internal static Level4StepResult Step(in Level4Layout layout, ref int column, ref int row, ref bool collected, Level4MoveStep step)
        {
            int nextColumn = column + step.DeltaColumn;
            int nextRow = row + step.DeltaRow;
            if (IsOutOfBounds(nextColumn, nextRow)) return Level4StepResult.OutOfBounds;

            column = nextColumn;
            row = nextRow;

            if (column == ResourceColumn && row == layout.ResourceRow && !collected)
            {
                collected = true;
                return Level4StepResult.CollectedResource;
            }

            if (column == TrapColumn && row == layout.TrapRow) return Level4StepResult.Trap;
            if (column == HqColumn && row == layout.HqRow) return Level4StepResult.Hq;
            return Level4StepResult.Moved;
        }

        /// <summary>
        /// 반복하기를 써야만 풀리는 배치 목록을 새로 만듦. 로봇과 기지가 같은 행인 일자 경로는 빼고, 이동하기만으로는 카드 MaxCards장 안에
        /// 풀 수 없지만 '반복하기(RequiredRepeatCount회) + 이동하기'를 쓰면 MaxCards장 안에 풀리는 조합만 남김. 보통은 캐시된 PlacementPool을 씀.
        /// </summary>
        internal static List<Level4Layout> BuildPlacementPool()
        {
            List<Level4Layout> pool = new List<Level4Layout>();
            int maxCards = Constants.Level4Board.MaxCards;
            int repeatCount = Constants.Level4Board.RequiredRepeatCount;

            for (int robotRow = 0; robotRow < Rows; robotRow++)
            {
                for (int resourceRow = 0; resourceRow < Rows; resourceRow++)
                {
                    for (int hqRow = 0; hqRow < Rows; hqRow++)
                    {
                        if (hqRow == robotRow) continue; // 일자 진행 방지

                        for (int trapRow = 0; trapRow < Rows; trapRow++)
                        {
                            Level4Layout layout = new Level4Layout(robotRow, resourceRow, trapRow, hqRow);
                            if (!CanClearWithin(layout, maxCards, 1) && CanClearWithin(layout, maxCards, repeatCount))
                            {
                                pool.Add(layout);
                            }
                        }
                    }
                }
            }

            return pool;
        }

        /// <summary>
        /// 이 배치를 카드 maxCards장 안에 풀 수 있는지 판정함. Step과 같은 규칙으로 가능한 카드 조합을 모두 따져 봄.
        /// repeatCount가 2 이상이면 '반복하기(repeatCount회) + 이동하기' 두 장 묶음도 쓰고, 1이면 이동하기만 씀.
        /// </summary>
        internal static bool CanClearWithin(Level4Layout layout, int maxCards, int repeatCount)
        {
            return CanClearFrom(layout, RobotColumn, layout.RobotRow, false, maxCards, repeatCount);
        }

        /// <summary>
        /// AI 정답(단계별 칸·결과 화면 AI 설계창)으로 쓸, 이 배치를 가장 적은 카드로 푸는 경로를 찾음(카드 1장부터 maxCards장까지 늘려 가며 탐색).
        /// 이동하기 한 장 외에 repeatCounts의 횟수마다 '반복하기(횟수) + 이동하기' 두 장 묶음을 쓰고, 마지막 구간의 마지막 칸에서 자원을 모은 채
        /// 기지에 정확히 닿는 경로만 정답으로 봄(반복 도중 기지에 먼저 닿아 남은 이동을 하지 않는 경로는 정답으로 안내하지 않음).
        /// 같은 장수로 푸는 경로가 여럿이면 반복하기를 가장 많이 쓰는 경로를 고르고(반복 블록을 우선 안내), 반복하기 수까지 같으면
        /// 탐색 순서(위·아래·오른쪽·왼쪽, 방향마다 이동하기 한 장 → repeatCounts 순서)로 먼저 찾은 경로를 씀.
        /// 반환하는 구간 하나는 (이동 칸 수, 이동 방향 id)이며, 칸 수가 1이면 이동하기 한 장, 그보다 크면 '반복하기(칸 수) + 이동하기' 두 장임.
        /// maxCards장 안에 풀 수 없으면 null.
        /// </summary>
        internal static List<(int moves, string directionId)> FindShortestSolution(Level4Layout layout, int maxCards, IReadOnlyList<int> repeatCounts)
        {
            List<(int moves, string directionId)> path = new List<(int moves, string directionId)>();
            List<(int moves, string directionId)> best = new List<(int moves, string directionId)>();
            for (int cards = 1; cards <= maxCards; cards++)
            {
                int bestRepeats = -1;
                CollectMostRepeats(layout, RobotColumn, layout.RobotRow, false, cards, repeatCounts, path, best, ref bestRepeats);
                if (bestRepeats >= 0) return best; // 더 적은 장수로는 못 풀었으므로 찾은 경로는 모두 이 장수를 씀
            }

            return null;
        }

        /// <summary> 현재 칸에서 이동하기 한 장 또는 반복하기+이동하기 두 장을 방향마다 놓아 보며, 남은 카드로 성공할 수 있는지 재귀로 찾음(하나만 찾으면 멈춤). </summary>
        private static bool CanClearFrom(Level4Layout layout, int column, int row, bool collected, int cardsLeft, int repeatCount)
        {
            for (int d = 0; d < DirectionSteps.Length; d++)
            {
                if (cardsLeft >= 1 && RunSucceeds(layout, column, row, collected, DirectionSteps[d], 1, cardsLeft - 1, repeatCount)) return true;
                if (repeatCount > 1 && cardsLeft >= 2 && RunSucceeds(layout, column, row, collected, DirectionSteps[d], repeatCount, cardsLeft - 2, repeatCount)) return true;
            }

            return false;
        }

        /// <summary> direction으로 moves칸 가 봄. 기지에 닿으면 자원을 먼저 모았는지가 곧 결과이고, 그리드 밖·함정이면 실패, 그 외에는 남은 카드로 이어서 찾음. </summary>
        private static bool RunSucceeds(Level4Layout layout, int column, int row, bool collected, Level4MoveStep direction, int moves, int cardsLeft, int repeatCount)
        {
            switch (Walk(layout, ref column, ref row, ref collected, direction, moves, out _))
            {
                case Level4StepResult.OutOfBounds:
                case Level4StepResult.Trap:
                    return false;
                case Level4StepResult.Hq:
                    return collected;
                default:
                    return cardsLeft > 0 && CanClearFrom(layout, column, row, collected, cardsLeft, repeatCount);
            }
        }

        /// <summary>
        /// 현재 칸에서 이동하기 한 장, 또는 repeatCounts의 횟수마다 반복하기+이동하기 두 장을 방향마다 놓아 보며 남은 카드로 기지에 정확히 닿는 경로를
        /// 모두 따져, 반복하기를 가장 많이 쓰는 경로를 best에 남김(bestRepeats는 그 반복하기 수, 아직 없으면 -1). 반복하기 수가 같으면 먼저 찾은 경로를 둠.
        /// </summary>
        private static void CollectMostRepeats(Level4Layout layout, int column, int row, bool collected, int cardsLeft, IReadOnlyList<int> repeatCounts,
            List<(int moves, string directionId)> path, List<(int moves, string directionId)> best, ref int bestRepeats)
        {
            for (int d = 0; d < DirectionSteps.Length; d++)
            {
                if (cardsLeft >= 1) CollectRun(layout, column, row, collected, d, 1, cardsLeft - 1, repeatCounts, path, best, ref bestRepeats);
                if (cardsLeft < 2) continue; // 반복하기+이동하기 두 장을 놓을 카드가 없음(정상)

                for (int r = 0; r < repeatCounts.Count; r++)
                {
                    if (repeatCounts[r] > 1) CollectRun(layout, column, row, collected, d, repeatCounts[r], cardsLeft - 2, repeatCounts, path, best, ref bestRepeats);
                }
            }
        }

        /// <summary>
        /// 구간 하나(directionIndex 방향으로 moves칸)를 경로에 넣고 가 봄. 이 구간의 마지막 칸에서 자원을 모은 채 기지에 닿으면 후보로 비교하고,
        /// 기지·그리드 밖·함정 없이 끝났으면 남은 카드로 이어서 찾은 뒤 경로에서 다시 뺌. 구간 도중에 기지에 닿으면 정답으로 보지 않음.
        /// </summary>
        private static void CollectRun(Level4Layout layout, int column, int row, bool collected, int directionIndex, int moves, int cardsLeft, IReadOnlyList<int> repeatCounts,
            List<(int moves, string directionId)> path, List<(int moves, string directionId)> best, ref int bestRepeats)
        {
            path.Add((moves, DirectionIds[directionIndex]));
            switch (Walk(layout, ref column, ref row, ref collected, DirectionSteps[directionIndex], moves, out int stepsTaken))
            {
                case Level4StepResult.Hq:
                    if (collected && stepsTaken == moves) KeepIfMoreRepeats(path, best, ref bestRepeats);
                    break;
                case Level4StepResult.Moved:
                    if (cardsLeft > 0) CollectMostRepeats(layout, column, row, collected, cardsLeft, repeatCounts, path, best, ref bestRepeats);
                    break;
            }

            path.RemoveAt(path.Count - 1);
        }

        /// <summary> path의 반복하기 수(이동 칸 수가 1보다 큰 구간 수)가 bestRepeats보다 많으면 best를 path로 바꿈. </summary>
        private static void KeepIfMoreRepeats(List<(int moves, string directionId)> path, List<(int moves, string directionId)> best, ref int bestRepeats)
        {
            int repeats = 0;
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i].moves > 1) repeats++;
            }

            if (repeats <= bestRepeats) return; // 같으면 먼저 찾은 경로를 둠(정상)

            bestRepeats = repeats;
            best.Clear();
            best.AddRange(path);
        }

        /// <summary>
        /// direction으로 moves칸을 Step으로 한 칸씩 가 봄. 그리드 밖·함정·기지에 닿으면 그 결과를 바로 돌려주고(기지면 성공 여부는 collected로 판단),
        /// 끝까지 갔으면 Moved. column·row·collected는 마지막으로 간 칸 기준으로 바뀌고, stepsTaken은 결과가 난 칸까지 간 칸 수.
        /// </summary>
        private static Level4StepResult Walk(in Level4Layout layout, ref int column, ref int row, ref bool collected, Level4MoveStep direction, int moves, out int stepsTaken)
        {
            for (int i = 0; i < moves; i++)
            {
                Level4StepResult result = Step(layout, ref column, ref row, ref collected, direction);
                if (result == Level4StepResult.OutOfBounds || result == Level4StepResult.Trap || result == Level4StepResult.Hq)
                {
                    stepsTaken = i + 1;
                    return result;
                }
            }

            stepsTaken = moves;
            return Level4StepResult.Moved;
        }
    }

    /// <summary> 한 칸 이동(Level4Rules.Step)의 결과. </summary>
    internal enum Level4StepResult
    {
        Moved,
        CollectedResource,
        OutOfBounds,
        Trap,
        Hq
    }

    /// <summary> 한 칸 이동을 나타내는 열/행 변화량. </summary>
    internal readonly struct Level4MoveStep
    {
        public readonly int DeltaColumn;
        public readonly int DeltaRow;

        /// <summary> 열/행 변화량으로 이동 스텝을 초기화함. </summary>
        public Level4MoveStep(int deltaColumn, int deltaRow)
        {
            DeltaColumn = deltaColumn;
            DeltaRow = deltaRow;
        }
    }

    /// <summary> 로봇·자원·함정·기지의 행 배치 하나(열은 로봇=0, 자원=1, 함정=2, 기지=3으로 고정). </summary>
    internal readonly struct Level4Layout
    {
        public readonly int RobotRow;
        public readonly int ResourceRow;
        public readonly int TrapRow;
        public readonly int HqRow;

        /// <summary> 각 아이콘의 행으로 배치를 초기화함. </summary>
        public Level4Layout(int robotRow, int resourceRow, int trapRow, int hqRow)
        {
            RobotRow = robotRow;
            ResourceRow = resourceRow;
            TrapRow = trapRow;
            HqRow = hqRow;
        }

        /// <summary> 로그·테스트 메시지용 배치 표기. </summary>
        public override string ToString() => $"로봇 R{RobotRow}, 자원 R{ResourceRow}, 함정 R{TrapRow}, 기지 R{HqRow}";
    }
}
