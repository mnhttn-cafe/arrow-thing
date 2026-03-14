using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the visual representation of the entire board.
/// Owns ArrowView instances and the grid renderer.
/// </summary>
public sealed class BoardView : MonoBehaviour
{
    private Board _board = null!;
    private VisualSettings _settings = null!;
    private readonly Dictionary<Arrow, ArrowView> _arrowViews = new();

    public void Init(Board board, VisualSettings settings)
    {
        _board = board;
        _settings = settings;

        // Grid
        var grid = gameObject.AddComponent<BoardGridRenderer>();
        grid.Init(board, settings);

        // Arrows
        var arrowParent = new GameObject("Arrows").transform;
        arrowParent.SetParent(transform, false);

        foreach (Arrow arrow in board.Arrows)
        {
            var go = new GameObject($"Arrow_{arrow.HeadCell.X}_{arrow.HeadCell.Y}");
            go.transform.SetParent(arrowParent, false);

            var view = go.AddComponent<ArrowView>();
            view.Init(arrow, board.Width, board.Height, settings);
            _arrowViews[arrow] = view;
        }
    }

    /// <summary>
    /// Attempts to clear an arrow. Returns true if it was clearable and removed.
    /// </summary>
    public bool TryClearArrow(Arrow arrow)
    {
        if (!_board.IsClearable(arrow))
        {
            if (_arrowViews.TryGetValue(arrow, out ArrowView? view))
                view.PlayRejectFlash();
            return false;
        }

        RemoveArrow(arrow);
        return true;
    }

    /// <summary>
    /// Returns the ArrowView for a given arrow, or null if not found.
    /// </summary>
    public ArrowView? GetArrowView(Arrow arrow)
    {
        return _arrowViews.TryGetValue(arrow, out ArrowView? view) ? view : null;
    }

    private void RemoveArrow(Arrow arrow)
    {
        if (_arrowViews.TryGetValue(arrow, out ArrowView? view))
        {
            _arrowViews.Remove(arrow);
            Destroy(view.gameObject);
        }
        _board.RemoveArrow(arrow);
    }
}
