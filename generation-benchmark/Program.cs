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

        string[] algorithms = { "current", "ranked" };

        // Warm up JIT for all algorithms
        Console.WriteLine("Warming up...");
        foreach (string algo in algorithms)
            RunBenchmark(algo, 20, 20, maxArrowLength, seed: 0);

        foreach (string algo in algorithms)
        {
            string label = algo switch
            {
                "current" => "Current Algorithm (Constructive + Cycle Detection)",
                "ranked" => "Ranked (Topological Ordering, Conservative)",
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
            case "layered":
                (board, elapsedMs) = RunLayered(width, height, maxLength, seed);
                break;
            case "ranked":
                (board, elapsedMs) = RunRanked(width, height, maxLength, seed);
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
