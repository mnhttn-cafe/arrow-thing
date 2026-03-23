using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

[TestFixture]
public class ReplayHudLayoutTests : UILayoutTestBase
{
    [UnityTest]
    public IEnumerator ReplayHud_AllElementsVisible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(ReplayHudUxmlPath, ratio);

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
}
