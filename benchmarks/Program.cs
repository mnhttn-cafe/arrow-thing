using System;
using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        // Warmup
        RunBenchmark(5, 5, 3, 1, "warmup", verbose: false);

        Console.WriteLine("=== Board Generation Benchmark ===\n");

        // Correctness: solvability check
        Console.WriteLine("--- Solvability Check ---");
        int solvableCount = 0;
        for (int seed = 0; seed < 50; seed++)
        {
            var board = new Board(8, 8);
            FillBoard(board, 5, new Random(seed));
            if (IsFullySolvable(board))
                solvableCount++;
            else
                Console.WriteLine($"  FAIL: seed {seed} produced unsolvable board!");
        }
        Console.WriteLine($"  {solvableCount}/50 boards solvable (8x8, maxLen=5)\n");

        // Determinism check
        Console.WriteLine("--- Determinism Check ---");
        var b1 = new Board(10, 10);
        var b2 = new Board(10, 10);
        FillBoard(b1, 5, new Random(42));
        FillBoard(b2, 5, new Random(42));
        bool deterministic = b1.Arrows.Count == b2.Arrows.Count;
        if (deterministic)
        {
            for (int i = 0; i < b1.Arrows.Count; i++)
            {
                if (b1.Arrows[i].Cells.Count != b2.Arrows[i].Cells.Count)
                {
                    deterministic = false;
                    break;
                }
                for (int j = 0; j < b1.Arrows[i].Cells.Count; j++)
                {
                    if (b1.Arrows[i].Cells[j] != b2.Arrows[i].Cells[j])
                    {
                        deterministic = false;
                        break;
                    }
                }
            }
        }
        Console.WriteLine($"  Same seed produces identical board: {deterministic}\n");

        // Performance benchmarks
        Console.WriteLine("--- Performance ---");
        RunBenchmark(10, 10, 5, 100, "100x 10x10 maxLen=5");
        RunBenchmark(20, 20, 10, 10, "10x 20x20 maxLen=10");
        RunBenchmark(50, 50, 20, 3, "3x 50x50 maxLen=20");
        RunBenchmark(50, 50, 50, 3, "3x 50x50 maxLen=50");
        RunBenchmark(100, 100, 50, 1, "1x 100x100 maxLen=50");
        RunBenchmark(200, 200, 50, 1, "1x 200x200 maxLen=50");

        // Solvability stress test
        Console.WriteLine("\n--- Solvability Stress ---");
        RunSolvabilityStress(10, 10, 5, 500);
        RunSolvabilityStress(20, 20, 10, 100);
        RunSolvabilityStress(50, 50, 20, 20);
    }

    static void RunBenchmark(int w, int h, int maxLen, int iterations, string label, bool verbose = true)
    {
        var sw = Stopwatch.StartNew();
        int totalArrows = 0;
        int totalCells = 0;
        for (int i = 0; i < iterations; i++)
        {
            var board = new Board(w, h);
            FillBoard(board, maxLen, new Random(i));
            totalArrows += board.Arrows.Count;
            totalCells += board.OccupiedCellCount;
        }
        sw.Stop();
        if (verbose)
        {
            double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
            double avgArrows = (double)totalArrows / iterations;
            double avgCells = (double)totalCells / iterations;
            double fillRate = avgCells / (w * h) * 100;
            Console.WriteLine($"  {label}: {sw.ElapsedMilliseconds}ms total, {avgMs:F1}ms/board, {avgArrows:F0} arrows, {fillRate:F0}% fill");
        }
    }

    static void RunSolvabilityStress(int w, int h, int maxLen, int seeds)
    {
        var sw = Stopwatch.StartNew();
        int failures = 0;
        for (int seed = 0; seed < seeds; seed++)
        {
            var board = new Board(w, h);
            FillBoard(board, maxLen, new Random(seed));
            if (!IsFullySolvable(board))
                failures++;
        }
        sw.Stop();
        Console.WriteLine($"  {seeds}x {w}x{h} maxLen={maxLen}: {(failures == 0 ? "ALL SOLVABLE" : $"{failures} FAILURES")}, {sw.ElapsedMilliseconds}ms");
    }

    static void FillBoard(Board board, int maxLen, Random random)
    {
        var gen = BoardGeneration.FillBoardIncremental(board, maxLen, random);
        while (gen.MoveNext()) { }
    }

    static bool IsFullySolvable(Board board)
    {
        if (board.Arrows.Count == 0) return false;
        // Make a copy to test solvability
        var testBoard = new Board(board.Width, board.Height);
        foreach (var arrow in board.Arrows)
            testBoard.AddArrow(arrow);

        while (testBoard.Arrows.Count > 0)
        {
            Arrow toClear = null;
            foreach (var arrow in testBoard.Arrows)
            {
                if (testBoard.IsClearable(arrow))
                {
                    toClear = arrow;
                    break;
                }
            }
            if (toClear == null) return false;
            testBoard.RemoveArrow(toClear);
        }
        return true;
    }
}
