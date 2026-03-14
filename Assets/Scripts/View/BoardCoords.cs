using UnityEngine;

/// <summary>
/// Converts between board grid coordinates and world-space positions.
/// The board is centered at the world origin. Each cell is 1×1 Unity unit.
/// </summary>
public static class BoardCoords
{
    /// <summary>
    /// Returns the world-space center of the given cell on a board of the specified size.
    /// Board is centered at world origin: cell (0,0) is at bottom-left.
    /// </summary>
    public static Vector3 CellToWorld(Cell cell, int boardWidth, int boardHeight)
    {
        float x = cell.X - (boardWidth - 1) * 0.5f;
        float y = cell.Y - (boardHeight - 1) * 0.5f;
        return new Vector3(x, y, 0f);
    }

    /// <summary>
    /// Returns the nearest cell for a world-space position. Does NOT bounds-check.
    /// </summary>
    public static Cell WorldToCell(Vector3 worldPos, int boardWidth, int boardHeight)
    {
        int x = Mathf.RoundToInt(worldPos.x + (boardWidth - 1) * 0.5f);
        int y = Mathf.RoundToInt(worldPos.y + (boardHeight - 1) * 0.5f);
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
