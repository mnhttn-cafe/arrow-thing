using NUnit.Framework;

namespace ArrowThing.Model.Tests;

[TestFixture]
public sealed class BoardCellTests
{
    [Test]
    public void Constructor_SetsCoordinates()
    {
        Cell cell = new(7, 11);

        Assert.That(cell.X, Is.EqualTo(7));
        Assert.That(cell.Y, Is.EqualTo(11));
    }

    [Test]
    public void EqualsBoardCell_ReturnsTrueForSameCoordinates_AndFalseForDifferentCoordinates()
    {
        Cell a = new(2, 3);
        Cell same = new(2, 3);
        Cell differentX = new(9, 3);
        Cell differentY = new(2, 8);

        Assert.That(a.Equals(same), Is.True);
        Assert.That(a.Equals(differentX), Is.False);
        Assert.That(a.Equals(differentY), Is.False);
    }

    [Test]
    public void EqualsObject_ReturnsExpectedForBoardCellNullAndDifferentType()
    {
        Cell cell = new(5, 6);
        object sameAsObject = new BoardCell(5, 6);
        object differentType = "not-a-board-cell";

        Assert.That(cell.Equals(sameAsObject), Is.True);
        Assert.That(cell.Equals(differentType), Is.False);
        Assert.That(cell.Equals(null!), Is.False);
    }

    [Test]
    public void GetHashCode_IsEqualForEqualCells()
    {
        Cell a = new(4, 9);
        Cell b = new(4, 9);

        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void EqualityOperators_ReturnExpectedValues()
    {
        Cell a = new(1, 1);
        Cell same = new(1, 1);
        Cell different = new(1, 2);

        Assert.That(a == same, Is.True);
        Assert.That(a != same, Is.False);
        Assert.That(a == different, Is.False);
        Assert.That(a != different, Is.True);
    }
}
