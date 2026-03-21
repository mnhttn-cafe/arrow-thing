using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class ReplayPlayerTests
{
    // ── Basic playback ───────────────────────────────────────────────────────

    [Test]
    public void Advance_FiresEventsInOrder()
    {
        var player = new ReplayPlayer(MakeReplayData(3, 1.0));

        // Advance past all events
        var fired = player.Advance(10.0);

        Assert.AreEqual(3, fired.Count);
        Assert.AreEqual(ReplayEventType.Clear, fired[0].type);
        Assert.AreEqual(ReplayEventType.Clear, fired[1].type);
        Assert.AreEqual(ReplayEventType.Clear, fired[2].type);
    }

    [Test]
    public void Advance_FiresOnlyEventsUpToCurrentTime()
    {
        var player = new ReplayPlayer(MakeReplayData(3, 2.0)); // events at 2, 4, 6

        var fired1 = player.Advance(3.0); // should fire event at t=2
        Assert.AreEqual(1, fired1.Count);

        var fired2 = player.Advance(2.5); // advance to t=5.5, should fire event at t=4
        Assert.AreEqual(1, fired2.Count);
    }

    [Test]
    public void Advance_WhenPaused_NoEventsFire()
    {
        var player = new ReplayPlayer(MakeReplayData(3, 1.0));
        player.IsPlaying = false;

        var fired = player.Advance(100.0);
        Assert.AreEqual(0, fired.Count);
    }

    [Test]
    public void IsFinished_TrueAfterAllEvents()
    {
        var player = new ReplayPlayer(MakeReplayData(2, 1.0));
        Assert.IsFalse(player.IsFinished);

        player.Advance(100.0);
        Assert.IsTrue(player.IsFinished);
    }

    // ── Speed ────────────────────────────────────────────────────────────────

    [Test]
    public void CycleSpeed_CyclesThroughSteps()
    {
        var player = new ReplayPlayer(MakeReplayData(1, 1.0));

        Assert.AreEqual(1f, player.PlaybackSpeed); // default
        Assert.AreEqual(2f, player.CycleSpeed());
        Assert.AreEqual(4f, player.CycleSpeed());
        Assert.AreEqual(0.5f, player.CycleSpeed());
        Assert.AreEqual(1f, player.CycleSpeed());
    }

    [Test]
    public void Advance_RespectsPlaybackSpeed()
    {
        var player = new ReplayPlayer(MakeReplayData(2, 5.0)); // events at 5 and 10
        player.CycleSpeed(); // 2x

        // At 2x, advancing 3s of real time = 6s of playback time
        var fired = player.Advance(3.0);
        Assert.AreEqual(1, fired.Count); // event at t=5
    }

    // ── Seek forward ─────────────────────────────────────────────────────────

    [Test]
    public void SeekTo_Forward_ReturnsEventsToApply()
    {
        var player = new ReplayPlayer(MakeReplayData(4, 2.5)); // events at 2.5, 5, 7.5, 10
        // Total duration = 10

        var result = player.SeekTo(0.6); // seek to t=6, should include events at 2.5 and 5
        Assert.IsTrue(result.IsForward);
        Assert.AreEqual(2, result.EventsToApply.Count);
        Assert.AreEqual(0, result.EventsToUndo.Count);
    }

    [Test]
    public void SeekTo_Forward_UpdatesCurrentTime()
    {
        var player = new ReplayPlayer(MakeReplayData(2, 5.0)); // total = 10
        player.SeekTo(0.5); // t = 5
        Assert.AreEqual(5.0, player.CurrentTime, 0.01);
    }

    // ── Seek backward ────────────────────────────────────────────────────────

    [Test]
    public void SeekTo_Backward_ReturnsEventsToUndo()
    {
        var player = new ReplayPlayer(MakeReplayData(4, 2.5)); // events at 2.5, 5, 7.5, 10

        // Advance past all events
        player.Advance(100.0);
        Assert.AreEqual(4, player.CurrentEventIndex);

        // Seek back to t=3 (only first event at 2.5 should remain)
        var result = player.SeekTo(0.3); // t=3
        Assert.IsTrue(result.IsBackward);
        Assert.AreEqual(3, result.EventsToUndo.Count); // undo events at 5, 7.5, 10
    }

    [Test]
    public void SeekTo_Backward_UpdatesCurrentIndex()
    {
        var player = new ReplayPlayer(MakeReplayData(4, 2.5));
        player.Advance(100.0);

        player.SeekTo(0.0); // back to start
        Assert.AreEqual(0, player.CurrentEventIndex);
    }

    [Test]
    public void SeekTo_BackwardThenForward_Works()
    {
        var player = new ReplayPlayer(MakeReplayData(4, 2.5)); // events at 2.5, 5, 7.5, 10
        player.Advance(100.0);

        // Seek back
        player.SeekTo(0.0);
        Assert.AreEqual(0, player.CurrentEventIndex);

        // Seek forward again
        var result = player.SeekTo(0.6); // t=6
        Assert.IsTrue(result.IsForward);
        Assert.AreEqual(2, result.EventsToApply.Count);
    }

    // ── NormalizedTime ───────────────────────────────────────────────────────

    [Test]
    public void NormalizedTime_ProgressesDuringPlayback()
    {
        var player = new ReplayPlayer(MakeReplayData(2, 5.0)); // total = 10
        Assert.AreEqual(0.0, player.NormalizedTime, 0.01);

        player.Advance(5.0);
        Assert.AreEqual(0.5, player.NormalizedTime, 0.01);
    }

    [Test]
    public void NormalizedTime_ClampsToOne()
    {
        var player = new ReplayPlayer(MakeReplayData(1, 5.0)); // total = 5
        player.Advance(100.0);
        Assert.LessOrEqual(player.NormalizedTime, 1.0);
    }

    // ── ClearedEventIndices tracking ─────────────────────────────────────────

    [Test]
    public void ClearedEventIndices_TracksClears()
    {
        var player = new ReplayPlayer(MakeReplayData(3, 1.0));
        player.Advance(100.0);

        Assert.AreEqual(3, player.ClearedEventIndices.Count);
    }

    [Test]
    public void ClearedEventIndices_ExcludesRejects()
    {
        var data = MakeReplayDataWithRejects(2, 1, 1.0); // 2 clears + 1 reject
        var player = new ReplayPlayer(data);
        player.Advance(100.0);

        Assert.AreEqual(2, player.ClearedEventIndices.Count);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Test]
    public void EmptyReplay_NoErrors()
    {
        var data = new ReplayData
        {
            events = new List<ReplayEvent>
            {
                new ReplayEvent
                {
                    type = ReplayEventType.SessionStart,
                    timestamp = "2026-01-01T00:00:00Z",
                },
                new ReplayEvent
                {
                    type = ReplayEventType.StartSolve,
                    timestamp = "2026-01-01T00:00:01Z",
                },
                new ReplayEvent
                {
                    type = ReplayEventType.EndSolve,
                    timestamp = "2026-01-01T00:00:01Z",
                },
            },
            finalTime = 0,
        };

        var player = new ReplayPlayer(data);
        Assert.AreEqual(0, player.TimedEventCount);
        Assert.IsTrue(player.IsFinished);

        var fired = player.Advance(1.0);
        Assert.AreEqual(0, fired.Count);
    }

    [Test]
    public void SeekTo_ClampsBounds()
    {
        var player = new ReplayPlayer(MakeReplayData(2, 1.0));

        // Negative normalizes to 0
        player.SeekTo(-1.0);
        Assert.AreEqual(0.0, player.CurrentTime, 0.01);

        // >1 normalizes to 1
        player.SeekTo(2.0);
        Assert.AreEqual(player.TotalDuration, player.CurrentTime, 0.01);
    }

    [Test]
    public void TotalDuration_UsesFinalTimeWhenSet()
    {
        var data = MakeReplayData(2, 3.0); // events at 3, 6; finalTime = 6
        var player = new ReplayPlayer(data);
        Assert.AreEqual(6.0, player.TotalDuration, 0.01);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a ReplayData with <paramref name="clearCount"/> clear events,
    /// each spaced <paramref name="interval"/> seconds apart after start_solve.
    /// </summary>
    private static ReplayData MakeReplayData(int clearCount, double interval)
    {
        var events = new List<ReplayEvent>
        {
            new ReplayEvent
            {
                seq = 0,
                type = ReplayEventType.SessionStart,
                timestamp = "2026-01-01T00:00:00.000Z",
            },
            new ReplayEvent
            {
                seq = 1,
                type = ReplayEventType.StartSolve,
                timestamp = "2026-01-01T00:00:01.000Z",
            },
        };

        int seq = 2;
        double baseSeconds = 1.0; // start_solve is at 1s

        for (int i = 0; i < clearCount; i++)
        {
            double t = baseSeconds + (i + 1) * interval;
            events.Add(
                new ReplayEvent
                {
                    seq = seq++,
                    type = ReplayEventType.Clear,
                    posX = i * 1f,
                    posY = i * 1f,
                    timestamp = $"2026-01-01T00:00:{t:00.000}Z",
                }
            );
        }

        double endTime = baseSeconds + clearCount * interval;
        events.Add(
            new ReplayEvent
            {
                seq = seq,
                type = ReplayEventType.EndSolve,
                timestamp = $"2026-01-01T00:00:{endTime:00.000}Z",
            }
        );

        return new ReplayData { events = events, finalTime = clearCount * interval };
    }

    /// <summary>
    /// Creates a ReplayData with clears and rejects interleaved.
    /// </summary>
    private static ReplayData MakeReplayDataWithRejects(
        int clearCount,
        int rejectCount,
        double interval
    )
    {
        var events = new List<ReplayEvent>
        {
            new ReplayEvent
            {
                seq = 0,
                type = ReplayEventType.SessionStart,
                timestamp = "2026-01-01T00:00:00.000Z",
            },
            new ReplayEvent
            {
                seq = 1,
                type = ReplayEventType.StartSolve,
                timestamp = "2026-01-01T00:00:01.000Z",
            },
        };

        int seq = 2;
        int totalEvents = clearCount + rejectCount;

        for (int i = 0; i < totalEvents; i++)
        {
            double t = 1.0 + (i + 1) * interval;
            bool isReject = i >= clearCount; // rejects come after clears
            events.Add(
                new ReplayEvent
                {
                    seq = seq++,
                    type = isReject ? ReplayEventType.Reject : ReplayEventType.Clear,
                    posX = i * 1f,
                    posY = i * 1f,
                    timestamp = $"2026-01-01T00:00:{t:00.000}Z",
                }
            );
        }

        double endTime = 1.0 + totalEvents * interval;
        events.Add(
            new ReplayEvent
            {
                seq = seq,
                type = ReplayEventType.EndSolve,
                timestamp = $"2026-01-01T00:00:{endTime:00.000}Z",
            }
        );

        return new ReplayData { events = events, finalTime = totalEvents * interval };
    }
}
