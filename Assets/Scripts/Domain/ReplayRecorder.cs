using System;
using System.Collections.Generic;

/// <summary>
/// Accumulates <see cref="ReplayEvent"/>s during a game session. Auto-increments seq.
/// Can be initialized from prior events to continue a resumed save.
/// Pure C# — no Unity dependency.
/// </summary>
public sealed class ReplayRecorder
{
    private int _nextSeq;
    private readonly List<ReplayEvent> _events;

    /// <summary>Start a fresh recorder for a new game.</summary>
    public ReplayRecorder()
    {
        _events = new List<ReplayEvent>();
    }

    /// <summary>
    /// Resume from a prior save's event list. New events will be appended with seq
    /// continuing from <paramref name="nextSeq"/>.
    /// </summary>
    public ReplayRecorder(List<ReplayEvent> priorEvents, int nextSeq)
    {
        _events = new List<ReplayEvent>(priorEvents);
        _nextSeq = nextSeq;
    }

    public IReadOnlyList<ReplayEvent> Events => _events;

    public void RecordSessionStart()
    {
        _events.Add(
            new ReplayEvent
            {
                seq = _nextSeq++,
                type = ReplayEventType.SessionStart,
                timestamp = DateTime.UtcNow.ToString("O"),
            }
        );
    }

    /// <summary>
    /// Records a leave event with the current solve-elapsed snapshot for timer restoration.
    /// </summary>
    public void RecordSessionLeave(double solveElapsed)
    {
        _events.Add(
            new ReplayEvent
            {
                seq = _nextSeq++,
                type = ReplayEventType.SessionLeave,
                t = solveElapsed,
                timestamp = DateTime.UtcNow.ToString("O"),
            }
        );
    }

    public void RecordSessionRejoin()
    {
        _events.Add(
            new ReplayEvent
            {
                seq = _nextSeq++,
                type = ReplayEventType.SessionRejoin,
                timestamp = DateTime.UtcNow.ToString("O"),
            }
        );
    }

    /// <param name="t">Seconds since solve start (always 0 — marks inspection→solve transition).</param>
    public void RecordStartSolve(double t)
    {
        _events.Add(
            new ReplayEvent
            {
                seq = _nextSeq++,
                type = ReplayEventType.StartSolve,
                t = t,
                timestamp = DateTime.UtcNow.ToString("O"),
            }
        );
    }

    /// <param name="t">Seconds since solve start.</param>
    public void RecordClear(double t, float posX, float posY)
    {
        _events.Add(
            new ReplayEvent
            {
                seq = _nextSeq++,
                type = ReplayEventType.Clear,
                t = t,
                posX = posX,
                posY = posY,
                timestamp = DateTime.UtcNow.ToString("O"),
            }
        );
    }

    /// <summary>Records the end of the solve (board fully cleared).</summary>
    /// <param name="t">Seconds since solve start (the final time).</param>
    public void RecordEndSolve(double t)
    {
        _events.Add(
            new ReplayEvent
            {
                seq = _nextSeq++,
                type = ReplayEventType.EndSolve,
                t = t,
                timestamp = DateTime.UtcNow.ToString("O"),
            }
        );
    }

    /// <param name="t">Seconds since solve start.</param>
    public void RecordReject(double t, float posX, float posY)
    {
        _events.Add(
            new ReplayEvent
            {
                seq = _nextSeq++,
                type = ReplayEventType.Reject,
                t = t,
                posX = posX,
                posY = posY,
                timestamp = DateTime.UtcNow.ToString("O"),
            }
        );
    }

    /// <summary>
    /// Produces a <see cref="ReplayData"/> snapshot of all accumulated events.
    /// </summary>
    /// <param name="boardSnapshot">
    /// Remaining arrows at save time. Each inner list is one arrow's cells (head to tail).
    /// Pass null only for legacy compatibility; all new saves should include a snapshot.
    /// </param>
    /// <param name="finalTime">Pass the solve elapsed at completion, or -1 for in-progress.</param>
    public ReplayData ToReplayData(
        string gameId,
        int seed,
        int boardWidth,
        int boardHeight,
        int maxArrowLength,
        float inspectionDuration,
        List<List<Cell>> boardSnapshot = null,
        double finalTime = -1.0
    )
    {
        return new ReplayData
        {
            version = 2,
            gameId = gameId,
            seed = seed,
            boardWidth = boardWidth,
            boardHeight = boardHeight,
            maxArrowLength = maxArrowLength,
            inspectionDuration = inspectionDuration,
            boardSnapshot = boardSnapshot,
            events = new List<ReplayEvent>(_events),
            finalTime = finalTime,
        };
    }
}
