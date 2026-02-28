using System.Diagnostics;
using NUnit.Framework;

namespace ArrowThing.Model.Tests;

[TestFixture]
public sealed class BoardGeneratorModelTests
{
    [Test]
    public void FillBoard_ReturnsNumberOfPlacedArrows()
    {
        BoardModel board = new(2, 1);
        BoardGenerator generator = new(seed: 1234);

        int placed = generator.FillBoard(board, arrowCount: 2, minLength: 2, maxLength: 2);

        Assert.That(placed, Is.EqualTo(1));
        Assert.That(board.Arrows, Has.Count.EqualTo(1));
    }

    [Test]
    public void TryGenerateSingleArrow_ReturnsTrueWhenPlacementExists()
    {
        BoardModel board = new(2, 1);
        BoardGenerator generator = new(seed: 7);

        bool success = generator.TryGenerateSingleArrow(board, minLength: 2, maxLength: 2, out ArrowModel arrow);

        Assert.That(success, Is.True);
        Assert.That(arrow, Is.Not.Null);
        Assert.That(arrow!.Cells, Has.Count.EqualTo(2));
        Assert.That(board.CanPlaceArrow(arrow), Is.True);
        Assert.That(board.Arrows, Is.Empty);
    }

    [Test]
    public void TryGenerateSingleArrow_ReturnsFalseWhenNoPlacementExists()
    {
        BoardModel board = new(2, 1);
        Assert.That(board.TryAddArrow(CreateArrowFacingRight(1, 0)), Is.True);
        BoardGenerator generator = new(seed: 7);

        bool success = generator.TryGenerateSingleArrow(board, minLength: 2, maxLength: 2, out ArrowModel arrow);

        Assert.That(success, Is.False);
        Assert.That(arrow, Is.Null);
    }

    [Test]
    public void Methods_ValidateLengthRangeArguments()
    {
        BoardModel board = new(3, 3);
        BoardGenerator generator = new(seed: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            generator.FillBoard(board, arrowCount: 1, minLength: 1, maxLength: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            generator.FillBoard(board, arrowCount: 1, minLength: 3, maxLength: 2));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            generator.TryGenerateSingleArrow(board, minLength: 1, maxLength: 2, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            generator.TryGenerateSingleArrow(board, minLength: 3, maxLength: 2, out _));
    }

    [Test]
    [Explicit("Performance test; run intentionally.")]
    [Category("Performance")]
    [TestCase(8, 8, 2, 2, 300, 2500)]
    [TestCase(12, 12, 2, 24, 200, 3500)]
    [TestCase(16, 16, 2, 20, 120, 5000)]
    [TestCase(20, 20, 2, 40, 60, 10_000)]
    [TestCase(40, 40, 2, 100, 30, 25_000)]
    [TestCase(80, 80, 2, 500, 1, 100_000)]
    public void TryGenerateSingleArrow_Performance_AcrossLengthRanges(
        int width,
        int height,
        int minLength,
        int maxLength,
        int iterations,
        int maxDurationMs)
    {
        BoardGenerator generator = new(seed: 42);
        int successfulPlacements = 0;
        Stopwatch timer = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            BoardModel board = new(width, height);
            bool success = generator.TryGenerateSingleArrow(board, minLength, maxLength, out ArrowModel arrow);

            if (success)
            {
                successfulPlacements++;
                Assert.That(arrow, Is.Not.Null);
                Assert.That(arrow!.Cells.Count, Is.InRange(minLength, maxLength));
            }
            else
            {
                Assert.That(arrow, Is.Null);
            }
        }

        timer.Stop();

        Assert.That(successfulPlacements, Is.GreaterThan(0));
        Assert.That(
            timer.ElapsedMilliseconds,
            Is.LessThanOrEqualTo(maxDurationMs),
            $"TryGenerateSingleArrow took {timer.ElapsedMilliseconds} ms for {iterations} iterations.");
    }

    [Test]
    [Explicit("Performance test; run intentionally.")]
    [Category("Performance")]
    [TestCase(8, 8, 2, 3, 40, 3000)]
    [TestCase(12, 12, 2, 6, 25, 4000)]
    [TestCase(20, 20, 3, 10, 10, 5000)]
    [TestCase(40, 40, 2, 20, 4, 20_000)]
    [TestCase(80, 80, 2, 40, 2, 60_000)]
    public void FillBoard_Performance_AcrossBoardSizesAndLengthRanges(
        int width,
        int height,
        int minLength,
        int maxLength,
        int runs,
        int maxDurationMs)
    {
        const double fillCaseMultiplier = 2d;
        double averageLength = (minLength + maxLength) / 2d;
        int targetArrowCount = Math.Max(1, (int)Math.Round((width * height / averageLength) * fillCaseMultiplier));

        int totalPlaced = 0;
        Stopwatch timer = Stopwatch.StartNew();

        for (int i = 0; i < runs; i++)
        {
            BoardModel board = new(width, height);
            BoardGenerator generator = new(seed: 1000 + i);

            int placed = generator.FillBoard(board, targetArrowCount, minLength, maxLength);

            Assert.That(placed, Is.InRange(0, targetArrowCount));
            Assert.That(board.Arrows, Has.Count.EqualTo(placed));
            foreach (ArrowModel arrow in board.Arrows)
            {
                Assert.That(arrow.Cells.Count, Is.InRange(minLength, maxLength));
            }

            totalPlaced += placed;
        }

        timer.Stop();

        Assert.That(totalPlaced, Is.GreaterThan(0));
        Assert.That(
            timer.ElapsedMilliseconds,
            Is.LessThanOrEqualTo(maxDurationMs),
            $"FillBoard took {timer.ElapsedMilliseconds} ms for {runs} runs (target={targetArrowCount}).");
    }

    private static ArrowModel CreateArrowFacingRight(int headX, int headY)
    {
        BoardCell head = new(headX, headY);
        BoardCell next = new(headX - 1, headY);
        return new ArrowModel(new[] { head, next });
    }
}
