using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Full save / replay record for a single game session.
/// Serializes to JSON via Newtonsoft.Json.
/// A <see cref="finalTime"/> of -1 indicates an in-progress (incomplete) game.
/// </summary>
public sealed class ReplayData
{
    /// <summary>Format version — increment if the schema changes incompatibly.</summary>
    public int version = 1;

    /// <summary>UUID string. Uniquely identifies this game session.</summary>
    public string gameId;

    public int seed;
    public int boardWidth;
    public int boardHeight;
    public int maxArrowLength;

    /// <summary>Inspection phase duration in seconds, as configured when the board was created.</summary>
    public float inspectionDuration;

    /// <summary>Application version at the time of recording (version 3+). Null for older replays.</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string gameVersion;

    /// <summary>
    /// Initial arrow configuration — all arrows on the board before any clears (version 2+).
    /// Each inner list is one arrow's cells in head-to-tail order. On resume, the board is
    /// restored from this snapshot and clear events are replayed to reconstruct current state.
    /// Null for v1 legacy saves — resume falls back to seed-based regeneration.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<List<Cell>> boardSnapshot;

    /// <summary>Ordered event log (by seq). Never null; always at least one session_start.</summary>
    public List<ReplayEvent> events = new List<ReplayEvent>();

    /// <summary>
    /// Solve time in seconds at board completion. -1 if the game is still in progress.
    /// </summary>
    public double finalTime = -1.0;

    /// <summary>
    /// Computes solve elapsed time in seconds from event timestamps.
    /// Sets a checkpoint at start_solve, accumulates elapsed on session_leave / end_solve,
    /// resets checkpoint on session_rejoin. Time between session_leave and session_rejoin
    /// is excluded. Returns 0 if no start_solve event is found.
    /// If the event stream ends without a closing event (session_leave / end_solve),
    /// includes time up to the last recorded event (handles autosaves and force-quits).
    /// </summary>
    [JsonIgnore]
    public double ComputedSolveElapsed
    {
        get
        {
            double elapsed = 0.0;
            DateTime checkpoint = DateTime.MinValue;
            DateTime lastEventTime = DateTime.MinValue;
            bool paused = false;
            bool finished = false;

            foreach (var evt in events)
            {
                var ts = DateTime.Parse(evt.timestamp).ToUniversalTime();

                switch (evt.type)
                {
                    case ReplayEventType.StartSolve:
                        checkpoint = ts;
                        paused = false;
                        break;
                    case ReplayEventType.SessionRejoin:
                        if (!paused && checkpoint != DateTime.MinValue)
                        {
                            // Orphan rejoin: accumulate up to last event, skip the gap.
                            elapsed += (lastEventTime - checkpoint).TotalSeconds;
                        }
                        checkpoint = ts;
                        paused = false;
                        break;
                    case ReplayEventType.SessionLeave:
                        elapsed += (ts - checkpoint).TotalSeconds;
                        paused = true;
                        break;
                    case ReplayEventType.EndSolve:
                        if (!paused)
                            elapsed += (ts - checkpoint).TotalSeconds;
                        finished = true;
                        break;
                }
                lastEventTime = ts;
            }

            // Include time from an unterminated session (autosave or force-quit
            // without session_leave). Without this, resuming from an autosave
            // would lose all solve time from the current session.
            if (!paused && !finished && checkpoint != DateTime.MinValue && lastEventTime != DateTime.MinValue)
                elapsed += (lastEventTime - checkpoint).TotalSeconds;

            return elapsed;
        }
    }

    /// <summary>Serializes this instance to a JSON string.</summary>
    public string ToJson() => JsonConvert.SerializeObject(this);
}
