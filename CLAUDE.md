# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Arrow Thing** — a minimalist speed-clearing puzzle game (Unity 2D URP). Players tap arrows on a grid to clear them; an arrow is clearable only when the ray extending forward from its head to the board boundary contains no other arrow body cells. The dependency graph between arrows must be acyclic (DAG) for a board to be solvable. Competitive PvP with Tetris-like garbage mechanics is planned post-MVP. Board generation will run server-side for the networked game.

Docs: `docs/GDD.md` (game design), `docs/TechnicalDesign.md` (architecture), `docs/BoardGeneration.md` (generator algorithm), `docs/TODO.md` (per-feature task tracking — created during planning, deleted when the feature PR is complete). When a TODO.md exists, treat it as the authoritative task list for the current feature. Do not delete or simplify it mid-feature; it captures design decisions and context that inform implementation.

## Architecture

The codebase is split into two layers:

- **Domain layer** (`Assets/Scripts/`) — Unity-independent pure C#. Contains board state, arrow rules, clearability logic, and generation. Must be testable without Unity runtime.
- **Unity adapter layer** — input handling, rendering, animation, scene wiring. Translates player actions into domain operations and reflects resulting state. Should not own gameplay rules. Unity is used for graphics only.

The board interaction flow: `BoardGeneration` fills `Board` → Unity renders it → player selects arrow → Unity queries `Board.IsClearable` → Unity plays feedback.

View layer scripts live in `Assets/Scripts/View/`:

- **`GameController`** — scene entry point. Creates `Board`, runs generation, spawns `BoardView`, wires `CameraController` and `InputHandler`.
- **`InputHandler`** — unified PC/mobile input via Unity Input System. Left-click/touch is disambiguated into tap (select arrow) vs drag (pan camera) by a screen-space distance threshold. Scroll wheel and pinch-to-zoom for camera zoom.
- **`CameraController`** — orthographic camera with `Pan`/`Zoom`/`PinchZoom` methods. Fits to board on init. Clamped to board bounds.
- **`BoardView`** — owns `Dictionary<Arrow, ArrowView>`. Spawns grid and arrow views. `TryClearArrow` checks clearability, removes or flashes reject.
- **`BoardGridRenderer`** — spawns dot sprites at each cell center.
- **`ArrowView`** — procedural mesh body + arrowhead sprite. Reject flash via `MaterialPropertyBlock` coroutine.
- **`BoardCoords`** — static coordinate mapping (cell ↔ world space).

## Core Types (`Assets/Scripts/Models/`)

- **`Cell`** — immutable `(X, Y)` value struct with `IEquatable<Cell>`. Y increases **upward** (Unity convention): `Direction.Up → dy = +1`, `Direction.Down → dy = -1`.
- **`Arrow`** — immutable ordered list of `Cell`s. `Cells[0]` is the head; `HeadDirection` is derived from the vector `Cells[0]→Cells[1]` and points **opposite** to that first segment (e.g., if next is to the right of head, the arrow faces Left).
- **`Board`** — mutable container. Arrows are private; mutate only via `AddArrow`/`RemoveArrow`. `Arrows` is exposed as `IReadOnlyList<Arrow>`. Owns `Arrow[,] _occupancy` and a dependency graph (`_dependsOn`, `_dependedOnBy`), both maintained atomically in `AddArrow`/`RemoveArrow`. `GetArrowAt(Cell)` returns the arrow at a cell (or null). `IsClearable(Arrow)` returns true when the arrow's dependency set is empty (O(1)). `IsInRay` is a public static helper for ray geometry. `InitializeForGeneration()` creates the candidate pool for generation (not needed for deserialized boards).

Model classes are intentionally minimal and self-contained. Generation logic lives in `BoardGeneration`; clearability and dependency tracking are on `Board` since they're direct graph queries.

## Board Generation (`Assets/Scripts/BoardGeneration.cs`)

Static class, purely algorithmic — all persistent state lives on `Board`. Key design points:

- **Candidate pool** — owned by `Board`, initialized via `InitializeForGeneration()`. All valid adjacent (head, next) pairs for all 4 directions are precomputed by `CreateInitialArrowHeads`. Candidates are eagerly pruned inside `Board.AddArrow` when their head or next cell becomes occupied (via `_candidateLookup`). They are also removed when the 2-cell form causes a cycle, or when the best reachable tail is shorter than `minLength`.
- **Tail construction** — `CompleteArrowTail` runs DFS+backtracking from `[head, next]`, filtering neighbors that are out-of-bounds, already visited, already occupied, lie on the head's forward ray (`Board.IsInRay`), or would cause a dependency cycle (`WouldCellCauseCycle`). It tracks the longest valid path (`best`) and returns it if `targetLength` cannot be reached exactly.
- **Cycle detection** — uses a reachability set approach. `ComputeForwardDeps` walks the candidate's ray to find all arrows it would depend on. `ComputeReachableSet` does BFS through the committed dependency graph from those arrows. `WouldCellCauseCycle` checks if any existing arrow whose ray crosses a candidate cell is in that reachable set. The reachability set is computed once per head candidate and reused for all tail cells — each cell check is stateless and independent, requiring no backtracking.

## Testing

Tests use Unity Test Framework (NUnit) in `Assets/Tests/EditMode/`. Run via Unity's **Test Runner** window (Window > General > Test Runner, EditMode tab). Performance tests are marked `[Explicit]` and only run when manually selected. Coverage: head-direction derivation, `GetDirectionStep`, `Board` mutation/bounds, generation correctness, determinism under fixed seeds, no-overlap, min-length enforcement, no-tail-in-own-ray, full solvability verification (50 seeds + counterexample), external AddArrow compatibility, and a 100-iteration timing gate. Explicit perf tests include multi-seed solvability stress tests (500×10x10, 100×20x20, 20×50x50). Unity C# is version 9.0 — avoid C# 12+ features like collection expressions.

## Key Design Rules

- Arrow minimum length: 2 cells. No hard maximum; practical caps are per-mode tuning variables.
- Board occupancy is exclusive — one arrow per cell.
- Seeded RNG must be supported for reproducible boards.
- Replay system is event-log driven (JSON format).
- C# nullable annotations are not used (no `csc.rsp`). Reference types are nullable by default.
