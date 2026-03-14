# Clear Animation Design

## Overview

When a player clears an arrow, it visually slides out of the board along its own path, head-first. If the sliding arrow's tail end reaches a static arrow that was previously blocked behind it, the static arrow plays a compression bump — a brief head-forward push and spring-back along its own path. Both animations use the same underlying mechanism: sliding an arc-length window across the arrow's path.

Domain state (`Board.RemoveArrow`) is updated immediately on clear, before the animation starts. This keeps the model ahead of the visuals so the player can chain clears without waiting for animations. Multiple pull-outs and bumps can run concurrently — overlapping arrows sliding through each other is expected and satisfying.

## Mechanism: Arc-Length Window

`ArrowMeshBuilder.Build` already accepts `windowStart` and `windowEnd` parameters that clip the visible body mesh to a sub-range of the arrow's total arc length. Segments outside the window are skipped; segments partially inside are interpolated.

The path polyline (`Vector3[]` from `BoardCoords.ArrowPathToWorld`) is fixed for the lifetime of the animation — only the window parameters change.

### Why not rigid-body translation?

Arrows are polylines with bends. A `transform.position` offset shifts every vertex uniformly, so a bent arrow would appear to move sideways in its middle segments rather than compressing or sliding along its own shape. Animating the arc-length window keeps all deformation aligned with the arrow's path.

## Arrowhead as Separate Object

The arrowhead is a separate child GameObject with its own material, not part of the body mesh. This gives several benefits:

- **Visual distinction**: a different material on the arrowhead makes arrow direction more readable.
- **Simpler mesh generation**: `ArrowMeshBuilder` only builds the body strip; no arrowhead logic.
- **Resolution-independent**: a procedural triangle (3 verts, flat-color material) is perfectly sharp at any zoom, unlike a textured quad which would pixelate like the grid dots.
- **Simpler animation**: the arrowhead never rotates during a pull-out (the head direction is constant), so animating it is just a position translation along the head direction. No mesh rebuild needed for the arrowhead — only the body rebuilds.

During pull-out, the arrowhead position is computed by sampling the extended path at the current `windowEnd` (the leading edge of the window). During a bump, the arrowhead translates forward/back with the `windowEnd` offset.

## Pull-Out Animation (Cleared Arrow)

The cleared arrow slides head-first out of the board. The arrowhead leads.

- The path is extended at init time by appending a synthetic point along the head direction, far enough to guarantee the arrow fully exits the visible area. The extension distance is derived from the board dimensions and a static multiplier (same approach as `CameraController`'s board-fit calculation — just ensure the animation multiplier is larger than the camera one so the arrow clears the viewport).
- `windowEnd` advances along the extended path, pulling the head forward.
- `windowStart` advances in lockstep, keeping the visible body length constant — the arrow appears to maintain its shape as it slides out, not stretch.
- Each frame: rebuild the body mesh with the updated window. Move the arrowhead transform to the position at `windowEnd` on the path.
- When `windowStart >= originalArcLength` (the original path, not the extension), the arrow has fully left the board — destroy the GameObject.

## Bump Animation (Static Arrow Hit by Departing Tail)

When the cleared arrow's retreating tail passes a static arrow that was blocked behind it, that arrow plays a compression bump.

- **Finding the target**: before starting the pull-out, walk the ray from the cleared arrow's head in `HeadDirection` using `Board.GetArrowAt` to find the first hit. If no arrow is in the ray, skip the bump.
- **Trigger timing**: the bump fires when the cleared arrow's `windowStart` (the trailing edge / tail) passes the arc-length position corresponding to the contact point with the static arrow. Each frame of the pull-out coroutine checks this threshold; when crossed, it triggers the bump on the target.
- **Bump shape**: temporarily advance the static arrow's `windowEnd` slightly beyond its `totalArcLength` (extending the head forward along its own extended path), then spring it back. The arrowhead translates in sync. Driven by an `AnimationCurve` in `VisualSettings`, same pattern as the reject flash.

## Body Animation: Arc-Length UV Shader vs. CPU Mesh Rebuild

Two approaches for animating the body during the window slide:

### Option A: CPU mesh rebuild (current path)
Each frame, call `ArrowMeshBuilder.Build` with updated `windowStart`/`windowEnd` and assign the new mesh. Simple, already works, low vertex counts make it cheap.

### Option B: Shader-driven clipping via arc-length UVs
Build the full mesh once. Pass `windowStart`/`windowEnd` as material uniforms. The fragment shader discards pixels where `UV.x` falls outside the window. No CPU mesh rebuild — just two `material.SetFloat` calls per frame.

Option B avoids per-frame mesh allocation and is more GPU-friendly, but adds shader complexity and requires the full mesh to remain allocated. **Decision deferred to implementation** — start with Option A, profile, switch to B if needed.

## Required Changes

### `ArrowMeshBuilder`

- Remove arrowhead triangle generation (the `headLength > 0` block). The arrowhead becomes a separate object.

### `ArrowView`

- Spawn a child GameObject for the arrowhead (procedural triangle mesh, 3 verts, flat-color material).
- Cache the `Vector3[] path` (extended), `totalArcLength`, and body mesh-build parameters from `Init`.
- Expose `PlayPullOut(float duration, System.Action onBumpContact, System.Action onComplete)` coroutine. `onBumpContact` fires at the arc-length threshold where the tail reaches the bump target.
- Expose `PlayBump(float duration, AnimationCurve curve, float magnitude)` coroutine.
- Pull-out rebuilds the body mesh each frame; moves the arrowhead transform.
- Bump rebuilds the body mesh each frame; moves the arrowhead transform.

### `BoardView`

- `TryClearArrow`: call `Board.RemoveArrow` immediately. Remove the arrow from `_arrowViews`. Find the bump target by ray-walking (before removal, or walk using board geometry since the ray direction and cells are known). Start pull-out on the cleared `ArrowView`, passing a bump-contact callback that triggers `PlayBump` on the target's `ArrowView`. Destroy the cleared arrow's GameObject on completion.

### `VisualSettings`

- Add fields: `clearSlideDuration`, `clearSlideCurve`, `bumpDuration`, `bumpMagnitude`, `bumpCurve`.
- Add field: `arrowHeadMaterial` (flat-color, separate from `arrowBodyMaterial`).
- Path extension multiplier (board-size-relative distance for the synthetic exit point).

## Resolved Decisions

- **Arrowhead geometry**: procedural triangle mesh (3 verts, flat-color material). Resolution-independent — no pixelation at any zoom level. The shape is simple enough that texture-based iteration isn't needed.
- **Bump contact point**: midpoint of the arrow body cell being hit, not the edge. Using the cell center keeps the math trivial (just the arc-length at that cell). Incorporating `arrowBodyWidth` from `VisualSettings` to use the exact edge is also straightforward if the midpoint doesn't feel right during playtesting.
- **Pull-out easing**: `AnimationCurve` in `VisualSettings` (`clearSlideCurve`). Separate curve for bump (`bumpCurve`).
- **No bump chaining**: only the cleared arrow animates. The bump target is treated as a static wall — it plays the bump animation but does not propagate force to anything behind it.
