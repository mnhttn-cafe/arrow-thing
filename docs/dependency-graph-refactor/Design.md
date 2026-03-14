# Dependency Graph Refactor — Design Document

## The Bug

### Counterexample

On a 5-wide board with Y-up coordinates:

```
A = Arrow[(2,0), (2,1)]  — head (2,0), faces Down, ray goes to (2,-1) → off board
B = Arrow[(3,0), (4,0)]  — head (3,0), faces Left,  ray goes (2,0), (1,0), (0,0)
C = Arrow[(1,0), (0,0)]  — head (1,0), faces Right, ray goes (2,0), (3,0), (4,0)
```

Current cycle check for C: walk C's ray Right from (1,0). Hit A at (2,0). Jump to A's head (2,0) facing Down. Ray exits board immediately. **No cycle detected — C is accepted.**

But after clearing A:
- B's ray Left hits C at (1,0). B depends on C.
- C's ray Right hits B at (3,0). C depends on B.
- **Deadlock.** B and C form a 2-cycle that was invisible while A existed.

### Root Cause

`DoesArrowCandidateCauseCycle` follows a **single chain** of "first hit" arrows. When it hits an arrow, it jumps to that arrow's head and continues in that arrow's direction. This models a chain of pairwise blockages but **ignores all arrows beyond the first hit in each ray**.

C's ray passes through cells of **both** A and B, but the algorithm only follows A (the first hit). It never registers that C also depends on B. The dependency C→B is silently dropped, and the B→C dependency (B's ray passes through C's cells) creates an undetected cycle.

The fundamental flaw: **an arrow depends on every arrow whose cells appear anywhere in its forward ray, not just the first one.** The "hop to the first hit's head" strategy cannot represent multi-dependency relationships.

## Correct Model

### Dependency Definition

Arrow X **depends on** arrow Y if any cell of Y lies in X's forward ray (the half-line from X's head in X's facing direction, extending to the board boundary). This means Y blocks X — X cannot be cleared until Y is removed first.

