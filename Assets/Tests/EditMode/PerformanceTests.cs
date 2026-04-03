using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

[TestFixture, Category("Performance"), Explicit("Run manually — these are slow benchmarks")]
public class PerformanceTests
{
    private static void Run(int w, int h, int maxLen, int seed = 0)
    {
        var board = new Board(w, h);
        var sw = Stopwatch.StartNew();
        TestBoardHelper.FillBoard(board, maxLen, new System.Random(seed));
        sw.Stop();
        UnityEngine.Debug.Log(
            $"{w}x{h}  maxLen={maxLen}  arrows={board.Arrows.Count}  cells={TotalCells(board)}  time={sw.ElapsedMilliseconds}ms"
        );
    }

    private static int TotalCells(Board board)
    {
        int n = 0;
        foreach (var a in board.Arrows)
            n += a.Cells.Count;
        return n;
    }

    [Test]
    public void Perf_50x50_VeryLong() => Run(50, 50, 50);

    [Test]
    public void Perf_50x50_Short() => Run(50, 50, 5);

    [Test]
    public void Perf_50x50_Long() => Run(50, 50, 20);

    [Test]
    public void Perf_200x200_Short() => Run(200, 200, 5);

    [Test]
    public void Perf_200x200_Long() => Run(200, 200, 20);

    [Test]
    public void Perf_200x200_VeryLong() => Run(200, 200, 50);

    [Test]
    public void Perf_200x200_MaxWidth() => Run(200, 200, 300);

    [Test]
    public void Solvability_500Seeds_10x10()
    {
        int totalArrows = 0;
        var sw = Stopwatch.StartNew();
        for (int seed = 0; seed < 500; seed++)
        {
            var board = new Board(10, 10);
            TestBoardHelper.FillBoard(board, 5, new System.Random(seed));
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
            TestBoardHelper.FillBoard(board, 10, new System.Random(seed));
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
            TestBoardHelper.FillBoard(board, 20, new System.Random(seed));
            totalArrows += board.Arrows.Count;
            AssertFullyClearable(board, seed);
        }
        sw.Stop();
        UnityEngine.Debug.Log(
            $"20 seeds 50x50: {totalArrows} total arrows, all solvable, {sw.ElapsedMilliseconds}ms"
        );
    }

    // ── Candidate depletion curve profiling ──────────────────────────────

    /// <summary>
    /// Profiles candidate depletion vs wall time (per-arrow granularity).
    /// Outputs a CSV to the console and 5%-interval buckets for visual analysis.
    /// Run manually and inspect the Unity console output.
    /// </summary>
    [Test]
    public void ProfileDepletionCurve_DumpData()
    {
        var configs = new[] { (100, 100, 50, 1), (200, 200, 50, 1) };

        foreach (var (w, h, maxLen, seedCount) in configs)
        {
            for (int s = 0; s < seedCount; s++)
            {
                var board = new Board(w, h);
                var sw = Stopwatch.StartNew();
                var gen = BoardGeneration.FillBoardIncremental(board, maxLen, new Random(s));

                int initial = 0;
                var samples = new List<(double ms, float raw, int arrowCount, int cellCount)>();
                int prevArrows = 0;
                var lengthCounts = new Dictionary<int, int>(); // length -> count

                while (gen.MoveNext())
                {
                    if (initial == 0)
                        initial = board.InitialCandidateCount;
                    float raw =
                        initial > 0 ? 1f - (float)board.RemainingCandidateCount / initial : 0f;

                    // Track arrow lengths as they're placed
                    int curArrows = board.Arrows.Count;
                    for (int a = prevArrows; a < curArrows; a++)
                    {
                        int len = board.Arrows[a].Cells.Count;
                        if (!lengthCounts.ContainsKey(len))
                            lengthCounts[len] = 0;
                        lengthCounts[len]++;
                    }
                    prevArrows = curArrows;

                    samples.Add(
                        (sw.Elapsed.TotalMilliseconds, raw, curArrows, board.OccupiedCellCount)
                    );
                }
                sw.Stop();
                int finalArrows = board.Arrows.Count;
                int finalCells = board.OccupiedCellCount;
                int totalCells = w * h;
                samples.Add((sw.Elapsed.TotalMilliseconds, 1f, finalArrows, finalCells));

                double totalMs = samples[samples.Count - 1].ms;

                // 5%-interval buckets
                int bucketCount = 20;
                var bucketRawSum = new double[bucketCount + 1];
                var bucketArrowSum = new double[bucketCount + 1];
                var bucketCellSum = new double[bucketCount + 1];
                var bucketN = new int[bucketCount + 1];
                foreach (var (ms, raw, ac, cc) in samples)
                {
                    int b = Math.Min((int)(ms / totalMs * bucketCount), bucketCount);
                    bucketRawSum[b] += raw;
                    bucketArrowSum[b] += (float)ac / finalArrows;
                    bucketCellSum[b] += (float)cc / finalCells;
                    bucketN[b]++;
                }

                string header =
                    $"\n=== {w}x{h} seed={s}: {finalArrows} arrows, {finalCells}/{totalCells} cells ({100f * finalCells / totalCells:F1}%), {totalMs:F0}ms ===";
                string rawLine = "time% -> candidateDepletion:  ";
                string arrowLine = "time% -> arrowProgress:       ";
                string cellLine = "time% -> cellProgress:        ";
                for (int i = 0; i <= bucketCount; i++)
                {
                    double avgRaw = bucketN[i] > 0 ? bucketRawSum[i] / bucketN[i] : 0;
                    double avgArr = bucketN[i] > 0 ? bucketArrowSum[i] / bucketN[i] : 0;
                    double avgCell = bucketN[i] > 0 ? bucketCellSum[i] / bucketN[i] : 0;
                    rawLine += $"{i * 5}%={avgRaw:F4}  ";
                    arrowLine += $"{i * 5}%={avgArr:F4}  ";
                    cellLine += $"{i * 5}%={avgCell:F4}  ";
                }

                // Length distribution
                var lengths = new List<int>(lengthCounts.Keys);
                lengths.Sort();
                string lengthDist = "Arrow length distribution: ";
                float avgLength = 0f;
                foreach (int len in lengths)
                {
                    lengthDist += $"{len}cells={lengthCounts[len]}  ";
                    avgLength += len * lengthCounts[len];
                }
                avgLength /= finalArrows;
                lengthDist += $"(avg={avgLength:F1})";

                // CSV with all signals
                int step = Math.Max(1, samples.Count / 100);
                string csv = "csv: ms,timeNorm,candidateDepletion,arrowProgress,cellProgress";
                for (int i = 0; i < samples.Count; i += step)
                {
                    var (ms, raw, ac, cc) = samples[i];
                    csv +=
                        $"\n{ms:F1},{ms / totalMs:F6},{raw:F6},{(float)ac / finalArrows:F6},{(float)cc / finalCells:F6}";
                }

                UnityEngine.Debug.Log(
                    header
                        + "\n"
                        + rawLine
                        + "\n"
                        + arrowLine
                        + "\n"
                        + cellLine
                        + "\n"
                        + lengthDist
                        + "\n"
                        + csv
                );
            }
        }

        Assert.Pass("Data dumped — inspect console output");
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
