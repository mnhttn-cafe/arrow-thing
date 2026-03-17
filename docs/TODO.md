# TODO: Arrow Feedback Improvements (v0.1.x)

## Context
Playability feedback from 100×100 board release. Two improvements to player feedback and discoverability, split across two PRs.

---

## Feature 1: Trajectory Highlight Toggle (this PR)

### Design
A toggle button in the bottom-right HUD corner shows/hides a faint directional line on every remaining arrow. The line extends from the arrow head outward along its facing direction (the clearability ray) to the edge of the visible area. This helps players on large boards understand which arrows are in each other's path without tapping to find out.

When any arrow is successfully cleared, the toggle automatically turns off to avoid visual artifacts from now-stale trajectory lines.

### Implementation
- `VisualSettings.cs`: `trajectoryHighlightColor` (arrow color at ~18% alpha)
- `ArrowView.cs`: `_trajectoryLine` child GameObject built from the already-computed extended path (window `[0, extensionDist]`) at 30% body width, `sortingOrder = 0`; `SetTrajectoryVisible(bool)`
- `BoardView.cs`: `SetAllTrajectoriesVisible(bool)`, `TrajectoryAutoOff` event, auto-disable on successful clear
- `GameHud.uxml`: `trajectory-toggle-btn` (bottom-right)
- `GameHud.uss`: `.hud-btn--bottom-right`, `.hud-btn--active`
- `GameController.cs`: wires toggle button and subscribes to `TrajectoryAutoOff`

### Manual Test Cases
- [ ] 1. Launch game — no trajectory lines visible by default
- [ ] 2. Tap trajectory toggle — faint lines appear on all arrows, extending well past board boundary
- [ ] 3. Toggle off — all trajectory lines disappear
- [ ] 4. Clear an arrow while toggle is on — toggle auto-disables; button styling reverts
- [ ] 5. Arrow at board boundary — no crash; trajectory line has zero or minimal visible length

---

## Feature 2: Persistent Blocked Tint (next PR)

### Design
When a clear attempt is blocked, both the tapped arrow (source) and the blocking arrow (blocker) receive a light red tint — less intense than the reject flash. The tint is ephemeral: it clears when the player selects any arrow next (success or failure). Exactly the two previously tinted views are tracked in `BoardView` and restored to their original colors at the start of the next `TryClearArrow` call.

### Status
Planned — pending Feature 1 merge.
