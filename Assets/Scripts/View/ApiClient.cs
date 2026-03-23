using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// HTTP client wrapper for the Arrow Thing API.
/// Handles base URL configuration, JWT attachment, and error handling.
/// </summary>
public class ApiClient
{
    private const string DefaultBaseUrl = "https://api.arrow-thing.com";
    private const string LocalBaseUrl = "http://localhost:5000";

    private readonly string _baseUrl;

    public ApiClient()
    {
#if UNITY_EDITOR
        _baseUrl = LocalBaseUrl;
#else
        _baseUrl = DefaultBaseUrl;
#endif
    }

    /// <summary>
    /// Checks server connectivity by hitting the health endpoint.
    /// Returns true if the server responds with 200.
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            using var request = UnityWebRequest.Get($"{_baseUrl}/health");
            request.timeout = 5;
            var op = request.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            return request.result == UnityWebRequest.Result.Success;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ApiClient] Health check failed: {e.Message}");
            return false;
        }
    }
}
