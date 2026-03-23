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
    private const string LeaderboardUxmlPath = "Assets/UI/Leaderboard.uxml";
    private const string ReplayHudUxmlPath = "Assets/UI/ReplayHud.uxml";
    private const string PanelSettingsPath = "Assets/Settings/UI/PanelSettings.asset";

    // Representative messages for each font-size tier in VictoryController.
    private const string ShortMessage = "Nice!"; // len 5 → 40px
    private const string MediumMessage = "Where did my arrows go????? :("; // len 30 → 28px
    private const string LongMessage =
        "DONT PRESS THAT BUTTON DOWN THERE ITS A TRAP IT WILL MAKE YOU PLAY A DIFFERENT "
        + "LEVEL ENTIRELY ITS DANGEROUS DONT DO IT NO!!!!!!!!!"; // len 130 → 20px

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

        // Save originals so TearDown can restore them.
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

        // Clean up test render texture and restore PanelSettings.
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
            mainMenu.Q<Button>("info-btn"),
            mainMenu.Q<Button>("link-github-btn"),
            mainMenu.Q<Button>("link-discord-btn")
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
            modeSelect.Q<Button>("preset-xlarge"),
            modeSelect.Q<Button>("preset-custom"),
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
            settings.Q("drag-threshold-row"),
            settings.Q("zoom-speed-row"),
            settings.Q<Toggle>("arrow-coloring-toggle"),
            settings.Q<Button>("clear-scores-btn"),
            settings.Q<Button>("settings-back-btn")
        );
    }

    // ───────── Clear Scores Modal ─────────

    [UnityTest]
    public IEnumerator ClearScoresModal_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        root.Q("main-menu").AddToClassList("screen--hidden");
        root.Q("clear-scores-modal").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var modal = root.Q("clear-scores-modal");
        var panelBounds = root.worldBound;
        string ctx = $"ClearScoresModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            modal,
            panelBounds,
            ctx,
            warn,
            modal.Q<Label>(className: "modal-label"),
            modal.Q<Label>(className: "modal-sublabel"),
            modal.Q<Button>("clear-scores-yes-btn"),
            modal.Q<Button>("clear-scores-no-btn")
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

    // ───────── Main Menu — With Save (Continue button visible) ─────────

    [UnityTest]
    public IEnumerator MainMenu_WithSave_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        // Simulate the state when a save exists: continue-btn is visible
        root.Q<Button>("continue-btn").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var mainMenu = root.Q("main-menu");
        var panelBounds = root.worldBound;
        string ctx = $"MainMenu_WithSave @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            mainMenu,
            panelBounds,
            ctx,
            warn,
            mainMenu.Q<Button>("play-btn"),
            mainMenu.Q<Button>("continue-btn"),
            mainMenu.Q<Button>("settings-btn")
        );
    }

    // ───────── Game HUD — Loading Overlay ─────────

    [UnityTest]
    public IEnumerator GameHud_LoadingOverlay_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(GameHudUxmlPath, ratio);

        // Show the loading overlay as GameController does during generation
        var loadingOverlay = root.Q("loading-overlay");
        loadingOverlay.style.display = StyleKeyword.Null;
        loadingOverlay.style.opacity = 1f;

        // Set percent text so the label has content to lay out
        root.Q<Label>("loading-percent").text = "42%";

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"GameHud_LoadingOverlay @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            loadingOverlay,
            panelBounds,
            ctx,
            warn,
            root.Q<Label>("loading-label"),
            root.Q<Label>("loading-percent")
        );
    }

    // ───────── Game HUD — Leave Modal (Save variant) ─────────

    [UnityTest]
    public IEnumerator GameHudLeaveModal_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(GameHudUxmlPath, ratio);

        var modal = root.Q("leave-modal");
        modal.RemoveFromClassList("modal--hidden");

        // Show sublabel to test the save variant (with replace-save warning)
        var sublabel = modal.Q("leave-sublabel");
        if (sublabel != null)
            sublabel.RemoveFromClassList("modal--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"GameHudLeaveModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            modal,
            panelBounds,
            ctx,
            warn,
            modal.Q<Label>("leave-title"),
            modal.Q("leave-sublabel"),
            modal.Q<Button>("leave-close-btn"),
            modal.Q<Button>("leave-yes-btn"),
            modal.Q<Button>("leave-no-btn")
        );
    }

    [UnityTest]
    public IEnumerator GameHudCancelGenerationModal_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(GameHudUxmlPath, ratio);

        var modal = root.Q("cancel-generation-modal");
        modal.RemoveFromClassList("modal--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"GameHudCancelGenerationModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            modal,
            panelBounds,
            ctx,
            warn,
            modal.Q<Button>("cancel-generation-yes-btn"),
            modal.Q<Button>("cancel-generation-no-btn")
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

    // ───────── Mode Select — Trophy Button ─────────

    [UnityTest]
    public IEnumerator ModeSelect_TrophyButtonVisible(
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
        string ctx = $"ModeSelect_Trophy @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(modeSelect, panelBounds, ctx, warn, modeSelect.Q<Button>("trophy-btn"));
    }

    // ───────── Victory — New Best + View Leaderboard ─────────

    [UnityTest]
    public IEnumerator Victory_NewBestAndLeaderboard_AllElementsVisible(
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

        // Show New Best label
        var newBest = root.Q<Label>("new-best-label");
        newBest.RemoveFromClassList("victory--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"Victory_NewBest @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            overlay,
            panelBounds,
            ctx,
            warn,
            msgLabel,
            timeLabel,
            newBest,
            root.Q<Button>("view-leaderboard-btn"),
            root.Q<Button>("play-again-btn"),
            root.Q<Button>("menu-btn")
        );
    }

    // ───────── Leaderboard Screen ─────────

    [UnityTest]
    public IEnumerator Leaderboard_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);
        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var lb = root.Q("leaderboard-root");
        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            lb,
            panelBounds,
            ctx,
            warn,
            lb.Q<Button>("lb-back-btn"),
            lb.Q<Button>("lb-local-btn"),
            lb.Q<Button>("lb-global-btn"),
            lb.Q<Button>("tab-small"),
            lb.Q<Button>("tab-medium"),
            lb.Q<Button>("tab-large"),
            lb.Q<Button>("tab-xlarge"),
            lb.Q<Button>("tab-all"),
            lb.Q<Button>("sort-fastest"),
            lb.Q<Button>("sort-biggest"),
            lb.Q<Button>("sort-favorites"),
            lb.Q("lb-scroll")
        );
    }

    // ───────── Leaderboard — Entry Rows (All tab, with size column) ─────────

    [UnityTest]
    public IEnumerator Leaderboard_EntryRows_AllTab_FitWithinBounds(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        // Populate the list with mock entry rows matching CreateEntryRow layout (All tab = size column visible)
        var list = root.Q("lb-list");
        for (int i = 0; i < 3; i++)
            list.Add(CreateMockEntryRow(i + 1, showSize: true));

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_EntryRows_All @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        // Assert each row and all its children fit within panel bounds
        var rows = list.Query(className: "lb-entry").ToList();
        foreach (var row in rows)
            UILayoutTestHelper.AssertAllVisibleChildren(row, panelBounds, ctx, warn);
    }

    // ───────── Leaderboard — Entry Rows (Size tab, no size column) ─────────

    [UnityTest]
    public IEnumerator Leaderboard_EntryRows_SizeTab_FitWithinBounds(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        var list = root.Q("lb-list");
        for (int i = 0; i < 3; i++)
            list.Add(CreateMockEntryRow(i + 1, showSize: false));

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_EntryRows_Size @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var rows = list.Query(className: "lb-entry").ToList();
        foreach (var row in rows)
            UILayoutTestHelper.AssertAllVisibleChildren(row, panelBounds, ctx, warn);
    }

    // ───────── Leaderboard — Empty State ─────────

    [UnityTest]
    public IEnumerator Leaderboard_EmptyState_Visible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        // Show empty state, hide scroll
        root.Q("lb-scroll").AddToClassList("lb--hidden");
        root.Q<Label>("lb-empty").RemoveFromClassList("lb--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var lb = root.Q("leaderboard-root");
        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_Empty @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            lb,
            panelBounds,
            ctx,
            warn,
            lb.Q<Label>("lb-empty"),
            lb.Q<Button>("lb-back-btn")
        );
    }

    // ───────── Leaderboard — Coming Soon Overlay ─────────

    [UnityTest]
    public IEnumerator Leaderboard_ComingSoon_Visible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        root.Q("lb-coming-soon").RemoveFromClassList("lb--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var lb = root.Q("leaderboard-root");
        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_ComingSoon @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(lb, panelBounds, ctx, warn, lb.Q("lb-coming-soon"));
    }

    // ───────── Leaderboard — Delete Confirmation Modal ─────────

    [UnityTest]
    public IEnumerator Leaderboard_DeleteModal_Visible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        root.Q("lb-delete-modal").RemoveFromClassList("lb--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var modal = root.Q("lb-delete-modal");
        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_DeleteModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            modal,
            panelBounds,
            ctx,
            warn,
            modal.Q<Button>("delete-yes-btn"),
            modal.Q<Button>("delete-no-btn")
        );
    }

    // ───────── Replay HUD ─────────

    [UnityTest]
    public IEnumerator ReplayHud_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(ReplayHudUxmlPath, ratio);

        // Hide loading overlay as it would be after load
        root.Q("loading-overlay").style.display = DisplayStyle.None;

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"ReplayHud @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            root,
            panelBounds,
            ctx,
            warn,
            root.Q<Button>("exit-btn"),
            root.Q<Button>("highlight-btn"),
            root.Q<Button>("controls-toggle-btn"),
            root.Q("controls-bar"),
            root.Q<Label>("time-current"),
            root.Q<Label>("time-total"),
            root.Q("seek-track"),
            root.Q<Button>("play-pause-btn"),
            root.Q<Button>("speed-btn")
        );
    }

    // ───────── Replay HUD — Loading Overlay ─────────

    [UnityTest]
    public IEnumerator ReplayHud_LoadingOverlay_Visible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(ReplayHudUxmlPath, ratio);

        root.Q<Label>("loading-percent").text = "50%";

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var overlay = root.Q("loading-overlay");
        var panelBounds = root.worldBound;
        string ctx = $"ReplayHud_Loading @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            overlay,
            panelBounds,
            ctx,
            warn,
            root.Q<Label>("loading-label"),
            root.Q<Label>("loading-percent")
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

    /// <summary>
    /// Builds a mock leaderboard entry row matching the structure in
    /// LeaderboardScreenController.CreateEntryRow, using USS classes from Leaderboard.uss.
    /// </summary>
    private static VisualElement CreateMockEntryRow(int rank, bool showSize)
    {
        var row = new VisualElement();
        row.AddToClassList("lb-entry");

        var rankLabel = new Label($"#{rank}");
        rankLabel.AddToClassList("lb-rank");
        row.Add(rankLabel);

        if (showSize)
        {
            var sizeLabel = new Label("100\u00d7100");
            sizeLabel.AddToClassList("lb-size");
            row.Add(sizeLabel);
        }

        var timeLabel = new Label("12:34.567");
        timeLabel.AddToClassList("lb-time");
        row.Add(timeLabel);

        var dateLabel = new Label("3 days ago");
        dateLabel.AddToClassList("lb-date");
        row.Add(dateLabel);

        var favBtn = new Button();
        favBtn.AddToClassList("lb-fav-btn");
        var favIcon = new VisualElement();
        favIcon.AddToClassList("lb-fav-icon");
        favIcon.AddToClassList("lb-fav-icon--off");
        favBtn.Add(favIcon);
        row.Add(favBtn);

        var playBtn = new Button();
        playBtn.AddToClassList("lb-play-btn");
        var playIcon = new VisualElement();
        playIcon.AddToClassList("lb-play-icon");
        playBtn.Add(playIcon);
        row.Add(playBtn);

        var ctxBtn = new Button();
        ctxBtn.AddToClassList("lb-ctx-trigger");
        var ctxIcon = new VisualElement();
        ctxIcon.AddToClassList("lb-ctx-trigger-icon");
        ctxBtn.Add(ctxIcon);
        row.Add(ctxBtn);

        return row;
    }
}
