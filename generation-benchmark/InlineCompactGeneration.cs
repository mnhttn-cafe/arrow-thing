using System;
using System.Collections.Generic;

/// <summary>
/// Ranked generation with inline trivial chain compaction.
/// After each arrow placement, checks immediate forward and reverse deps
/// for adjacent collinear same-direction merge opportunities. Merged arrows
/// replace both originals, updating occupancy, rank, and spatial indices.
///
/// Benefits over post-processing compaction:
///   - Freed cells from reduced arrow count can be reused by later placements
///   - Merged arrows create different dependency structures mid-generation
///   - The visual loading can show arrows growing (merge) rather than disappearing
/// </summary>
static class InlineCompactGeneration
{
    private const int MinArrowLength = 2;

    public static List<Arrow> Generate(int width, int height, int maxLength, Random random)
    {
        var occupancy = new Arrow[width, height];
        var placed = new List<Arrow>();
        var rankMap = new Dictionary<Arrow, double>();

        var rightByRow = new List<Arrow>[height];
        var leftByRow = new List<Arrow>[height];
        for (int y = 0; y < height; y++)
        {
            rightByRow[y] = new List<Arrow>();
            leftByRow[y] = new List<Arrow>();
        }
        var upByCol = new List<Arrow>[width];
        var downByCol = new List<Arrow>[width];
        for (int x = 0; x < width; x++)
        {
            upByCol[x] = new List<Arrow>();
            downByCol[x] = new List<Arrow>();
        }

        var candidates = CreateCandidates(width, height);
        int maxPossible = width * height / 2;

        var visited = new bool[width, height];
        var path = new List<Cell>(64);
        var dirs = new int[] { 0, 1, 2, 3 };

        int created = 0;
        while (created < maxPossible && candidates.Count > 0)
        {
            int targetLength = random.Next(MinArrowLength, maxLength + 1);
            int idx = random.Next(candidates.Count);
            var cand = candidates[idx];

            if (occupancy[cand.head.X, cand.head.Y] != null ||
                occupancy[cand.next.X, cand.next.Y] != null)
            {
                SwapRemove(candidates, idx);
                continue;
            }

            // Forward deps: walk the ray, get max rank
            double minRank = 0.0;
            bool hasForwardDeps = false;
            {
                (int dx, int dy) = Arrow.GetDirectionStep(cand.direction);
                int cx = cand.head.X + dx, cy = cand.head.Y + dy;
                while (cx >= 0 && cx < width && cy >= 0 && cy < height)
                {
                    Arrow hit = occupancy[cx, cy];
                    if (hit != null)
                    {
                        double r = rankMap[hit];
                        if (r >= minRank) minRank = r;
                        hasForwardDeps = true;
                    }
                    cx += dx;
                    cy += dy;
                }
            }

            // Reverse deps for head and next
            double maxRank = double.MaxValue;
            GetMinReverseDepRank(cand.head.X, cand.head.Y, rightByRow, leftByRow, upByCol, downByCol, rankMap, ref maxRank);
            GetMinReverseDepRank(cand.next.X, cand.next.Y, rightByRow, leftByRow, upByCol, downByCol, rankMap, ref maxRank);

            if (maxRank <= minRank)
            {
                SwapRemove(candidates, idx);
                continue;
            }

            // Greedy walk
            var cells = GreedyWalk(width, height, targetLength, cand, random,
                occupancy, visited, path, dirs, hasForwardDeps, minRank,
                rightByRow, leftByRow, upByCol, downByCol, rankMap, ref maxRank);

            if (cells == null || cells.Count < MinArrowLength)
            {
                SwapRemove(candidates, idx);
                continue;
            }

            var arrow = new Arrow(cells);

            // Assign rank
            double range = maxRank - minRank;
            double assignedRank;
            if (range > 1e6)
                assignedRank = minRank + 1.0 + random.NextDouble();
            else
                assignedRank = minRank + range * (0.3 + 0.4 * random.NextDouble());

            // Place the arrow
            PlaceArrow(arrow, assignedRank, placed, rankMap, occupancy, rightByRow, leftByRow, upByCol, downByCol);
            created++;

            // Inline compaction: try to merge with immediate deps
            TryInlineCompact(arrow, placed, rankMap, occupancy, width, height,
                rightByRow, leftByRow, upByCol, downByCol);
        }

        return placed;
    }

