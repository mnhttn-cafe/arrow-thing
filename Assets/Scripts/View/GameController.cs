using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-level scene controller. Creates the board, spawns the view, and wires input.
/// </summary>
public sealed class GameController : MonoBehaviour
{
    [Header("Board")]
    [SerializeField]
    private int boardWidth = 6;

    [SerializeField]
    private int boardHeight = 6;

    [SerializeField]
    private int seed = 42;

    [SerializeField]
    private int minArrowLength = 2;

    [SerializeField]
    private int maxArrowLength = 5;

    [Header("References")]
    [SerializeField]
    private VisualSettings visualSettings;

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private InputActionAsset inputActions;

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

        // Generate board
        _board = new Board(boardWidth, boardHeight);
        BoardGeneration.FillBoard(_board, minArrowLength, maxArrowLength, new System.Random(seed));

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
        inputHandler.Init(_board, _boardView, camCtrl, inputActions);
    }
}
