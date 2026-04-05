using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Result of a score submission attempt. Carries either a successful server response
/// or a user-facing error message describing what went wrong.
/// </summary>
public class SubmitResult
{
    public SubmitResultResponse Response { get; private set; }
    public string Error { get; private set; }

    public bool IsSuccess => Response != null && Response.verified;

    public static SubmitResult Success(SubmitResultResponse response) =>
        new SubmitResult { Response = response };

    public static SubmitResult Fail(string error) => new SubmitResult { Error = error };

    /// <summary>Returns a user-friendly error message based on the failure type.</summary>
    public static string DescribeError(long statusCode, string serverError)
    {
        if (statusCode == 0)
            return "No internet connection.";
        if (statusCode == 401)
            return "Session expired. Please log in again.";
        if (statusCode == 413)
            return "Replay file is too large to upload.";
        if (statusCode == 429)
            return "Too many submissions. Try again later.";
        if (statusCode >= 500)
            return "Server error. Try again later.";

        // 400-level with a server message
        if (!string.IsNullOrEmpty(serverError) && serverError != "Unknown error")
            return serverError;

        return "Could not submit score.";
    }

    /// <summary>Describes a verification failure using the server's reason field.</summary>
    public static string DescribeVerificationFailure(SubmitResultResponse response)
    {
        if (response == null)
            return "Could not submit score.";
        if (!string.IsNullOrEmpty(response.reason))
            return $"Verification failed: {response.reason}";
        return "Score could not be verified.";
    }
}

/// <summary>
/// Static helper for submitting scores to the global leaderboard.
/// Creates its own ApiClient (reads saved JWT from PlayerPrefs).
/// </summary>
public static class ScoreSubmitter
{
    /// <summary>
    /// Attempts to submit a completed replay to the server.
    /// Returns a <see cref="SubmitResult"/> with either success data or a descriptive error.
    /// </summary>
    public static async Task<SubmitResult> TrySubmitAsync(ReplayData replay)
    {
        var api = new ApiClient();
        if (!api.IsLoggedIn)
            return SubmitResult.Fail("Not logged in.");

        var replayJson = JsonConvert.SerializeObject(replay);
        var result = await api.SubmitScoreAsync(replayJson);

        if (!result.Success)
        {
            string message = SubmitResult.DescribeError(result.StatusCode, result.Error);
            Debug.LogWarning(
                $"[ScoreSubmitter] Submission failed ({result.StatusCode}): {message}"
            );
            return SubmitResult.Fail(message);
        }

        if (!result.Data.verified)
        {
            string message = SubmitResult.DescribeVerificationFailure(result.Data);
            Debug.LogWarning($"[ScoreSubmitter] Verification failed: {message}");
            return SubmitResult.Fail(message);
        }

        return SubmitResult.Success(result.Data);
    }
}
