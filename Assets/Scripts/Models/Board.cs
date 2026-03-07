using System.Collections.Generic;

public sealed class Board
{
    private readonly List<Arrow> _arrows = new();

    public IReadOnlyList<Arrow> Arrows => _arrows;
    public int Width { get; }
    public int Height { get; }
    /// <summary>Incremented on every structural mutation. Use to detect external desync.</summary>
    public int Version { get; private set; }

    public Board(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public void AddArrow(Arrow arrow) { _arrows.Add(arrow); Version++; }
    public void RemoveArrow(Arrow arrow) { _arrows.Remove(arrow); Version++; }

    public bool Contains(Cell cell)
    {
        return cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
    }
}
