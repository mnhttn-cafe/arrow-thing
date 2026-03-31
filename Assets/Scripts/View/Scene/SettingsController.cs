using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Singleton settings panel that persists across all scenes. Bootstrapped automatically
/// from Assets/Resources/SettingsController.prefab before the first scene loads.
/// Call SettingsController.Instance.Open() / .Close() / .Toggle() from any scene.
/// The prefab must have a UIDocument (pointing to SettingsDocument.uxml) with a
/// PanelSettings sort order higher than the game UI, a UIThemeApplier, and this
/// component with InputActionAsset assigned.
/// </summary>
public sealed class SettingsController : MonoBehaviour
{
    public static SettingsController Instance { get; private set; }

    [SerializeField]
    private InputActionAsset inputActions;

    private const string GitHubUrl = "https://github.com/vicplusplus/arrow-thing";
    private const string DiscordUrl = "https://discord.gg/FBwTyaWzpE";

    private VisualElement _settings;
    private AccountManager _accountManager;
    private CustomDropdown _themeDropdown;
    private ConfirmModal _clearScoresModal;
    private ConfirmModal _externalLinkModal;
    private Label _externalLinkLabel;
    private bool _navScrollPending;

    public bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var prefab = Resources.Load<GameObject>("SettingsController");
        if (prefab == null)
        {
            Debug.LogError(
                "[SettingsController] Prefab not found at Resources/SettingsController. "
                    + "Create a prefab there with UIDocument, UIThemeApplier, and SettingsController."
            );
            return;
        }
        var go = Instantiate(prefab);
        go.name = "SettingsController";
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        ExternalLinks.LinkRequested += OnExternalLinkRequested;
    }

    private void OnDisable()
    {
        ExternalLinks.LinkRequested -= OnExternalLinkRequested;
    }

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _settings = root.Q("settings");

        if (_settings == null)
        {
            Debug.LogError(
                "[SettingsController] Could not find 'settings' element in UIDocument root. "
                    + "Ensure the UIDocument's Source Asset on the SettingsController prefab "
                    + "is set to SettingsDocument.uxml."
            );
            return;
        }

        WireSettingsControls();
        WireSettingsNav();
        WireModals(root);

        _accountManager = new AccountManager(_settings, root.Q("logout-modal"));

        if (inputActions != null)
        {
            var menuMap = inputActions.FindActionMap("Menu", true);
            menuMap.FindAction("ToggleSettings", true).performed += _ => Toggle();
            menuMap.Enable();
        }
    }

    public void Open()
    {
        IsOpen = true;
        SetVisible(_settings, true);
    }

    public void Close()
    {
        IsOpen = false;
        if (_themeDropdown != null)
            _themeDropdown.Close();
        _accountManager.CancelEditing();
        SetVisible(_settings, false);
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    // -- Settings controls --------------------------------------------------

    private void WireSettingsControls()
    {
        float savedThreshold = PlayerPrefs.GetFloat(
            GameSettings.DragThresholdPrefKey,
            GameSettings.DefaultDragThreshold
        );
        var dragSnap = new SnapSlider(
            GameSettings.MinDragThreshold,
            GameSettings.MaxDragThreshold,
            savedThreshold,
            smallStep: 1f,
            snapStep: 0f,
            format: "0",
            showLock: false
        );
        dragSnap.OnValueChanged += val =>
            PlayerPrefs.SetFloat(GameSettings.DragThresholdPrefKey, val);
        dragSnap.Root.AddToClassList("setting-snap-slider");
        _settings.Q("drag-threshold-row").Add(dragSnap.Root);

        float savedZoom = PlayerPrefs.GetFloat(
            GameSettings.ZoomSpeedPrefKey,
            GameSettings.DefaultZoomSpeed
        );
        var zoomSnap = new SnapSlider(
            GameSettings.MinZoomSpeed,
            GameSettings.MaxZoomSpeed,
            savedZoom,
            smallStep: 0.1f,
            snapStep: 0f,
            format: "F1",
            showLock: false
        );
        zoomSnap.OnValueChanged += val => PlayerPrefs.SetFloat(GameSettings.ZoomSpeedPrefKey, val);
        zoomSnap.Root.AddToClassList("setting-snap-slider");
        _settings.Q("zoom-speed-row").Add(zoomSnap.Root);

        bool savedColoring = PlayerPrefs.GetInt(GameSettings.ArrowColoringPrefKey, 0) == 1;
        var coloringToggle = _settings.Q<Toggle>("arrow-coloring-toggle");
        coloringToggle.value = savedColoring;
        coloringToggle.RegisterValueChangedCallback(evt =>
            PlayerPrefs.SetInt(GameSettings.ArrowColoringPrefKey, evt.newValue ? 1 : 0)
        );

        var themeChoices = new System.Collections.Generic.List<string>();
        foreach (var t in ThemeManager.Available)
            if (t != null)
                themeChoices.Add(t.name);
        _themeDropdown = new CustomDropdown(
            themeChoices,
            ThemeManager.Current != null ? ThemeManager.Current.name : ""
        );
        _themeDropdown.ValueChanged += name =>
        {
            foreach (var t in ThemeManager.Available)
                if (t != null && t.name == name)
                {
                    ThemeManager.Apply(t);
                    break;
                }
        };
        _settings.Q("theme-dropdown-slot").Add(_themeDropdown.Root);
    }

    private void WireSettingsNav()
    {
        var settingsScroll = _settings.Q<ScrollView>("settings-scroll");
        var sectionAccount = _settings.Q("section-account");
        var sectionGameplay = _settings.Q("section-gameplay");
        var sectionData = _settings.Q("section-data");
        var sectionAbout = _settings.Q("section-about");

        var navAccount = _settings.Q<Button>("nav-account");
        var navGameplay = _settings.Q<Button>("nav-gameplay");
        var navData = _settings.Q<Button>("nav-data");
        var navAbout = _settings.Q<Button>("nav-about");
        Button[] navBtns = { navAccount, navGameplay, navData, navAbout };
        VisualElement[] sections = { sectionAccount, sectionGameplay, sectionData, sectionAbout };

        navAccount.clicked += () =>
        {
            _navScrollPending = true;
            ScrollToSection(settingsScroll, sectionAccount);
            SetNavActive(navBtns, 0);
        };
        navGameplay.clicked += () =>
        {
            _navScrollPending = true;
            ScrollToSection(settingsScroll, sectionGameplay);
            SetNavActive(navBtns, 1);
        };
        navData.clicked += () =>
        {
            _navScrollPending = true;
            ScrollToSection(settingsScroll, sectionData);
            SetNavActive(navBtns, 2);
        };
        navAbout.clicked += () =>
        {
            _navScrollPending = true;
            ScrollToSection(settingsScroll, sectionAbout);
            SetNavActive(navBtns, 3);
        };

        _settings.Q<Label>("about-version").text = $"v{Application.version} ({GitCommitHash()})";
        _settings.Q<Button>("about-github-btn").clicked += () => ExternalLinks.Open(GitHubUrl);
        _settings.Q<Button>("about-discord-btn").clicked += () => ExternalLinks.Open(DiscordUrl);

        settingsScroll.verticalScroller.valueChanged += _ =>
        {
            if (_navScrollPending)
            {
                _navScrollPending = false;
                return;
            }
            if (settingsScroll.verticalScroller.highValue > 8)
                UpdateSettingsNavActive(settingsScroll, sections, navBtns);
        };

        _settings.Q<Button>("settings-close-btn").clicked += Close;
        _settings.Q("settings-backdrop").RegisterCallback<PointerDownEvent>(_ => Close());
        _settings.Q<Button>("clear-scores-btn").clicked += OnClearScores;
    }

    private void WireModals(VisualElement root)
    {
        _clearScoresModal = new ConfirmModal(
            root.Q("clear-scores-modal"),
            "Delete all non-favorited local scores?",
            "Delete",
            "Cancel",
            subtitle: "Favorited entries will be kept.",
            isDanger: true
        );
        _clearScoresModal.Confirmed += OnClearScoresConfirm;
        _clearScoresModal.Cancelled += () => _clearScoresModal.Hide();

        _externalLinkModal = new ConfirmModal(
            root.Q("external-link-modal"),
            "Open external link?",
            "Open",
            "Cancel",
            subtitle: ""
        );
        _externalLinkLabel = root.Q("external-link-modal").Q<Label>("modal-subtitle");
    }

    // -- Callbacks -----------------------------------------------------------

    private void OnClearScores() => _clearScoresModal.Show();

    private void OnClearScoresConfirm()
    {
        _clearScoresModal.Hide();
        LeaderboardManager.Instance.RemoveAllNonFavorited();
    }

    private void OnExternalLinkRequested(string url, Action confirm)
    {
        _externalLinkLabel.text = url;
        _externalLinkModal.Show();
        _externalLinkModal.Confirmed += OnConfirm;
        _externalLinkModal.Cancelled += OnCancel;

        void Cleanup()
        {
            _externalLinkModal.Confirmed -= OnConfirm;
            _externalLinkModal.Cancelled -= OnCancel;
        }

        void OnConfirm()
        {
            Cleanup();
            _externalLinkModal.Hide();
            confirm();
        }

        void OnCancel()
        {
            Cleanup();
            _externalLinkModal.Hide();
        }
    }

    // -- Helpers ------------------------------------------------------------

    private static void ScrollToSection(ScrollView scroll, VisualElement section)
    {
        scroll.schedule.Execute(() => scroll.verticalScroller.value = section.layout.y);
    }

    private static void UpdateSettingsNavActive(
        ScrollView scroll,
        VisualElement[] sections,
        Button[] navBtns
    )
    {
        float scrollY = scroll.verticalScroller.value;
        int active = 0;
        if (scrollY >= scroll.verticalScroller.highValue - 8)
        {
            active = sections.Length - 1;
        }
        else
        {
            for (int i = sections.Length - 1; i >= 0; i--)
            {
                if (scrollY >= sections[i].layout.y - 8)
                {
                    active = i;
                    break;
                }
            }
        }
        SetNavActive(navBtns, active);
    }

    private static void SetNavActive(Button[] navBtns, int active)
    {
        for (int i = 0; i < navBtns.Length; i++)
        {
            if (i == active)
                navBtns[i].AddToClassList("settings-nav-btn--active");
            else
                navBtns[i].RemoveFromClassList("settings-nav-btn--active");
        }
    }

    private static void SetVisible(VisualElement el, bool visible)
    {
        if (visible)
            el.RemoveFromClassList("screen--hidden");
        else
            el.AddToClassList("screen--hidden");
    }

    private static string GitCommitHash()
    {
        var asset = Resources.Load<TextAsset>("git-commit");
        return asset != null ? asset.text.Trim() : "dev";
    }
}
