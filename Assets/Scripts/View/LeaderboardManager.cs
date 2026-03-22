using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// View-layer singleton that wraps <see cref="LeaderboardStore"/> with file-based persistence.
/// Index stored as <c>leaderboard.json</c>; replays stored individually as GZip-compressed
/// JSON at <c>replays/{gameId}.json.gz</c>. Lives across scenes via DontDestroyOnLoad.
/// </summary>
public sealed class LeaderboardManager : MonoBehaviour
{
    private const string IndexFileName = "leaderboard.json";
    private const string ReplayDirectory = "replays";

    private static LeaderboardManager _instance;
    public static LeaderboardManager Instance => _instance;

    private LeaderboardStore _store;
    public LeaderboardStore Store => _store;

    private string IndexPath => Path.Combine(Application.persistentDataPath, IndexFileName);
    private string ReplayDir => Path.Combine(Application.persistentDataPath, ReplayDirectory);

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void SyncFilesystem();
#endif

    private static void SyncFS()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SyncFilesystem();
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null)
            return;
        var go = new GameObject("LeaderboardManager");
        go.AddComponent<LeaderboardManager>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadIndex();
    }

    /// <summary>
    /// Records a completed game result. Builds a <see cref="LeaderboardEntry"/>,
    /// adds it to the store, saves the index, and writes the replay file.
    /// Returns the created entry.
    /// </summary>
    public LeaderboardEntry RecordResult(ReplayData replayData)
    {
        var entry = new LeaderboardEntry(replayData, replayData.gameVersion ?? Application.version);
        string pruned = _store.AddEntry(entry);

        SaveIndex();
        SaveReplay(entry.gameId, replayData);

        if (pruned != null)
            DeleteReplay(pruned);

        return entry;
    }

    /// <summary>
    /// Returns true if the given time beats the current personal best for this board size.
    /// Also returns true if there is no existing entry (first play is always a PB).
    /// </summary>
    public bool IsPersonalBest(int width, int height, double time)
    {
        var best = _store.GetPersonalBest(width, height);
        return best == null || time < best.solveTime;
    }

    public void SetFavorite(string gameId, bool isFavorite)
    {
        _store.SetFavorite(gameId, isFavorite);
        SaveIndex();
    }

    public void RemoveEntry(string gameId)
    {
        _store.RemoveEntry(gameId);
        SaveIndex();
        DeleteReplay(gameId);
    }

    public void RemoveAllNonFavorited()
    {
        var removed = _store.RemoveAllNonFavorited();
        SaveIndex();
        foreach (var gameId in removed)
            DeleteReplay(gameId);
    }

    /// <summary>
    /// Loads a replay from disk. Returns null if the file is missing or corrupted.
    /// Tries GZip first, then falls back to plain JSON for backwards compatibility.
    /// </summary>
    public ReplayData LoadReplay(string gameId)
    {
        string gzPath = Path.Combine(ReplayDir, gameId + ".json.gz");
        string plainPath = Path.Combine(ReplayDir, gameId + ".json");

        // Try compressed first
        if (File.Exists(gzPath))
        {
            try
            {
                byte[] compressed = File.ReadAllBytes(gzPath);
                string json = DecompressGZip(compressed);
                return JsonConvert.DeserializeObject<ReplayData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"LeaderboardManager: failed to load replay {gameId} — {e.Message}"
                );
                return null;
            }
        }

        // Fall back to plain JSON
        if (File.Exists(plainPath))
        {
            try
            {
                string json = File.ReadAllText(plainPath);
                return JsonConvert.DeserializeObject<ReplayData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"LeaderboardManager: failed to load plain replay {gameId} — {e.Message}"
                );
                return null;
            }
        }

        return null;
    }

    // --- Persistence helpers ---

    private void LoadIndex()
    {
        string path = IndexPath;
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                _store = LeaderboardStore.FromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"LeaderboardManager: failed to load index — {e.Message}. Starting fresh."
                );
                _store = new LeaderboardStore();
            }
        }
        else
        {
            _store = new LeaderboardStore();
        }
    }

    private void SaveIndex()
    {
        try
        {
            string json = _store.ToJson();
            File.WriteAllText(IndexPath, json);
            SyncFS();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LeaderboardManager: failed to save index — {e.Message}");
        }
    }

    private void SaveReplay(string gameId, ReplayData data)
    {
        try
        {
            if (!Directory.Exists(ReplayDir))
                Directory.CreateDirectory(ReplayDir);

            string json = JsonConvert.SerializeObject(data);
            byte[] compressed = CompressGZip(json);
            File.WriteAllBytes(Path.Combine(ReplayDir, gameId + ".json.gz"), compressed);
            SyncFS();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LeaderboardManager: failed to save replay {gameId} — {e.Message}");
        }
    }

    private void DeleteReplay(string gameId)
    {
        try
        {
            string gzPath = Path.Combine(ReplayDir, gameId + ".json.gz");
            string plainPath = Path.Combine(ReplayDir, gameId + ".json");

            if (File.Exists(gzPath))
                File.Delete(gzPath);
            if (File.Exists(plainPath))
                File.Delete(plainPath);

            SyncFS();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LeaderboardManager: failed to delete replay {gameId} — {e.Message}");
        }
    }

    private static byte[] CompressGZip(string text)
    {
        byte[] raw = System.Text.Encoding.UTF8.GetBytes(text);
        using (var output = new MemoryStream())
        {
            using (var gz = new GZipStream(output, CompressionMode.Compress))
            {
                gz.Write(raw, 0, raw.Length);
            }
            return output.ToArray();
        }
    }

    private static string DecompressGZip(byte[] compressed)
    {
        using (var input = new MemoryStream(compressed))
        using (var gz = new GZipStream(input, CompressionMode.Decompress))
        using (var reader = new StreamReader(gz, System.Text.Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }
}
