using UnityEngine;
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

    private const string GitHubUrl = "https://github.com/vicplusplus/arrow-thing";
    private const string DiscordUrl = "https://discord.gg/FBwTyaWzpE";
    private const string DragThresholdPrefKey = "DragThreshold";
    private const string ZoomSpeedPrefKey = "ZoomSpeed";
    private const string ArrowColoringPrefKey = "ArrowColoring";

    private const int CustomDimMin = 2;
    private const int CustomDimMax = 400;

    private VisualElement _mainMenu;
    private VisualElement _modeSelect;
    private VisualElement _settings;
    private VisualElement _quitModal;
    private VisualElement _infoPanel;

    // Preset buttons for selection highlight
    private Button _presetSmall;
    private Button _presetMedium;
    private Button _presetLarge;
    private Button _presetXLarge;

    // Custom preset card
    private VisualElement _presetCustom;
    private SnapSlider _customWidthSnap;
    private SnapSlider _customHeightSnap;
    private bool _isCustomSelected;

    // Currently selected board size (default to small)
    private int _selectedWidth = 10;
    private int _selectedHeight = 10;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _mainMenu = root.Q("main-menu");
        _modeSelect = root.Q("mode-select");
        _settings = root.Q("settings");
        _quitModal = root.Q("quit-modal");

        // Main menu buttons
        var playBtn = _mainMenu.Q<Button>("play-btn");
        var continueBtn = _mainMenu.Q<Button>("continue-btn");

        if (SaveManager.HasSave())
            SetVisible(continueBtn, true);

        playBtn.clicked += () => ShowScreen(Screen.ModeSelect);
        continueBtn.clicked += OnContinue;

        _mainMenu.Q<Button>("settings-btn").clicked += OnSettings;

        // Quit button — desktop only (no quit action on mobile or web)
        var quitBtn = _mainMenu.Q<Button>("quit-btn");
        if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
        {
            quitBtn.style.display = DisplayStyle.None;
        }
        else
        {
            quitBtn.clicked += OnQuitPressed;
        }

        // Preset grid buttons
        _presetSmall = _modeSelect.Q<Button>("preset-small");
        _presetMedium = _modeSelect.Q<Button>("preset-medium");
        _presetLarge = _modeSelect.Q<Button>("preset-large");
        _presetXLarge = _modeSelect.Q<Button>("preset-xlarge");

        _presetSmall.clicked += () => SelectPreset(10, 10);
        _presetMedium.clicked += () => SelectPreset(20, 20);
        _presetLarge.clicked += () => SelectPreset(40, 40);
        _presetXLarge.clicked += () => SelectPreset(100, 100);

        // Custom preset card
        _presetCustom = _modeSelect.Q("preset-custom");

        _customWidthSnap = new SnapSlider(
            CustomDimMin, CustomDimMax, 20f, smallStep: 1f,
            snapStep: 10f, format: "0", showLock: true);
        _customWidthSnap.OnValueChanged += _ => SelectCustom();
        _modeSelect.Q("custom-width-row").Add(_customWidthSnap.Root);

        _customHeightSnap = new SnapSlider(
            CustomDimMin, CustomDimMax, 20f, smallStep: 1f,
            snapStep: 10f, format: "0", showLock: true);
        _customHeightSnap.OnValueChanged += _ => SelectCustom();
        _modeSelect.Q("custom-height-row").Add(_customHeightSnap.Root);

        _presetCustom.RegisterCallback<ClickEvent>(_ => SelectCustom());

        _modeSelect.Q<Button>("start-btn").clicked += OnStart;
        _modeSelect.Q<Button>("mode-back-btn").clicked += OnModeBack;

        // Settings: drag threshold slider
        float savedThreshold = PlayerPrefs.GetFloat(
            DragThresholdPrefKey,
            GameSettings.DefaultDragThreshold
        );
        GameSettings.DragThreshold = savedThreshold;

        var dragSnap = new SnapSlider(
            GameSettings.MinDragThreshold, GameSettings.MaxDragThreshold,
            savedThreshold, smallStep: 1f, snapStep: 0f, format: "0", showLock: false);
        dragSnap.OnValueChanged += val =>
        {
            GameSettings.DragThreshold = val;
            PlayerPrefs.SetFloat(DragThresholdPrefKey, val);
        };
        dragSnap.Root.AddToClassList("setting-snap-slider");
        _settings.Q("drag-threshold-row").Add(dragSnap.Root);

        // Settings: zoom speed slider
        float savedZoom = PlayerPrefs.GetFloat(ZoomSpeedPrefKey, GameSettings.DefaultZoomSpeed);
        GameSettings.ZoomSpeed = savedZoom;

        var zoomSnap = new SnapSlider(
            GameSettings.MinZoomSpeed, GameSettings.MaxZoomSpeed,
            savedZoom, smallStep: 0.1f, snapStep: 0f, format: "F1", showLock: false);
        zoomSnap.OnValueChanged += val =>
        {
            GameSettings.ZoomSpeed = val;
            PlayerPrefs.SetFloat(ZoomSpeedPrefKey, val);
        };
        zoomSnap.Root.AddToClassList("setting-snap-slider");
        _settings.Q("zoom-speed-row").Add(zoomSnap.Root);

        // Settings: arrow coloring toggle
        bool savedColoring = PlayerPrefs.GetInt(ArrowColoringPrefKey, 0) == 1;
        GameSettings.ArrowColoring = savedColoring;

        var coloringToggle = _settings.Q<Toggle>("arrow-coloring-toggle");
        coloringToggle.value = savedColoring;
        coloringToggle.RegisterValueChangedCallback(evt =>
        {
            GameSettings.ArrowColoring = evt.newValue;
            PlayerPrefs.SetInt(ArrowColoringPrefKey, evt.newValue ? 1 : 0);
        });

        // Settings buttons
        _settings.Q<Button>("settings-back-btn").clicked += OnSettingsBack;

        // Info button + panel
        _infoPanel = _mainMenu.Q("info-panel");
        _mainMenu.Q<Button>("info-btn").clicked += OnInfoToggle;
        _mainMenu.Q<Label>("info-version").text = $"v{Application.version} ({GitCommitHash()})";

        // Bottom-right link buttons
        _mainMenu.Q<Button>("link-github-btn").clicked += () => Application.OpenURL(GitHubUrl);
        _mainMenu.Q<Button>("link-discord-btn").clicked += () => Application.OpenURL(DiscordUrl);

        // Quit modal buttons
        _quitModal.Q<Button>("quit-yes-btn").clicked += OnQuitConfirm;
        _quitModal.Q<Button>("quit-no-btn").clicked += OnQuitCancel;

        // Start with main menu visible, everything else hidden
        ShowScreen(Screen.MainMenu);

        // Restore last selection if returning from a game, otherwise default to small
        if (GameSettings.IsSet)
        {
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
        else
            SelectPreset(10, 10);
    }

    // -- Screen navigation --------------------------------------------------

    private enum Screen
    {
        MainMenu,
        ModeSelect,
        Settings,
    }

    private void ShowScreen(Screen screen)
    {
        SetVisible(_mainMenu, screen == Screen.MainMenu);
        SetVisible(_modeSelect, screen == Screen.ModeSelect);
        SetVisible(_settings, screen == Screen.Settings);
        SetVisible(_quitModal, false);
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
        ReplayData data = SaveManager.Load();
        if (data == null)
        {
            // Save was corrupted — fall back to fresh game
            ShowScreen(Screen.ModeSelect);
            return;
        }
        GameSettings.Resume(data);
        SceneManager.LoadScene("Game");
    }

    private void OnSettings() => ShowScreen(Screen.Settings);

    private void OnModeBack() => ShowScreen(Screen.MainMenu);

    private void OnSettingsBack() => ShowScreen(Screen.MainMenu);

    private void SelectPreset(int width, int height)
    {
        _isCustomSelected = false;
        _selectedWidth = width;
        _selectedHeight = height;
        UpdateAllPresetHighlights();
    }

    private void SelectCustom()
    {
        _isCustomSelected = true;
        _selectedWidth = Mathf.RoundToInt(_customWidthSnap.Value);
        _selectedHeight = Mathf.RoundToInt(_customHeightSnap.Value);
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

    private void OnQuitPressed()
    {
        SetVisible(_quitModal, true);
    }

    private void OnQuitConfirm()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnQuitCancel()
    {
        SetVisible(_quitModal, false);
    }

    private void OnInfoToggle()
    {
        bool isHidden = _infoPanel.ClassListContains("screen--hidden");
        SetVisible(_infoPanel, isHidden);
    }

    private static string GitCommitHash()
    {
        var asset = Resources.Load<TextAsset>("git-commit");
        if (asset != null)
            return asset.text.Trim();
        return "dev";
    }
}
