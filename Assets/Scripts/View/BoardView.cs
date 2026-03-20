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

    private Transform _arrowParent;

    /// <summary>
    /// Sets up grid and arrow parent. If <paramref name="spawnArrows"/> is false,
    /// arrows must be added incrementally via <see cref="AddArrowView"/> followed
    /// by <see cref="ApplyColoring"/> when complete.
    /// </summary>
    public void Init(Board board, VisualSettings settings, bool spawnArrows = true)
    {
        _board = board;
        _settings = settings;

        GridRenderer = gameObject.AddComponent<BoardGridRenderer>();
        GridRenderer.Init(board, settings);

        _arrowParent = new GameObject("Arrows").transform;
        _arrowParent.SetParent(transform, false);

        if (spawnArrows)
        {
            foreach (Arrow arrow in board.Arrows)
                AddArrowView(arrow);

            ApplyColoring();
        }
    }

    /// <summary>
    /// Spawns an <see cref="ArrowView"/> for a single arrow. Used for incremental
    /// board rendering during generation. <see cref="Init"/> must be called first.
    /// </summary>
    public void AddArrowView(Arrow arrow)
    {
        var go = new GameObject($"Arrow_{arrow.HeadCell.X}_{arrow.HeadCell.Y}");
        go.transform.SetParent(_arrowParent, false);

        var view = go.AddComponent<ArrowView>();
        view.Init(arrow, _board.Width, _board.Height, _settings);
        _arrowViews[arrow] = view;
    }

    /// <summary>
    /// Applies map-coloring palette to all current arrow views. Call after all
    /// arrows have been added (coloring requires the full adjacency graph).
    /// </summary>
    public void ApplyColoring()
    {
        if (
            PlayerPrefs.GetInt(GameSettings.ArrowColoringPrefKey, 0) == 1
            && _settings.arrowPalette.Count > 0
        )
        {
            int[] colors = ArrowColoring.AssignColors(_board, _settings.arrowPalette.Count);
            for (int i = 0; i < _board.Arrows.Count; i++)
            {
                Color c = _settings.arrowPalette[colors[i]];
                _arrowViews[_board.Arrows[i]].SetBaseColor(c, c);
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
    /// Removes an arrow's view without animation. Used during resume clear replay.
    /// </summary>
    public void RemoveArrowView(Arrow arrow)
    {
        if (_arrowViews.TryGetValue(arrow, out ArrowView view))
        {
            _arrowViews.Remove(arrow);
            Destroy(view.gameObject);
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
