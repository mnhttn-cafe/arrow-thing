using UnityEngine;

/// <summary>
/// Renders a dotted background grid for the board.
/// Spawns one small sprite per cell center and an optional border outline.
/// </summary>
public sealed class BoardGridRenderer : MonoBehaviour
{
    private Transform? _dotParent;

    /// <summary>
    /// Creates the grid dots for the given board dimensions.
    /// </summary>
    public void Init(Board board, VisualSettings settings)
    {
        _dotParent = new GameObject("GridDots").transform;
        _dotParent.SetParent(transform, false);

        if (settings.boardDotSprite == null)
        {
            Debug.LogWarning("BoardGridRenderer: boardDotSprite is not assigned in VisualSettings.");
            return;
        }

        float dotScale = settings.gridDotScale;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                Vector3 pos = BoardCoords.CellToWorld(new Cell(x, y), board.Width, board.Height);
                var dot = new GameObject($"Dot_{x}_{y}");
                dot.transform.SetParent(_dotParent, false);
                dot.transform.localPosition = pos;
                dot.transform.localScale = new Vector3(dotScale, dotScale, 1f);

                var sr = dot.AddComponent<SpriteRenderer>();
                sr.sprite = settings.boardDotSprite;
                sr.color = settings.gridDotColor;
                sr.sortingOrder = 0;
            }
        }
    }
}
