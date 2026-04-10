# Burst-Compile Board Generation

## Context

Board generation is the main CPU bottleneck. The hot path (`TryGenerateArrow` loop with cycle detection BFS and greedy walk) already operates on flat `ulong[]` bitsets, `int[]` geometry arrays, and `bool[,]` grids -- the data layout is Burst-ready, but the code runs as managed C# through IL2CPP on WebGL. Burst compiles to WASM SIMD, giving 3-5x on the bitwise BFS that dominates large-board generation.

Current profiled numbers (200x200 ~2.7s, 400x400 ~68s domain-only in PlayMode) would drop to sub-second and ~15s respectively with Burst, making boards up to ~100x100 feel instant.

## Design

### Approach: IJob wrapping the generation loop

Create a `GenerationJob : IJob` that runs the full `TryGenerateArrow` loop on native data, then convert results back to managed `Arrow`/`Board` objects. Compaction and finalization remain managed (they're post-processing, not hot).

**Why IJob, not Burst function pointers:** The hot path is the entire generate-arrow loop (candidate selection -> cycle detection -> greedy walk -> placement), not individual functions. A single IJob avoids managed/native transition overhead per candidate.

**Why not IJobParallelFor:** Prior benchmarking (documented in `BoardGeneration.md` Design History) found parallel candidate evaluation 60% *slower* due to batch overhead and wasted work. One arrow is placed per iteration; candidates are invalidated by placement.

### Data layout: NativeGenerationState

A disposable struct holding all generation-time state as NativeArrays. Mirrors the flat arrays already on `Board` (`_depsBitsFlat`, `_genHeadX`, etc.) plus the occupancy grid as `NativeArray<int>` (gen-index, -1 = empty) instead of `Arrow[,]`.

Spatial ray index becomes four flat arrays with per-row/col counts:
- `rightByRow`: `NativeArray<int>` of size `height * width`, row-major. `rightByRow[row * width + i]` = genIndex of the i-th right-facing arrow head in that row.
- `rightByRowCount`: `NativeArray<int>` of size `height`.
- Same pattern for left/up/down.

Max capacity per row = `width` (for horizontal directions) or `height` (for vertical). These bounds are tight.

Arrow output stored as flat cells:
- `cellsX`, `cellsY`: `NativeArray<int>`, total capacity = `width * height` (upper bound on all cells).
- `arrowStart`, `arrowLength`, `arrowDir`: `NativeArray<int>`, capacity = `maxArrows`.
- `arrowCount`: `NativeReference<int>` (or `NativeArray<int>` length 1).

RNG: `Unity.Mathematics.Random` (uint seed). Deterministic, same sequence as `System.Random` is NOT required -- seed-to-board mapping will change. Existing replays carry snapshots so they don't need to regenerate.

### Generation flow change

**Before (managed coroutine):**
```
FillBoardIncremental:
  InitializeForGeneration
  loop { TryGenerateArrow -> AddArrowForGeneration -> yield null }   // per-arrow yield
  yield Compacting
  CompactBoardInPlace (yields per merge)
  yield Finalizing
  FinalizeGenerationIncremental (yields per arrow)
```

**After (Burst job + managed post-processing):**
```
FillBoardIncremental:
  yield Generating                             // signal phase start
  allocate NativeGenerationState
  GenerationJob.Run()                          // Burst, synchronous, no yields
  convert native output -> Arrow objects
  add arrows to Board via bulk restore         // populates occupancy + ray index
  dispose native state
  yield Compacting
  CompactBoardInPlace (yields per merge)       // unchanged
  yield Finalizing
  FinalizeGenerationIncremental (yields per arrow)  // unchanged
```

Per-arrow progress reporting during generation goes away (the phase is now near-instant). The progress model shifts:
- Generation: 0% -> instant (one frame)
- Compaction: 0% -> 90% (bulk of remaining time is finalization)
- Finalization: 90% -> 100%

The GameController progress constants (`genEndProgress`, etc.) are adjusted to match. The per-arrow `AddArrowView` during generation is replaced with batch view creation after the job completes (same pattern as the existing post-compaction view rebuild).

### Callers affected

| Caller | Current API | Change |
|--------|------------|--------|
| GameController.GenerateBoard | FillBoardIncremental, per-arrow yields | No per-arrow yields during generation phase. Batch ArrowView creation after job. Progress constants shift. |
| ReplayVerifier.Verify | FillBoardIncremental, sync drain | No change (still drains to completion). |
| ReplayViewController | FillBoardIncremental, frame-budget yield | No change (still yields on non-phase values; generation phase produces none). |
| TestBoardHelper.FillBoard | FillBoardIncremental, sync drain | No change. |
| GenerationTests (GenerateArrows) | GenerateArrows sync API | GenerateArrows updated to use Burst job internally. |
| PerformanceTests (ProfileDepletionCurve) | Per-yield candidate tracking | Rework to read from NativeGenerationState output or remove (superseded by Burst timing). |

### Seed determinism

`Unity.Mathematics.Random` uses a different algorithm than `System.Random`. Board layout for a given seed WILL change. This is acceptable because:
1. Replay playback uses snapshots (not seed regeneration) for top-50 entries.
2. Non-top-50 global replays regenerate from seed -- these will produce different boards. The `ReplayViewController` already handles this gracefully (regenerates and saves the snapshot locally).
3. Existing local replays carry full snapshots.
4. There is no seed-to-board contract in the API or storage.

### Ctz64 replacement

The hand-rolled De Bruijn `Ctz64` is replaced by `math.tzcnt(ulong)` from Unity.Mathematics, which Burst lowers to a single hardware `TZCNT` instruction (or WASM `i64.ctz`).

## Implementation plan

### Step 0: Package setup
- [ ] Add `com.unity.burst`, `com.unity.collections`, `com.unity.mathematics` to `Packages/manifest.json`.
- [ ] Remove `com.unity.testtools.codecoverage` from `Packages/manifest.json`.
- [ ] Add `Unity.Burst`, `Unity.Collections`, `Unity.Mathematics` to `ArrowThing.asmdef` references.
- [ ] Set `allowUnsafeCode: true` in `ArrowThing.asmdef` (required by Collections NativeArray).

### Step 1: NativeGenerationState
- [ ] Create `Assets/Scripts/Domain/Generation/NativeGenerationState.cs` -- disposable struct holding all NativeArrays for one generation run.
- [ ] Constructor takes `(int width, int height, int maxArrows, int bitsetWords, Allocator)`.
- [ ] `InitializeCandidates(int width, int height)` -- populates candidate NativeList (same logic as `Board.CreateInitialArrowHeads`).
- [ ] `Dispose()` frees all native memory.

### Step 2: GenerationJob
- [ ] Create `Assets/Scripts/Domain/Generation/GenerationJob.cs` -- `[BurstCompile] struct GenerationJob : IJob`.
- [ ] Port `TryGenerateArrow` loop: candidate selection, `ComputeForwardDeps`, `ComputeReachableSetEarlyAbort`, `GreedyWalk`, arrow placement. All operating on NativeGenerationState fields.
- [ ] Port `AnyArrowWithRayThroughBitset` using flat spatial ray index arrays.
- [ ] Port `SetDepBit` and nonzero-word tracking.
- [ ] Port `AddArrowForGeneration` equivalent (update occupancy, ray index, bitsets, output cells).
- [ ] Use `Unity.Mathematics.Random` for RNG, `math.tzcnt` for CTZ.

### Step 3: Wire into BoardGeneration
- [ ] Add `Board.ApplyNativeGenerationOutput(NativeGenerationState)` -- creates Arrow objects from flat cell arrays, populates Board's managed state (occupancy, ray index, arrow list). Does NOT build dependency graph (that's finalization).
- [ ] Update `FillBoardIncremental` to allocate NativeGenerationState, run GenerationJob, call ApplyNativeGenerationOutput, then proceed to compaction/finalization as before.
- [ ] Update `GenerateArrows` sync API similarly (or have it call FillBoardIncremental internally).
- [ ] Keep existing managed `CompactBoardInPlace` and `FinalizeGenerationIncremental` unchanged.

