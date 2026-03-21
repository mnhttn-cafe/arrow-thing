# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Arrow Thing** — a minimalist speed-clearing puzzle game (Unity 2D URP). Players tap arrows on a grid to clear them; an arrow is clearable only when the ray extending forward from its head to the board boundary contains no other arrow body cells. The dependency graph between arrows must be acyclic (DAG) for a board to be solvable.

The game is free and open-source (MIT). Primary distribution is WebGL on GitHub Pages, deployed automatically via CD pipeline on push to `main`.

**Current status**: Active development. Playable on GitHub Pages. See `docs/OnlineRoadmap.md` for the broader plan.

Docs: `docs/GDD.md` (game design), `docs/TechnicalDesign.md` (architecture — single source of truth for all technical decisions), `docs/BoardGeneration.md` (generator algorithm), `docs/OnlineRoadmap.md` (planned features). See **Feature Workflow** below for how `docs/TODO.md` is used during feature development.

## Architecture

The codebase is split into two layers:

- **Domain layer** (`Assets/Scripts/Domain/`) — Unity-independent pure C#. Contains board state, arrow rules, clearability logic, and generation. Must be testable without Unity runtime.
- **Unity adapter layer** — input handling, rendering, animation, scene wiring. Translates player actions into domain operations and reflects resulting state. Should not own gameplay rules. Unity is used for graphics only.

The board interaction flow: `BoardGeneration` fills `Board` → Unity renders it → player selects arrow → Unity queries `Board.IsClearable` → Unity plays feedback.

View layer scripts live in `Assets/Scripts/View/`:

- **`MainMenuController`** — drives main menu UI (UI Toolkit). Manages screen navigation, board-size preset selection, and scene transition to Game. Desktop-only quit button with confirmation modal.
- **`GameController`** — scene entry point. Orchestrated by `GenerateAndSetup` coroutine which delegates to focused helper methods. Creates `Board`, runs generation or snapshot restore, spawns `BoardView` with incremental arrow display, wires `CameraController`, `InputHandler`, and `VictoryController`. Shows loading overlay with progress bar during generation/restore; cancel button opens confirmation modal. Loading overlay rendering decoupled from work (Update-driven). Reads from `GameSettings` when coming from menu; uses inspector fields otherwise.
- **`InputHandler`** — unified PC/mobile input via Unity Input System. Left-click/touch is disambiguated into tap (select arrow) vs drag (pan camera) by a configurable screen-space distance threshold (set on `GameController`, passed via `Init`). Scroll wheel and pinch-to-zoom for camera zoom. `SetInputEnabled` suppresses all input during the victory sequence.
- **`CameraController`** — orthographic camera with `Pan`/`Zoom`/`PinchZoom`/`ZoomToFit` methods. Fits to board on init; max zoom derived from initial fit. Clamped to board bounds.
- **`VictoryController`** — handles board-cleared sequence: zoom-to-fit → grid fade-out → victory popup with randomized message and Play Again / Menu buttons. Input is disabled for the entire sequence. Font auto-scales for long messages.
- **`BoardView`** — owns `Dictionary<Arrow, ArrowView>`. Supports incremental arrow spawning via `AddArrowView`/`ApplyColoring` (used during generation/restore) or batch spawning via `Init(spawnArrows: true)`. `RemoveArrowView` removes without animation (resume replay). `TryClearArrow` checks clearability, removes or flashes reject. Fires `BoardCleared` event after last arrow's pull-out animation.
- **`BoardGridRenderer`** — renders background dot grid as a single tiling quad. `FadeOut` coroutine fades to transparent.
- **`ArrowView`** — procedural mesh body + arrowhead child GameObject. Reject flash, pull-out animation, and bump animation.
- **`ArrowMeshBuilder`** — static builder for polyline body mesh with arc-length UV windowing.
- **`VisualSettings`** — `ScriptableObject` with colors, widths, animation curves, and durations.
- **`BoardCoords`** — static coordinate mapping (cell ↔ world space).
- **`SnapSlider`** — reusable slider row: custom track+handle, value label, +/- buttons, optional lock button (snap-to-grid toggle). Used for custom board-size pickers and settings sliders.

## Core Types (`Assets/Scripts/Domain/Models/`)

- **`Cell`** — immutable `(X, Y)` value struct with `IEquatable<Cell>`. Y increases **upward** (Unity convention): `Direction.Up → dy = +1`, `Direction.Down → dy = -1`.
- **`Arrow`** — immutable ordered list of `Cell`s. `Cells[0]` is the head; `HeadDirection` is derived from the vector `Cells[0]→Cells[1]` and points **opposite** to that first segment (e.g., if next is to the right of head, the arrow faces Left).
- **`Board`** — mutable container. Arrows are private; mutate only via `AddArrow`/`RemoveArrow`. `Arrows` is exposed as `IReadOnlyList<Arrow>`. Owns `Arrow[,] _occupancy` and a dependency graph (`_dependsOn`, `_dependedOnBy`), both maintained atomically in `AddArrow`/`RemoveArrow`. `GetArrowAt(Cell)` returns the arrow at a cell (or null). `IsClearable(Arrow)` returns true when the arrow's dependency set is empty (O(1)). `IsInRay` is a public static helper for ray geometry. `InitializeForGeneration()` creates the candidate pool for generation (not needed for deserialized boards). `RestoreArrowsIncremental` coroutine restores a saved board from a snapshot in two phases (placement + dependency graph), yielding for progress reporting.

