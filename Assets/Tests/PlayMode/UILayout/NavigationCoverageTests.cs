using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// Verifies that every actionable UI element (Button) in each scene's UXML
/// exists and is queryable. This catches buttons that are removed or renamed
/// in UXML without updating the controller's nav graph.
///
/// These tests validate UXML structure only — they don't instantiate scene
/// controllers (which require a full scene environment). The controllers'
/// BuildNavGraph methods reference buttons by name; if a name changes in
/// UXML, these tests fail, signaling that the nav graph needs updating.
/// </summary>
[TestFixture]
public class NavigationCoverageTests : UILayoutTestBase
{
    [UnityTest]
    public IEnumerator MainMenu_AllNavigableButtonsExist()
    {
        var root = SetUpDocument(
            MainMenuUxmlPath,
            new UILayoutTestHelper.AspectRatio("16:9", 1920, 1080)
        );
        yield return UILayoutTestHelper.WaitForLayoutResolve();

        AssertButtonExists(root, "MainMenu", "quit-btn");
        AssertButtonExists(root, "MainMenu", "leaderboard-btn");
        AssertButtonExists(root, "MainMenu", "play-btn");
        AssertButtonExists(root, "MainMenu", "continue-btn");
        AssertButtonExists(root, "MainMenu", "settings-btn");
        AssertButtonExists(root, "MainMenu", "link-github-btn");
        AssertButtonExists(root, "MainMenu", "link-discord-btn");
    }

    [UnityTest]
    public IEnumerator SoloSizeSelect_AllNavigableButtonsExist()
    {
        var root = SetUpDocument(
            SoloSizeSelectUxmlPath,
            new UILayoutTestHelper.AspectRatio("16:9", 1920, 1080)
        );
        yield return UILayoutTestHelper.WaitForLayoutResolve();

        AssertButtonExists(root, "SoloSizeSelect", "back-btn");
        AssertButtonExists(root, "SoloSizeSelect", "trophy-btn");
        AssertButtonExists(root, "SoloSizeSelect", "preset-small");
        AssertButtonExists(root, "SoloSizeSelect", "preset-medium");
        AssertButtonExists(root, "SoloSizeSelect", "preset-large");
        AssertButtonExists(root, "SoloSizeSelect", "preset-xlarge");
        AssertButtonExists(root, "SoloSizeSelect", "preset-custom");
        AssertButtonExists(root, "SoloSizeSelect", "start-btn");
    }

    [UnityTest]
    public IEnumerator GameHud_AllNavigableButtonsExist()
    {
        var root = SetUpDocument(
            GameHudUxmlPath,
            new UILayoutTestHelper.AspectRatio("16:9", 1920, 1080)
        );
        yield return UILayoutTestHelper.WaitForLayoutResolve();

        AssertButtonExists(root, "GameHud", "back-to-menu-btn");
        AssertButtonExists(root, "GameHud", "trail-toggle-btn");
        // Leave modals use ConfirmModal template (modal-confirm-btn, modal-cancel-btn).
        var leaveModal = root.Q("leave-modal");
        Assert.IsNotNull(leaveModal, "[GameHud] leave-modal not found");
        Assert.IsNotNull(
            leaveModal.Q<Button>("modal-confirm-btn"),
            "[GameHud] leave-modal missing modal-confirm-btn"
        );
        Assert.IsNotNull(
            leaveModal.Q<Button>("modal-cancel-btn"),
            "[GameHud] leave-modal missing modal-cancel-btn"
        );
    }

    [UnityTest]
    public IEnumerator VictoryPopup_AllNavigableButtonsExist()
    {
        var root = SetUpDocument(
            VictoryUxmlPath,
            new UILayoutTestHelper.AspectRatio("16:9", 1920, 1080)
        );
        yield return UILayoutTestHelper.WaitForLayoutResolve();

        AssertButtonExists(root, "Victory", "play-again-btn");
        AssertButtonExists(root, "Victory", "menu-btn");
        AssertButtonExists(root, "Victory", "view-leaderboard-btn");
        AssertButtonExists(root, "Victory", "toast-action-btn");
    }

    [UnityTest]
    public IEnumerator Leaderboard_AllNavigableButtonsExist()
    {
        var root = SetUpDocument(
            LeaderboardUxmlPath,
            new UILayoutTestHelper.AspectRatio("16:9", 1920, 1080)
        );
        yield return UILayoutTestHelper.WaitForLayoutResolve();

        AssertButtonExists(root, "Leaderboard", "lb-back-btn");
        AssertButtonExists(root, "Leaderboard", "lb-local-btn");
        AssertButtonExists(root, "Leaderboard", "lb-global-btn");
        AssertButtonExists(root, "Leaderboard", "tab-small");
        AssertButtonExists(root, "Leaderboard", "tab-medium");
        AssertButtonExists(root, "Leaderboard", "tab-large");
        AssertButtonExists(root, "Leaderboard", "tab-xlarge");
        AssertButtonExists(root, "Leaderboard", "tab-all");
        AssertButtonExists(root, "Leaderboard", "lb-refresh-btn");
        AssertButtonExists(root, "Leaderboard", "sort-fastest");
        AssertButtonExists(root, "Leaderboard", "sort-biggest");
        AssertButtonExists(root, "Leaderboard", "sort-favorites");
        AssertButtonExists(root, "Leaderboard", "lb-player-play-btn");
        AssertButtonExists(root, "Leaderboard", "ctx-favorite-btn");
        AssertButtonExists(root, "Leaderboard", "ctx-play-btn");
        AssertButtonExists(root, "Leaderboard", "ctx-delete-btn");
    }

    [UnityTest]
    public IEnumerator ReplayHud_AllNavigableButtonsExist()
    {
        var root = SetUpDocument(
            ReplayHudUxmlPath,
            new UILayoutTestHelper.AspectRatio("16:9", 1920, 1080)
        );
        yield return UILayoutTestHelper.WaitForLayoutResolve();

        AssertButtonExists(root, "ReplayHud", "exit-btn");
        AssertButtonExists(root, "ReplayHud", "play-pause-btn");
        AssertButtonExists(root, "ReplayHud", "speed-btn");
        AssertButtonExists(root, "ReplayHud", "highlight-btn");
        AssertButtonExists(root, "ReplayHud", "controls-toggle-btn");
    }

    private static void AssertButtonExists(VisualElement root, string context, string buttonName)
    {
        Assert.IsNotNull(
            root.Q<Button>(buttonName),
            $"[{context}] Button '{buttonName}' not found in UXML — "
                + "if renamed, update the controller's BuildNavGraph"
        );
    }
}
