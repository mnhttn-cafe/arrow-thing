using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace ArrowThing.Model.Tests;

[TestFixture]
public sealed class BoardModelTests
{
    [Test]
    public void Constructor_SetsDimensions_AndArrowsStartsEmpty()
    {
        Board board = new(4, 6);

        Assert.That(board.Width, Is.EqualTo(4));
        Assert.That(board.Height, Is.EqualTo(6));
        Assert.That(board.Arrows, Is.Empty);
    }

    [Test]
    public void Contains_ReturnsExpectedForInBoundsAndOutOfBounds()
    {
        Board board = new(3, 2);

        Assert.That(board.Contains(new BoardCell(0, 0)), Is.True);
        Assert.That(board.Contains(new BoardCell(2, 1)), Is.True);
        Assert.That(board.Contains(new BoardCell(-1, 0)), Is.False);
        Assert.That(board.Contains(new BoardCell(0, -1)), Is.False);
        Assert.That(board.Contains(new BoardCell(3, 0)), Is.False);
        Assert.That(board.Contains(new BoardCell(0, 2)), Is.False);
    }

    [Test]
    public void IsOccupied_ReturnsFalseForEmptyCell_AndTrueAfterAdd()
    {
        Board board = new(5, 5);
        ArrowModel arrow = CreateArrowFacingRight(1, 1);

        Assert.That(board.IsOccupied(new BoardCell(1, 1)), Is.False);
        Assert.That(board.TryAddArrow(arrow), Is.True);
        Assert.That(board.IsOccupied(new BoardCell(1, 1)), Is.True);
        Assert.That(board.IsOccupied(new BoardCell(0, 1)), Is.True);
    }

    [Test]
    public void TryAddArrow_AddsArrowWhenPlacementIsValid()
    {
        Board board = new(5, 5);
        ArrowModel arrow = CreateArrowFacingRight(1, 1);

        bool added = board.TryAddArrow(arrow);

        Assert.That(added, Is.True);
        Assert.That(board.Arrows, Has.Count.EqualTo(1));
        Assert.That(board.Arrows[0], Is.SameAs(arrow));
        Assert.That(board.IsOccupied(new BoardCell(1, 1)), Is.True);
        Assert.That(board.IsOccupied(new BoardCell(0, 1)), Is.True);
    }

    [Test]
    public void TryAddArrow_ReturnsFalse_WhenPlacementIsInvalid()
    {
        Board board = new(4, 4);
        ArrowModel outOfBounds = CreateArrowFacingRight(-1, 1);

        bool added = board.TryAddArrow(outOfBounds);

        Assert.That(added, Is.False);
        Assert.That(board.Arrows, Is.Empty);
    }

    [Test]
    public void TryRemoveArrow_ReturnsFalse_WhenArrowWasNotAdded()
    {
        Board board = new(5, 5);
        ArrowModel arrow = CreateArrowFacingRight(1, 1);

        bool removed = board.TryRemoveArrow(arrow);

        Assert.That(removed, Is.False);
    }

    [Test]
    public void TryRemoveArrow_ReturnsFalse_WhenArrowIsBlockedInHeadDirection()
    {
        Board board = new(6, 4);
        ArrowModel blocker = CreateArrowFacingRight(3, 1);
        ArrowModel blocked = CreateArrowFacingRight(1, 1);

        Assert.That(board.TryAddArrow(blocker), Is.True);
        Assert.That(board.TryAddArrow(blocked), Is.True);

        bool removed = board.TryRemoveArrow(blocked);

        Assert.That(removed, Is.False);
        Assert.That(board.Arrows, Has.Count.EqualTo(2));
    }

    [Test]
    public void TryRemoveArrow_RemovesArrowWhenAllowed()
    {
        Board board = new(5, 5);
        ArrowModel arrow = CreateArrowFacingRight(1, 1);
        Assert.That(board.TryAddArrow(arrow), Is.True);

        bool removed = board.TryRemoveArrow(arrow);

        Assert.That(removed, Is.True);
        Assert.That(board.Arrows, Is.Empty);
        Assert.That(board.IsOccupied(new BoardCell(1, 1)), Is.False);
        Assert.That(board.IsOccupied(new BoardCell(0, 1)), Is.False);
    }

