using System;
using System.Collections;
using System.Collections.Generic;

public static class BoardGeneration
{
    /// <summary>
    /// Default cap on DFS dead ends per arrow candidate. Beyond this the DFS returns
    /// the best path found so far rather than exhausting the search tree.
    /// </summary>
    private const int DefaultDeadEndLimit = 10;
    private const int MinArrowLength = 2;

    /// <summary>
    /// Pooled resources reused across candidates to eliminate per-candidate allocations.
    /// </summary>
    private sealed class GenerationContext
    {
        public readonly int bitsetWords;
        public readonly ulong[] reachable;
        public readonly ulong[] forwardDeps;
        public readonly Queue<int> bfsQueue;
        public readonly bool[,] visited;
        public readonly List<Cell> path;
        public readonly List<Cell> best;

        public GenerationContext(int maxArrows, int boardWidth, int boardHeight)
        {
            bitsetWords = (maxArrows + 63) >> 6;
            reachable = new ulong[bitsetWords];
            forwardDeps = new ulong[bitsetWords];
            bfsQueue = new Queue<int>(Math.Max(maxArrows, 16));
            visited = new bool[boardWidth, boardHeight];
            path = new List<Cell>(32);
            best = new List<Cell>(32);
        }

        public void ClearBitset(ulong[] bits)
        {
            Array.Clear(bits, 0, bitsetWords);
        }
    }

    /// <summary>
    /// Incremental version of <see cref="FillBoard"/>. Places as many arrows as possible,
    /// yielding after each arrow to let the caller (e.g. a Unity coroutine) process the next frame.
    /// </summary>
    public static IEnumerator FillBoardIncremental(
        Board board,
        int maxLength,
        Random random,
        int deadEndLimit = DefaultDeadEndLimit
    )
    {
        board.InitializeForGeneration();
        int maxPossibleArrows = board.Width * board.Height / 2;
        var ctx = new GenerationContext(maxPossibleArrows, board.Width, board.Height);
        int created = 0;

        while (
            created < maxPossibleArrows
            && board._availableArrowHeads != null
            && board._availableArrowHeads.Count > 0
            && TryGenerateArrow(board, maxLength, random, out Arrow arrow, deadEndLimit, ctx)
        )
        {
            board.AddArrow(arrow!);
            created++;
            yield return null;
        }
    }

    public static bool GenerateArrows(
        Board board,
        int maxLength,
        int amount,
        Random random,
        out int createdArrows,
        int deadEndLimit = DefaultDeadEndLimit
    )
    {
        createdArrows = 0;
        if (board._availableArrowHeads == null)
            board.InitializeForGeneration();

        int maxPossibleArrows = board.Width * board.Height / 2;
        var ctx = new GenerationContext(maxPossibleArrows, board.Width, board.Height);

        while (
            createdArrows < amount
            && TryGenerateArrow(board, maxLength, random, out Arrow arrow, deadEndLimit, ctx)
        )
        {
            board.AddArrow(arrow!);
            createdArrows++;
        }
        return createdArrows == amount;
    }

