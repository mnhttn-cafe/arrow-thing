using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene navigation stack. Pushes new scenes additively and disables previous
/// ones. Popping re-enables the previous scene without reloading it.
///
/// Controllers should save their state in OnDisable and restore in OnEnable
/// to survive the disable/enable cycle.
/// </summary>
public static class SceneNav
{
    private static readonly Stack<string> _stack = new Stack<string>();

    /// <summary>
    /// Load a new scene additively and disable the current scene.
    /// The current scene stays in memory and can be restored with <see cref="Pop"/>.
    /// </summary>
    public static void Push(string sceneName)
    {
        string current = SceneManager.GetActiveScene().name;
        _stack.Push(current);
        SetSceneActive(current, false);

        SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        SceneManager.sceneLoaded += OnPushedSceneLoaded;

        void OnPushedSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != sceneName)
                return;
            SceneManager.sceneLoaded -= OnPushedSceneLoaded;
            SceneManager.SetActiveScene(scene);
        }
    }

    /// <summary>
    /// Unload the current scene and re-enable the previous scene on the stack.
    /// If the stack is empty, falls back to loading MainMenu normally.
    /// </summary>
    public static void Pop()
    {
        if (_stack.Count == 0)
        {
            SceneManager.LoadScene("MainMenu");
            return;
        }

        string current = SceneManager.GetActiveScene().name;
        string previous = _stack.Pop();

        // Disable the current scene before async unload to prevent
        // overlapping audio listeners / cameras during the unload frame.
        SetSceneActive(current, false);
        SceneManager.UnloadSceneAsync(current);
        SetSceneActive(previous, true);

        var scene = SceneManager.GetSceneByName(previous);
        if (scene.IsValid())
            SceneManager.SetActiveScene(scene);
    }

    /// <summary>
    /// Replace the current scene with a new one (unload current, load new).
    /// The stack is unchanged — the new scene takes the current scene's position.
    /// </summary>
    public static void Replace(string sceneName)
    {
        string current = SceneManager.GetActiveScene().name;
        SetSceneActive(current, false);
        SceneManager.UnloadSceneAsync(current);

        SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        SceneManager.sceneLoaded += OnReplacedSceneLoaded;

        void OnReplacedSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != sceneName)
                return;
            SceneManager.sceneLoaded -= OnReplacedSceneLoaded;
            SceneManager.SetActiveScene(scene);
        }
    }

    /// <summary>
    /// Clear the stack and load a scene normally (non-additive).
    /// Use for hard resets like returning to the title screen.
    /// </summary>
    public static void Reset(string sceneName)
    {
        _stack.Clear();
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Number of scenes on the stack (not counting the current active scene).</summary>
    public static int Depth => _stack.Count;

    private static void SetSceneActive(string sceneName, bool active)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid())
            return;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.scene != scene)
                continue;
            root.SetActive(active);
        }
    }
}
