using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

[TestFixture]
public class GenerationTests
{
    // Mirrors the private BoardGeneration.IsInRay for postcondition checks.
    private static bool IsInRay(Cell target, Cell head, Arrow.Direction direction) => direction switch
    {
        Arrow.Direction.Up => target.X == head.X && target.Y > head.Y,
        Arrow.Direction.Down => target.X == head.X && target.Y < head.Y,
        Arrow.Direction.Right => target.Y == head.Y && target.X > head.X,
        Arrow.Direction.Left => target.Y == head.Y && target.X < head.X,
        _ => false
    };

    [Test]
    public void FillBoard_SameSeed_ProducesIdenticalBoards()
    {
        var board1 = new Board(6, 6);
        var board2 = new Board(6, 6);
        BoardGeneration.FillBoard(board1, 2, 5, new Random(42));
        BoardGeneration.FillBoard(board2, 2, 5, new Random(42));

        Assert.That(board1.Arrows.Count, Is.EqualTo(board2.Arrows.Count));
        for (int i = 0; i < board1.Arrows.Count; i++)
            Assert.That(board1.Arrows[i].Cells, Is.EqualTo(board2.Arrows[i].Cells));
    }

    [Test]
    public void FillBoard_NoCellsOverlap()
    {
        var board = new Board(6, 6);
        BoardGeneration.FillBoard(board, 2, 5, new Random(7));

        var seen = new HashSet<Cell>();
        foreach (var arrow in board.Arrows)
            foreach (var cell in arrow.Cells)
                Assert.That(seen.Add(cell), Is.True,
                    $"Cell ({cell.X},{cell.Y}) is shared by multiple arrows.");
    }

    [Test]
    public void FillBoard_AllCellsWithinBounds()
    {
        var board = new Board(5, 7);
        BoardGeneration.FillBoard(board, 2, 4, new Random(13));

        foreach (var arrow in board.Arrows)
            foreach (var cell in arrow.Cells)
                Assert.That(board.Contains(cell), Is.True,
                    $"Cell ({cell.X},{cell.Y}) is outside board bounds.");
    }

    [Test]
    public void FillBoard_AllArrowsRespectMinLength()
    {
        const int minLength = 3;
        var board = new Board(6, 6);
        BoardGeneration.FillBoard(board, minLength, 6, new Random(99));

        foreach (var arrow in board.Arrows)
            Assert.That(arrow.Cells.Count, Is.GreaterThanOrEqualTo(minLength),
                $"Arrow has only {arrow.Cells.Count} cells, expected >= {minLength}.");
    }

    [Test]
    public void FillBoard_NoTailCellInOwnRay()
    {
        var board = new Board(6, 6);
        BoardGeneration.FillBoard(board, 2, 6, new Random(55));

        foreach (var arrow in board.Arrows)
            for (int i = 1; i < arrow.Cells.Count; i++)
                Assert.That(IsInRay(arrow.Cells[i], arrow.HeadCell, arrow.HeadDirection), Is.False,
                    $"Tail cell ({arrow.Cells[i].X},{arrow.Cells[i].Y}) lies in arrow's own ray.");
    }

    [Test]
    public void GenerateArrows_ExactAmountRequested_ReturnsTrue()
    {
        var board = new Board(8, 8);
        bool success = BoardGeneration.GenerateArrows(board, 2, 3, 4, new Random(1), out int created);
        Assert.That(success, Is.True);
        Assert.That(created, Is.EqualTo(4));
        Assert.That(board.Arrows.Count, Is.EqualTo(4));
    }

    [Test]
    public void GenerateArrows_ExternalMutation_Throws()
    {
        var board = new Board(6, 6);
        // Seed the cache by running generation.
        BoardGeneration.GenerateArrows(board, 2, 3, 1, new Random(0), out _);
        // Mutate the board outside of BoardGeneration.
        board.AddArrow(new Arrow([new(0, 1), new(0, 0)]));
        // Next generation call should detect the desync.
        Assert.Throws<InvalidOperationException>(() =>
            BoardGeneration.GenerateArrows(board, 2, 3, 1, new Random(0), out _));
    }

    [Test]
    public void FillBoard_100Iterations_CompletesUnder5Seconds()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            var board = new Board(10, 10);
            BoardGeneration.FillBoard(board, 2, 5, new Random(i));
        }
        sw.Stop();
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5000),
            $"100x FillBoard(10×10) took {sw.ElapsedMilliseconds}ms, expected < 5000ms.");
    }
}
