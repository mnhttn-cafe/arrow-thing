using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Keeps the UIDocument's root stylesheet in sync with the active theme from
/// <see cref="ThemeManager"/>. Attach to every UIDocument GameObject; no
/// per-component configuration is needed — all appliers update automatically
/// when <see cref="ThemeManager.Apply"/> is called.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public sealed class UIThemeApplier : MonoBehaviour
{
    private StyleSheet _appliedSheet;

    private void OnEnable()
    {
        ThemeManager.ThemeChanged += ApplyTheme;
        ApplyTheme(ThemeManager.Current);
    }

    private void OnDisable()
    {
        ThemeManager.ThemeChanged -= ApplyTheme;
    }

    private void ApplyTheme(VisualSettings settings)
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        if (_appliedSheet != null && root.styleSheets.Contains(_appliedSheet))
            root.styleSheets.Remove(_appliedSheet);

        _appliedSheet = settings?.themeUIStyleSheet;

        if (_appliedSheet != null && !root.styleSheets.Contains(_appliedSheet))
            root.styleSheets.Add(_appliedSheet);
    }
}
