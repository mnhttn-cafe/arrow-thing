using System;
using System.Collections;
using System.Collections.Generic;

public static class BoardGeneration
{
    private const int MinArrowLength = 2;

    /// <summary>
    /// Pooled resources reused across candidates to eliminate per-candidate allocations.
    /// </summary>
    private sealed class GenerationContext
    {
        public int bitsetStride;
        public int activeWords;
        public ulong[] reachable;
        public ulong[] forwardDeps;
        public ulong[] frontier; // level-based BFS frontier
        public readonly bool[,] visited;
        public readonly List<Cell> path;
        public readonly int[] dirOrder;

        public GenerationContext(int initialStride, int boardWidth, int boardHeight)
        {
            bitsetStride = initialStride;
            activeWords = 0;
            reachable = new ulong[bitsetStride];
            forwardDeps = new ulong[bitsetStride];
            frontier = new ulong[bitsetStride];
            visited = new bool[boardWidth, boardHeight];
            path = new List<Cell>(64);
            dirOrder = new int[] { 0, 1, 2, 3 };
        }

        public void EnsureCapacity(int requiredWords)
        {
            if (requiredWords <= bitsetStride)
                return;
            bitsetStride = requiredWords;
            reachable = new ulong[bitsetStride];
            forwardDeps = new ulong[bitsetStride];
            frontier = new ulong[bitsetStride];
        }
    }

    /// <summary>
    /// Incremental version of board filling. Places as many arrows as possible,
    /// yielding after each arrow to let the caller (e.g. a Unity coroutine) process the next frame.
    /// </summary>
    /// <summary>
    /// Signals that generation has finished placing arrows and is now building
    /// the dependency graph. Yielded once before finalization begins.
    /// </summary>
    public static readonly object FinalizationMarker = new object();

    public static IEnumerator FillBoardIncremental(Board board, int maxLength, Random random)
    {
        board.InitializeForGeneration();
        int maxPossibleArrows = board.Width * board.Height / 2;
        var ctx = new GenerationContext(board._bitsetWords, board.Width, board.Height);
        int created = 0;

        while (
            created < maxPossibleArrows
            && board._availableArrowHeads != null
            && board._availableArrowHeads.Count > 0
            && TryGenerateArrow(board, maxLength, random, out Arrow arrow, ctx)
        )
        {
            board.AddArrowForGeneration(arrow!);
            ctx.EnsureCapacity(board._bitsetWords);
            ctx.activeWords = (board._nextGenIndex + 63) >> 6;
            created++;
            yield return null;
        }

        // Signal phase transition, then yield during finalization
        yield return FinalizationMarker;
        var finalizer = board.FinalizeGenerationIncremental();
        while (finalizer.MoveNext())
            yield return finalizer.Current;
    }

    public static bool GenerateArrows(
        Board board,
        int maxLength,
        int amount,
        Random random,
        out int createdArrows
    )
    {
        createdArrows = 0;
        if (board._availableArrowHeads == null)
            board.InitializeForGeneration();

        var ctx = new GenerationContext(board._bitsetWords, board.Width, board.Height);
        ctx.activeWords = (board._nextGenIndex + 63) >> 6;

        while (
            createdArrows < amount
            && TryGenerateArrow(board, maxLength, random, out Arrow arrow, ctx)
        )
        {
            board.AddArrow(arrow!);
            ctx.activeWords = (board._nextGenIndex + 63) >> 6;
            createdArrows++;
        }
        return createdArrows == amount;
    }

