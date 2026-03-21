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

**Scene**: Dedicated `Leaderboard` scene (separate from MainMenu). Has its own `UIDocument` and `LeaderboardScreenController`.

**Access points**:
- Trophy icon button in the **top-right corner of mode-select screen** → `SceneManager.LoadScene("Leaderboard")`. Uses `Assets/Art/trophy_icon.png`.
- "View Leaderboard" button in the victory popup → loads `Leaderboard` scene with `GameSettings.LeaderboardFocusGameId` set to the just-completed entry's `gameId`, so the screen auto-scrolls to and highlights that entry.

**Auto-scroll on entry from victory**: When `GameSettings.LeaderboardFocusGameId` is set, the leaderboard screen:
1. Determines the correct size tab from the entry's `boardWidth × boardHeight`
2. Selects that tab and sorts by Fastest (default)
3. Scrolls the list so the focused entry is visible
4. Highlights the focused row with a distinct style
5. Clears `LeaderboardFocusGameId` after consuming it

**Screen layout** (`Leaderboard.uxml`):
- Header: "Leaderboards" title + back button (top-left)
- **Local / Global toggle** (top-right, next to header) — two pill buttons
  - Local (default): shows local data
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

**`LeaderboardScreenController`** (view layer, MonoBehaviour on Leaderboard scene):
- Scene entry point for the leaderboard screen
- Manages tab selection, sort state, list population, context menu
- On start: checks `GameSettings.LeaderboardFocusGameId` for auto-scroll
- Replay launch → `GameSettings.StartReplay(replayData, "Leaderboard")` → load Replay scene
- Back button → `SceneManager.LoadScene("MainMenu")`

### Victory Screen Integration

After board clear, before showing the victory popup:
- Record entry via `LeaderboardManager.RecordResult()`
- Check if this is a personal best for the board size
- If personal best: timer label turns gold, "New Best!" text shown in victory popup

**Victory popup additions**:
- "New Best!" gold label (hidden unless applicable)
- **"View Leaderboard" button** — loads `Leaderboard` scene with `GameSettings.LeaderboardFocusGameId` set to the just-completed entry's `gameId` (auto-scrolls to the entry)

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

- [ ] 3.1 Add `LeaderboardFocusGameId` to GameSettings
  - Nullable string property, set by VictoryController before loading Leaderboard scene
  - Consumed and cleared by LeaderboardScreenController on start

- [ ] 3.2 Update VictoryController to set `LeaderboardFocusGameId`
  - Store the recorded entry's `gameId` after `RecordResult`
  - "View Leaderboard" button sets `GameSettings.LeaderboardFocusGameId` and loads `Leaderboard` scene

- [ ] 3.3 Add trophy button to mode-select screen
  - Add button element to Root.uxml in mode-select section
  - Style with trophy icon, position top-right
  - Wire in MainMenuController to load `Leaderboard` scene

- [ ] 3.4 Build leaderboard screen UXML/USS
  - New `Leaderboard.uxml` and `Leaderboard.uss` (separate scene, not in Root.uxml)
  - Header with back button + Local/Global toggle
  - 5 tabs, 3 sort buttons, scrollable list, empty state
  - Entry row template: rank, size label (All tab only), time, date, star icon, play button, triple-dot menu button
  - Context menu panel: Favorite/Unfavorite, Delete
  - Global view: "Coming Soon" overlay
  - Styling matches existing dark theme

- [ ] 3.5 Create Leaderboard scene
  - New Unity scene with camera, UIDocument for Leaderboard.uxml
  - LeaderboardScreenController as scene root MonoBehaviour

- [ ] 3.6 Build `LeaderboardScreenController` (`Assets/Scripts/View/LeaderboardScreenController.cs`)
  - Scene entry point for leaderboard screen
  - Tab selection logic (5 tabs)
  - Sort button state management (Fastest/Biggest/Favorites)
  - Populate scroll list from LeaderboardManager
  - Context menu show/hide/actions
  - Delete confirmation for favorited entries
  - Personal best gold highlighting
  - Auto-scroll: on start, check `GameSettings.LeaderboardFocusGameId` → select correct tab, scroll to entry, highlight it
  - Watch Replay → `GameSettings.StartReplay(replayData, "Leaderboard")` → load Replay scene
  - Back button → load MainMenu scene

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

- [x] 5.2 Manual test cases

#### Leaderboard Screen

