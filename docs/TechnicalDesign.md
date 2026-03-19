# Arrow Thing - Technical Design Document

## Purpose

Capture technical design decisions for architecture, domain model structure, and rules implementation.

This document is the implementation-facing counterpart to [`GDD.md`](GDD.md).

## Goals

- Keep gameplay rules deterministic and testable.
- Isolate Unity-independent domain logic from Unity scene/view code.
- Make multiplayer/server-authoritative evolution feasible without rewriting core rules.

## Related Docs

- [`GDD.md`](GDD.md): game design goals and player-facing behavior.
- [`BoardGeneration.md`](BoardGeneration.md): generator algorithm, dependency graph maintenance, and cycle detection.
- [`OnlineRoadmap.md`](OnlineRoadmap.md): v0.2 online features plan (server, leaderboards, replays, accounts).

## Architecture Overview

- Domain layer (Unity-independent):
  - Location: `Assets/Scripts/Domain/`
  - Contains board state, arrow data, and generation logic.
  - Must be testable without Unity runtime dependencies (tests use Unity Test Framework / NUnit in `Assets/Tests/EditMode/`).
- Unity adapter layer (Unity-dependent):
  - Input handling, rendering, animation, scene wiring, and UI.
  - Should translate user actions to domain operations and reflect resulting state.
  - Should avoid owning gameplay rules.

## Core Types and Responsibilities

### `Cell` (`readonly struct`)

- Immutable value type with `X`, `Y`. Y increases upward.
- Implements `IEquatable<Cell>` for use in `HashSet<Cell>` and `Dictionary` keying.

### `Arrow.Direction` (`enum`)

- Values: `Up`, `Right`, `Down`, `Left`.
- Nested inside `Arrow`. Used for ray traversal and cycle detection.

### `Arrow` (`sealed class`)

- Represents one arrow as an ordered list of contiguous cells.
- Invariant: at least 2 cells.
- `HeadCell` is `Cells[0]`. `HeadDirection` is derived from the vector `Cells[0] → Cells[1]` and points **opposite** to that segment.
- `GetDirectionStep(Direction)` converts a direction to a `(dx, dy)` step for ray traversal.

### `Board` (`sealed class`)

- Grid dimensions (`Width`, `Height`) and `List<Arrow> Arrows`.
- Owns `Arrow[,] _occupancy` and a dependency graph (`_dependsOn`, `_dependedOnBy`), both maintained atomically in `AddArrow`/`RemoveArrow`.
- `OccupiedCellCount` — incremental counter maintained by `AddArrow`/`RemoveArrow`. Used by the loading progress bar without per-frame iteration.
- `InitialCandidateCount` / `RemainingCandidateCount` — candidate pool size at initialization and current remaining count. Useful for diagnostics and profiling.
- `Contains(Cell)` performs bounds checking.
- `GetArrowAt(Cell)` returns the arrow occupying a cell, or null.
- `IsClearable(Arrow)` returns true when the arrow's dependency set is empty (O(1)).
- `IsInRay(Cell, Cell, Direction)` is a public static helper for ray geometry.
- `InitializeForGeneration()` creates the candidate pool for arrow generation (only needed when generating, not for deserialized boards).

### `GameTimer` (`sealed class`)

- Two-phase timer: inspection countdown followed by solve timer. Pure C# — no Unity dependency.
- Phases: `Inspection → Solving → Finished`. Driven by `Tick(double current)` for display updates.
- `StartSolve(current)` transitions from inspection to solving. `Finish(current)` ends the solve.
- `Resume(current, priorElapsed)` skips inspection and restores the timer to a previously saved solve-elapsed offset, used when loading a saved game.
- Display during play uses frame time (`Time.timeAsDouble`). Final precise time uses input-event timestamps (via `InputAction.canceled` callback) to avoid frame-boundary imprecision.
- Fires `PhaseChanged` event on transitions.

### `ReplayEvent` (`sealed class`, `[Serializable]`)

