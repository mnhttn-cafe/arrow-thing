using NUnit.Framework;

[TestFixture]
public class GameSettingsTests
{
    [TearDown]
    public void TearDown()
    {
        GameSettings.Reset();
    }

    [Test]
    public void IsSet_DefaultsFalse()
    {
        Assert.That(GameSettings.IsSet, Is.False);
    }

    [Test]
    public void Apply_SetsWidthAndHeight()
    {
        GameSettings.Apply(20, 20);
        Assert.That(GameSettings.Width, Is.EqualTo(20));
        Assert.That(GameSettings.Height, Is.EqualTo(20));
        Assert.That(GameSettings.IsSet, Is.True);
    }

    [Test]
    public void Apply_MaxArrowLength_UsesLargerDimension()
    {
        GameSettings.Apply(10, 20);
        Assert.That(GameSettings.MaxArrowLength, Is.EqualTo(40));

        GameSettings.Apply(30, 10);
        Assert.That(GameSettings.MaxArrowLength, Is.EqualTo(60));
    }

    [Test]
    public void Apply_SquareBoard_MaxArrowLengthIsTwiceSize()
    {
        GameSettings.Apply(10, 10);
        Assert.That(GameSettings.MaxArrowLength, Is.EqualTo(20));
    }

    [Test]
    public void Reset_ClearsState()
    {
        GameSettings.Apply(20, 20);
        GameSettings.Reset();
        Assert.That(GameSettings.IsSet, Is.False);
        Assert.That(GameSettings.Width, Is.EqualTo(0));
        Assert.That(GameSettings.Height, Is.EqualTo(0));
        Assert.That(GameSettings.MaxArrowLength, Is.EqualTo(0));
    }
}
