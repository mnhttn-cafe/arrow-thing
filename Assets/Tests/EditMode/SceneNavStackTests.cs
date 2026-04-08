using NUnit.Framework;

/// <summary>
/// Tests for <see cref="SceneNavStack"/> — the pure stack logic behind
/// <see cref="SceneNav"/>. Each test models a real user flow through
/// the game's scene graph and verifies the stack returns the correct
/// scene on Pop.
/// </summary>
[TestFixture]
public class SceneNavStackTests
{
    private SceneNavStack _stack;

    [SetUp]
    public void SetUp()
    {
        _stack = new SceneNavStack();
    }

    // ── Basic operations ────────────────────────────────────────────

    [Test]
    public void Push_IncreasesDepth()
    {
        _stack.Push("MainMenu", "Game");
        Assert.AreEqual(1, _stack.Depth);
    }

    [Test]
    public void Pop_EmptyStack_ReturnsNull()
    {
        Assert.IsNull(_stack.Pop());
    }

    [Test]
    public void Push_Pop_ReturnsPushedScene()
    {
        _stack.Push("MainMenu", "Game");
        Assert.AreEqual("MainMenu", _stack.Pop());
        Assert.AreEqual(0, _stack.Depth);
    }

    [Test]
    public void Replace_DoesNotChangeStack()
    {
        _stack.Push("MainMenu", "Game");
        Assert.AreEqual(1, _stack.Depth);
        _stack.Replace("Game", "Game");
        Assert.AreEqual(1, _stack.Depth);
        Assert.AreEqual("MainMenu", _stack.Pop());
    }

    [Test]
    public void Reset_ClearsStack()
    {
        _stack.Push("MainMenu", "SizeSelect");
        _stack.Push("SizeSelect", "Game");
        Assert.AreEqual(2, _stack.Depth);
        _stack.Reset();
        Assert.AreEqual(0, _stack.Depth);
    }

    [Test]
    public void ToArray_ReturnsBottomToTop()
    {
        _stack.Push("MainMenu", "SizeSelect");
        _stack.Push("SizeSelect", "Game");
        CollectionAssert.AreEqual(new[] { "MainMenu", "SizeSelect" }, _stack.ToArray());
    }

    // ── Real user flows ─────────────────────────────────────────────

    [Test]
    public void Flow_MainMenu_SizeSelect_Back()
    {
        // MainMenu → SizeSelect → Back
        _stack.Push("MainMenu", "SizeSelect");
        Assert.AreEqual("MainMenu", _stack.Pop());
    }

    [Test]
    public void Flow_MainMenu_Leaderboard_Back()
    {
        // MainMenu → Leaderboard → Back
        _stack.Push("MainMenu", "Leaderboard");
        Assert.AreEqual("MainMenu", _stack.Pop());
    }

    [Test]
    public void Flow_Play_NewGame_Menu()
    {
        // MainMenu → SizeSelect → Game → Victory → Menu (Pop)
        _stack.Push("MainMenu", "SizeSelect");
        _stack.Push("SizeSelect", "Game");
        // Victory is part of Game scene (not a separate push).
        // OnMenu pops Game, returning to SizeSelect.
        Assert.AreEqual("SizeSelect", _stack.Pop());
    }

    [Test]
    public void Flow_Continue_Menu()
    {
        // MainMenu → Game (Continue) → Victory → Menu (Pop)
        _stack.Push("MainMenu", "Game");
        Assert.AreEqual("MainMenu", _stack.Pop());
    }

    [Test]
    public void Flow_PlayAgain_ReplacesGame()
    {
        // MainMenu → SizeSelect → Game → Victory → Play Again (Replace)
        _stack.Push("MainMenu", "SizeSelect");
        _stack.Push("SizeSelect", "Game");
        _stack.Replace("Game", "Game");
        // Stack unchanged — new Game is on top, SizeSelect below.
        Assert.AreEqual("SizeSelect", _stack.Pop());
    }

