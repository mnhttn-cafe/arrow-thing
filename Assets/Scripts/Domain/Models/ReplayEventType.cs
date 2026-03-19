/// <summary>
/// String constants for replay event types. Using strings keeps the JSON human-readable
/// and compatible with Unity's JsonUtility, which serializes enums as integers.
/// </summary>
public static class ReplayEventType
{
    /// <summary>First entry of the session that generated the board.</summary>
    public const string SessionStart = "session_start";

    /// <summary>Player left the game mid-session. Carries solveElapsed snapshot.</summary>
    public const string SessionLeave = "session_leave";

    /// <summary>Player returned to a previously saved game.</summary>
    public const string SessionRejoin = "session_rejoin";

    /// <summary>Player tapped first arrow, transitioning from inspection to solve.</summary>
    public const string StartSolve = "start_solve";

    /// <summary>Player successfully cleared an arrow.</summary>
    public const string Clear = "clear";

    /// <summary>Player tapped a blocked arrow (no gameplay effect, recorded for replay fidelity).</summary>
    public const string Reject = "reject";

    /// <summary>Player cleared the last arrow on the board, ending the solve.</summary>
    public const string EndSolve = "end_solve";
}
