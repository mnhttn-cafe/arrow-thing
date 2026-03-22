using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class ReplayPlayerTests
{
    // Lead-in offset applied to all event times
    private static readonly double L = ReplayPlayer.LeadInSeconds;
    private static readonly double E = ReplayPlayer.ExitPaddingSeconds;

    // ── Basic playback ───────────────────────────────────────────────────────

    [Test]
    public void Advance_FiresEventsInOrder()
    {
        var player = new ReplayPlayer(MakeReplayData(3, 1.0));

        // Advance past all events (lead-in + events)
        var fired = player.Advance(100.0);

        Assert.AreEqual(3, fired.Count);
        Assert.AreEqual(ReplayEventType.Clear, fired[0].type);
        Assert.AreEqual(ReplayEventType.Clear, fired[1].type);
        Assert.AreEqual(ReplayEventType.Clear, fired[2].type);
    }

    [Test]
    public void Advance_FiresOnlyEventsUpToCurrentTime()
    {
        var player = new ReplayPlayer(MakeReplayData(3, 2.0)); // events at L+2, L+4, L+6

        // Advance past the first event but not the second
        var fired1 = player.Advance(L + 3.0); // t = L+3, fires event at L+2
        Assert.AreEqual(1, fired1.Count);

        // Advance 2.5 more to t = L+5.5, fires event at L+4
        var fired2 = player.Advance(2.5);
        Assert.AreEqual(1, fired2.Count);
    }

    [Test]
    public void Advance_NothingFiresDuringLeadIn()
    {
        var player = new ReplayPlayer(MakeReplayData(3, 1.0)); // events at L+1, L+2, L+3

        // Advance only through lead-in period
        var fired = player.Advance(L - 0.01);
        Assert.AreEqual(0, fired.Count);
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
        var player = new ReplayPlayer(MakeReplayData(2, 5.0)); // events at L+5, L+10
        player.CycleSpeed(); // 2x

        // At 2x, advancing (L+5+0.5)/2 real seconds reaches just past L+5
        double realTime = (L + 5.5) / 2.0;
        var fired = player.Advance(realTime);
        Assert.AreEqual(1, fired.Count); // event at L+5
    }

    // ── Seek forward ─────────────────────────────────────────────────────────

    [Test]
    public void SeekTo_Forward_ReturnsEventsToApply()
    {
        // events at L+2.5, L+5, L+7.5, L+10; total = 10+L
        var player = new ReplayPlayer(MakeReplayData(4, 2.5));

        // Seek to a point that includes the first 2 events
        double targetTime = L + 5.5;
        double norm = targetTime / player.DisplayDuration;
        var result = player.SeekTo(norm);
        Assert.IsTrue(result.IsForward);
        Assert.AreEqual(2, result.EventsToApply.Count);
        Assert.AreEqual(0, result.EventsToUndo.Count);
    }

    [Test]
    public void SeekTo_Forward_UpdatesCurrentTime()
    {
        var player = new ReplayPlayer(MakeReplayData(2, 5.0)); // total = 10+L
        double target = L + 5.0;
        player.SeekTo(target / player.DisplayDuration);
        Assert.AreEqual(target, player.CurrentTime, 0.01);
    }

    // ── Seek backward ────────────────────────────────────────────────────────

    [Test]
    public void SeekTo_Backward_ReturnsEventsToUndo()
    {
        // events at L+2.5, L+5, L+7.5, L+10; total = 10+L
        var player = new ReplayPlayer(MakeReplayData(4, 2.5));

        // Advance past all events
        player.Advance(100.0);
        Assert.AreEqual(4, player.CurrentEventIndex);

        // Seek back to t = L+3 (only first event at L+2.5 should remain)
        double target = L + 3.0;
        var result = player.SeekTo(target / player.DisplayDuration);
        Assert.IsTrue(result.IsBackward);
        Assert.AreEqual(3, result.EventsToUndo.Count); // undo events at L+5, L+7.5, L+10
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
        // events at L+2.5, L+5, L+7.5, L+10; total = 10+L
        var player = new ReplayPlayer(MakeReplayData(4, 2.5));
        player.Advance(100.0);

        // Seek back
        player.SeekTo(0.0);
        Assert.AreEqual(0, player.CurrentEventIndex);

        // Seek forward to include first 2 events
        double target = L + 5.5;
        var result = player.SeekTo(target / player.DisplayDuration);
        Assert.IsTrue(result.IsForward);
        Assert.AreEqual(2, result.EventsToApply.Count);
    }

    // ── NormalizedTime ───────────────────────────────────────────────────────

    [Test]
    public void NormalizedTime_ProgressesDuringPlayback()
    {
        var player = new ReplayPlayer(MakeReplayData(2, 5.0));
        Assert.AreEqual(0.0, player.NormalizedTime, 0.01);

        // NormalizedTime is based on DisplayDuration, so advance half of that
        double halfDisplay = player.DisplayDuration / 2.0;
        player.Advance(halfDisplay);
        Assert.AreEqual(0.5, player.NormalizedTime, 0.01);
    }

    [Test]
    public void NormalizedTime_ClampsToOne()
    {
        var player = new ReplayPlayer(MakeReplayData(1, 5.0));
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

        // >1 normalizes to 1 (clamped to DisplayDuration)
        player.SeekTo(2.0);
        Assert.AreEqual(player.DisplayDuration, player.CurrentTime, 0.01);
    }

    [Test]
    public void TotalDuration_IncludesLeadInAndExitPadding()
    {
        var data = MakeReplayData(2, 3.0); // raw finalTime = 6
        var player = new ReplayPlayer(data);
        Assert.AreEqual(6.0 + L + E, player.TotalDuration, 0.01);
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
