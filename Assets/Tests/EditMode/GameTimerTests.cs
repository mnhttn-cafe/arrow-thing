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
    public void Tick_InspectionExpiredDuringTabOut_SolveElapsedReflectsFullTime()
    {
        // Inspection = 15s. First Tick arrives 20s after Start (simulates tab-out).
        // Solve should show 5s — the time since inspection actually expired.
        var timer = new GameTimer(15.0);
        timer.Start(0.0);
        timer.Tick(20.0);

        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Solving));
        Assert.That(timer.SolveElapsed, Is.EqualTo(5.0).Within(0.001));
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

        // 1-arrow board: start and finish at same timestamp
        timer.StartSolve(2.0);
        timer.Finish(2.0);

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
