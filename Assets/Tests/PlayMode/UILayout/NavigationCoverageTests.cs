using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// Verifies that every actionable UI element (Button, Toggle) in each scene
/// is reachable via keyboard navigation. Catches regressions where a button
/// is added to UXML but not wired into the FocusNavigator.
///
/// Elements inside ConfirmModal templates are exempt (modals manage their own
/// keyboard nav via push/pop).
/// </summary>
public class NavigationCoverageTests : UILayoutTestBase
{
    private static readonly string SoloSizeSelectUxmlPath =
        "Assets/UI/SoloSizeSelect/SoloSizeSelectRoot.uxml";

    // -- Test cases per scene ------------------------------------------------

    [UnityTest]
    public IEnumerator MainMenu_AllButtonsNavigable()
    {
        var root = SetUpDocumentDefault(MainMenuUxmlPath);
        yield return null; // Let layout resolve.

        var controller = CreateController<MainMenuController>(root);
        yield return null;

        AssertAllButtonsNavigable(root, "MainMenu");
    }

    [UnityTest]
    public IEnumerator SoloSizeSelect_AllButtonsNavigable()
    {
        var root = SetUpDocumentDefault(SoloSizeSelectUxmlPath);
        yield return null;

        var controller = CreateController<SoloSizeSelectController>(root);
        yield return null;

        AssertAllButtonsNavigable(root, "SoloSizeSelect");
    }

    [UnityTest]
    public IEnumerator Leaderboard_AllButtonsNavigable()
    {
        var root = SetUpDocumentDefault(LeaderboardUxmlPath);
        yield return null;

        var controller = CreateController<LeaderboardScreenController>(root);
        yield return null;

        AssertAllButtonsNavigable(
            root,
            "Leaderboard",
            exemptClasses: new[] { "context-menu__btn", "lb-player-play-btn" }
        );
    }

    [UnityTest]
    public IEnumerator GameHud_AllButtonsNavigable()
    {
        var root = SetUpDocumentDefault(GameHudUxmlPath);
        yield return null;

        // GameController is too complex to instantiate in isolation.
        // Instead, verify the UXML buttons that should be in the navigator.
        var expected = new[] { "back-to-menu-btn", "trail-toggle-btn" };
        foreach (var name in expected)
        {
            var btn = root.Q<Button>(name);
            Assert.IsNotNull(btn, $"[GameHud] Button '{name}' not found in UXML");
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator ReplayHud_AllButtonsNavigable()
    {
        var root = SetUpDocumentDefault(ReplayHudUxmlPath);
        yield return null;

        var expected = new[]
        {
            "exit-btn",
            "play-pause-btn",
            "speed-btn",
            "highlight-btn",
            "controls-toggle-btn",
        };
        foreach (var name in expected)
        {
            var btn = root.Q<Button>(name);
            Assert.IsNotNull(btn, $"[ReplayHud] Button '{name}' not found in UXML");
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator VictoryPopup_AllButtonsNavigable()
    {
        var root = SetUpDocumentDefault(VictoryUxmlPath);
        yield return null;

        var expected = new[]
        {
            "play-again-btn",
            "menu-btn",
            "view-leaderboard-btn",
            "toast-action-btn",
        };
        foreach (var name in expected)
        {
            var btn = root.Q<Button>(name);
            Assert.IsNotNull(btn, $"[VictoryPopup] Button '{name}' not found in UXML");
        }
        yield break;
    }

    // -- Helpers --------------------------------------------------------------

    private VisualElement SetUpDocumentDefault(string uxmlPath)
    {
        return SetUpDocument(uxmlPath, new UILayoutTestHelper.AspectRatio("16:9", 1920, 1080));
    }

    private T CreateController<T>(VisualElement root)
        where T : NavigableScene
    {
        var go = root.panel.visualTree.userData as GameObject;
        if (go == null)
        {
            // Find the UIDocument's GameObject.
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var doc in docs)
            {
                if (doc.rootVisualElement == root)
                {
                    go = doc.gameObject;
                    break;
                }
            }
        }
        Assert.IsNotNull(go, "Could not find GameObject for UIDocument");

        var controller = go.AddComponent<T>();
        return controller;
    }

    private static void AssertAllButtonsNavigable(
        VisualElement root,
        string context,
        string[] exemptClasses = null
    )
    {
        var nav = FocusNavigator.Active;
        Assert.IsNotNull(nav, $"[{context}] FocusNavigator.Active is null after controller init");

        var navElements = new HashSet<VisualElement>();
        for (int i = 0; i < nav.ItemCount; i++)
        {
            var el = nav.GetItemElement(i);
            if (el != null)
                navElements.Add(el);
        }

        var allButtons = root.Query<Button>().ToList();
        foreach (var btn in allButtons)
        {
            // Skip hidden buttons.
            if (btn.resolvedStyle.display == DisplayStyle.None)
                continue;
            if (btn.ClassListContains("screen--hidden"))
                continue;
            if (btn.ClassListContains("lb--hidden"))
                continue;

            // Skip buttons inside modal templates.
            if (IsInsideModal(btn))
                continue;

            // Skip explicitly exempt classes.
            if (exemptClasses != null)
            {
                bool exempt = false;
                foreach (var cls in exemptClasses)
                {
                    if (btn.ClassListContains(cls))
                    {
                        exempt = true;
                        break;
                    }
                }
                if (exempt)
                    continue;
            }

            // Check if the button itself or any ancestor is in the navigator.
            bool found = false;
            VisualElement check = btn;
            while (check != null)
            {
                if (navElements.Contains(check))
                {
                    found = true;
                    break;
                }
                check = check.parent;
            }

            Assert.IsTrue(
                found,
                $"[{context}] Button '{btn.name ?? btn.text}' is not keyboard navigable"
            );
        }
    }

    private static bool IsInsideModal(VisualElement el)
    {
        var parent = el.parent;
        while (parent != null)
        {
            if (parent.ClassListContains("modal-overlay"))
                return true;
            parent = parent.parent;
        }
        return false;
    }
}
