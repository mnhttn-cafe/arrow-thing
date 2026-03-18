using System.Collections;
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

    [Header("Loading Screen")]
    [Tooltip("Duration of the loading screen fade in/out in seconds.")]
    [SerializeField]
    private float loadingFadeDuration = 0.3f;

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

        StartCoroutine(GenerateAndSetup());
    }

    private IEnumerator GenerateAndSetup()
    {
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

        // Generate board, overlapping with loading overlay fade when needed
        VisualElement loadingOverlay = null;
        if (hudUIDocument != null && hudUIDocument.rootVisualElement != null)
        {
            loadingOverlay = hudUIDocument.rootVisualElement.Q("loading-overlay");
            if (loadingOverlay != null)
                loadingOverlay.style.display = DisplayStyle.None;
        }

        _board = new Board(w, h);
        var generator = BoardGeneration.FillBoardIncremental(
            _board,
            minLen,
            maxLen,
            new System.Random(activeSeed)
        );

        bool generating = generator.MoveNext();

        if (generating && loadingOverlay != null)
        {
            // Generation needs multiple frames — fade in overlay while generating
            loadingOverlay.style.display = DisplayStyle.Flex;
            loadingOverlay.style.opacity = 0f;
            float fadeIn = 0f;

            // Fade in + generate simultaneously
            while (generating)
            {
                fadeIn += Time.deltaTime;
                float t = Mathf.Clamp01(fadeIn / loadingFadeDuration);
                loadingOverlay.style.opacity = t;
                generating = generator.MoveNext();
                yield return null;
            }

            // Fade out from current opacity
            float currentOpacity = Mathf.Clamp01(fadeIn / loadingFadeDuration);
            yield return FadeElement(
                loadingOverlay,
                currentOpacity,
                0f,
                loadingFadeDuration * currentOpacity,
                hide: true
            );
        }
        else
        {
            // Finish any remaining generation (no overlay needed)
            while (generating)
            {
                generating = generator.MoveNext();
                yield return null;
            }
        }

        // Guard: empty board means generation params are too restrictive
        if (_board.Arrows.Count == 0)
        {
            Debug.LogWarning(
                $"BoardGeneration produced 0 arrows (board {w}x{h}, minLen={minLen}, maxLen={maxLen}, seed={activeSeed}). Returning to menu."
            );
            SceneManager.LoadScene("MainMenu");
            yield break;
        }

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
            if (GameSettings.IsSet)
                camCtrl.ZoomSpeed = GameSettings.ZoomSpeed;
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

            // Trail toggle button (bottom-right)
            var trailBtn = hudRoot.Q<Button>("trail-toggle-btn");
            bool trailOn = false;
            trailBtn.clicked += () =>
            {
                trailOn = !trailOn;
                _boardView.SetAllTrailsVisible(trailOn);
                if (trailOn)
                    trailBtn.AddToClassList("hud-btn--active");
                else
                    trailBtn.RemoveFromClassList("hud-btn--active");
            };
            _boardView.TrailAutoOff += () =>
            {
                trailOn = false;
                trailBtn.RemoveFromClassList("hud-btn--active");
            };
        }

        // Setup input — use player's saved drag threshold if coming from menu, otherwise inspector default
        float dragThreshold = GameSettings.IsSet ? GameSettings.DragThreshold : dragThresholdPixels;
        var inputHandler = gameObject.AddComponent<InputHandler>();
        inputHandler.Init(_board, _boardView, camCtrl, inputActions, dragThreshold, timer);

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
            victory.Init(
                victoryUIDocument,
                _boardView.GridRenderer,
                camCtrl,
                w,
                h,
                timer,
                hudUIDocument
            );
            _boardView.BoardCleared += () =>
            {
                inputHandler.SetInputEnabled(false);
                victory.OnBoardCleared();
            };
        }
    }

    private static IEnumerator FadeElement(
        VisualElement element,
        float from,
        float to,
        float duration,
        bool hide = false
    )
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            element.style.opacity = Mathf.Lerp(from, to, t);
            yield return null;
        }
        element.style.opacity = to;
        if (hide)
            element.style.display = DisplayStyle.None;
    }
}
