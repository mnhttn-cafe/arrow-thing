using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

[TestFixture]
public class UILayoutTests
{
    private const string MainMenuUxmlPath = "Assets/UI/Root.uxml";
    private const string VictoryUxmlPath = "Assets/UI/VictoryPopup.uxml";
    private const string GameHudUxmlPath = "Assets/UI/GameHud.uxml";
    private const string PanelSettingsPath = "Assets/Settings/UI/PanelSettings.asset";

    // Representative messages for each font-size tier in VictoryController.
    private const string ShortMessage = "Nice!"; // len 5 → 40px
    private const string MediumMessage = "Where did my arrows go????? :("; // len 30 → 28px
    private const string LongMessage =
        "DONT PRESS THAT BUTTON DOWN THERE ITS A TRAP IT WILL MAKE YOU PLAY A DIFFERENT "
        + "LEVEL ENTIRELY ITS DANGEROUS DONT DO IT NO!!!!!!!!!"; // len 130 → 20px

    // Aspect ratios that are expected to have layout issues with current fixed-px CSS.
    private static readonly string[] KnownIssueRatios = { "9:16" };

    private GameObject _uiHost;
    private PanelSettings _panelSettings;
    private PanelScaleMode _originalScaleMode;
    private Vector2Int _originalReferenceResolution;
    private PanelScreenMatchMode _originalMatchMode;
    private float _originalMatch;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        Assert.IsNotNull(_panelSettings, "PanelSettings asset not found");

        // Save originals so TearDown can restore them.
        _originalScaleMode = _panelSettings.scaleMode;
        _originalReferenceResolution = _panelSettings.referenceResolution;
        _originalMatchMode = _panelSettings.screenMatchMode;
        _originalMatch = _panelSettings.match;

