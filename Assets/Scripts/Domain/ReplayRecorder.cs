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
        _events.Add(new ReplayEvent
        {
            seq = _nextSeq++,
            type = ReplayEventType.SessionStart,
            wallTime = DateTime.UtcNow.ToString("O"),
        });
    }

    /// <summary>
    /// Records a leave event with the current solve-elapsed snapshot for timer restoration.
    /// </summary>
    public void RecordSessionLeave(double solveElapsed)
    {
        _events.Add(new ReplayEvent
        {
            seq = _nextSeq++,
            type = ReplayEventType.SessionLeave,
            solveElapsed = solveElapsed,
            wallTime = DateTime.UtcNow.ToString("O"),
        });
    }

    public void RecordSessionRejoin()
    {
        _events.Add(new ReplayEvent
        {
            seq = _nextSeq++,
            type = ReplayEventType.SessionRejoin,
            wallTime = DateTime.UtcNow.ToString("O"),
        });
    }

    /// <param name="t">Seconds since solve start.</param>
    /// <param name="posX">World-space X of the tap.</param>
    /// <param name="posY">World-space Y of the tap.</param>
    public void RecordStartSolve(double t, float posX, float posY)
    {
        _events.Add(new ReplayEvent
        {
            seq = _nextSeq++,
            type = ReplayEventType.StartSolve,
            t = t,
            posX = posX,
            posY = posY,
        });
    }

    /// <param name="t">Seconds since solve start.</param>
    public void RecordClear(double t, float posX, float posY)
    {
        _events.Add(new ReplayEvent
        {
            seq = _nextSeq++,
            type = ReplayEventType.Clear,
            t = t,
            posX = posX,
            posY = posY,
        });
    }

    /// <param name="t">Seconds since solve start.</param>
    public void RecordReject(double t, float posX, float posY)
    {
        _events.Add(new ReplayEvent
        {
            seq = _nextSeq++,
            type = ReplayEventType.Reject,
            t = t,
            posX = posX,
            posY = posY,
        });
    }

    /// <summary>
    /// Produces a <see cref="ReplayData"/> snapshot of all accumulated events.
    /// </summary>
    /// <param name="finalTime">Pass the solve elapsed at completion, or -1 for in-progress.</param>
    public ReplayData ToReplayData(
        string gameId,
        int seed,
        int boardWidth,
        int boardHeight,
        int maxArrowLength,
        float inspectionDuration,
        double finalTime = -1.0
    )
    {
        return new ReplayData
        {
            version = 1,
            gameId = gameId,
            seed = seed,
            boardWidth = boardWidth,
            boardHeight = boardHeight,
            maxArrowLength = maxArrowLength,
            inspectionDuration = inspectionDuration,
            events = new List<ReplayEvent>(_events),
            finalTime = finalTime,
        };
    }
}
