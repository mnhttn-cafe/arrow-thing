using UnityEngine;

/// <summary>
/// Lists all available <see cref="VisualSettings"/> themes.
/// Place a single instance at <c>Resources/ThemeRegistry</c> so
/// <see cref="ThemeManager"/> can load it at startup without a scene reference.
/// </summary>
[CreateAssetMenu(fileName = "ThemeRegistry", menuName = "Arrow Thing/Theme Registry")]
public sealed class ThemeRegistry : ScriptableObject
{
    [Tooltip(
        "Themes shown in the settings dropdown, in order. Names are taken from the asset name."
    )]
    public VisualSettings[] themes;

    [Tooltip(
        "Fallback when no saved preference exists or the saved name no longer matches any theme."
    )]
    public VisualSettings defaultTheme;
}
