# Local Leaderboards & Replay Viewer

## Overview

Add a local leaderboard system that tracks solve times per board configuration, accessible from the mode-select screen via a trophy icon button. Include a replay viewer that lets players watch any leaderboard entry's replay with video-like controls, tap indicators, and optional clearable-arrow highlighting. Local/global toggle on both leaderboard views, with global showing "Coming Soon" for now.

## Design

### Leaderboard Data Model

**`LeaderboardEntry`** (domain model, serializable — stored in index, not replay data):
- `gameId` (string) — maps to replay file at `replays/{gameId}.json`
- `seed` (int)
- `boardWidth`, `boardHeight` (int)
- `solveTime` (double) — seconds
- `completedAt` (string) — ISO 8601 UTC timestamp
- `isFavorite` (bool) — user-flagged to prevent pruning
- `gameVersion` (string) — application version at time of play

**`ReplayData` addition**: Add `gameVersion` (string) field to existing `ReplayData`. Populated by `ReplayRecorder` from `Application.version` at recording time. Bump replay version to 3.

**Storage**: `Application.persistentDataPath` (same as `SaveManager` — maps to IndexedDB on WebGL). Leaderboard index stored as a JSON file (`leaderboard.json`) containing entry metadata without replay data. Replays stored as individual files (`replays/{gameId}.json`) — same format as `savegame.json` since a completed replay and an in-progress save are both `ReplayData`, just with different `finalTime` values. This avoids loading all replay data into memory at once; replays are loaded on demand when the player watches one. Cap at 50 entries per unique `(boardWidth, boardHeight)` configuration; when full, drop the slowest **non-favorited** time (and delete its replay file). Favorited entries are never auto-pruned. Total cap across all configs: 500 entries. Compression deferred — a sizing test (phase 1.5) will measure actual replay sizes across board configs to inform whether compression is needed and at what threshold.

