using System;
using System.Collections.Generic;

public sealed class BoardGenerator
{
    private readonly Random _random;

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
        if (board == null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        if (arrowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrowCount), "Arrow count cannot be negative.");
        }

        ValidateLengthRange(minLength, maxLength);

        int countSoFar = 0;

        while(countSoFar < arrowCount)
        {
            bool success = TryGenerateSingleArrow(board, minLength, maxLength, out ArrowModel arrow);
            if(success)
            {
                board.TryAddArrow(arrow);
                countSoFar++;
            }
            else break;
        }

        return countSoFar;
    }

    public bool TryGenerateSingleArrow(
        BoardModel board,
        int minLength,
        int maxLength,
        out ArrowModel arrow)
    {
        if (board == null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        ValidateLengthRange(minLength, maxLength);

        arrow = null;

        List<ArrowModel> validMinimalArrows = BuildValidMinimalArrows(board);

        while (validMinimalArrows.Count > 0)
        {
            int index = _random.Next(validMinimalArrows.Count);
            ArrowModel candidateArrow = validMinimalArrows[index];
            HashSet<BoardCell> headRaySet = BuildHeadRaySet(board, candidateArrow);

            int currentArrowMaxLength = maxLength;

            while (currentArrowMaxLength >= minLength)
            {
                int length = _random.Next(Math.Max(2, minLength), currentArrowMaxLength + 1);
                if (length == 2)
                {
                    arrow = candidateArrow;
                    return true;
                }

                List<BoardCell> path = new(candidateArrow.Cells);
                if (TryGrowTailWithDfs(board, path, length, headRaySet))
                {
                    arrow = new(path);
                    return true;
                }
                else
                {
                    currentArrowMaxLength--;
                }
            }

            validMinimalArrows.RemoveAt(index);
        }

        return false;
    }

    private List<ArrowModel> BuildValidMinimalArrows(BoardModel board)
    {
        List<ArrowModel> validArrows = new();

        List<BoardCell> freeCells = new(board.GetFreeBoardCells());

        foreach (BoardCell head in freeCells)
        {
            foreach (BoardCell next in EnumerateOrthogonalNeighbors(head))
            {
                ArrowModel testArrow = new ArrowModel(new[] { head, next });
                if (board.CanPlaceArrow(testArrow)) validArrows.Add(testArrow);
            }
        }

        return validArrows;
    }

    private HashSet<BoardCell> BuildHeadRaySet(BoardModel board, ArrowModel arrow)
    {
        HashSet<BoardCell> cellsInFront = new();
        BoardCell current = arrow.HeadCell;
        (int dx, int dy) = ArrowModel.GetDirectionStep(arrow.HeadDirection);

        while (true)
        {
            current = new BoardCell(current.X + dx, current.Y + dy);
            if (board.Contains(current))
            {
                cellsInFront.Add(current);
            }
            else return cellsInFront;
        }
    }

    private bool TryGrowTailWithDfs(
        BoardModel board,
        List<BoardCell> path,
        int targetLength,
        HashSet<BoardCell> headRaySet)
    {
        if(path.Count == targetLength) return true;

        List<BoardCell> shuffledNeighbors = new(EnumerateOrthogonalNeighbors(path[^1]));
        ShuffleInPlace(shuffledNeighbors);

        foreach (BoardCell neighbor in shuffledNeighbors)
        {
            if(!board.Contains(neighbor)) continue;
            if(board.IsOccupied(neighbor)) continue;
            if(path.Contains(neighbor)) continue;
            if(headRaySet.Contains(neighbor)) continue;

            path.Add(neighbor);
            if(board.CanPlaceArrow(new(path)) && TryGrowTailWithDfs(board, path, targetLength, headRaySet))
            {
                return true;
            }
            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    private static IEnumerable<BoardCell> EnumerateOrthogonalNeighbors(BoardCell cell)
    {
        yield return new BoardCell(cell.X, cell.Y - 1);
        yield return new BoardCell(cell.X + 1, cell.Y);
        yield return new BoardCell(cell.X, cell.Y + 1);
        yield return new BoardCell(cell.X - 1, cell.Y);
    }

    private void ShuffleInPlace<T>(IList<T> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static void ValidateLengthRange(int minLength, int maxLength)
    {
        if (minLength < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(minLength), "Minimum arrow length must be at least 2.");
        }

        if (maxLength < minLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Maximum arrow length must be greater than or equal to minimum arrow length.");
        }
    }
}
