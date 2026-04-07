using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        int[] sizes = { 40, 100, 200 };
        int maxArrowLength = 20;
        int runsPerSize = 5;

        string[] algorithms = { "current", "current+compact", "current+inline", "current+true-inline", "ranked+inline" };

        // Warm up JIT for all algorithms
        Console.WriteLine("Warming up...");
        foreach (string algo in algorithms)
            RunBenchmark(algo, 20, 20, maxArrowLength, seed: 0);

        foreach (string algo in algorithms)
        {
            string label = algo switch
            {
                "current" => "Current Algorithm (Constructive + Cycle Detection)",
                "current+compact" => "Current + Post-Process Compaction",
                "current+inline" => "Current + Inline Compaction (Replay)",
                "current+true-inline" => "Current + True Inline Compaction",
                "ranked" => "Ranked (Topological Ordering, Conservative)",
                "ranked+inline" => "Ranked + Inline Compaction",
                "ranked-hybrid" => "Ranked Hybrid (Rank Fast-Path + BFS Fallback)",
                "repair-random" => "Random Placement + Flip/Repair (Unbiased)",
                "repair-biased" => "Random Placement + Flip/Repair (Edge-Biased)",
                _ => algo,
            };
            Console.WriteLine();
            Console.WriteLine($"=== {label} ===");
            Console.WriteLine();
            RunSuite(algo, sizes, maxArrowLength, runsPerSize);
        }
    }

    static void RunSuite(string name, int[] sizes, int maxArrowLength, int runsPerSize)
    {
        Console.WriteLine(
            $"{"Size",-12} {"Run",-6} {"Arrows",-10} {"Density",-10} {"Time ms",-12} " +
            $"{"DAG Dep",-10} {"Triv%",-8} {"XDir%",-8} {"Solvable",-10}"
        );
        Console.WriteLine(new string('-', 96));

        foreach (int size in sizes)
        {
            var timings = new List<double>();
            var arrowCounts = new List<int>();

            for (int run = 0; run < runsPerSize; run++)
            {
                int seed = size * 1000 + run;
                var result = RunBenchmark(name, size, size, maxArrowLength, seed);

                Console.WriteLine(
                    $"{size}x{size,-7} {run + 1,-6} {result.ArrowCount,-10} " +
                    $"{result.Density:F3}     {result.ElapsedMs,-12:F1} " +
                    $"{result.DagDepth,-10} {result.TrivialChainPct:F1}%   " +
                    $"{result.CrossDirDepPct:F1}%   {(result.Solvable ? "YES" : "FAIL"),-10}"
                );

                timings.Add(result.ElapsedMs);
                arrowCounts.Add(result.ArrowCount);
            }

            Console.WriteLine(
                $"{"  avg",-12} {"--",-6} {(int)arrowCounts.Average(),-10} " +
                $"{"--",-10} {timings.Average(),-12:F1}"
            );
            Console.WriteLine();
        }
    }

    static BenchmarkResult RunBenchmark(string algorithm, int width, int height, int maxLength, int seed)
    {
        Board board;
        double elapsedMs;

        switch (algorithm)
        {
            case "current":
                (board, elapsedMs) = RunCurrentAlgorithm(width, height, maxLength, seed);
                break;
            case "current+compact":
                (board, elapsedMs) = RunCurrentWithCompaction(width, height, maxLength, seed);
                break;
            case "current+inline":
                (board, elapsedMs) = RunCurrentWithInlineCompaction(width, height, maxLength, seed);
                break;
            case "current+true-inline":
                (board, elapsedMs) = RunCurrentWithTrueInlineCompaction(width, height, maxLength, seed);
                break;
            case "layered":
                (board, elapsedMs) = RunLayered(width, height, maxLength, seed);
                break;
            case "ranked":
                (board, elapsedMs) = RunRanked(width, height, maxLength, seed);
                break;
            case "ranked+inline":
                (board, elapsedMs) = RunRankedInline(width, height, maxLength, seed);
                break;
            case "ranked-hybrid":
                (board, elapsedMs) = RunRankedHybrid(width, height, maxLength, seed);
                break;
            case "repair-random":
                (board, elapsedMs) = RunRepair(width, height, maxLength, seed, biased: false);
                break;
            case "repair-biased":
                (board, elapsedMs) = RunRepair(width, height, maxLength, seed, biased: true);
                break;
            default:
                throw new ArgumentException($"Unknown algorithm: {algorithm}");
        }

        var metrics = AnalyzeBoard(board);
        bool solvable = VerifySolvable(board);

        return new BenchmarkResult
        {
            ArrowCount = board.Arrows.Count,
            OccupiedCells = board.OccupiedCellCount,
            Density = (double)board.OccupiedCellCount / (width * height),
            ElapsedMs = elapsedMs,
            DagDepth = metrics.DagDepth,
            TrivialChainPct = metrics.TrivialChainPct,
            CrossDirDepPct = metrics.CrossDirDepPct,
            Solvable = solvable,
        };
    }

    static (Board board, double elapsedMs) RunCurrentAlgorithm(int width, int height, int maxLength, int seed)
    {
        var board = new Board(width, height);
        var random = new Random(seed);
        var sw = Stopwatch.StartNew();

        var enumerator = BoardGeneration.FillBoardIncremental(board, maxLength, random);
        while (enumerator.MoveNext()) { }

        sw.Stop();
        return (board, sw.Elapsed.TotalMilliseconds);
    }

    static (Board board, double elapsedMs) RunCurrentWithTrueInlineCompaction(int width, int height, int maxLength, int seed)
    {
        var board = new Board(width, height);
        var random = new Random(seed);
        var sw = Stopwatch.StartNew();

        var enumerator = BoardGeneration.FillBoardIncremental(board, maxLength, random, compactInline: true);
        while (enumerator.MoveNext()) { }

        sw.Stop();
        return (board, sw.Elapsed.TotalMilliseconds);
    }

    static (Board board, double elapsedMs) RunCurrentWithInlineCompaction(int width, int height, int maxLength, int seed)
    {
        // Phase 1: Generate using the current algorithm to get the arrow list
        var genBoard = new Board(width, height);
        var random = new Random(seed);
        var sw = Stopwatch.StartNew();

        var enumerator = BoardGeneration.FillBoardIncremental(genBoard, maxLength, random);
        // Collect arrows in placement order
        var placementOrder = new List<Arrow>();
        int prevCount = 0;
        while (enumerator.MoveNext())
        {
            if (genBoard.Arrows.Count > prevCount)
            {
                placementOrder.Add(genBoard.Arrows[genBoard.Arrows.Count - 1]);
                prevCount = genBoard.Arrows.Count;
            }
        }

        // Phase 2: Replay arrows through inline compaction using raw occupancy
        var occupancy = new Arrow[width, height];
        var placed = new List<Arrow>();
        var rightByRow = new List<Arrow>[height];
        var leftByRow = new List<Arrow>[height];
        for (int y = 0; y < height; y++) { rightByRow[y] = new List<Arrow>(); leftByRow[y] = new List<Arrow>(); }
        var upByCol = new List<Arrow>[width];
        var downByCol = new List<Arrow>[width];
        for (int x = 0; x < width; x++) { upByCol[x] = new List<Arrow>(); downByCol[x] = new List<Arrow>(); }

        foreach (var original in placementOrder)
        {
            var arrow = new Arrow(original.Cells);
            Place(arrow, placed, occupancy, rightByRow, leftByRow, upByCol, downByCol);
            TryInlineCompact(arrow, placed, occupancy, width, height, rightByRow, leftByRow, upByCol, downByCol);
        }

        sw.Stop();

        // Build final Board for analysis
        var board = new Board(width, height);
        foreach (var a in placed)
            board.AddArrow(new Arrow(a.Cells));
        return (board, sw.Elapsed.TotalMilliseconds);
    }

    private static void Place(Arrow arrow, List<Arrow> placed, Arrow[,] occupancy,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow, List<Arrow>[] upByCol, List<Arrow>[] downByCol)
    {
        placed.Add(arrow);
        foreach (var c in arrow.Cells) occupancy[c.X, c.Y] = arrow;
        switch (arrow.HeadDirection)
        {
            case Arrow.Direction.Right: rightByRow[arrow.HeadCell.Y].Add(arrow); break;
            case Arrow.Direction.Left: leftByRow[arrow.HeadCell.Y].Add(arrow); break;
            case Arrow.Direction.Up: upByCol[arrow.HeadCell.X].Add(arrow); break;
            case Arrow.Direction.Down: downByCol[arrow.HeadCell.X].Add(arrow); break;
        }
    }

    private static void Unplace(Arrow arrow, List<Arrow> placed, Arrow[,] occupancy,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow, List<Arrow>[] upByCol, List<Arrow>[] downByCol)
    {
        placed.Remove(arrow);
        foreach (var c in arrow.Cells) occupancy[c.X, c.Y] = null;
        switch (arrow.HeadDirection)
        {
            case Arrow.Direction.Right: rightByRow[arrow.HeadCell.Y].Remove(arrow); break;
            case Arrow.Direction.Left: leftByRow[arrow.HeadCell.Y].Remove(arrow); break;
            case Arrow.Direction.Up: upByCol[arrow.HeadCell.X].Remove(arrow); break;
            case Arrow.Direction.Down: downByCol[arrow.HeadCell.X].Remove(arrow); break;
        }
    }

    private static void TryInlineCompact(Arrow newArrow, List<Arrow> placed, Arrow[,] occupancy,
        int width, int height,
        List<Arrow>[] rightByRow, List<Arrow>[] leftByRow, List<Arrow>[] upByCol, List<Arrow>[] downByCol)
    {
        // Check forward deps: arrows in newArrow's ray
        {
            (int dx, int dy) = Arrow.GetDirectionStep(newArrow.HeadDirection);
            int cx = newArrow.HeadCell.X + dx, cy = newArrow.HeadCell.Y + dy;
            while (cx >= 0 && cx < width && cy >= 0 && cy < height)
            {
                Arrow blocker = occupancy[cx, cy];
                if (blocker != null && blocker != newArrow && CanMerge(blocker, newArrow))
                {
                    var merged = DoMerge(blocker, newArrow);
                    Unplace(newArrow, placed, occupancy, rightByRow, leftByRow, upByCol, downByCol);
                    Unplace(blocker, placed, occupancy, rightByRow, leftByRow, upByCol, downByCol);
                    Place(merged, placed, occupancy, rightByRow, leftByRow, upByCol, downByCol);
                    newArrow = merged;
                    (dx, dy) = Arrow.GetDirectionStep(newArrow.HeadDirection);
                    cx = newArrow.HeadCell.X + dx;
                    cy = newArrow.HeadCell.Y + dy;
                    continue;
                }
                cx += dx; cy += dy;
            }
        }

        // Check reverse deps: arrows whose rays pass through newArrow's cells
        foreach (var cell in newArrow.Cells)
        {
            int cx = cell.X, cy = cell.Y;
            Arrow dep = null;
            foreach (var a in rightByRow[cy])
                if (a != newArrow && a.HeadCell.X < cx && CanMerge(newArrow, a)) { dep = a; break; }
            if (dep == null) foreach (var a in leftByRow[cy])
                if (a != newArrow && a.HeadCell.X > cx && CanMerge(newArrow, a)) { dep = a; break; }
            if (dep == null) foreach (var a in upByCol[cx])
                if (a != newArrow && a.HeadCell.Y < cy && CanMerge(newArrow, a)) { dep = a; break; }
            if (dep == null) foreach (var a in downByCol[cx])
                if (a != newArrow && a.HeadCell.Y > cy && CanMerge(newArrow, a)) { dep = a; break; }

            if (dep != null)
            {
                var merged = DoMerge(newArrow, dep);
                Unplace(dep, placed, occupancy, rightByRow, leftByRow, upByCol, downByCol);
                Unplace(newArrow, placed, occupancy, rightByRow, leftByRow, upByCol, downByCol);
                Place(merged, placed, occupancy, rightByRow, leftByRow, upByCol, downByCol);
                newArrow = merged;
                break;
            }
        }
    }

    private static bool CanMerge(Arrow blocker, Arrow dependent)
    {
        if (blocker.HeadDirection != dependent.HeadDirection) return false;
        bool collinear = blocker.HeadDirection switch
        {
            Arrow.Direction.Right or Arrow.Direction.Left => blocker.HeadCell.Y == dependent.HeadCell.Y,
            Arrow.Direction.Up or Arrow.Direction.Down => blocker.HeadCell.X == dependent.HeadCell.X,
            _ => false,
        };
        if (!collinear) return false;
        Cell bt = blocker.Cells[blocker.Cells.Count - 1];
        Cell dh = dependent.HeadCell;
        int adx = Math.Abs(bt.X - dh.X), ady = Math.Abs(bt.Y - dh.Y);
        return (adx == 1 && ady == 0) || (adx == 0 && ady == 1);
    }

    private static Arrow DoMerge(Arrow blocker, Arrow dependent)
    {
        var cells = new List<Cell>(blocker.Cells.Count + dependent.Cells.Count);
        cells.AddRange(blocker.Cells);
        cells.AddRange(dependent.Cells);
        return new Arrow(cells);
    }

    static (Board board, double elapsedMs) RunRankedInline(int width, int height, int maxLength, int seed)
    {
        var random = new Random(seed);
        var sw = Stopwatch.StartNew();
        var arrows = InlineCompactGeneration.Generate(width, height, maxLength, random);
        sw.Stop();

        var board = new Board(width, height);
        foreach (var a in arrows)
            board.AddArrow(new Arrow(a.Cells));
        return (board, sw.Elapsed.TotalMilliseconds);
    }

    static (Board board, double elapsedMs) RunCurrentWithCompaction(int width, int height, int maxLength, int seed)
    {
        var board = new Board(width, height);
        var random = new Random(seed);
        var sw = Stopwatch.StartNew();

        var enumerator = BoardGeneration.FillBoardIncremental(board, maxLength, random);
        while (enumerator.MoveNext()) { }

        board = TrivialChainCompactor.Compact(board);

        sw.Stop();
        return (board, sw.Elapsed.TotalMilliseconds);
    }

    static (Board board, double elapsedMs) RunRanked(int width, int height, int maxLength, int seed)
    {
        var random = new Random(seed);
        var sw = Stopwatch.StartNew();
        var arrows = RankedGeneration.Generate(width, height, maxLength, random);
        sw.Stop();

        var board = new Board(width, height);
        foreach (var a in arrows)
            board.AddArrow(new Arrow(a.Cells));
        return (board, sw.Elapsed.TotalMilliseconds);
    }

    static (Board board, double elapsedMs) RunRankedHybrid(int width, int height, int maxLength, int seed)
    {
        var random = new Random(seed);
        var sw = Stopwatch.StartNew();
        var arrows = RankedHybridGeneration.Generate(width, height, maxLength, random);
        sw.Stop();

        var board = new Board(width, height);
        foreach (var a in arrows)
            board.AddArrow(new Arrow(a.Cells));
        return (board, sw.Elapsed.TotalMilliseconds);
    }

    static (Board board, double elapsedMs) RunLayered(int width, int height, int maxLength, int seed)
    {
        var random = new Random(seed);
        var sw = Stopwatch.StartNew();
        var arrows = LayeredGeneration.Generate(width, height, maxLength, random);
        sw.Stop();

        var board = new Board(width, height);
        foreach (var a in arrows)
            board.AddArrow(new Arrow(a.Cells));
        return (board, sw.Elapsed.TotalMilliseconds);
    }

    static (Board board, double elapsedMs) RunRepair(int width, int height, int maxLength, int seed, bool biased)
    {
        var random = new Random(seed);
        var sw = Stopwatch.StartNew();
        var arrows = RepairGeneration.Generate(width, height, maxLength, random, biased);
        sw.Stop();

        var board = new Board(width, height);
        foreach (var a in arrows)
            board.AddArrow(new Arrow(a.Cells));
        return (board, sw.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Verify the board is solvable by repeatedly clearing any clearable arrow
    /// until all arrows are removed.
    /// </summary>
    static bool VerifySolvable(Board board)
    {
        // Work on a copy — build a fresh board and add all arrows
        var copy = new Board(board.Width, board.Height);
        foreach (Arrow arrow in board.Arrows)
            copy.AddArrow(arrow);

        while (copy.Arrows.Count > 0)
        {
            Arrow clearable = null;
            foreach (Arrow a in copy.Arrows)
            {
                if (copy.IsClearable(a))
                {
                    clearable = a;
                    break;
                }
            }
            if (clearable == null)
                return false; // stuck — cycle detected
            copy.RemoveArrow(clearable);
        }
        return true;
    }

    /// <summary>
    /// Analyze the DAG structure of a board for quality metrics.
    /// </summary>
    static BoardMetrics AnalyzeBoard(Board board)
    {
        var arrows = board.Arrows;
        if (arrows.Count == 0)
            return new BoardMetrics();

        // Build adjacency for analysis: arrow -> set of arrows it depends on
        var deps = new Dictionary<Arrow, List<Arrow>>();
        foreach (Arrow a in arrows)
        {
            var depList = new List<Arrow>();
            // Walk the ray to find dependencies
            (int dx, int dy) = Arrow.GetDirectionStep(a.HeadDirection);
            int cx = a.HeadCell.X + dx, cy = a.HeadCell.Y + dy;
            while (cx >= 0 && cx < board.Width && cy >= 0 && cy < board.Height)
            {
                Arrow hit = board.GetArrowAt(new Cell(cx, cy));
                if (hit != null && hit != a && !depList.Contains(hit))
                    depList.Add(hit);
                cx += dx;
                cy += dy;
            }
            deps[a] = depList;
        }

        // DAG depth via iterative topological sort (Kahn's algorithm)
        // Compute in-degree, then process in topological order
        var depth = new Dictionary<Arrow, int>();
        var reverseDeps = new Dictionary<Arrow, List<Arrow>>();
        var inDegree = new Dictionary<Arrow, int>();
        foreach (Arrow a in arrows)
        {
            depth[a] = 0;
            inDegree[a] = deps[a].Count;
            reverseDeps[a] = new List<Arrow>();
        }
        foreach (Arrow a in arrows)
            foreach (Arrow dep in deps[a])
                reverseDeps[dep].Add(a);

        var queue = new Queue<Arrow>();
        foreach (Arrow a in arrows)
            if (inDegree[a] == 0) queue.Enqueue(a);

        int maxDepth = 0;
        while (queue.Count > 0)
        {
            var a = queue.Dequeue();
            foreach (Arrow dependent in reverseDeps[a])
            {
                int candidate = depth[a] + 1;
                if (candidate > depth[dependent])
                    depth[dependent] = candidate;
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
            if (depth[a] > maxDepth) maxDepth = depth[a];
        }

        // Trivial chain detection: collinear same-direction arrows where one depends on the other
        int trivialPairs = 0;
        int totalDeps = 0;
        foreach (Arrow a in arrows)
        {
            foreach (Arrow dep in deps[a])
            {
                totalDeps++;
                if (a.HeadDirection == dep.HeadDirection)
                {
                    // Check collinearity: same row (horizontal) or same column (vertical)
                    bool collinear = a.HeadDirection switch
                    {
                        Arrow.Direction.Right or Arrow.Direction.Left => a.HeadCell.Y == dep.HeadCell.Y,
                        Arrow.Direction.Up or Arrow.Direction.Down => a.HeadCell.X == dep.HeadCell.X,
                        _ => false,
                    };
                    if (collinear) trivialPairs++;
                }
            }
        }

        // Cross-direction dependency ratio
        int crossDirDeps = 0;
        foreach (Arrow a in arrows)
            foreach (Arrow dep in deps[a])
                if (a.HeadDirection != dep.HeadDirection)
                    crossDirDeps++;

        return new BoardMetrics
        {
            DagDepth = maxDepth,
            TrivialChainPct = totalDeps > 0 ? 100.0 * trivialPairs / totalDeps : 0,
            CrossDirDepPct = totalDeps > 0 ? 100.0 * crossDirDeps / totalDeps : 0,
        };
    }

    struct BenchmarkResult
    {
        public int ArrowCount;
        public int OccupiedCells;
        public double Density;
        public double ElapsedMs;
        public int DagDepth;
        public double TrivialChainPct;
        public double CrossDirDepPct;
        public bool Solvable;
    }

    struct BoardMetrics
    {
        public int DagDepth;
        public double TrivialChainPct;
        public double CrossDirDepPct;
    }
}
