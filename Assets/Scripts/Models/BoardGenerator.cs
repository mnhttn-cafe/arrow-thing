using System;
using System.Collections.Generic;

public sealed class BoardGenerator
{
    private const int MaxStarterChecksOnLargeBoards = 256;
    private const int RandomPathAttemptsPerLength = 3;

    private readonly Random _random;
    private List<ArrowModel> _possibleStarters;
    private HashSet<BoardCell> _occupiedCells;
    private Dictionary<BoardCell, ArrowDirection> _arrowHeads;
    private Dictionary<BoardCell, BoardCell> _occupiedOwnerHeads;

    public BoardGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public int FillBoard(
        BoardModel board,
        int arrowCount,
        int minLength,
        int maxLength)
    {
        if (board == null) throw new ArgumentNullException(nameof(board));
        if (arrowCount < 0) throw new ArgumentOutOfRangeException(nameof(arrowCount));
        ValidateLengthRange(minLength, maxLength);

        InitializeStateFromBoard(board);
        InitializePossibleStarters(board);

        int placedCount = 0;
        int consecutiveFailures = 0;

        while (placedCount < arrowCount)
        {
            bool success = TryGenerateSingleArrow(board, minLength, maxLength, out ArrowModel arrow);

            if (success && board.TryAddArrow(arrow))
            {
                UpdateAfterPlacement(board, arrow);
                placedCount++;
                consecutiveFailures = 0;
            }
            else
            {
                consecutiveFailures++;

                // Greedy fallback: place any valid arrow within length range when struggling
                if (consecutiveFailures >= 8 && consecutiveFailures % 4 == 0 && _possibleStarters.Count > 0)
                {
                    ArrowModel fallback = TryBuildFallbackArrow(board, minLength, maxLength);
                    if (fallback != null && board.TryAddArrow(fallback))
                    {
                        UpdateAfterPlacement(board, fallback);
                        placedCount++;
                        consecutiveFailures = 0;
                        continue;
                    }
                }

                // Can't place anything meaningful anymore → stop
                if (consecutiveFailures > 24 || _possibleStarters.Count == 0)
                {
                    break;
                }
            }
        }

        return placedCount;
    }

    private void InitializePossibleStarters(BoardModel board)
    {
        _possibleStarters = BuildValidMinimalArrows(board);
    }

    private void UpdateAfterPlacement(BoardModel board, ArrowModel placed)
    {
        foreach (var cell in placed.Cells)
        {
            _occupiedCells.Add(cell);
            _occupiedOwnerHeads[cell] = placed.HeadCell;
        }

        _arrowHeads[placed.HeadCell] = placed.HeadDirection;
    }

    public bool TryGenerateSingleArrow(
        BoardModel board,
        int minLength,
        int maxLength,
        out ArrowModel arrow)
    {
        ValidateLengthRange(minLength, maxLength);
        arrow = null;

        if (_possibleStarters == null)
        {
            InitializeStateFromBoard(board);
            InitializePossibleStarters(board);
        }

        if (_possibleStarters.Count == 0)
        {
            return false;
        }

        int startIndex = _random.Next(_possibleStarters.Count);
        int maxChecks = _possibleStarters.Count > 1024
            ? Math.Min(_possibleStarters.Count, MaxStarterChecksOnLargeBoards)
            : _possibleStarters.Count;

        for (int checkedCount = 0; checkedCount < maxChecks && _possibleStarters.Count > 0;)
        {
            if (startIndex >= _possibleStarters.Count)
            {
                startIndex = 0;
            }

            int candidateIndex = startIndex;
            ArrowModel candidate = _possibleStarters[candidateIndex];

            if (!IsStarterUsable(board, candidate))
            {
                _possibleStarters.RemoveAt(candidateIndex);
                continue;
            }

            checkedCount++;

            // Sample uniformly from [minLength, maxLength]
            int targetLen = _random.Next(minLength, maxLength + 1);
            
            // Try target length first, then only higher lengths if target fails
            for (int len = targetLen; len <= maxLength; len++)
            {
                if (TryBuildArrowPath(
                        board,
                        candidate,
                        len,
                        out List<BoardCell> path))
                {
                    arrow = new ArrowModel(path);
                    _possibleStarters.RemoveAt(candidateIndex);
                    return true;
                }
            }

            startIndex++;
        }

        return false;
    }