    /// <summary>
    /// After placing newArrow, check its immediate forward and reverse deps
    /// for trivial merge opportunities. A merge is possible when:
    ///   1. Both arrows face the same direction
    ///   2. Both are on the same row (horizontal) or column (vertical)
    ///   3. The blocker's tail is adjacent to the dependent's head
    /// </summary>
    private static void TryInlineCompact(Arrow newArrow, List<Arrow> placed,
        Dictionary<Arrow, double> rankMap, Arrow[,] occupancy,
        int width, int height,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow,
        List<Arrow>[] upByCol, List<Arrow>[] downByCol)
    {
        // Check forward deps: arrows in newArrow's ray (newArrow depends on them)
        // If merge: blocker's cells + newArrow's cells, head = blocker's head
        {
            (int dx, int dy) = Arrow.GetDirectionStep(newArrow.HeadDirection);
            int cx = newArrow.HeadCell.X + dx, cy = newArrow.HeadCell.Y + dy;
            while (cx >= 0 && cx < width && cy >= 0 && cy < height)
            {
                Arrow blocker = occupancy[cx, cy];
                if (blocker != null && blocker != newArrow && CanMerge(blocker, newArrow))
                {
                    double blockerRank = rankMap[blocker];
                    var merged = DoMerge(blocker, newArrow);

                    RemoveArrow(newArrow, placed, rankMap, occupancy, rightByRow, leftByRow, upByCol, downByCol);
                    RemoveArrow(blocker, placed, rankMap, occupancy, rightByRow, leftByRow, upByCol, downByCol);
                    PlaceArrow(merged, blockerRank, placed, rankMap, occupancy, rightByRow, leftByRow, upByCol, downByCol);

                    // Continue checking with the merged arrow as the "new" arrow
                    newArrow = merged;
                    // Reset ray walk from merged arrow's head
                    (dx, dy) = Arrow.GetDirectionStep(newArrow.HeadDirection);
                    cx = newArrow.HeadCell.X + dx;
                    cy = newArrow.HeadCell.Y + dy;
                    continue;
                }
                cx += dx;
                cy += dy;
            }
        }

        // Check reverse deps: arrows whose rays pass through newArrow's cells
        // (they depend on newArrow). If merge: newArrow's cells + dependent's cells,
        // head = newArrow's head
        foreach (var cell in newArrow.Cells)
        {
            Arrow dependent = FindReverseMergeCandidate(cell, newArrow, occupancy, width, height,
                rightByRow, leftByRow, upByCol, downByCol);
            if (dependent != null)
            {
                double newRank = rankMap[newArrow];
                var merged = DoMerge(newArrow, dependent);

                RemoveArrow(dependent, placed, rankMap, occupancy, rightByRow, leftByRow, upByCol, downByCol);
                RemoveArrow(newArrow, placed, rankMap, occupancy, rightByRow, leftByRow, upByCol, downByCol);
                PlaceArrow(merged, newRank, placed, rankMap, occupancy, rightByRow, leftByRow, upByCol, downByCol);

                newArrow = merged;
                // After merging, newArrow has more cells — but we don't re-scan
                // all cells for additional reverse merges to keep it simple.
                // The next placement will catch further opportunities.
                break;
            }
        }
    }

    /// <summary>
    /// Check if blocker and dependent can be merged:
    /// same direction, collinear, blocker's tail adjacent to dependent's head.
    /// </summary>
    private static bool CanMerge(Arrow blocker, Arrow dependent)
    {
        if (blocker.HeadDirection != dependent.HeadDirection) return false;

        // Collinear check
        bool collinear = blocker.HeadDirection switch
        {
            Arrow.Direction.Right or Arrow.Direction.Left => blocker.HeadCell.Y == dependent.HeadCell.Y,
            Arrow.Direction.Up or Arrow.Direction.Down => blocker.HeadCell.X == dependent.HeadCell.X,
            _ => false,
        };
        if (!collinear) return false;

        // Adjacency: blocker's tail adjacent to dependent's head
        Cell blockerTail = blocker.Cells[blocker.Cells.Count - 1];
        Cell dependentHead = dependent.HeadCell;
        int adx = Math.Abs(blockerTail.X - dependentHead.X);
        int ady = Math.Abs(blockerTail.Y - dependentHead.Y);
        return (adx == 1 && ady == 0) || (adx == 0 && ady == 1);
    }

