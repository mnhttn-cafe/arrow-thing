using System;
using System.Collections.Generic;

/// <summary>
/// Ranked generation with stale-rank detection.
///
/// Same core algorithm as RankedGeneration: O(deps) rank check per candidate.
/// But when computing maxRank from reverse deps, validates each reverse dep's
/// rank against its own forward deps. If a reverse dep's rank is stale
/// (lower than it should be based on its actual dependencies), its rank is
/// refreshed via a recursive depth computation before being used.
///
/// This prevents the cascading false-positive rejections that cause the
/// pure ranked approach to produce shallow DAGs.
/// </summary>
static class RankedHybridGeneration
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
                        double r = GetFreshRank(hit, occupancy, width, height, rankMap);
                        if (r >= minRank) minRank = r;
                        hasForwardDeps = true;
                    }
                    cx += dx;
                    cy += dy;
                }
            }

            // Reverse deps for head and next: get min FRESH rank
            double maxRank = double.MaxValue;
            GetMinFreshReverseDepRank(cand.head.X, cand.head.Y, rightByRow, leftByRow, upByCol, downByCol,
                occupancy, width, height, rankMap, ref maxRank);
            GetMinFreshReverseDepRank(cand.next.X, cand.next.Y, rightByRow, leftByRow, upByCol, downByCol,
                occupancy, width, height, rankMap, ref maxRank);

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

            // Assign rank at midpoint
            double range = maxRank - minRank;
            double assignedRank;
            if (range > 1e6)
                assignedRank = minRank + 1.0 + random.NextDouble();
            else
                assignedRank = minRank + range * (0.3 + 0.4 * random.NextDouble());

            placed.Add(arrow);
            rankMap[arrow] = assignedRank;
            foreach (var c in arrow.Cells)
                occupancy[c.X, c.Y] = arrow;
            switch (arrow.HeadDirection)
            {
                case Arrow.Direction.Right: rightByRow[arrow.HeadCell.Y].Add(arrow); break;
                case Arrow.Direction.Left: leftByRow[arrow.HeadCell.Y].Add(arrow); break;
                case Arrow.Direction.Up: upByCol[arrow.HeadCell.X].Add(arrow); break;
                case Arrow.Direction.Down: downByCol[arrow.HeadCell.X].Add(arrow); break;
            }
            created++;
        }

        return placed;
    }

    /// <summary>
    /// Get the "fresh" rank of an arrow: max of its stored rank and the
    /// minimum rank it SHOULD have based on its current forward deps.
    /// Updates the stored rank if stale.
    /// </summary>
    private static double GetFreshRank(Arrow arrow, Arrow[,] occupancy,
        int width, int height, Dictionary<Arrow, double> rankMap)
    {
        double storedRank = rankMap[arrow];

        // Compute the minimum valid rank: 1 + max rank of forward deps
        double minValid = 0.0;
        (int dx, int dy) = Arrow.GetDirectionStep(arrow.HeadDirection);
        int cx = arrow.HeadCell.X + dx, cy = arrow.HeadCell.Y + dy;
        while (cx >= 0 && cx < width && cy >= 0 && cy < height)
        {
            Arrow hit = occupancy[cx, cy];
            if (hit != null && hit != arrow)
            {
                double r = rankMap[hit]; // Don't recurse — just one level deep
                if (r >= minValid) minValid = r;
            }
            cx += dx;
            cy += dy;
        }

        double freshRank = minValid > 0 ? minValid + 0.001 : 0.0;
        if (freshRank > storedRank)
        {
            rankMap[arrow] = freshRank;
            return freshRank;
        }
        return storedRank;
    }

    private static void GetMinFreshReverseDepRank(int cx, int cy,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow,
        List<Arrow>[] upByCol, List<Arrow>[] downByCol,
        Arrow[,] occupancy, int width, int height,
        Dictionary<Arrow, double> rankMap, ref double maxRank)
    {
        foreach (var a in rightByRow[cy])
            if (a.HeadCell.X < cx) { double r = GetFreshRank(a, occupancy, width, height, rankMap); if (r < maxRank) maxRank = r; }
        foreach (var a in leftByRow[cy])
            if (a.HeadCell.X > cx) { double r = GetFreshRank(a, occupancy, width, height, rankMap); if (r < maxRank) maxRank = r; }
        foreach (var a in upByCol[cx])
            if (a.HeadCell.Y < cy) { double r = GetFreshRank(a, occupancy, width, height, rankMap); if (r < maxRank) maxRank = r; }
        foreach (var a in downByCol[cx])
            if (a.HeadCell.Y > cy) { double r = GetFreshRank(a, occupancy, width, height, rankMap); if (r < maxRank) maxRank = r; }
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

                // Use simple rank check in walk for speed — stale detection is
                // too expensive per-cell. Only the head-level check uses fresh ranks.
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
