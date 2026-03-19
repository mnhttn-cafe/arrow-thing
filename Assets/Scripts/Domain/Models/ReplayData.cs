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

    /// <summary>
    /// Snapshot of all remaining arrows at save time (version 2+). Each inner list is
    /// one arrow's cells in head-to-tail order. Null for v1 legacy saves — resume falls
    /// back to seed-based regeneration when this is absent.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<List<Cell>> boardSnapshot;

    /// <summary>Ordered event log (by seq). Never null; always at least one session_start.</summary>
    public List<ReplayEvent> events = new List<ReplayEvent>();

    /// <summary>
    /// Solve time in seconds at board completion. -1 if the game is still in progress.
    /// </summary>
    public double finalTime = -1.0;
}
