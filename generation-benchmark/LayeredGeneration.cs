using System;
using System.Collections.Generic;

/// <summary>
/// "No-reverse-deps" layered generation. Places arrows such that no existing
/// arrow's ray passes through the new arrow's cells. This guarantees the
/// dependency graph is a DAG by construction (all edges go from newer to older
/// arrows), eliminating cycle detection entirely.
///
/// Trade-off: rejects more candidates than the current algorithm (conservative),
/// which may reduce fill density, especially in the board interior.
/// </summary>
static class LayeredGeneration
{
    private const int MinArrowLength = 2;

    public static List<Arrow> Generate(int width, int height, int maxLength, Random random)
    {
        var occupancy = new Arrow[width, height];
        var placed = new List<Arrow>();

        // Spatial ray index for reverse-dep checks
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

        // Build candidate pool (same as current algorithm)
        var candidates = CreateCandidates(width, height);
        int maxPossible = width * height / 2;

        // Pooled state for greedy walk
        var visited = new bool[width, height];
        var path = new List<Cell>(64);
        var dirs = new int[] { 0, 1, 2, 3 };

        int created = 0;
        while (created < maxPossible && candidates.Count > 0)
        {
            int targetLength = random.Next(MinArrowLength, maxLength + 1);
            int idx = random.Next(candidates.Count);
            var cand = candidates[idx];

            // Occupancy check
            if (occupancy[cand.head.X, cand.head.Y] != null ||
                occupancy[cand.next.X, cand.next.Y] != null)
            {
                SwapRemove(candidates, idx);
                continue;
            }

            // Reverse-dep check: reject if any existing arrow's ray crosses head or next
            if (HasReverseDepAt(cand.head.X, cand.head.Y, rightByRow, leftByRow, upByCol, downByCol) ||
                HasReverseDepAt(cand.next.X, cand.next.Y, rightByRow, leftByRow, upByCol, downByCol))
            {
                SwapRemove(candidates, idx);
                continue;
            }

            // Greedy walk with reverse-dep check per cell
            var cells = GreedyWalk(
                width, height, targetLength, cand, random, occupancy,
                visited, path, dirs,
                rightByRow, leftByRow, upByCol, downByCol
            );

            if (cells == null || cells.Count < MinArrowLength)
            {
                SwapRemove(candidates, idx);
                continue;
            }

            var arrow = new Arrow(cells);
            placed.Add(arrow);
            foreach (var c in arrow.Cells)
                occupancy[c.X, c.Y] = arrow;
            AddToRayIndex(arrow, rightByRow, leftByRow, upByCol, downByCol);
            created++;
        }

        return placed;
    }

    private static bool HasReverseDepAt(
        int cx, int cy,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow,
        List<Arrow>[] upByCol, List<Arrow>[] downByCol)
    {
        foreach (var a in rightByRow[cy])
            if (a.HeadCell.X < cx) return true;
        foreach (var a in leftByRow[cy])
            if (a.HeadCell.X > cx) return true;
        foreach (var a in upByCol[cx])
            if (a.HeadCell.Y < cy) return true;
        foreach (var a in downByCol[cx])
            if (a.HeadCell.Y > cy) return true;
        return false;
    }

    private static List<Cell> GreedyWalk(
        int w, int h, int targetLength,
        ArrowHeadData headData, Random random,
        Arrow[,] occupancy,
        bool[,] visited, List<Cell> path, int[] dirs,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow,
        List<Arrow>[] upByCol, List<Arrow>[] downByCol)
    {
        path.Clear();
        path.Add(headData.head);
        path.Add(headData.next);
        visited[headData.head.X, headData.head.Y] = true;
        visited[headData.next.X, headData.next.Y] = true;

        // Pre-mark ray cells in visited
        (int rdx, int rdy) = Arrow.GetDirectionStep(headData.direction);
        int rx = headData.head.X + rdx, ry = headData.head.Y + rdy;
        while (rx >= 0 && rx < w && ry >= 0 && ry < h)
        {
            visited[rx, ry] = true;
            rx += rdx;
            ry += rdy;
        }

        Cell current = headData.next;

        while (path.Count < targetLength)
        {
            dirs[0] = 0; dirs[1] = 1; dirs[2] = 2; dirs[3] = 3;
            for (int i = 3; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = dirs[i]; dirs[i] = dirs[j]; dirs[j] = tmp;
            }

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
                // Reverse-dep check for this cell
                if (HasReverseDepAt(nx, ny, rightByRow, leftByRow, upByCol, downByCol)) continue;

                path.Add(new Cell(nx, ny));
                visited[nx, ny] = true;
                current = new Cell(nx, ny);
                found = true;
                break;
            }

            if (!found) break;
        }

        // Clean up visited
        foreach (var c in path) visited[c.X, c.Y] = false;
        rx = headData.head.X + rdx; ry = headData.head.Y + rdy;
        while (rx >= 0 && rx < w && ry >= 0 && ry < h)
        {
            visited[rx, ry] = false;
            rx += rdx;
            ry += rdy;
        }

        return path.Count >= MinArrowLength ? new List<Cell>(path) : null;
    }

    private static void AddToRayIndex(
        Arrow arrow,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow,
        List<Arrow>[] upByCol, List<Arrow>[] downByCol)
    {
        switch (arrow.HeadDirection)
        {
            case Arrow.Direction.Right: rightByRow[arrow.HeadCell.Y].Add(arrow); break;
            case Arrow.Direction.Left: leftByRow[arrow.HeadCell.Y].Add(arrow); break;
            case Arrow.Direction.Up: upByCol[arrow.HeadCell.X].Add(arrow); break;
            case Arrow.Direction.Down: downByCol[arrow.HeadCell.X].Add(arrow); break;
        }
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