    /// <summary>
    /// Merge blocker + dependent into a single arrow.
    /// Head = blocker's head (it's further forward in the ray).
    /// Body = blocker's cells followed by dependent's cells.
    /// </summary>
    private static Arrow DoMerge(Arrow blocker, Arrow dependent)
    {
        var cells = new List<Cell>(blocker.Cells.Count + dependent.Cells.Count);
        cells.AddRange(blocker.Cells);
        cells.AddRange(dependent.Cells);
        return new Arrow(cells);
    }

    /// <summary>
    /// Find a reverse dep at the given cell that can merge with newArrow.
    /// The reverse dep is an arrow whose ray passes through this cell and
    /// is a valid merge candidate (same dir, collinear, adjacent).
    /// </summary>
    private static Arrow FindReverseMergeCandidate(Cell cell, Arrow newArrow,
        Arrow[,] occupancy, int width, int height,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow,
        List<Arrow>[] upByCol, List<Arrow>[] downByCol)
    {
        int cx = cell.X, cy = cell.Y;

        // Check each direction's spatial index
        foreach (var a in rightByRow[cy])
            if (a != newArrow && a.HeadCell.X < cx && CanMerge(newArrow, a)) return a;
        foreach (var a in leftByRow[cy])
            if (a != newArrow && a.HeadCell.X > cx && CanMerge(newArrow, a)) return a;
        foreach (var a in upByCol[cx])
            if (a != newArrow && a.HeadCell.Y < cy && CanMerge(newArrow, a)) return a;
        foreach (var a in downByCol[cx])
            if (a != newArrow && a.HeadCell.Y > cy && CanMerge(newArrow, a)) return a;

        return null;
    }

    private static void PlaceArrow(Arrow arrow, double rank,
        List<Arrow> placed, Dictionary<Arrow, double> rankMap, Arrow[,] occupancy,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow,
        List<Arrow>[] upByCol, List<Arrow>[] downByCol)
    {
        placed.Add(arrow);
        rankMap[arrow] = rank;
        foreach (var c in arrow.Cells)
            occupancy[c.X, c.Y] = arrow;
        switch (arrow.HeadDirection)
        {
            case Arrow.Direction.Right: rightByRow[arrow.HeadCell.Y].Add(arrow); break;
            case Arrow.Direction.Left: leftByRow[arrow.HeadCell.Y].Add(arrow); break;
            case Arrow.Direction.Up: upByCol[arrow.HeadCell.X].Add(arrow); break;
            case Arrow.Direction.Down: downByCol[arrow.HeadCell.X].Add(arrow); break;
        }
    }

    private static void RemoveArrow(Arrow arrow,
        List<Arrow> placed, Dictionary<Arrow, double> rankMap, Arrow[,] occupancy,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow,
        List<Arrow>[] upByCol, List<Arrow>[] downByCol)
    {
        placed.Remove(arrow);
        rankMap.Remove(arrow);
        foreach (var c in arrow.Cells)
            occupancy[c.X, c.Y] = null;
        switch (arrow.HeadDirection)
        {
            case Arrow.Direction.Right: rightByRow[arrow.HeadCell.Y].Remove(arrow); break;
            case Arrow.Direction.Left: leftByRow[arrow.HeadCell.Y].Remove(arrow); break;
            case Arrow.Direction.Up: upByCol[arrow.HeadCell.X].Remove(arrow); break;
            case Arrow.Direction.Down: downByCol[arrow.HeadCell.X].Remove(arrow); break;
        }
    }

