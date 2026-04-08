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
        var root = SetUpDocument(SoloSizeSelectUxmlPath, ratio);

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"ModeSelect @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            root,
            panelBounds,
            ctx,
            warn,
            root.Q<Label>(className: "section-label"),
            root.Q<Button>("preset-small"),
            root.Q<Button>("preset-medium"),
            root.Q<Button>("preset-large"),
            root.Q<Button>("preset-xlarge"),
            root.Q<Button>("preset-custom"),
            root.Q<Button>("start-btn"),
            root.Q<Button>("back-btn")
        );
    }

    [UnityTest]
    public IEnumerator ModeSelect_TrophyButtonVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(SoloSizeSelectUxmlPath, ratio);

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"ModeSelect_Trophy @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(root, panelBounds, ctx, warn, root.Q<Button>("trophy-btn"));
    }

    [UnityTest]
    public IEnumerator Settings_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(SettingsPanelUxmlPath, ratio);

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
            settings.Q<Button>("nav-account"),
            settings.Q<Button>("nav-gameplay"),
            settings.Q<Button>("nav-data"),
            settings.Q<Button>("nav-about"),
            settings.Q<Button>("settings-close-btn")
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
        var root = SetUpDocument(SettingsPanelUxmlPath, ratio);

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
    public IEnumerator Settings_LoginForm_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(SettingsPanelUxmlPath, ratio);

        root.Q("settings").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var settings = root.Q("settings");
        var panelBounds = root.worldBound;
        string ctx = $"Settings_Login @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var loginForm = settings.Q("login-form");
        AssertElements(
            loginForm,
            panelBounds,
            ctx,
            warn,
            loginForm.Q<Button>("login-submit-btn"),
            loginForm.Q<Button>("register-submit-btn")
        );
    }

    [UnityTest]
    public IEnumerator Settings_ResetForm_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(SettingsPanelUxmlPath, ratio);

        root.Q("settings").RemoveFromClassList("screen--hidden");

        var settings = root.Q("settings");
        settings.Q("login-form").AddToClassList("screen--hidden");
        settings.Q("reset-form").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"Settings_Reset @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var resetForm = settings.Q("reset-form");
        AssertElements(
            resetForm,
            panelBounds,
            ctx,
            warn,
            resetForm.Q<Label>("reset-message"),
            resetForm.Q<Button>("reset-submit-btn"),
            resetForm.Q<Button>("reset-back-btn")
        );
    }

    [UnityTest]
    public IEnumerator Settings_ConfirmEmailForm_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(SettingsPanelUxmlPath, ratio);

        root.Q("settings").RemoveFromClassList("screen--hidden");

        var settings = root.Q("settings");
        settings.Q("login-form").AddToClassList("screen--hidden");
        settings.Q("confirm-email-form").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"Settings_ConfirmEmail @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var confirmForm = settings.Q("confirm-email-form");
        AssertElements(
            confirmForm,
            panelBounds,
            ctx,
            warn,
            confirmForm.Q<Label>("confirm-email-message"),
            confirmForm.Q<Button>("confirm-email-submit-btn")
        );
    }
}