        _uiHost = new GameObject("UILayoutTestHost");
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_uiHost != null)
            Object.DestroyImmediate(_uiHost);

        // Restore PanelSettings to avoid polluting other tests.
        if (_panelSettings != null)
        {
            _panelSettings.scaleMode = _originalScaleMode;
            _panelSettings.referenceResolution = _originalReferenceResolution;
            _panelSettings.screenMatchMode = _originalMatchMode;
            _panelSettings.match = _originalMatch;
        }

        yield return null;
    }

    // ───────── Main Menu ─────────

    [UnityTest]
    public IEnumerator MainMenu_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);
        yield return UILayoutTestHelper.WaitForLayoutResolve();

        // main-menu is visible by default (no screen--hidden).
        var mainMenu = root.Q("main-menu");
        var panelBounds = root.worldBound;
        string ctx = $"MainMenu @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            mainMenu,
            panelBounds,
            ctx,
            warn,
            mainMenu.Q(className: "title"),
            mainMenu.Q<Button>("play-btn"),
            mainMenu.Q<Button>("settings-btn"),
            mainMenu.Q<Button>("quit-btn"),
            mainMenu.Q<Button>("info-btn")
        );
    }

    // ───────── Mode Select ─────────

    [UnityTest]
    public IEnumerator ModeSelect_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        root.Q("main-menu").AddToClassList("screen--hidden");
        root.Q("mode-select").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var modeSelect = root.Q("mode-select");
        var panelBounds = root.worldBound;
        string ctx = $"ModeSelect @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            modeSelect,
            panelBounds,
            ctx,
            warn,
            modeSelect.Q<Label>(className: "section-label"),
            modeSelect.Q<Button>("preset-small"),
            modeSelect.Q<Button>("preset-medium"),
            modeSelect.Q<Button>("preset-large"),
            modeSelect.Q<Button>("start-btn"),
            modeSelect.Q<Button>("mode-back-btn")
        );
    }

    // ───────── Settings ─────────

    [UnityTest]
    public IEnumerator Settings_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        root.Q("main-menu").AddToClassList("screen--hidden");
        root.Q("settings").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var settings = root.Q("settings");
        var panelBounds = root.worldBound;
        string ctx = $"Settings @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            settings,
            panelBounds,
            ctx,
            warn,
            settings.Q<Label>(className: "section-label"),
            settings.Q<Slider>("drag-threshold-slider"),
            settings.Q<Toggle>("arrow-coloring-toggle"),
            settings.Q<Button>("settings-back-btn")
        );
    }

    // ───────── Quit Modal ─────────

    [UnityTest]
    public IEnumerator QuitModal_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        root.Q("quit-modal").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var modal = root.Q("quit-modal");
        var panelBounds = root.worldBound;
        string ctx = $"QuitModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            modal,
            panelBounds,
            ctx,
            warn,
            modal.Q<Label>(className: "modal-label"),
            modal.Q<Button>("quit-yes-btn"),
            modal.Q<Button>("quit-no-btn")
        );
    }

    // ───────── Victory Popup — Short Message (40px) ─────────

    [UnityTest]
    public IEnumerator VictoryShort_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        yield return RunVictoryTest(ratio, ShortMessage, 40, "VictoryShort");
    }

    // ───────── Victory Popup — Medium Message (28px) ─────────

    [UnityTest]
    public IEnumerator VictoryMedium_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        yield return RunVictoryTest(ratio, MediumMessage, 28, "VictoryMedium");
    }

    // ───────── Victory Popup — Long Message (20px) ─────────

    [UnityTest]
    public IEnumerator VictoryLong_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        yield return RunVictoryTest(ratio, LongMessage, 20, "VictoryLong");
    }

    // ───────── Game HUD ─────────

    [UnityTest]
    public IEnumerator GameHud_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(GameHudUxmlPath, ratio);
        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"GameHud @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            root,
            panelBounds,
            ctx,
            warn,
            root.Q<Button>("back-to-menu-btn"),
            root.Q<Label>("timer-label")
        );
    }

    // ───────── Game HUD — Leave Modal ─────────

    [UnityTest]
    public IEnumerator GameHudLeaveModal_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(GameHudUxmlPath, ratio);

        root.Q("leave-modal").RemoveFromClassList("modal--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var modal = root.Q("leave-modal");
        var panelBounds = root.worldBound;
        string ctx = $"GameHudLeaveModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            modal,
            panelBounds,
            ctx,
            warn,
            modal.Q<Label>(className: "modal-label"),
            modal.Q<Button>("leave-yes-btn"),
            modal.Q<Button>("leave-no-btn")
        );
    }

    // ───────── Victory Popup with Time ─────────

    [UnityTest]
    public IEnumerator VictoryWithTime_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(VictoryUxmlPath, ratio);

        var overlay = root.Q("victory-overlay");
        overlay.RemoveFromClassList("victory--hidden");

        var msgLabel = root.Q<Label>("victory-message");
        msgLabel.text = ShortMessage;
        msgLabel.style.fontSize = 40;

        var timeLabel = root.Q<Label>("victory-time");
        timeLabel.text = "1:23.456";

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"VictoryWithTime @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            overlay,
            panelBounds,
            ctx,
            warn,
            msgLabel,
            timeLabel,
            root.Q<Button>("play-again-btn"),
            root.Q<Button>("menu-btn")
        );
    }

    // ───────── Helpers ─────────

    private IEnumerator RunVictoryTest(
        UILayoutTestHelper.AspectRatio ratio,
        string message,
        int fontSize,
        string label
    )
    {
        var root = SetUpDocument(VictoryUxmlPath, ratio);

        // Make overlay visible and set message text + font size.
        var overlay = root.Q("victory-overlay");
        overlay.RemoveFromClassList("victory--hidden");

        var msgLabel = root.Q<Label>("victory-message");
        msgLabel.text = message;
        msgLabel.style.fontSize = fontSize;

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"{label} @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            overlay,
            panelBounds,
            ctx,
            warn,
            msgLabel,
            root.Q<Button>("play-again-btn"),
            root.Q<Button>("menu-btn")
        );
    }

    private VisualElement SetUpDocument(string uxmlPath, UILayoutTestHelper.AspectRatio ratio)
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        Assert.IsNotNull(uxml, $"UXML not found at {uxmlPath}");

        var doc = _uiHost.AddComponent<UIDocument>();
        doc.panelSettings = _panelSettings;
        doc.visualTreeAsset = uxml;

        UILayoutTestHelper.SetPanelReferenceResolution(_panelSettings, ratio.Width, ratio.Height);

        return doc.rootVisualElement;
    }

    private static void AssertElements(
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

    private static bool IsKnownIssueRatio(UILayoutTestHelper.AspectRatio ratio)
    {
        foreach (string known in KnownIssueRatios)
        {
            if (ratio.Name == known)
                return true;
        }
        return false;
    }
}
