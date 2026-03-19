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

    // Fields shared between GenerateAndSetup and leave callbacks
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
    private int _initialArrowCount;
    private InputHandler _inputHandler;

    /// <summary>Set to true by the X button during generation to abort.</summary>
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
        VisualElement loadingBarFill = null;
        Label loadingPercent = null;
        Button backBtn = null;
        Label timerLabel = null;
        Button trailToggleBtn = null;

        if (hudUIDocument != null && hudUIDocument.rootVisualElement != null)
        {
            var hudRoot = hudUIDocument.rootVisualElement;
            loadingOverlay = hudRoot.Q("loading-overlay");
            backBtn = hudRoot.Q<Button>("back-to-menu-btn");
            timerLabel = hudRoot.Q<Label>("timer-label");
            trailToggleBtn = hudRoot.Q<Button>("trail-toggle-btn");

            if (loadingOverlay != null)
            {
                loadingOverlay.style.display = DisplayStyle.None;
                loadingBarFill = loadingOverlay.Q("loading-bar-fill");
                loadingPercent = loadingOverlay.Q<Label>("loading-percent");
            }
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
            // Hide gameplay HUD elements during generation
            if (timerLabel != null)
                timerLabel.style.display = DisplayStyle.None;
            if (trailToggleBtn != null)
                trailToggleBtn.style.display = DisplayStyle.None;

            // Wire X button to cancel generation (no modal — immediate cancel)
            if (backBtn != null)
                backBtn.clicked += () => _cancelGeneration = true;

            // Generation needs multiple frames — fade in overlay while generating
            loadingOverlay.style.display = DisplayStyle.Flex;
            loadingOverlay.style.opacity = 0f;
            float fadeIn = 0f;

            // See docs/BoardGeneration.md § "Loading Progress Heuristic" for derivation.
            const float estimatedArrowDensity = 0.064f;
            float estimatedArrows = _w * _h * estimatedArrowDensity;

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
                if (loadingBarFill != null)
                {
                    float progress = Mathf.Clamp01(_board.Arrows.Count / estimatedArrows);
                    loadingBarFill.style.width = new StyleLength(
                        new Length(progress * 100f, LengthUnit.Percent)
                    );
                    if (loadingPercent != null)
                        loadingPercent.text = Mathf.RoundToInt(progress * 100f) + "%";
                }
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

            // Restore gameplay HUD elements after generation
            if (timerLabel != null)
                timerLabel.style.display = DisplayStyle.Flex;
            if (trailToggleBtn != null)
                trailToggleBtn.style.display = DisplayStyle.Flex;

            // Clear cancel handler — X button will be re-wired for leave modal below
            if (backBtn != null)
                backBtn.clickable = new Clickable(() => { });
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

        // Capture full arrow count before applying resume clears, so that
        // HasAnyClearedArrows is true for resumed games with prior progress.
        _initialArrowCount = _board.Arrows.Count;

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

            int nextSeq =
                priorData.events.Count > 0
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

        // Setup timer — resume skips inspection and restores solve elapsed
        _timer = new GameTimer(_inspectionDur);
        double wallNow = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        if (resumeSolving)
            _timer.Resume(wallNow, resumeSolveElapsed);
        else
            _timer.Start(wallNow);

        GameTimerView timerView = null;

        // Setup HUD
        if (hudUIDocument != null && hudUIDocument.rootVisualElement != null)
        {
            var hudRoot = hudUIDocument.rootVisualElement;
            var leaveModal = hudRoot.Q("leave-modal");
            var leaveTitle = hudRoot.Q<Label>("leave-title");
            var leaveSublabel = hudRoot.Q("leave-sublabel");
            var leaveCloseBtn = hudRoot.Q<Button>("leave-close-btn");

            // X button opens the leave modal
            if (backBtn != null)
            {
                backBtn.clickable = new Clickable(() => { });
                backBtn.clicked += () =>
                    ShowLeaveModal(leaveModal, leaveTitle, leaveSublabel, leaveCloseBtn);
            }

            // Leave modal: Yes = save (if applicable) and leave
            hudRoot.Q<Button>("leave-yes-btn").clicked += () => OnLeaveYes(leaveModal);

            // Leave modal: No = leave without saving (or cancel if no clears)
            hudRoot.Q<Button>("leave-no-btn").clicked += () => OnLeaveNo(leaveModal);

            // Leave modal: X close = cancel, stay in game
            if (leaveCloseBtn != null)
                leaveCloseBtn.clicked += () => HideLeaveModal(leaveModal);

            timerView = gameObject.AddComponent<GameTimerView>();
            timerView.Init(_timer, hudUIDocument, inspectionWarningThreshold);

            // Trail toggle button (bottom-right)
            if (trailToggleBtn != null)
            {
                bool trailOn = false;
                trailToggleBtn.clicked += () =>
                {
                    trailOn = !trailOn;
                    _boardView.SetAllTrailsVisible(trailOn);
                    if (trailOn)
                        trailToggleBtn.AddToClassList("hud-btn--active");
                    else
                        trailToggleBtn.RemoveFromClassList("hud-btn--active");
                };
                _boardView.TrailAutoOff += () =>
                {
                    trailOn = false;
                    trailToggleBtn.RemoveFromClassList("hud-btn--active");
                };
            }
        }

        // Setup input
        float dragThreshold = GameSettings.IsSet ? GameSettings.DragThreshold : dragThresholdPixels;
        _inputHandler = gameObject.AddComponent<InputHandler>();
        _inputHandler.Init(
            _board,
            _boardView,
            camCtrl,
            inputActions,
            dragThreshold,
            _timer,
            _recorder
        );

        // Wire X button to also suppress input when opening leave modal
        if (backBtn != null)
            backBtn.clicked += () => _inputHandler.SetInputEnabled(false);

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
                _inputHandler.SetInputEnabled(false);
                _recorder?.RecordEndSolve(_timer?.SolveElapsed ?? 0.0);
                SaveManager.Delete();
                victory.OnBoardCleared();
            };
        }
    }

    /// <summary>Returns true if any arrows have been cleared this session (including resumed clears).</summary>
    private bool HasAnyClearedArrows => _board != null && _board.Arrows.Count < _initialArrowCount;

    private void ShowLeaveModal(
        VisualElement modal,
        Label title,
        VisualElement sublabel,
        Button closeBtn
    )
    {
        if (HasAnyClearedArrows)
        {
            // Save-worthy state: show "Save game?" with Yes/No/X
            if (title != null)
                title.text = "Save game?";
            if (sublabel != null)
            {
                // Only warn about replacing if a DIFFERENT game's save exists
                bool hasDifferentSave = false;
                if (SaveManager.HasSave())
                {
                    var existing = SaveManager.Load();
                    hasDifferentSave = existing != null && existing.gameId != _gameId;
                }
                if (hasDifferentSave)
                    sublabel.RemoveFromClassList("modal--hidden");
                else
                    sublabel.AddToClassList("modal--hidden");
            }
            if (closeBtn != null)
                closeBtn.style.display = DisplayStyle.Flex;
        }
        else
        {
            // Nothing to save: show "Leave game?" with Yes/No only
            if (title != null)
                title.text = "Leave game?";
            if (sublabel != null)
                sublabel.AddToClassList("modal--hidden");
            if (closeBtn != null)
                closeBtn.style.display = DisplayStyle.None;
        }
        modal.RemoveFromClassList("modal--hidden");
    }

    private void HideLeaveModal(VisualElement modal)
    {
        modal.AddToClassList("modal--hidden");
        _inputHandler?.SetInputEnabled(true);
    }

    private void OnLeaveYes(VisualElement modal)
    {
        if (HasAnyClearedArrows && _recorder != null && _timer != null)
        {
            _recorder.RecordSessionLeave(_timer.SolveElapsed);
            SaveManager.Save(
                _recorder.ToReplayData(_gameId, _activeSeed, _w, _h, _maxLen, _inspectionDur)
            );
        }
        SceneManager.LoadScene("MainMenu");
    }

    private void OnLeaveNo(VisualElement modal)
    {
        if (HasAnyClearedArrows)
        {
            // "No" on save prompt = leave without saving
            SceneManager.LoadScene("MainMenu");
        }
        else
        {
            // "No" on leave prompt = cancel, stay in game
            HideLeaveModal(modal);
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
