using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

[TestFixture]
public class LeaderboardLayoutTests : UILayoutTestBase
{
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

    [UnityTest]
    public IEnumerator Leaderboard_GlobalView_TabBarWithRefresh_FitsWithinBounds(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        // Simulate global view: unhide the refresh button
        root.Q<Button>("lb-refresh-btn").RemoveFromClassList("lb--hidden");

        // Simulate the controller's responsive tab labels: on narrow viewports
        // the controller abbreviates labels via GeometryChangedEvent.
        string[] shortLabels = { "S", "M", "L", "XL", "All" };
        string[] tabNames = { "tab-small", "tab-medium", "tab-large", "tab-xlarge", "tab-all" };
        if (ratio.Width < 420)
        {
            for (int i = 0; i < tabNames.Length; i++)
                root.Q<Button>(tabNames[i]).text = shortLabels[i];
        }

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var lb = root.Q("leaderboard-root");
        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_GlobalTabBar @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            lb,
            panelBounds,
            ctx,
            warn,
            lb.Q<Button>("tab-small"),
            lb.Q<Button>("tab-medium"),
            lb.Q<Button>("tab-large"),
            lb.Q<Button>("tab-xlarge"),
            lb.Q<Button>("tab-all"),
            lb.Q<Button>("lb-refresh-btn")
        );
    }

    [UnityTest]
    public IEnumerator Leaderboard_EntryRows_AllTab_FitWithinBounds(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        var list = root.Q("lb-list");
        for (int i = 0; i < 3; i++)
            list.Add(CreateMockEntryRow(i + 1, showSize: true, compactTime: true));

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_EntryRows_All @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var rows = list.Query(className: "lb-entry").ToList();
        foreach (var row in rows)
            UILayoutTestHelper.AssertAllVisibleChildren(row, panelBounds, ctx, warn);
    }

    [UnityTest]
    public IEnumerator Leaderboard_EntryRows_AllTab_LongNames_FitWithinBounds(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        var list = root.Q("lb-list");
        list.Add(
            CreateMockEntryRow(
                1,
                showSize: true,
                compactTime: true,
                name: "WWWWWWWWWWWWWWWWWWWWWWWWWWWW"
            )
        );
        list.Add(
            CreateMockEntryRow(
                2,
                showSize: true,
                compactTime: true,
                name: "A Very Long Display Name That Should Not Break Layout"
            )
        );
        list.Add(CreateMockEntryRow(3, showSize: true, compactTime: true, timeText: "12h 34m"));

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_EntryRows_All_LongNames @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var rows = list.Query(className: "lb-entry").ToList();
        foreach (var row in rows)
            UILayoutTestHelper.AssertAllVisibleChildren(row, panelBounds, ctx, warn);
    }

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

    [UnityTest]
    public IEnumerator Leaderboard_EntryRows_SizeTab_LongNames_FitWithinBounds(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        var list = root.Q("lb-list");
        list.Add(CreateMockEntryRow(1, showSize: false, name: "WWWWWWWWWWWWWWWWWWWWWWWWWWWW"));
        list.Add(
            CreateMockEntryRow(
                2,
                showSize: false,
                name: "A Very Long Display Name That Should Not Break Layout"
            )
        );
        list.Add(CreateMockEntryRow(3, showSize: false, timeText: "59:59.999"));

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_EntryRows_Size_LongNames @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        var rows = list.Query(className: "lb-entry").ToList();
        foreach (var row in rows)
            UILayoutTestHelper.AssertAllVisibleChildren(row, panelBounds, ctx, warn);
    }

    [UnityTest]
    public IEnumerator Leaderboard_EmptyState_Visible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

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

    [UnityTest]
    public IEnumerator Leaderboard_DeleteModal_Visible(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        var modalInstance = root.Q("delete-modal");
        modalInstance.style.display = DisplayStyle.Flex;
        var overlay = modalInstance.Q(className: "modal-overlay");
        overlay.RemoveFromClassList("screen--hidden");

        modalInstance.Q<Label>("modal-title").text = "Delete this favorited entry?";
        modalInstance.Q<Button>("modal-confirm-btn").text = "Delete";
        modalInstance.Q<Button>("modal-cancel-btn").text = "Cancel";

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_DeleteModal @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(
            overlay,
            panelBounds,
            ctx,
            warn,
            modalInstance.Q<Button>("modal-confirm-btn"),
            modalInstance.Q<Button>("modal-cancel-btn")
        );
    }

    [UnityTest]
    public IEnumerator Leaderboard_GlobalView_PlayerPanel_FitsWithinBounds(
        [ValueSource(typeof(UILayoutTestHelper), nameof(UILayoutTestHelper.StandardAspectRatios))]
            UILayoutTestHelper.AspectRatio ratio
    )
    {
        var root = SetUpDocument(LeaderboardUxmlPath, ratio);

        // Simulate global view: unhide player panel with longest text + play button
        var playerPanel = root.Q("lb-player-panel");
        playerPanel.RemoveFromClassList("lb--hidden");
        root.Q<Label>("lb-player-panel-label").text =
            "No scores yet for this board size. Play a game to enter the leaderboard.";
        var playBtn = root.Q<Button>("lb-player-play-btn");
        playBtn.RemoveFromClassList("lb--hidden");

        yield return UILayoutTestHelper.WaitForLayoutResolve();

        var lb = root.Q("leaderboard-root");
        var panelBounds = root.worldBound;
        string ctx = $"Leaderboard_PlayerPanel @ {ratio.Name}";
        bool warn = IsKnownIssueRatio(ratio);

        AssertElements(lb, panelBounds, ctx, warn, playerPanel, playBtn);
    }

    private static VisualElement CreateMockEntryRow(
        int rank,
        bool showSize,
        bool compactTime = false,
        string name = "Player",
        string timeText = null
    )
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

        string time = timeText ?? (compactTime ? "1h 23m" : "12:34.567");
        var timeLabel = new Label(time);
        timeLabel.AddToClassList("lb-time");
        if (compactTime)
            timeLabel.AddToClassList("lb-time--compact");
        row.Add(timeLabel);

        var nameWrapper = new VisualElement();
        nameWrapper.AddToClassList("lb-name-wrapper");
        var nameLabel = new Label(name);
        nameLabel.AddToClassList("lb-name");
        nameWrapper.Add(nameLabel);
        row.Add(nameWrapper);

        var dateLabel = new Label("3 days ago");
        dateLabel.AddToClassList("lb-date");
        row.Add(dateLabel);

        var favBtn = new Button();
        favBtn.AddToClassList("lb-row-btn");
        favBtn.AddToClassList("lb-fav-btn");
        var favIcon = new VisualElement();
        favIcon.AddToClassList("lb-row-btn__icon");
        favIcon.AddToClassList("lb-fav-icon--off");
        favBtn.Add(favIcon);
        row.Add(favBtn);

        var playBtn = new Button();
        playBtn.AddToClassList("lb-row-btn");
        var playIcon = new VisualElement();
        playIcon.AddToClassList("lb-row-btn__icon");
        playIcon.AddToClassList("lb-play-icon");
        playBtn.Add(playIcon);
        row.Add(playBtn);

        var ctxBtn = new Button();
        ctxBtn.AddToClassList("lb-row-btn");
        ctxBtn.AddToClassList("lb-ctx-trigger");
        var ctxIcon = new VisualElement();
        ctxIcon.AddToClassList("lb-row-btn__icon");
        ctxIcon.AddToClassList("lb-ctx-trigger-icon");
        ctxBtn.Add(ctxIcon);
        row.Add(ctxBtn);

        return row;
    }
}
