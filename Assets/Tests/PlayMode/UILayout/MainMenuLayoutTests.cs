using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

[TestFixture]
public class MainMenuLayoutTests : UILayoutTestBase
{
    [UnityTest]
    public IEnumerator MainMenu_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);
        yield return UILayoutTestHelper.WaitForLayoutResolve();

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
            mainMenu.Q<Button>("account-btn"),
            mainMenu.Q<Button>("info-btn"),
            mainMenu.Q<Button>("link-github-btn"),
            mainMenu.Q<Button>("link-discord-btn")
        );
    }

    [UnityTest]
    public IEnumerator MainMenu_WithSave_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

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

    [UnityTest]
    public IEnumerator QuitModal_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        var modal = root.Q("quit-modal");
        modal.style.display = DisplayStyle.Flex;
        var overlay = modal.Q(className: "modal-overlay");
        overlay.RemoveFromClassList("screen--hidden");

        modal.Q<Label>("modal-title").text = "Quit game?";
        modal.Q<Button>("modal-confirm-btn").text = "Yes";
        modal.Q<Button>("modal-cancel-btn").text = "No";

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"QuitModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            overlay,
            panelBounds,
            ctx,
            warn,
            modal.Q<Label>("modal-title"),
            modal.Q<Button>("modal-confirm-btn"),
            modal.Q<Button>("modal-cancel-btn")
        );
    }

    [UnityTest]
    public IEnumerator ClearScoresModal_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        root.Q("main-menu").AddToClassList("screen--hidden");
        var modal = root.Q("clear-scores-modal");
        modal.style.display = DisplayStyle.Flex;
        var overlay = modal.Q(className: "modal-overlay");
        overlay.RemoveFromClassList("screen--hidden");

        modal.Q<Label>("modal-title").text = "Delete all non-favorited scores?";
        var subtitle = modal.Q<Label>("modal-subtitle");
        subtitle.text = "Favorited entries will be kept.";
        subtitle.RemoveFromClassList("screen--hidden");
        modal.Q<Button>("modal-confirm-btn").text = "Delete";
        modal.Q<Button>("modal-cancel-btn").text = "Cancel";

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"ClearScoresModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            overlay,
            panelBounds,
            ctx,
            warn,
            modal.Q<Label>("modal-title"),
            subtitle,
            modal.Q<Button>("modal-confirm-btn"),
            modal.Q<Button>("modal-cancel-btn")
        );
    }

    [UnityTest]
    public IEnumerator AccountScreen_LoginForm_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        // Show account screen, hide main menu
        root.Q("main-menu").AddToClassList("screen--hidden");
        root.Q("account").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var account = root.Q("account");
        var panelBounds = root.worldBound;
        string ctx = $"AccountScreen_Login @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var loginForm = account.Q("login-form");
        AssertElements(
            loginForm,
            panelBounds,
            ctx,
            warn,
            loginForm.Q<TextField>("login-username"),
            loginForm.Q<TextField>("login-password"),
            loginForm.Q<Button>("login-submit-btn"),
            loginForm.Q<Button>("switch-to-register-btn"),
            account.Q<Button>("account-back-btn")
        );
    }
}
