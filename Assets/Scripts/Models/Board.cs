using System.Collections.Generic;

public sealed class Board
{
    private readonly List<Arrow> _arrows = new();
    private readonly Arrow?[,] _occupancy;

    public IReadOnlyList<Arrow> Arrows => _arrows;
    public int Width { get; }
    public int Height { get; }
    /// <summary>Incremented on every structural mutation. Use to detect external desync.</summary>
    public int Version { get; private set; }

    public Board(int width, int height)
    {
        Width = width;
        Height = height;
        _occupancy = new Arrow?[width, height];
    }

    public void AddArrow(Arrow arrow)
    {
        _arrows.Add(arrow);
        foreach (Cell c in arrow.Cells) _occupancy[c.X, c.Y] = arrow;
        Version++;
    }

    public void RemoveArrow(Arrow arrow)
    {
        _arrows.Remove(arrow);
        foreach (Cell c in arrow.Cells) _occupancy[c.X, c.Y] = null;
        Version++;
    }

    public bool Contains(Cell cell)
    {
        return cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
    }

    /// <summary>Returns the arrow occupying <paramref name="cell"/>, or null if empty or out of bounds.</summary>
    public Arrow? GetArrowAt(Cell cell) => Contains(cell) ? _occupancy[cell.X, cell.Y] : null;

    /// <summary>
    /// Returns true if <paramref name="arrow"/> can be cleared — i.e. the forward ray from its
    /// head to the board boundary contains no other arrow's cells.
    /// </summary>
    public bool IsClearable(Arrow arrow)
    {
        (int dx, int dy) = Arrow.GetDirectionStep(arrow.HeadDirection);
        Cell cursor = new(arrow.HeadCell.X + dx, arrow.HeadCell.Y + dy);
        while (Contains(cursor))
        {
            Arrow? occupant = _occupancy[cursor.X, cursor.Y];
            if (occupant != null && occupant != arrow)
                return false;
            cursor = new Cell(cursor.X + dx, cursor.Y + dy);
        }
        return true;
    }
}
