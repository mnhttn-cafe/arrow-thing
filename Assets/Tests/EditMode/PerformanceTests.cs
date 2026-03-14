using System.Diagnostics;
using NUnit.Framework;

[TestFixture, Category("Performance"), Explicit("Run manually — these are slow benchmarks")]
public class PerformanceTests
{
    private static void Run(int w, int h, int minLen, int maxLen, int deadEndLimit, int seed = 0)
    {
        var board = new Board(w, h);
        var sw = Stopwatch.StartNew();
        BoardGeneration.FillBoard(board, minLen, maxLen, new System.Random(seed), deadEndLimit);
        sw.Stop();
        UnityEngine.Debug.Log(
            $"{w}x{h}  len=[{minLen},{maxLen}]  deadEnds={deadEndLimit, -5}  arrows={board.Arrows.Count}  cells={TotalCells(board)}  time={sw.ElapsedMilliseconds}ms"
        );
    }

    private static int TotalCells(Board board)
    {
        int n = 0;
        foreach (var a in board.Arrows)
            n += a.Cells.Count;
        return n;
    }

    // Sweep dead-end limits on the expensive 50x50 case
    [Test]
    public void Perf_50x50_VeryLong_DeadEnd10() => Run(50, 50, 10, 50, 10);

    [Test]
    public void Perf_50x50_VeryLong_DeadEnd20() => Run(50, 50, 10, 50, 20);

    [Test]
    public void Perf_50x50_VeryLong_DeadEnd50() => Run(50, 50, 10, 50, 50);

    [Test]
    public void Perf_50x50_VeryLong_DeadEnd100() => Run(50, 50, 10, 50, 100);

    // Baseline short/long at a sensible default (10) for comparison
    [Test]
    public void Perf_50x50_Short() => Run(50, 50, 2, 5, 10);

    [Test]
    public void Perf_50x50_Long() => Run(50, 50, 5, 20, 10);

    [Test]
    public void Perf_200x200_Short() => Run(200, 200, 2, 5, 10);

    [Test]
    public void Perf_200x200_Long() => Run(200, 200, 5, 20, 10);

    [Test]
    public void Perf_200x200_VeryLong() => Run(200, 200, 10, 50, 10);

    [Test]
    public void Perf_200x200_MaxWidth() => Run(200, 200, 10, 300, 10);

    [Test]
    public void Solvability_500Seeds_10x10()
    {
        int totalArrows = 0;
        var sw = Stopwatch.StartNew();
        for (int seed = 0; seed < 500; seed++)
        {
            var board = new Board(10, 10);
            BoardGeneration.FillBoard(board, 2, 5, new System.Random(seed));
            totalArrows += board.Arrows.Count;
            AssertFullyClearable(board, seed);
        }
        sw.Stop();
        UnityEngine.Debug.Log(
            $"500 seeds 10x10: {totalArrows} total arrows, all solvable, {sw.ElapsedMilliseconds}ms"
        );
    }

    [Test]
    public void Solvability_100Seeds_20x20()
    {
        int totalArrows = 0;
        var sw = Stopwatch.StartNew();
        for (int seed = 0; seed < 100; seed++)
        {
            var board = new Board(20, 20);
            BoardGeneration.FillBoard(board, 2, 10, new System.Random(seed));
            totalArrows += board.Arrows.Count;
            AssertFullyClearable(board, seed);
        }
        sw.Stop();
        UnityEngine.Debug.Log(
            $"100 seeds 20x20: {totalArrows} total arrows, all solvable, {sw.ElapsedMilliseconds}ms"
        );
    }

    [Test]
    public void Solvability_20Seeds_50x50()
    {
        int totalArrows = 0;
        var sw = Stopwatch.StartNew();
        for (int seed = 0; seed < 20; seed++)
        {
            var board = new Board(50, 50);
            BoardGeneration.FillBoard(board, 2, 20, new System.Random(seed));
            totalArrows += board.Arrows.Count;
            AssertFullyClearable(board, seed);
        }
        sw.Stop();
        UnityEngine.Debug.Log(
            $"20 seeds 50x50: {totalArrows} total arrows, all solvable, {sw.ElapsedMilliseconds}ms"
        );
    }

    private static void AssertFullyClearable(Board board, int seed)
    {
        int initial = board.Arrows.Count;
        Assert.That(initial, Is.GreaterThan(0), $"Seed {seed}: no arrows generated.");

        int cleared = 0;
        while (board.Arrows.Count > 0)
        {
            Arrow toClear = null;
            foreach (var arrow in board.Arrows)
            {
                if (board.IsClearable(arrow))
                {
                    toClear = arrow;
                    break;
                }
            }
            Assert.That(
                toClear,
                Is.Not.Null,
                $"Seed {seed}: deadlock with {board.Arrows.Count}/{initial} arrows remaining."
            );
            board.RemoveArrow(toClear);
            cleared++;
        }
    }
}
