using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArrowThing.Server.Auth;

namespace ArrowThing.Server.Tests;

public class AuthTests : IClassFixture<TestFactory>, IDisposable
{
    private readonly HttpClient _client;

    public AuthTests(TestFactory factory)
    {
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    // -- Register --

    [Fact]
    public async Task Register_ValidRequest_ReturnsTokenAndDisplayName()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = "testuser",
                password = "password123",
                displayName = "Test User",
            }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.Token));
        Assert.Equal("Test User", body.DisplayName);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns409()
    {
        await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = "dupeuser",
                password = "password123",
                displayName = "First",
            }
        );

        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = "dupeuser",
                password = "password456",
                displayName = "Second",
            }
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateUsernameCaseInsensitive_Returns409()
    {
        await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = "CaseUser",
                password = "password123",
                displayName = "First",
            }
        );

        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = "caseuser",
                password = "password456",
                displayName = "Second",
            }
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("ab", "password123", "Name")] // username too short
    [InlineData("valid_user", "short", "Name")] // password too short
    [InlineData("valid_user", "password123", "X")] // display name too short
    [InlineData("bad user!", "password123", "Name")] // username invalid chars
    public async Task Register_InvalidInput_Returns400(
        string username,
        string password,
        string displayName
    )
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username,
                password,
                displayName,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Login --

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = "loginuser",
                password = "password123",
                displayName = "Login User",
            }
        );

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "loginuser", password = "password123" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.Token));
        Assert.Equal("Login User", body.DisplayName);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = "wrongpwuser",
                password = "password123",
                displayName = "User",
            }
        );

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "wrongpwuser", password = "wrongpassword" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_NonexistentUser_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "noexist", password = "password123" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -- Protected endpoint (PATCH /api/auth/me) --

    [Fact]
    public async Task UpdateDisplayName_WithToken_Succeeds()
    {
        // Register and get token
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = "dnuser",
                password = "password123",
                displayName = "Old Name",
            }
        );
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Update display name with token
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/me")
        {
            Content = JsonContent.Create(new { displayName = "New Name" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DisplayNameResponse>();
        Assert.Equal("New Name", body!.DisplayName);
    }

    [Fact]
    public async Task UpdateDisplayName_WithoutToken_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/me")
        {
            Content = JsonContent.Create(new { displayName = "New Name" }),
        };

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDisplayName_InvalidName_Returns400()
    {
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = "dnvaluser",
                password = "password123",
                displayName = "Valid",
            }
        );
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/me")
        {
            Content = JsonContent.Create(new { displayName = "X" }), // too short
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
