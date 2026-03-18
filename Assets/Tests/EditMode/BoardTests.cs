using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class BoardTests
{
    [Test]
    public void Contains_CellInsideBounds_ReturnsTrue()
    {
        var board = new Board(5, 5);
        Assert.That(board.Contains(new Cell(0, 0)), Is.True);
        Assert.That(board.Contains(new Cell(4, 4)), Is.True);
        Assert.That(board.Contains(new Cell(2, 3)), Is.True);
    }

    [Test]
    public void Contains_CellOutsideBounds_ReturnsFalse()
    {
        var board = new Board(5, 5);
        Assert.That(board.Contains(new Cell(-1, 0)), Is.False);
        Assert.That(board.Contains(new Cell(0, -1)), Is.False);
        Assert.That(board.Contains(new Cell(5, 0)), Is.False);
        Assert.That(board.Contains(new Cell(0, 5)), Is.False);
    }

    [Test]
    public void Arrows_ReflectsAddedArrows()
    {
        var board = new Board(4, 4);
        var arrow = new Arrow(new Cell[] { new(0, 1), new(0, 0) });
        board.AddArrow(arrow);
        Assert.That(board.Arrows, Has.Count.EqualTo(1));
        Assert.That(board.Arrows[0], Is.SameAs(arrow));
    }

    [Test]
    public void Arrows_ReflectsRemovedArrows()
    {
        var board = new Board(4, 4);
        var arrow = new Arrow(new Cell[] { new(0, 1), new(0, 0) });
        board.AddArrow(arrow);
        board.RemoveArrow(arrow);
        Assert.That(board.Arrows, Is.Empty);
    }

    [Test]
    public void Arrows_IsReadOnlyList() =>
        Assert.That(new Board(3, 3).Arrows, Is.InstanceOf<IReadOnlyList<Arrow>>());

    // --- GetArrowAt ---

    [Test]
    public void GetArrowAt_ReturnsArrowAfterAdd()
    {
        var board = new Board(4, 4);
        var arrow = new Arrow(new Cell[] { new(1, 1), new(1, 0) });
        board.AddArrow(arrow);
        Assert.That(board.GetArrowAt(new Cell(1, 1)), Is.SameAs(arrow));
        Assert.That(board.GetArrowAt(new Cell(1, 0)), Is.SameAs(arrow));
    }

    [Test]
    public void GetArrowAt_ReturnsNullForEmptyCell()
    {
        var board = new Board(4, 4);
        Assert.That(board.GetArrowAt(new Cell(2, 2)), Is.Null);
    }

    [Test]
    public void GetArrowAt_ReturnsNullAfterRemove()
    {
        var board = new Board(4, 4);
        var arrow = new Arrow(new Cell[] { new(1, 1), new(1, 0) });
        board.AddArrow(arrow);
        board.RemoveArrow(arrow);
        Assert.That(board.GetArrowAt(new Cell(1, 1)), Is.Null);
        Assert.That(board.GetArrowAt(new Cell(1, 0)), Is.Null);
    }

    [Test]
    public void GetArrowAt_ReturnsNullForOutOfBoundsCell()
    {
        var board = new Board(4, 4);
        Assert.That(board.GetArrowAt(new Cell(-1, 0)), Is.Null);
        Assert.That(board.GetArrowAt(new Cell(4, 0)), Is.Null);
    }

    // --- IsClearable ---

    [Test]
    public void IsClearable_EmptyRay_ReturnsTrue()
    {
        // Arrow facing Up at (2,3); ray fires upward — no arrows in the way
        var board = new Board(5, 5);
        var arrow = new Arrow(new Cell[] { new(2, 3), new(2, 2) }); // HeadDirection = Up
        board.AddArrow(arrow);
        Assert.That(board.IsClearable(arrow), Is.True);
    }

    [Test]
    public void IsClearable_BlockedRay_ReturnsFalse()
    {
        // Arrow A faces Up at (2,1); blocker at (2,3)
        var board = new Board(5, 5);
        var arrowA = new Arrow(new Cell[] { new(2, 1), new(2, 0) }); // HeadDirection = Up
        var blocker = new Arrow(new Cell[] { new(2, 3), new(1, 3) }); // occupies (2,3)
        board.AddArrow(arrowA);
        board.AddArrow(blocker);
        Assert.That(board.IsClearable(arrowA), Is.False);
    }

    [Test]
    public void IsClearable_HeadAtBoardEdge_ReturnsTrue()
    {
        // Arrow head is on the top edge; ray immediately exits the board
        var board = new Board(5, 5);
        var arrow = new Arrow(new Cell[] { new(2, 4), new(2, 3) }); // HeadDirection = Up, head at y=4 (top)
        board.AddArrow(arrow);
        Assert.That(board.IsClearable(arrow), Is.True);
    }

    [Test]
    public void IsClearable_OwnCellsDoNotBlockSelf()
    {
        // Sanity: an arrow's body is behind its head, so it never appears in its own forward ray.
        // This test confirms IsClearable returns true even if we verify own-cell exclusion logic.
        var board = new Board(5, 5);
        // Right-facing: head at (3,2), body at (2,2),(1,2) — ray fires rightward (x>3), board edge at x=4
        var arrow = new Arrow(new Cell[] { new(3, 2), new(2, 2), new(1, 2) });
        board.AddArrow(arrow);
        Assert.That(board.IsClearable(arrow), Is.True);
    }

    // --- Candidate counts ---

    [Test]
    public void InitialCandidateCount_IsZeroBeforeInitialization()
    {
        var board = new Board(5, 5);
        Assert.AreEqual(0, board.InitialCandidateCount);
    }

    [Test]
    public void InitialCandidateCount_IsPositiveAfterInitialization()
    {
        var board = new Board(5, 5);
        board.InitializeForGeneration();
        Assert.Greater(board.InitialCandidateCount, 0);
    }

    [Test]
    public void RemainingCandidateCount_EqualsInitialBeforeAnyArrows()
    {
        var board = new Board(5, 5);
        board.InitializeForGeneration();
        Assert.AreEqual(board.InitialCandidateCount, board.RemainingCandidateCount);
    }

    [Test]
    public void RemainingCandidateCount_DecreasesAfterAddArrow()
    {
        var board = new Board(5, 5);
        board.InitializeForGeneration();
        int before = board.RemainingCandidateCount;
        var arrow = new Arrow(new Cell[] { new(2, 2), new(2, 1) });
        board.AddArrow(arrow);
        Assert.Less(board.RemainingCandidateCount, before);
    }
}