- One entry in the save/replay event log. Fields vary by event type; unused fields default to 0/null.
- `seq` — monotonically increasing, defines event order (timestamps can tie at e.g. `start_solve + clear`).
- `type` — string constant from `ReplayEventType` (e.g. `"clear"`, `"session_leave"`).
- `t` — seconds since solve start. Solve-relative time for `clear`/`reject`/`end_solve`; solve elapsed snapshot for `session_leave` (used to restore the timer on resume); always 0 for `start_solve`.
- `posX`, `posY` — world-space tap position (for `clear`, `reject`). Cell derived via `BoardCoords.WorldToCell`.
- `timestamp` — ISO 8601 UTC string (for session events, `clear`, and `end_solve`).

### `ReplayEventType` (`static class`)

- String constants for all event types: `session_start`, `session_leave`, `session_rejoin`, `start_solve`, `clear`, `reject`, `end_solve`.
- Uses strings (not enum) for human-readable JSON serialization.

### `ReplayData` (`sealed class`, `[Serializable]`)

- Full save/replay record for one game session.
- Contains: `version`, `gameId` (UUID), `seed`, board dimensions, `inspectionDuration`, `List<ReplayEvent> events`, `finalTime` (-1 = in-progress).
- Serializes to JSON via `Newtonsoft.Json`. Stored at `Application.persistentDataPath/savegame.json`.

### `ReplayRecorder` (`sealed class`)

- Accumulates `ReplayEvent`s during play, auto-increments `seq`.
- Constructor overload accepts prior events + `nextSeq` for resuming a saved game.
- `ToReplayData(...)` returns a snapshot (copy) of all accumulated events as a `ReplayData`.
- Pure C# — no Unity dependency.

### `ClearResult` (`enum`)

- Return type of `BoardView.TryClearArrow`. Values: `Blocked = 0`, `Cleared`, `ClearedFirst`, `ClearedLast`.
- `Blocked = 0` so all success values are nonzero for easy truthiness-style checks.
- `ClearedFirst`/`ClearedLast` drive timer phase transitions in `InputHandler`.

### `BoardGeneration` (`static class`)

- Procedurally fills a `Board` with acyclic arrows.
- Public entry points: `FillBoard(...)` and `GenerateArrows(...)`.
- Stateless — all persistent state (dependency graph, candidate pool) lives on `Board`.
- Cycle detection uses a reachability set computed from forward deps and checked per-cell against the committed dependency graph.
- See [`BoardGeneration.md`](BoardGeneration.md) for full algorithm details.

## Rule and Data Invariants

- Cells in an arrow are orthogonally connected.
- Board occupancy is exclusive (one arrow per cell).
- An arrow is clearable only when no occupied cell exists on its forward head ray to the board boundary.
- New arrow placements must not create cyclic clear dependencies.
- Generation must only emit arrows that satisfy the acyclicity invariant.

## Board Interaction Flow (Intended)

1. Generate board state in domain (`BoardGeneration` fills a `Board`).
2. Unity layer renders domain state.
3. Player selects arrow in Unity layer.
4. Unity layer queries a domain rules class for clearability and removes the arrow if valid.
5. Unity layer plays success/failure feedback based on the result.

## View Layer (`Assets/Scripts/View/`)

### Main Menu (`MainMenu` Scene)

- **`MainMenuController`** — drives the main menu UI via UI Toolkit. Manages three screens (Main Menu, Mode Select, Settings) toggled via USS `display: none`. Mode Select presents a flex-wrap preset grid (Small 10×10, Medium 20×20, Large 40×40, XLarge 100×100) plus a custom preset card with width/height `SliderInt` controls (range 2–400). Custom selection is restored when returning from a game if dimensions don't match a preset. On Start, writes chosen dimensions to `GameSettings` and loads the Game scene. Desktop-only quit button (top-left X) opens a confirmation modal; hidden on mobile via `Application.isMobilePlatform`. When a saved game exists, a teal "Continue" button appears below "Play"; "Play" always goes to mode select (no confirmation modal). "Continue" calls `GameSettings.Resume()` and loads the Game scene.
- **`GameSettings`** (static class, domain layer) — holds `Width`, `Height`, `MaxArrowLength`, `IsSet`, `IsResuming`, and `ResumeData`. `GameController` reads from it when `IsSet` is true. `Apply()` sets board params for a new game; `Resume(ReplayData)` sets params and flags a resume; `Reset()` clears all.
- **`SaveManager`** (static class, view layer) — saves/loads/deletes the in-progress game JSON at `Application.persistentDataPath/savegame.json`. Wraps `Newtonsoft.Json` serialization. Safe: catches I/O exceptions, logs warnings, auto-deletes on corruption.