    private static bool TryGenerateArrow(
        Board board,
        int maxLength,
        Random random,
        out Arrow arrow,
        int deadEndLimit,
        GenerationContext ctx
    )
    {
        arrow = null;
        int targetLength = random.Next(MinArrowLength, maxLength + 1);
        var candidates = board._availableArrowHeads!;

        while (candidates.Count > 0)
        {
            int headIndex = random.Next(candidates.Count);
            ArrowHeadData candidate = candidates[headIndex];

            if (
                board.GetArrowAt(candidate.head) != null
                || board.GetArrowAt(candidate.next) != null
            )
            {
                // Swap-and-pop: O(1) removal instead of O(N) shift
                SwapRemove(candidates, headIndex);
                continue;
            }

            // Compute forward deps as bitset
            ctx.ClearBitset(ctx.forwardDeps);
            ctx.ClearBitset(ctx.reachable);
            int depCount = ComputeForwardDeps(
                board,
                candidate.head,
                candidate.direction,
                ctx.forwardDeps
            );

            if (depCount > 0)
            {
                // BFS reachability through dependency graph
                ComputeReachableSet(board, ctx.forwardDeps, ctx.reachable, ctx);

                if (
                    WouldCellCauseCycle(board, candidate.head, ctx.reachable)
                    || WouldCellCauseCycle(board, candidate.next, ctx.reachable)
                )
                {
                    SwapRemove(candidates, headIndex);
                    continue;
                }
            }

            List<Cell> tail = CompleteArrowTail(
                board,
                targetLength,
                candidate,
                random,
                deadEndLimit,
                ctx.reachable,
                ctx
            );
            if (tail.Count < MinArrowLength)
            {
                SwapRemove(candidates, headIndex);
                continue;
            }

            arrow = new(tail);
            return true;
        }

        return false;
    }

    private static List<Cell> CompleteArrowTail(
        Board board,
        int targetLength,
        ArrowHeadData headData,
        Random random,
        int deadEndLimit,
        ulong[] reachable,
        GenerationContext ctx
    )
    {
        var path = ctx.path;
        var best = ctx.best;
        var visited = ctx.visited;

        path.Clear();
        path.Add(headData.head);
        path.Add(headData.next);

        best.Clear();
        best.Add(headData.head);
        best.Add(headData.next);

        visited[headData.head.X, headData.head.Y] = true;
        visited[headData.next.X, headData.next.Y] = true;

        int deadEnds = 0;

        void Dfs(Cell current)
        {
            if (deadEnds >= deadEndLimit)
                return;
            if (path.Count == targetLength)
            {
                best.Clear();
                best.AddRange(path);
                return;
            }

            bool anyValid = false;
            Cell[] neighbors = GetNeighbors(current);
            Shuffle(neighbors, random);
            foreach (Cell neighbor in neighbors)
            {
                if (
                    neighbor.X < 0
                    || neighbor.X >= board.Width
                    || neighbor.Y < 0
                    || neighbor.Y >= board.Height
                )
                    continue;
                if (visited[neighbor.X, neighbor.Y])
                    continue;
                if (Board.IsInRay(neighbor, headData.head, headData.direction))
                    continue;
                if (board.GetArrowAt(neighbor) != null)
                    continue;
                if (WouldCellCauseCycle(board, neighbor, reachable))
                    continue;

                path.Add(neighbor);
                visited[neighbor.X, neighbor.Y] = true;
                if (path.Count > best.Count)
                {
                    best.Clear();
                    best.AddRange(path);
                }
                anyValid = true;
                Dfs(neighbor);
                if (best.Count == targetLength || deadEnds >= deadEndLimit)
                    return;

                visited[neighbor.X, neighbor.Y] = false;
                path.RemoveAt(path.Count - 1);
            }

            if (!anyValid)
                deadEnds++;
        }

        Dfs(headData.next);

        // Clean up visited grid (handles both normal backtrack and early exit)
        foreach (Cell c in path)
            visited[c.X, c.Y] = false;

        // Return a copy since best is pooled and will be reused
        return new List<Cell>(best);
    }

    /// <summary>Collects all distinct arrows in the forward ray, storing them in the bitset. Returns the count.</summary>
    private static int ComputeForwardDeps(
        Board board,
        Cell head,
        Arrow.Direction direction,
        ulong[] depsBitset
    )
    {
        int count = 0;
        (int dx, int dy) = Arrow.GetDirectionStep(direction);
        Cell cursor = new(head.X + dx, head.Y + dy);
        while (board.Contains(cursor))
        {
            Arrow hit = board.GetArrowAt(cursor);
            if (hit != null)
            {
                int idx = hit._generationIndex;
                if (idx >= 0)
                {
                    int word = idx >> 6;
                    ulong bit = 1UL << (idx & 63);
                    if ((depsBitset[word] & bit) == 0)
                    {
                        depsBitset[word] |= bit;
                        count++;
                    }
                }
            }
            cursor = new(cursor.X + dx, cursor.Y + dy);
        }
        return count;
    }

