using System;
using UnityEngine;

/// <summary>
/// Central authority for the active UI theme. Loads available themes from
/// <c>Resources/ThemeRegistry</c> at startup and restores the player's last
/// choice from <c>PlayerPrefs</c>. All <see cref="UIThemeApplier"/> instances
/// subscribe to <see cref="ThemeChanged"/> so a single <see cref="Apply"/> call
/// updates every open UIDocument simultaneously.
/// </summary>
public static class ThemeManager
{
    private const string PrefKey = "SelectedTheme";

    /// <summary>Fired when the active theme changes. Passes the new settings.</summary>
    public static event Action<VisualSettings> ThemeChanged;

    /// <summary>The currently active visual settings.</summary>
    public static VisualSettings Current { get; private set; }

    /// <summary>All themes available for selection, in registry order.</summary>
    public static VisualSettings[] Available { get; private set; } = Array.Empty<VisualSettings>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        var registry = Resources.Load<ThemeRegistry>("ThemeRegistry");
        if (registry == null)
        {
            Debug.LogWarning(
                "ThemeManager: no ThemeRegistry asset found at Resources/ThemeRegistry."
            );
            return;
        }

        Available = registry.themes ?? Array.Empty<VisualSettings>();

        string saved = PlayerPrefs.GetString(PrefKey, "");
        VisualSettings resolved = null;
        foreach (var t in Available)
        {
            if (t != null && t.name == saved)
            {
                resolved = t;
                break;
            }
        }

        // Set without firing — UIThemeAppliers haven't been enabled yet.
        Current = resolved ?? registry.defaultTheme;
    }

    /// <summary>
    /// Switches to <paramref name="settings"/>, persists the choice, and notifies
    /// all <see cref="UIThemeApplier"/> instances via <see cref="ThemeChanged"/>.
    /// No-op if <paramref name="settings"/> is already active.
    /// </summary>
    public static void Apply(VisualSettings settings)
    {
        if (settings == null || settings == Current)
            return;

        Current = settings;
        PlayerPrefs.SetString(PrefKey, settings.name);
        ThemeChanged?.Invoke(settings);
    }
}
