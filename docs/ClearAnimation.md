# Clear Animation Design

## Overview

All animations apply only to the tapped arrow itself. No other arrow on the board is ever moved or modified by a clear attempt.

There are two outcomes when a player taps an arrow:

1. **Clearable** — the arrow's forward ray is empty. The arrow plays the **pull-out** animation, sliding head-first along its path until it exits the visible area. `Board.RemoveArrow` is called immediately (before the animation starts) so the player can chain clears without waiting.

2. **Blocked** — the arrow's forward ray contains at least one other arrow. The tapped arrow slides head-first along its path until it reaches the first blocking arrow (`Board.GetFirstInRay`), plays a **bump** animation (compresses against the blocker and springs back to its original position), and simultaneously plays the **reject flash**. The arrow remains on the board. No domain state changes.

## Mechanism: Arc-Length Window

`ArrowMeshBuilder.Build` already accepts `windowStart` and `windowEnd` parameters that clip the visible body mesh to a sub-range of the arrow's total arc length. Segments outside the window are skipped; segments partially inside are interpolated.

The path polyline (`Vector3[]` from `BoardCoords.ArrowPathToWorld`) is fixed for the lifetime of the animation — only the window parameters change.

### Constant arc length

The arrow body maintains a constant visible arc length throughout all animations. Both `windowStart` and `windowEnd` advance by the same amount each frame — they are defined by a single `slideOffset` value:

    windowStart = slideOffset
    windowEnd   = slideOffset + totalArcLength

All animation curves drive `slideOffset` only. The body shape never stretches or compresses.

