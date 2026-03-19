using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;

[TestFixture]
public class ReplayRecorderTests
{
    // ── Seq ordering ──────────────────────────────────────────────────────────

    [Test]
    public void SeqNumbers_AreMonotonicallyIncreasing()
    {
        var rec = new ReplayRecorder();
        rec.RecordSessionStart();
        rec.RecordStartSolve(0.0);
        rec.RecordClear(0.5, 1f, 2f);
        rec.RecordReject(1.0, 3f, 4f);
        rec.RecordSessionLeave(1.0);

        var events = rec.Events;
        for (int i = 1; i < events.Count; i++)
            Assert.Less(events[i - 1].seq, events[i].seq, $"seq not increasing at index {i}");
    }

    [Test]
    public void SeqNumbers_StartAtZero()
    {
        var rec = new ReplayRecorder();
        rec.RecordSessionStart();
        Assert.AreEqual(0, rec.Events[0].seq);
    }

    // ── Event types ───────────────────────────────────────────────────────────

    [Test]
    public void RecordSessionStart_SetsCorrectType()
    {
        var rec = new ReplayRecorder();
        rec.RecordSessionStart();
        Assert.AreEqual(ReplayEventType.SessionStart, rec.Events[0].type);
    }

    [Test]
    public void RecordSessionStart_SetsWallTime()
    {
        var rec = new ReplayRecorder();
        rec.RecordSessionStart();
        Assert.IsNotEmpty(rec.Events[0].timestamp);
    }

    [Test]
    public void RecordSessionLeave_SetsCorrectTypeAndElapsed()
    {
        var rec = new ReplayRecorder();
        rec.RecordSessionLeave(42.5);
        Assert.AreEqual(ReplayEventType.SessionLeave, rec.Events[0].type);
        Assert.AreEqual(42.5, rec.Events[0].t, 1e-9);
    }

    [Test]
    public void RecordSessionRejoin_SetsCorrectType()
    {
        var rec = new ReplayRecorder();
        rec.RecordSessionRejoin();
        Assert.AreEqual(ReplayEventType.SessionRejoin, rec.Events[0].type);
    }

    [Test]
    public void RecordStartSolve_SetsTypeAndTime()
    {
        var rec = new ReplayRecorder();
        rec.RecordStartSolve(0.0);
        var evt = rec.Events[0];
        Assert.AreEqual(ReplayEventType.StartSolve, evt.type);
        Assert.AreEqual(0.0, evt.t, 1e-9);
    }

    [Test]
    public void RecordClear_SetsTypeTimePositionAndTimestamp()
    {
        var rec = new ReplayRecorder();
        rec.RecordClear(3.14, 5f, 7f);
        var evt = rec.Events[0];
        Assert.AreEqual(ReplayEventType.Clear, evt.type);
        Assert.AreEqual(3.14, evt.t, 1e-9);
        Assert.AreEqual(5f, evt.posX.Value, 1e-5f);
        Assert.AreEqual(7f, evt.posY.Value, 1e-5f);
        Assert.IsNotEmpty(evt.timestamp);
    }

    [Test]
    public void RecordReject_SetsTypeTimeAndPosition()
    {
        var rec = new ReplayRecorder();
        rec.RecordReject(1.23, -3f, 4f);
        var evt = rec.Events[0];
        Assert.AreEqual(ReplayEventType.Reject, evt.type);
        Assert.AreEqual(1.23, evt.t, 1e-9);
        Assert.AreEqual(-3f, evt.posX.Value, 1e-5f);
        Assert.AreEqual(4f, evt.posY.Value, 1e-5f);
    }

    [Test]
    public void RecordEndSolve_SetsTypeTimeAndTimestamp()
    {
        var rec = new ReplayRecorder();
        rec.RecordEndSolve(42.0);
        var evt = rec.Events[0];
        Assert.AreEqual(ReplayEventType.EndSolve, evt.type);
        Assert.AreEqual(42.0, evt.t, 1e-9);
        Assert.IsNotEmpty(evt.timestamp);
    }

    // ── start_solve + clear same-timestamp scenario ───────────────────────────

    [Test]
    public void StartSolveAndClear_SameTimestamp_OrderedBySeq()
    {
        var rec = new ReplayRecorder();
        rec.RecordStartSolve(0.0); // seq 0, t = 0
        rec.RecordClear(0.0, 2f, 3f); // seq 1, t = 0

        Assert.AreEqual(2, rec.Events.Count);
        Assert.AreEqual(0, rec.Events[0].seq);
        Assert.AreEqual(1, rec.Events[1].seq);
        Assert.AreEqual(0.0, rec.Events[0].t, 1e-9);
        Assert.AreEqual(0.0, rec.Events[1].t, 1e-9);
        Assert.AreEqual(ReplayEventType.StartSolve, rec.Events[0].type);
        Assert.AreEqual(ReplayEventType.Clear, rec.Events[1].type);
    }

    // ── ToReplayData ──────────────────────────────────────────────────────────

    [Test]
    public void ToReplayData_IncludesAllEvents()
    {
        var rec = new ReplayRecorder();
        rec.RecordSessionStart();
        rec.RecordClear(1.0, 0f, 0f);

        var data = rec.ToReplayData("game-1", 42, 10, 10, 20, 15f);
        Assert.AreEqual(2, data.events.Count);
        Assert.AreEqual("game-1", data.gameId);
        Assert.AreEqual(42, data.seed);
        Assert.AreEqual(10, data.boardWidth);
        Assert.AreEqual(10, data.boardHeight);
        Assert.AreEqual(20, data.maxArrowLength);
        Assert.AreEqual(15f, data.inspectionDuration, 1e-5f);
        Assert.AreEqual(-1.0, data.finalTime, 1e-9); // in-progress
    }

