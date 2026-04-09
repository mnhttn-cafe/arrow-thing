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

`Board` maintains two dictionaries and a spatial ray index:

- `_dependsOn[arrow]`: the set of arrows that block `arrow` from being cleared (their cells are in `arrow`'s forward ray).
- `_dependedOnBy[arrow]`: the set of arrows whose forward rays pass through `arrow`'s cells (they depend on `arrow`).
- **Spatial ray index**: four arrays of lists (`_rightHeadsByRow`, `_leftHeadsByRow`, `_upHeadsByCol`, `_downHeadsByCol`) grouping arrow heads by direction and row/column. Enables O(crossing) lookup of arrows whose ray passes through a given cell, replacing O(N) full-arrow scans in both `AddArrow` (reverse dependency computation) and `WouldCellCauseCycle` (cycle detection during generation).

All are updated atomically in `AddArrow` and `RemoveArrow`.

### Maintenance in `AddArrow`

1. Assign a generation index if generation is active (for bitset-based cycle detection).
2. Update occupancy.
3. Walk the new arrow's forward ray — every existing arrow hit is a forward dependency.
4. Use the spatial ray index to find existing arrows whose rays cross the new arrow's cells — these are reverse dependencies. O(crossing) instead of O(N).
5. Update bitset dependency storage (`_depsBitsFlat`) for both forward and reverse deps.
6. Add the new arrow to the ray index.

### Maintenance in `RemoveArrow`

`RemoveArrow` requires `IsClearable(arrow)` — the arrow's `_dependsOn` set is guaranteed empty.

1. Clear occupancy.
2. Remove `arrow`'s (empty) `_dependsOn` entry.
3. For each arrow in `_dependedOnBy[arrow]`: remove `arrow` from their `_dependsOn` set.
4. Remove `arrow`'s `_dependedOnBy` entry.

### `IsClearable`

```csharp
public bool IsClearable(Arrow arrow) => _dependsOn[arrow].Count == 0;
```

O(1). An arrow is clearable when nothing blocks its forward ray.

## Generation Candidate Pool

### Initialization

`Board.InitializeForGeneration()` creates the candidate pool and bitset infrastructure. Called by `FillBoardIncremental` or lazily by `GenerateArrows` if not yet initialized. `CreateInitialArrowHeads` populates the candidate list.

- `_availableArrowHeads`: pool of 2-cell head candidates. Stale candidates (occupied cells) are pruned lazily via swap-and-pop when encountered during `TryGenerateArrow`.
- `_depsBitsFlat`: flat `ulong[]` array storing per-arrow dependency bitsets for O(1) BFS during cycle detection.
- `_bitsetWords`: number of 64-bit words per arrow bitset (`ceil(maxArrows / 64)`).

The candidate pool and bitset storage are only needed during generation. Deserialized/restored boards skip initialization entirely.

### `ArrowHeadData`

- `head`: candidate `Cells[0]`.
- `next`: candidate `Cells[1]`.
- `direction`: candidate head direction.

### Head Candidate Enumeration

`Board.CreateInitialArrowHeads()` precomputes all valid adjacent `(head, next)` pairs for all 4 directions. These are geometric candidates only — filtered later by occupancy checks, cycle detection, and tail length.

Y-up coordinate convention: `Direction.Up → dy = +1`, `Direction.Down → dy = -1`.

## Public Entry Points

### `FillBoardIncremental(Board board, int maxLength, Random random)`

- Coroutine (returns `IEnumerator`). Yields once per arrow placed, allowing the caller to drive frame budgeting.
- Uses the **Burst-compiled path**: allocates a `NativeGenerationState`, calls `NativeGeneration.TryGenerateArrow` per candidate, extracts cells from scratch buffers to create managed `Arrow` objects.
- After the generation loop, `Board.InitializeFromNativeGeneration` copies native bitset/occupancy/ray-index data back into Board's managed arrays for compaction. The `NativeGenerationState` is disposed in a `finally` block (safe against cancellation).
- Yields `CompactionMarker`, then runs post-process compaction (see below), yielding per merge.
- Yields `FinalizationMarker`, then yields during `FinalizeGenerationIncremental` (builds HashSet dependency graph incrementally).
- Used by `GameController.GenerateBoard` for incremental board display during generation.
- RNG: `Unity.Mathematics.Random` (seeded from the `System.Random` parameter). Seed-to-board mapping differs from the pre-Burst managed path.

### `GenerateArrows(Board board, int maxLength, int amount, Random random, out int createdArrows)`

Flow:

1. Ensure candidate pool is initialized.
2. Repeatedly call `TryGenerateArrow(...)` until `createdArrows == amount` or candidates are exhausted.
3. On each successful arrow, call `board.AddArrow(arrow)` which atomically updates occupancy, dependency graph, and candidate pool.
4. Return `createdArrows == amount`.

Note: `GenerateArrows` uses the **managed path** (not Burst): `AddArrow` (full path with HashSet deps), not `AddArrowForGeneration`. Used by tests and for adding arrows to an existing board.

## Single Arrow Construction

### `TryGenerateArrow(...)`

1. Sample one target length in `[MinArrowLength, maxLength]` (where `MinArrowLength = 2`).
2. While head candidates remain:
   - pick a random candidate from `_availableArrowHeads`
   - reject and swap-remove if head or next cell is occupied
   - compute forward deps; if any exist, run cycle detection (see below)
   - reject and swap-remove if head or next cell would cause a cycle
   - call `GreedyWalk(...)` to grow the body
   - reject and swap-remove candidate if resulting tail is shorter than `MinArrowLength`
   - otherwise return the arrow
3. Return false if all candidates are exhausted.

### `GreedyWalk(...)`

Linear-time random walk from the fixed 2-cell start `[head, next]`. No backtracking.

State (pooled in `GenerationContext` to avoid per-candidate allocation):

- `path`: current arrow cells (ordered)
- `visited`: `bool[,]` grid — pre-marked with path cells AND the candidate's forward ray cells (eliminates per-step `IsInRay` checks)
- `dirOrder`: shuffled direction indices for randomized neighbor selection

Per step: shuffle 4 directions, take the first valid neighbor (in bounds, not visited, not occupied, not cycle-causing), advance. Stop when `targetLength` is reached or no valid neighbor exists.

Returns `path`, which may be shorter than `targetLength` if the walk hits a dead end. Unlike the earlier DFS+backtracking approach, the greedy walk does not search for longer alternatives — it accepts whatever path length results from a single forward pass. This trades fill optimality for dramatically faster generation (O(targetLength) per arrow instead of O(4^d) worst-case DFS).

## Cycle Detection

### Algorithm: Reachability Set

A candidate arrow's forward dependencies are fixed by its head position and direction (the arrows in its ray). A cycle exists when some existing arrow Y satisfies both:

1. Y is transitively reachable from the candidate's forward deps through the committed `dependsOn` graph (the candidate would depend on Y).
2. Y's ray passes through one of the candidate's cells (Y would depend on the candidate).

The algorithm:

1. **`ComputeForwardDeps(board, head, direction, depsBitset)`** — walk the candidate's ray, collect all existing arrows into a `ulong[]` bitset. Returns the count. If count is 0, cycle detection is skipped entirely (no deps = no cycle possible).
2. **`ComputeReachableSetEarlyAbort(board, startBits, reachable, head, next, ctx)`** — BFS from forward deps through `Board._depsBitsFlat` using bitset operations. Each BFS step processes 64 arrows per word via bitwise OR. Instead of computing the full transitive closure and then checking for cycles, each newly discovered arrow is immediately checked via `IsInRayOf` against the candidate's head and next cells using flat geometry arrays (`_genHeadX`, `_genHeadY`, `_genDir`). Returns `true` (cycle) as soon as any reachable arrow's ray crosses head or next, aborting the BFS early. If the BFS completes without finding a cycle, the full reachable set is available in `reachable` for use during tail construction.
3. **`IsInRayOf(ax, ay, dir, cx, cy)`** — inline ray geometry check using integer coordinates from the flat arrays. Returns whether cell `(cx, cy)` is in the forward ray of an arrow at `(ax, ay)` facing `dir`. Used by `ComputeReachableSetEarlyAbort` for per-arrow cycle checks during BFS.

### Key Property: Stateless Per-Cell Checks

The reachability set is fixed for the entire greedy walk — it depends only on the head candidate and the committed graph (which doesn't change during generation of a single arrow). Each cell check is independent: adding a tail cell can only create reverse edges (arrows that depend on the candidate), which point *to* the candidate and don't extend its reachability set. Two cells cannot jointly cause a cycle that neither causes alone.

This means no tentative graph modification, no walk state beyond the current path, and no ghost node.

### `IsInRay(Cell target, Cell head, Arrow.Direction direction)`

Public static method on `Board`. Returns whether `target` lies strictly forward of `head` along `direction`. Used for both cycle detection (reverse edge lookup) and tail construction (preventing tail cells from falling on the arrow's own ray).

## Post-Process Compaction

After all arrows are placed and before finalization, a compaction pass merges redundant trivial chains to improve board quality.

### Problem

The greedy walk produces many short (length-2) arrows that point in the same direction and are collinear and adjacent. These "trivial chains" add visual clutter without adding puzzle depth — the player clears them in a fixed sequence with no choice involved.

### Algorithm (`CompactBoardInPlace`)

Iterative merge passes over the arrow list:

1. For each arrow (the "dependent"), walk its forward ray looking for an adjacent collinear same-direction arrow (the "blocker") whose tail is exactly one cell from the dependent's head.
2. If found and both arrows are still alive (`_generationIndex >= 0`), merge them: concatenate blocker's cells then dependent's cells into a single arrow.
3. Remove both originals via `RemoveArrowForGeneration` (zeros bitset row, clears occupancy/ray index, marks generation index dead) and add the merged arrow via `AddArrowForGeneration`.
4. Repeat passes until no merges are found.

The compactor yields after each merge for smooth loading progress. Arrows marked dead (`_generationIndex < 0`) are skipped within a pass.

### Why Post-Process

Inline compaction (merging during generation, feeding merged arrows back into cycle detection) was benchmarked and found to produce identical results to post-process compaction — merged arrows don't change the candidate pool or cycle detection outcomes since the merged pair was already in a fixed dependency order. Post-process is simpler: it runs after generation is complete, doesn't interleave with the bitset infrastructure, and integrates cleanly into the loading progress model as a distinct phase.

### Impact

Compaction typically merges 15–25% of arrows on standard boards, reducing trivial chain count without affecting solvability (merged arrows preserve the same dependency relationships). The DAG depth is unchanged since merged pairs were already in a fixed dependency order.

## Performance

Measured via `GenerationProfiler.cs` (PlayMode), single-threaded. These numbers predate the compaction pass; compaction adds negligible overhead (a few merge iterations on the already-placed arrow set).

| Board | Unity (PlayMode, incl. ArrowView) |
|-------|-----------------------------------|
| 200×200 | ~2.7s |
| 400×400 | ~68s |

Unity overhead is ~3–4x vs. raw .NET due to ArrowView creation (~38% of work time) and frame yields/rendering (~30% of wall time). The domain algorithm itself accounts for ~62% of work time.

Key optimisations applied (in order of impact):

1. **Burst compilation** — the generation hot path (`NativeGeneration.TryGenerateArrow` and callees) runs as Burst-compiled native code via `[BurstCompile]` static methods on `NativeGeneration`. On WebGL this compiles to WASM SIMD; on desktop it uses SSE/AVX. All generation-time state is held in `NativeGenerationState` (NativeArrays) for Burst compatibility. The managed coroutine calls Burst per-candidate and yields per-arrow for progress reporting. `math.tzcnt` replaces the hand-rolled De Bruijn CTZ with a single hardware instruction.
2. **Bitset-based reachability with early abort** — dependency graph stored as flat `ulong[]` bitsets (`_depsBitsFlat`). BFS processes 64 arrows per word via bitwise OR, replacing `HashSet<Arrow>` iteration with cache-friendly sequential memory access. Membership tests are single bit-checks instead of hash lookups. Cycle detection is integrated into the BFS via `ComputeReachableSetEarlyAbort`: each newly discovered arrow is immediately checked via flat geometry arrays (`genHeadX`, `genHeadY`, `genDir`), aborting early if a cycle is found rather than computing the full transitive closure first.
3. **Greedy walk** — linear-time random walk replaces DFS+backtracking for tail construction. O(targetLength) per arrow instead of O(4^d) worst-case. Pre-marks ray cells in `visited` grid to eliminate per-step `IsInRay` checks.
4. **Spatial ray index** — per-row/per-column flat arrays of arrow head indices grouped by direction. In the Burst path, stored as `NativeArray<int>` with per-row/col counts. Replaces O(N) full-arrow scans in cycle detection and reverse-dep computation with O(crossing) lookups.
5. **Allocation pooling** — `NativeGenerationState` holds all working arrays (bitsets, frontier, visited grid, path scratch, direction shuffle). Allocated once at generation start, disposed in `finally` block. Zero per-candidate allocations.
6. **Swap-and-pop candidate removal** — `NativeList<T>.RemoveAtSwapBack` for O(1) candidate pruning. Stale candidates are pruned lazily when encountered.
7. **Empty-deps fast path** — when a candidate's forward ray is clear (common for edge-pointing arrows), the entire reachability computation and cycle check is skipped.
8. **Leaf-deps fast path** — when all forward deps have no deps of their own (`hasAnyDeps` flag), the BFS is skipped and reachable set is just the forward deps.
9. **Reachability set computed once per head candidate** — the BFS runs once, then each tail cell is checked against the fixed bitset via the ray index.
10. **Generation-only fast path** — Burst path skips HashSet dependency tracking during generation; `FinalizeGenerationIncremental` builds it in one pass afterward.
11. **O(1) arrow membership** — `HashSet<Arrow> _arrowSet` alongside the `List<Arrow>` for O(1) `Contains` checks in `AddArrow`/`RemoveArrow` validation.

## Loading Progress Heuristic

The loading bar uses a three-phase model to approximate wall-time progress:

### Phase 1: Generation (0% → ~90%)

```
rawProgress = Clamp01(arrowCount / (w * h * EstimatedArrowDensity))
displayProgress = genEndProgress * pow(rawProgress, progressExponent)
```

`EstimatedArrowDensity = 0.16`, `progressExponent = 1.5`, `genEndProgress = 0.90`. The power curve compensates for the nonlinear relationship between arrow count and wall time: early arrows are placed quickly (sparse board, few candidate rejections) while late arrows are slow (dense board, many cycle-detection rejections). The density and exponent constants were experimentally derived on boards 100×100 and up (where loading screens are visible long enough to matter) using a standalone .NET benchmark during the optimization pass.

### Phase 2: Compaction (~90% → 95%)

At `CompactionMarker`, the actual progress is captured as `genFinalProgress` (avoids a visual jump if generation finished below 90%). Compaction progress interpolates linearly from `genFinalProgress` to `compactEndProgress = 0.95` based on the ratio of merges yielded so far.

### Phase 3: Finalization (95% → 100%)

After `FinalizationMarker`, finalization builds the HashSet dependency graph incrementally. Progress interpolates linearly from 95% to 100% based on arrows finalized.

### Phase Transitions

`FillBoardIncremental` yields `GenerationPhase` enum values between phases:

- `GenerationPhase.Compacting` — between generation and compaction
- `GenerationPhase.Finalizing` — between compaction and finalization

`GameController` detects these via `is GenerationPhase` pattern matching to switch progress phase, rebuild ArrowViews after compaction (compacted arrows replace the originals), and transition the progress bar smoothly.

If generation parameters change significantly (e.g. different `minLength` defaults or dead-end limits), re-run the profiling test and re-tune the constants.

## Design History

The current implementation is a rewrite (`generation-rewrite` branch) of an earlier `BoardGenerator` (sealed class, instance-based). Key decisions made during that history:

- **Original `BoardGenerator`** (pre-rewrite): used `BoardModel`'s `CanPlaceArrow` for legality, recursive DFS+backtracking for tail growth. Correct but very slow on large boards.
- **Optimization pass** (`15c8f90`, external contributor): replaced recursive DFS with bounded stochastic constructive growth. Much faster, but non-exhaustive and relied on magic thresholds.
- **Static class rewrite**: move to a static class with minimal model classes; restore exhaustive DFS+backtracking (now feasible because expensive legality checks are replaced with direct occupancy lookup).
- **Dependency graph refactor** (`board-generation-validation-v2` branch): replaced geometric ray-hopping cycle detection (`DoesArrowCandidateCauseCycle`) with an explicit dependency graph on `Board`. The old algorithm only followed the first hit per ray, missing multi-dependency cycles. The new algorithm builds a reachability set from forward deps and checks each cell against it. Generation cache (`boardCacheDict`) was merged into `Board` to eliminate desync fragility.
- **Spatial ray index** (`optimize-domain-performance` branch): added per-row/per-column arrow head lists grouped by direction. Replaced O(N) scans in `WouldCellCauseCycle` and `AddArrow` reverse-dep computation with O(crossing) lookups. Also added `HashSet<Arrow>` for O(1) membership checks and reduced DFS allocation overhead.
- **Bitset-based cycle detection** (`optimize-board-generation` branch): replaced `HashSet<Arrow>` reachability with `ulong[]` bitsets for all generation-time set operations. BFS through dependency graph now processes 64 arrows per word via bitwise OR. Added `GenerationContext` to pool all per-candidate allocations (bitsets, BFS queue, DFS path/visited). Switched candidate removal from O(N) `List.RemoveAt`/`RemoveAll` to O(1) swap-and-pop with lazy pruning. Replaced `HashSet<Cell>` DFS visited tracking with `bool[,]` grid. Added empty-forward-deps fast path to skip cycle detection entirely for edge-pointing candidates.
- **Early-abort BFS** (`optimize-domain-performance` branch): integrated cycle detection into the BFS itself via `ComputeReachableSetEarlyAbort`. Each newly discovered arrow is immediately checked against the candidate's head and next cells using flat geometry arrays (`_genHeadX`, `_genHeadY`, `_genDir`), returning as soon as a cycle is found instead of computing the full transitive closure first. Provides ~2x speedup on large boards where cycles are frequently attempted.
- **Generation algorithm benchmarking** (`board-generation-optimization` branch): explored alternative generation strategies to improve board quality (arrow density, DAG depth, trivial chain ratio). Alternatives benchmarked: **ranked generation** (topological ordering — 10x faster but shallower DAGs), **layered generation** (depth-first layer filling), **repair generation** (flip-based cycle repair for density), **ranked-hybrid** (ranked with depth recovery passes), and **inline compaction** (merging during generation with bitset feedback). The original depth-prioritizing constructive algorithm consistently produced the deepest DAGs and best puzzle quality. Parallelization of candidate evaluation was tested and found 60% slower due to batch overhead and wasted work (N candidates evaluated, 1 used). Parallel post-processing (finalization, compaction scan) gave only 9–39% speedups — not worth the complexity for a once-per-session operation. Final decision: keep the original algorithm with a post-process compaction pass for trivial chain reduction. The standalone benchmark project (`generation-benchmark/`) was removed after benchmarking concluded.
