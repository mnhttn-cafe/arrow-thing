using Newtonsoft.Json;

/// <summary>
/// One entry in the replay/save event log. Unused fields are omitted from JSON per event type:
/// <list type="bullet">
///   <item>session_start, session_rejoin — only timestamp is meaningful.</item>
///   <item>session_leave — t (solve elapsed snapshot) + timestamp.</item>
///   <item>start_solve — t (always 0).</item>
///   <item>clear, reject — t (solve-relative seconds) + posX/posY (world-space) + timestamp.</item>
///   <item>end_solve — t (final solve time) + timestamp.</item>
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

    /// <summary>
    /// Wall-clock time in ISO 8601 format (UTC). Used by session events, clear, and end_solve.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string timestamp;
}
