using Newtonsoft.Json;

/// <summary>
/// One entry in the replay/save event log. Unused fields are omitted from JSON per event type:
/// <list type="bullet">
///   <item>All events carry seq, type, and timestamp.</item>
///   <item>session_start, session_rejoin — timestamp only (t is 0).</item>
///   <item>session_leave — t (solve elapsed snapshot).</item>
///   <item>start_solve — t (always 0).</item>
///   <item>clear, reject — t (solve-relative seconds) + posX/posY (world-space).</item>
///   <item>end_solve — t (final solve time).</item>
/// </list>
/// </summary>
public sealed class ReplayEvent
{
    /// <summary>Monotonically increasing. Defines event order; timestamps can tie.</summary>
    public int seq;

    /// <summary>Event type — one of the <see cref="ReplayEventType"/> string constants.</summary>
    public string type;

    /// <summary>
    /// Seconds since solve start. Meaning varies by event type:
    /// solve elapsed snapshot for session_leave; solve-relative time for clear/reject/end_solve;
    /// always 0 for start_solve.
    /// </summary>
    public double t;

    /// <summary>World-space X of the tap. Used by clear and reject.</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? posX;

    /// <summary>World-space Y of the tap. Used by clear and reject.</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? posY;

    /// <summary>Wall-clock time in ISO 8601 format (UTC). Present on all events.</summary>
    public string timestamp;
}
