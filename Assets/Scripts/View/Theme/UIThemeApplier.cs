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
        // Also apply to panel.visualTree so elements injected above the UIDocument
        // root (e.g. CustomDropdown popup) inherit the theme's CSS custom properties.
        var panelRoot = root.panel != null ? root.panel.visualTree : null;

        if (_appliedSheet != null)
        {
            if (root.styleSheets.Contains(_appliedSheet))
                root.styleSheets.Remove(_appliedSheet);
            if (panelRoot != null && panelRoot.styleSheets.Contains(_appliedSheet))
                panelRoot.styleSheets.Remove(_appliedSheet);
        }

        _appliedSheet = settings != null ? settings.themeUIStyleSheet : null;

        if (_appliedSheet != null)
        {
            if (!root.styleSheets.Contains(_appliedSheet))
                root.styleSheets.Add(_appliedSheet);
            if (panelRoot != null && !panelRoot.styleSheets.Contains(_appliedSheet))
                panelRoot.styleSheets.Add(_appliedSheet);
        }
    }
}
