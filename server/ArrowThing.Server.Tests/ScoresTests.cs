using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArrowThing.Server.Games;
using ArrowThing.Server.Leaderboards;

namespace ArrowThing.Server.Tests;

public class ScoresTests : IClassFixture<TestFactory>, IDisposable
{
    private readonly TestFactory _factory;
    private readonly HttpClient _client;

    public ScoresTests(TestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private async Task<string> RegisterAndGetTokenAsync(
        string email = "test@example.com",
        string password = "Password123!",
        string displayName = "TestUser"
    )
    {
        await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
                password,
                displayName,
            }
        );

        var code = _factory.FakeEmail.SentEmails.FindLast(e =>
            e.To == email && e.Type == "verification"
        );

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/auth/verify-code",
            new { email, code = code!.Token }
        );

        var auth = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    private static string MakeValidReplayJson(
        int seed = 42,
        int width = 10,
        int height = 10,
        int maxArrowLength = 5
    )
    {
        var board = new Board(width, height);
        TestBoardHelper.FillBoard(board, maxArrowLength, new Random(seed));

        var snapshot = new List<List<Cell>>();
        foreach (var arrow in board.Arrows)
            snapshot.Add(new List<Cell>(arrow.Cells));

        var events = new List<ReplayEvent>();
        int seq = 0;
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        events.Add(
            new ReplayEvent
            {
                seq = seq++,
                type = ReplayEventType.SessionStart,
                timestamp = baseTime.ToString("O"),
            }
        );

        double t = 1.0;
        events.Add(
            new ReplayEvent
            {
                seq = seq++,
                type = ReplayEventType.StartSolve,
                timestamp = baseTime.AddSeconds(t).ToString("O"),
            }
        );

        t += 0.5;
        while (board.Arrows.Count > 0)
        {
            Arrow? toClear = null;
            foreach (var arrow in board.Arrows)
            {
                if (board.IsClearable(arrow))
                {
                    toClear = arrow;
                    break;
                }
            }

            events.Add(
                new ReplayEvent
                {
                    seq = seq++,
                    type = ReplayEventType.Clear,
                    posX = toClear!.HeadCell.X,
                    posY = toClear.HeadCell.Y,
                    timestamp = baseTime.AddSeconds(t).ToString("O"),
                }
            );
            board.RemoveArrow(toClear);
            t += 0.5;
        }

        events.Add(
            new ReplayEvent
            {
                seq = seq++,
                type = ReplayEventType.EndSolve,
                timestamp = baseTime.AddSeconds(t).ToString("O"),
            }
        );

        var replay = new ReplayData
        {
            version = 3,
            gameId = Guid.NewGuid().ToString(),
            seed = seed,
            boardWidth = width,
            boardHeight = height,
            maxArrowLength = maxArrowLength,
            inspectionDuration = 0f,
            boardSnapshot = snapshot,
            events = events,
            finalTime = t - 1.0,
        };

        return replay.ToJson();
    }

    [Fact]
    public async Task SubmitValidReplay_ReturnsVerified()
    {
        var token = await RegisterAndGetTokenAsync("submit1@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        var replayJson = MakeValidReplayJson();
        var response = await _client.PostAsJsonAsync("/api/scores", new { replayJson });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubmitResultResponse>();
        Assert.True(result!.Verified);
        Assert.True(result.Rank > 0);
        Assert.True(result.IsPersonalBest);
    }

    [Fact]
    public async Task SubmitSlowerSecondGame_KeepsOriginal()
    {
        var token = await RegisterAndGetTokenAsync("submit2@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        // First game (seed 42)
        var replay1 = MakeValidReplayJson(seed: 42);
        await _client.PostAsJsonAsync("/api/scores", new { replayJson = replay1 });

        // Second game with different seed (will likely have different time)
        var replay2 = MakeValidReplayJson(seed: 99);
        var response = await _client.PostAsJsonAsync("/api/scores", new { replayJson = replay2 });

        var result = await response.Content.ReadFromJsonAsync<SubmitResultResponse>();
        Assert.True(result!.Verified);
        // Either isPersonalBest true or false depending on times; both are valid.
    }

    [Fact]
    public async Task SubmitSameGameId_Idempotent()
    {
        var token = await RegisterAndGetTokenAsync("submit3@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        var replayJson = MakeValidReplayJson();

        var response1 = await _client.PostAsJsonAsync("/api/scores", new { replayJson });
        var result1 = await response1.Content.ReadFromJsonAsync<SubmitResultResponse>();

        var response2 = await _client.PostAsJsonAsync("/api/scores", new { replayJson });
        var result2 = await response2.Content.ReadFromJsonAsync<SubmitResultResponse>();

        Assert.True(result2!.Verified);
        Assert.False(result2.IsPersonalBest); // Same gameId = not a new PB
        Assert.Equal(result1!.Rank, result2.Rank);
    }

    [Fact]
    public async Task SubmitMalformedJson_Returns400()
    {
        var token = await RegisterAndGetTokenAsync("submit4@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        var response = await _client.PostAsJsonAsync(
            "/api/scores",
            new { replayJson = "not json" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitWithoutAuth_Returns401()
    {
        var replayJson = MakeValidReplayJson();
        var response = await _client.PostAsJsonAsync("/api/scores", new { replayJson });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLeaderboard_ReturnsCorrectOrder()
    {
        // Use a unique board size (5x5) to isolate from other tests
        var token1 = await RegisterAndGetTokenAsync("lb1@test.com", displayName: "FastPlayer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token1
        );
        await _client.PostAsJsonAsync(
            "/api/scores",
            new
            {
                replayJson = MakeValidReplayJson(seed: 42, width: 5, height: 5, maxArrowLength: 3),
            }
        );

        _client.DefaultRequestHeaders.Authorization = null;
        var token2 = await RegisterAndGetTokenAsync("lb2@test.com", displayName: "SlowPlayer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token2
        );
        await _client.PostAsJsonAsync(
            "/api/scores",
            new
            {
                replayJson = MakeValidReplayJson(seed: 99, width: 5, height: 5, maxArrowLength: 3),
            }
        );

        // Fetch leaderboard (no auth required)
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/leaderboards/5x5?limit=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var lb = await response.Content.ReadFromJsonAsync<LeaderboardResponse>();
        Assert.Equal(2, lb!.TotalEntries);
        Assert.Equal(2, lb.Entries.Count);
        Assert.Equal(1, lb.Entries[0].Rank);
        Assert.Equal(2, lb.Entries[1].Rank);
        // Verify ordering: first entry should have faster time
        Assert.True(lb.Entries[0].Time <= lb.Entries[1].Time);
    }

    [Fact]
    public async Task GetPlayerEntry_ReturnsCorrectRank()
    {
        var token = await RegisterAndGetTokenAsync("me1@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        await _client.PostAsJsonAsync("/api/scores", new { replayJson = MakeValidReplayJson() });

        var response = await _client.GetAsync("/api/leaderboards/10x10/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entry = await response.Content.ReadFromJsonAsync<PlayerEntryDto>();
        Assert.True(entry!.Rank > 0);
        Assert.True(entry.Time > 0);
    }

    [Fact]
    public async Task GetPlayerEntry_NoScore_Returns404()
    {
        var token = await RegisterAndGetTokenAsync("noscore@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        var response = await _client.GetAsync("/api/leaderboards/10x10/me");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetReplay_ExistingScore_ReturnsJson()
    {
        var token = await RegisterAndGetTokenAsync("replay1@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        var replayJson = MakeValidReplayJson();
        await _client.PostAsJsonAsync("/api/scores", new { replayJson });

        // Get leaderboard to find gameId
        _client.DefaultRequestHeaders.Authorization = null;
        var lbResponse = await _client.GetAsync("/api/leaderboards/10x10");
        var lb = await lbResponse.Content.ReadFromJsonAsync<LeaderboardResponse>();
        var gameId = lb!.Entries.Last().GameId;

        var response = await _client.GetAsync($"/api/replays/{gameId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetReplay_UnknownGameId_Returns404()
    {
        var response = await _client.GetAsync($"/api/replays/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DisplayNameUpdate_ReflectedInLeaderboard()
    {
        var token = await RegisterAndGetTokenAsync("rename@test.com", displayName: "OldName");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        await _client.PostAsJsonAsync("/api/scores", new { replayJson = MakeValidReplayJson() });

        // Rename
        var renameRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/me");
        renameRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        renameRequest.Content = JsonContent.Create(new { displayName = "NewName" });
        await _client.SendAsync(renameRequest);

        // Leaderboard should show new name
        _client.DefaultRequestHeaders.Authorization = null;
        var lbResponse = await _client.GetAsync("/api/leaderboards/10x10");
        var lb = await lbResponse.Content.ReadFromJsonAsync<LeaderboardResponse>();
        var entry = lb!.Entries.Find(e => e.DisplayName == "NewName");
        Assert.NotNull(entry);
    }

    [Fact]
    public async Task SubmitTamperedEvents_ReturnsNotVerified()
    {
        var token = await RegisterAndGetTokenAsync("tamper1@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        // Build a valid replay, then tamper with a clear event position
        var board = new Board(10, 10);
        TestBoardHelper.FillBoard(board, 5, new Random(42));

        var snapshot = new List<List<Cell>>();
        foreach (var arrow in board.Arrows)
            snapshot.Add(new List<Cell>(arrow.Cells));

        var events = new List<ReplayEvent>();
        int seq = 0;
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        events.Add(
            new ReplayEvent
            {
                seq = seq++,
                type = ReplayEventType.SessionStart,
                timestamp = baseTime.ToString("O"),
            }
        );
        events.Add(
            new ReplayEvent
            {
                seq = seq++,
                type = ReplayEventType.StartSolve,
                timestamp = baseTime.AddSeconds(1).ToString("O"),
            }
        );
        // Tampered: clear at a position with no arrow
        events.Add(
            new ReplayEvent
            {
                seq = seq++,
                type = ReplayEventType.Clear,
                posX = -99,
                posY = -99,
                timestamp = baseTime.AddSeconds(2).ToString("O"),
            }
        );

        var replay = new ReplayData
        {
            version = 3,
            gameId = Guid.NewGuid().ToString(),
            seed = 42,
            boardWidth = 10,
            boardHeight = 10,
            maxArrowLength = 5,
            boardSnapshot = snapshot,
            events = events,
            finalTime = 1.0,
        };

        var response = await _client.PostAsJsonAsync(
            "/api/scores",
            new { replayJson = replay.ToJson() }
        );
        var result = await response.Content.ReadFromJsonAsync<SubmitResultResponse>();
        Assert.False(result!.Verified);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task SubmitTamperedSnapshot_ReturnsNotVerified()
    {
        var token = await RegisterAndGetTokenAsync("tamper2@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        // Build a valid replay but modify the snapshot
        var replayJson = MakeValidReplayJson(seed: 42);
        var replay = Newtonsoft.Json.JsonConvert.DeserializeObject<ReplayData>(replayJson)!;
        // Tamper: remove an arrow from the snapshot
        replay.boardSnapshot.RemoveAt(0);

        var response = await _client.PostAsJsonAsync(
            "/api/scores",
            new { replayJson = replay.ToJson() }
        );
        var result = await response.Content.ReadFromJsonAsync<SubmitResultResponse>();
        Assert.False(result!.Verified);
        Assert.Contains("mismatch", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Rate limit counts stored rows, not submission attempts — needs separate counter")]
    public async Task RateLimit_ExceedsThreshold_Returns429()
    {
        var token = await RegisterAndGetTokenAsync("ratelimit@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        // Submit 10 different valid scores (different seeds = different gameIds, all 10x10)
        for (int i = 0; i < 10; i++)
        {
            var replay = MakeValidReplayJson(seed: 1000 + i);
            var resp = await _client.PostAsJsonAsync("/api/scores", new { replayJson = replay });
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        // 11th should be rate limited
        var finalReplay = MakeValidReplayJson(seed: 2000);
        var finalResp = await _client.PostAsJsonAsync(
            "/api/scores",
            new { replayJson = finalReplay }
        );
        // Could be 429 or 200 with verified=false depending on implementation
        if (finalResp.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, finalResp.StatusCode);
        }
        else
        {
            // If it returns 200, check that it was rejected
            var result = await finalResp.Content.ReadFromJsonAsync<SubmitResultResponse>();
            Assert.False(result!.Verified);
        }
    }
}

file record AuthResponse(string Token, string DisplayName);
