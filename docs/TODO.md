# TODO — Main Gameplay Scene (`feature/main-gameplay-scene`)

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

- [ ] Configure Main Camera: orthographic, `Clear Flags = Solid Color`, background dark neutral.
- [ ] Add `CameraController` MonoBehaviour:
  - Pan: click-drag (middle mouse or right mouse). Clamp to board bounds + buffer.
  - Zoom: scroll wheel. Clamp between a min/max orthographic size.
  - On start: fit camera to board with buffer margin (e.g. 10% of board size on each side).
- [ ] Add `GameController` MonoBehaviour:
  - Holds `Board` reference.
  - On `Awake`: create a `Board`, call `BoardGeneration.FillBoard(...)` with a test size (e.g. 6×6) and fixed seed.
  - Instantiates `BoardView`.

---

## Board rendering

- [ ] Add `BoardView` MonoBehaviour:
  - Owns a `Dictionary<Arrow, ArrowView>` mapping.
  - On init: spawns one `ArrowView` per arrow.
  - Exposes `RemoveArrow(Arrow)` — destroys the corresponding `ArrowView` and calls `Board.RemoveArrow`.
  - On arrow removed: no clearability highlight refresh needed — clearable arrows are not highlighted. Failed attempts trigger a reject flash on the arrow instead.
- [ ] Add `BoardGridRenderer` MonoBehaviour (or static helper):
  - Draws dotted background grid using a grid of small dot sprites.
  - Dots at each integer cell center. Board boundary outlined with a thin border sprite/line.
  - Board is centered in world space. Cell size configurable (default 1 Unity unit).
- [ ] Define world-space coordinate mapping:
  - `CellToWorld(Cell)` → `Vector3`: `(cell.X * cellSize, cell.Y * cellSize, 0)`, offset so board center = world origin.

---

## Arrow rendering

Confirmed approach: **procedural mesh** with arc-length UVs, sharp path corners, quarter-round edge profile via shader.

- The path has sharp 90° corners. Each corner is filled with a square cap quad (same width as segments). No arc tessellation in geometry.
- U coordinate = cumulative arc length along path (used for future pattern/texture skinning). V coordinate = 0..1 across width.
- Quarter-round edge profile (dome/bead appearance) is a **shader concern**, not geometry: a gradient derived from V (e.g. `1 - (2v-1)²`) simulates a curved surface. Swapping the material is how skins and the 3D look are achieved — the mesh never changes.
- Sliding window (for future pull animation) = two floats `[windowStart, windowEnd]` on the arc-length axis. The mesh is clipped to that range. Sharp corners require no special handling vs. arcs.

- [ ] Add `ArrowMeshBuilder` static class (`Assets/Scripts/View/ArrowMeshBuilder.cs`):
  - `Build(Vector3[] pathPoints, float width, float windowStart, float windowEnd) → Mesh`
  - Generates: one rect per segment, one square fill quad per corner, UV.x = arc length, UV.y = 0..1 across width.
  - `windowStart`/`windowEnd` default to 0 and total path length (full arrow shown).
  - **Track polyline** = path from board boundary behind tail through body cells to board boundary ahead of head. Full arc length defines the window range. Store on `ArrowView` for future animation.

- [ ] Add `ArrowView` MonoBehaviour:
  - Holds a `MeshFilter` + `MeshRenderer`. On init: computes track polyline, calls `ArrowMeshBuilder.Build(...)`, assigns mesh.
  - **Arrowhead**: a child quad mesh or sprite at `CellToWorld(arrow.HeadCell)`, rotated to face `HeadDirection`. Shares the same material as the body (gets the same skin automatically).
  - **Reject flash**: `PlayRejectFlash()` — triggers a red flash on failed clear attempt. Implemented as a coroutine driving a `MaterialPropertyBlock` parameter (e.g. `_FlashColor`, `_FlashT`). The exact behavior (color, duration, curve) should be a parameter so skins/themes can override it.
  - Layer/sorting: arrows above grid.

- [ ] Add arrow body shader (`Assets/Art/Shaders/ArrowBody.shader`):
  - Unlit URP shader (or Shader Graph).
  - Input: `_Color`, `_ClearableColor` (or drive via `MaterialPropertyBlock`).
  - V-based highlight: `smoothstep` dome profile across width to simulate quarter-round edge.
  - Optionally: `_HighlightStrength` to dial the 3D illusion from flat to pronounced.

---

## Input & interaction

- [ ] Add `InputHandler` MonoBehaviour (or integrate into `GameController`):
  - Use the **Unity Input System** with an `InputActions` asset (not the legacy `Input` class).
  - On left-click: raycast from camera at mouse position.
  - Determine which arrow was hit: convert mouse world pos to nearest cell (`WorldToCell`), call `board.GetArrowAt(cell)` — no per-arrow collider/tappable region needed.
  - Call `board.IsClearable(arrow)`. If yes: `boardView.RemoveArrow(arrow)`. If no: `arrowView.PlayRejectFlash()`.

---

## Assets to create

- [ ] **`Assets/Art/Sprites/ArrowHead.png`** — simple filled triangle or chevron pointing right (Unity rotates via transform). ~64×64 px. Pivot at the flat base center. White so the shader/tint controls color.
- [ ] **`Assets/Art/Sprites/Dot.png`** — small filled circle, ~12×12 px, for the grid background. White (tint in code).
- [ ] **`Assets/Art/Shaders/ArrowBody.shader`** (or Shader Graph) — see Arrow rendering section above.
- [ ] **`VisualSettings` ScriptableObject** — a single container for all visual customization, replacing the standalone `ColorPalette` idea. Covers colors and visual assets in one place:
  - Colors: background (`#1A1A2E`), grid dot (`#2E3A59`), arrow body default (`#D0D8E8`), arrow reject flash (`#FF4444`)
  - Board dot sprite reference
  - Arrow body material reference
  - Arrowhead sprite reference
  - Reject flash parameters (color, duration, curve) — so skins/themes can override flash behavior without code changes

---

## Stretch (do not implement now, note for later)

- Pull animation: slide `ArrowView` along the track polyline on clear.
- Win condition: all arrows cleared.
- Sound effects on clear / reject.
