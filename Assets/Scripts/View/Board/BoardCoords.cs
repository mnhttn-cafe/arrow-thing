using UnityEngine;

/// <summary>
/// Converts between board grid coordinates and world-space positions.
/// Cell (0,0) maps to world origin (bottom-left corner). Each cell is 1×1 Unity unit.
/// </summary>
public static class BoardCoords
{
    /// <summary>
    /// Returns the world-space center of the given cell.
    /// Cell (0,0) is at world origin (bottom-left).
    /// </summary>
    public static Vector3 CellToWorld(Cell cell, int boardWidth, int boardHeight)
    {
        return new Vector3(cell.X, cell.Y, 0f);
    }

    /// <summary>
    /// Returns the nearest cell for a world-space position. Does NOT bounds-check.
    /// </summary>
    public static Cell WorldToCell(Vector3 worldPos, int boardWidth, int boardHeight)
    {
        int x = Mathf.RoundToInt(worldPos.x);
        int y = Mathf.RoundToInt(worldPos.y);
        return new Cell(x, y);
    }

    /// <summary>
    /// Converts an arrow path (list of cells) into world-space points for mesh building.
    /// </summary>
    public static Vector3[] ArrowPathToWorld(Arrow arrow, int boardWidth, int boardHeight)
    {
        var points = new Vector3[arrow.Cells.Count];
        for (int i = 0; i < arrow.Cells.Count; i++)
            points[i] = CellToWorld(arrow.Cells[i], boardWidth, boardHeight);
        return points;
    }
}
