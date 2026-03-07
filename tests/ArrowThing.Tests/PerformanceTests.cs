using System;
using System.Diagnostics;
using NUnit.Framework;

[TestFixture]
public class PerformanceTests
{
    private static void Run(int w, int h, int minLen, int maxLen, int deadEndLimit, int seed = 0)
    {
        var board = new Board(w, h);
        var sw = Stopwatch.StartNew();
        BoardGeneration.FillBoard(board, minLen, maxLen, new Random(seed), deadEndLimit);
        sw.Stop();
        TestContext.Out.WriteLine(
            $"{w}x{h}  len=[{minLen},{maxLen}]  deadEnds={deadEndLimit,-5}  arrows={board.Arrows.Count}  cells={TotalCells(board)}  time={sw.ElapsedMilliseconds}ms");
    }

    private static int TotalCells(Board board)
    {
        int n = 0;
        foreach (var a in board.Arrows) n += a.Cells.Count;
        return n;
    }

    // Sweep dead-end limits on the expensive 50x50 VeryLong case
    [Test] public void Perf_50x50_VeryLong_DeadEnd10()  => Run(50, 50, 10, 50, 10);
    [Test] public void Perf_50x50_VeryLong_DeadEnd20()  => Run(50, 50, 10, 50, 20);
    [Test] public void Perf_50x50_VeryLong_DeadEnd50()  => Run(50, 50, 10, 50, 50);
    [Test] public void Perf_50x50_VeryLong_DeadEnd100() => Run(50, 50, 10, 50, 100);

    // Baseline short/long at a sensible default (10) for comparison
    [Test] public void Perf_50x50_Short()     => Run(50,  50,  2,  5,  10);
    [Test] public void Perf_50x50_Long()      => Run(50,  50,  5,  20, 10);
    [Test] public void Perf_200x200_Short()   => Run(200, 200, 2,   5,   10);
    [Test] public void Perf_200x200_Long()    => Run(200, 200, 5,   20,  10);
    [Test] public void Perf_200x200_VeryLong() => Run(200, 200, 10, 50,  10);
    [Test] public void Perf_200x200_MaxWidth() => Run(200, 200, 10, 300, 10);
}
