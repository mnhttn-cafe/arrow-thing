using NUnit.Framework;

[TestFixture]
public class GameTimerTests
{
    [Test]
    public void InitialState_IsInspection()
    {
        var timer = new GameTimer(15.0);
        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Inspection));
        Assert.That(timer.InspectionRemaining, Is.EqualTo(15.0));
        Assert.That(timer.SolveElapsed, Is.EqualTo(0.0));
        Assert.That(timer.IsSolving, Is.False);
    }

    [Test]
    public void Tick_DuringInspection_CountsDown()
    {
        var timer = new GameTimer(10.0);
        timer.Start(0.0);
        timer.Tick(3.0);

        Assert.That(timer.InspectionRemaining, Is.EqualTo(7.0).Within(0.001));
        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Inspection));
    }

    [Test]
    public void Tick_InspectionExpires_TransitionsToSolving()
    {
        var timer = new GameTimer(5.0);
        timer.Start(0.0);

        GameTimer.Phase receivedPhase = GameTimer.Phase.Inspection;
        timer.PhaseChanged += p => receivedPhase = p;

        timer.Tick(5.0);

        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Solving));
        Assert.That(timer.InspectionRemaining, Is.EqualTo(0.0));
        Assert.That(receivedPhase, Is.EqualTo(GameTimer.Phase.Solving));
    }

    [Test]
    public void StartSolve_DuringInspection_TransitionsToSolving()
    {
        var timer = new GameTimer(15.0);
        timer.Start(0.0);
        timer.Tick(3.0);

        timer.StartSolve(3.5);

        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Solving));
        Assert.That(timer.SolveElapsed, Is.EqualTo(0.0));
        Assert.That(timer.InspectionRemaining, Is.EqualTo(0.0));
    }

    [Test]
    public void StartSolve_WhenAlreadySolving_IsIgnored()
    {
        var timer = new GameTimer(15.0);
        timer.Start(0.0);
        timer.StartSolve(1.0);
        timer.Tick(3.0);

        // Second StartSolve should not reset the start time
        timer.StartSolve(3.0);
        timer.Tick(4.0);

        Assert.That(timer.SolveElapsed, Is.EqualTo(3.0).Within(0.001));
    }

    [Test]
    public void Tick_DuringSolving_CountsUp()
    {
        var timer = new GameTimer(15.0);
        timer.Start(0.0);
        timer.StartSolve(1.0);
        timer.Tick(4.0);

        Assert.That(timer.SolveElapsed, Is.EqualTo(3.0).Within(0.001));
    }

    [Test]
    public void Finish_WithFrameTime_SetsFinalSolveElapsed()
    {
        var timer = new GameTimer(15.0);
        timer.Start(0.0);
        timer.StartSolve(2.0);
        timer.Finish(5.5);

        Assert.That(timer.SolveElapsed, Is.EqualTo(3.5).Within(0.001));
        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Finished));
    }

    [Test]
    public void Finish_WithInputTimestamps_UsesPreciseTiming()
    {
        var timer = new GameTimer(15.0);
        timer.Start(0.0);
        // Frame time = 2.0, input timestamp = 2.001
        timer.StartSolve(2.0, 2.001);
        // Frame time = 5.5, input timestamp = 5.502
        timer.Finish(5.5, 5.502);

        double expected = 5.502 - 2.001;
        Assert.That(timer.SolveElapsed, Is.EqualTo(expected).Within(0.0000001));
    }

    [Test]
    public void Finish_FiresPhaseChangedEvent()
    {
        var timer = new GameTimer(15.0);
        timer.Start(0.0);
        timer.StartSolve(1.0);

        GameTimer.Phase receivedPhase = GameTimer.Phase.Solving;
        timer.PhaseChanged += p => receivedPhase = p;

        timer.Finish(2.0);

        Assert.That(receivedPhase, Is.EqualTo(GameTimer.Phase.Finished));
    }

    [Test]
    public void Finish_WhenNotSolving_IsIgnored()
    {
        var timer = new GameTimer(15.0);
        timer.Start(0.0);

        timer.Finish(1.0);

        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Inspection));
    }

    [Test]
    public void Finish_WithInvertedInputTimestamps_FallsBackToFrameTime()
    {
        var timer = new GameTimer(15.0);
        timer.Start(0.0);
        // Input start at 2.5 (after frame time 2.0)
        timer.StartSolve(2.0, 2.5);
        // Input finish at 2.4 — earlier than input start (inverted); frame time is 5.0
        timer.Finish(5.0, 2.4);

        // Input elapsed would be 2.4 - 2.5 = -0.1 (negative), so falls back to 5.0 - 2.0 = 3.0
        Assert.That(timer.SolveElapsed, Is.EqualTo(3.0).Within(0.001));
        Assert.That(timer.SolveElapsed, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Finished));
    }

    [Test]
    public void Tick_AfterFinished_DoesNotChangeSolveElapsed()
    {
        var timer = new GameTimer(15.0);
        timer.Start(0.0);
        timer.StartSolve(1.0);
        timer.Finish(3.0);

        double finalTime = timer.SolveElapsed;
        timer.Tick(10.0);

        Assert.That(timer.SolveElapsed, Is.EqualTo(finalTime));
    }

    [Test]
    public void OneArrowBoard_StartSolveThenFinish_SameTimestamp()
    {
        var timer = new GameTimer(15.0);
        timer.Start(0.0);
        timer.Tick(2.0);

        // 1-arrow board: start and finish at same frame time, same input time
        timer.StartSolve(2.0, 2.3);
        timer.Finish(2.0, 2.3);

        Assert.That(timer.SolveElapsed, Is.EqualTo(0.0));
        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Finished));
    }

    [Test]
    public void OneArrowBoard_AfterInspectionExpiry_HasNonZeroSolveTime()
    {
        var timer = new GameTimer(5.0);
        timer.Start(0.0);
        timer.Tick(5.0); // Inspection expires, solve starts at t=5.0

        // Player clears the only arrow at t=7.0
        timer.Finish(7.0);

        Assert.That(timer.SolveElapsed, Is.EqualTo(2.0).Within(0.001));
        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Finished));
    }

    [Test]
    public void InspectionRemaining_NeverGoesNegative()
    {
        var timer = new GameTimer(5.0);
        timer.Start(0.0);
        timer.Tick(100.0);

        Assert.That(timer.InspectionRemaining, Is.EqualTo(0.0));
    }
}
