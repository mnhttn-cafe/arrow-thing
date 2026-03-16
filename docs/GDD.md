# Arrow Thing - Game Design Document

## Metadata
- Working Title: Arrow Thing
- Genre: Minimalist puzzle, speed-clearing, competitive PvP (planned)
- Platform(s): Desktop for MVP; mobile gameplay works (touch/pinch) but UI needs a responsive scaling pass before shipping
- Target Audience: Puzzle players who enjoy speed, pattern recognition, and competitive pressure
- Status: Draft v0.6
- Last Updated: 2026-03-15

## High Concept
- One-sentence pitch: Clear winding grid-based arrows as fast as possible, then weaponize your speed against opponents by sending garbage.
- Core fantasy for the player: Out-read opponents under pressure by instantly spotting free arrows and maintaining clearing flow.
- Design pillars:
  - Readability first (minimalist visuals, clear board state)
  - Speed and flow over deep puzzle solving
  - Competitive pressure through board disruption (garbage)
  - Deterministic core rules with fair procedural generation

## Core Gameplay Loop
1. Scan board for currently free arrows.
2. Tap/click an arrow to attempt a clear.
3. If clearable, resolve clear animation and update board state.
4. Repeat until board is empty (MVP) or until match end conditions are met (PvP modes).

## Controls
- Mouse: Primary control for MVP desktop build.
- Touch: Gameplay input works (tap, drag-pan, pinch-zoom); menu UI needs responsive scaling pass.
- Keyboard: No gameplay use.
- Controller: Out of scope (not a design consideration).

## Player
- Player goal:
  - MVP: Clear the board as quickly as possible.
  - PvP: Clear faster than opponents while managing incoming/outgoing garbage pressure.
- Movement model: No avatar movement; input is direct board interaction.
- Actions:
  - Select arrow.
  - Valid clear removes arrow after clear animation.
  - Invalid clear attempt plays fail animation and returns to original state.
- Resource systems:
  - MVP: No explicit punishment for misclicks. The only cost is the player's own time spent on the failed attempt.
  - PvP planned: garbage meter and packet exchange.

## Arrows System
- Arrow definition:
  - A winding shape occupying multiple contiguous grid cells with a defined arrowhead direction.
- Spawn/generation:
  - Procedural generation for initial board setup.
  - Procedural generation for garbage arrows (post-MVP).
- Behavior:
  - Logically static on the board.
  - Visually animated during clear/fail interactions.
- Selection resolution:
  - On tap, the arrow begins a "pull out" animation as if a string is being pulled.
  - The entire arrow moves along its exit path (snake-like), not a tail-to-head dissolve.
  - Display representation should support a polyline-based animation path derived from the logical arrow path.
  - If unobstructed, clear completes and arrow is removed.
  - If obstructed, motion advances until obstruction, bumps, flashes red, then retracts.
  - Audio feedback accompanies both success and failure.
- Obstruction rule (authoritative):
  - An arrow is clearable only if the ray extending forward from the arrowhead to the board boundary contains no other arrow body cells.
  - This is a discrete board-state query only; no physics colliders/hitboxes are involved.
- Solvability constraint:
  - Generation should avoid impossible boards.
  - Equivalent framing: the dependency graph between arrows must be acyclic (DAG).

## Board / Playfield
- Board topology:
  - Grid-based rectangular board for MVP and initial competitive modes.
- MVP target:
  - Start with a 20x20 playable area and tune from there.
- Occupancy and collision:
  - Cell occupancy is exclusive per arrow body segment.
  - Obstruction checks use arrowhead ray-to-edge logic.
- Camera:
  - Player can drag/pan and zoom.
  - Static framing is avoided for larger boards to reduce visual overload.

## Game States
- Main menu (minimal)
- In-game
- Clear/victory screen
- Planned later:
  - PvP match countdown/start
  - Match result screen
  - Optional pause

## Difficulty and Progression
- Core challenge source:
  - Spatial awareness and working memory under time pressure.
- Difficulty knobs:
  - Arrow count
  - Board size
  - Arrow length distribution and variance
  - Layout density
- Generation direction (initial):
  - Arrow lengths sampled from a distribution centered on shorter lengths.
  - Minimum length is 2; no fixed design-level maximum length is required.
  - Practical upper bounds can be set per mode/profile for tuning and performance.
  - Distribution shape and exact parameters are tuning variables.
  - Initial arrow-count baseline is deferred until generator playtesting.
- Mode ideas:
  - Fixed-count challenges (example: 200-arrow board leaderboard category)
  - Additional variants as systems mature
- Ranking metric:
  - Primary metric is completion time.

## PvP Vision (Post-MVP)
- Match concept:
  - Players race to clear their own boards using identical core rules.
- Main mode target:
  - Top-out mode as primary competitive ruleset.
  - Incoming garbage can overflow board capacity and cause defeat.
- Alternative mode:
  - Race-to-empty mode with board expansion is possible but secondary.