    private static bool TryGenerateArrow(
        Board board,
        int maxLength,
        Random random,
        out Arrow arrow,
        GenerationContext ctx
    )
    {
        arrow = null;
        int targetLength = random.Next(MinArrowLength, maxLength + 1);
        var candidates = board._availableArrowHeads!;
        var occupancy = board._occupancy;

        while (candidates.Count > 0)
        {
            int headIndex = random.Next(candidates.Count);
            ArrowHeadData candidate = candidates[headIndex];

            if (
                occupancy[candidate.head.X, candidate.head.Y] != null
                || occupancy[candidate.next.X, candidate.next.Y] != null
            )
            {
                SwapRemove(candidates, headIndex);
                continue;
            }

            // Compute forward deps as bitset
            int activeWords = ctx.activeWords;
            Array.Clear(ctx.forwardDeps, 0, activeWords);
            Array.Clear(ctx.reachable, 0, activeWords);
            int depCount = ComputeForwardDeps(
                board,
                candidate.head,
                candidate.direction,
                ctx.forwardDeps
            );

            bool hasReachable = depCount > 0;
            if (hasReachable)
            {
                // Check if any forward dep has deps — if all are leaves, skip BFS
                bool needBFS = false;
                for (int w = 0; w < activeWords && !needBFS; w++)
                {
                    ulong bits = ctx.forwardDeps[w];
                    while (bits != 0)
                    {
                        int bit = Ctz64(bits);
                        if (board._hasAnyDeps[(w << 6) | bit])
                        {
                            needBFS = true;
                            break;
                        }
                        bits &= bits - 1;
                    }
                }

                bool hasCycle;
                if (needBFS)
                {
                    // BFS with integrated per-level cycle check — aborts early
                    // if any newly reachable arrow's ray crosses head or next.
                    hasCycle = ComputeReachableSetEarlyAbort(
                        board,
                        ctx.forwardDeps,
                        ctx.reachable,
                        candidate.head,
                        candidate.next,
                        ctx
                    );
                }
                else
                {
                    Array.Copy(ctx.forwardDeps, ctx.reachable, activeWords);
                    hasCycle =
                        board.AnyArrowWithRayThroughBitset(candidate.head, ctx.reachable)
                        || board.AnyArrowWithRayThroughBitset(candidate.next, ctx.reachable);
                }

                if (hasCycle)
                {
                    SwapRemove(candidates, headIndex);
                    continue;
                }
            }

            List<Cell> tail = GreedyWalk(
                board,
                targetLength,
                candidate,
                random,
                ctx.reachable,
                hasReachable,
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

    /// <summary>
    /// Greedy random walk to build an arrow tail. Replaces DFS for speed:
    /// no backtracking, inline neighbor iteration, ray pre-marked in visited array.
    /// </summary>
    private static List<Cell> GreedyWalk(
        Board board,
        int targetLength,
        ArrowHeadData headData,
        Random random,
        ulong[] reachable,
        bool hasReachable,
        GenerationContext ctx
    )
    {
        var path = ctx.path;
        var visited = ctx.visited;
        var dirs = ctx.dirOrder;
        var occupancy = board._occupancy;
        int w = board.Width,
            h = board.Height;

        path.Clear();
        path.Add(headData.head);
        path.Add(headData.next);
        visited[headData.head.X, headData.head.Y] = true;
        visited[headData.next.X, headData.next.Y] = true;

        // Pre-mark ray cells in visited to eliminate per-step IsInRay checks
        (int rdx, int rdy) = Arrow.GetDirectionStep(headData.direction);
        int rx = headData.head.X + rdx,
            ry = headData.head.Y + rdy;
        while (rx >= 0 && rx < w && ry >= 0 && ry < h)
        {
            visited[rx, ry] = true;
            rx += rdx;
            ry += rdy;
        }

        Cell current = headData.next;

        while (path.Count < targetLength)
        {
            // Fisher-Yates shuffle on 4 directions
            dirs[0] = 0;
            dirs[1] = 1;
            dirs[2] = 2;
            dirs[3] = 3;
            for (int i = 3; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = dirs[i];
                dirs[i] = dirs[j];
                dirs[j] = tmp;
            }

            bool found = false;
            for (int i = 0; i < 4; i++)
            {
                int nx,
                    ny;
                switch (dirs[i])
                {
                    case 0:
                        nx = current.X + 1;
                        ny = current.Y;
                        break;
                    case 1:
                        nx = current.X - 1;
                        ny = current.Y;
                        break;
                    case 2:
                        nx = current.X;
                        ny = current.Y + 1;
                        break;
                    default:
                        nx = current.X;
                        ny = current.Y - 1;
                        break;
                }

                if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                    continue;
                if (visited[nx, ny])
                    continue;
                if (occupancy[nx, ny] != null)
                    continue;
                if (hasReachable && board.AnyArrowWithRayThroughBitset(new Cell(nx, ny), reachable))
                    continue;

                Cell neighbor = new(nx, ny);
                path.Add(neighbor);
                visited[nx, ny] = true;
                current = neighbor;
                found = true;
                break;
            }

            if (!found)
                break;
        }

        // Clean up visited: path cells
        foreach (Cell c in path)
            visited[c.X, c.Y] = false;
        // Clean up visited: ray cells
        rx = headData.head.X + rdx;
        ry = headData.head.Y + rdy;
        while (rx >= 0 && rx < w && ry >= 0 && ry < h)
        {
            visited[rx, ry] = false;
            rx += rdx;
            ry += rdy;
        }

        // Return a copy since path is pooled and will be reused
        return new List<Cell>(path);
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
        int cx = head.X + dx,
            cy = head.Y + dy;
        int w = board.Width,
            h = board.Height;
        var occupancy = board._occupancy;

        while (cx >= 0 && cx < w && cy >= 0 && cy < h)
        {
            Arrow hit = occupancy[cx, cy];
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
            cx += dx;
            cy += dy;
        }
        return count;
    }

    /// <summary>
    /// BFS transitive closure with inline early cycle detection.
    /// Checks each newly discovered arrow immediately via direct ray geometry,
    /// avoiding the need to compute the full closure before checking for cycles.
    /// Returns true if any reachable arrow's ray crosses head or next (cycle).
    /// </summary>
    private static bool ComputeReachableSetEarlyAbort(
        Board board,
        ulong[] startBits,
        ulong[] reachable,
        Cell head,
        Cell next,
        GenerationContext ctx
    )
    {
        int stride = board._bitsetWords;
        int words = ctx.activeWords;
        var frontier = ctx.frontier;
        ulong[] depsBits = board._depsBitsFlat;
        int[] nzWords = board._depsNonZeroWords;
        int[] nzCounts = board._depsNonZeroCount;
        int[] headX = board._genHeadX;
        int[] headY = board._genHeadY;
        Arrow.Direction[] dirs = board._genDir;
        int hx = head.X,
            hy = head.Y;
        int nx = next.X,
            ny = next.Y;

        for (int w = 0; w < words; w++)
        {
            reachable[w] = startBits[w];
            frontier[w] = startBits[w];
        }

        // Check level 0 (forward deps themselves)
        if (
            board.AnyArrowWithRayThroughBitset(head, reachable)
            || board.AnyArrowWithRayThroughBitset(next, reachable)
        )
            return true;

        while (true)
        {
            bool hasNext = false;
            for (int w = 0; w < words; w++)
            {
                ulong bits = frontier[w];
                if (bits == 0)
                    continue;
                frontier[w] = 0;
                while (bits != 0)
                {
                    int bit = Ctz64(bits);
                    int idx = (w << 6) | bit;
                    int offset = idx * stride;
                    int nzCount = nzCounts[idx];

                    if (nzCount >= 0 && nzCount <= Board.MaxNonZeroTracked)
                    {
                        int nzBase = idx * Board.MaxNonZeroTracked;
                        for (int i = 0; i < nzCount; i++)
                        {
                            int ww = nzWords[nzBase + i];
                            ulong newBits = depsBits[offset + ww] & ~reachable[ww];
                            if (newBits != 0)
                            {
                                reachable[ww] |= newBits;
                                frontier[ww] |= newBits;
                                hasNext = true;
                                // Inline cycle check for each newly discovered arrow
                                ulong check = newBits;
                                while (check != 0)
                                {
                                    int b = Ctz64(check);
                                    int newIdx = (ww << 6) | b;
                                    if (
                                        IsInRayOf(
                                            headX[newIdx],
                                            headY[newIdx],
                                            dirs[newIdx],
                                            hx,
                                            hy
                                        )
                                        || IsInRayOf(
                                            headX[newIdx],
                                            headY[newIdx],
                                            dirs[newIdx],
                                            nx,
                                            ny
                                        )
                                    )
                                        return true;
                                    check &= check - 1;
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int ww = 0; ww < words; ww++)
                        {
                            ulong newBits = depsBits[offset + ww] & ~reachable[ww];
                            if (newBits != 0)
                            {
                                reachable[ww] |= newBits;
                                frontier[ww] |= newBits;
                                hasNext = true;
                                ulong check = newBits;
                                while (check != 0)
                                {
                                    int b = Ctz64(check);
                                    int newIdx = (ww << 6) | b;
                                    if (
                                        IsInRayOf(
                                            headX[newIdx],
                                            headY[newIdx],
                                            dirs[newIdx],
                                            hx,
                                            hy
                                        )
                                        || IsInRayOf(
                                            headX[newIdx],
                                            headY[newIdx],
                                            dirs[newIdx],
                                            nx,
                                            ny
                                        )
                                    )
                                        return true;
                                    check &= check - 1;
                                }
                            }
                        }
                    }
                    bits &= bits - 1;
                }
            }

            if (!hasNext)
                break;
        }

        return false;
    }

    /// <summary>
    /// Returns true if cell (cx, cy) is in the forward ray of arrow at (ax, ay) facing dir.
    /// </summary>
    private static bool IsInRayOf(int ax, int ay, Arrow.Direction dir, int cx, int cy)
    {
        return dir switch
        {
            Arrow.Direction.Right => cy == ay && cx > ax,
            Arrow.Direction.Left => cy == ay && cx < ax,
            Arrow.Direction.Up => cx == ax && cy > ay,
            Arrow.Direction.Down => cx == ax && cy < ay,
            _ => false,
        };
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

struct ArrowHeadData
{
    public Cell head;
    public Cell next;
    public Arrow.Direction direction;
}
