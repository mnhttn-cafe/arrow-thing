# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Arrow Thing** — a minimalist speed-clearing puzzle game (Unity 2D URP). Players tap arrows on a grid to clear them; an arrow is clearable only when the ray extending forward from its head to the board boundary contains no other arrow body cells. The dependency graph between arrows must be acyclic (DAG) for a board to be solvable. Competitive PvP with Tetris-like garbage mechanics is planned post-MVP. Board generation will run server-side for the networked game.

Docs: `docs/GDD.md` (game design), `docs/TechnicalDesign.md` (architecture), `docs/BoardGeneration.md` (generator algorithm), `docs/TODO.md` (tracked bugs and pending decisions — recreated per feature, may not exist).

## Architecture

The codebase is split into two layers:

- **Domain layer** (`Assets/Scripts/`) — Unity-independent pure C#. Contains board state, arrow rules, clearability logic, and generation. Must be testable without Unity runtime.
- **Unity adapter layer** — input handling, rendering, animation, scene wiring. Translates player actions into domain operations and reflects resulting state. Should not own gameplay rules. Unity is used for graphics only.

The board interaction flow: `BoardGeneration` fills `Board` → Unity renders it → player selects arrow → Unity queries a rules/logic class for clearability → Unity plays feedback.

## Core Types (`Assets/Scripts/Models/`)

- **`Cell`** — immutable `(X, Y)` value struct with `IEquatable<Cell>`. Y increases **upward** (Unity convention): `Direction.Up → dy = +1`, `Direction.Down → dy = -1`.
- **`Arrow`** — immutable ordered list of `Cell`s. `Cells[0]` is the head; `HeadDirection` is derived from the vector `Cells[0]→Cells[1]` and points **opposite** to that first segment (e.g., if next is to the right of head, the arrow faces Left).
- **`Board`** — mutable container. Arrows are private; mutate only via `AddArrow`/`RemoveArrow`, which increment `Board.Version`. `Arrows` is exposed as `IReadOnlyList<Arrow>`.

Model classes are intentionally minimal and self-contained. Game logic (generation, clearability checks, etc.) lives in separate static classes, not on the models.

## Board Generation (`Assets/Scripts/BoardGeneration.cs`)

Static class. Key design points:

- **`boardCacheDict`** — static `Dictionary<Board, BoardCacheData>` caches generation state per board across calls. `BoardCacheData` (a `class`) holds `occupancy[x,y]`, `availableArrowHeads`, `candidateLookup`, and `version`. If `board.Version` doesn't match `cache.version` on entry, `GetOrCreateCache` throws `InvalidOperationException` — external mutation is detected immediately rather than silently desyncing.
- **Head candidates** — all valid adjacent (head, next) pairs for all 4 directions are precomputed by `CreateInitialArrowHeads`. Candidates are eagerly pruned from `availableArrowHeads` when their head or next cell becomes occupied (via `candidateLookup`, a `List<ArrowHeadData>[,]` reverse-lookup array). They are also removed when the 2-cell form causes a cycle, or when the best reachable tail is shorter than `minLength`.
- **Tail construction** — `CompleteArrowTail` runs DFS+backtracking from `[head, next]`, filtering neighbors that are out-of-bounds, already visited, already occupied, or lie on the head's forward ray (`IsInRay`). It tracks the longest valid path (`best`) and returns it if `targetLength` cannot be reached exactly.
- **Cycle detection** — `DoesArrowCandidateCauseCycle` follows the forward ray from the candidate head, then hops to the next arrow's head if one is hit, continuing until the ray exits the board (no cycle) or a body cell from the current candidate or a previously-visited arrow is encountered (cycle).

## Testing

Tests live in `tests/ArrowThing.Tests` — a plain .NET 8 NUnit project with no Unity dependency. Domain source files are linked via glob in the `.csproj`. Run with `dotnet test tests/ArrowThing.Tests`. Coverage: head-direction derivation, `GetDirectionStep`, `Board` mutation/version/bounds, generation correctness, determinism under fixed seeds, no-overlap, min-length enforcement, no-tail-in-own-ray, cache desync detection, and a 100-iteration timing gate.

## Key Design Rules

- Arrow minimum length: 2 cells. No hard maximum; practical caps are per-mode tuning variables.
- Board occupancy is exclusive — one arrow per cell.
- Seeded RNG must be supported for reproducible boards.
- Replay system is event-log driven (JSON format).
- C# nullable is enabled globally via `Assets/csc.rsp`.
