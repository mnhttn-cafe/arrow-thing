using System;
using System.Collections.Generic;

/// <summary>
/// Random placement + SCC repair. Places arrows without any cycle checking
/// (occupancy only), then iteratively finds strongly connected components
/// and removes arrows to break cycles.
///
/// Variant: biased placement prefers arrow directions toward nearby edges
/// to statistically reduce cycle probability.
/// </summary>
static class RepairGeneration
{
    private const int MinArrowLength = 2;

    public static List<Arrow> Generate(int width, int height, int maxLength, Random random, bool biased)
    {
        var occupancy = new Arrow[width, height];
        var placed = new List<Arrow>();

        // Spatial ray index (needed for greedy walk's own-ray avoidance)
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

        // Build candidate pool
        var candidates = CreateCandidates(width, height);
        int maxPossible = width * height / 2;

        // Pooled walk state
        var visited = new bool[width, height];
        var path = new List<Cell>(64);
        var dirs = new int[] { 0, 1, 2, 3 };

        // Phase 1: Place arrows freely (no cycle detection)
        int created = 0;
        while (created < maxPossible && candidates.Count > 0)
        {
            int targetLength = random.Next(MinArrowLength, maxLength + 1);

            int idx;
            if (biased)
            {
                // Weighted selection: prefer candidates with short rays (near edges)
                idx = BiasedSelect(candidates, width, height, random);
            }
            else
            {
                idx = random.Next(candidates.Count);
            }

            var cand = candidates[idx];

            if (occupancy[cand.head.X, cand.head.Y] != null ||
                occupancy[cand.next.X, cand.next.Y] != null)
            {
                SwapRemove(candidates, idx);
                continue;
            }

            var cells = GreedyWalk(width, height, targetLength, cand, random, occupancy, visited, path, dirs);
            if (cells == null || cells.Count < MinArrowLength)
            {
                SwapRemove(candidates, idx);
                continue;
            }

            var arrow = new Arrow(cells);
            placed.Add(arrow);
            foreach (var c in arrow.Cells)
                occupancy[c.X, c.Y] = arrow;
            created++;
        }

        // Phase 2: Repair cycles via flip-then-remove strategy.
        // Flipping reverses cell order — same cells, different head/direction/ray.
        // Preserves density while potentially breaking cycles.
        int iterations = 0;
        while (iterations < 500)
        {
            iterations++;
            var depGraph = BuildDepGraph(placed, width, height);
            int n = placed.Count;

            // Kahn's to find cycle nodes
            var revGraph = new List<int>[n];
            for (int i = 0; i < n; i++) revGraph[i] = new List<int>();
            var depCount = new int[n];
            for (int i = 0; i < n; i++)
            {
                depCount[i] = depGraph[i].Count;
                foreach (int dep in depGraph[i])
                    revGraph[dep].Add(i);
            }
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
                if (depCount[i] == 0) queue.Enqueue(i);
            var processed = new bool[n];
            while (queue.Count > 0)
            {
                int nd = queue.Dequeue();
                processed[nd] = true;
                foreach (int dependent in revGraph[nd])
                    if (--depCount[dependent] == 0)
                        queue.Enqueue(dependent);
            }

            var cycleNodes = new List<int>();
            for (int i = 0; i < n; i++)
                if (!processed[i]) cycleNodes.Add(i);
            if (cycleNodes.Count == 0) break;

            // Score and sort cycle nodes
            var cycleSet = new HashSet<int>(cycleNodes);
            var scores = new List<(int node, int score)>();
            foreach (int node in cycleNodes)
            {
                int score = 0;
                foreach (int dep in depGraph[node])
                    if (cycleSet.Contains(dep)) score++;
                foreach (int dependent in revGraph[node])
                    if (cycleSet.Contains(dependent)) score++;
                scores.Add((node, score));
            }
            scores.Sort((a, b) => b.score.CompareTo(a.score));

            // Batch flip: flip a chunk of top-scoring cycle nodes at once,
            // then re-evaluate. Much faster than per-flip cycle detection.
            int batchSize = Math.Max(1, cycleNodes.Count / 5);
            int flipCount = 0;
            for (int i = 0; i < batchSize && i < scores.Count; i++)
            {
                int nodeIdx = scores[i].node;
                Arrow original = placed[nodeIdx];
                var reversed = new List<Cell>(original.Cells);
                reversed.Reverse();
                placed[nodeIdx] = new Arrow(reversed);
                flipCount++;
            }

            // Check if batch flip helped
            var newCycleNodes = FindCycleNodes(placed, width, height);
            if (newCycleNodes.Count < cycleNodes.Count)
            {
                // Progress — continue with flips
                continue;
            }

            // Flipping didn't help (or made it worse). Revert flips and remove instead.
            // Revert all flips
            for (int i = 0; i < flipCount && i < scores.Count; i++)
            {
                int nodeIdx = scores[i].node;
                Arrow flippedBack = placed[nodeIdx];
                var reReversed = new List<Cell>(flippedBack.Cells);
                reReversed.Reverse();
                placed[nodeIdx] = new Arrow(reReversed);
            }

            // Remove top 20% of cycle nodes
            int removeCount = Math.Max(1, cycleNodes.Count / 5);
            var toRemove = new List<int>();
            for (int i = 0; i < removeCount && i < scores.Count; i++)
                toRemove.Add(scores[i].node);
            toRemove.Sort();
            for (int i = toRemove.Count - 1; i >= 0; i--)
            {
                int ri = toRemove[i];
                var arrow = placed[ri];
                foreach (var c in arrow.Cells)
                    occupancy[c.X, c.Y] = null;
                placed.RemoveAt(ri);
            }
        }

        return placed;
    }