    private bool TryBuildArrowPath(
        BoardModel board,
        ArrowModel starter,
        int targetLength,
        out List<BoardCell> path)
    {
        path = null;

        for (int attempt = 0; attempt < RandomPathAttemptsPerLength; attempt++)
        {
            List<BoardCell> workingPath = new(targetLength)
            {
                starter.Cells[0],
                starter.Cells[1]
            };

            HashSet<BoardCell> pathSet = new(workingPath);

            while (workingPath.Count < targetLength)
            {
                if (!TryPickNextTailCell(
                        board,
                        workingPath,
                        pathSet,
                        starter.HeadCell,
                        starter.HeadDirection,
                        out BoardCell nextCell))
                {
                    break;
                }

                workingPath.Add(nextCell);
                pathSet.Add(nextCell);
            }

            if (workingPath.Count != targetLength)
            {
                continue;
            }

            if (WouldPlacementCreateCycle(board, pathSet, starter.HeadCell, starter.HeadDirection))
            {
                continue;
            }

            path = workingPath;
            return true;
        }

        return false;
    }

    private bool TryPickNextTailCell(
        BoardModel board,
        List<BoardCell> path,
        HashSet<BoardCell> pathSet,
        BoardCell headCell,
        ArrowDirection headDirection,
        out BoardCell nextCell)
    {
        nextCell = default;

        BoardCell tail = path[^1];
        BoardCell[] options = new BoardCell[4];
        int optionCount = 0;

        foreach (BoardCell n in EnumerateOrthogonalNeighbors(tail))
        {
            if (!board.Contains(n)) continue;
            if (_occupiedCells.Contains(n)) continue;
            if (pathSet.Contains(n)) continue;
            if (IsOnHeadRay(headCell, headDirection, n)) continue;
            
            pathSet.Add(n);
            bool createsCycle = WouldPlacementCreateCycle(board, pathSet, headCell, headDirection);
            pathSet.Remove(n);
            
            if (createsCycle) {
                continue;
            }

            options[optionCount++] = n;
        }

        if (optionCount == 0)
        {
            return false;
        }

        // Keep some randomness but prefer cells that preserve future options.
        int bestScore = int.MinValue;
        int bestIndex = 0;

        for (int i = 0; i < optionCount; i++)
        {
            BoardCell candidate = options[i];
            int score = CountAvailableExits(board, candidate, pathSet, headCell, headDirection);
            score += _random.Next(2); // light tie-break randomness

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        nextCell = options[bestIndex];
        return true;
    }

    private int CountAvailableExits(
        BoardModel board,
        BoardCell cell,
        HashSet<BoardCell> pathSet,
        BoardCell headCell,
        ArrowDirection headDirection)
    {
        int count = 0;
        foreach (BoardCell n in EnumerateOrthogonalNeighbors(cell))
        {
            if (!board.Contains(n)) continue;
            if (_occupiedCells.Contains(n)) continue;
            if (pathSet.Contains(n)) continue;
            if (IsOnHeadRay(headCell, headDirection, n)) continue;
            count++;
        }

        return count;
    }

    private List<ArrowModel> BuildValidMinimalArrows(BoardModel board)
    {
        List<ArrowModel> valid = new(board.Width * board.Height * 2);

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                BoardCell head = new(x, y);
                if (_occupiedCells.Contains(head)) continue;

                foreach (BoardCell next in EnumerateOrthogonalNeighbors(head))
                {
                    if (!board.Contains(next)) continue;
                    if (_occupiedCells.Contains(next)) continue;
                    valid.Add(new ArrowModel(new[] { head, next }));
                }
            }
        }

