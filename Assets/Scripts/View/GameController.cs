using UnityEngine;
using UnityEngine.InputSystem;
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

        // Setup input
        var inputHandler = gameObject.AddComponent<InputHandler>();
        inputHandler.Init(_board, _boardView, camCtrl, inputActions, dragThresholdPixels);

        // Setup victory screen
        if (
            victoryUIDocument != null
            && victoryUIDocument.enabled
            && victoryUIDocument.rootVisualElement != null
        )
        {
            var victory = gameObject.AddComponent<VictoryController>();
            victory.Init(victoryUIDocument, _boardView.GridRenderer, camCtrl);
            _boardView.BoardCleared += () =>
            {
                inputHandler.SetInputEnabled(false);
                victory.OnBoardCleared();
            };
        }
    }
}
