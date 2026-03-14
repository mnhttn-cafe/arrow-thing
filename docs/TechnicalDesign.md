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
- `Contains(Cell)` performs bounds checking.
- `GetArrowAt(Cell)` returns the arrow occupying a cell, or null.
- `IsClearable(Arrow)` returns true when the arrow's dependency set is empty (O(1)).
- `IsInRay(Cell, Cell, Direction)` is a public static helper for ray geometry.
- `InitializeForGeneration()` creates the candidate pool for arrow generation (only needed when generating, not for deserialized boards).

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

### Scene Wiring

- **`GameController`** — scene entry point. Creates `Board`, runs generation, spawns `BoardView`, wires `CameraController` and `InputHandler`.
- **`InputHandler`** — unified PC/mobile input via Unity Input System. Left-click/touch is disambiguated into tap (select arrow) vs drag (pan camera) by a screen-space distance threshold. Scroll wheel and pinch-to-zoom for camera zoom.
- **`CameraController`** — orthographic camera with `Pan`/`Zoom`/`PinchZoom` methods. Fits to board on init. Clamped to board bounds.

### Board and Arrow Rendering

- **`BoardView`** — owns `Dictionary<Arrow, ArrowView>`. Spawns grid and arrow views. `TryClearArrow` checks clearability, triggers pull-out or bump animation accordingly.
- **`BoardGridRenderer`** — spawns dot sprites at each cell center.
- **`BoardCoords`** — static coordinate mapping between cell indices and world-space positions.
- **`ArrowView`** — procedural mesh body + arrowhead child GameObject. Manages reject flash and clear/bump animations.
- **`ArrowMeshBuilder`** — static builder that generates a polyline mesh for the arrow body with arc-length UVs and a sliding visibility window.
- **`VisualSettings`** — `ScriptableObject` with visual tuning parameters: colors, widths, animation curves, and durations.

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

## Testing Strategy

- Domain logic must be testable without Unity runtime dependencies.
- Tests use Unity Test Framework (NUnit) in `Assets/Tests/EditMode/`.
- Priority test areas:
  - head-direction derivation
  - clearability / ray obstruction logic
  - generation validity, correctness, and determinism under fixed seeds
  - occupancy and bounds invariants
  - generation performance benchmarks (to catch regressions)

## CI/CD

### Formatting

[CSharpier](https://csharpier.com/) (Roslyn-based, opinionated) owns all C# formatting. Configured as a local dotnet tool (`.config/dotnet-tools.json`, pinned version). Respects `.editorconfig` for `indent_size`, `indent_style`, and `max_line_length`.

IDE0055 (the IDE's built-in formatting diagnostic) is disabled in `.editorconfig` to avoid conflicting with CSharpier's output. IDE0001 (simplify fully qualified names) is left at default — it shows IDE squiggles in VS/Rider but cannot be enforced at build time (Roslyn marks it `EnforceOnBuild.Never`).

Unity's Roslyn analyzer pipeline does not read `.editorconfig` during compilation — only `.ruleset` files. For IDE-time analysis, `.editorconfig` works normally in VS/Rider.

### Git Hooks (`.githooks/`)

Activated via `git config core.hooksPath .githooks`. Setup: `dotnet tool restore && git config core.hooksPath .githooks`.

- **Pre-commit**: CSharpier formatting check on staged `.cs` files, 100 MB file size gate (GitHub's limit), Asset `.meta` file sync (added/removed files must have matching `.meta`).
- **Post-merge**: removes empty directories under `Assets/` to prevent Unity from generating orphan `.meta` files.

### GitHub Actions (`.github/workflows/ci.yml`)

Two jobs run in parallel:

- **`format`**: CSharpier check, file size validation, meta file sync. Uses `dotnet tool restore` — no Unity license needed.
- **`test`**: EditMode tests via [`game-ci/unity-test-runner@v4`](https://github.com/game-ci/unity-test-runner). Requires `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` secrets.

### Branch Protection

`main` requires PRs, disallows force pushes and branch deletion. `enforce_admins` is off and `required_approving_review_count` is 0 so the sole contributor can merge their own PRs.

### Git Configuration

- **`.gitattributes`**: LF normalization, `diff=csharp` for `.cs` files, Unity YAML merge driver (`unityyamlmerge`) for scenes/prefabs/assets, `linguist-generated` markers to collapse Unity files in GitHub diffs, comprehensive binary type coverage. Based on [NYU Game Center's Unity-Git-Config](https://github.com/NYUGameCenter/Unity-Git-Config).
- **`.gitignore`**: Unity-generated folders, IDE files, build outputs. Includes `![Aa]ssets/**/*.meta` safety rule to prevent accidentally ignoring Asset meta files.
- **SmartMerge** (optional): `git config merge.unityyamlmerge.driver '<path>/UnityYAMLMerge merge -p %O %A %B %P'` for better Unity YAML conflict resolution.

### Future: Builds and Deployment

[Avalin/Unity-CI-CD](https://github.com/Avalin/Unity-CI-CD) — a modular GitHub Actions pipeline (test → build → release → deploy → notify) for Unity projects. Supports multi-platform builds, SemVer tagging, and deployment to itch.io, Steam, gh-pages, AWS S3, and more. Uses `game-ci` under the hood. Reference for when we need automated builds and deployment.

## Decision Log

- 2026-02-28: Adopted split between Unity-independent domain logic and Unity adapter layer.
- 2026-02-28: Defined `BoardModel` as authoritative source for occupancy and legality checks.
- 2026-02-28: Defined `BoardGenerator` as reusable source for initial fill and single-arrow generation.
- 2026-02-28: Standardized this document as the source of truth for architecture and class-structure changes.
- 2026-03-06: `generation-rewrite` branch refactored away from `BoardModel`/`BoardGenerator` toward minimal model classes (`Cell`, `Arrow`, `Board`) with game logic in static classes (`BoardGeneration`). Model classes are now intentionally minimal and self-contained.
- 2026-03-13: Occupancy and `IsClearable` moved into `Board`. View layer added: `GameController`, `CameraController`, `BoardView`, `BoardGridRenderer`, `ArrowView`, `InputHandler`, `BoardCoords`. Tests migrated from standalone .NET project to Unity Test Framework (`Assets/Tests/EditMode/`).
- 2026-03-13: Replaced geometric ray-hopping cycle detection with explicit dependency graph on `Board`. The old algorithm followed only the first hit per ray, missing multi-dependency cycles that surfaced after intermediate arrows were cleared. The new algorithm builds a reachability set from forward deps and checks each candidate cell against it. Generation cache (`boardCacheDict`) merged into `Board` to eliminate desync fragility. `Board.Version` removed (no longer needed without external cache). See [`BoardGeneration.md`](BoardGeneration.md) for the current algorithm.
