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

    /// <summary>
    /// Fired after the last arrow's pull-out animation finishes (board fully cleared).
    /// </summary>
    public event System.Action BoardCleared;

    public BoardGridRenderer GridRenderer { get; private set; }

    public void Init(Board board, VisualSettings settings)
    {
        _board = board;
        _settings = settings;

        // Grid
        GridRenderer = gameObject.AddComponent<BoardGridRenderer>();
        GridRenderer.Init(board, settings);

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
        if (!_arrowViews.TryGetValue(arrow, out ArrowView view))
            return false;

        if (!_board.IsClearable(arrow))
        {
            // Blocked: slide toward blocker, bump, flash, return
            Arrow blocker = _board.GetFirstInRay(arrow);
            if (blocker != null)
            {
                // Contact distance: cells between arrow head and blocker's first ray cell
                (int dx, int dy) = Arrow.GetDirectionStep(arrow.HeadDirection);
                Cell cursor = new(arrow.HeadCell.X + dx, arrow.HeadCell.Y + dy);
                int cellDistance = 1;
                while (_board.Contains(cursor))
                {
                    if (_board.GetArrowAt(cursor) == blocker)
                        break;
                    cursor = new(cursor.X + dx, cursor.Y + dy);
                    cellDistance++;
                }
                // Each cell is 1 world unit; contact at midpoint of the hit cell
                float contactArcLength = cellDistance - 0.5f;
                view.PlayBump(contactArcLength);
            }
            else
            {
                view.PlayRejectFlash();
            }
            return false;
        }

        // Clearable: remove from domain immediately, play pull-out animation
        _arrowViews.Remove(arrow);
        _board.RemoveArrow(arrow);
        bool wasLast = _board.Arrows.Count == 0;
        view.PlayPullOut(onComplete: () =>
        {
            Destroy(view.gameObject);
            if (wasLast)
                BoardCleared?.Invoke();
        });
        return true;
    }

    /// <summary>
    /// Returns the ArrowView for a given arrow, or null if not found.
    /// </summary>
    public ArrowView GetArrowView(Arrow arrow)
    {
        return _arrowViews.TryGetValue(arrow, out ArrowView view) ? view : null;
    }
}
