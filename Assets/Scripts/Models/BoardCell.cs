using System;

public readonly struct BoardCell : IEquatable<BoardCell>
{
    public int X { get; }
    public int Y { get; }

    public BoardCell(int x, int y)
    {
        X = x;
        Y = y;
    }

    public bool Equals(BoardCell other)
    {
        return X == other.X && Y == other.Y;
    }

    public override bool Equals(object obj)
    {
        return obj is BoardCell other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public static bool operator ==(BoardCell left, BoardCell right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(BoardCell left, BoardCell right)
    {
        return !left.Equals(right);
    }
}
