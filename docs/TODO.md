# TODO — Main Gameplay Scene

Goal: a functional but bare-bones board. Display a generated board, pan/zoom the camera, click an arrow to attempt clearing it. No animations, no win condition, no garbage.

---

## Domain additions

- [x] **Occupancy moved into `Board`** (`Assets/Scripts/Models/Board.cs`):
  - `Board` now owns `Arrow?[,] _occupancy`, maintained atomically in `AddArrow`/`RemoveArrow`.
  - `GetArrowAt(Cell) → Arrow?` — returns the arrow at a cell, or null if empty/out-of-bounds. Enables hit-testing tap coordinates against the grid without defining per-arrow tappable regions.
  - `IsClearable(Arrow) → bool` — walks the forward ray from the head; returns false if any other arrow occupies a ray cell.
  - `BoardGeneration.BoardCacheData` no longer owns its own `occupancy` array; all occupancy reads now go through `board.GetArrowAt()`. `DoesArrowCandidateCauseCycle` and `CompleteArrowTail` no longer take `cache` for occupancy purposes.
  - NUnit tests added to `BoardTests.cs`: `GetArrowAt` (after add, empty cell, after remove, out-of-bounds), `IsClearable` (empty ray, blocked ray, head at edge, own cells don't block self). All 47 tests pass.

---

## Scene setup (`Assets/Scenes/Game.unity`)

- [x] `GameController` MonoBehaviour (`Assets/Scripts/View/GameController.cs`):
  - Holds `Board` reference. Configurable board size, seed, min/max arrow length via Inspector.
  - On `Awake`: creates `Board`, calls `BoardGeneration.FillBoard(...)`, instantiates `BoardView`, wires `CameraController` and `InputHandler`.
  - Sets camera background color from `VisualSettings`.
- [x] `CameraController` MonoBehaviour (`Assets/Scripts/View/CameraController.cs`):
  - Pan: right/middle mouse drag. Clamped to board bounds + configurable buffer.
  - Zoom: scroll wheel. Clamped between min/max orthographic size.
  - On init: fits camera to board with 10% buffer margin.
- [x] **Scene file**: `Assets/Scenes/Game.unity` wired with GameController GameObject, VisualSettings and Camera references assigned.

---

## Board rendering

- [x] `BoardCoords` static class (`Assets/Scripts/View/BoardCoords.cs`):
  - `CellToWorld(Cell, boardWidth, boardHeight)` — board centered at world origin, 1 unit per cell.
  - `WorldToCell(Vector3, boardWidth, boardHeight)` — inverse mapping for input hit-testing.
  - `ArrowPathToWorld(Arrow, boardWidth, boardHeight)` — converts arrow cells to world-space points for mesh building.
- [x] `BoardView` MonoBehaviour (`Assets/Scripts/View/BoardView.cs`):
  - Owns `Dictionary<Arrow, ArrowView>` mapping.
  - On init: spawns grid via `BoardGridRenderer`, spawns one `ArrowView` per arrow.
  - `TryClearArrow(Arrow)` — checks clearability, removes or flashes reject.
- [x] `BoardGridRenderer` MonoBehaviour (`Assets/Scripts/View/BoardGridRenderer.cs`):
  - Spawns dot sprites at each cell center using `VisualSettings.boardDotSprite`.
  - Dot scale and color driven by `VisualSettings`.

---

## Arrow rendering

Confirmed approach: **procedural mesh** with arc-length UVs, sharp path corners, quarter-round edge profile via shader.

- The path has sharp 90° corners. Each corner is filled with a square cap quad (same width as segments). No arc tessellation in geometry.
- U coordinate = cumulative arc length along path (used for future pattern/texture skinning). V coordinate = 0..1 across width.
- Quarter-round edge profile (dome/bead appearance) is a **shader concern**, not geometry: a gradient derived from V (e.g. `1 - (2v-1)²`) simulates a curved surface. Swapping the material is how skins and the 3D look are achieved — the mesh never changes.
- Sliding window (for future pull animation) = two floats `[windowStart, windowEnd]` on the arc-length axis. The mesh is clipped to that range. Sharp corners require no special handling vs. arcs.

- [x] `ArrowMeshBuilder` static class (`Assets/Scripts/View/ArrowMeshBuilder.cs`) — implemented in prior PR.
- [x] `ArrowView` MonoBehaviour (`Assets/Scripts/View/ArrowView.cs`):
  - MeshFilter + MeshRenderer. On init: builds mesh via `ArrowMeshBuilder`, assigns material from `VisualSettings`.
  - Arrowhead: child sprite at head cell, rotated to face `HeadDirection`. Color matches body.
  - Reject flash: coroutine-driven `MaterialPropertyBlock` animation (`_FlashT`, `_FlashColor`). Duration and curve from `VisualSettings`.
  - Sorting: body at order 1, head sprite at order 2 (above grid dots at 0).
- [x] Arrow body shader (`Assets/Art/Shaders/ArrowBody.shader`) — implemented in prior PR.

---

## Input & interaction

- [x] `InputHandler` MonoBehaviour (`Assets/Scripts/View/InputHandler.cs`):
  - Left-click: converts screen pos to world pos, then to nearest cell via `BoardCoords.WorldToCell`.
  - Looks up arrow via `board.GetArrowAt(cell)`. Delegates to `boardView.TryClearArrow(arrow)`.
  - Uses Unity Input System (`InputActions` asset) for click detection.
- [x] **Migrated to Unity Input System** with `InputActions` asset. Applies to both `InputHandler` and `CameraController`.

---

## Assets to create

- [x] `Assets/Art/ArrowHead.png` — arrowhead sprite (created in prior PR).
- [x] `Assets/Art/BackgroundDot.png` — grid dot sprite (created in prior PR).
- [x] `Assets/Art/Shaders/ArrowBody.shader` — URP unlit shader with dome profile and flash support.
- [x] `VisualSettings` ScriptableObject — centralized visual customization. Now includes scale parameters (`gridDotScale`, `arrowBodyWidth`, `arrowHeadScale`).
- [x] **VisualSettings asset wired**: sprite/material references assigned in the VisualSettings asset via Unity Editor.
- [x] **ArrowBody material** created using the `ArrowThing/ArrowBody` shader (`Assets/Art/Materials/ArrowBody.mat`).

---

## Stretch (do not implement now, note for later)

- Pull animation: slide `ArrowView` along the track polyline on clear.
- Win condition: all arrows cleared.
- Sound effects on clear / reject.