### Step 4: Update callers
- [ ] Update `GameController.GenerateBoard` progress model: generation is instant, shift weight to compaction + finalization. Batch ArrowView creation after generation completes (replaces per-arrow spawn).
- [ ] Update `PerformanceTests.ProfileDepletionCurve_DumpData` if it relies on per-arrow yields during generation.

### Step 5: Tests
- [ ] Verify all existing EditMode tests pass (determinism tests will need updated expected values since RNG changed).
- [ ] Verify solvability tests pass across all seed ranges.
- [ ] Add a test that generation output matches between `.Run()` (Burst) and a managed reference (optional, for validation).
- [ ] Run PlayMode layout tests.

### Step 6: Cleanup
- [ ] Remove `GenerationContext` class, `Ctz64`, `DeBruijn64Tab` from `BoardGeneration.cs` (dead code after Burst port).
- [ ] Update `docs/BoardGeneration.md` with Burst details.
- [ ] Update `docs/TechnicalDesign.md` with new types.
- [ ] Delete this file.

## Open questions (resolved)

**Q: Does Burst work on WebGL?**
A: Yes. Burst compiles to WASM with optional SIMD. Supported since Burst 1.5+.

**Q: Does NativeArray work in EditMode tests?**
A: Yes, with `Allocator.Temp` or `Allocator.TempJob`. Tests must dispose native containers.

**Q: Will the `GenerateArrows` sync API still work for the 2 tests that use it?**
A: Yes. It will internally allocate native state, run the job with `.Run()`, and convert results. Same sync contract, faster execution.
