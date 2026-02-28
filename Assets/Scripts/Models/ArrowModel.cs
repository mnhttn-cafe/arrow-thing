using System;
using System.Collections.Generic;

public sealed class ArrowModel
{
    private readonly List<BoardCell> _cells;

    public IReadOnlyList<BoardCell> Cells => _cells;
    public BoardCell HeadCell => _cells[0];
    public ArrowDirection HeadDirection { get; }

    public ArrowModel(IEnumerable<BoardCell> cells)
    {
        _cells = cells == null
            ? throw new ArgumentNullException(nameof(cells))
            : new List<BoardCell>(cells);

        if (_cells.Count < 2)
        {
            throw new ArgumentException("Arrow requires at least 2 cells to derive head direction.", nameof(cells));
        }

        HeadDirection = DeriveHeadDirection(_cells[0], _cells[1]);
    }

    public static (int dx, int dy) GetDirectionStep(ArrowDirection direction)
    {
        return direction switch
        {
            ArrowDirection.Up => (0, -1),
            ArrowDirection.Right => (1, 0),
            ArrowDirection.Down => (0, 1),
            ArrowDirection.Left => (-1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported arrow direction.")
        };
    }

    private static ArrowDirection DeriveHeadDirection(BoardCell head, BoardCell next)
    {
        int dx = next.X - head.X;
        int dy = next.Y - head.Y;

        return (dx, dy) switch
        {
            (0, -1) => ArrowDirection.Down,
            (1, 0) => ArrowDirection.Left,
            (0, 1) => ArrowDirection.Up,
            (-1, 0) => ArrowDirection.Right,
            _ => throw new ArgumentException("Head and next cells must be orthogonally adjacent.")
        };
    }
}
