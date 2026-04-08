using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

[TestFixture]
public class GameHudLayoutTests : UILayoutTestBase
{
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
            root.Q<Button>("retry-btn"),
            root.Q<Label>("timer-label")
        );
    }

    [UnityTest]
    public IEnumerator GameHud_LoadingOverlay_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(GameHudUxmlPath, ratio);

        var loadingOverlay = root.Q("loading-overlay");
        loadingOverlay.style.display = StyleKeyword.Null;
        loadingOverlay.style.opacity = 1f;

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

    [UnityTest]
    public IEnumerator GameHudLeaveModal_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(GameHudUxmlPath, ratio);

        // Leave modal uses ConfirmModal template structure.
        var modal = root.Q("leave-modal");
        var overlay = modal.Q(className: "modal-overlay");
        overlay.RemoveFromClassList("screen--hidden");
        modal.style.display = DisplayStyle.Flex;

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"GameHudLeaveModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            modal,
            panelBounds,
            ctx,
            warn,
            modal.Q<Label>("modal-title"),
            modal.Q<Button>("modal-confirm-btn"),
            modal.Q<Button>("modal-cancel-btn")
        );
    }

    [UnityTest]
    public IEnumerator GameHudRetryModal_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(GameHudUxmlPath, ratio);

        var modal = root.Q("retry-modal");
        var overlay = modal.Q(className: "modal-overlay");
        overlay.RemoveFromClassList("screen--hidden");
        modal.style.display = DisplayStyle.Flex;

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"GameHudRetryModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            modal,
            panelBounds,
            ctx,
            warn,
            modal.Q<Label>("modal-title"),
            modal.Q<Button>("modal-confirm-btn"),
            modal.Q<Button>("modal-cancel-btn")
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
}
