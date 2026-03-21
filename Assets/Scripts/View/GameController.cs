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
            mainCamera.backgroundColor = visualSettings.backgroundColor;

        StartCoroutine(GenerateAndSetup());
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
        ResolveParameters(out int minLen, out ReplayData priorData, out bool deferredResume);
        ResolveHudElements();

        ShowLoading(deferredResume ? "Resuming..." : "Generating...");
        yield return null;

        if (deferredResume)
        {
            yield return LoadSaveAsync(result => priorData = result);
            if (priorData == null)
            {
                SceneManager.LoadScene("MainMenu");
                yield break;
            }
            ApplyResumeData(priorData);
        }

        ResolveSeed(priorData);
        bool hasSnapshot = priorData?.boardSnapshot != null && priorData.boardSnapshot.Count > 0;

        CreateBoardAndView();
        SetupCamera();

        if (hasSnapshot)
        {
            _initialBoardSnapshot = priorData.boardSnapshot;
            yield return RestoreBoard(priorData);
        }
        else
        {
            yield return GenerateBoard(minLen);
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

    private void ResolveParameters(
        out int minLen,
        out ReplayData priorData,
        out bool deferredResume
    )
    {
        _w = boardWidth;
        _h = boardHeight;
        minLen = minArrowLength;
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
        _board = new Board(_w, _h);
        var boardGo = new GameObject("BoardView");
        _boardView = boardGo.AddComponent<BoardView>();
        _boardView.Init(_board, visualSettings, spawnArrows: false);
    }

    private void SetupCamera()
    {
        if (mainCamera == null)
            return;
        _camCtrl = mainCamera.gameObject.GetComponent<CameraController>();
        if (_camCtrl == null)
            _camCtrl = mainCamera.gameObject.AddComponent<CameraController>();
        _camCtrl.Init(_board);
        if (GameSettings.IsSet)
            _camCtrl.ZoomSpeed = PlayerPrefs.GetFloat(
                GameSettings.ZoomSpeedPrefKey,
                GameSettings.DefaultZoomSpeed
            );
    }

    // --- Work coroutines (no UI code — just work + _loadProgress) ---

    private IEnumerator RestoreBoard(ReplayData priorData)
    {
        var snapshotArrows = new List<Arrow>(priorData.boardSnapshot.Count);
        foreach (List<Cell> arrowCells in priorData.boardSnapshot)
            snapshotArrows.Add(new Arrow(arrowCells));

        int totalArrows = snapshotArrows.Count;
        int totalSteps = totalArrows * 2;
        var restorer = _board.RestoreArrowsIncremental(snapshotArrows);

        int viewedCount = 0;
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
                if (!restorer.MoveNext())
                {
                    done = true;
                    break;
                }
                if (viewedCount < totalArrows)
                    _boardView.AddArrowView(snapshotArrows[viewedCount++]);
            }

            _loadProgress = (float)restorer.Current / totalSteps;

            if (done)
                break;
            yield return null;
        }
    }

    private IEnumerator GenerateBoard(int minLen)
    {
        var generator = BoardGeneration.FillBoardIncremental(
            _board,
            minLen,
            _maxLen,
            new System.Random(_activeSeed)
        );

        // See docs/BoardGeneration.md § "Loading Progress Heuristic" for derivation.
        const float estimatedArrowDensity = 0.064f;
        float estimatedArrows = _w * _h * estimatedArrowDensity;
        int viewedArrows = 0;
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
                _boardView.AddArrowView(_board.Arrows[viewedArrows++]);
            }

            _loadProgress = Mathf.Clamp01(_board.Arrows.Count / estimatedArrows);

            if (done)
                break;
            yield return null;
        }

        if (_board.Arrows.Count == 0)
        {
            Debug.LogWarning(
                $"BoardGeneration produced 0 arrows (board {_w}x{_h}, minLen={minLen}, maxLen={_maxLen}, seed={_activeSeed}). Returning to menu."
            );
            SceneManager.LoadScene("MainMenu");
            yield break;
        }

        _initialBoardSnapshot = new List<List<Cell>>(_board.Arrows.Count);
        foreach (Arrow arrow in _board.Arrows)
            _initialBoardSnapshot.Add(new List<Cell>(arrow.Cells));
    }

    // --- Resume logic ---

    private bool ReplayClears(ReplayData priorData, out double solveElapsed)
    {
        bool solving = false;
        foreach (ReplayEvent evt in priorData.events)
        {
            if (evt.type == ReplayEventType.Clear)
            {
                var worldPos = new Vector3(evt.posX ?? 0f, evt.posY ?? 0f, 0f);
                Cell cell = BoardCoords.WorldToCell(worldPos, _board.Width, _board.Height);
                if (_board.Contains(cell))
                {
                    Arrow arrow = _board.GetArrowAt(cell);
                    if (arrow != null && _board.IsClearable(arrow))
                    {
                        _boardView.RemoveArrowView(arrow);
                        _board.RemoveArrow(arrow);
                    }
                }
            }
            if (evt.type == ReplayEventType.StartSolve)
                solving = true;
        }
        solveElapsed = priorData.ComputedSolveElapsed;
        return solving;
    }

    private void SetupResumedRecorder(ReplayData priorData)
    {
        _gameId = priorData.gameId;
        int nextSeq =
            priorData.events.Count > 0 ? priorData.events[priorData.events.Count - 1].seq + 1 : 0;
        _recorder = new ReplayRecorder(priorData.events, nextSeq);
        _recorder.RecordSessionRejoin();
    }

    private void SetupNewRecorder()
    {
        _gameId = Guid.NewGuid().ToString();
        _recorder = new ReplayRecorder();
        _recorder.RecordSessionStart();
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
            _timer.Resume(wallNow, resumeSolveElapsed);
        else
            _timer.Start(wallNow);
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
                    _trailToggleBtn.AddToClassList("hud-btn--active");
                else
                    _trailToggleBtn.RemoveFromClassList("hud-btn--active");
            };
            _boardView.TrailAutoOff += () =>
            {
                trailOn = false;
                _trailToggleBtn.RemoveFromClassList("hud-btn--active");
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
            hudUIDocument
        );
        _boardView.LastArrowClearing += () =>
        {
            _inputHandler.SetInputEnabled(false);
            if (_backBtn != null)
                _backBtn.style.display = DisplayStyle.None;
            _recorder?.RecordEndSolve();
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
        _cancelGenModal?.AddToClassList("modal--hidden");
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
        _inputHandler?.SetInputEnabled(true);
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
            SaveManager.Save(BuildReplayData());
        }
    }

    private void SaveAndLeave()
    {
        if (_recorder != null && _timer != null)
        {
            _recorder.RecordSessionLeave();
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
