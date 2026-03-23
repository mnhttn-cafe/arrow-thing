using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Editor utility to test server connectivity.
/// Menu: Tools > Arrow Thing > Check Server Health
/// </summary>
public static class ServerHealthCheck
{
    [MenuItem("Tools/Arrow Thing/Check Server Health")]
    public static void CheckHealth()
    {
        var request = UnityWebRequest.Get("http://localhost:5000/health");
        request.timeout = 5;
        var op = request.SendWebRequest();
        EditorApplication.CallbackFunction callback = null;
        callback = () =>
        {
            if (!op.isDone)
                return;
            EditorApplication.update -= callback;

            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log("[ServerHealthCheck] Server is up! (200 OK)");
            else
                Debug.LogWarning($"[ServerHealthCheck] Server unreachable: {request.error}");

            request.Dispose();
        };
        EditorApplication.update += callback;
    }
}
