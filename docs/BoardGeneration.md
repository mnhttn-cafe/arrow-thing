# Board Generation

## Purpose

Describes how board generation works in `Assets/Scripts/BoardGeneration.cs` and how `Board` maintains the dependency graph that guarantees solvability.

This document is descriptive, not a future plan or API proposal.

## Scope

`BoardGeneration` is responsible for:

- Filling a board with generated arrows.
- Generating up to a requested number of additional arrows.
- Building arrow shapes by extending from length-2 head candidates.
- Rejecting candidates that would introduce cyclic clear dependencies.

`Board` is responsible for:

- Maintaining the dependency graph (`_dependsOn`, `_dependedOnBy`) atomically in `AddArrow`/`RemoveArrow`.
- Providing O(1) clearability checks via `IsClearable`.
- Owning the generation candidate pool (initialized on demand via `InitializeForGeneration`).

Neither is responsible for gameplay removal logic or rendering.

## Dependency Graph

### Structure

`Board` maintains two dictionaries:

- `_dependsOn[arrow]`: the set of arrows that block `arrow` from being cleared (their cells are in `arrow`'s forward ray).
- `_dependedOnBy[arrow]`: the set of arrows whose forward rays pass through `arrow`'s cells (they depend on `arrow`).

Both are updated atomically in `AddArrow` and `RemoveArrow`.

### Maintenance in `AddArrow`

1. Update occupancy.
2. Walk the new arrow's forward ray — every existing arrow hit is a forward dependency.
3. For each existing arrow, check if any of the new arrow's cells lie on that arrow's ray (`IsInRay`) — if yes, that existing arrow now depends on the new arrow (reverse dependency).
4. Prune stale generation candidates if the candidate pool is initialized.

### Maintenance in `RemoveArrow`

1. Clear occupancy.
2. For each arrow in `_dependsOn[arrow]`: remove `arrow` from their `_dependedOnBy` set.
3. For each arrow in `_dependedOnBy[arrow]`: remove `arrow` from their `_dependsOn` set.
4. Remove `arrow`'s own entries from both dictionaries.

### `IsClearable`

```csharp
public bool IsClearable(Arrow arrow) => _dependsOn[arrow].Count == 0;
```

O(1). An arrow is clearable when nothing blocks its forward ray.

## Generation Candidate Pool

### Initialization

`Board.InitializeForGeneration()` creates the candidate pool. Called by `FillBoard` or lazily by `GenerateArrows` if not yet initialized.

- `_availableArrowHeads`: pool of remaining unpruned 2-cell head candidates.
- `_candidateLookup`: `List<ArrowHeadData>[,]` reverse-lookup — maps each cell to the candidates whose `head` or `next` touches it.

The candidate pool is only needed during generation. Deserialized boards (replays, netcode) skip initialization entirely.

### `ArrowHeadData`

- `head`: candidate `Cells[0]`.
- `next`: candidate `Cells[1]`.
- `direction`: candidate head direction.

### Head Candidate Enumeration

`Board.CreateInitialArrowHeads()` precomputes all valid adjacent `(head, next)` pairs for all 4 directions. These are geometric candidates only — filtered later by occupancy checks, cycle detection, and tail length.

Y-up coordinate convention: `Direction.Up → dy = +1`, `Direction.Down → dy = -1`.

## Public Entry Points

### `FillBoard(Board board, int minLength, int maxLength, Random random, int deadEndLimit = 10)`

- Calls `board.InitializeForGeneration()`.
- Computes `maxPossibleArrows = width * height / 2`.
- Delegates to `GenerateArrows(...)` with that amount.

### `GenerateArrows(Board board, int minLength, int maxLength, int amount, Random random, out int createdArrows, int deadEndLimit = 10)`

Flow:

1. Ensure candidate pool is initialized.
2. Repeatedly call `TryGenerateArrow(...)` until `createdArrows == amount` or candidates are exhausted.
3. On each successful arrow, call `board.AddArrow(arrow)` which atomically updates occupancy, dependency graph, and candidate pool.
4. Return `createdArrows == amount`.

## Single Arrow Construction

### `TryGenerateArrow(...)`

1. Sample one target length in `[minLength, maxLength]`.
2. While head candidates remain:
   - pick a random candidate from `_availableArrowHeads`
   - reject and remove if head or next cell is occupied
   - compute the reachability set for cycle detection (see below)
   - reject and remove if head or next cell would cause a cycle
   - call `CompleteArrowTail(...)` to grow the body
   - reject and remove candidate if resulting tail is shorter than `minLength`
   - otherwise return the arrow
3. Return false if all candidates are exhausted.

### `CompleteArrowTail(...)`

DFS+backtracking from the fixed 2-cell start `[head, next]`.

State:

- `path`: current arrow cells (ordered)
- `visited`: `HashSet<Cell>` of cells in `path`
- `best`: longest cycle-free path found so far
- `deadEnds`: count of DFS nodes where no valid neighbor could be taken

Neighbor filtering per step:

- already in `visited`
- out of bounds
- lies on the candidate's head ray (`Board.IsInRay`)
- occupied by an existing arrow (`board.GetArrowAt`)
- would cause a cycle (`WouldCellCauseCycle`)

Early exits:

- `path.Count == targetLength` — exact match found
- `deadEnds >= deadEndLimit` — search budget exhausted

The dead-end limit (default `10`) prevents exhaustive search in dense boards.

Returns `best`, which may be shorter than `targetLength` if the DFS could not reach it within the budget.

## Cycle Detection

### Algorithm: Reachability Set

A candidate arrow's forward dependencies are fixed by its head position and direction (the arrows in its ray). A cycle exists when some existing arrow Y satisfies both:

1. Y is transitively reachable from the candidate's forward deps through the committed `dependsOn` graph (the candidate would depend on Y).
2. Y's ray passes through one of the candidate's cells (Y would depend on the candidate).

The algorithm:

1. **`ComputeForwardDeps(board, head, direction)`** — walk the candidate's ray, collect all existing arrows.
2. **`ComputeReachableSet(board, forwardDeps)`** — BFS from forward deps through `Board._dependsOn`. Returns the full transitive closure.
3. **`WouldCellCauseCycle(board, cell, reachable)`** — for a single cell, iterate `board.Arrows` and check if any arrow whose ray crosses the cell is in the reachable set.

### Key Property: Stateless Per-Cell Checks

The reachability set is fixed for the entire tail DFS — it depends only on the head candidate and the committed graph (which doesn't change during generation of a single arrow). Each cell check is independent: adding a tail cell can only create reverse edges (arrows that depend on the candidate), which point *to* the candidate and don't extend its reachability set. Two cells cannot jointly cause a cycle that neither causes alone.

This means no tentative graph modification, no backtracking state, and no ghost node.

### `IsInRay(Cell target, Cell head, Arrow.Direction direction)`

Public static method on `Board`. Returns whether `target` lies strictly forward of `head` along `direction`. Used for both cycle detection (reverse edge lookup) and tail construction (preventing tail cells from falling on the arrow's own ray).

## Supporting Helpers

### `GetNeighbors(Cell cell)`

Returns the 4 orthogonal neighbors in fixed order (right, left, up, down).

## Performance

Measured in Unity EditMode tests, single-threaded, seed 0.

| Board | len=[2,5] | len=[5,20] | len=[10,50] |
|-------|-----------|------------|-------------|
| 10×10 | <1ms | <1ms | 2ms |
| 20×20 | 1ms | 1ms | 1ms |
| 50×50 | ~56ms | ~30ms | ~20ms |

Key optimisations applied (in order of impact):

1. **Dead-end limit** — DFS capped at `DefaultDeadEndLimit = 10` dead ends per candidate.
2. **Eager candidate pruning** — stale candidates are removed immediately on placement via `_candidateLookup`, now inside `Board.AddArrow`.
3. **Occupancy guard on head/next before cycle check** — avoids computing reachability for candidates whose start cells are already taken.
4. **Reachability set computed once per head candidate** — the BFS runs once, then each tail cell is checked with a simple O(N) scan against the fixed set.

## Loading Progress Heuristic

The loading bar during generation uses arrow count as its progress signal. Arrow placement rate is nearly constant with respect to wall time — unlike candidate depletion or cell fill, which are both front-loaded and stall in the tail.

The estimated total arrows for a board is `w * h * EstimatedArrowDensity`, where `EstimatedArrowDensity = 0.064`. This was derived as follows:

1. **Profiling** (`ProfileDepletionCurve_DumpData` in `PerformanceTests.cs`) sampled arrow count, cell count, and candidate depletion per-arrow across 100×100 and 200×200 boards with multiple seeds.
2. **Observation**: boards consistently fill ~93% of cells with an average arrow length of ~10, giving a theoretical arrow density of `0.93 / 10 = 0.093` arrows per cell.
3. **Tuning**: the constant was reduced from 0.093 to 0.064 so the bar reaches ~99% before generation finishes across board sizes from 100×100 to 400×400. The gap between the theoretical 0.093 and the tuned 0.064 exists because arrows placed early in generation tend to be longer than average (the board is less crowded, so DFS finds longer paths). This means the average arrow length measured across the full run (~10) is lower than the effective average during most of generation, and fewer total arrows end up being placed than the theoretical estimate predicts.

Progress is displayed as `Clamp01(arrowCount / estimatedArrows)`, so slight overestimates cap at 100%.

If generation parameters change significantly (e.g. different `minLength` defaults or dead-end limits), re-run the profiling test and re-tune the constant.

## Design History

The current implementation is a rewrite (`generation-rewrite` branch) of an earlier `BoardGenerator` (sealed class, instance-based). Key decisions made during that history:

- **Original `BoardGenerator`** (pre-rewrite): used `BoardModel`'s `CanPlaceArrow` for legality, recursive DFS+backtracking for tail growth. Correct but very slow on large boards.
- **Optimization pass** (`15c8f90`, external contributor): replaced recursive DFS with bounded stochastic constructive growth. Much faster, but non-exhaustive and relied on magic thresholds.
- **Static class rewrite**: move to a static class with minimal model classes; restore exhaustive DFS+backtracking (now feasible because expensive legality checks are replaced with direct occupancy lookup).
- **Dependency graph refactor** (`board-generation-validation-v2` branch): replaced geometric ray-hopping cycle detection (`DoesArrowCandidateCauseCycle`) with an explicit dependency graph on `Board`. The old algorithm only followed the first hit per ray, missing multi-dependency cycles. The new algorithm builds a reachability set from forward deps and checks each cell against it. Generation cache (`boardCacheDict`) was merged into `Board` to eliminate desync fragility.