An arrow with **no dependencies** is immediately clearable (its ray is empty of other arrows' cells).

A board is **solvable** if and only if the dependency graph is a **DAG** (directed acyclic graph). A topological ordering of the DAG gives a valid clear sequence.

### Why All Ray Intersections Matter

Consider arrow X whose ray passes through cells of arrows A, B, C (in that order along the ray). X cannot be cleared until A, B, and C are all gone. If we only track X→A, we miss the constraint that B and C must also be cleared first. Those missed edges can form cycles that go undetected.

## Algorithm Design

### Dependency Graph Structure

Maintain an explicit directed graph alongside the board:

```
dependsOn:     Dictionary<Arrow, HashSet<Arrow>>   // X → {arrows that block X}
dependedOnBy:  Dictionary<Arrow, HashSet<Arrow>>   // X → {arrows that X blocks}
```

Both directions are stored for efficient updates. `dependsOn[X]` is the set of arrows in X's ray. `dependedOnBy[X]` is the set of arrows whose rays pass through X's cells.

### Computing Dependencies for a New Arrow

When considering adding arrow C to a board with existing arrows:

**Step 1 — Forward edges (C depends on ?):**
Walk C's forward ray. Every existing arrow with a cell in that ray → add to `dependsOn[C]`.

**Step 2 — Reverse edges (? depends on C):**
For each existing arrow A, check if any of C's cells lie on A's forward ray. If yes → add C to `dependsOn[A]`.

For step 2, checking "does cell P lie on arrow A's ray?" is a simple geometric test:

```
IsInRay(P, A.Head, A.Direction):
  Right: P.Y == A.Head.Y && P.X > A.Head.X
  Left:  P.Y == A.Head.Y && P.X < A.Head.X
  Up:    P.X == A.Head.X && P.Y > A.Head.Y
  Down:  P.X == A.Head.X && P.Y < A.Head.Y
```

The `IsInRay` function already exists in `BoardGeneration`.

### Cycle Detection via Reachability Set

**Key property:** If the graph was a DAG before adding C, any new cycle **must include C.** A cycle through C has the form: C → X → ... → Y → C, where the first hop is a forward edge (C depends on X, X is in C's ray) and the last hop is a reverse edge (Y depends on C, C's cell is in Y's ray).

This means: **a cycle exists iff any arrow reachable from C's forward deps (through committed `dependsOn` edges) is also an arrow whose ray crosses one of C's cells.**

Algorithm (implemented in `BoardGeneration`):
1. Compute forward deps F: walk C's ray, collect all existing arrows hit.
2. Compute reachability set R: BFS from F through the committed `dependsOn` graph. R contains all arrows that C would transitively depend on.
3. For each cell of C: check if any existing arrow whose ray crosses that cell is in R. If yes → cycle.

No graph modification is needed during tentative checks. The committed graph on `Board` is never touched until `AddArrow` is called with the finalized arrow.

### Integration with Tail DFS

The reachability set R is **fixed for the entire tail DFS** — it depends only on the candidate's forward deps (determined by the head position/direction) and the committed graph (which doesn't change during generation of a single arrow).

Each new tail cell check is therefore stateless and independent:

1. For the new cell, iterate existing arrows and test `IsInRay(cell, arrow.Head, arrow.Direction)`.
2. If any matching arrow is in R → reject cell.
3. No tentative edges, no backtracking cleanup, no ghost node.

This works because adding more tail cells can only add reverse edges (arrows that depend on the candidate), not forward edges. Reverse edges point *to* the candidate, so they don't extend the reachability set *from* the candidate. Each cell check is truly independent — two cells can't jointly cause a cycle that neither causes alone.

### Optimization: Row/Column Index for Reverse Edge Lookup

The naive approach to step 2 (checking all existing arrows) is O(N) per cell added. For small N this is fine. But we can do better with a spatial index:

For each **row** y, maintain two sorted lists:
- Right-facing arrow heads with head.Y == y (sorted by head.X ascending)
- Left-facing arrow heads with head.Y == y (sorted by head.X ascending)

For each **column** x, maintain two sorted lists:
- Up-facing arrow heads with head.X == x (sorted by head.Y ascending)
- Down-facing arrow heads with head.X == x (sorted by head.Y ascending)

To find "which arrows' rays cross cell (cx, cy)":
- Row cy, right-facing: heads with X < cx (their ray extends right past cx)
- Row cy, left-facing: heads with X > cx (their ray extends left past cx)
- Column cx, up-facing: heads with Y < cy
- Column cx, down-facing: heads with Y > cy

With sorted lists, each lookup is O(log N) via binary search to count how many qualify, or O(k) to enumerate the k results. Since we need the actual arrows (not just counts), it's O(k) per cell.

**Whether this index is worth building depends on board density.** For boards up to 50×50 with ~200 arrows, the naive O(N) scan per cell is fast enough (microseconds). The index becomes valuable only if generation scales to much larger boards or if profiling shows this as a bottleneck. **Start with the naive approach.**

## Data Structures

### Merging the Cache into `Board`

The current `BoardCacheData` lives in a static dictionary keyed by `Board` reference, with a `version` field to detect external mutation. This is fragile — any desync is unrecoverable, and the cache is invisible to code outside `BoardGeneration`.

The dependency graph needs to survive beyond generation (garbage arrows must be validated against it too), which means the cache-as-external-dict model doesn't work. The fix is to move all generation-relevant state into `Board` itself:

**Fields moving from `BoardCacheData` to `Board`:**

```csharp
// Dependency graph
Dictionary<Arrow, HashSet<Arrow>> _dependsOn;      // X → {arrows blocking X}
Dictionary<Arrow, HashSet<Arrow>> _dependedOnBy;    // X → {arrows X blocks}

// Generation candidate pool
List<ArrowHeadData> _availableArrowHeads;
List<ArrowHeadData>[,] _candidateLookup;
```

**Why this eliminates the desync problem:** `AddArrow` and `RemoveArrow` already maintain `_occupancy`. They can also maintain the dependency graph and candidate pool atomically in the same methods. There's no separate cache to drift out of sync — the board *is* the single source of truth.

**What happens to `BoardGeneration`:** It becomes purely algorithmic — DFS, tail construction, cycle checking — with no owned state. It reads and writes `Board` fields. The static `boardCacheDict` is deleted entirely.

**Initialization:** `Board` exposes an `InitializeForGeneration()` method that `BoardGeneration.FillBoard` calls. This avoids paying setup cost for boards that don't need generation — deserialized boards arriving via netcode already have their arrows and dependency graph, and never need the candidate pool. The dependency graph itself (`_dependsOn`, `_dependedOnBy`) is always initialized (needed for `IsClearable` and garbage validation), but the candidate pool (`_availableArrowHeads`, `_candidateLookup`) is only created on `InitializeForGeneration()`.

### Dependency Graph on `Board`

```csharp
Dictionary<Arrow, HashSet<Arrow>> _dependsOn;
Dictionary<Arrow, HashSet<Arrow>> _dependedOnBy;
```

Maintained atomically in `AddArrow`/`RemoveArrow`:

- **`AddArrow(arrow)`:** Walk arrow's ray → populate `_dependsOn[arrow]`. For each existing arrow, check if arrow's cells lie on its ray → update `_dependsOn[existingArrow]` and `_dependedOnBy[arrow]`. Update `_occupancy`. Prune candidates.
- **`RemoveArrow(arrow)`:** Remove arrow from all `_dependsOn` / `_dependedOnBy` entries. Clear `_occupancy`. (Candidate pool is not restored — removed arrows don't re-open candidates during gameplay.)

This also makes `IsClearable` trivial:

```csharp
public bool IsClearable(Arrow arrow) => _dependsOn[arrow].Count == 0;
```

O(1) instead of the current O(R) ray walk.

### What Changes in Cycle Detection

Replace `DoesArrowCandidateCauseCycle` with three stateless methods on `BoardGeneration`:

**`ComputeForwardDeps(board, head, direction)`** — walks the candidate's ray, returns all existing arrows hit.

**`ComputeReachableSet(board, forwardDeps)`** — BFS from forward deps through committed `Board._dependsOn`. Returns the full transitive closure.

**`WouldCellCauseCycle(board, cell, reachable)`** — iterates `board.Arrows`, checks if any arrow whose ray crosses `cell` is in the reachable set. Returns true if found.

In `TryGenerateArrow`: compute forward deps and reachable set once per head candidate. Check head+next cells. In `CompleteArrowTail`: check each new tail cell against the same reachable set. No ghost node, no tentative edges, no backtracking state.

### Graph Maintenance on Arrow Placement

When an arrow is finalized and `Board.AddArrow` is called, the dependency graph is computed from scratch for that arrow (forward deps by ray walk, reverse deps by `IsInRay` scan). This is independent of the tentative cycle checks — `AddArrow` always computes the correct edges from the current board state. No transfer from temporary state needed.

## Complexity Analysis

Variables:
- **N** = number of arrows on board
- **R** = average ray length (bounded by max board dimension, typically ≤50)
- **L** = average arrow length in cells
- **E** = total edges in dependency graph

### Per-Arrow Addition

| Operation | Cost | When |
|-----------|------|------|
| Walk candidate's ray (forward edges) | O(R) | Once per head candidate |
| BFS reachability from forward deps | O(N + E) | Once per head candidate |
| Per-cell cycle check (IsInRay scan) | O(N) | Per DFS step |
| **Total per arrow (with T tail DFS steps)** | **O(R + N + E + T × N)** | |

### Full Board Fill (N arrows)

With T bounded by dead-end budget (~10 steps per arrow), N ≈ 200, E ≈ N (sparse):
O(N × T × (N + E)) ≈ 200 × 10 × 400 ≈ 800K operations. **Sub-millisecond on modern hardware.**

Note: the per-cell reverse edge check is O(N) — iterate existing arrows and test `IsInRay` for one cell. This replaces the old O(N × L) full-body scan, since we only check a single cell at a time.

### Comparison to Current Approach

The current geometric ray-hopping is O(R × chain_length) per check, where chain_length is the number of arrows visited before the ray escapes. In practice this is fast but **incorrect**. The graph-based approach is slightly more expensive per check but bounded by the same small constants, and critically, **it's correct**.

## Integration Plan

### What Gets Replaced

1. **`DoesArrowCandidateCauseCycle`** — entire method. Replaced by graph-based cycle check.
2. **`BoardCacheData`** and **`boardCacheDict`** — deleted. State moves into `Board`.
3. **`Board.IsClearable` ray walk** — replaced by O(1) dependency set check.

### What Gets Added to `Board`

1. **Dependency graph** (`_dependsOn`, `_dependedOnBy`) — maintained in `AddArrow`/`RemoveArrow`.
2. **Generation candidate pool** (`_availableArrowHeads`, `_candidateLookup`) — initialized via `InitializeForGeneration()`, pruned in `AddArrow`.
3. **`IsInRay`** — public static helper, moved from `BoardGeneration`.
4. **`GetDependencies`** — internal accessor for `_dependsOn`, used by `BoardGeneration` for reachability BFS.

### What Gets Added to `BoardGeneration`

1. **`ComputeForwardDeps` / `ComputeReachableSet` / `WouldCellCauseCycle`** — stateless cycle checks that operate on `Board`'s graph. No tentative edge management or backtracking state.

### What Stays the Same

- `FillBoard`, `GenerateArrows` public API signatures — unchanged.
- `TryGenerateArrow` flow — same structure, just calls new cycle check.
- `CompleteArrowTail` — same DFS structure, calls new cycle check instead of old one.
- `CreateInitialArrowHeads` logic — moves to `Board` initialization but same algorithm.
- `IsInRay` — reused for reverse edge computation.
- `Arrow`, `Cell` models — unchanged.

### Estimated Scope

Changes touch `Board.cs` (expanded) and `BoardGeneration.cs` (simplified). No view changes. Existing tests should pass (they verify solvability, which this fixes). New tests needed for: multi-dependency cycle detection, `IsClearable` via dependency set, graph consistency after `RemoveArrow`.

## Open Questions

1. **Board serialization:** Only board dimensions and arrows need to be serialized — the dependency graph is derived state, computable on demand from the arrow set. Deserialized boards (replays, spectating) likely won't need the graph at all since generation runs authoritatively on the server. If a client does need clearability info, the graph can be rebuilt in O(N × R), negligible for gameplay-sized boards.

2. **Row/column index:** Deferred until profiling shows reverse-edge lookup is a bottleneck. The naive scan is simple and fast for expected board sizes.