- Garbage model direction:
  - Clears build outgoing garbage potential during active chains/combos.
  - Outgoing garbage is grouped into packets.
  - Packet size increases with sustained clearing before combo end.
  - Packets send after a delay window.
  - Defensive clearing reduces/cancels incoming garbage during that window (parry-like interaction).
  - If no legal garbage placement exists for required insertion, that board is topped out.
  - For network determinism, garbage events should carry concrete arrow payloads, not only RNG seeds.
- Placement notes:
  - Candidate optimization: maintain an enumerated set of legal insertion positions and sample from it.
- Multiplayer scale:
  - Design should support more than 2 players.
- Tie policy:
  - Ties are allowed and require no tiebreaker.

## UX and Feedback
- Visual feedback:
  - Strong readable distinction between board states.
  - Clear success animation and clear failure bump/retract animation.
  - Red flash on obstruction bump.
- Audio feedback:
  - Distinct success and failure tap responses.
  - Future PvP warning cues for garbage pressure.
- Feel priorities:
  - Snappy input response.
  - High readability while zooming/panning.
  - Rising tension under top-out pressure.

## Art Direction
- Style keywords:
  - Minimalist, clean, high-contrast, legible.
- Palette direction:
  - Restrained palette with functional highlights for state changes.
- MVP asset scope:
  - Simple geometric rendering.
  - Light, purposeful effects supporting readability.

## Audio Direction
- Music:
  - Minimal and focused (optional in MVP).
- SFX priorities:
  - Tap/select
  - Successful clear resolve
  - Obstructed bump/fail
  - Board clear completion cue

## Technical Notes (Unity)
- Target Unity version: Use current project version.
- Architecture priority:
  - Board state and game rules are decoupled from Unity objects.
  - Core logic should run as pure model + controller/services.
  - Unity layer should be primarily view/input adapter over core domain API.
  - Event-driven flow is preferred for state updates and reactions.
- Rationale:
  - Easier testing and determinism.
  - Cleaner multiplayer/server-authoritative migration path.
  - Easier multi-board rendering with minimal shared mutable state.
- Scene structure (MVP):
  - Minimal menu scene.
  - Core gameplay scene.
  - Clear/result UI state.
- Key systems:
  - Board model and occupancy map.
  - Arrow model (cells + head direction).
  - Procedural board generator with solvability guarantees.
  - Input command handling and clear validation.
  - Interaction animation system (polyline pull, bump, retract).
  - Timer UI.
- Determinism and timing:
  - Isolate RNG used by board generation.
  - Seeded generation should be supported for reproducible boards.
  - UI timer updates per frame.
  - Final times are resolved from precise input/event timestamps.
  - Replay system is event-log driven: record board events/inputs and play them back deterministically.
  - Replay file format: JSON.

## Production Scope
- MVP definition:
  - Minimal start menu. **[Done — UI Toolkit menu with Play/Mode Select/Settings screens, board-size presets]**
  - Procedural arrow generation. **[Done]**
  - Core click/tap clear loop with success/fail animations. **[Done]**
  - Timer UI. **[Not started]**
  - Simple clear screen. **[Done — grid fade + victory popup with randomized messages, Play Again / Menu]**
  - Audio feedback for success/fail/clear. **[Not started]**
  - Build configuration (all gameplay scenes in build settings). **[Not started]**
- Stretch goals:
  - Fixed-count challenge variants.
  - Leaderboards by board profile.
  - Early local garbage simulation.
- Non-goals:
  - Full online PvP in first implementation pass.
  - Controller support.
  - Local persistence/profile systems in first pass.
  - Heavy art polish before rule/generation validation.
- Note on endless mode:
  - Not a current explicit target, but may emerge naturally during mode and multiplayer testing.

## Open Questions
- None currently open.

## Resolved Questions
- **Target arrow count**: Do not target a fixed count. Provide a maximum (`board area / min arrow size`) and let generation stop naturally when no valid candidates remain.
- **Length distribution**: Controlling the distribution precisely is not feasible without significant performance cost as the board fills. Accept that arrow lengths become less controllable at high density; this is an acceptable constraint given generation speed requirements.

## Changelog
- 2026-02-25: Created initial GDD skeleton in `docs/GDD.md`.
- 2026-02-25: Added detailed draft based on reference game analysis and PvP-forward vision.
- 2026-02-25: Revised to v0.3 with finalized interaction rules, mobile-first input, ray obstruction logic, camera controls, and decoupled architecture direction.
- 2026-02-25: Revised to v0.4 with concrete generation targets, top-out garbage insertion rule, precise timing/replay direction, and discrete collision-check clarifications.
- 2026-02-25: Revised to v0.5 with JSON replay format, tie-allowed policy, and generator-playtest-driven arrow-count decision.
- 2026-02-28: Revised to v0.6 with updated generation bounds language (minimum-only rule with mode-specific practical caps).
- 2026-03-06: Closed open questions on arrow count and length distribution based on generation rewrite experience.
- 2026-03-16: Updated platform target to desktop-first for MVP; mobile gameplay works but UI scaling deferred. Updated controls section accordingly.
