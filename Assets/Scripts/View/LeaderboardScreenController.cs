using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Scene entry point for the Leaderboard scene. Manages tabs, sorting, entry list,
/// context menu, and auto-scroll from victory screen.
/// </summary>
public sealed class LeaderboardScreenController : MonoBehaviour
{
    [SerializeField]
    private UIDocument uiDocument;

    // Tab definitions: name → (width, height) or (0,0) for All
    private static readonly (string name, int w, int h)[] Tabs =
    {
        ("tab-small", 10, 10),
        ("tab-medium", 20, 20),
        ("tab-large", 40, 40),
        ("tab-xlarge", 100, 100),
        ("tab-all", 0, 0),
    };

    private VisualElement _root;
    private VisualElement _list;
    private ScrollView _scroll;
    private Label _emptyLabel;
    private VisualElement _comingSoon;
    private VisualElement _contextMenu;
    private ConfirmModal _deleteModal;

    private Button[] _tabButtons;
    private Button[] _sortButtons;

    private int _activeTabIndex;
    private SortCriterion _activeSortCriterion = SortCriterion.Fastest;
    private bool _isGlobalView;

    // Context menu state
    private string _contextGameId;
    private bool _contextIsFavorite;
    private string _pendingDeleteGameId;

    // Drag-to-scroll state
    private bool _isDragScrolling;
    private float _dragScrollStartY;
    private float _dragScrollStartValue;

    // Focus (auto-scroll) state from victory screen
    private string _focusGameId;

    private void OnEnable()
    {
        _root = uiDocument.rootVisualElement;
        _list = _root.Q("lb-list");
        _scroll = _root.Q<ScrollView>("lb-scroll");
        _emptyLabel = _root.Q<Label>("lb-empty");
        _comingSoon = _root.Q("lb-coming-soon");
        _contextMenu = _root.Q("lb-context-menu");

        // Back button
        _root.Q<Button>("lb-back-btn").clicked += OnBack;

        // Scope toggle
        var localBtn = _root.Q<Button>("lb-local-btn");
        var globalBtn = _root.Q<Button>("lb-global-btn");
        localBtn.clicked += () => SetScope(false, localBtn, globalBtn);
        globalBtn.clicked += () => SetScope(true, localBtn, globalBtn);

        // Size tabs
        _tabButtons = new Button[Tabs.Length];
        for (int i = 0; i < Tabs.Length; i++)
        {
            int idx = i;
            _tabButtons[i] = _root.Q<Button>(Tabs[i].name);
            _tabButtons[i].clicked += () => SelectTab(idx);
        }

        // Sort buttons
        _sortButtons = new Button[3];
        _sortButtons[0] = _root.Q<Button>("sort-fastest");
        _sortButtons[1] = _root.Q<Button>("sort-biggest");
        _sortButtons[2] = _root.Q<Button>("sort-favorites");
        _sortButtons[0].clicked += () => SelectSort(SortCriterion.Fastest);
        _sortButtons[1].clicked += () => SelectSort(SortCriterion.Biggest);
        _sortButtons[2].clicked += () => SelectSort(SortCriterion.Favorites);

        // Context menu buttons (delete only — favorite is a direct icon toggle)
        _root.Q<Button>("ctx-delete-btn").clicked += OnContextDelete;

        // Delete confirmation modal (reusable ConfirmModal template)
        _deleteModal = new ConfirmModal(
            _root.Q("delete-modal"),
            "Delete this favorited entry?",
            "Delete",
            "Cancel",
            isDanger: true
        );
        _deleteModal.Confirmed += OnDeleteConfirm;
        _deleteModal.Cancelled += OnDeleteCancel;

        // Dismiss context menu on click outside or scroll
        _root.RegisterCallback<PointerDownEvent>(OnRootPointerDown);
        _scroll.RegisterCallback<WheelEvent>(_ => DismissContextMenu());
        _scroll.verticalScroller.valueChanged += _ => DismissContextMenu();

        // Drag-to-scroll on content area
        _scroll.RegisterCallback<PointerDownEvent>(OnScrollPointerDown);
        _scroll.RegisterCallback<PointerMoveEvent>(OnScrollPointerMove);
        _scroll.RegisterCallback<PointerUpEvent>(OnScrollPointerUp);
        _scroll.RegisterCallback<PointerCaptureOutEvent>(_ => _isDragScrolling = false);

        // Handle auto-scroll from victory
        _focusGameId = GameSettings.LeaderboardFocusGameId;
        GameSettings.LeaderboardFocusGameId = null;

        if (_focusGameId != null)
            AutoScrollToFocusEntry();
        else
            SelectTab(0);
    }

