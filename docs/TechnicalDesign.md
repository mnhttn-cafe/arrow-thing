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
- [`BoardGenerationPlan.md`](BoardGenerationPlan.md): high-level generator notes and algorithm explanation.

## Architecture Overview

- Domain layer (Unity-independent):
  - Location: `Assets/Scripts/Models`
  - Contains board state, arrow rules, clearability logic, and generation logic.
  - Must be testable via .NET unit tests without Unity runtime.
- Unity adapter layer (Unity-dependent):
  - Input handling, rendering, animation, scene wiring, and UI.
  - Should translate user actions to domain operations and reflect resulting state.
  - Should avoid owning gameplay rules.

## Core Types and Responsibilities

### `BoardCell` (`struct`)

- Immutable value type with `X`, `Y`.
- Equality and hashing support dictionary/set usage for occupancy and path checks.

### `ArrowDirection` (`enum`)

- Direction values: `Up`, `Right`, `Down`, `Left`.
- Shared directional primitive used by clear checks and ray traversal.

### `ArrowModel` (`sealed class`)

- Represents one arrow as ordered contiguous cells.
- Invariant: arrow has at least 2 cells.
- `HeadCell` is `Cells[0]`.
- `HeadDirection` is derived from `Cells[0] -> Cells[1]` and points opposite first segment.
- Provides direction-to-step utility (`GetDirectionStep`) for ray traversal.

### `BoardModel` (`sealed class`)

- Owns board dimensions and authoritative occupancy state.
- Maintains:
  - `List<ArrowModel>` for current arrows.
  - `Dictionary<BoardCell, ArrowModel>` occupancy map for fast collision/ray queries.
- Primary responsibilities:
  - Bounds and occupancy checks (`Contains`, `IsOccupied`).
  - Placement/removal entry points (`TryAddArrow`, `TryRemoveArrow`).
  - Rule checks (`CanPlaceArrow`, `CanRemoveArrow`).
  - Enumerating free cells for generation (`GetFreeBoardCells`).

### `BoardGenerator` (`sealed class`)

- Procedurally creates arrows using seeded or unseeded RNG.
- Public operations:
  - `FillBoard(...)` to place up to N arrows.
  - `TryGenerateSingleArrow(...)` to produce one legal arrow candidate.
- Algorithm characteristics:
  - Builds valid minimal arrows (length 2) as start states.
  - Extends arrow tails with DFS.
  - Uses a precomputed head-ray set to reject self-blocking growth.
  - Validates candidate paths through `BoardModel.CanPlaceArrow`.

## Rule and Data Invariants

- Cells in an arrow are orthogonally connected.
- Board occupancy is exclusive (one arrow per cell).
- An arrow is removable only when no occupied cell exists on its forward head ray.
- New placements must not create impossible dependency chains.
- Generation must only emit arrows that pass board placement checks.

## Board Interaction Flow (Current)

1. Generate board state in domain (`BoardGenerator` + `BoardModel`).
2. Unity layer renders domain state.
3. Player selects arrow in Unity layer.
4. Unity layer queries/removes via domain (`CanRemoveArrow`/`TryRemoveArrow`).
5. Unity layer plays success/failure feedback based on domain result.

## Testing Strategy

- Unity-independent domain logic is covered by NUnit tests under `tests/ArrowThing.Model.Tests`.
- Coverage expectations are defined in [`CONTRIBUTING.md`](../CONTRIBUTING.md).
- Priority test areas:
  - head-direction derivation
  - placement/removal legality
  - generation validity and determinism characteristics
  - occupancy and bounds invariants

## Decision Log

- 2026-02-28: Adopted split between Unity-independent domain logic and Unity adapter layer.
- 2026-02-28: Defined `BoardModel` as authoritative source for occupancy and legality checks.
- 2026-02-28: Defined `BoardGenerator` as reusable source for initial fill and single-arrow generation.
- 2026-02-28: Standardized this document as the source of truth for architecture and class-structure changes.