The one exception is the tail-drain at the very end of a pull-out: once the arrowhead has exited the visible area, `windowEnd` stops advancing (there's no more extended path) and `windowStart` continues until it reaches `windowEnd`, shrinking the visible body to zero.

### Why not rigid-body translation?

Arrows are polylines with bends. A `transform.position` offset shifts every vertex uniformly, so a bent arrow would appear to move sideways in its middle segments rather than compressing or sliding along its own shape. Animating the arc-length window keeps all deformation aligned with the arrow's path.

## Arrowhead as Separate Object

The arrowhead is a separate child GameObject with its own material, not part of the body mesh. This gives several benefits:

- **Visual distinction**: separate material instance allows different color/highlight settings from the body.
- **Simpler mesh generation**: `ArrowMeshBuilder` only builds the body strip; no arrowhead logic.
- **Resolution-independent**: a procedural triangle (3 verts) is perfectly sharp at any zoom, unlike a textured quad which would pixelate like the grid dots.
- **Consistent flash**: uses the same `ArrowBody` shader as the body, so reject flash drives `_FlashT` on both materials in sync.
- **Simpler animation**: the arrowhead never rotates during either animation (the head direction is constant), so animating it is just a position translation along the head direction. No mesh rebuild needed for the arrowhead — only the body rebuilds.

During both pull-out and bump, the arrowhead position is computed by sampling the path at the current `windowEnd` (the leading edge of the window).

## Pull-Out Animation (Clearable Arrow)

The tapped arrow is clearable — nothing blocks its ray. It slides head-first out of the board and is destroyed.

- The path is extended at init time by appending a synthetic point along the head direction, far enough to guarantee the arrow fully exits the visible area. The extension distance is derived from the board dimensions and a static multiplier (same approach as `CameraController`'s board-fit calculation — just ensure the animation multiplier is larger than the camera one so the arrow clears the viewport).
- `Board.RemoveArrow` is called immediately, before the animation starts. This lets other arrows become clearable right away.
- `slideOffset` advances from `0` along the extended path, driven by `clearSlideCurve`. Both `windowStart` and `windowEnd` move in lockstep — the arrow maintains its shape as it slides out.
- Each frame: rebuild the body mesh with the updated window. Move the arrowhead transform to the position at `windowEnd` on the path.
- Once the arrowhead exits the visible area, the head has no more path to advance along. `windowEnd` stops at the end of the extended path and `windowStart` continues (tail-drain), shrinking the visible body to zero.
- When `windowStart >= windowEnd`, the arrow is fully gone — destroy the GameObject.

## Bump Animation (Blocked Arrow)

The tapped arrow is not clearable — at least one arrow blocks its ray. It slides forward until it hits the blocker, then springs back. The reject flash plays simultaneously with the bump.

All three phases animate `slideOffset` only — the visible arc length stays constant throughout.

- **Finding the blocker**: call `Board.GetFirstInRay(arrow)` to find the first arrow in the tapped arrow's forward ray.
- **Contact point**: the midpoint of the first cell of the blocking arrow that lies in the ray. Using the cell center keeps the math trivial. Incorporating `arrowBodyWidth` from `VisualSettings` to use the exact edge is straightforward if the midpoint doesn't feel right during playtesting.
- **Slide phase**: `slideOffset` advances from `0` to `contactArcLength`, driven by `bumpSlideCurve`.
- **Bump phase**: on contact, `slideOffset` overshoots slightly past `contactArcLength` and springs back to it, driven by `bumpCurve`. The reject flash fires at the same moment.
- **Return phase**: `slideOffset` goes from `contactArcLength` back to `0`, driven by `bumpReturnCurve`.

No domain state changes during any phase — the arrow stays on the board throughout.

## Body Animation: Arc-Length UV Shader vs. CPU Mesh Rebuild

Two approaches for animating the body during the window slide:

### Option A: CPU mesh rebuild (current path)
Each frame, call `ArrowMeshBuilder.Build` with updated `windowStart`/`windowEnd` and assign the new mesh. Simple, already works, low vertex counts make it cheap.

### Option B: Shader-driven clipping via arc-length UVs
Build the full mesh once. Pass `windowStart`/`windowEnd` as material uniforms. The fragment shader discards pixels where `UV.x` falls outside the window. No CPU mesh rebuild — just two `material.SetFloat` calls per frame.

Option B avoids per-frame mesh allocation and is more GPU-friendly, but adds shader complexity and requires the full mesh to remain allocated. **Decision deferred to implementation** — start with Option A, profile, switch to B if needed.

## Implementation Status

All animation features are implemented:

### Arrowhead separation (merged to main)

- `ArrowMeshBuilder` — arrowhead triangle generation removed; body-only mesh builder.
- `ArrowView` — spawns arrowhead as child GameObject (procedural 3-vert triangle mesh, centered at origin so transform controls placement). Separate material instance using the same `ArrowBody` shader. Reject flash drives `_FlashT` on both body and head material instances in sync.
- `VisualSettings` — `arrowHeadMaterial` and `arrowHeadColor` fields added. `ArrowHead.mat` asset created and wired up.
- `Board` — added input validation to `AddArrow`/`RemoveArrow` (null, duplicate, bounds, occupancy, clearability). Added `GetFirstInRay(Arrow)` to find the first blocking arrow in a ray.

### Animation system

- `ArrowView` — path is extended at init with a synthetic exit point along head direction (`pathExtensionMultiplier × max(boardWidth, boardHeight)`). Caches extended path, arc lengths, and body width. `ApplySlideOffset(float)` rebuilds body mesh with windowed arc-length and repositions the arrowhead. `PlayPullOut(Action onComplete)` slides the arrow off-screen. `PlayBump(float contactArcLength)` runs the three-phase slide/bump/return with reject flash on contact.
- `BoardView.TryClearArrow` — clearable arrows: `RemoveArrow` called immediately, pull-out animation plays, `Destroy` deferred to `onComplete`. Blocked arrows: walks ray to compute contact distance, calls `PlayBump`.
- `VisualSettings` — `clearSlideDuration`, `clearSlideCurve`, `pathExtensionMultiplier`, `bumpSlideDuration`, `bumpSlideCurve`, `bumpDuration`, `bumpMagnitude`, `bumpCurve`, `bumpReturnDuration`, `bumpReturnCurve`.

## Resolved Decisions

- **Arrowhead geometry**: procedural triangle mesh (3 verts), same `ArrowBody` shader as the body with its own material instance. Resolution-independent, consistent flash behavior.
- **Bump contact point**: midpoint of the blocking arrow's first ray-intersecting cell, not the edge. Adjustable during playtesting.
- **Easing**: separate `AnimationCurve`s in `VisualSettings` — `clearSlideCurve` for pull-out, `bumpSlideCurve` / `bumpCurve` / `bumpReturnCurve` for the three bump phases.
- **No effect on other arrows**: only the tapped arrow animates. The blocker is static — it is a wall, not a participant.
- **Reject flash timing**: fires simultaneously with the bump contact, not as a separate animation.
- **Domain state timing**: `RemoveArrow` is called immediately for clearable arrows. Blocked arrows never modify domain state.
