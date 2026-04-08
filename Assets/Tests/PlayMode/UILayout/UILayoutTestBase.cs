using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// Shared setup, teardown, and assertion helpers for UI layout tests.
/// Each scene/UXML group inherits from this base class.
/// </summary>
public abstract class UILayoutTestBase
{
    protected const string MainMenuUxmlPath = "Assets/UI/Shared/Root.uxml";
    protected const string SettingsPanelUxmlPath = "Assets/UI/Shared/Settings Panel.uxml";
    protected const string VictoryUxmlPath = "Assets/UI/Game/VictoryPopup.uxml";
    protected const string GameHudUxmlPath = "Assets/UI/Game/GameHud.uxml";
    protected const string LeaderboardUxmlPath = "Assets/UI/Leaderboard/Leaderboard.uxml";
    protected const string ReplayHudUxmlPath = "Assets/UI/Replay/ReplayHud.uxml";
    protected const string SoloSizeSelectUxmlPath =
        "Assets/UI/SoloSizeSelect/SoloSizeSelectRoot.uxml";
    private const string PanelSettingsPath = "Assets/UI/Shared/PanelSettings.asset";

    // Aspect ratios that are expected to have layout issues with current fixed-px CSS.
    private static readonly string[] KnownIssueRatios = { };

    private GameObject _uiHost;
    private PanelSettings _panelSettings;
    private PanelScaleMode _originalScaleMode;
    private Vector2Int _originalReferenceResolution;
    private PanelScreenMatchMode _originalMatchMode;
    private float _originalMatch;
    private RenderTexture _originalTargetTexture;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        Assert.IsNotNull(_panelSettings, "PanelSettings asset not found");

        _originalScaleMode = _panelSettings.scaleMode;
        _originalReferenceResolution = _panelSettings.referenceResolution;
        _originalMatchMode = _panelSettings.screenMatchMode;
        _originalMatch = _panelSettings.match;
        _originalTargetTexture = _panelSettings.targetTexture;

        _uiHost = new GameObject("UILayoutTestHost");
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_uiHost != null)
            Object.DestroyImmediate(_uiHost);

        if (_panelSettings != null)
        {
            UILayoutTestHelper.CleanUpTargetTexture(_panelSettings);
            _panelSettings.targetTexture = _originalTargetTexture;
            _panelSettings.scaleMode = _originalScaleMode;
            _panelSettings.referenceResolution = _originalReferenceResolution;
            _panelSettings.screenMatchMode = _originalMatchMode;
            _panelSettings.match = _originalMatch;
        }

        yield return null;
    }

    protected VisualElement SetUpDocument(string uxmlPath, UILayoutTestHelper.AspectRatio ratio)
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        Assert.IsNotNull(uxml, $"UXML not found at {uxmlPath}");

        var doc = _uiHost.AddComponent<UIDocument>();
        doc.panelSettings = _panelSettings;
        doc.visualTreeAsset = uxml;

        UILayoutTestHelper.SetPanelReferenceResolution(_panelSettings, ratio.Width, ratio.Height);

        return doc.rootVisualElement;
    }

    protected static void AssertElements(
        VisualElement container,
        Rect panelBounds,
        string context,
        bool warnOnly,
        params VisualElement[] elements
    )
    {
        Assert.IsNotNull(container, $"[{context}] Container is null");

        foreach (var el in elements)
        {
            Assert.IsNotNull(el, $"[{context}] Element not found in container");

            if (warnOnly)
                UILayoutTestHelper.WarnElementFullyVisible(el, panelBounds, context);
            else
                UILayoutTestHelper.AssertElementFullyVisible(el, panelBounds, context);
        }
    }

    protected static bool IsKnownIssueRatio(UILayoutTestHelper.AspectRatio ratio)
    {
        foreach (string known in KnownIssueRatios)
        {
            if (ratio.Name == known)
                return true;
        }
        return false;
    }
}