### Scene Wiring

- **`GameController`** — scene entry point. Creates `Board`, runs generation, spawns `BoardView`, wires `CameraController`, `InputHandler`, `GameTimerView`, and `VictoryController`. Creates the `GameTimer` domain model and passes it to both `InputHandler` (for input-precision timestamps) and `VictoryController` (for final time display). Creates a `ReplayRecorder` and passes it to `InputHandler` to capture all tap events. During multi-frame generation, shows a loading overlay with a progress bar and percentage label; the existing HUD X button cancels generation (timer and trail toggle are hidden until generation completes). Progress is based on arrow count against an estimated total (see `docs/BoardGeneration.md` § "Loading Progress Heuristic"). Reads board parameters from `GameSettings`; when `IsResuming`, regenerates the board from the saved seed, applies prior cleared arrows (no animation), and restores the timer via `GameTimer.Resume()`. The leave-game modal adapts to game state: if arrows have been cleared, it shows "Save game?" with Yes/No/X-close (and a "replace save" warning if a prior save exists); if no arrows cleared, it shows "Leave game?" with Yes/No. Board completion records `end_solve` and deletes the save file.
- **`InputHandler`** — unified PC/mobile input via Unity Input System. Left-click/touch is disambiguated into tap (select arrow) vs drag (pan camera) by a configurable screen-space distance threshold (set on `GameController`, passed via `Init`). Scroll wheel and pinch-to-zoom for camera zoom. Exposes `SetInputEnabled` to suppress all input during the victory sequence. On each tap: records `start_solve` (if transitioning from inspection), then `clear` or `reject` to the optional `ReplayRecorder`. Timer phase transitions driven by input-precision wall-clock timestamps.
- **`CameraController`** — orthographic camera with `Pan`/`Zoom`/`PinchZoom`/`ZoomToFit` methods. Fits to board on init; max zoom is derived from the initial fit (not configurable). Clamped to board bounds. `ZoomToFit` smoothly returns to the initial view with a SmoothStep coroutine.
- **`GameTimerView`** — drives a `GameTimer` each frame and updates the HUD timer label. During inspection: grey whole-second countdown, turns red at a configurable warning threshold. During solving: white whole-second count-up. On finish: precise millisecond display.
- **`VictoryController`** — handles the board-cleared sequence. `GameController` disables input then invokes `OnBoardCleared`, which runs: zoom-to-fit → grid fade → victory popup with a randomized playful message, final solve time, and Play Again / Menu buttons. Font size auto-scales for long messages. Hides the game HUD when the popup appears.

### Board and Arrow Rendering

- **`BoardView`** — owns `Dictionary<Arrow, ArrowView>`. Spawns grid and arrow views. `TryClearArrow` checks clearability, returns `ClearResult` (replacing the old `bool` return), and triggers pull-out or bump animation accordingly. Tracks clear count to distinguish `ClearedFirst` / `ClearedLast`. Manages trajectory highlight state (`SetAllTrajectoriesVisible`) and fires `TrajectoryAutoOff` when a successful clear is made while the toggle is on.
- **`BoardGridRenderer`** — spawns dot sprites at each cell center.
- **`BoardCoords`** — static coordinate mapping between cell indices and world-space positions.
- **`ArrowView`** — procedural mesh body + arrowhead child GameObject. Manages reject flash and clear/bump animations. Owns a `TrajectoryLine` child GameObject (hidden by default) built from the already-computed extended path — the mesh window `[0, extensionDist]` renders a thin line from the exit point back to the arrow head, making the clearability ray visible to the player.
- **`ArrowMeshBuilder`** — static builder that generates a polyline mesh for the arrow body with arc-length UVs and a sliding visibility window.
- **`VisualSettings`** — `ScriptableObject` with visual tuning parameters: colors, widths, animation curves, and durations. Includes `trajectoryHighlightColor` (arrow color at low alpha, for trajectory line rendering).

### Arrowhead Separation

The arrowhead is a separate child GameObject with its own material instance, not part of the body mesh:

