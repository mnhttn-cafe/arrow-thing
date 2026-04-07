using System;
using System.Collections;
using System.Collections.Generic;
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

    // Game state
    private Board _board;
    private BoardView _boardView;
    private CameraController _camCtrl;
    private GameTimer _timer;
    private ReplayRecorder _recorder;
    private InputHandler _inputHandler;
    private string _gameId;
    private int _activeSeed;
    private int _w;
    private int _h;
    private int _maxLen;
    private float _inspectionDur;
    private int _initialArrowCount;
    private bool _autosaveEnabled;
    private bool _isContinuedGame;
    private int _clearsSinceLastSave;
    private const int AutosaveInterval = 10;
    private List<List<Cell>> _initialBoardSnapshot;
    private const float FrameBudgetMs = 12f;

    /// <summary>Set to true by the X button during loading to abort.</summary>
    private bool _cancelRequested;

    // Loading overlay state — driven by Update()
    private VisualElement _loadingOverlay;
    private VisualElement _loadingBarFill;
    private Label _loadingPercent;
    private Label _timerLabel;
    private Button _trailToggleBtn;
    private Button _backBtn;
    private VisualElement _cancelGenModal;
    private float _loadProgress;
    private bool _loadingActive;
    private float _loadingFadeStart;

    // --- Lifecycle ---

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
        if (mainCamera != null)
            mainCamera.backgroundColor = (ThemeManager.Current ?? visualSettings).backgroundColor;

        SettingsController.IsOpenChanged += OnSettingsOpenChanged;
        ThemeManager.ThemeChanged += OnThemeChanged;
        StartCoroutine(GenerateAndSetup());
    }

    private void OnDestroy()
    {
        SettingsController.IsOpenChanged -= OnSettingsOpenChanged;
        ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(VisualSettings theme)
    {
        if (mainCamera != null)
            mainCamera.backgroundColor = theme.backgroundColor;
        if (_boardView != null)
            _boardView.ApplyTheme(theme);
    }

    private void OnSettingsOpenChanged(bool open)
    {
        if (_inputHandler != null)
            _inputHandler.SetInputEnabled(!open);
    }

    private void Update()
    {
        if (!_loadingActive || _loadingOverlay == null)
            return;

        _loadingOverlay.style.opacity = Mathf.Clamp01(
            (Time.unscaledTime - _loadingFadeStart) / loadingFadeDuration
        );

        if (_loadingBarFill != null)
        {
            _loadingBarFill.style.width = new StyleLength(
                new Length(_loadProgress * 100f, LengthUnit.Percent)
            );
            if (_loadingPercent != null)
                _loadingPercent.text = Mathf.RoundToInt(_loadProgress * 100f) + "%";
        }
    }

    // --- Main setup orchestrator ---

    private IEnumerator GenerateAndSetup()
    {
        ResolveParameters(out ReplayData priorData, out bool deferredResume);
        ResolveHudElements();

        string modeStr =
            deferredResume ? "deferred-resume"
            : priorData != null ? "resume"
            : "new";
        Debug.Log(
            $"[GameController] GenerateAndSetup: mode={modeStr}, board={_w}x{_h}, maxLen={_maxLen}"
        );

        ShowLoading(deferredResume ? "Resuming..." : "Generating...");
        yield return null;

        if (deferredResume)
        {
            yield return LoadSaveAsync(result => priorData = result);
            if (priorData == null)
            {
                Debug.LogWarning(
                    "[GameController] Deferred save load returned null — returning to MainMenu."
                );
                SceneManager.LoadScene("MainMenu");
                yield break;
            }
            Debug.Log(
                $"[GameController] Save loaded: gameId={priorData.gameId}, board={priorData.boardWidth}x{priorData.boardHeight}, events={priorData.events.Count}"
            );
            ApplyResumeData(priorData);
        }

        ResolveSeed(priorData);
        bool hasSnapshot =
            priorData != null
            && priorData.boardSnapshot != null
            && priorData.boardSnapshot.Count > 0;

        CreateBoardAndView();
        SetupCamera();

        if (hasSnapshot)
        {
            _initialBoardSnapshot = priorData.boardSnapshot;
            yield return RestoreBoard(priorData);
        }
        else
        {
            yield return GenerateBoard();
        }

        if (priorData != null)
        {
            bool resumeSolving = ReplayClears(priorData, out double resumeSolveElapsed);
            if (_board.Arrows.Count == 0)
            {
                SaveManager.Delete();
                SceneManager.LoadScene("MainMenu");
                yield break;
            }
            SetupResumedRecorder(priorData);
            FinalizeSession(priorData);
            _boardView.ApplyColoring();
            HideLoading();
            SetupTimer(resumeSolving, resumeSolveElapsed);
        }
        else
        {
            SetupNewRecorder();
            FinalizeSession(null);
            _boardView.ApplyColoring();
            HideLoading();
            SetupTimer(false, 0.0);
        }

        WireHud();
        WireInput();
        WireVictory();
    }

    // --- Parameter resolution ---

    private void ResolveParameters(out ReplayData priorData, out bool deferredResume)
    {
        _w = boardWidth;
        _h = boardHeight;
        _maxLen = maxArrowLength;
        _inspectionDur = inspectionDuration;
        priorData = null;
        deferredResume = false;

        if (!GameSettings.IsSet)
            return;

        if (GameSettings.IsResuming && GameSettings.ResumeData == null)
        {
            deferredResume = true;
            return;
        }

        _w = GameSettings.Width;
        _h = GameSettings.Height;
        _maxLen = GameSettings.MaxArrowLength;

        if (GameSettings.IsResuming)
        {
            priorData = GameSettings.ResumeData;
            _inspectionDur = priorData.inspectionDuration;
        }
    }

    private void ResolveHudElements()
    {
        if (hudUIDocument == null || hudUIDocument.rootVisualElement == null)
            return;

        var hudRoot = hudUIDocument.rootVisualElement;
        _loadingOverlay = hudRoot.Q("loading-overlay");
        _backBtn = hudRoot.Q<Button>("back-to-menu-btn");
        _timerLabel = hudRoot.Q<Label>("timer-label");
        _trailToggleBtn = hudRoot.Q<Button>("trail-toggle-btn");
        _cancelGenModal = hudRoot.Q("cancel-generation-modal");

        if (_loadingOverlay != null)
        {
            _loadingOverlay.style.display = DisplayStyle.None;
            _loadingOverlay.style.opacity = 0f;
            _loadingBarFill = _loadingOverlay.Q("loading-bar-fill");
            _loadingPercent = _loadingOverlay.Q<Label>("loading-percent");
        }
    }

    private IEnumerator LoadSaveAsync(Action<ReplayData> onResult)
    {
        ReplayData loaded = null;
        yield return SaveManager.LoadAsync(d => loaded = d);
        onResult(loaded);
    }

    private void ApplyResumeData(ReplayData data)
    {
        GameSettings.SetResumeData(data);
        _w = GameSettings.Width;
        _h = GameSettings.Height;
        _maxLen = GameSettings.MaxArrowLength;
        _inspectionDur = data.inspectionDuration;
    }

    private void ResolveSeed(ReplayData priorData)
    {
        _activeSeed =
            (priorData != null) ? priorData.seed
            : (GameSettings.IsSet || useRandomSeed) ? Environment.TickCount
            : seed;
    }

    // --- Board and view creation ---

    private void CreateBoardAndView()
    {
        (_board, _boardView) = BoardSetupHelper.CreateBoardAndView(
            _w,
            _h,
            ThemeManager.Current ?? visualSettings
        );
    }

    private void SetupCamera()
    {
        if (mainCamera == null)
            return;
        float? zoom = GameSettings.IsSet
            ? PlayerPrefs.GetFloat(GameSettings.ZoomSpeedPrefKey, GameSettings.DefaultZoomSpeed)
            : null;
        _camCtrl = BoardSetupHelper.SetupCamera(mainCamera, _board, zoom);
    }

    // --- Work coroutines (no UI code — just work + _loadProgress) ---

    private IEnumerator RestoreBoard(ReplayData priorData)
    {
        int totalArrows = priorData.boardSnapshot.Count;
        Debug.Log($"[GameController] RestoreBoard: restoring {totalArrows} arrows from snapshot");
        int totalSteps = totalArrows * 2;
        var restorer = BoardSetupHelper.RestoreBoardFromSnapshot(
            _board,
            _boardView,
            priorData.boardSnapshot,
            FrameBudgetMs
        );

        while (restorer.MoveNext())
        {
            if (_cancelRequested)
            {
                SceneManager.LoadScene("MainMenu");
                yield break;
            }

            _loadProgress = (float)restorer.Current / totalSteps;
            yield return null;
        }
    }

    private IEnumerator GenerateBoard()
    {
        var generator = BoardGeneration.FillBoardIncremental(
            _board,
            _maxLen,
            new System.Random(_activeSeed)
        );

        // Progress is split across three phases:
        //   Generation  0% → 90%  (bulk of the work)
        //   Compaction 90% → 95%  (fast merge pass)
        //   Finalize   95% → 100% (dependency graph build)
        // Density 0.16 and exponent 1.5 were experimentally derived on boards
        // 100×100 and up, where loading screens are visible long enough to matter.
        const float genEndProgress = 0.90f;
        const float compactEndProgress = 0.95f;
        const float estimatedArrowDensity = 0.16f;
        const float progressExponent = 1.5f;
        float estimatedArrows = _w * _h * estimatedArrowDensity;
        // Track Arrow refs so we can remove their views after compaction
        var spawnedArrows = new List<Arrow>();
        int arrowsBeforeCompaction = 0;
        float genFinalProgress = 0f;
        var phase = GenerationPhase.Generating;

        while (true)
        {
            if (_cancelRequested)
            {
                SceneManager.LoadScene("MainMenu");
                yield break;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool done = false;
            while (sw.ElapsedMilliseconds < FrameBudgetMs)
            {
                if (!generator.MoveNext())
                {
                    done = true;
                    break;
                }

                if (generator.Current is GenerationPhase nextPhase)
                {
                    if (nextPhase == GenerationPhase.Compacting)
                    {
                        arrowsBeforeCompaction = _board.Arrows.Count;
                        genFinalProgress = _loadProgress;
                    }
                    else if (
                        nextPhase == GenerationPhase.Finalizing
                        && phase == GenerationPhase.Compacting
                    )
                    {
                        // Compaction done — remove stale views, add new ones
                        foreach (Arrow a in spawnedArrows)
                            _boardView.RemoveArrowView(a);
                        spawnedArrows.Clear();
                        for (int i = 0; i < _board.Arrows.Count; i++)
                        {
                            Arrow a = _board.Arrows[i];
                            _boardView.AddArrowView(a);
                            spawnedArrows.Add(a);
                        }
                    }
                    phase = nextPhase;
                }
                else if (phase == GenerationPhase.Generating)
                {
                    Arrow arrow = _board.Arrows[spawnedArrows.Count];
                    _boardView.AddArrowView(arrow);
                    spawnedArrows.Add(arrow);
                }
            }

            // Progress calculation per phase
            switch (phase)
            {
                case GenerationPhase.Generating:
                {
                    float rawProgress = Mathf.Clamp01(_board.Arrows.Count / estimatedArrows);
                    _loadProgress = genEndProgress * Mathf.Pow(rawProgress, progressExponent);
                    break;
                }
                case GenerationPhase.Compacting:
                {
                    int mergesCompleted = arrowsBeforeCompaction - _board.Arrows.Count;
                    float compactRatio =
                        arrowsBeforeCompaction > 0
                            ? Mathf.Clamp01(
                                (float)mergesCompleted / (arrowsBeforeCompaction * 0.15f)
                            )
                            : 1f;
                    _loadProgress =
                        genFinalProgress + (compactEndProgress - genFinalProgress) * compactRatio;
                    break;
                }
                case GenerationPhase.Finalizing:
                {
                    int arrowCount = _board.Arrows.Count;
                    float finalizeRatio =
                        arrowCount > 0 && generator.Current is int finalized
                            ? Mathf.Clamp01((float)finalized / arrowCount)
                            : 0f;
                    _loadProgress = compactEndProgress + (1f - compactEndProgress) * finalizeRatio;
                    break;
                }
            }

            if (done)
                break;
            yield return null;
        }

        if (_board.Arrows.Count == 0)
        {
            Debug.LogWarning(
                $"[GameController] GenerateBoard produced 0 arrows (board {_w}x{_h}, maxLen={_maxLen}, seed={_activeSeed}). Returning to menu."
            );
            SceneManager.LoadScene("MainMenu");
            yield break;
        }
        Debug.Log(
            $"[GameController] GenerateBoard complete: {_board.Arrows.Count} arrows, board={_w}x{_h}, seed={_activeSeed}"
        );

        _initialBoardSnapshot = new List<List<Cell>>(_board.Arrows.Count);
        foreach (Arrow arrow in _board.Arrows)
            _initialBoardSnapshot.Add(new List<Cell>(arrow.Cells));
    }

    // --- Resume logic ---

    private bool ReplayClears(ReplayData priorData, out double solveElapsed)
    {
        bool solving = false;
        int totalClearEvents = 0;
        int successfulClears = 0;
        foreach (ReplayEvent evt in priorData.events)
        {
            if (evt.type == ReplayEventType.Clear)
            {
                totalClearEvents++;
                var worldPos = new Vector3(evt.posX ?? 0f, evt.posY ?? 0f, 0f);
                Cell cell = BoardCoords.WorldToCell(worldPos, _board.Width, _board.Height);
                if (_board.Contains(cell))
                {
                    Arrow arrow = _board.GetArrowAt(cell);
                    if (arrow != null && _board.IsClearable(arrow))
                    {
                        _boardView.RemoveArrowView(arrow);
                        _board.RemoveArrow(arrow);
                        successfulClears++;
                    }
                    else if (arrow == null)
                        Debug.LogWarning(
                            $"[GameController] ReplayClears: no arrow at cell ({cell.X},{cell.Y}) for clear event seq={evt.seq}"
                        );
                    else
                        Debug.LogWarning(
                            $"[GameController] ReplayClears: arrow at ({cell.X},{cell.Y}) not clearable for clear event seq={evt.seq}"
                        );
                }
                else
                {
                    Debug.LogWarning(
                        $"[GameController] ReplayClears: clear event seq={evt.seq} maps to out-of-bounds cell ({cell.X},{cell.Y})"
                    );
                }
            }
            if (evt.type == ReplayEventType.StartSolve)
                solving = true;
        }
        solveElapsed = priorData.ComputedSolveElapsed;
        Debug.Log(
            $"[GameController] ReplayClears: {successfulClears}/{totalClearEvents} clears applied, solving={solving}, solveElapsed={solveElapsed:F3}s"
        );
        return solving;
    }

    private void SetupResumedRecorder(ReplayData priorData)
    {
        _gameId = priorData.gameId;
        int nextSeq =
            priorData.events.Count > 0 ? priorData.events[priorData.events.Count - 1].seq + 1 : 0;
        _recorder = new ReplayRecorder(priorData.events, nextSeq);
        _recorder.RecordSessionRejoin();
        Debug.Log(
            $"[GameController] Resumed game: id={_gameId}, nextSeq={nextSeq}, remainingArrows={_board.Arrows.Count}"
        );
    }

    private void SetupNewRecorder()
    {
        _gameId = Guid.NewGuid().ToString();
        _recorder = new ReplayRecorder();
        _recorder.RecordSessionStart();
        Debug.Log(
            $"[GameController] New game: id={_gameId}, arrows={_board.Arrows.Count}, board={_w}x{_h}, seed={_activeSeed}"
        );
    }

    private void FinalizeSession(ReplayData priorData)
    {
        _initialArrowCount = _board.Arrows.Count;
        _autosaveEnabled = !SaveManager.HasSave() || priorData != null;
        _isContinuedGame = priorData != null;
    }

    // --- Timer and gameplay wiring ---

    private void SetupTimer(bool resumeSolving, double resumeSolveElapsed)
    {
        _timer = new GameTimer(_inspectionDur);
        double wallNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        if (resumeSolving)
        {
            _timer.Resume(wallNow, resumeSolveElapsed);
            Debug.Log($"[GameController] Timer resumed: priorElapsed={resumeSolveElapsed:F3}s");
        }
        else
        {
            _timer.Start(wallNow);
            Debug.Log(
                $"[GameController] Timer started fresh: inspectionDuration={_inspectionDur}s"
            );
        }
    }

    private void WireHud()
    {
        if (hudUIDocument == null || hudUIDocument.rootVisualElement == null)
            return;

        var hudRoot = hudUIDocument.rootVisualElement;
        var leaveModal = hudRoot.Q("leave-modal");
        var leaveTitle = hudRoot.Q<Label>("leave-title");
        var leaveSublabel = hudRoot.Q("leave-sublabel");
        var leaveCloseBtn = hudRoot.Q<Button>("leave-close-btn");

        if (_backBtn != null)
        {
            _backBtn.clickable = new Clickable(() => { });
            _backBtn.clicked += () =>
                ShowLeaveModal(leaveModal, leaveTitle, leaveSublabel, leaveCloseBtn);
        }

        hudRoot.Q<Button>("leave-yes-btn").clicked += () => OnLeaveYes(leaveModal);
        hudRoot.Q<Button>("leave-no-btn").clicked += () => OnLeaveNo(leaveModal);

        if (leaveCloseBtn != null)
            leaveCloseBtn.clicked += () => HideLeaveModal(leaveModal);

        var timerView = gameObject.AddComponent<GameTimerView>();
        timerView.Init(_timer, hudUIDocument, inspectionWarningThreshold);

        if (_trailToggleBtn != null)
        {
            bool trailOn = false;
            _trailToggleBtn.clicked += () =>
            {
                trailOn = !trailOn;
                _boardView.SetAllTrailsVisible(trailOn);
                if (trailOn)
                    _trailToggleBtn.AddToClassList("hud-icon-btn--active");
                else
                    _trailToggleBtn.RemoveFromClassList("hud-icon-btn--active");
            };
            _boardView.TrailAutoOff += () =>
            {
                trailOn = false;
                _trailToggleBtn.RemoveFromClassList("hud-icon-btn--active");
            };
        }
    }

    private void WireInput()
    {
        float dragThreshold = GameSettings.IsSet
            ? PlayerPrefs.GetFloat(
                GameSettings.DragThresholdPrefKey,
                GameSettings.DefaultDragThreshold
            )
            : dragThresholdPixels;
        _inputHandler = gameObject.AddComponent<InputHandler>();
        _inputHandler.Init(
            _board,
            _boardView,
            _camCtrl,
            inputActions,
            dragThreshold,
            _timer,
            _recorder,
            OnArrowCleared
        );

        if (_backBtn != null)
            _backBtn.clicked += () => _inputHandler.SetInputEnabled(false);
    }

    private void WireVictory()
    {
        if (
            victoryUIDocument == null
            || !victoryUIDocument.enabled
            || victoryUIDocument.rootVisualElement == null
        )
            return;

        var victory = gameObject.AddComponent<VictoryController>();
        victory.Init(
            victoryUIDocument,
            _boardView.GridRenderer,
            _camCtrl,
            _w,
            _h,
            _timer,
            hudUIDocument,
            BuildReplayData
        );
        _boardView.LastArrowClearing += () =>
        {
            _inputHandler.SetInputEnabled(false);
            if (_backBtn != null)
                _backBtn.style.display = DisplayStyle.None;
            if (_recorder != null)
                _recorder.RecordEndSolve();
            if (_autosaveEnabled)
                SaveManager.Delete();
            victory.OnLastArrowClearing();
        };
        _boardView.BoardCleared += victory.OnBoardCleared;
    }

    // --- Loading overlay ---

    private void ShowLoading(string label)
    {
        if (_loadingOverlay == null)
            return;
        var loadingLabel = _loadingOverlay.Q<Label>("loading-label");
        if (loadingLabel != null)
            loadingLabel.text = label;
        if (_timerLabel != null)
            _timerLabel.style.display = DisplayStyle.None;
        if (_trailToggleBtn != null)
            _trailToggleBtn.style.display = DisplayStyle.None;
        if (_backBtn != null && _cancelGenModal != null)
        {
            _backBtn.clicked += () => _cancelGenModal.RemoveFromClassList("modal--hidden");
            _cancelGenModal.Q<Button>("cancel-generation-yes-btn").clicked += () =>
            {
                _cancelGenModal.AddToClassList("modal--hidden");
                _cancelRequested = true;
            };
            _cancelGenModal.Q<Button>("cancel-generation-no-btn").clicked += () =>
                _cancelGenModal.AddToClassList("modal--hidden");
        }
        else if (_backBtn != null)
        {
            _backBtn.clicked += () => _cancelRequested = true;
        }

        _loadingOverlay.style.display = DisplayStyle.Flex;
        _loadingOverlay.style.opacity = 0f;
        _loadProgress = 0f;
        _loadingActive = true;
        _loadingFadeStart = Time.unscaledTime;
    }

    private void HideLoading()
    {
        _loadingActive = false;
        if (_loadingOverlay != null)
        {
            float currentOpacity = Mathf.Clamp01(
                (Time.unscaledTime - _loadingFadeStart) / loadingFadeDuration
            );
            StartCoroutine(
                FadeElement(
                    _loadingOverlay,
                    currentOpacity,
                    0f,
                    loadingFadeDuration * currentOpacity,
                    hide: true
                )
            );
        }
        if (_timerLabel != null)
            _timerLabel.style.display = DisplayStyle.Flex;
        if (_trailToggleBtn != null)
            _trailToggleBtn.style.display = DisplayStyle.Flex;
        if (_backBtn != null)
            _backBtn.clickable = new Clickable(() => { });
        if (_cancelGenModal != null)
            _cancelGenModal.AddToClassList("modal--hidden");
    }

    // --- Leave modal ---

    private bool HasAnyClearedArrows => _board != null && _board.Arrows.Count < _initialArrowCount;

    private bool WouldOverwriteDifferentSave =>
        !_autosaveEnabled && HasAnyClearedArrows && SaveManager.HasSave();

    private void ShowLeaveModal(
        VisualElement modal,
        Label title,
        VisualElement sublabel,
        Button closeBtn
    )
    {
        if (WouldOverwriteDifferentSave)
        {
            if (title != null)
                title.text = "Leaving. Save?";
            if (sublabel != null)
                sublabel.RemoveFromClassList("modal--hidden");
            if (closeBtn != null)
                closeBtn.style.display = DisplayStyle.Flex;
        }
        else
        {
            if (title != null)
                title.text = "Leave?";
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
        if (_inputHandler != null)
            _inputHandler.SetInputEnabled(true);
    }

    private void OnLeaveYes(VisualElement modal)
    {
        if (_autosaveEnabled && (_isContinuedGame || HasAnyClearedArrows))
            SaveAndLeave();
        else if (WouldOverwriteDifferentSave)
            SaveAndLeave();
        else
            SceneManager.LoadScene("MainMenu");
    }

    private void OnLeaveNo(VisualElement modal)
    {
        if (WouldOverwriteDifferentSave)
            SceneManager.LoadScene("MainMenu");
        else
            HideLeaveModal(modal);
    }

    // --- Save ---

    private void OnArrowCleared()
    {
        if (!_autosaveEnabled || _recorder == null || _timer == null)
            return;

        _clearsSinceLastSave++;
        if (_clearsSinceLastSave >= AutosaveInterval)
        {
            _clearsSinceLastSave = 0;
            int cleared = _initialArrowCount - _board.Arrows.Count;
            Debug.Log(
                $"[GameController] Autosave triggered: {cleared}/{_initialArrowCount} arrows cleared"
            );
            SaveManager.Save(BuildReplayData());
        }
    }

    private void SaveAndLeave()
    {
        if (_recorder != null && _timer != null)
        {
            _recorder.RecordSessionLeave();
            Debug.Log(
                $"[GameController] SaveAndLeave: saving game id={_gameId}, arrows remaining={_board?.Arrows.Count}"
            );
            SaveManager.Save(BuildReplayData());
        }
        SceneManager.LoadScene("MainMenu");
    }

    private ReplayData BuildReplayData()
    {
        return _recorder.ToReplayData(
            _gameId,
            _activeSeed,
            _w,
            _h,
            _maxLen,
            _inspectionDur,
            boardSnapshot: _initialBoardSnapshot,
            gameVersion: UnityEngine.Application.version
        );
    }

    // --- Utilities ---

    private static IEnumerator FadeElement(
        VisualElement element,
        float from,
        float to,
        float duration,
        bool hide = false
    )
    {
        float start = Time.unscaledTime;
        while (true)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - start) / duration);
            element.style.opacity = Mathf.Lerp(from, to, t);
            if (t >= 1f)
                break;
            yield return null;
        }
        if (hide)
            element.style.display = DisplayStyle.None;
    }
}
