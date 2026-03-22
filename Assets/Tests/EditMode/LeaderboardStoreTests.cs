using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class LeaderboardStoreTests
{
    // ── AddEntry / GetEntries ────────────────────────────────────────────────

    [Test]
    public void AddEntry_SingleEntry_CanRetrieveByBoardSize()
    {
        var store = new LeaderboardStore();
        store.AddEntry(MakeEntry("g1", 10, 10, 5.0));

        var entries = store.GetEntries(10, 10);
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("g1", entries[0].gameId);
    }

    [Test]
    public void GetEntries_FiltersCorrectly()
    {
        var store = new LeaderboardStore();
        store.AddEntry(MakeEntry("g1", 10, 10, 5.0));
        store.AddEntry(MakeEntry("g2", 20, 20, 8.0));
        store.AddEntry(MakeEntry("g3", 10, 10, 3.0));

        var small = store.GetEntries(10, 10);
        Assert.AreEqual(2, small.Count);

        var medium = store.GetEntries(20, 20);
        Assert.AreEqual(1, medium.Count);
    }

    [Test]
    public void GetEntries_EmptyForUnknownSize()
    {
        var store = new LeaderboardStore();
        store.AddEntry(MakeEntry("g1", 10, 10, 5.0));

        var entries = store.GetEntries(40, 40);
        Assert.AreEqual(0, entries.Count);
    }

    [Test]
    public void GetAllEntries_ReturnsAll()
    {
        var store = new LeaderboardStore();
        store.AddEntry(MakeEntry("g1", 10, 10, 5.0));
        store.AddEntry(MakeEntry("g2", 20, 20, 8.0));

        Assert.AreEqual(2, store.GetAllEntries().Count);
    }

    // ── PersonalBest ─────────────────────────────────────────────────────────

    [Test]
    public void GetPersonalBest_ReturnsFastest()
    {
        var store = new LeaderboardStore();
        store.AddEntry(MakeEntry("g1", 10, 10, 5.0));
        store.AddEntry(MakeEntry("g2", 10, 10, 3.0));
        store.AddEntry(MakeEntry("g3", 10, 10, 7.0));

        var best = store.GetPersonalBest(10, 10);
        Assert.AreEqual("g2", best.gameId);
        Assert.AreEqual(3.0, best.solveTime, 1e-9);
    }

    [Test]
    public void GetPersonalBest_ReturnsNull_WhenNoEntries()
    {
        var store = new LeaderboardStore();
        Assert.IsNull(store.GetPersonalBest(10, 10));
    }

    [Test]
    public void GetPersonalBest_CorrectPerConfig()
    {
        var store = new LeaderboardStore();
        store.AddEntry(MakeEntry("g1", 10, 10, 5.0));
        store.AddEntry(MakeEntry("g2", 20, 20, 2.0));

        Assert.AreEqual("g1", store.GetPersonalBest(10, 10).gameId);
        Assert.AreEqual("g2", store.GetPersonalBest(20, 20).gameId);
    }

    // ── Sorting ──────────────────────────────────────────────────────────────

    [Test]
    public void SortBy_Fastest_SortsByTimeAscending()
    {
        var entries = new List<LeaderboardEntry>
        {
            MakeEntry("g1", 10, 10, 5.0),
            MakeEntry("g2", 10, 10, 2.0),
            MakeEntry("g3", 10, 10, 8.0),
        };

        var sorted = LeaderboardStore.SortBy(entries, SortCriterion.Fastest);
        Assert.AreEqual("g2", sorted[0].gameId);
        Assert.AreEqual("g1", sorted[1].gameId);
        Assert.AreEqual("g3", sorted[2].gameId);
    }

    [Test]
    public void SortBy_Biggest_SortsByAreaDescending()
    {
        var entries = new List<LeaderboardEntry>
        {
            MakeEntry("g1", 10, 10, 5.0),
            MakeEntry("g2", 40, 40, 5.0),
            MakeEntry("g3", 20, 20, 5.0),
        };

        var sorted = LeaderboardStore.SortBy(entries, SortCriterion.Biggest);
        Assert.AreEqual("g2", sorted[0].gameId); // 1600
        Assert.AreEqual("g3", sorted[1].gameId); // 400
        Assert.AreEqual("g1", sorted[2].gameId); // 100
    }

    [Test]
    public void SortBy_Favorites_FavoritesFirstThenByTime()
    {
        var entries = new List<LeaderboardEntry>
        {
            MakeEntry("g1", 10, 10, 2.0), // fastest, not fav
            MakeEntry("g2", 10, 10, 5.0, isFavorite: true), // fav, slow
            MakeEntry("g3", 10, 10, 3.0, isFavorite: true), // fav, fast
            MakeEntry("g4", 10, 10, 1.0), // fastest overall, not fav
        };

        var sorted = LeaderboardStore.SortBy(entries, SortCriterion.Favorites);
        // Favorites first, sorted by time
        Assert.AreEqual("g3", sorted[0].gameId);
        Assert.AreEqual("g2", sorted[1].gameId);
        // Non-favorites, sorted by time
        Assert.AreEqual("g4", sorted[2].gameId);
        Assert.AreEqual("g1", sorted[3].gameId);
    }

    [Test]
    public void SortBy_DoesNotMutateOriginalList()
    {
        var entries = new List<LeaderboardEntry>
        {
            MakeEntry("g1", 10, 10, 5.0),
            MakeEntry("g2", 10, 10, 2.0),
        };

        LeaderboardStore.SortBy(entries, SortCriterion.Fastest);
        Assert.AreEqual("g1", entries[0].gameId); // original unchanged
    }

    // ── Cap enforcement ──────────────────────────────────────────────────────

    [Test]
    public void PerConfigCap_PrunesSlowestNonFavorited()
    {
        var store = new LeaderboardStore();

        // Fill to cap
        for (int i = 0; i < LeaderboardStore.MaxEntriesPerConfig; i++)
            store.AddEntry(MakeEntry($"g{i}", 10, 10, i + 1.0)); // times: 1, 2, ..., 50

        Assert.AreEqual(LeaderboardStore.MaxEntriesPerConfig, store.GetEntries(10, 10).Count);

        // Adding one more should prune the slowest (time = 50)
        string pruned = store.AddEntry(MakeEntry("gNew", 10, 10, 0.5));

        Assert.AreEqual(LeaderboardStore.MaxEntriesPerConfig, store.GetEntries(10, 10).Count);
        Assert.AreEqual("g49", pruned); // slowest was g49 (time = 50)
    }

    [Test]
    public void PerConfigCap_SkipsFavoritedEntries()
    {
        var store = new LeaderboardStore();

        for (int i = 0; i < LeaderboardStore.MaxEntriesPerConfig; i++)
            store.AddEntry(MakeEntry($"g{i}", 10, 10, i + 1.0));

        // Favorite the slowest
        store.SetFavorite("g49", true);

        // Adding one more should prune the next slowest (g48, time = 49)
        string pruned = store.AddEntry(MakeEntry("gNew", 10, 10, 0.5));

        Assert.AreEqual(LeaderboardStore.MaxEntriesPerConfig, store.GetEntries(10, 10).Count);
        Assert.AreEqual("g48", pruned);
        // g49 should still exist (favorited)
        Assert.IsNotNull(store.GetEntries(10, 10).Find(e => e.gameId == "g49"));
    }

    // ── Favorites ────────────────────────────────────────────────────────────

    [Test]
    public void SetFavorite_TogglesFlag()
    {
        var store = new LeaderboardStore();
        store.AddEntry(MakeEntry("g1", 10, 10, 5.0));

        Assert.IsFalse(store.GetEntries(10, 10)[0].isFavorite);

        store.SetFavorite("g1", true);
        Assert.IsTrue(store.GetEntries(10, 10).Find(e => e.gameId == "g1").isFavorite);

        store.SetFavorite("g1", false);
        Assert.IsFalse(store.GetEntries(10, 10).Find(e => e.gameId == "g1").isFavorite);
    }

    [Test]
    public void SetFavorite_UnknownGameId_NoError()
    {
        var store = new LeaderboardStore();
        Assert.DoesNotThrow(() => store.SetFavorite("nonexistent", true));
    }

    // ── RemoveEntry ──────────────────────────────────────────────────────────

    [Test]
    public void RemoveEntry_RemovesAndReturnsTrue()
    {
        var store = new LeaderboardStore();
        store.AddEntry(MakeEntry("g1", 10, 10, 5.0));

        Assert.IsTrue(store.RemoveEntry("g1"));
        Assert.AreEqual(0, store.GetEntries(10, 10).Count);
    }

    [Test]
    public void RemoveEntry_UnknownId_ReturnsFalse()
    {
        var store = new LeaderboardStore();
        Assert.IsFalse(store.RemoveEntry("nonexistent"));
    }

    // ── GetNeighborEntries ───────────────────────────────────────────────────

    [Test]
    public void GetNeighborEntries_CentersAroundTargetTime()
    {
        var store = new LeaderboardStore();
        for (int i = 1; i <= 10; i++)
            store.AddEntry(MakeEntry($"g{i}", 10, 10, i * 1.0));

        // Target time 5.5 — should center around entries with times 4, 5, 6, 7, 8 (indices 3-7)
        var neighbors = store.GetNeighborEntries(10, 10, 5.5, 5);
        Assert.AreEqual(5, neighbors.Count);
        Assert.AreEqual(4.0, neighbors[0].solveTime, 1e-9);
        Assert.AreEqual(8.0, neighbors[4].solveTime, 1e-9);
    }

    [Test]
    public void GetNeighborEntries_ClampsToStart()
    {
        var store = new LeaderboardStore();
        for (int i = 1; i <= 10; i++)
            store.AddEntry(MakeEntry($"g{i}", 10, 10, i * 1.0));

        var neighbors = store.GetNeighborEntries(10, 10, 0.5, 5);
        Assert.AreEqual(5, neighbors.Count);
        Assert.AreEqual(1.0, neighbors[0].solveTime, 1e-9);
    }

    [Test]
    public void GetNeighborEntries_ClampsToEnd()
    {
        var store = new LeaderboardStore();
        for (int i = 1; i <= 10; i++)
            store.AddEntry(MakeEntry($"g{i}", 10, 10, i * 1.0));

        var neighbors = store.GetNeighborEntries(10, 10, 100.0, 5);
        Assert.AreEqual(5, neighbors.Count);
        Assert.AreEqual(10.0, neighbors[4].solveTime, 1e-9);
    }

    [Test]
    public void GetNeighborEntries_FewerThanCount_ReturnsAll()
    {
        var store = new LeaderboardStore();
        store.AddEntry(MakeEntry("g1", 10, 10, 3.0));
        store.AddEntry(MakeEntry("g2", 10, 10, 7.0));

        var neighbors = store.GetNeighborEntries(10, 10, 5.0, 5);
        Assert.AreEqual(2, neighbors.Count);
    }

    // ── Serialization ────────────────────────────────────────────────────────

    [Test]
    public void ToJson_FromJson_Roundtrips()
    {
        var store = new LeaderboardStore();
        store.AddEntry(MakeEntry("g1", 10, 10, 5.0));
        store.AddEntry(MakeEntry("g2", 20, 20, 3.0, isFavorite: true));

        string json = store.ToJson();
        var restored = LeaderboardStore.FromJson(json);

        Assert.AreEqual(2, restored.Entries.Count);
        Assert.AreEqual("g1", restored.Entries[0].gameId);
        Assert.AreEqual(5.0, restored.Entries[0].solveTime, 1e-9);
        Assert.IsTrue(restored.Entries[1].isFavorite);
    }

    [Test]
    public void FromJson_NullOrEmpty_ReturnsEmptyStore()
    {
        Assert.AreEqual(0, LeaderboardStore.FromJson(null).Entries.Count);
        Assert.AreEqual(0, LeaderboardStore.FromJson("").Entries.Count);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LeaderboardEntry MakeEntry(
        string gameId,
        int width,
        int height,
        double solveTime,
        bool isFavorite = false
    )
    {
        return new LeaderboardEntry
        {
            gameId = gameId,
            seed = 42,
            boardWidth = width,
            boardHeight = height,
            solveTime = solveTime,
            completedAt = "2026-01-01T00:00:00Z",
            isFavorite = isFavorite,
            gameVersion = "1.0.0",
        };
    }
}