    [Test]
    public void CanPlaceArrow_ReturnsFalse_ForNullAndEmptyArrow()
    {
        Board board = new(5, 5);
        ArrowModel emptyArrow = CreateArrowWithZeroCells();

        Assert.That(board.CanPlaceArrow(null!), Is.False);
        Assert.That(board.CanPlaceArrow(emptyArrow), Is.False);
    }

    [Test]
    public void CanPlaceArrow_ReturnsFalse_WhenArrowHasOutOfBoundsCell()
    {
        Board board = new(3, 3);
        ArrowModel outOfBounds = CreateArrowFacingRight(-1, 0);

        Assert.That(board.CanPlaceArrow(outOfBounds), Is.False);
    }

    [Test]
    public void CanPlaceArrow_ReturnsFalse_WhenAnyCellOverlapsExistingOccupancy()
    {
        Board board = new(6, 3);
        ArrowModel existing = CreateArrowFacingRight(3, 1);
        ArrowModel overlapping = CreateArrow(new BoardCell(2, 1), new BoardCell(1, 1));
        Assert.That(board.TryAddArrow(existing), Is.True);

        Assert.That(board.CanPlaceArrow(overlapping), Is.False);
    }

    [Test]
    public void CanPlaceArrow_ReturnsFalse_WhenNewArrowWouldSelfCycleInHeadDirection()
    {
        Board board = new(6, 4);
        ArrowModel selfCycling = CreateArrow(
            new BoardCell(1, 1),
            new BoardCell(0, 1),
            new BoardCell(2, 1));

        Assert.That(board.CanPlaceArrow(selfCycling), Is.False);
    }

    [Test]
    public void CanPlaceArrow_ReturnsTrue_WhenBlockingChainEventuallyLeavesBoard()
    {
        Board board = new(6, 4);
        ArrowModel blocker = CreateArrowFacingRight(3, 1);
        ArrowModel candidate = CreateArrowFacingRight(1, 1);
        Assert.That(board.TryAddArrow(blocker), Is.True);

        Assert.That(board.CanPlaceArrow(candidate), Is.True);
    }

    [Test]
    public void CanRemoveArrow_ReturnsFalse_ForNullAndBlockedArrow_AndTrueOtherwise()
    {
        Board board = new(6, 4);
        ArrowModel blocker = CreateArrowFacingRight(3, 1);
        ArrowModel blocked = CreateArrowFacingRight(1, 1);
        ArrowModel clear = CreateArrowFacingRight(5, 3);
        Assert.That(board.TryAddArrow(blocker), Is.True);
        Assert.That(board.TryAddArrow(blocked), Is.True);
        Assert.That(board.TryAddArrow(clear), Is.True);

        Assert.That(board.CanRemoveArrow(null!), Is.False);
        Assert.That(board.CanRemoveArrow(blocked), Is.False);
        Assert.That(board.CanRemoveArrow(clear), Is.True);
    }

    private static ArrowModel CreateArrow(params Cell[] cells)
    {
        return new ArrowModel(cells);
    }

    private static ArrowModel CreateArrowFacingRight(int headX, int headY)
    {
        Cell head = new(headX, headY);
        Cell next = new(headX - 1, headY);
        return CreateArrow(head, next);
    }

    private static ArrowModel CreateArrowWithZeroCells()
    {
        ArrowModel arrow = (ArrowModel)RuntimeHelpers.GetUninitializedObject(typeof(ArrowModel));
        FieldInfo cellsField = typeof(ArrowModel).GetField("_cells", BindingFlags.Instance | BindingFlags.NonPublic)!;
        cellsField.SetValue(arrow, new List<Cell>());
        return arrow;
    }
}
