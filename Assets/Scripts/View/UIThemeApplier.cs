using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Injects the theme stylesheet from VisualSettings into the UIDocument's
/// root VisualElement so all USS var(--...) references resolve correctly.
/// Attach to the same GameObject as the UIDocument.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public sealed class UIThemeApplier : MonoBehaviour
{
    [SerializeField]
    private VisualSettings visualSettings;

    private void OnEnable()
    {
        if (visualSettings == null || visualSettings.themeUIStyleSheet == null)
            return;

        var root = GetComponent<UIDocument>().rootVisualElement;
        var sheet = visualSettings.themeUIStyleSheet;

        if (!root.styleSheets.Contains(sheet))
            root.styleSheets.Add(sheet);
    }
}
