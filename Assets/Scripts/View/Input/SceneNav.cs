using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene navigation stack. Pushes new scenes additively and disables previous
/// ones. Popping re-enables the previous scene without reloading it.
///
/// Controllers should save their state in OnDisable and restore in OnEnable
/// to survive the disable/enable cycle.
///
/// Stack logic is delegated to <see cref="SceneNavStack"/> so it can be
/// unit-tested without loading real scenes.
/// </summary>
public static class SceneNav
{
    private static readonly SceneNavStack _stack = new SceneNavStack();

    /// <summary>
    /// Load a new scene additively and disable the current scene.
    /// The current scene stays in memory and can be restored with <see cref="Pop"/>.
    /// </summary>
    public static void Push(string sceneName)
    {
        string current = SceneManager.GetActiveScene().name;
        _stack.Push(current, sceneName);
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
        string previous = _stack.Pop();
        if (previous == null)
        {
            SceneManager.LoadScene("MainMenu");
            return;
        }

        string current = SceneManager.GetActiveScene().name;

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
        _stack.Replace(current, sceneName);
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
        _stack.Reset();
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Number of scenes on the stack (not counting the current active scene).</summary>
    public static int Depth => _stack.Depth;

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