    // ── Victory → Leaderboard (the fixed bug) ──────────────────────

    [Test]
    public void Flow_Victory_Leaderboard_Back_ReturnsToSizeSelect()
    {
        // MainMenu → SizeSelect → Game → Victory → View Leaderboard (Replace) → Back
        _stack.Push("MainMenu", "SizeSelect");
        _stack.Push("SizeSelect", "Game");
        // Victory replaces Game with Leaderboard.
        _stack.Replace("Game", "Leaderboard");
        // Back from Leaderboard should return to SizeSelect, not Game.
        Assert.AreEqual("SizeSelect", _stack.Pop());
    }

    [Test]
    public void Flow_Continue_Victory_Leaderboard_Back_ReturnsToMainMenu()
    {
        // MainMenu → Game (Continue) → Victory → View Leaderboard (Replace) → Back
        _stack.Push("MainMenu", "Game");
        _stack.Replace("Game", "Leaderboard");
        Assert.AreEqual("MainMenu", _stack.Pop());
    }

    [Test]
    public void Flow_Victory_Leaderboard_Replay_Back_Back()
    {
        // MainMenu → SizeSelect → Game → Victory → Leaderboard (Replace) → Replay → Back → Back
        _stack.Push("MainMenu", "SizeSelect");
        _stack.Push("SizeSelect", "Game");
        _stack.Replace("Game", "Leaderboard");
        _stack.Push("Leaderboard", "Replay");
        // Back from Replay returns to Leaderboard.
        Assert.AreEqual("Leaderboard", _stack.Pop());
        // Back from Leaderboard returns to SizeSelect.
        Assert.AreEqual("SizeSelect", _stack.Pop());
    }

    // ── Regression: Push would have left Game on stack ──────────────

    [Test]
    public void Regression_Push_Instead_Of_Replace_Would_Return_To_Stale_Game()
    {
        // This test documents the bug that existed before the fix.
        // If Victory used Push instead of Replace, popping Leaderboard
        // would return to the dead Game scene.
        _stack.Push("MainMenu", "SizeSelect");
        _stack.Push("SizeSelect", "Game");
        // BUG: Push instead of Replace.
        _stack.Push("Game", "Leaderboard");
        // Pop returns to Game (stale!) instead of SizeSelect.
        Assert.AreEqual("Game", _stack.Pop());
        // This is the WRONG result — the fix changes Push to Replace
        // so this path never happens in production.
    }

    // ── Leaderboard from MainMenu (not via Victory) ────────────────

    [Test]
    public void Flow_MainMenu_Leaderboard_Replay_Back_Back()
    {
        // MainMenu → Leaderboard → Replay → Back → Back
        _stack.Push("MainMenu", "Leaderboard");
        _stack.Push("Leaderboard", "Replay");
        Assert.AreEqual("Leaderboard", _stack.Pop());
        Assert.AreEqual("MainMenu", _stack.Pop());
    }

    // ── Quick Reset from gameplay ───────────────────────────────────

    [Test]
    public void Flow_QuickReset_Then_Menu()
    {
        // MainMenu → SizeSelect → Game → Quick Reset (Replace) → Victory → Menu
        _stack.Push("MainMenu", "SizeSelect");
        _stack.Push("SizeSelect", "Game");
        _stack.Replace("Game", "Game");
        // Menu from Victory pops Game.
        Assert.AreEqual("SizeSelect", _stack.Pop());
    }

    [Test]
    public void Flow_MultipleQuickResets()
    {
        // MainMenu → SizeSelect → Game → Reset → Reset → Reset → Menu
        _stack.Push("MainMenu", "SizeSelect");
        _stack.Push("SizeSelect", "Game");
        _stack.Replace("Game", "Game");
        _stack.Replace("Game", "Game");
        _stack.Replace("Game", "Game");
        // Stack unchanged through all replaces.
        Assert.AreEqual("SizeSelect", _stack.Pop());
        Assert.AreEqual("MainMenu", _stack.Pop());
    }
}
