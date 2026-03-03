# BoardGenerator Optimization and Pruning Log

## Purpose
Track all optimization and pruning techniques used in `BoardGenerator` so we can rewrite from scratch without losing working ideas or repeating failed ones.

## Scope
- Current implementation: `Assets/Scripts/Models/BoardGenerator.cs` (HEAD)
- History reviewed:
  - `15c8f90` (2026-02-28): major optimization pass
  - `f298531` (2026-02-28): follow-up behavior changes
  - `43535b5` (2026-03-02): formatting only

## Current Techniques (HEAD)

### State and lookup optimization
- Keep occupancy in `_occupiedCells` instead of repeated board queries.
- Keep head direction map in `_arrowHeads` for dependency traversal.
- Keep cell-to-owner-head map in `_occupiedOwnerHeads` for fast ray blocking lookup.
- Build `_possibleStarters` once and reuse across generation calls.

### Candidate pruning
- Minimal starters are only orthogonal pairs from unoccupied cells.
- Remove unusable starters lazily (`IsStarterUsable`) when encountered.
- On large starter pools, cap starter checks per call with `MaxStarterChecksOnLargeBoards`.
- Start from random index to reduce deterministic front-bias in scanning.

### Path growth optimization
- Use bounded stochastic attempts per target length (`RandomPathAttemptsPerLength`).
- Use `HashSet<BoardCell>` for O(1) membership during growth (`pathSet`).
- Tail-neighbor filtering prunes by:
  - out of bounds
  - occupied by existing arrows
  - already in current path
  - on the head forward ray (`IsOnHeadRay`)
  - immediate cycle risk against existing dependency graph
- Score remaining neighbor options by future branching (`CountAvailableExits`) with light randomness tie-break.

### Dependency/cycle pruning
- `WouldPlacementCreateCycle` simulates head-ray dependency chain before placement.
- `TryFindFirstBlockingHead` uses owner-head map for first-hit lookup.
- Cycle checks happen both:
  - incrementally per candidate tail neighbor
  - once again on full candidate path

### Failure-control / throughput heuristics
- In `FillBoard`, track `consecutiveFailures` and stop early when stalled.
- Trigger greedy fallback every few failures to recover throughput.
- Hard stop when failures are too high or no starters remain.

### Length selection strategy
- Sample one target length uniformly in `[minLength, maxLength]`.
- Try sampled length first, then only larger lengths.
- Do not search below sampled length for that attempt.

## Historical Changes and Lessons

### 2026-02-28 `15c8f90` (major optimization pass)
Introduced most of the current high-performance shape:
- Added cached state maps (`_occupiedCells`, `_arrowHeads`, `_occupiedOwnerHeads`).
- Replaced recursive DFS/backtracking-style growth with bounded random constructive growth (`TryBuildArrowPath`).
- Added capped starter scanning on large pools.
- Added greedy fallback and explicit failure thresholds in `FillBoard`.
- Added cycle simulation helpers for pre-placement rejection.
- Moved away from frequent whole-arrow legality checks during growth.

Lesson:
- Big speed gains came from avoiding deep recursion and reducing repeated global legality checks.
- Tradeoff: completeness can drop; generator may miss valid arrows due to bounded stochastic search.

### 2026-02-28 `f298531` (follow-up behavior changes)
- Removed descending prebuilt length buffer (`_lengthsBuffer`).
- Switched from max-to-min sweep to sampled-length-first strategy.
- Added immediate cycle check inside neighbor selection loop.

Lesson:
- Better distribution fairness and potentially less repeated work.
- Tradeoff: when sampled length is high, shorter valid arrows are skipped for that candidate attempt.

### 2026-03-02 `43535b5`
- Formatting only, no behavioral changes.

## Known Tradeoffs to Preserve in Rewrite Notes
- `MaxStarterChecksOnLargeBoards` can return false negatives in very large candidate sets.
- `RandomPathAttemptsPerLength` can miss valid paths by chance.
- Non-backtracking constructive growth is fast but not exhaustive.
- Sampled-length-first strategy intentionally sacrifices some completeness.
- Magic thresholds (`8`, `4`, `24`, etc.) are heuristic and currently undocumented by metric.

## Rewrite Guidance

### Keep
- Cached occupancy/head maps.
- Early head-ray pruning.
- Early cycle pruning with owner-head lookup.
- Failure budgeting and controlled bailout behavior.

### Re-evaluate
- Fixed constants for checks/retries/fallback cadence.
- Non-exhaustive growth vs bounded backtracking hybrid.
- Length search policy (sample-first vs adaptive downward fallback).

### Add during rewrite
- Instrumentation counters (starter checks, prune reasons, retry counts, fail-stop reason).
- Deterministic benchmark harness to tune constants with data.
- Tests that assert both correctness and distribution goals under fixed seeds.

## Existing Test/Perf Anchors
From `tests/ArrowThing.Model.Tests/BoardGeneratorModelTests.cs`:
- Explicit performance tests exist for `TryGenerateSingleArrow` and `FillBoard` across board/length ranges.
- Duration caps in tests can serve as baseline guardrails during rewrite.
