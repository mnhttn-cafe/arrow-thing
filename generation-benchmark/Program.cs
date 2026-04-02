using System;
using System.Collections.Generic;
using System.Diagnostics;

class Program
{
    delegate void FillFunc(int width, int height, int maxLength, Random random);

    static readonly (string name, FillFunc fill)[] Versions = new (string, FillFunc)[]
    {
        ("V1 Original", FillV1),
        ("V2 SpatialRayIndex", FillV2),
        ("V3 Bitset+DFS", FillV3),
        ("V4 GreedyWalk", FillV4),
        ("V5 CachedClosure", FillV5),
    };

    static V1.Board _lastV1;
    static V2.Board _lastV2;
    static V3.Board _lastV3;
    static V4.Board _lastV4;
    static V5.Board _lastV5;

    static void FillV1(int w, int h, int maxLen, Random rng)
    {
        var board = new V1.Board(w, h);
        V1.BoardGeneration.FillBoard(board, 2, maxLen, rng);
        _lastV1 = board;
    }

    static void FillV2(int w, int h, int maxLen, Random rng)
    {
        var board = new V2.Board(w, h);
        V2.BoardGeneration.FillBoard(board, 2, maxLen, rng);
        _lastV2 = board;
    }

    static void FillV3(int w, int h, int maxLen, Random rng)
    {
        var board = new V3.Board(w, h);
        V3.BoardGeneration.FillBoard(board, maxLen, rng);
        _lastV3 = board;
    }

    static void FillV4(int w, int h, int maxLen, Random rng)
    {
        var board = new V4.Board(w, h);
        V4.BoardGeneration.FillBoard(board, maxLen, rng);
        _lastV4 = board;
    }

    static void FillV5(int w, int h, int maxLen, Random rng)
    {
        var board = new V5.Board(w, h);
        V5.BoardGeneration.FillBoard(board, maxLen, rng);
        _lastV5 = board;
    }

