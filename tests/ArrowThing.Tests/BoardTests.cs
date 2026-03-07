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
    public void Version_StartsAtZero()
        => Assert.That(new Board(3, 3).Version, Is.EqualTo(0));

    [Test]
    public void Version_IncrementsOnAddArrow()
    {
        var board = new Board(4, 4);
        board.AddArrow(new Arrow([new(0, 1), new(0, 0)]));
        Assert.That(board.Version, Is.EqualTo(1));
        board.AddArrow(new Arrow([new(3, 3), new(3, 2)]));
        Assert.That(board.Version, Is.EqualTo(2));
    }

    [Test]
    public void Version_IncrementsOnRemoveArrow()
    {
        var board = new Board(4, 4);
        var arrow = new Arrow([new(0, 1), new(0, 0)]);
        board.AddArrow(arrow);
        board.RemoveArrow(arrow);
        Assert.That(board.Version, Is.EqualTo(2));
    }

    [Test]
    public void Arrows_ReflectsAddedArrows()
    {
        var board = new Board(4, 4);
        var arrow = new Arrow([new(0, 1), new(0, 0)]);
        board.AddArrow(arrow);
        Assert.That(board.Arrows, Has.Count.EqualTo(1));
        Assert.That(board.Arrows[0], Is.SameAs(arrow));
    }

    [Test]
    public void Arrows_ReflectsRemovedArrows()
    {
        var board = new Board(4, 4);
        var arrow = new Arrow([new(0, 1), new(0, 0)]);
        board.AddArrow(arrow);
        board.RemoveArrow(arrow);
        Assert.That(board.Arrows, Is.Empty);
    }

    [Test]
    public void Arrows_IsReadOnlyList()
        => Assert.That(new Board(3, 3).Arrows, Is.InstanceOf<System.Collections.Generic.IReadOnlyList<Arrow>>());
}
