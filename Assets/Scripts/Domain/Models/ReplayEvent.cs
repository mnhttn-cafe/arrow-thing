using System;

/// <summary>
/// One entry in the replay/save event log. Unused fields default to 0 / null per event type:
/// <list type="bullet">
///   <item>session_start, session_rejoin — only wallTime is meaningful.</item>
///   <item>session_leave — wallTime + solveElapsed.</item>
///   <item>start_solve, clear, reject — t (solve-relative seconds) + posX/posY (world-space).</item>
/// </list>
/// </summary>
[Serializable]
public sealed class ReplayEvent
{
    /// <summary>Monotonically increasing. Defines event order; timestamps can tie.</summary>
    public int seq;

    /// <summary>Event type — one of the <see cref="ReplayEventType"/> string constants.</summary>
    public string type;

    /// <summary>
    /// Seconds since solve start. Used by start_solve, clear, and reject.
    /// </summary>
    public double t;

    /// <summary>
    /// Snapshot of SolveElapsed at the moment the player left. Used by session_leave.
    /// </summary>
    public double solveElapsed;

    /// <summary>World-space X of the tap. Used by start_solve, clear, and reject.</summary>
    public float posX;

    /// <summary>World-space Y of the tap. Used by start_solve, clear, and reject.</summary>
    public float posY;

    /// <summary>
    /// Wall-clock time in ISO 8601 format (UTC). Used by session events.
    /// </summary>
    public string wallTime;
}