        return valid;
    }

    private void InitializeStateFromBoard(BoardModel board)
    {
        _occupiedCells = new HashSet<BoardCell>();
        _arrowHeads = new Dictionary<BoardCell, ArrowDirection>();
        _occupiedOwnerHeads = new Dictionary<BoardCell, BoardCell>();

        foreach (ArrowModel existing in board.Arrows)
        {
            _arrowHeads[existing.HeadCell] = existing.HeadDirection;
            foreach (BoardCell cell in existing.Cells)
            {
                _occupiedCells.Add(cell);
                _occupiedOwnerHeads[cell] = existing.HeadCell;
            }
        }
    }

    private bool IsStarterUsable(BoardModel board, ArrowModel starter)
    {
        BoardCell head = starter.Cells[0];
        BoardCell next = starter.Cells[1];

        if (_occupiedCells.Contains(head)) return false;
        if (_occupiedCells.Contains(next)) return false;
        return true;
    }

    private ArrowModel TryBuildFallbackArrow(BoardModel board, int minLength, int maxLength)
    {
        if (_possibleStarters.Count == 0)
        {
            return null;
        }

        int attempts = Math.Min(_possibleStarters.Count, 24);
        int startIndex = _random.Next(_possibleStarters.Count);

        for (int i = 0; i < attempts; i++)
        {
            int index = (startIndex + i) % _possibleStarters.Count;
            ArrowModel starter = _possibleStarters[index];
            if (!IsStarterUsable(board, starter))
            {
                continue;
            }

            // Sample uniformly from [minLength, maxLength]
            int targetLen = _random.Next(minLength, maxLength + 1);
            
            // Try target length first, then only higher lengths if target fails
            for (int len = targetLen; len <= maxLength; len++)
            {
                if (TryBuildArrowPath(board, starter, len, out List<BoardCell> path))
                {
                    _possibleStarters.RemoveAt(index);
                    return new ArrowModel(path);
                }
            }
        }

        return null;
    }

    private static bool IsOnHeadRay(BoardCell headCell, ArrowDirection direction, BoardCell candidate)
    {
        return direction switch
        {
            ArrowDirection.Up => candidate.X == headCell.X && candidate.Y > headCell.Y,
            ArrowDirection.Right => candidate.Y == headCell.Y && candidate.X < headCell.X,
            ArrowDirection.Down => candidate.X == headCell.X && candidate.Y < headCell.Y,
            ArrowDirection.Left => candidate.Y == headCell.Y && candidate.X > headCell.X,
            _ => false
        };
    }

    private bool WouldPlacementCreateCycle(
        BoardModel board,
        HashSet<BoardCell> candidateCells,
        BoardCell candidateHead,
        ArrowDirection candidateDirection)
    {
        BoardCell currentHead = candidateHead;
        ArrowDirection currentDirection = candidateDirection;
        HashSet<BoardCell> visitedExistingHeads = new();

        while (true)
        {
            if (!TryFindFirstBlockingHead(
                    board,
                    currentHead,
                    currentDirection,
                    candidateCells,
                    out BoardCell blockingHead,
                    out bool blocksByCandidate))
            {
                return false;
            }

            if (blocksByCandidate)
            {
                return true;
            }

            if (!visitedExistingHeads.Add(blockingHead))
            {
                return true;
            }

            if (!_arrowHeads.TryGetValue(blockingHead, out currentDirection))
            {
                return false;
            }

            currentHead = blockingHead;
        }
    }

    private bool TryFindFirstBlockingHead(
        BoardModel board,
        BoardCell head,
        ArrowDirection direction,
        HashSet<BoardCell> candidateCells,
        out BoardCell blockingHead,
        out bool blocksByCandidate)
    {
        (int dx, int dy) = ArrowModel.GetDirectionStep(direction);
        BoardCell current = head;

        while (true)
        {
            current = new BoardCell(current.X + dx, current.Y + dy);
            if (!board.Contains(current))
            {
                blockingHead = default;
                blocksByCandidate = false;
                return false;
            }

            if (candidateCells.Contains(current))
            {
                blockingHead = default;
                blocksByCandidate = true;
                return true;
            }

            if (_occupiedOwnerHeads.TryGetValue(current, out blockingHead))
            {
                blocksByCandidate = false;
                return true;
            }
        }
    }

    private static IEnumerable<BoardCell> EnumerateOrthogonalNeighbors(BoardCell cell)
    {
        yield return new BoardCell(cell.X, cell.Y - 1);
        yield return new BoardCell(cell.X + 1, cell.Y);
        yield return new BoardCell(cell.X, cell.Y + 1);
        yield return new BoardCell(cell.X - 1, cell.Y);
    }

    private static void ValidateLengthRange(int minLength, int maxLength)
    {
        if (minLength < 2)
            throw new ArgumentOutOfRangeException(nameof(minLength), "Minimum length must be at least 2.");
        if (maxLength < minLength)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be >= min length.");
    }
}