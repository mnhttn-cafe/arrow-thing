using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Drives the main menu UI: screen navigation, board-size selection, and scene transition.
/// Attach to the same GameObject as the UIDocument.
/// </summary>
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField]
    private UIDocument uiDocument;

    [SerializeField]
    private InputActionAsset inputActions;

    private const string GitHubUrl = "https://github.com/vicplusplus/arrow-thing";
    private const string DiscordUrl = "https://discord.gg/FBwTyaWzpE";

    private const int CustomDimMin = 2;
    private const int CustomDimMax = 400;

    private VisualElement _mainMenu;
    private VisualElement _modeSelect;
    private VisualElement _settings;
    private bool _settingsOpen;

    private ConfirmModal _quitModal;
    private ConfirmModal _clearScoresModal;
    private ConfirmModal _externalLinkModal;
    private Label _externalLinkLabel;

    private AccountManager _accountManager;

    // Preset buttons for selection highlight
    private Button _presetSmall;
    private Button _presetMedium;
    private Button _presetLarge;
    private Button _presetXLarge;

    // Custom preset + slider panel
    private Button _presetCustom;
    private VisualElement _customPanel;
    private SnapSlider _customWidthSnap;
    private SnapSlider _customHeightSnap;
    private bool _isCustomSelected;

    // Currently selected board size (default to small)
    private int _selectedWidth = 10;
    private int _selectedHeight = 10;

    private void OnDisable() => ExternalLinks.LinkRequested -= OnExternalLinkRequested;

    private void OnEnable()
    {
        ExternalLinks.LinkRequested += OnExternalLinkRequested;

        var root = uiDocument.rootVisualElement;
        _mainMenu = root.Q("main-menu");
        _modeSelect = root.Q("mode-select");
        _settings = root.Q("settings");

        WireMainMenuButtons();
        WireModeSelect();
        WireSettingsControls();
        WireSettingsNav();
        WireModals(root);

        _accountManager = new AccountManager(_settings, root.Q("logout-modal"));

        ShowScreen(Screen.MainMenu);
        RestoreSelection();
    }

    private void WireMainMenuButtons()
    {
        var continueBtn = _mainMenu.Q<Button>("continue-btn");
        if (SaveManager.HasSave())
            SetVisible(continueBtn, true);

        _mainMenu.Q<Button>("play-btn").clicked += () => ShowScreen(Screen.ModeSelect);
        continueBtn.clicked += OnContinue;
        _mainMenu.Q<Button>("settings-btn").clicked += () => OpenSettings();
        _mainMenu.Q<Button>("link-github-btn").clicked += () => ExternalLinks.Open(GitHubUrl);
        _mainMenu.Q<Button>("link-discord-btn").clicked += () => ExternalLinks.Open(DiscordUrl);

        // Ctrl+O — open/close settings from anywhere in this scene (Input System)
        var menuMap = inputActions.FindActionMap("Menu", true);
        menuMap.FindAction("ToggleSettings", true).performed += _ => ToggleSettings();
        menuMap.Enable();

        // Quit button — desktop only (no quit action on mobile or web)
        var quitBtn = _mainMenu.Q<Button>("quit-btn");
        if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
            quitBtn.style.display = DisplayStyle.None;
        else
            quitBtn.clicked += OnQuitPressed;
    }

    private void WireModeSelect()
    {
        _presetSmall = _modeSelect.Q<Button>("preset-small");
        _presetMedium = _modeSelect.Q<Button>("preset-medium");
        _presetLarge = _modeSelect.Q<Button>("preset-large");
        _presetXLarge = _modeSelect.Q<Button>("preset-xlarge");

        _presetSmall.clicked += () => SelectPreset(10, 10);
        _presetMedium.clicked += () => SelectPreset(20, 20);
        _presetLarge.clicked += () => SelectPreset(40, 40);
        _presetXLarge.clicked += () => SelectPreset(100, 100);

        _presetCustom = _modeSelect.Q<Button>("preset-custom");
        _customPanel = _modeSelect.Q("custom-panel");

        _customWidthSnap = new SnapSlider(
            CustomDimMin,
            CustomDimMax,
            20f,
            smallStep: 1f,
            snapStep: 10f,
            format: "0",
            showLock: true
        );
        _customWidthSnap.OnValueChanged += _ => SelectCustom();
        _customPanel.Q("custom-width-row").Add(_customWidthSnap.Root);

        _customHeightSnap = new SnapSlider(
            CustomDimMin,
            CustomDimMax,
            20f,
            smallStep: 1f,
            snapStep: 10f,
            format: "0",
            showLock: true
        );
        _customHeightSnap.OnValueChanged += _ => SelectCustom();
        _customPanel.Q("custom-height-row").Add(_customHeightSnap.Root);

        _presetCustom.clicked += SelectCustom;
        _modeSelect.Q<Button>("start-btn").clicked += OnStart;
        _modeSelect.Q<Button>("mode-back-btn").clicked += OnModeBack;

        var trophyBtn = _modeSelect.Q<Button>("trophy-btn");
        if (trophyBtn != null)
            trophyBtn.clicked += OnTrophy;
    }

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

        var themeDropdown = _settings.Q<DropdownField>("theme-dropdown");
        var themeChoices = new System.Collections.Generic.List<string>();
        foreach (var t in ThemeManager.Available)
            if (t != null)
                themeChoices.Add(t.name);
        themeDropdown.choices = themeChoices;
        themeDropdown.value = ThemeManager.Current?.name ?? "";
        themeDropdown.RegisterValueChangedCallback(evt =>
        {
            foreach (var t in ThemeManager.Available)
                if (t != null && t.name == evt.newValue)
                {
                    ThemeManager.Apply(t);
                    break;
                }
        });
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
            ScrollToSection(settingsScroll, sectionAccount);
            SetNavActive(navBtns, 0);
        };
        navGameplay.clicked += () =>
        {
            ScrollToSection(settingsScroll, sectionGameplay);
            SetNavActive(navBtns, 1);
        };
        navData.clicked += () =>
        {
            ScrollToSection(settingsScroll, sectionData);
            SetNavActive(navBtns, 2);
        };
        navAbout.clicked += () =>
        {
            ScrollToSection(settingsScroll, sectionAbout);
            SetNavActive(navBtns, 3);
        };

        _settings.Q<Label>("about-version").text = $"v{Application.version} ({GitCommitHash()})";
        _settings.Q<Button>("about-github-btn").clicked += () => ExternalLinks.Open(GitHubUrl);
        _settings.Q<Button>("about-discord-btn").clicked += () => ExternalLinks.Open(DiscordUrl);

        // Only update via scroll position when content is actually scrollable (avoids false
        // "at bottom" triggers when all sections fit in the viewport).
        settingsScroll.verticalScroller.valueChanged += _ =>
        {
            if (settingsScroll.verticalScroller.highValue > 8)
                UpdateSettingsNavActive(settingsScroll, sections, navBtns);
        };

        _settings.Q<Button>("settings-close-btn").clicked += CloseSettings;
        _settings.Q("settings-backdrop").RegisterCallback<PointerDownEvent>(_ => CloseSettings());
        _settings.Q<Button>("clear-scores-btn").clicked += OnClearScores;
    }

    private void WireModals(VisualElement root)
    {
        _quitModal = new ConfirmModal(root.Q("quit-modal"), "Quit game?", "Yes", "No");
        _quitModal.Confirmed += OnQuitConfirm;
        _quitModal.Cancelled += () => _quitModal.Hide();

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

    private void RestoreSelection()
    {
        if (!GameSettings.IsSet)
        {
            SelectPreset(10, 10);
            return;
        }

        bool matchesPreset =
            (GameSettings.Width == 10 && GameSettings.Height == 10)
            || (GameSettings.Width == 20 && GameSettings.Height == 20)
            || (GameSettings.Width == 40 && GameSettings.Height == 40)
            || (GameSettings.Width == 100 && GameSettings.Height == 100);

        if (matchesPreset)
            SelectPreset(GameSettings.Width, GameSettings.Height);
        else
        {
            _customWidthSnap.SetValueWithoutNotify(GameSettings.Width);
            _customHeightSnap.SetValueWithoutNotify(GameSettings.Height);
            SelectCustom();
        }
    }

    // -- Screen navigation --------------------------------------------------

    private enum Screen
    {
        MainMenu,
        ModeSelect,
    }

    private void ShowScreen(Screen screen)
    {
        SetVisible(_mainMenu, screen == Screen.MainMenu);
        SetVisible(_modeSelect, screen == Screen.ModeSelect);
    }

    private void OpenSettings()
    {
        _settingsOpen = true;
        SetVisible(_settings, true);
    }

    private void CloseSettings()
    {
        _settingsOpen = false;
        _accountManager.CancelEditing();
        SetVisible(_settings, false);
    }

    private void ToggleSettings()
    {
        if (_settingsOpen)
            CloseSettings();
        else
            OpenSettings();
    }

    private static void SetVisible(VisualElement el, bool visible)
    {
        if (visible)
            el.RemoveFromClassList("screen--hidden");
        else
            el.AddToClassList("screen--hidden");
    }

    // -- Callbacks -----------------------------------------------------------

    private void OnContinue()
    {
        GameSettings.ResumeFromSave();
        SceneManager.LoadScene("Game");
    }

    private void OnModeBack() => ShowScreen(Screen.MainMenu);

    private static void ScrollToSection(ScrollView scroll, VisualElement section)
    {
        // layout.y is valid once the element has been laid out
        scroll.schedule.Execute(() =>
        {
            scroll.verticalScroller.value = section.layout.y;
        });
    }

    private static void UpdateSettingsNavActive(
        ScrollView scroll,
        VisualElement[] sections,
        Button[] navBtns
    )
    {
        float scrollY = scroll.verticalScroller.value;

        // At the bottom of a scrollable view → last section is active.
        int active = 0;
        if (scrollY >= scroll.verticalScroller.highValue - 8)
        {
            active = sections.Length - 1;
        }
        else
        {
            // Walk backwards: the last section whose top edge is at or above the scroll position
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

    private void SelectPreset(int width, int height)
    {
        _isCustomSelected = false;
        _selectedWidth = width;
        _selectedHeight = height;
        SetVisible(_customPanel, false);
        UpdateAllPresetHighlights();
    }

    private void SelectCustom()
    {
        _isCustomSelected = true;
        _selectedWidth = Mathf.RoundToInt(_customWidthSnap.Value);
        _selectedHeight = Mathf.RoundToInt(_customHeightSnap.Value);
        SetVisible(_customPanel, true);
        UpdateAllPresetHighlights();
    }

    private void UpdateAllPresetHighlights()
    {
        UpdatePresetHighlight(_presetSmall, 10, 10);
        UpdatePresetHighlight(_presetMedium, 20, 20);
        UpdatePresetHighlight(_presetLarge, 40, 40);
        UpdatePresetHighlight(_presetXLarge, 100, 100);

        if (_isCustomSelected)
            _presetCustom.AddToClassList("preset-btn--selected");
        else
            _presetCustom.RemoveFromClassList("preset-btn--selected");
    }

    private void UpdatePresetHighlight(Button btn, int w, int h)
    {
        if (!_isCustomSelected && w == _selectedWidth && h == _selectedHeight)
            btn.AddToClassList("preset-btn--selected");
        else
            btn.RemoveFromClassList("preset-btn--selected");
    }

    private void OnStart()
    {
        GameSettings.Apply(_selectedWidth, _selectedHeight);
        SceneManager.LoadScene("Game");
    }

    private void OnTrophy()
    {
        SceneManager.LoadScene("Leaderboard");
    }

    private void OnQuitPressed()
    {
        _quitModal.Show();
    }

    private void OnQuitConfirm()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnClearScores()
    {
        _clearScoresModal.Show();
    }

    private void OnClearScoresConfirm()
    {
        _clearScoresModal.Hide();
        var manager = LeaderboardManager.Instance;
        if (manager != null)
            manager.RemoveAllNonFavorited();
    }

    private void OnExternalLinkRequested(string url, System.Action confirm)
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

    private static string GitCommitHash()
    {
        var asset = Resources.Load<TextAsset>("git-commit");
        if (asset != null)
            return asset.text.Trim();
        return "dev";
    }
}