- Procedural triangle mesh (3 verts) — resolution-independent at any zoom.
- Uses the same `ArrowBody` shader as the body, so the reject flash drives `_FlashT` on both materials in sync.
- During animations, the arrowhead position is set by sampling the path at the window's leading edge. No mesh rebuild needed for the arrowhead.

### Animation System

All animations apply only to the tapped arrow. No other arrow on the board moves during a clear attempt.

#### Arc-Length Windowing

`ArrowMeshBuilder.Build` accepts `windowStart` and `windowEnd` parameters that clip the visible body mesh to a sub-range of the arrow's total arc length. Both parameters advance by the same `slideOffset` each frame, keeping the visible body length constant (the arrow slides along its path without stretching).

This approach is necessary because arrows are polylines with bends — a rigid `transform.position` offset would shift all vertices uniformly, causing bent arrows to move sideways at their middle segments instead of sliding along their own shape.

#### Pull-Out (Clearable Arrow)

- `Board.RemoveArrow` is called immediately before the animation starts, so other arrows become clearable right away.
- The path is extended at init with a synthetic exit point along the head direction to ensure the arrow fully exits the viewport.
- `slideOffset` advances from `0` along the extended path, driven by `clearSlideCurve`. Both window edges move in lockstep.
- Once the arrowhead exits the visible area, `windowEnd` stops and `windowStart` continues (tail-drain), shrinking the visible body to zero. The GameObject is destroyed when `windowStart >= windowEnd`.

#### Bump (Blocked Arrow)

- `Board.GetFirstInRay` finds the blocking arrow. The contact point is the midpoint of the blocker's first ray-intersecting cell.
- **Slide phase**: `slideOffset` advances to `contactArcLength` via `bumpSlideCurve`.
- **Bump phase**: `slideOffset` overshoots slightly past contact and springs back, driven by `bumpCurve`. The reject flash fires at contact.
- **Return phase**: `slideOffset` returns to `0` via `bumpReturnCurve`.
- No domain state changes — the arrow stays on the board throughout.

## Known Limitations

### Mobile UI Scaling

The menu UI (UI Toolkit) is designed and tested for desktop resolutions only. On mobile devices, the UI renders oversized and vertically cropped due to fixed pixel font sizes and padding that don't adapt to mobile DPI or aspect ratios.

**What's broken:** buttons and title overflow the viewport on portrait mobile screens. The game scene (world-space rendering) scales fine since `CameraController` fits to board bounds — only the screen-space UI is affected.

**Why it's deferred:** fixing this properly requires either responsive USS (viewport-relative units, media-query-like breakpoints) or a PanelSettings scale mode tuned per platform. Both approaches need dedicated design and testing across device sizes — it's a separate UX pass, not a quick CSS fix. The GDD targets mobile-first for shipping, but desktop is sufficient for MVP gameplay validation.

**Unblocks:** all gameplay and input (including touch/pinch) work correctly on mobile. Only the menu UI is affected.

## Testing Strategy

- Domain logic must be testable without Unity runtime dependencies.
- Tests use Unity Test Framework (NUnit) in `Assets/Tests/EditMode/`.
- Priority test areas:
  - head-direction derivation
  - clearability / ray obstruction logic
  - generation validity, correctness, and determinism under fixed seeds
  - occupancy and bounds invariants
  - generation performance benchmarks (to catch regressions)

### PlayMode Tests (`Assets/Tests/PlayMode/`)

UI layout tests verify that all UI elements are visible and not clipped across multiple aspect ratios. Tests load UXML assets programmatically (via `AssetDatabase`) onto a runtime `UIDocument`, simulate different screen sizes by modifying `PanelSettings.referenceResolution`, and assert element bounds.

- **`UILayoutTestHelper`** — reusable utilities: `AspectRatio` struct, `SetPanelReferenceResolution`, `AssertElementFullyVisible`, `WarnElementFullyVisible`, `AssertAllVisibleChildren`, `WaitForLayoutResolve`.
- **`UILayoutTests`** — 12 UI states (main menu, main menu with save/continue, mode select, settings, quit modal, 3 victory message tiers, game HUD, game HUD loading overlay, game HUD leave modal with save variant, victory with time) tested across 5 aspect ratios (16:9, 4:3, 21:9, 9:16, 1:1) = 60 test cases. Portrait (9:16) failures are reported as warnings (not hard failures) since fixed-pixel CSS is a known limitation.
- PanelSettings is saved/restored in SetUp/TearDown to avoid polluting other tests.