**`LeaderboardStore`** (domain class, pure C#):
- `AddEntry(entry)` — insert, enforce per-config and global caps (skip favorited entries when pruning)
- `GetEntries(width, height)` — returns entries for a specific board size
- `GetAllEntries()` — returns all entries (for All tab)
- `GetPersonalBest(width, height)` — fastest time for a config, or null
- `GetNeighborEntries(width, height, time, count)` — returns `count` entries centered around the given time (for victory mini-leaderboard)
- `SetFavorite(gameId, bool)` — toggle favorite flag
- `RemoveEntry(gameId)` — delete an entry
- `SortBy(entries, criterion)` — sort by `SortCriterion` enum: {Fastest, Biggest, Favorites}
  - Fastest (default): solveTime ascending
  - Biggest: boardWidth × boardHeight descending (useful in All tab)
  - Favorites: favorited first, then by solveTime ascending
- Serialization: `ToJson()` / `FromJson(string)`

**`LeaderboardManager`** (view layer, handles persistence):
- Wraps `LeaderboardStore` + file I/O (same `Application.persistentDataPath` as `SaveManager`)
- `SaveIndex()` / `LoadIndex()` — read/write `leaderboard.json` (entry metadata only)
- `SaveReplay(gameId, replayData)` — write individual replay file to `replays/{gameId}.json`
- `LoadReplay(gameId)` — load replay on demand (when player watches it)
- `DeleteReplay(gameId)` — remove replay file (on entry pruning or manual delete)
- `RecordResult(replayData)` — called from VictoryController after board clear; constructs entry, adds to store, saves index + replay file
- `IsPersonalBest(width, height, time)` — check before recording

### Leaderboard Screen (UI)

**Access point**: Trophy icon button in the **top-right corner of mode-select screen**. Uses `Assets/Art/trophy_icon.png`.

**Screen layout** (`leaderboard` screen in Root.uxml):
- Header: "Leaderboards" title + back button (top-left)
- **Local / Global toggle** (top-right, next to header) — two pill buttons
  - Local (default): shows local PlayerPrefs data
  - Global: shows "Coming Soon" placeholder overlay
- **5 tabs** below header, horizontally arranged:
  - Small (10×10)
  - Medium (20×20)
  - Large (40×40)
  - XLarge (100×100)
  - All (every entry across all board sizes, including custom)
- **Sort controls** below tabs — three pill buttons:
  - Fastest (default) — sort by solveTime ascending
  - Biggest — sort by area descending (most useful in All tab)
  - Favorites — favorited entries first, then by solveTime
- **Scrollable entry list** — each row shows:
  - Rank number (in current sort)
  - Board size label (e.g. "20×20") — shown in All tab; hidden in size-specific tabs
  - Solve time (formatted mm:ss.ff or hh:mm:ss.ff)
  - Date (relative: "2h ago", "3d ago", "Mar 5")
  - Favorite star icon (filled if favorited)
  - Play button (right side) — opens replay viewer. Uses `Assets/Art/play_icon.png`.
  - Triple-dot menu button (right of play button) — opens context menu:
    - "Favorite" / "Unfavorite" — toggle favorite flag
    - "Delete" — remove entry (with confirmation for favorited entries)
- Personal best row highlighted in gold
- Empty state: "No scores yet. Play a game to see your times here!"

**Context menu**: A small floating panel that appears anchored to the triple-dot button. Clicking outside dismisses it. Simple list of text buttons.

**`LeaderboardScreenController`** (view layer):
- Manages tab selection, sort state, list population, context menu
- Wired by MainMenuController as a fourth screen alongside main-menu, mode-select, settings
- Replay launch → sets GameSettings replay mode, records return destination as "leaderboard", loads Replay scene

### Victory Screen Integration

After board clear, before showing the victory popup:
- Record entry via `LeaderboardManager.RecordResult()`
- Check if this is a personal best for the board size
- If personal best: timer label turns gold, "New Best!" text shown in victory popup

**Victory popup additions**:
- "New Best!" gold label (hidden unless applicable)
- **"View Leaderboard" button** — navigates to the leaderboard screen (replays are viewable from there)

### Replay Viewer

**Scene**: Dedicated `Replay` scene (not reusing Game scene). Shared logic between GameController and ReplayViewController is extracted into a helper class to avoid bloating GameController further.

**`BoardSetupHelper`** (view layer, static or instance utility):
- Extracted from GameController — contains reusable logic for:
  - `CreateBoardAndView(width, height, visualSettings, parent)` → returns `(Board, BoardView)`
  - `RestoreBoardFromSnapshot(board, boardView, snapshot)` — coroutine, restores arrows from snapshot data
  - `SetupCamera(camera, cameraController, board)` — fit camera to board
- GameController refactored to delegate to this helper (no behavior change, just extraction)
- ReplayViewController uses the same helper

**Entry point**:
- Leaderboard screen → play button on entry row → `GameSettings.StartReplay(replayData, "MainMenu")` → load Replay scene

**`ReplayPlayer`** (domain class, pure C#):
- Takes `ReplayData`, provides playback logic
- `CurrentEventIndex` — position in event list
- `CurrentTimestamp` — playback time (seconds from first event)
- `TotalDuration` — time span from first to last event
- `PlaybackSpeed` — multiplier (0.5×, 1×, 2×, 4×)
- `IsPlaying` — play/pause state
- `Advance(deltaTime)` — advance playback clock by deltaTime × speed, return list of events that fired
- `SeekTo(normalizedTime)` — jump to position (0.0–1.0); returns events to apply or undo
  - Forward seek: returns clear/reject events between current and target position
  - Backward seek: returns cleared arrows to re-add (in reverse clear order)
- Tracks cleared arrow list internally so backward seek knows what to undo

**`ReplayViewController`** (view layer, MonoBehaviour on Replay scene):
- Scene entry point for replay playback
- On start: use `BoardSetupHelper` to restore board from snapshot, set up camera
- Each frame: call `ReplayPlayer.Advance()`, execute returned events via `BoardView.TryClearArrow()` / flash, spawn tap indicators
- Seek: incremental — forward seeks replay clears from current to target, backward seeks re-add cleared arrows via `Board.AddArrow` / `BoardView.AddArrowView` in reverse clear order (no animations). Full snapshot rebuild only on initial load.
- Wires replay HUD controls
- Exit: returns to the screen the player came from (stored in `GameSettings.ReturnScene`)

**Replay HUD** (`Assets/UI/ReplayHud.uxml`):
- **Seek slider** (bottom of screen, full width) — shows playback position as normalized 0–1
  - Draggable handle for seeking
  - Progress fill showing current position
  - Time labels: current time (left) and total time (right), formatted mm:ss
- **Speed controls** (bottom-right, above slider):
  - Cycle button showing current speed: 0.5× → 1× → 2× → 4× → 0.5×
- **Play/Pause button** (bottom-center, above slider)
- **Exit button** (top-left) — returns to previous screen
- **Highlight toggle** (top-right) — "Show Clearable" button
  - When active: all currently clearable arrows are tinted **bright green** (`#00FF66` or similar)
  - Updates after each clear event and on seek
  - Uses `Board.IsClearable()` to determine which arrows to highlight
  - `ArrowView` needs a `SetHighlight(bool)` method that applies/removes the green tint

**Highlighting implementation**:
- `BoardView.UpdateClearableHighlights(board)` — full pass, iterates all remaining arrows. Used on initial toggle-on and after seek.
- `BoardView.UpdateClearableHighlightsAfterClear(board, clearedArrow)` — targeted pass. Uses `Board.GetDependents(clearedArrow)` (expose existing `_dependedOnBy`) to update only the arrows whose clearability may have changed. O(dependents) instead of O(all arrows) — important at 4× speed on large boards.
- `ArrowView.SetHighlight(bool)` — lerps body + head color to bright green (or restores original color)
- Toggle off: restore all arrows to their normal colors

### Tap Indicator

**`TapIndicator`** (view layer, MonoBehaviour):
- Spawned at exact world-space tap position from replay `clear` and `reject` events (`posX`, `posY`)
- Visual: expanding ring/circle that fades out over ~0.4s
  - Clear events: white or green ring
  - Reject events: red ring
- Uses a simple sprite or procedural quad with a ring texture
- Self-destructs after animation completes
- Prefab: `Assets/Prefabs/TapIndicator.prefab` with SpriteRenderer + animation curve on VisualSettings

**`TapIndicatorPool`** (view layer):
- Simple object pool to avoid per-tap allocation
- `Spawn(position, isReject)` — activate from pool, set color, start expand+fade coroutine
- Pool size: 8–10 (replays rarely have overlapping taps)
- Owned by `ReplayViewController`

## Implementation Plan

### Phase 1: Domain Layer

- [ ] 1.1 Create `LeaderboardEntry` model (`Assets/Scripts/Domain/Models/LeaderboardEntry.cs`)
  - Serializable class with all fields listed above
  - Constructor from `ReplayData` (extracts board params, computes solve time, captures timestamp and game version)

- [ ] 1.2 Add `gameVersion` field to `ReplayData`
  - New string field, passed into `ReplayRecorder` from the view layer (since `Application.version` is Unity API and the recorder is in the domain layer)
  - Bump replay data version to 3
  - Handle missing field gracefully for v2 replays (default to "unknown")

- [ ] 1.3 Create `LeaderboardStore` (`Assets/Scripts/Domain/LeaderboardStore.cs`)
  - Pure C# class, no Unity dependencies
  - Methods: `AddEntry`, `GetEntries`, `GetAllEntries`, `GetPersonalBest`, `GetNeighborEntries`, `SetFavorite`, `RemoveEntry`
  - Sorting: `SortBy(entries, SortCriterion)` where `SortCriterion` is enum {Fastest, Biggest, Favorites}
  - Cap enforcement: 50 per config, 500 global; favorited entries exempt from pruning
  - JSON serialization via Newtonsoft.Json

- [ ] 1.4 Create `ReplayPlayer` (`Assets/Scripts/Domain/ReplayPlayer.cs`)
  - Pure C# class, no Unity dependencies
  - Playback state machine: play, pause, seek
  - `Advance(deltaTime)` returns fired events
  - `SeekTo(normalized)` returns target event index for rebuild
  - Speed multiplier support (0.5×, 1×, 2×, 4×)

- [ ] 1.5 Write NUnit tests (`Assets/Tests/EditMode/`)
  - `LeaderboardStoreTests.cs` — add/get/sort/cap enforcement/personal best/favorites/neighbor entries/remove
  - `ReplayPlayerTests.cs` — advance/seek/speed/boundary conditions
  - `ReplayStorageSizeTests.cs` — generate boards at various sizes (10×10, 20×20, 40×40, 100×100, 200×200), simulate a full clear sequence, serialize the resulting `ReplayData` to JSON, and assert/log the byte size. Use `[Explicit]` so they run on demand. 400×400 can be extrapolated from the growth curve rather than running directly. Output informs whether compression is needed and at what board size it becomes worthwhile.

### Phase 2: Persistence & Recording

- [ ] 2.1 Create `LeaderboardManager` (`Assets/Scripts/View/LeaderboardManager.cs`)
  - Singleton (lives across scenes via DontDestroyOnLoad)
  - File-based storage: `leaderboard.json` for index, `replays/{gameId}.json` for individual replays
  - `RecordResult(ReplayData)` — build entry, add to store, save index + replay file
  - `LoadReplay(gameId)` — load replay on demand for viewer
  - `IsPersonalBest(width, height, time)` — query store
  - `SetFavorite(gameId, bool)`, `RemoveEntry(gameId)` — delegate to store + save; delete replay file on remove

- [ ] 2.2 Hook into VictoryController
  - After board clear, call `LeaderboardManager.RecordResult()`
  - Check personal best; if yes, set gold timer + show "New Best!" label
  - Add "New Best!" label to VictoryPopup.uxml (hidden by default)
  - Add "View Leaderboard" button to VictoryPopup.uxml (navigates to leaderboard screen)

### Phase 3: Leaderboard UI

- [ ] 3.1 Add trophy button to mode-select screen
  - Add button element to Root.uxml in mode-select section
  - Style with trophy icon, position top-right
  - Wire in MainMenuController to navigate to leaderboard screen

- [ ] 3.2 Build leaderboard screen UXML/USS
  - Add leaderboard section to Root.uxml (consistent with existing screen pattern)
  - Header with back button + Local/Global toggle
  - 5 tabs, 3 sort buttons, scrollable list, empty state
  - Entry row template: rank, size label (All tab only), time, date, star icon, triple-dot menu button
  - Context menu panel: Favorite/Unfavorite, Delete
  - Global view: "Coming Soon" overlay
  - `Assets/UI/Leaderboard.uss` — styling (match existing dark theme)

- [ ] 3.3 Build `LeaderboardScreenController` (`Assets/Scripts/View/LeaderboardScreenController.cs`)
  - Tab selection logic (5 tabs)
  - Sort button state management (Fastest/Biggest/Favorites)
  - Populate scroll list from LeaderboardManager
  - Context menu show/hide/actions
  - Delete confirmation for favorited entries
  - Personal best gold highlighting
  - Watch Replay → set GameSettings, load Replay scene

- [ ] 3.4 Wire leaderboard screen into MainMenuController
  - Add as fourth screen in the screen navigation system
  - Back button returns to mode-select
  - Handle return from replay viewer (restore tab/sort state if possible)

### Phase 4: Replay Viewer

- [ ] 4.1 Extract `BoardSetupHelper` from GameController
  - Move `CreateBoardAndView`, `RestoreBoardFromSnapshot`, `SetupCamera` logic into a static/utility class
  - Refactor GameController to call the helper (no behavior change)
  - Verify existing tests still pass

- [ ] 4.2 Add replay mode to GameSettings
  - `IsReplaying` flag + `ReplayData ReplaySource` property
  - `ReturnScene` (string) — which scene/screen to return to after replay
  - `StartReplay(ReplayData, returnScene)` — set state and signal scene load

- [ ] 4.3 Create Replay scene
  - New Unity scene: camera, UIDocument for ReplayHud, BoardView parent transform
  - `ReplayViewController` as scene root MonoBehaviour
  - References to VisualSettings, camera, UIDocument (same pattern as GameController)

- [ ] 4.4 Build ReplayHud UXML/USS
  - `Assets/UI/ReplayHud.uxml` — seek slider, play/pause, speed button, exit button, highlight toggle
  - `Assets/UI/ReplayHud.uss` — styling
  - Time labels for current/total time

- [ ] 4.5 Build `ReplayViewController` (`Assets/Scripts/View/ReplayViewController.cs`)
  - Use `BoardSetupHelper` to restore board from snapshot + set up camera
  - Frame-driven playback via `ReplayPlayer.Advance()`
  - Execute clear/reject events on BoardView
  - Spawn `TapIndicator` at event positions
  - Seek: incremental add/remove — forward replays clears, backward re-adds arrows in reverse order
  - Wire HUD controls (play/pause, speed cycle, seek drag, exit, highlight toggle)
  - Exit: load `GameSettings.ReturnScene`

- [ ] 4.6 Build `TapIndicator` prefab and pool
  - `TapIndicator.cs` — expanding ring sprite, fades out over ~0.4s, self-returns to pool
  - Ring sprite asset or procedural quad
  - Color: white/green for clears, red for rejects
  - Animation curve on VisualSettings (expand scale + fade alpha)
  - `TapIndicatorPool.cs` — simple pool, `Spawn(position, isReject)`, pool size 8–10

- [ ] 4.7 Implement clearable highlighting
  - `ArrowView.SetHighlight(bool)` — apply/remove bright green tint (#00FF66)
  - `BoardView.UpdateClearableHighlights(Board)` — iterate arrows, set highlight based on `IsClearable`
  - Called after each clear event and on seek
  - Toggle button in replay HUD enables/disables

### Phase 5: Testing & Polish

- [ ] 5.1 PlayMode UI layout tests
  - Add leaderboard screen elements to UILayoutTests.cs
  - Add replay HUD elements to UILayoutTests.cs
  - Add trophy button, mini leaderboard, "New Best!" label, "Watch Replay" button assertions

- [ ] 5.2 Manual test cases (to be filled in after implementation)

- [ ] 5.3 Update documentation
  - Update `docs/TechnicalDesign.md` with new classes and architecture
  - Update `docs/OnlineRoadmap.md` to reflect local leaderboard completion
  - Delete this TODO.md
