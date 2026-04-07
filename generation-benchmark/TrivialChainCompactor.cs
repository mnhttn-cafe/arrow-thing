using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Post-processing pass that merges trivial collinear same-direction dependency
/// chains into single longer arrows. Two arrows form a "trivial pair" when:
///   1. Both face the same direction
///   2. Both are on the same row (horizontal) or column (vertical)
///   3. One depends on the other (its ray hits the other's cells)
///   4. The tail of the blocking arrow is adjacent to the head of the dependent
///
/// Merging replaces N arrows with 1, reducing arrow count and trivial chain %
/// without affecting difficulty. The merged arrow's head is the blocking arrow's
/// head; its body is the concatenation of both cell paths.
///
/// Solvability is preserved: the merged arrow's deps are a subset of the original
/// arrows' combined deps (the internal dependency is eliminated).
/// </summary>
static class TrivialChainCompactor
{
    /// <summary>
    /// Compact a board by merging trivial collinear same-direction chains.
    /// Returns a new Board with merged arrows.
    /// </summary>
    public static Board Compact(Board original)
    {
        var arrows = new List<Arrow>(original.Arrows);
        bool changed = true;

        while (changed)
        {
            changed = false;
            var merged = TryMergePass(arrows, original.Width, original.Height);
            if (merged != null)
            {
                arrows = merged;
                changed = true;
            }
        }

        var result = new Board(original.Width, original.Height);
        foreach (var a in arrows)
            result.AddArrow(new Arrow(a.Cells));
        return result;
    }

    /// <summary>
    /// Single pass: find and execute one round of merges.
    /// Returns null if no merges were possible.
    /// </summary>
    private static List<Arrow> TryMergePass(List<Arrow> arrows, int width, int height)
    {
        // Build occupancy for dependency checks
        var occupancy = new Arrow[width, height];
        foreach (var a in arrows)
            foreach (var c in a.Cells)
                occupancy[c.X, c.Y] = a;

        // Build forward dependency map: arrow -> list of arrows it depends on
        var deps = new Dictionary<Arrow, List<Arrow>>();
        foreach (var a in arrows)
        {
            var depList = new List<Arrow>();
            (int dx, int dy) = Arrow.GetDirectionStep(a.HeadDirection);
            int cx = a.HeadCell.X + dx, cy = a.HeadCell.Y + dy;
            while (cx >= 0 && cx < width && cy >= 0 && cy < height)
            {
                Arrow hit = occupancy[cx, cy];
                if (hit != null && hit != a && !depList.Contains(hit))
                    depList.Add(hit);
                cx += dx;
                cy += dy;
            }
            deps[a] = depList;
        }

        // Find mergeable trivial pairs
        var mergedSet = new HashSet<Arrow>();
        var result = new List<Arrow>();

        foreach (var dependent in arrows)
        {
            if (mergedSet.Contains(dependent)) continue;

            Arrow bestBlocker = null;

            foreach (var blocker in deps[dependent])
            {
                if (mergedSet.Contains(blocker)) continue;

                // Check trivial: same direction + collinear
                if (dependent.HeadDirection != blocker.HeadDirection) continue;
                if (!IsCollinear(dependent, blocker)) continue;

                // Check adjacency: dependent's head must be adjacent to blocker's last cell
                Cell blockerTail = blocker.Cells[blocker.Cells.Count - 1];
                Cell dependentHead = dependent.HeadCell;

                if (AreAdjacent(blockerTail, dependentHead))
                {
                    bestBlocker = blocker;
                    break;
                }
            }

            if (bestBlocker != null && !mergedSet.Contains(bestBlocker))
            {
                var mergedCells = new List<Cell>(bestBlocker.Cells.Count + dependent.Cells.Count);
                mergedCells.AddRange(bestBlocker.Cells);
                mergedCells.AddRange(dependent.Cells);

                var merged = new Arrow(mergedCells);
                result.Add(merged);
                mergedSet.Add(dependent);
                mergedSet.Add(bestBlocker);
            }
        }

        if (mergedSet.Count == 0) return null;

        // Add un-merged arrows
        foreach (var a in arrows)
            if (!mergedSet.Contains(a))
                result.Add(a);

        return result;
    }

    private static bool IsCollinear(Arrow a, Arrow b)
    {
        return a.HeadDirection switch
        {
            Arrow.Direction.Right or Arrow.Direction.Left => a.HeadCell.Y == b.HeadCell.Y,
            Arrow.Direction.Up or Arrow.Direction.Down => a.HeadCell.X == b.HeadCell.X,
            _ => false,
        };
    }

    private static bool AreAdjacent(Cell a, Cell b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
    }
}
