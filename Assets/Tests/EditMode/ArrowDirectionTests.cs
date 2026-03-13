using NUnit.Framework;

[TestFixture]
public class ArrowDirectionTests
{
    // DeriveHeadDirection: direction is opposite to the head→next vector (Y-up convention)

    [Test]
    public void HeadDirection_NextBelow_IsUp()
        => Assert.That(new Arrow(new Cell[] { new(5, 5), new(5, 4) }).HeadDirection, Is.EqualTo(Arrow.Direction.Up));

    [Test]
    public void HeadDirection_NextAbove_IsDown()
        => Assert.That(new Arrow(new Cell[] { new(5, 5), new(5, 6) }).HeadDirection, Is.EqualTo(Arrow.Direction.Down));

    [Test]
    public void HeadDirection_NextRight_IsLeft()
        => Assert.That(new Arrow(new Cell[] { new(5, 5), new(6, 5) }).HeadDirection, Is.EqualTo(Arrow.Direction.Left));

    [Test]
    public void HeadDirection_NextLeft_IsRight()
        => Assert.That(new Arrow(new Cell[] { new(5, 5), new(4, 5) }).HeadDirection, Is.EqualTo(Arrow.Direction.Right));

    [Test]
    public void Constructor_NonAdjacentCells_Throws()
        => Assert.Throws<System.ArgumentException>(() => _ = new Arrow(new Cell[] { new(0, 0), new(2, 0) }));

    [Test]
    public void Constructor_SingleCell_Throws()
        => Assert.Throws<System.ArgumentException>(() => _ = new Arrow(new Cell[] { new(0, 0) }));

    [Test]
    public void Constructor_NullCells_Throws()
        => Assert.Throws<System.ArgumentNullException>(() => _ = new Arrow(null!));

    // GetDirectionStep: Y-up convention

    [Test]
    public void DirectionStep_Up_IsPositiveY()
        => Assert.That(Arrow.GetDirectionStep(Arrow.Direction.Up), Is.EqualTo((0, 1)));

    [Test]
    public void DirectionStep_Down_IsNegativeY()
        => Assert.That(Arrow.GetDirectionStep(Arrow.Direction.Down), Is.EqualTo((0, -1)));

    [Test]
    public void DirectionStep_Right_IsPositiveX()
        => Assert.That(Arrow.GetDirectionStep(Arrow.Direction.Right), Is.EqualTo((1, 0)));

    [Test]
    public void DirectionStep_Left_IsNegativeX()
        => Assert.That(Arrow.GetDirectionStep(Arrow.Direction.Left), Is.EqualTo((-1, 0)));

    [Test]
    public void HeadCell_IsFirstCell()
    {
        var arrow = new Arrow(new Cell[] { new(3, 7), new(3, 6), new(3, 5) });
        Assert.That(arrow.HeadCell, Is.EqualTo(new Cell(3, 7)));
    }

    [Test]
    public void LongerArrow_DirectionDerivedFromFirstTwoCellsOnly()
    {
        // Head at (0,0), next at (1,0) — faces Left regardless of remaining cells
        var arrow = new Arrow(new Cell[] { new(0, 0), new(1, 0), new(2, 0), new(2, 1) });
        Assert.That(arrow.HeadDirection, Is.EqualTo(Arrow.Direction.Left));
    }
}