    private void AutoScrollToFocusEntry()
    {
        var manager = LeaderboardManager.Instance;
        if (manager == null)
        {
            SelectTab(0);
            return;
        }

        // Find the entry to determine which tab to select
        LeaderboardEntry focusEntry = null;
        foreach (var entry in manager.Store.Entries)
        {
            if (entry.gameId == _focusGameId)
            {
                focusEntry = entry;
                break;
            }
        }

        if (focusEntry == null)
        {
            SelectTab(0);
            return;
        }

        // Find the matching tab
        int targetTab = Tabs.Length - 1; // default to All
        for (int i = 0; i < Tabs.Length - 1; i++)
        {
            if (Tabs[i].w == focusEntry.boardWidth && Tabs[i].h == focusEntry.boardHeight)
            {
                targetTab = i;
                break;
            }
        }

        SelectTab(targetTab);

        // Schedule scroll after layout resolves
        _scroll.schedule.Execute(() => ScrollToFocusEntry()).ExecuteLater(50);
    }

    private void ScrollToFocusEntry()
    {
        if (_focusGameId == null)
            return;

        // Find the focused row element and scroll to it
        foreach (var child in _list.Children())
        {
            if (child.userData is string gameId && gameId == _focusGameId)
            {
                _scroll.ScrollTo(child);
                return;
            }
        }
    }

    // --- Tab / Sort selection ---

    private void SelectTab(int index)
    {
        _activeTabIndex = index;
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            if (i == index)
                _tabButtons[i].AddToClassList("tab-bar__tab--active");
            else
                _tabButtons[i].RemoveFromClassList("tab-bar__tab--active");
        }

        DismissContextMenu();

        // "Biggest" sort is only useful on the All tab
        bool isAllTab = Tabs[index].w == 0 && Tabs[index].h == 0;
        ShowElement(_sortButtons[1], isAllTab);