| # | Test | Steps | Expected | Pass? |
|---|------|-------|----------|-------|
| L1 | Trophy button opens leaderboard | From mode-select, tap trophy icon (top-right) | Leaderboard scene loads, Small tab selected, Fastest sort active | |
| L2 | Tab switching | Tap each tab (Small → Medium → Large → XLarge → All) | List updates to show entries for that board size; All tab shows entries from all sizes with size labels | |
| L3 | Sort switching | On All tab: tap Fastest, Biggest, Favorites | List reorders correctly; Biggest sort hidden on size-specific tabs | |
| L4 | Biggest sort fallback | Select Biggest sort on All tab, then switch to Small tab | Sort falls back to Fastest (Biggest button hidden) | |
| L5 | Empty state | Select a tab with no entries | "No scores yet" message shown; scroll area hidden | |
| L6 | Personal best highlight | Complete a game on Small, open leaderboard Small tab | Fastest entry has gold highlight styling | |
| L7 | Favorite toggle | Tap star icon on an entry | Star fills/unfills; entry appears first under Favorites sort | |
| L8 | Delete non-favorited | Tap triple-dot → Delete on a non-favorited entry | Entry removed immediately, list refreshes | |
| L9 | Delete favorited | Tap triple-dot → Delete on a favorited entry | Confirmation modal appears; "Yes" deletes, "No" cancels | |
| L10 | Context menu dismiss | Tap triple-dot, then tap outside the menu | Context menu closes | |
| L11 | Local/Global toggle | Tap "Global" button | "Coming Soon" overlay shown; scroll area hidden. Tap "Local" restores list | |
| L12 | Back button | Tap back button | Returns to MainMenu scene | |
| L13 | Auto-scroll from victory | Complete a game (personal best), tap "View Leaderboard" | Correct tab selected, entry highlighted with focused style, scrolled into view | |
| L14 | Best+focused compound style | Complete a personal best on Small, tap "View Leaderboard" | Entry shows gold background + gold border (not blue focused style) | |
| L15 | Play replay from entry | Tap play button on a leaderboard entry | Replay viewer opens with correct board | |
| L16 | Return from replay | Play a replay from Large tab, exit replay | Returns to leaderboard, Large tab selected, replayed entry highlighted | |

#### Replay Viewer

| # | Test | Steps | Expected | Pass? |
|---|------|-------|----------|-------|
| R1 | Loading and playback | Open a replay from leaderboard | Loading bar fills, board appears, playback starts automatically | |
| R2 | Lead-in timer | Watch the start of a replay | 0.5s pause before first arrow clears (first clear visible) | |
| R3 | Arrow animations | Watch arrows being cleared during replay | Pull-out animations play for clears; bump animations play for rejects | |
| R4 | Tap indicators | Watch clear and reject events | White rings at clear positions, red rings at reject positions | |
| R5 | Play/Pause | Tap play/pause button | Playback pauses/resumes; button text toggles between ▶ and \|\| | |
| R6 | Speed cycle | Tap speed button repeatedly | Cycles through 0.5x → 1x → 2x → 4x → 0.5x; playback speed changes | |
| R7 | Seek forward | Drag seek handle forward while paused | Board state updates (arrows removed), time label updates | |
| R8 | Seek backward | Drag seek handle backward | Board rebuilds from scratch with arrows re-added, time label updates | |
| R9 | Seek while playing | Drag seek handle while playing | Playback pauses during drag, resumes after release | |
| R10 | Progress bar accuracy | Watch a full replay to completion | Progress bar reaches 100%, time label matches total time, no overshoot | |
| R11 | Auto-pause at end | Let a replay play to completion | Playback pauses; pressing play restarts from beginning | |
| R12 | Clearable highlighting | Toggle "Clearable" button on | All currently clearable arrows tinted electric cyan; updates after each clear | |
| R13 | Highlight toggle off | Toggle "Clearable" button off | All arrows return to normal palette colors | |
| R14 | Highlight after seek | Enable highlighting, seek to different position | Highlights update correctly for the new board state | |
| R15 | Controls toggle hide | Tap the down-arrow toggle button (bottom-right) | Controls bar hides; toggle icon changes to up-arrow; toggle moves down | |
| R16 | Controls toggle show | Tap the up-arrow toggle button | Controls bar reappears; toggle icon changes to down-arrow; toggle moves up | |
| R17 | Camera pan/zoom | Pan (drag) and zoom (scroll/pinch) during replay | Camera moves freely; playback continues normally | |
| R18 | Exit button | Tap X button (top-left) | Returns to the scene that launched the replay (leaderboard or menu) | |

#### Victory Screen Integration

| # | Test | Steps | Expected | Pass? |
|---|------|-------|----------|-------|
| V1 | Result recorded | Complete a game | Entry appears in leaderboard for that board size | |
| V2 | Personal best detection | Complete a game with best time for that size | Timer turns gold, "New Best!" label appears in victory popup | |
| V3 | Non-best result | Complete a game slower than existing best | Timer stays white, no "New Best!" label | |
| V4 | View Leaderboard button | Tap "View Leaderboard" in victory popup | Leaderboard opens to correct tab with entry highlighted | |

#### Cross-Feature

| # | Test | Steps | Expected | Pass? |
|---|------|-------|----------|-------|
| X1 | Full flow | Play game → victory → View Leaderboard → play replay → exit → verify entry | All transitions work, entry persists, replay matches original game | |
| X2 | Multiple board sizes | Complete games on Small, Medium, Large | Each appears in correct tab; All tab shows all three | |
| X3 | Cap enforcement | Add 51 entries to Small (non-favorited) | Slowest entry pruned; 50 remain; replay file deleted | |
| X4 | Favorited cap exemption | Favorite 50 entries on Small, add 51st | 51st entry added (no pruning since all are favorited) | |
| X5 | Data persistence | Complete a game, close and reopen the app | Leaderboard entry and replay still present | |

- [ ] 5.3 Update documentation
  - Update `docs/TechnicalDesign.md` with new classes and architecture
  - Update `docs/OnlineRoadmap.md` to reflect local leaderboard completion
  - Delete this TODO.md
