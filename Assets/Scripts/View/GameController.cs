using System;
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

    // Fields shared between GenerateAndSetup and save/focus-loss callbacks
    private Board _board = null!;
    private BoardView _boardView = null!;
    private GameTimer _timer;
    private ReplayRecorder _recorder;
    private string _gameId;
    private int _activeSeed;
    private int _w;
    private int _h;
    private int _maxLen;
    private float _inspectionDur;

    /// <summary>True once generation is done and gameplay has started.</summary>
    private bool _generationComplete;

    /// <summary>Set to true by the Cancel button during generation.</summary>
    private bool _cancelGeneration;

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
        _w = boardWidth;
        _h = boardHeight;
        int minLen = minArrowLength;
        _maxLen = maxArrowLength;
        _inspectionDur = inspectionDuration;

        ReplayData priorData = null;

        if (GameSettings.IsSet)
        {
            _w = GameSettings.Width;
            _h = GameSettings.Height;
            _maxLen = GameSettings.MaxArrowLength;

            if (GameSettings.IsResuming)
            {
                priorData = GameSettings.ResumeData;
                _inspectionDur = priorData.inspectionDuration;
            }
        }

        _activeSeed =
            (priorData != null) ? priorData.seed
            : (GameSettings.IsSet || useRandomSeed) ? Environment.TickCount
            : seed;

        // Generate board, overlapping with loading overlay fade when needed
        VisualElement loadingOverlay = null;
        if (hudUIDocument != null && hudUIDocument.rootVisualElement != null)
        {
            loadingOverlay = hudUIDocument.rootVisualElement.Q("loading-overlay");
            if (loadingOverlay != null)
                loadingOverlay.style.display = DisplayStyle.None;
        }

        _board = new Board(_w, _h);
        var generator = BoardGeneration.FillBoardIncremental(
            _board,
            minLen,
            _maxLen,
            new System.Random(_activeSeed)
        );

        bool generating = generator.MoveNext();

        if (generating && loadingOverlay != null)
        {
            // Wire cancel button before the generation loop
            var cancelBtn = loadingOverlay.Q<Button>("cancel-generation-btn");
            if (cancelBtn != null)
                cancelBtn.clicked += () => _cancelGeneration = true;

            // Generation needs multiple frames — fade in overlay while generating
            loadingOverlay.style.display = DisplayStyle.Flex;
            loadingOverlay.style.opacity = 0f;
            float fadeIn = 0f;

            // Fade in + generate simultaneously
            while (generating)
            {
                if (_cancelGeneration)
                {
                    SceneManager.LoadScene("MainMenu");
                    yield break;
                }
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
                $"BoardGeneration produced 0 arrows (board {_w}x{_h}, minLen={minLen}, maxLen={_maxLen}, seed={_activeSeed}). Returning to menu."
            );
            SceneManager.LoadScene("MainMenu");
            yield break;
        }

        // --- Resume: apply prior clears with no animation ---
        bool resumeSolving = false;
        double resumeSolveElapsed = 0.0;

        if (priorData != null)
        {
            _gameId = priorData.gameId;

            foreach (ReplayEvent evt in priorData.events)
            {
                if (evt.type == ReplayEventType.Clear)
                {
                    var worldPos = new UnityEngine.Vector3(evt.posX, evt.posY, 0f);
                    Cell cell = BoardCoords.WorldToCell(worldPos, _board.Width, _board.Height);
                    if (_board.Contains(cell))
                    {
                        Arrow arrow = _board.GetArrowAt(cell);
                        if (arrow != null && _board.IsClearable(arrow))
                            _board.RemoveArrow(arrow);
                    }
                }
                if (evt.type == ReplayEventType.StartSolve)
                    resumeSolving = true;
                if (evt.type == ReplayEventType.SessionLeave)
                    resumeSolveElapsed = evt.solveElapsed;
            }

            int nextSeq = priorData.events.Count > 0
                ? priorData.events[priorData.events.Count - 1].seq + 1
                : 0;
            _recorder = new ReplayRecorder(priorData.events, nextSeq);
            _recorder.RecordSessionRejoin();

            // Safety: if all arrows were somehow already cleared, wipe the save and go back
            if (_board.Arrows.Count == 0)
            {
                SaveManager.Delete();
                SceneManager.LoadScene("MainMenu");
                yield break;
            }
        }
        else
        {
            _gameId = System.Guid.NewGuid().ToString();
            _recorder = new ReplayRecorder();
            _recorder.RecordSessionStart();
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
        _timer = new GameTimer(_inspectionDur);
        double wallNow = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        if (resumeSolving && resumeSolveElapsed > 0.0)
            _timer.Resume(wallNow, resumeSolveElapsed);
        else
            _timer.Start(wallNow);

        GameTimerView timerView = null;

        // Setup HUD
        if (hudUIDocument != null && hudUIDocument.rootVisualElement != null)
        {
            var hudRoot = hudUIDocument.rootVisualElement;
            var leaveModal = hudRoot.Q("leave-modal");

            hudRoot.Q<Button>("back-to-menu-btn").clicked += () =>
                leaveModal.RemoveFromClassList("modal--hidden");

            hudRoot.Q<Button>("leave-yes-btn").clicked += OnLeaveConfirmed;
            hudRoot.Q<Button>("leave-no-btn").clicked += () =>
                leaveModal.AddToClassList("modal--hidden");

            timerView = gameObject.AddComponent<GameTimerView>();
            timerView.Init(_timer, hudUIDocument, inspectionWarningThreshold);

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
        inputHandler.Init(_board, _boardView, camCtrl, inputActions, dragThreshold, _timer, _recorder);

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
                _w,
                _h,
                _timer,
                hudUIDocument
            );
            _boardView.BoardCleared += () =>
            {
                inputHandler.SetInputEnabled(false);
                // Board fully cleared — save is no longer needed
                SaveManager.Delete();
                victory.OnBoardCleared();
            };
        }

        _generationComplete = true;
    }

    private void OnLeaveConfirmed()
    {
        if (_recorder != null && _timer != null && _board != null && _board.Arrows.Count > 0)
        {
            _recorder.RecordSessionLeave(_timer.SolveElapsed);
            SaveManager.Save(
                _recorder.ToReplayData(_gameId, _activeSeed, _w, _h, _maxLen, _inspectionDur)
            );
        }
        SceneManager.LoadScene("MainMenu");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!_generationComplete || _recorder == null || _board == null || _board.Arrows.Count == 0)
            return;

        if (!hasFocus)
        {
            _recorder.RecordSessionLeave(_timer?.SolveElapsed ?? 0.0);
            SaveManager.Save(
                _recorder.ToReplayData(_gameId, _activeSeed, _w, _h, _maxLen, _inspectionDur)
            );
        }
        else
        {
            _recorder.RecordSessionRejoin();
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
