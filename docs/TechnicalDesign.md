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
- [`BoardGeneration.md`](BoardGeneration.md): generator behavior and algorithm explanation.

## Architecture Overview

- Domain layer (Unity-independent):
  - Location: `Assets/Scripts/`
  - Contains board state, arrow data, and generation logic.
  - Must be testable via .NET unit tests without Unity runtime.
- Unity adapter layer (Unity-dependent):
  - Input handling, rendering, animation, scene wiring, and UI.
  - Should translate user actions to domain operations and reflect resulting state.
  - Should avoid owning gameplay rules.

## Core Types and Responsibilities

### `Cell` (`readonly struct`)

- Immutable value type with `X`, `Y`. Y increases downward.
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

- Minimal container: grid dimensions (`Width`, `Height`) and `List<Arrow> Arrows`.
- `Contains(Cell)` performs bounds checking.
- Does not own an occupancy map or legality logic — those live in `BoardGeneration`.

### `BoardGeneration` (`static class`)

- Procedurally fills a `Board` with acyclic arrows.
- Public entry points: `FillBoard(...)` and `GenerateArrows(...)`.
- Maintains a static per-board cache (`boardCacheDict`) holding an occupancy grid and available head candidates.
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

## Testing Strategy

- Domain logic must be testable without Unity. Unity is used only for graphics; the test suite should not depend on it.
- Tests live in a standalone .NET NUnit project at `tests/ArrowThing.Tests`. The original project (`tests/ArrowThing.Model.Tests`) was removed because it depended on the old `BoardModel`/`BoardGenerator` APIs. It will be recreated once the TODO bugs are resolved and the API is stable.
- Priority test areas:
  - head-direction derivation
  - clearability / ray obstruction logic
  - generation validity, correctness, and determinism under fixed seeds
  - occupancy and bounds invariants
  - generation performance benchmarks (to catch regressions)

## Decision Log

- 2026-02-28: Adopted split between Unity-independent domain logic and Unity adapter layer.
- 2026-02-28: Defined `BoardModel` as authoritative source for occupancy and legality checks.
- 2026-02-28: Defined `BoardGenerator` as reusable source for initial fill and single-arrow generation.
- 2026-02-28: Standardized this document as the source of truth for architecture and class-structure changes.
- 2026-03-06: `generation-rewrite` branch refactored away from `BoardModel`/`BoardGenerator` toward minimal model classes (`Cell`, `Arrow`, `Board`) with game logic in static classes (`BoardGeneration`). Occupancy map and placement/removal API moved out of the model layer. Model classes are now intentionally minimal and self-contained.
