using System.Collections.Generic;

public sealed class BoardModel
{
    private readonly List<ArrowModel> _arrows = new();
    private readonly Dictionary<BoardCell, ArrowModel> _occupancy = new();

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<ArrowModel> Arrows => _arrows;

    public BoardModel(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public bool Contains(BoardCell cell)
    {
        return cell.X >= 0
            && cell.X < Width
            && cell.Y >= 0
            && cell.Y < Height;
    }

    public bool IsOccupied(BoardCell cell)
    {
        return _occupancy.ContainsKey(cell);
    }

    public bool TryAddArrow(ArrowModel arrow)
    {
        if (!CanPlaceArrow(arrow))
        {
            return false;
        }

        _arrows.Add(arrow);
        foreach (BoardCell cell in arrow.Cells)
        {
            _occupancy[cell] = arrow;
        }

        return true;
    }

    public bool TryRemoveArrow(ArrowModel arrow)
    {
        if (!_arrows.Contains(arrow))
        {
            return false;
        }

        if (!CanRemoveArrow(arrow))
        {
            return false;
        }

        _arrows.Remove(arrow);
        foreach (BoardCell cell in arrow.Cells)
        {
            _occupancy.Remove(cell);
        }

        return true;
    }

    public bool CanPlaceArrow(ArrowModel arrow)
    {
        if (arrow == null)
        {
            return false;
        }

        if (arrow.Cells.Count == 0)
        {
            return false;
        }
        HashSet<BoardCell> candidateCells = new(arrow.Cells);

        foreach (BoardCell cell in arrow.Cells)
        {
            if (!Contains(cell))
            {
                return false;
            }

            if (_occupancy.ContainsKey(cell))
            {
                return false;
            }
        }
        BoardCell currentHead = arrow.HeadCell;
        ArrowDirection currentDirection = arrow.HeadDirection;
        HashSet<ArrowModel> visitedExistingArrows = new();

        while (true)
        {
            if (!TryFindFirstBlockingArrow(currentHead, currentDirection, candidateCells, out ArrowModel blockingArrow, out bool blocksByCandidate))
            {
                return true;
            }

            if (blocksByCandidate)
            {
                return false;
            }

            if (!visitedExistingArrows.Add(blockingArrow))
            {
                return false;
            }

            currentHead = blockingArrow.HeadCell;
            currentDirection = blockingArrow.HeadDirection;
        }
    }

    public bool CanRemoveArrow(ArrowModel arrow)
    {
        if (arrow == null)
        {
            return false;
        }

        return FindFirstArrowInHeadDirection(arrow, _occupancy) == null;
    }

    public IEnumerable<BoardCell> GetFreeBoardCells()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                BoardCell cell = new BoardCell(x, y);
                if (!IsOccupied(cell)) yield return cell;
            }
        }
    }

    private bool TryFindFirstBlockingArrow(
        BoardCell head,
        ArrowDirection direction,
        HashSet<BoardCell> candidateCells,
        out ArrowModel blockingArrow,
        out bool blocksByCandidate)
    {
        (int dx, int dy) = ArrowModel.GetDirectionStep(direction);
        BoardCell cell = head;

        while (true)
        {
            cell = new BoardCell(cell.X + dx, cell.Y + dy);
            if (!Contains(cell))
            {
                blockingArrow = null;
                blocksByCandidate = false;
                return false;
            }

            if (candidateCells.Contains(cell))
            {
                blockingArrow = null;
                blocksByCandidate = true;
                return true;
            }

            if (_occupancy.TryGetValue(cell, out blockingArrow))
            {
                blocksByCandidate = false;
                return true;
            }
        }
    }

    private ArrowModel FindFirstArrowInHeadDirection(ArrowModel arrow, Dictionary<BoardCell, ArrowModel> occupancy)
    {
        (int dx, int dy) = ArrowModel.GetDirectionStep(arrow.HeadDirection);
        BoardCell cell = arrow.HeadCell;

        while (true)
        {
            cell = new BoardCell(cell.X + dx, cell.Y + dy);
            if (!Contains(cell))
            {
                return null;
            }

            if (occupancy.TryGetValue(cell, out ArrowModel blockingArrow))
            {
                return blockingArrow;
            }
        }
    }
}
