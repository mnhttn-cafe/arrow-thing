using System.Collections.Generic;

public sealed class Board
{
    public List<Arrow> Arrows = new();
    public int Width { get; }
    public int Height { get; }

    public Board(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public bool Contains(Cell cell)
    {
        return cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
    }
}
