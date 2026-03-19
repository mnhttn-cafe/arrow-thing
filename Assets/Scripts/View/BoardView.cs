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
    private int _clearedCount;

    private bool _trailVisible;
    private ArrowView _tintedSource;
    private ArrowView _tintedBlocker;

    /// <summary>
    /// Fired after the last arrow is cleared and recorded, but before its pull-out animation finishes.
    /// Must be invoked explicitly via <see cref="NotifyLastArrowClearing"/> after the caller
    /// has finished recording the clear event, to ensure correct event ordering.
    /// </summary>
    public event System.Action LastArrowClearing;

    /// <summary>
    /// Fired after the last arrow's pull-out animation finishes (board fully cleared).
    /// </summary>
    public event System.Action BoardCleared;

    /// <summary>
    /// Fired when trails are auto-disabled (e.g. after a successful clear).
    /// </summary>
    public event System.Action TrailAutoOff;

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

        // Apply map-coloring palette if enabled
        if (GameSettings.ArrowColoring && settings.arrowPalette.Count > 0)
        {
            int[] colors = ArrowColoring.AssignColors(board, settings.arrowPalette.Count);
            for (int i = 0; i < board.Arrows.Count; i++)
            {
                Color c = settings.arrowPalette[colors[i]];
                _arrowViews[board.Arrows[i]].SetBaseColor(c, c);
            }
        }
    }

    /// <summary>
    /// Attempts to clear an arrow. Returns a ClearResult indicating what happened.
    /// </summary>
    public ClearResult TryClearArrow(Arrow arrow)
    {
        if (!_arrowViews.TryGetValue(arrow, out ArrowView view))
            return ClearResult.Blocked;

        // Reset any previously tinted arrows before applying new feedback.
        ClearPreviousTints();

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

                // Persistent tint on source and blocker (clears on next selection)
                view.SetBlockedTint(_settings.blockedTintIntensity, _settings.rejectFlashColor);
                _tintedSource = view;
                if (_arrowViews.TryGetValue(blocker, out ArrowView blockerView))
                {
                    blockerView.SetBlockedTint(
                        _settings.blockedTintIntensity,
                        _settings.rejectFlashColor
                    );
                    _tintedBlocker = blockerView;
                }

                view.PlayBump(contactArcLength);
            }
            else
            {
                view.PlayRejectFlash();
            }
            return ClearResult.Blocked;
        }

        // Clearable: remove from domain immediately, play pull-out animation
        _arrowViews.Remove(arrow);
        _board.RemoveArrow(arrow);
        _clearedCount++;
        bool wasFirst = _clearedCount == 1;
        bool wasLast = _board.Arrows.Count == 0;

        // Auto-disable trails when an arrow is cleared to avoid stale lines
        if (_trailVisible)
        {
            SetAllTrailsVisible(false);
            TrailAutoOff?.Invoke();
        }

        view.PlayPullOut(onComplete: () =>
        {
            Destroy(view.gameObject);
            if (wasLast)
                BoardCleared?.Invoke();
        });

        if (wasLast)
            return ClearResult.ClearedLast;
        if (wasFirst)
            return ClearResult.ClearedFirst;
        return ClearResult.Cleared;
    }

    /// <summary>
    /// Call after recording the final clear event to fire <see cref="LastArrowClearing"/>.
    /// </summary>
    public void NotifyLastArrowClearing() => LastArrowClearing?.Invoke();

    private void ClearPreviousTints()
    {
        if (_tintedSource != null)
        {
            _tintedSource.ClearBlockedTint();
            _tintedSource = null;
        }
        if (_tintedBlocker != null)
        {
            _tintedBlocker.ClearBlockedTint();
            _tintedBlocker = null;
        }
    }

    /// <summary>
    /// Returns the ArrowView for a given arrow, or null if not found.
    /// </summary>
    public ArrowView GetArrowView(Arrow arrow)
    {
        return _arrowViews.TryGetValue(arrow, out ArrowView view) ? view : null;
    }

    /// <summary>
    /// Shows or hides trail lines on all remaining arrows.
    /// </summary>
    public void SetAllTrailsVisible(bool visible)
    {
        _trailVisible = visible;
        foreach (ArrowView view in _arrowViews.Values)
            view.SetTrailVisible(visible);
    }
}