        // If Biggest was active and we switched to a size tab, fall back to Fastest
        if (!isAllTab && _activeSortCriterion == SortCriterion.Biggest)
            SelectSort(SortCriterion.Fastest);
        else
            RefreshList();
    }

    private void SelectSort(SortCriterion criterion)
    {
        DismissContextMenu();
        _activeSortCriterion = criterion;
        int idx = (int)criterion;
        for (int i = 0; i < _sortButtons.Length; i++)
        {
            if (i == idx)
                _sortButtons[i].AddToClassList("filter-row__btn--active");
            else
                _sortButtons[i].RemoveFromClassList("filter-row__btn--active");
        }
        RefreshList();
        _scroll.verticalScroller.value = 0;
    }

    private void SetScope(bool isGlobal, Button localBtn, Button globalBtn)
    {
        _isGlobalView = isGlobal;
        if (isGlobal)
        {
            globalBtn.AddToClassList("toggle-group__btn--active");
            localBtn.RemoveFromClassList("toggle-group__btn--active");
            ShowElement(_comingSoon, true);
            ShowElement(_scroll, false);
            ShowElement(_emptyLabel, false);
        }
        else
        {
            localBtn.AddToClassList("toggle-group__btn--active");
            globalBtn.RemoveFromClassList("toggle-group__btn--active");
            ShowElement(_comingSoon, false);
            RefreshList();
        }
    }

    // --- List population ---

    private void RefreshList()
    {
        if (_isGlobalView)
            return;

        var manager = LeaderboardManager.Instance;
        if (manager == null)
        {
            ShowEmpty(true);
            return;
        }

        var (w, h) = (Tabs[_activeTabIndex].w, Tabs[_activeTabIndex].h);
        bool isAllTab = w == 0 && h == 0;

        List<LeaderboardEntry> entries = isAllTab
            ? manager.Store.GetAllEntries()
            : manager.Store.GetEntries(w, h);

        entries = LeaderboardStore.SortBy(entries, _activeSortCriterion);

        _list.Clear();

        if (entries.Count == 0)
        {
            ShowEmpty(true);
            return;
        }

        ShowEmpty(false);

        var best = isAllTab ? null : manager.Store.GetPersonalBest(w, h);

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var row = CreateEntryRow(i + 1, entry, isAllTab, best);
            _list.Add(row);
        }
    }

    private VisualElement CreateEntryRow(
        int rank,
        LeaderboardEntry entry,
        bool showSize,
        LeaderboardEntry best
    )
    {
        var row = new VisualElement();
        row.AddToClassList("lb-entry");
        row.userData = entry.gameId;

        bool isBest = best != null && entry.gameId == best.gameId;
        bool isFocused = _focusGameId != null && entry.gameId == _focusGameId;

        if (isBest)
            row.AddToClassList("lb-entry--best");
        if (isFocused)
            row.AddToClassList("lb-entry--focused");

        // Rank
        var rankLabel = new Label($"#{rank}");
        rankLabel.AddToClassList("lb-rank");
        if (isBest)
            rankLabel.AddToClassList("lb-rank--best");
        row.Add(rankLabel);

        // Size (All tab only)
        if (showSize)
        {
            var sizeLabel = new Label($"{entry.boardWidth}x{entry.boardHeight}");
            sizeLabel.AddToClassList("lb-size");
            row.Add(sizeLabel);
        }

        // Time
        var timeLabel = new Label(FormatTime(entry.solveTime));
        timeLabel.AddToClassList("lb-time");
        row.Add(timeLabel);

        // Date (relative text, tooltip shows exact date+time)
        var dateLabel = new Label(FormatRelativeDate(entry.completedAt));
        dateLabel.AddToClassList("lb-date");
        dateLabel.tooltip = FormatExactDate(entry.completedAt);
        row.Add(dateLabel);

        // Favorite icon (clickable toggle)
        string capturedGameId = entry.gameId;
        bool capturedFav = entry.isFavorite;
        var favBtn = new Button(() => OnToggleFavorite(capturedGameId, capturedFav));
        favBtn.AddToClassList("lb-fav-btn");
        var favIcon = new VisualElement();
        favIcon.AddToClassList("lb-fav-icon");
        favIcon.AddToClassList(entry.isFavorite ? "lb-fav-icon--on" : "lb-fav-icon--off");
        favBtn.Add(favIcon);
        row.Add(favBtn);

        // Play button
        var playBtn = new Button(() => OnPlayReplay(capturedGameId));
        playBtn.AddToClassList("lb-play-btn");
        var playIcon = new VisualElement();
        playIcon.AddToClassList("lb-play-icon");
        playBtn.Add(playIcon);
        row.Add(playBtn);

        // Context menu trigger (delete only)
        var ctxBtn = new Button(() => ShowContextMenu(capturedGameId, capturedFav, row));
        ctxBtn.AddToClassList("lb-ctx-trigger");
        var ctxIcon = new VisualElement();
        ctxIcon.AddToClassList("lb-ctx-trigger-icon");
        ctxBtn.Add(ctxIcon);
        row.Add(ctxBtn);

        return row;
    }

    private void ShowEmpty(bool show)
    {
        ShowElement(_emptyLabel, show);
        ShowElement(_scroll, !show);
    }

    // --- Context menu ---

    private void ShowContextMenu(string gameId, bool isFavorite, VisualElement anchorRow)
    {
        _contextGameId = gameId;
        _contextIsFavorite = isFavorite;

        // Position near the anchor row, flipping above if it would overflow
        var rowBounds = anchorRow.worldBound;
        float panelHeight = _root.resolvedStyle.height;
        float menuHeight = _contextMenu.resolvedStyle.height;
        if (menuHeight <= 0)
            menuHeight = 60; // fallback estimate

        bool fitsBelow = rowBounds.yMax + menuHeight <= panelHeight;

        _contextMenu.style.right = 16;
        _contextMenu.style.left = StyleKeyword.Auto;

        if (fitsBelow)
        {
            _contextMenu.style.top = rowBounds.yMax;
            _contextMenu.style.bottom = StyleKeyword.Auto;
        }
        else
        {
            _contextMenu.style.bottom = panelHeight - rowBounds.yMin;
            _contextMenu.style.top = StyleKeyword.Auto;
        }

        ShowElement(_contextMenu, true);
    }

    private void DismissContextMenu()
    {
        ShowElement(_contextMenu, false);
        _contextGameId = null;
    }

    // --- Drag-to-scroll ---

    private void OnScrollPointerDown(PointerDownEvent evt)
    {
        // Dismiss context menu if open (StopPropagation prevents OnRootPointerDown)
        DismissContextMenu();

        // Only drag-scroll when content overflows the viewport
        if (_scroll.verticalScroller.highValue <= 0)
            return;

        _isDragScrolling = true;
        _dragScrollStartY = evt.position.y;
        _dragScrollStartValue = _scroll.verticalScroller.value;
        _scroll.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnScrollPointerMove(PointerMoveEvent evt)
    {
        if (!_isDragScrolling)
            return;
        float delta = _dragScrollStartY - evt.position.y;
        _scroll.verticalScroller.value = _dragScrollStartValue + delta;
    }

    private void OnScrollPointerUp(PointerUpEvent evt)
    {
        if (!_isDragScrolling)
            return;
        _isDragScrolling = false;
        _scroll.ReleasePointer(evt.pointerId);
    }

    private void OnRootPointerDown(PointerDownEvent evt)
    {
        if (_contextMenu.ClassListContains("lb--hidden"))
            return;

        // Check if click is inside the context menu
        if (_contextMenu.worldBound.Contains(evt.position))
            return;

        DismissContextMenu();
    }

    private void OnToggleFavorite(string gameId, bool currentlyFavorite)
    {
        var manager = LeaderboardManager.Instance;
        if (manager != null)
            manager.SetFavorite(gameId, !currentlyFavorite);
        RefreshList();
    }

    private void OnContextDelete()
    {
        if (_contextGameId == null)
            return;

        // Re-check favorite status in case it was toggled while the context menu was open
        var manager = LeaderboardManager.Instance;
        if (manager != null)
            _contextIsFavorite = manager.IsFavorite(_contextGameId);

        // If favorited, show confirmation modal
        if (_contextIsFavorite)
        {
            _pendingDeleteGameId = _contextGameId;
            DismissContextMenu();
            _deleteModal.Show();
        }
        else
        {
            PerformDelete();
        }
    }

    private void OnDeleteConfirm()
    {
        _deleteModal.Hide();
        if (_pendingDeleteGameId != null)
        {
            var manager = LeaderboardManager.Instance;
            if (manager != null)
                manager.RemoveEntry(_pendingDeleteGameId);
            _pendingDeleteGameId = null;
            RefreshList();
        }
    }

    private void OnDeleteCancel()
    {
        _deleteModal.Hide();
        _pendingDeleteGameId = null;
    }

    private void PerformDelete()
    {
        if (_contextGameId == null)
            return;

        var manager = LeaderboardManager.Instance;
        if (manager != null)
            manager.RemoveEntry(_contextGameId);

        DismissContextMenu();
        RefreshList();
    }

    // --- Replay launch ---

    private void OnPlayReplay(string gameId)
    {
        var manager = LeaderboardManager.Instance;
        if (manager == null)
            return;

        var replay = manager.LoadReplay(gameId);
        if (replay == null)
        {
            Debug.LogWarning($"LeaderboardScreen: replay not found for {gameId}");
            return;
        }

        GameSettings.LeaderboardFocusGameId = gameId;
        GameSettings.StartReplay(replay, "Leaderboard");
        SceneManager.LoadScene("ReplayViewer");
    }

    // --- Navigation ---

    private void OnBack()
    {
        SceneManager.LoadScene("MainMenu");
    }

    // --- Formatting helpers ---

    private static string FormatTime(double seconds)
    {
        if (seconds < 0)
            seconds = 0;
        int totalMillis = (int)(seconds * 1000);
        int hours = totalMillis / 3600000;
        int mins = (totalMillis % 3600000) / 60000;
        int secs = (totalMillis % 60000) / 1000;
        int millis = totalMillis % 1000;

        if (hours > 0)
            return $"{hours}:{mins:D2}:{secs:D2}.{millis:D3}";
        if (mins > 0)
            return $"{mins}:{secs:D2}.{millis:D3}";
        return $"{secs}.{millis:D3}";
    }

    private static string FormatRelativeDate(string iso8601)
    {
        if (string.IsNullOrEmpty(iso8601))
            return "";

        DateTime date;
        if (
            !DateTime.TryParse(
                iso8601,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out date
            )
        )
            return "";

        var span = DateTime.UtcNow - date.ToUniversalTime();

        if (span.TotalMinutes < 1)
            return "now";
        if (span.TotalHours < 1)
            return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1)
            return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 30)
            return $"{(int)span.TotalDays}d ago";
        if (span.TotalDays < 365)
            return $"{(int)(span.TotalDays / 30)}mo ago";

        return $"{(int)(span.TotalDays / 365)}yr ago";
    }

    private static string FormatExactDate(string iso8601)
    {
        if (string.IsNullOrEmpty(iso8601))
            return "";

        DateTime date;
        if (
            !DateTime.TryParse(
                iso8601,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out date
            )
        )
            return "";

        return date.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static void ShowElement(VisualElement el, bool show)
    {
        if (el == null)
            return;
        if (show)
            el.RemoveFromClassList("lb--hidden");
        else
            el.AddToClassList("lb--hidden");
    }
}
