using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Saves and loads the in-progress game to a JSON file in <see cref="Application.persistentDataPath"/>.
/// On WebGL this maps to the browser's IndexedDB. One save slot: the file is overwritten on each save.
/// </summary>
public static class SaveManager
{
    private const string FileName = "savegame.json";

    private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void SyncFilesystem();
#endif

    /// <summary>
    /// Flushes the in-memory Emscripten filesystem to IndexedDB on WebGL so saves
    /// survive page refresh. No-op on all other platforms.
    /// </summary>
    private static void SyncFS()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SyncFilesystem();
#endif
    }

    /// <summary>Returns true if a save file currently exists on disk.</summary>
    public static bool HasSave() => File.Exists(SavePath);

    /// <summary>
    /// Loads and deserializes the save file.
    /// Returns null if the file is missing or corrupted.
    /// </summary>
    public static ReplayData Load()
    {
        string path = SavePath;
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<ReplayData>(json);
            if (data == null || data.events == null)
            {
                Debug.LogWarning("SaveManager: save file is corrupted — deleting.");
                Delete();
                return null;
            }
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"SaveManager: failed to load save — {e.Message}. Deleting.");
            Delete();
            return null;
        }
    }

    /// <summary>
    /// Coroutine that loads the save file without blocking the main thread.
    /// On standalone platforms, file I/O and deserialization run on a background
    /// thread. On WebGL (single-threaded), falls back to synchronous load.
    /// Invokes <paramref name="onComplete"/> with the result (null if missing/corrupted).
    /// </summary>
    public static IEnumerator LoadAsync(System.Action<ReplayData> onComplete)
    {
        string path = SavePath;
        if (!File.Exists(path))
        {
            onComplete?.Invoke(null);
            yield break;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL is single-threaded; IndexedDB reads are fast (already in memory)
        onComplete?.Invoke(Load());
#else
        ReplayData result = null;
        bool failed = false;
        var task = Task.Run(() =>
        {
            try
            {
                string json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<ReplayData>(json);
                if (data?.events == null)
                    failed = true;
                else
                    result = data;
            }
            catch
            {
                failed = true;
            }
        });

        // Always yield at least one frame so the caller's UI can render
        yield return null;
        while (!task.IsCompleted)
            yield return null;

        if (failed)
        {
            Debug.LogWarning("SaveManager: save file is corrupted — deleting.");
            Delete();
        }

        onComplete?.Invoke(result);
#endif
    }

    /// <summary>Serializes <paramref name="data"/> and writes it to disk.</summary>
    public static void Save(ReplayData data)
    {
        string path = SavePath;
        try
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(path, json);
            SyncFS();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"SaveManager: failed to write save — {e.Message}");
        }
    }

    /// <summary>Deletes the save file if it exists.</summary>
    public static void Delete()
    {
        string path = SavePath;
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                SyncFS();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"SaveManager: failed to delete save — {e.Message}");
            }
        }
    }
}