## CI/CD

### Formatting

[CSharpier](https://csharpier.com/) (Roslyn-based, opinionated) owns all C# formatting. Configured as a local dotnet tool (`.config/dotnet-tools.json`, pinned version). Respects `.editorconfig` for `indent_size`, `indent_style`, and `max_line_length`.

IDE0055 (the IDE's built-in formatting diagnostic) is disabled in `.editorconfig` to avoid conflicting with CSharpier's output.

Unity's Roslyn analyzer pipeline does not read `.editorconfig` during compilation — only `.ruleset` files. For IDE-time analysis, `.editorconfig` works normally in VS/Rider.

### Git Hooks (`.githooks/`)

Activated via `git config core.hooksPath .githooks`. Setup: `dotnet tool restore && git config core.hooksPath .githooks`.

- **Pre-commit**: CSharpier formatting check on staged `.cs` files, 100 MB file size gate (GitHub's limit), Asset `.meta` file sync (added/removed files must have matching `.meta`).
- **Post-merge**: removes empty directories under `Assets/` to prevent Unity from generating orphan `.meta` files.

### GitHub Actions (`.github/workflows/ci.yml`)

Three jobs run in parallel:

- **`format`**: CSharpier check, file size validation, meta file sync. Uses `dotnet tool restore` — no Unity license needed.
- **`test`**: EditMode tests via [`game-ci/unity-test-runner@v4`](https://github.com/game-ci/unity-test-runner). Requires `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` secrets.
- **`test-playmode`**: PlayMode tests (UI layout) via `game-ci/unity-test-runner@v4`. Runs without `-nographics` to ensure UI Toolkit resolves layout correctly.

### Branch Protection

`main` requires PRs, disallows force pushes and branch deletion. `enforce_admins` is off and `required_approving_review_count` is 0 so the sole contributor can merge their own PRs.

### Git Configuration

- **`.gitattributes`**: LF normalization, `diff=csharp` for `.cs` files, Unity YAML merge driver (`unityyamlmerge`) for scenes/prefabs/assets, `linguist-generated` markers to collapse Unity files in GitHub diffs, comprehensive binary type coverage. Based on [NYU Game Center's Unity-Git-Config](https://github.com/NYUGameCenter/Unity-Git-Config).
- **`.gitignore`**: Unity-generated folders, IDE files, build outputs. Includes `![Aa]ssets/**/*.meta` safety rule to prevent accidentally ignoring Asset meta files.
- **SmartMerge** (optional): `git config merge.unityyamlmerge.driver '<path>/UnityYAMLMerge merge -p %O %A %B %P'` for better Unity YAML conflict resolution.

### WebGL Deployment (`.github/workflows/deploy.yml`)

Continuous deployment to GitHub Pages. Triggers automatically after the CI workflow succeeds on `main`, or manually via `workflow_dispatch`.

- **`build-webgl`**: Checks out the repo, restores the Unity `Library/` cache, builds WebGL via [`game-ci/unity-builder@v4`](https://github.com/game-ci/unity-builder), and uploads the build as a Pages artifact. Cache key includes Unity version and a hash of `Assets/` + `Packages/manifest.json`.
- **`deploy`**: Deploys the artifact to GitHub Pages via `actions/deploy-pages@v4`. Runs in the `github-pages` environment.

WebGL player settings: Gzip compression, JS decompression fallback enabled, hash-based filenames for cache busting. Concurrency group `pages` prevents overlapping deploys.

## Decision Log

- 2026-02-28: Adopted split between Unity-independent domain logic and Unity adapter layer.
- 2026-02-28: Defined `BoardModel` as authoritative source for occupancy and legality checks.
- 2026-02-28: Defined `BoardGenerator` as reusable source for initial fill and single-arrow generation.
- 2026-02-28: Standardized this document as the source of truth for architecture and class-structure changes.
- 2026-03-06: `generation-rewrite` branch refactored away from `BoardModel`/`BoardGenerator` toward minimal model classes (`Cell`, `Arrow`, `Board`) with game logic in static classes (`BoardGeneration`). Model classes are now intentionally minimal and self-contained.
- 2026-03-13: Occupancy and `IsClearable` moved into `Board`. View layer added: `GameController`, `CameraController`, `BoardView`, `BoardGridRenderer`, `ArrowView`, `InputHandler`, `BoardCoords`. Tests migrated from standalone .NET project to Unity Test Framework (`Assets/Tests/EditMode/`).
- 2026-03-15: Added start menu (UI Toolkit). `MainMenuController` in `MainMenu` scene, `GameSettings` static class for scene-transition parameter passing, random seed by default with inspector override.
- 2026-03-15: Deferred mobile UI support. See **Known Limitations > Mobile UI Scaling** for rationale.
- 2026-03-15: Added board clear screen. `VictoryController` drives zoom-to-fit → grid fade → victory popup sequence, connected via `BoardView.BoardCleared` event. Input is disabled during the entire sequence.
- 2026-03-16: Camera max zoom derived from board fit; removed configurable `maxOrthoSize`. Drag threshold moved to `GameController` inspector field. `MainMenuController` preserves selected preset when returning from game.
- 2026-03-16: Added PlayMode UI layout tests. 35 test cases across 7 UI states and 5 aspect ratios catch clipping/overflow regressions. Portrait (9:16) failures tracked as warnings pending responsive CSS work. `UILayoutTestHelper` utility makes adding tests for new screens trivial.
- 2026-03-16: Added in-game HUD (`GameHud.uxml`) with back-to-menu button (with leave confirmation modal) and solve timer. `GameTimer` domain model tracks inspection/solve phases with input-precision timestamps for final time. `ClearResult` enum replaces `bool` return from `TryClearArrow`. `GameTimerView` drives the HUD label. Victory popup now shows final solve time.
- 2026-03-16: License changed from Source-Available v2.0 to MIT. Game is free and open-source, distributed via WebGL on GitHub Pages. Added CD pipeline (`deploy.yml`) — builds WebGL after CI passes on main, deploys to GitHub Pages automatically.
- 2026-03-13: Replaced geometric ray-hopping cycle detection with explicit dependency graph on `Board`. The old algorithm followed only the first hit per ray, missing multi-dependency cycles that surfaced after intermediate arrows were cleared. The new algorithm builds a reachability set from forward deps and checks each candidate cell against it. Generation cache (`boardCacheDict`) merged into `Board` to eliminate desync fragility. `Board.Version` removed (no longer needed without external cache). See [`BoardGeneration.md`](BoardGeneration.md) for the current algorithm.
- 2026-03-17: Added trajectory highlight toggle for playability on large boards. Trajectory lines reuse the already-computed extended path in `ArrowView` (window `[0, extensionDist]`), requiring no new geometry code. Auto-disables on successful clear to avoid stale lines.
- 2026-03-16: MVP (v0.1) declared complete. v0.2 planning started: authoritative ASP.NET Core server sharing domain code via monorepo shared `.csproj`, input-based replay system with sequence-numbered events, size-partitioned leaderboards (local + global), simple account system (username/display name/JWT). Offline-first design — game always playable without server. See [`OnlineRoadmap.md`](OnlineRoadmap.md).
- 2026-03-18: Added save-game and cancel-generation QoL features. Save uses the same event-log format as the planned v0.4 replay system (`ReplayEvent`, `ReplayData`, `ReplayRecorder`) — the save file doubles as a partial replay. `SaveManager` persists to `Application.persistentDataPath/savegame.json`. Resume regenerates the board from seed, applies prior clears via `BoardCoords.WorldToCell`, and restores the solve timer via `GameTimer.Resume()`. Cancel generation reuses the HUD X button (timer/trail hidden during generation). Leave-game modal adapts to state: "Save game?" with Yes/No/X-close when arrows have been cleared (with "replace save" sub-text if prior save exists); "Leave game?" with Yes/No when no arrows cleared. `InputHandler` records all tap events to the `ReplayRecorder`. `end_solve` event recorded on board completion. No auto-save on focus loss — save only via explicit leave modal.