    static void Main(string[] args)
    {
        // Parse optional flags
        bool skipSlow = Array.Exists(args, a => a == "--fast");
        bool solvCheck = !Array.Exists(args, a => a == "--no-solvability");

        // Warmup all versions
        foreach (var (_, fill) in Versions)
            fill(5, 5, 3, new Random(0));

        Console.WriteLine("=== Board Generation Benchmark ===");
        Console.WriteLine($"    Versions: {Versions.Length}");
        Console.WriteLine($"    Mode: {(skipSlow ? "fast (skip large boards)" : "full")}");
        Console.WriteLine($"    Solvability: {(solvCheck ? "enabled" : "disabled")}");
        Console.WriteLine();

        // --- Performance comparison ---
        Console.WriteLine("--- Performance ---");
        Console.WriteLine(
            $"{"Size", -18} {"Version", -22} {"Total ms", 10} {"ms/board", 10} {"Arrows", 8} {"Fill%", 6}"
        );
        Console.WriteLine(new string('-', 78));

        RunComparison(10, 10, 5, 100, "100x 10x10");
        RunComparison(20, 20, 10, 10, "10x 20x20");
        RunComparison(50, 50, 20, 3, "3x 50x50 ml=20");

        if (!skipSlow)
        {
            RunComparison(50, 50, 50, 3, "3x 50x50 ml=50");
            RunComparison(100, 100, 50, 1, "1x 100x100");
            RunComparison(200, 200, 50, 1, "1x 200x200");
        }

        // V4+V5 only for huge boards (V1-V3 take minutes+)
        Console.WriteLine("--- V4/V5 Only: Huge Boards ---");
        Console.WriteLine(
            $"{"Size", -18} {"Version", -22} {"Total ms", 10} {"ms/board", 10} {"Arrows", 8} {"Fill%", 6}"
        );
        Console.WriteLine(new string('-', 78));
        var fastVersions = new (string name, FillFunc fill)[]
        {
            ("V4 GreedyWalk", FillV4),
            ("V5 EarlyAbort", FillV5),
        };
        RunComparisonSubset(400, 400, 50, 1, "1x 400x400", fastVersions);

        // V5 only for extreme sizes (V4 takes 30+ min at 1000x1000)
        var v5Only = new (string name, FillFunc fill)[] { ("V5 EarlyAbort", FillV5) };
        RunComparisonSubset(1000, 1000, 50, 1, "1x 1000x1000", v5Only);

        // --- Solvability stress test ---
        if (solvCheck)
        {
            Console.WriteLine();
            Console.WriteLine("--- Solvability ---");
            Console.WriteLine(
                $"{"Size", -18} {"Version", -22} {"Seeds", 6} {"Failures", 10} {"Time ms", 10}"
            );
            Console.WriteLine(new string('-', 70));

            RunSolvability(10, 10, 5, 50);
            RunSolvability(20, 20, 10, 20);

            if (!skipSlow)
            {
                RunSolvability(50, 50, 20, 10);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Done.");
    }

    static void RunComparison(int w, int h, int maxLen, int iterations, string label)
    {
        foreach (var (name, fill) in Versions)
        {
            var sw = Stopwatch.StartNew();
            int totalArrows = 0;
            int totalCells = 0;
            for (int i = 0; i < iterations; i++)
            {
                fill(w, h, maxLen, new Random(i));
                var (arrows, cells) = GetLastStats(name);
                totalArrows += arrows;
                totalCells += cells;
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
            double avgArrows = (double)totalArrows / iterations;
            double fillRate = (double)totalCells / (w * h) / iterations * 100;

            Console.WriteLine(
                $"{label, -18} {name, -22} {sw.ElapsedMilliseconds, 10} {avgMs, 10:F1} {avgArrows, 8:F0} {fillRate, 5:F0}%"
            );
        }
        Console.WriteLine();
    }

    static void RunComparisonSubset(
        int w,
        int h,
        int maxLen,
        int iterations,
        string label,
        (string name, FillFunc fill)[] subset
    )
    {
        foreach (var (name, fill) in subset)
        {
            var sw = Stopwatch.StartNew();
            int totalArrows = 0;
            int totalCells = 0;
            for (int i = 0; i < iterations; i++)
            {
                fill(w, h, maxLen, new Random(i));
                var (arrows, cells) = GetLastStats(name);
                totalArrows += arrows;
                totalCells += cells;
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
            double avgArrows = (double)totalArrows / iterations;
            double fillRate = (double)totalCells / (w * h) / iterations * 100;

            Console.WriteLine(
                $"{label, -18} {name, -22} {sw.ElapsedMilliseconds, 10} {avgMs, 10:F1} {avgArrows, 8:F0} {fillRate, 5:F0}%"
            );
        }
        Console.WriteLine();
    }

    static void RunSolvability(int w, int h, int maxLen, int seeds)
    {
        foreach (var (name, fill) in Versions)
        {
            var sw = Stopwatch.StartNew();
            int failures = 0;
            for (int seed = 0; seed < seeds; seed++)
            {
                fill(w, h, maxLen, new Random(seed));
                if (!IsSolvable(name))
                    failures++;
            }
            sw.Stop();

            string result = failures == 0 ? "ALL OK" : $"{failures} FAIL";
            Console.WriteLine(
                $"{w}x{h} ml={maxLen, -6} {name, -22} {seeds, 6} {result, 10} {sw.ElapsedMilliseconds, 10}"
            );
        }
        Console.WriteLine();
    }

    static (int arrows, int cells) GetLastStats(string versionName)
    {
        if (versionName.Contains("V1"))
            return (_lastV1.Arrows.Count, _lastV1.OccupiedCellCount);
        if (versionName.Contains("V2"))
            return (_lastV2.Arrows.Count, _lastV2.OccupiedCellCount);
        if (versionName.Contains("V3"))
            return (_lastV3.Arrows.Count, _lastV3.OccupiedCellCount);
        if (versionName.Contains("V4"))
            return (_lastV4.Arrows.Count, _lastV4.OccupiedCellCount);
        return (_lastV5.Arrows.Count, _lastV5.OccupiedCellCount);
    }

    static bool IsSolvable(string versionName)
    {
        if (versionName.Contains("V1"))
            return IsSolvableV1(_lastV1);
        if (versionName.Contains("V2"))
            return IsSolvableV2(_lastV2);
        if (versionName.Contains("V3"))
            return IsSolvableV3(_lastV3);
        if (versionName.Contains("V4"))
            return IsSolvableV4(_lastV4);
        return IsSolvableV5(_lastV5);
    }

    static bool IsSolvableV1(V1.Board source)
    {
        if (source.Arrows.Count == 0)
            return false;
        var board = new V1.Board(source.Width, source.Height);
        foreach (var arrow in source.Arrows)
            board.AddArrow(arrow);
        while (board.Arrows.Count > 0)
        {
            Arrow toClear = null;
            foreach (var arrow in board.Arrows)
                if (board.IsClearable(arrow))
                {
                    toClear = arrow;
                    break;
                }
            if (toClear == null)
                return false;
            board.RemoveArrow(toClear);
        }
        return true;
    }

    static bool IsSolvableV2(V2.Board source)
    {
        if (source.Arrows.Count == 0)
            return false;
        var board = new V2.Board(source.Width, source.Height);
        foreach (var arrow in source.Arrows)
            board.AddArrow(arrow);
        while (board.Arrows.Count > 0)
        {
            Arrow toClear = null;
            foreach (var arrow in board.Arrows)
                if (board.IsClearable(arrow))
                {
                    toClear = arrow;
                    break;
                }
            if (toClear == null)
                return false;
            board.RemoveArrow(toClear);
        }
        return true;
    }

    static bool IsSolvableV3(V3.Board source)
    {
        if (source.Arrows.Count == 0)
            return false;
        var board = new V3.Board(source.Width, source.Height);
        foreach (var arrow in source.Arrows)
            board.AddArrow(arrow);
        while (board.Arrows.Count > 0)
        {
            Arrow toClear = null;
            foreach (var arrow in board.Arrows)
                if (board.IsClearable(arrow))
                {
                    toClear = arrow;
                    break;
                }
            if (toClear == null)
                return false;
            board.RemoveArrow(toClear);
        }
        return true;
    }

    static bool IsSolvableV4(V4.Board source)
    {
        if (source.Arrows.Count == 0)
            return false;
        var board = new V4.Board(source.Width, source.Height);
        foreach (var arrow in source.Arrows)
            board.AddArrow(arrow);
        while (board.Arrows.Count > 0)
        {
            Arrow toClear = null;
            foreach (var arrow in board.Arrows)
                if (board.IsClearable(arrow))
                {
                    toClear = arrow;
                    break;
                }
            if (toClear == null)
                return false;
            board.RemoveArrow(toClear);
        }
        return true;
    }

    static bool IsSolvableV5(V5.Board source)
    {
        if (source.Arrows.Count == 0)
            return false;
        var board = new V5.Board(source.Width, source.Height);
        foreach (var arrow in source.Arrows)
            board.AddArrow(arrow);
        while (board.Arrows.Count > 0)
        {
            Arrow toClear = null;
            foreach (var arrow in board.Arrows)
                if (board.IsClearable(arrow))
                {
                    toClear = arrow;
                    break;
                }
            if (toClear == null)
                return false;
            board.RemoveArrow(toClear);
        }
        return true;
    }
}
