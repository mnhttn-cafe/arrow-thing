using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Static helper for submitting scores to the global leaderboard.
/// Creates its own ApiClient (reads saved JWT from PlayerPrefs).
/// </summary>
public static class ScoreSubmitter
{
    /// <summary>
    /// Attempts to submit a completed replay to the server.
    /// Returns the server response, or null if not eligible or on failure/timeout.
    /// </summary>
    public static async Task<SubmitResultResponse> TrySubmitAsync(ReplayData replay)
    {
        var api = new ApiClient();
        if (!api.IsLoggedIn)
            return null;

        var replayJson = JsonConvert.SerializeObject(replay);
        var result = await api.SubmitScoreAsync(replayJson);

        if (!result.Success)
        {
            if (result.StatusCode != 0)
                Debug.LogWarning($"[ScoreSubmitter] Submission failed: {result.Error}");
            return null;
        }

        return result.Data;
    }
}
