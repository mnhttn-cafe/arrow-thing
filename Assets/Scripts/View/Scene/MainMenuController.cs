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

    [SerializeField]
    private SettingsController settingsController;

    private const string GitHubUrl = "https://github.com/vicplusplus/arrow-thing";
    private const string DiscordUrl = "https://discord.gg/FBwTyaWzpE";

    private const int CustomDimMin = 2;
    private const int CustomDimMax = 400;

    private VisualElement _mainMenu;
    private VisualElement _modeSelect;

    private ConfirmModal _quitModal;

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

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _mainMenu = root.Q("main-menu");
        _modeSelect = root.Q("mode-select");

        WireMainMenuButtons();
        WireModeSelect();
        WireModals(root);

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
        _mainMenu.Q<Button>("settings-btn").clicked += settingsController.Open;
        _mainMenu.Q<Button>("link-github-btn").clicked += () => ExternalLinks.Open(GitHubUrl);
        _mainMenu.Q<Button>("link-discord-btn").clicked += () => ExternalLinks.Open(DiscordUrl);

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

    private void WireModals(VisualElement root)
    {
        _quitModal = new ConfirmModal(root.Q("quit-modal"), "Quit game?", "Yes", "No");
        _quitModal.Confirmed += OnQuitConfirm;
        _quitModal.Cancelled += () => _quitModal.Hide();
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
}
