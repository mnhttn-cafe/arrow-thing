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
            loginForm.Q<TextField>("login-email"),
            loginForm.Q<TextField>("login-password"),
            loginForm.Q<Button>("login-submit-btn"),
            loginForm.Q<Button>("forgot-password-btn"),
            loginForm.Q<Button>("switch-to-register-btn"),
            account.Q<Button>("account-back-btn")
        );
    }

    [UnityTest]
    public IEnumerator AccountScreen_RegisterForm_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        root.Q("main-menu").AddToClassList("screen--hidden");
        root.Q("account").RemoveFromClassList("screen--hidden");

        // Show register form, hide login form
        var account = root.Q("account");
        account.Q("login-form").AddToClassList("screen--hidden");
        account.Q("register-form").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"AccountScreen_Register @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var registerForm = account.Q("register-form");
        AssertElements(
            registerForm,
            panelBounds,
            ctx,
            warn,
            registerForm.Q<TextField>("register-email"),
            registerForm.Q<TextField>("register-display-name"),
            registerForm.Q<TextField>("register-password"),
            registerForm.Q<TextField>("register-confirm-password"),
            registerForm.Q<Button>("register-submit-btn"),
            registerForm.Q<Button>("switch-to-login-btn"),
            account.Q<Button>("account-back-btn")
        );
    }

    [UnityTest]
    public IEnumerator AccountScreen_ResetForm_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        root.Q("main-menu").AddToClassList("screen--hidden");
        root.Q("account").RemoveFromClassList("screen--hidden");

        // Show reset form, hide login form
        var account = root.Q("account");
        account.Q("login-form").AddToClassList("screen--hidden");
        account.Q("reset-form").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"AccountScreen_Reset @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var resetForm = account.Q("reset-form");
        AssertElements(
            resetForm,
            panelBounds,
            ctx,
            warn,
            resetForm.Q<Label>("reset-message"),
            resetForm.Q<TextField>("reset-code"),
            resetForm.Q<TextField>("reset-new-password"),
            resetForm.Q<TextField>("reset-confirm-password"),
            resetForm.Q<Button>("reset-submit-btn"),
            resetForm.Q<Button>("reset-back-btn"),
            account.Q<Button>("account-back-btn")
        );
    }

    [UnityTest]
    public IEnumerator AccountScreen_ConfirmEmailForm_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(MainMenuUxmlPath, ratio);

        root.Q("main-menu").AddToClassList("screen--hidden");
        root.Q("account").RemoveFromClassList("screen--hidden");

        // Show confirm email form, hide login form
        var account = root.Q("account");
        account.Q("login-form").AddToClassList("screen--hidden");
        account.Q("confirm-email-form").RemoveFromClassList("screen--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"AccountScreen_ConfirmEmail @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var confirmForm = account.Q("confirm-email-form");
        AssertElements(
            confirmForm,
            panelBounds,
            ctx,
            warn,
            confirmForm.Q<Label>("confirm-email-message"),
            confirmForm.Q<TextField>("confirm-email-code"),
            confirmForm.Q<Button>("confirm-email-submit-btn"),
            confirmForm.Q<Button>("confirm-email-back-btn"),
            account.Q<Button>("account-back-btn")
        );
    }
}
