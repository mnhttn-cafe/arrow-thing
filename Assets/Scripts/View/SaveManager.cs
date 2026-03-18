using System.IO;
using UnityEngine;

/// <summary>
/// Saves and loads the in-progress game to a JSON file in <see cref="Application.persistentDataPath"/>.
/// On WebGL this maps to the browser's IndexedDB. One save slot: the file is overwritten on each save.
/// </summary>
public static class SaveManager
{
    private const string FileName = "savegame.json";

    private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

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
            var data = JsonUtility.FromJson<ReplayData>(json);
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

    /// <summary>Serializes <paramref name="data"/> and writes it to disk.</summary>
    public static void Save(ReplayData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, prettyPrint: false);
            File.WriteAllText(SavePath, json);
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
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"SaveManager: failed to delete save — {e.Message}");
            }
        }
    }
}
