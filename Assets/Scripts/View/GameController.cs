using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Top-level scene controller. Creates the board, spawns the view, and wires input.
/// </summary>
public sealed class GameController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private VisualSettings visualSettings;

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private InputActionAsset inputActions;

    [SerializeField]
    private UIDocument victoryUIDocument;

    [SerializeField]
    private UIDocument hudUIDocument;

    [Header("Timer")]
    [Tooltip("Inspection phase duration in seconds.")]
    [SerializeField]
    private float inspectionDuration = 15f;

    [Tooltip("Inspection countdown turns red at this many seconds remaining.")]
    [SerializeField]
    private float inspectionWarningThreshold = 5f;

    [Header("Input")]
    [Tooltip("Screen-space distance in pixels before a click/tap becomes a drag instead of a tap.")]
    [SerializeField]
    private float dragThresholdPixels = 15f;

    [Header("Editor Overrides (ignored when launched from menu)")]
    [Tooltip(
        "Board width used when playing this scene directly. Ignored when coming from the main menu."
    )]
    [SerializeField]
    private int boardWidth = 6;

    [Tooltip(
        "Board height used when playing this scene directly. Ignored when coming from the main menu."
    )]
    [SerializeField]
    private int boardHeight = 6;

    [SerializeField]
    private int minArrowLength = 2;

    [Tooltip(
        "Max arrow length used when playing this scene directly. Ignored when coming from the main menu."
    )]
    [SerializeField]
    private int maxArrowLength = 5;

    [Tooltip(
        "When checked, generates a random seed each run. When unchecked, uses the seed below. Only applies when playing this scene directly — menu always uses a random seed."
    )]
    [SerializeField]
    private bool useRandomSeed = true;

    [Tooltip(
        "Fixed seed for reproducible boards. Only used when 'Use Random Seed' is unchecked and playing this scene directly."
    )]
    [SerializeField]
    private int seed = 42;

    private Board _board = null!;
    private BoardView _boardView = null!;

    private void Awake()
    {
        if (visualSettings == null)
        {
            Debug.LogError("GameController: VisualSettings is not assigned.");
            return;
        }

        if (inputActions == null)
        {
            Debug.LogError("GameController: InputActions is not assigned.");
            return;
        }

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Set background color
        if (mainCamera != null)
            mainCamera.backgroundColor = visualSettings.backgroundColor;

        // Resolve board parameters: menu overrides take priority, then inspector fields
        int w = boardWidth;
        int h = boardHeight;
        int minLen = minArrowLength;
        int maxLen = maxArrowLength;

        if (GameSettings.IsSet)
        {
            w = GameSettings.Width;
            h = GameSettings.Height;
            maxLen = GameSettings.MaxArrowLength;
        }

        int activeSeed =
            (GameSettings.IsSet || useRandomSeed) ? System.Environment.TickCount : seed;

        // Generate board
        _board = new Board(w, h);
        BoardGeneration.FillBoard(_board, minLen, maxLen, new System.Random(activeSeed));

        // Create board view
        var boardGo = new GameObject("BoardView");
        _boardView = boardGo.AddComponent<BoardView>();
        _boardView.Init(_board, visualSettings);

        // Setup camera
        CameraController camCtrl = null!;
        if (mainCamera != null)
        {
            camCtrl = mainCamera.gameObject.GetComponent<CameraController>();
            if (camCtrl == null)
                camCtrl = mainCamera.gameObject.AddComponent<CameraController>();
            camCtrl.Init(_board);
        }

        // Setup timer
        var timer = new GameTimer(inspectionDuration);
        GameTimerView timerView = null;

        // Setup HUD
        if (hudUIDocument != null && hudUIDocument.rootVisualElement != null)
        {
            var hudRoot = hudUIDocument.rootVisualElement;
            var leaveModal = hudRoot.Q("leave-modal");

            hudRoot.Q<Button>("back-to-menu-btn").clicked += () =>
                leaveModal.RemoveFromClassList("modal--hidden");
            hudRoot.Q<Button>("leave-yes-btn").clicked += () => SceneManager.LoadScene("MainMenu");
            hudRoot.Q<Button>("leave-no-btn").clicked += () =>
                leaveModal.AddToClassList("modal--hidden");

            timerView = gameObject.AddComponent<GameTimerView>();
            timerView.Init(timer, hudUIDocument, inspectionWarningThreshold);

            // Trajectory toggle button (bottom-right)
            var trajBtn = hudRoot.Q<Button>("trajectory-toggle-btn");
            bool trajectoryOn = false;
            trajBtn.clicked += () =>
            {
                trajectoryOn = !trajectoryOn;
                _boardView.SetAllTrajectoriesVisible(trajectoryOn);
                if (trajectoryOn)
                    trajBtn.AddToClassList("hud-btn--active");
                else
                    trajBtn.RemoveFromClassList("hud-btn--active");
            };
            _boardView.TrajectoryAutoOff += () =>
            {
                trajectoryOn = false;
                trajBtn.RemoveFromClassList("hud-btn--active");
            };
        }

        // Setup input
        var inputHandler = gameObject.AddComponent<InputHandler>();
        inputHandler.Init(_board, _boardView, camCtrl, inputActions, dragThresholdPixels, timer);

        // Wire leave modal to suppress input while visible
        if (hudUIDocument != null && hudUIDocument.rootVisualElement != null)
        {
            var leaveModal = hudUIDocument.rootVisualElement.Q("leave-modal");
            hudUIDocument.rootVisualElement.Q<Button>("back-to-menu-btn").clicked += () =>
                inputHandler.SetInputEnabled(false);
            hudUIDocument.rootVisualElement.Q<Button>("leave-no-btn").clicked += () =>
                inputHandler.SetInputEnabled(true);
        }

        // Setup victory screen
        if (
            victoryUIDocument != null
            && victoryUIDocument.enabled
            && victoryUIDocument.rootVisualElement != null
        )
        {
            var victory = gameObject.AddComponent<VictoryController>();
            victory.Init(victoryUIDocument, _boardView.GridRenderer, camCtrl, timer, hudUIDocument);
            _boardView.BoardCleared += () =>
            {
                inputHandler.SetInputEnabled(false);
                victory.OnBoardCleared();
            };
        }
    }
}