    private static void GetMinReverseDepRank(int cx, int cy,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow,
        List<Arrow>[] upByCol, List<Arrow>[] downByCol,
        Dictionary<Arrow, double> rankMap, ref double maxRank)
    {
        foreach (var a in rightByRow[cy])
            if (a.HeadCell.X < cx) { double r = rankMap[a]; if (r < maxRank) maxRank = r; }
        foreach (var a in leftByRow[cy])
            if (a.HeadCell.X > cx) { double r = rankMap[a]; if (r < maxRank) maxRank = r; }
        foreach (var a in upByCol[cx])
            if (a.HeadCell.Y < cy) { double r = rankMap[a]; if (r < maxRank) maxRank = r; }
        foreach (var a in downByCol[cx])
            if (a.HeadCell.Y > cy) { double r = rankMap[a]; if (r < maxRank) maxRank = r; }
    }

    private static List<Cell> GreedyWalk(
        int w, int h, int targetLength, ArrowHeadData headData, Random random,
        Arrow[,] occupancy, bool[,] visited, List<Cell> path, int[] dirs,
        bool hasForwardDeps, double minRank,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow,
        List<Arrow>[] upByCol, List<Arrow>[] downByCol,
        Dictionary<Arrow, double> rankMap, ref double maxRank)
    {
        path.Clear();
        path.Add(headData.head);
        path.Add(headData.next);
        visited[headData.head.X, headData.head.Y] = true;
        visited[headData.next.X, headData.next.Y] = true;

        (int rdx, int rdy) = Arrow.GetDirectionStep(headData.direction);
        int rx = headData.head.X + rdx, ry = headData.head.Y + rdy;
        while (rx >= 0 && rx < w && ry >= 0 && ry < h) { visited[rx, ry] = true; rx += rdx; ry += rdy; }

        Cell current = headData.next;
        while (path.Count < targetLength)
        {
            dirs[0] = 0; dirs[1] = 1; dirs[2] = 2; dirs[3] = 3;
            for (int i = 3; i > 0; i--) { int j = random.Next(i + 1); int tmp = dirs[i]; dirs[i] = dirs[j]; dirs[j] = tmp; }

            bool found = false;
            for (int i = 0; i < 4; i++)
            {
                int nx, ny;
                switch (dirs[i])
                {
                    case 0: nx = current.X + 1; ny = current.Y; break;
                    case 1: nx = current.X - 1; ny = current.Y; break;
                    case 2: nx = current.X; ny = current.Y + 1; break;
                    default: nx = current.X; ny = current.Y - 1; break;
                }
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (visited[nx, ny]) continue;
                if (occupancy[nx, ny] != null) continue;

                double cellMaxRank = maxRank;
                GetMinReverseDepRank(nx, ny, rightByRow, leftByRow, upByCol, downByCol, rankMap, ref cellMaxRank);
                if (cellMaxRank <= minRank) continue;

                maxRank = cellMaxRank;
                path.Add(new Cell(nx, ny));
                visited[nx, ny] = true;
                current = new Cell(nx, ny);
                found = true;
                break;
            }
            if (!found) break;
        }

        foreach (var c in path) visited[c.X, c.Y] = false;
        rx = headData.head.X + rdx; ry = headData.head.Y + rdy;
        while (rx >= 0 && rx < w && ry >= 0 && ry < h) { visited[rx, ry] = false; rx += rdx; ry += rdy; }

        return path.Count >= MinArrowLength ? new List<Cell>(path) : null;
    }

    private static List<ArrowHeadData> CreateCandidates(int width, int height)
    {
        var list = new List<ArrowHeadData>();
        for (int x = 0; x < width - 1; x++)
        for (int y = 0; y < height; y++)
            list.Add(new ArrowHeadData { head = new(x + 1, y), next = new(x, y), direction = Arrow.Direction.Right });
        for (int x = 0; x < width - 1; x++)
        for (int y = 0; y < height; y++)
            list.Add(new ArrowHeadData { head = new(x, y), next = new(x + 1, y), direction = Arrow.Direction.Left });
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height - 1; y++)
            list.Add(new ArrowHeadData { head = new(x, y + 1), next = new(x, y), direction = Arrow.Direction.Up });
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height - 1; y++)
            list.Add(new ArrowHeadData { head = new(x, y), next = new(x, y + 1), direction = Arrow.Direction.Down });
        return list;
    }

    private static void SwapRemove<T>(List<T> list, int index)
    {
        int last = list.Count - 1;
        if (index != last) list[index] = list[last];
        list.RemoveAt(last);
    }
}
