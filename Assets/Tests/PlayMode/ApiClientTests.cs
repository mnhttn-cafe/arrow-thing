using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public class ApiClientTests
{
    [UnityTest, Explicit("Requires local server running on localhost:5000")]
    public IEnumerator HealthCheck_ReturnsTrue_WhenServerIsRunning()
    {
        var client = new ApiClient();
        var task = client.HealthCheckAsync();

        while (!task.IsCompleted)
            yield return null;

        // This test requires the local server to be running (dotnet run).
        // It will fail in CI where no server is present — mark as explicit
        // once we have CI for the Unity side, or gate on server availability.
        Assert.IsTrue(
            task.Result,
            "Server health check failed — is the server running on localhost:5000?"
        );
    }
}
