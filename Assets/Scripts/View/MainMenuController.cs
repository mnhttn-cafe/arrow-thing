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

    private VisualElement _mainMenu;
    private VisualElement _modeSelect;
    private VisualElement _settings;
    private VisualElement _quitModal;

    // Preset buttons for selection highlight
    private Button _presetSmall;
    private Button _presetMedium;
    private Button _presetLarge;

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
        _mainMenu.Q<Button>("play-btn").clicked += OnPlay;
        _mainMenu.Q<Button>("settings-btn").clicked += OnSettings;

        // Quit button — desktop only
        var quitBtn = _mainMenu.Q<Button>("quit-btn");
        if (Application.isMobilePlatform)
        {
            quitBtn.style.display = DisplayStyle.None;
        }
        else
        {
            quitBtn.clicked += OnQuitPressed;
        }

        // Mode select buttons
        _presetSmall = _modeSelect.Q<Button>("preset-small");
        _presetMedium = _modeSelect.Q<Button>("preset-medium");
        _presetLarge = _modeSelect.Q<Button>("preset-large");

        _presetSmall.clicked += () => SelectPreset(10, 10);
        _presetMedium.clicked += () => SelectPreset(20, 20);
        _presetLarge.clicked += () => SelectPreset(40, 40);

        _modeSelect.Q<Button>("start-btn").clicked += OnStart;
        _modeSelect.Q<Button>("mode-back-btn").clicked += OnModeBack;

        // Settings buttons
        _settings.Q<Button>("settings-back-btn").clicked += OnSettingsBack;

        // Quit modal buttons
        _quitModal.Q<Button>("quit-yes-btn").clicked += OnQuitConfirm;
        _quitModal.Q<Button>("quit-no-btn").clicked += OnQuitCancel;

        // Start with main menu visible, everything else hidden
        ShowScreen(Screen.MainMenu);

        // Restore last selection if returning from a game, otherwise default to small
        if (GameSettings.IsSet)
            SelectPreset(GameSettings.Width, GameSettings.Height);
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

    private void OnPlay() => ShowScreen(Screen.ModeSelect);

    private void OnSettings() => ShowScreen(Screen.Settings);

    private void OnModeBack() => ShowScreen(Screen.MainMenu);

    private void OnSettingsBack() => ShowScreen(Screen.MainMenu);

    private void SelectPreset(int width, int height)
    {
        _selectedWidth = width;
        _selectedHeight = height;

        UpdatePresetHighlight(_presetSmall, 10, 10);
        UpdatePresetHighlight(_presetMedium, 20, 20);
        UpdatePresetHighlight(_presetLarge, 40, 40);
    }

    private void UpdatePresetHighlight(Button btn, int w, int h)
    {
        if (w == _selectedWidth && h == _selectedHeight)
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
}
