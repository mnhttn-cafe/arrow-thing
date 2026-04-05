using System.Collections.Concurrent;

namespace ArrowThing.Server.Leaderboards;

/// <summary>
/// Singleton in-memory cache for global leaderboard responses.
/// Invalidated per board size when a top-50 score changes.
/// </summary>
public class LeaderboardCache
{
    private readonly ConcurrentDictionary<string, CachedLeaderboard> _cache = new();

    public LeaderboardResponse? Get(int width, int height)
    {
        var key = Key(width, height);
        if (_cache.TryGetValue(key, out var cached))
            return cached.Response;
        return null;
    }

    public void Set(int width, int height, LeaderboardResponse response)
    {
        _cache[Key(width, height)] = new CachedLeaderboard(response);
    }

    /// <summary>
    /// Invalidates the cache for a specific board size.
    /// Also invalidates the "all" leaderboard since it aggregates across sizes.
    /// </summary>
    public void Invalidate(int width, int height)
    {
        _cache.TryRemove(Key(width, height), out _);
        _cache.TryRemove("all", out _);
    }

    /// <summary>Invalidates all cached leaderboards.</summary>
    public void InvalidateAll()
    {
        _cache.Clear();
    }

    private static string Key(int width, int height) =>
        width == 0 && height == 0 ? "all" : $"{width}x{height}";

    private record CachedLeaderboard(LeaderboardResponse Response);
}
