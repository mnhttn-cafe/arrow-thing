using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ArrowThing.Model.Tests;

[TestFixture]
public sealed class ArrowModelTests
{
    [Test]
    public void Constructor_ThrowsArgumentNullException_WhenCellsIsNull()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => new ArrowModel(null!))!;
        Assert.That(ex.ParamName, Is.EqualTo("cells"));
    }

    [Test]
    [TestCase(0)]
    [TestCase(1)]
    public void Constructor_ThrowsArgumentException_WhenFewerThanTwoCellsAreProvided(int cellCount)
    {
        List<BoardCell> cells = new();
        for (int i = 0; i < cellCount; i++)
        {
            cells.Add(new BoardCell(i, 0));
        }

        ArgumentException ex = Assert.Throws<ArgumentException>(() => new ArrowModel(cells))!;
        Assert.That(ex.ParamName, Is.EqualTo("cells"));
    }

    [Test]
    [TestCase(0, -1, ArrowDirection.Down)]
    [TestCase(1, 0, ArrowDirection.Left)]
    [TestCase(0, 1, ArrowDirection.Up)]
    [TestCase(-1, 0, ArrowDirection.Right)]
    public void Constructor_SetsHeadCellCellsAndDerivedHeadDirection(int dx, int dy, ArrowDirection expectedDirection)
    {
        BoardCell head = new(10, 10);
        BoardCell next = new(head.X + dx, head.Y + dy);
        BoardCell tail = new(next.X, next.Y + 1);
        List<BoardCell> cells = new() { head, next, tail };

        ArrowModel arrow = new(cells);

        Assert.That(arrow.HeadCell, Is.EqualTo(head));
        Assert.That(arrow.HeadDirection, Is.EqualTo(expectedDirection));
        Assert.That(arrow.Cells, Has.Count.EqualTo(3));
        Assert.That(arrow.Cells[0], Is.EqualTo(head));
        Assert.That(arrow.Cells[1], Is.EqualTo(next));
        Assert.That(arrow.Cells[2], Is.EqualTo(tail));
    }

    [Test]
    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(2, 0)]
    [TestCase(0, 2)]
    public void Constructor_ThrowsArgumentException_WhenFirstTwoCellsAreNotOrthogonallyAdjacent(int dx, int dy)
    {
        BoardCell head = new(3, 3);
        BoardCell next = new(head.X + dx, head.Y + dy);

        Assert.Throws<ArgumentException>(() => new ArrowModel(new[] { head, next }));
    }

    [Test]
    [TestCase(ArrowDirection.Up, 0, -1)]
    [TestCase(ArrowDirection.Right, 1, 0)]
    [TestCase(ArrowDirection.Down, 0, 1)]
    [TestCase(ArrowDirection.Left, -1, 0)]
    public void GetDirectionStep_ReturnsExpectedOffsets(ArrowDirection direction, int expectedDx, int expectedDy)
    {
        (int dx, int dy) = ArrowModel.GetDirectionStep(direction);

        Assert.That(dx, Is.EqualTo(expectedDx));
        Assert.That(dy, Is.EqualTo(expectedDy));
    }

    [Test]
    public void GetDirectionStep_ThrowsArgumentOutOfRangeException_ForUnsupportedDirection()
    {
        ArrowDirection invalidDirection = (ArrowDirection)(-1);

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => ArrowModel.GetDirectionStep(invalidDirection))!;

        Assert.That(ex.ParamName, Is.EqualTo("direction"));
        Assert.That(ex.ActualValue, Is.EqualTo(invalidDirection));
    }
}