    [Test]
    public void ToReplayData_FinalTime_SetWhenProvided()
    {
        var rec = new ReplayRecorder();
        rec.RecordClear(9.99, 0f, 0f);
        var data = rec.ToReplayData("g", 0, 5, 5, 10, 15f, finalTime: 9.99);
        Assert.AreEqual(9.99, data.finalTime, 1e-9);
    }

    [Test]
    public void ToReplayData_EventsListIsACopy()
    {
        var rec = new ReplayRecorder();
        rec.RecordSessionStart();
        var data = rec.ToReplayData("g", 0, 5, 5, 10, 15f);

        // Adding more events after snapshot does not affect the snapshot
        rec.RecordClear(1.0, 0f, 0f);
        Assert.AreEqual(1, data.events.Count);
        Assert.AreEqual(2, rec.Events.Count);
    }

    // ── boardSnapshot ─────────────────────────────────────────────────────────

    [Test]
    public void ToReplayData_WithSnapshot_SnapshotIsPreserved()
    {
        var rec = new ReplayRecorder();
        rec.RecordSessionStart();

        var snapshot = new List<List<Cell>>
        {
            new List<Cell> { new Cell(0, 0), new Cell(0, 1) },
            new List<Cell> { new Cell(2, 3), new Cell(3, 3), new Cell(4, 3) },
        };

        var data = rec.ToReplayData("g", 1, 5, 5, 10, 15f, boardSnapshot: snapshot);

        Assert.IsNotNull(data.boardSnapshot);
        Assert.AreEqual(2, data.boardSnapshot.Count);
        Assert.AreEqual(new Cell(0, 0), data.boardSnapshot[0][0]);
        Assert.AreEqual(new Cell(0, 1), data.boardSnapshot[0][1]);
        Assert.AreEqual(3, data.boardSnapshot[1].Count);
    }

    [Test]
    public void ToReplayData_WithSnapshot_SetsVersionTwo()
    {
        var rec = new ReplayRecorder();
        var snapshot = new List<List<Cell>>
        {
            new List<Cell> { new Cell(0, 0), new Cell(1, 0) },
        };
        var data = rec.ToReplayData("g", 0, 5, 5, 10, 15f, boardSnapshot: snapshot);
        Assert.AreEqual(2, data.version);
    }

    [Test]
    public void ToReplayData_WithoutSnapshot_SnapshotIsNull()
    {
        var rec = new ReplayRecorder();
        rec.RecordSessionStart();
        var data = rec.ToReplayData("g", 0, 5, 5, 10, 15f);
        Assert.IsNull(data.boardSnapshot);
    }

    [Test]
    public void ResumeWithSnapshot_CanRestoreArrowsOnBoard()
    {
        // Build a minimal board from snapshot cells (domain-layer only, no Unity)
        var snapshotArrows = new List<List<Cell>>
        {
            new List<Cell> { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2) },
            new List<Cell> { new Cell(3, 3), new Cell(2, 3) },
        };

        var board = new Board(6, 6);
        foreach (var arrowCells in snapshotArrows)
            board.AddArrow(new Arrow(arrowCells));

        Assert.AreEqual(2, board.Arrows.Count);

        // Verify cells match what was stored in the snapshot
        var restoredCells0 = board.Arrows[0].Cells.ToList();
        Assert.AreEqual(new Cell(0, 0), restoredCells0[0]);
        Assert.AreEqual(new Cell(0, 2), restoredCells0[2]);

        var restoredCells1 = board.Arrows[1].Cells.ToList();
        Assert.AreEqual(new Cell(3, 3), restoredCells1[0]);
        Assert.AreEqual(new Cell(2, 3), restoredCells1[1]);
    }

    // ── Resume from prior events ──────────────────────────────────────────────

    [Test]
    public void Resume_ContinuesSeqFromNextSeq()
    {
        var prior = new List<ReplayEvent>
        {
            new ReplayEvent { seq = 0, type = ReplayEventType.SessionStart },
            new ReplayEvent { seq = 1, type = ReplayEventType.Clear },
        };

        var rec = new ReplayRecorder(prior, nextSeq: 2);
        rec.RecordSessionRejoin();

        Assert.AreEqual(3, rec.Events.Count);
        Assert.AreEqual(2, rec.Events[2].seq);
        Assert.AreEqual(ReplayEventType.SessionRejoin, rec.Events[2].type);
    }

    [Test]
    public void Resume_PriorEventsArePreserved()
    {
        var prior = new List<ReplayEvent>
        {
            new ReplayEvent
            {
                seq = 0,
                type = ReplayEventType.SessionStart,
                timestamp = "t0",
            },
            new ReplayEvent
            {
                seq = 1,
                type = ReplayEventType.Clear,
                t = 0.5,
            },
        };

        var rec = new ReplayRecorder(prior, nextSeq: 2);

        Assert.AreEqual(0, rec.Events[0].seq);
        Assert.AreEqual(ReplayEventType.SessionStart, rec.Events[0].type);
        Assert.AreEqual(1, rec.Events[1].seq);
        Assert.AreEqual(0.5, rec.Events[1].t, 1e-9);
    }

    [Test]
    public void Resume_PriorListNotMutated()
    {
        var prior = new List<ReplayEvent>
        {
            new ReplayEvent { seq = 0, type = ReplayEventType.SessionStart },
        };

        var rec = new ReplayRecorder(prior, nextSeq: 1);
        rec.RecordClear(1.0, 0f, 0f);

        // The original list should not have been modified
        Assert.AreEqual(1, prior.Count);
    }
}