    /// <summary>
    /// Select a candidate biased toward short ray lengths (arrows facing nearby edges).
    /// Uses rejection sampling with ray-length-based weights.
    /// </summary>
    private static int BiasedSelect(List<ArrowHeadData> candidates, int w, int h, Random random)
    {
        // Try up to 8 candidates and pick the one with the shortest ray
        int bestIdx = random.Next(candidates.Count);
        int bestRayLen = RayLength(candidates[bestIdx], w, h);

        for (int attempt = 0; attempt < 7; attempt++)
        {
            int idx = random.Next(candidates.Count);
            int rayLen = RayLength(candidates[idx], w, h);
            if (rayLen < bestRayLen)
            {
                bestIdx = idx;
                bestRayLen = rayLen;
            }
        }
        return bestIdx;
    }

    private static int RayLength(ArrowHeadData cand, int w, int h)
    {
        return cand.direction switch
        {
            Arrow.Direction.Right => w - 1 - cand.head.X,
            Arrow.Direction.Left => cand.head.X,
            Arrow.Direction.Up => h - 1 - cand.head.Y,
            Arrow.Direction.Down => cand.head.Y,
            _ => 0,
        };
    }

    /// <summary>
    /// Find all arrow indices that participate in cycles using Kahn's algorithm.
    /// Any node not consumed by topological sort is in a cycle.
    /// </summary>
    private static List<int> FindCycleNodes(List<Arrow> placed, int width, int height)
    {
        var depGraph = BuildDepGraph(placed, width, height);
        int n = placed.Count;

        var revGraph = new List<int>[n];
        for (int i = 0; i < n; i++) revGraph[i] = new List<int>();
        var depCount = new int[n];
        for (int i = 0; i < n; i++)
        {
            depCount[i] = depGraph[i].Count;
            foreach (int dep in depGraph[i])
                revGraph[dep].Add(i);
        }

        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
            if (depCount[i] == 0) queue.Enqueue(i);

        var processed = new bool[n];
        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            processed[node] = true;
            foreach (int dependent in revGraph[node])
            {
                depCount[dependent]--;
                if (depCount[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }

        var result = new List<int>();
        for (int i = 0; i < n; i++)
            if (!processed[i]) result.Add(i);
        return result;
    }

    /// <summary>
    /// Build the dependency graph: for each arrow, list which other arrows (by index)
    /// it depends on (have cells in its forward ray).
    /// </summary>
    private static List<int>[] BuildDepGraph(List<Arrow> arrows, int width, int height)
    {
        // Build occupancy for current arrows
        var occupancy = new int[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            occupancy[x, y] = -1;

        for (int i = 0; i < arrows.Count; i++)
            foreach (var c in arrows[i].Cells)
                occupancy[c.X, c.Y] = i;

        var deps = new List<int>[arrows.Count];
        for (int i = 0; i < arrows.Count; i++)
        {
            deps[i] = new List<int>();
            var seen = new HashSet<int>();
            var arrow = arrows[i];
            (int dx, int dy) = Arrow.GetDirectionStep(arrow.HeadDirection);
            int cx = arrow.HeadCell.X + dx, cy = arrow.HeadCell.Y + dy;
            while (cx >= 0 && cx < width && cy >= 0 && cy < height)
            {
                int hit = occupancy[cx, cy];
                if (hit >= 0 && hit != i && seen.Add(hit))
                    deps[i].Add(hit);
                cx += dx;
                cy += dy;
            }
        }

        return deps;
    }

    private static List<Cell> GreedyWalk(
        int w, int h, int targetLength,
        ArrowHeadData headData, Random random,
        Arrow[,] occupancy,
        bool[,] visited, List<Cell> path, int[] dirs)
    {
        path.Clear();
        path.Add(headData.head);
        path.Add(headData.next);
        visited[headData.head.X, headData.head.Y] = true;
        visited[headData.next.X, headData.next.Y] = true;

        // Pre-mark ray cells
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
