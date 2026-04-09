using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

public static class BoardGeneration
{
    private const int MinArrowLength = 2;

    /// <summary>
    /// Pooled resources reused across candidates to eliminate per-candidate allocations.
    /// Used only by <see cref="GenerateArrows"/> (managed sync API for tests).
    /// <see cref="FillBoardIncremental"/> uses the Burst path instead.
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
    /// Incremental version of board filling using Burst-compiled generation.
    /// Places as many arrows as possible, yielding after each arrow for progress.
    /// Yields <see cref="GenerationPhase"/> values between phases.
    /// </summary>
    public static IEnumerator FillBoardIncremental(Board board, int maxLength, Random random)
    {
        int maxPossibleArrows = board.Width * board.Height / 2;

        // Allocate native state for Burst generation.
        // try/finally ensures disposal even if the iterator is abandoned (cancelled).
        var state = new NativeGenerationState(board.Width, board.Height, Allocator.Persistent);
        try
        {
            state.InitializeCandidates();
            board.InitialCandidateCount = state.candidates.Length;

            // Unity.Mathematics.Random requires nonzero seed
            var rng = new Random((uint)random.Next(1, int.MaxValue));
            int created = 0;

            while (created < maxPossibleArrows && state.candidates.Length > 0)
            {
                if (!NativeGeneration.TryGenerateArrow(ref state, maxLength, ref rng))
                    break;

                // Extract arrow from native scratch buffers
                var cells = new List<Cell>(state.lastArrowCellCount);
                for (int i = 0; i < state.lastArrowCellCount; i++)
                    cells.Add(new Cell(state.scratchCellsX[i], state.scratchCellsY[i]));
                var arrow = new Arrow(cells);
                arrow._generationIndex = created;

                board._arrows.Add(arrow);
                board._arrowSet.Add(arrow);
                board.OccupiedCellCount += cells.Count;
                board._nativeRemainingCandidates = state.candidates.Length;
                created++;
                yield return null;
            }

            // Copy native generation state to Board for compaction
            board.InitializeFromNativeGeneration(ref state);
        }
        finally
        {
            state.Dispose();
        }

        // Compaction phase: merge trivial collinear same-direction chains
        yield return GenerationPhase.Compacting;
        var compactor = CompactBoardInPlace(board);
        while (compactor.MoveNext())
            yield return compactor.Current;

        // Finalization phase: build HashSet dependency graph
        yield return GenerationPhase.Finalizing;
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

            arrow = new Arrow(tail);
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

    /// <summary>
    /// Post-process compaction: iteratively merges trivial collinear same-direction
    /// chains in-place on the board using RemoveArrowForGeneration/AddArrowForGeneration.
    /// Yields the cumulative merge count after each merge for smooth progress tracking.
    /// </summary>
    private static IEnumerator<int> CompactBoardInPlace(Board board)
    {
        int mergeCount = 0;
        bool changed = true;
        while (changed)
        {
            changed = false;
            var arrows = new List<Arrow>(board.Arrows);

            foreach (Arrow dependent in arrows)
            {
                // Skip arrows merged earlier in this pass
                if (dependent._generationIndex < 0)
                    continue;

                (int dx, int dy) = Arrow.GetDirectionStep(dependent.HeadDirection);
                int cx = dependent.HeadCell.X + dx,
                    cy = dependent.HeadCell.Y + dy;
                while (cx >= 0 && cx < board.Width && cy >= 0 && cy < board.Height)
                {
                    Arrow blocker = board._occupancy[cx, cy];
                    if (
                        blocker != null
                        && blocker != dependent
                        && blocker._generationIndex >= 0
                        && CanMergeForCompaction(blocker, dependent)
                    )
                    {
                        var merged = MergeArrows(blocker, dependent);
                        board.RemoveArrowForGeneration(dependent);
                        board.RemoveArrowForGeneration(blocker);
                        board.AddArrowForGeneration(merged);
                        mergeCount++;
                        changed = true;
                        yield return mergeCount;
                        break;
                    }
                    cx += dx;
                    cy += dy;
                }
            }
        }
    }

    /// <summary>
    /// Checks if two arrows can be merged: same direction, collinear,
    /// and blocker's tail is adjacent to dependent's head.
    /// </summary>
    private static bool CanMergeForCompaction(Arrow blocker, Arrow dependent)
    {
        if (blocker.HeadDirection != dependent.HeadDirection)
            return false;
        bool collinear = blocker.HeadDirection switch
        {
            Arrow.Direction.Right or Arrow.Direction.Left => blocker.HeadCell.Y
                == dependent.HeadCell.Y,
            Arrow.Direction.Up or Arrow.Direction.Down => blocker.HeadCell.X
                == dependent.HeadCell.X,
            _ => false,
        };
        if (!collinear)
            return false;

        Cell blockerTail = blocker.Cells[blocker.Cells.Count - 1];
        Cell dependentHead = dependent.HeadCell;
        int adx = Math.Abs(blockerTail.X - dependentHead.X);
        int ady = Math.Abs(blockerTail.Y - dependentHead.Y);
        return (adx == 1 && ady == 0) || (adx == 0 && ady == 1);
    }

    /// <summary>Creates a merged arrow: blocker cells followed by dependent cells.</summary>
    private static Arrow MergeArrows(Arrow blocker, Arrow dependent)
    {
        var cells = new List<Cell>(blocker.Cells.Count + dependent.Cells.Count);
        for (int i = 0; i < blocker.Cells.Count; i++)
            cells.Add(blocker.Cells[i]);
        for (int i = 0; i < dependent.Cells.Count; i++)
            cells.Add(dependent.Cells[i]);
        return new Arrow(cells);
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
