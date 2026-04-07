using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Board generation with parallelized post-processing for non-WebGL targets.
/// Generation itself is sequential (inherently serial — each arrow depends on prior state).
/// Compaction scan and finalization are parallelized.
/// </summary>
public static class ParallelBoardGeneration
{
    public static void FillBoard(
        Board board, int maxLength, Random random,
        int threadCount = 0, bool compact = false)
    {
        if (threadCount <= 0)
            threadCount = Environment.ProcessorCount;

        // Phase 1: Sequential generation (same as current algorithm)
        board.InitializeForGeneration();
        int maxPossibleArrows = board.Width * board.Height / 2;
        var ctx = new BoardGeneration.GenerationContext(board._bitsetWords, board.Width, board.Height);
        int created = 0;

        while (
            created < maxPossibleArrows
            && board._availableArrowHeads != null
            && board._availableArrowHeads.Count > 0)
        {
            int targetLength = random.Next(2, maxLength + 1);
            var candidates = board._availableArrowHeads;
            bool placed = false;

            while (candidates.Count > 0)
            {
                int headIndex = random.Next(candidates.Count);
                ArrowHeadData candidate = candidates[headIndex];

                ctx.EnsureCapacity(board._bitsetWords);
                ctx.activeWords = (board._nextGenIndex + 63) >> 6;

                Arrow result = BoardGeneration.EvaluateCandidate(
                    board, candidate, targetLength, random, ctx);
                if (result == null)
                {
                    BoardGeneration.SwapRemove(candidates, headIndex);
                    continue;
                }

                board.AddArrowForGeneration(result);
                created++;
                placed = true;
                break;
            }

            if (!placed) break;
        }

        // Phase 2: Parallel compaction
        if (compact)
            CompactParallel(board, threadCount);

        // Phase 3: Parallel finalization
        board.FinalizeGenerationParallel(threadCount);
    }

    /// <summary>
    /// Compaction with parallel merge candidate scan.
    /// Each pass: scan arrows in parallel to find merge candidates, then apply sequentially.
    /// </summary>
    private static void CompactParallel(Board board, int threadCount)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            var arrows = board.Arrows;
            int count = arrows.Count;

            // Parallel scan: for each arrow, find its forward merge candidate (if any)
            var mergeCandidates = new (Arrow blocker, Arrow dependent)[count];
            bool[] hasMerge = new bool[count];

            Parallel.For(0, count,
                new ParallelOptions { MaxDegreeOfParallelism = threadCount },
                i =>
                {
                    Arrow dependent = arrows[i];
                    if (dependent._generationIndex < 0) return;

                    (int dx, int dy) = Arrow.GetDirectionStep(dependent.HeadDirection);
                    int cx = dependent.HeadCell.X + dx, cy = dependent.HeadCell.Y + dy;
                    while (cx >= 0 && cx < board.Width && cy >= 0 && cy < board.Height)
                    {
                        Arrow blocker = board._occupancy[cx, cy];
                        if (blocker != null && blocker != dependent &&
                            blocker._generationIndex >= 0 &&
                            CanMerge(blocker, dependent))
                        {
                            mergeCandidates[i] = (blocker, dependent);
                            hasMerge[i] = true;
                            break;
                        }
                        cx += dx; cy += dy;
                    }
                }
            );

            // Sequential application: greedily apply non-overlapping merges
            var consumed = new HashSet<Arrow>();
            for (int i = 0; i < count; i++)
            {
                if (!hasMerge[i]) continue;
                var (blocker, dependent) = mergeCandidates[i];
                if (consumed.Contains(blocker) || consumed.Contains(dependent)) continue;
                if (blocker._generationIndex < 0 || dependent._generationIndex < 0) continue;

                var merged = MergeArrows(blocker, dependent);
                board.RemoveArrowForGeneration(dependent);
                board.RemoveArrowForGeneration(blocker);
                board.AddArrowForGeneration(merged);
                consumed.Add(blocker);
                consumed.Add(dependent);
                changed = true;
            }
        }
    }

    private static bool CanMerge(Arrow blocker, Arrow dependent)
    {
        if (blocker.HeadDirection != dependent.HeadDirection) return false;
        bool collinear = blocker.HeadDirection switch
        {
            Arrow.Direction.Right or Arrow.Direction.Left =>
                blocker.HeadCell.Y == dependent.HeadCell.Y,
            Arrow.Direction.Up or Arrow.Direction.Down =>
                blocker.HeadCell.X == dependent.HeadCell.X,
            _ => false,
        };
        if (!collinear) return false;
        Cell bt = blocker.Cells[blocker.Cells.Count - 1];
        Cell dh = dependent.HeadCell;
        int adx = Math.Abs(bt.X - dh.X), ady = Math.Abs(bt.Y - dh.Y);
        return (adx == 1 && ady == 0) || (adx == 0 && ady == 1);
    }

    private static Arrow MergeArrows(Arrow blocker, Arrow dependent)
    {
        var cells = new List<Cell>(blocker.Cells.Count + dependent.Cells.Count);
        for (int i = 0; i < blocker.Cells.Count; i++) cells.Add(blocker.Cells[i]);
        for (int i = 0; i < dependent.Cells.Count; i++) cells.Add(dependent.Cells[i]);
        return new Arrow(cells);
    }
}