- **`GameSettings`** — static class holding board parameters chosen in the menu (`Width`, `Height`, `MaxArrowLength`) and `PlayerPrefs` key constants for persisted settings (drag threshold, zoom speed, arrow coloring). `IsSet` flag tells `GameController` whether to use menu values or inspector defaults.

Model classes are intentionally minimal and self-contained. Generation logic lives in `BoardGeneration`; clearability and dependency tracking are on `Board` since they're direct graph queries.

## Board Generation (`Assets/Scripts/Domain/BoardGeneration.cs`)

Static class, purely algorithmic — all persistent state lives on `Board`. Key design points:

- **Candidate pool** — owned by `Board`, initialized via `InitializeForGeneration()`. The lookup matrix is initialized first, then `CreateInitialArrowHeads` populates both the candidate list and lookup in a single pass. Candidates are eagerly pruned inside `Board.AddArrow` when their head or next cell becomes occupied (via `_candidateLookup`). They are also removed when the 2-cell form causes a cycle, or when the best reachable tail is shorter than `minLength`.
- **Tail construction** — `CompleteArrowTail` runs DFS+backtracking from `[head, next]`, filtering neighbors that are out-of-bounds, already visited, already occupied, lie on the head's forward ray (`Board.IsInRay`), or would cause a dependency cycle (`WouldCellCauseCycle`). It tracks the longest valid path (`best`) and returns it if `targetLength` cannot be reached exactly.
- **Cycle detection** — uses a reachability set approach. `ComputeForwardDeps` walks the candidate's ray to find all arrows it would depend on. `ComputeReachableSet` does BFS through the committed dependency graph from those arrows. `WouldCellCauseCycle` checks if any existing arrow whose ray crosses a candidate cell is in that reachable set. The reachability set is computed once per head candidate and reused for all tail cells — each cell check is stateless and independent, requiring no backtracking.

## Testing

Tests use Unity Test Framework (NUnit) in `Assets/Tests/EditMode/`. Run via Unity's **Test Runner** window (Window > General > Test Runner, EditMode tab). Performance tests are marked `[Explicit]` and only run when manually selected. Coverage: head-direction derivation, `GetDirectionStep`, `Board` mutation/bounds, generation correctness, determinism under fixed seeds, no-overlap, min-length enforcement, no-tail-in-own-ray, full solvability verification (50 seeds + counterexample), external AddArrow compatibility, and a 100-iteration timing gate. Explicit perf tests include multi-seed solvability stress tests (500×10x10, 100×20x20, 20×50x50). Unity C# is version 9.0 — avoid C# 12+ features like collection expressions.

## Feature Workflow

New features follow a three-phase workflow:

1. **Design** — Create `docs/TODO.md` with the feature design, implementation plan, and open questions. Resolve open questions before moving to implementation. When a `TODO.md` exists, treat it as the authoritative task list for the current feature. The plan must include a testing step — both automated tests for domain classes (per `CONTRIBUTING.md`) and manual test cases for user-facing behavior.
2. **Implement** — Build the feature against the plan in `TODO.md`. Do not delete or simplify `TODO.md` mid-feature; it captures design decisions and context that inform implementation.
3. **Test** — Add manual test cases to `TODO.md` after implementation. Run them and record pass/fail results before marking the feature as complete.
4. **Clean up** — Update stale documentation, delete `TODO.md`, validate `docs/TechnicalDesign.md` reflects the current architecture. The TDD is the single source of truth for all technical decisions.

## Pre-Commit / Pre-PR Checklist

Before committing or opening a PR, verify changes abide by `CONTRIBUTING.md`:

- Unity-independent domain classes have NUnit test coverage in `Assets/Tests/EditMode/`.
- UI changes (UXML/USS) are reflected in PlayMode layout tests in `Assets/Tests/PlayMode/UILayoutTests.cs` — add new elements to the relevant `AssertElements` call.
- `docs/TechnicalDesign.md` is updated if architecture or class structure changed.
- `docs/TODO.md` is deleted before the PR is merge-ready.
- No docs inconsistencies introduced.

## Key Design Rules

- Arrow minimum length: 2 cells. No hard maximum; practical caps are per-mode tuning variables.
- Board occupancy is exclusive — one arrow per cell.
- Seeded RNG must be supported for reproducible boards.
- Replay system is event-log driven (JSON format).
- C# nullable annotations are not used (no `csc.rsp`). Reference types are nullable by default.
