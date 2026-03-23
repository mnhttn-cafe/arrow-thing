using System;
using System.Text;
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
    private const string TokenPrefKey = "auth_token";
    private const string DisplayNamePrefKey = "auth_display_name";

    private readonly string _baseUrl;

    public string Token { get; private set; }
    public string DisplayName { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    public ApiClient()
    {
#if UNITY_EDITOR
        _baseUrl = LocalBaseUrl;
#else
        _baseUrl = DefaultBaseUrl;
#endif
        // Restore saved session
        Token = PlayerPrefs.GetString(TokenPrefKey, "");
        DisplayName = PlayerPrefs.GetString(DisplayNamePrefKey, "");
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

    public async Task<ApiResult<AuthResponse>> RegisterAsync(
        string username,
        string password,
        string displayName
    )
    {
        var body = JsonUtility.ToJson(
            new RegisterRequest
            {
                username = username,
                password = password,
                displayName = displayName,
            }
        );
        return await PostAuthAsync("/api/auth/register", body);
    }

    public async Task<ApiResult<AuthResponse>> LoginAsync(string username, string password)
    {
        var body = JsonUtility.ToJson(
            new LoginRequest { username = username, password = password }
        );
        return await PostAuthAsync("/api/auth/login", body);
    }

    public async Task<ApiResult<DisplayNameResponse>> UpdateDisplayNameAsync(string displayName)
    {
        var body = JsonUtility.ToJson(new UpdateDisplayNameRequest { displayName = displayName });

        try
        {
            using var request = new UnityWebRequest($"{_baseUrl}/api/auth/me", "PATCH");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {Token}");
            request.timeout = 10;

            var op = request.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<DisplayNameResponse>(
                    request.downloadHandler.text
                );
                DisplayName = response.displayName;
                PlayerPrefs.SetString(DisplayNamePrefKey, DisplayName);
                return ApiResult<DisplayNameResponse>.Ok(response);
            }

            var error = TryParseError(request.downloadHandler.text);
            return ApiResult<DisplayNameResponse>.Fail(request.responseCode, error);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ApiClient] UpdateDisplayName failed: {e.Message}");
            return ApiResult<DisplayNameResponse>.Fail(0, "Network error");
        }
    }

    public void Logout()
    {
        Token = "";
        DisplayName = "";
        PlayerPrefs.DeleteKey(TokenPrefKey);
        PlayerPrefs.DeleteKey(DisplayNamePrefKey);
    }

    private async Task<ApiResult<AuthResponse>> PostAuthAsync(string path, string jsonBody)
    {
        try
        {
            using var request = new UnityWebRequest($"{_baseUrl}{path}", "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            var op = request.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                Token = response.token;
                DisplayName = response.displayName;
                PlayerPrefs.SetString(TokenPrefKey, Token);
                PlayerPrefs.SetString(DisplayNamePrefKey, DisplayName);
                return ApiResult<AuthResponse>.Ok(response);
            }

            var error = TryParseError(request.downloadHandler.text);
            return ApiResult<AuthResponse>.Fail(request.responseCode, error);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ApiClient] Auth request failed: {e.Message}");
            return ApiResult<AuthResponse>.Fail(0, "Network error");
        }
    }

    private static string TryParseError(string responseBody)
    {
        if (string.IsNullOrEmpty(responseBody))
            return "Unknown error";
        try
        {
            var err = JsonUtility.FromJson<ErrorResponse>(responseBody);
            return string.IsNullOrEmpty(err.error) ? "Unknown error" : err.error;
        }
        catch
        {
            return "Unknown error";
        }
    }

    // DTOs (JsonUtility requires serializable classes with public fields)

    [Serializable]
    private class RegisterRequest
    {
        public string username;
        public string password;
        public string displayName;
    }

    [Serializable]
    private class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    private class UpdateDisplayNameRequest
    {
        public string displayName;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string error;
    }
}

[Serializable]
public class AuthResponse
{
    public string token;
    public string displayName;
}

[Serializable]
public class DisplayNameResponse
{
    public string displayName;
}

public class ApiResult<T>
{
    public bool Success { get; private set; }
    public T Data { get; private set; }
    public long StatusCode { get; private set; }
    public string Error { get; private set; }

    public static ApiResult<T> Ok(T data) => new ApiResult<T> { Success = true, Data = data };

    public static ApiResult<T> Fail(long statusCode, string error) =>
        new ApiResult<T>
        {
            Success = false,
            StatusCode = statusCode,
            Error = error,
        };
}