    /// <summary>BFS from <paramref name="startBits"/> through committed dependsOn edges using bitsets.</summary>
    private static void ComputeReachableSet(
        Board board,
        ulong[] startBits,
        ulong[] reachable,
        GenerationContext ctx
    )
    {
        int words = ctx.bitsetWords;
        var queue = ctx.bfsQueue;
        queue.Clear();

        // Initialize reachable with start set and enqueue all set bits
        for (int w = 0; w < words; w++)
        {
            reachable[w] = startBits[w];
            ulong bits = startBits[w];
            while (bits != 0)
            {
                int bit = Ctz64(bits);
                queue.Enqueue((w << 6) | bit);
                bits &= bits - 1;
            }
        }

        // BFS: for each reachable arrow, OR in its deps and enqueue newly discovered arrows
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int offset = idx * words;
            ulong[] depsBits = board._depsBitsFlat;
            for (int w = 0; w < words; w++)
            {
                ulong newBits = depsBits[offset + w] & ~reachable[w];
                if (newBits != 0)
                {
                    reachable[w] |= newBits;
                    ulong scan = newBits;
                    while (scan != 0)
                    {
                        int bit = Ctz64(scan);
                        queue.Enqueue((w << 6) | bit);
                        scan &= scan - 1;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns true if placing a candidate cell at <paramref name="cell"/> would create a cycle.
    /// Uses bitset-based reachable set for O(1) per-arrow membership tests.
    /// </summary>
    private static bool WouldCellCauseCycle(Board board, Cell cell, ulong[] reachable)
    {
        return board.AnyArrowWithRayThroughBitset(cell, reachable);
    }

    private static Cell[] GetNeighbors(Cell cell)
    {
        return new Cell[]
        {
            new(cell.X + 1, cell.Y),
            new(cell.X - 1, cell.Y),
            new(cell.X, cell.Y + 1),
            new(cell.X, cell.Y - 1),
        };
    }

    private static void Shuffle(Cell[] array, Random random)
    {
        int n = array.Length;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            (array[n], array[k]) = (array[k], array[n]);
        }
    }

    /// <summary>Swap-and-pop removal: O(1) instead of O(N) list shift.</summary>
    private static void SwapRemove(List<ArrowHeadData> list, int index)
    {
        int last = list.Count - 1;
        if (index != last)
            list[index] = list[last];
        list.RemoveAt(last);
    }

    /// <summary>Count trailing zeros in a 64-bit value (position of lowest set bit).</summary>
    private static int Ctz64(ulong value)
    {
        // De Bruijn method for 64-bit
        if (value == 0)
            return 64;
        ulong isolated = value & (ulong)(-(long)value); // isolate lowest set bit
        return DeBruijn64Tab[(isolated * 0x03F79D71B4CA8B09UL) >> 58];
    }

    private static readonly int[] DeBruijn64Tab =
    {
        0,
        1,
        56,
        2,
        57,
        49,
        28,
        3,
        61,
        58,
        42,
        50,
        38,
        29,
        17,
        4,
        62,
        47,
        59,
        36,
        45,
        43,
        51,
        22,
        53,
        39,
        33,
        30,
        24,
        18,
        12,
        5,
        63,
        55,
        48,
        27,
        60,
        41,
        37,
        16,
        46,
        35,
        44,
        21,
        52,
        32,
        23,
        11,
        54,
        26,
        40,
        15,
        34,
        20,
        31,
        10,
        25,
        14,
        19,
        9,
        13,
        8,
        7,
        6,
    };
}

sealed class ArrowHeadData
{
    public Cell head;
    public Cell next;
    public Arrow.Direction direction;
}
