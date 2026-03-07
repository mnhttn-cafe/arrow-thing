# Board Generation

## Purpose

Describes how board generation works in `Assets/Scripts/BoardGeneration.cs`.

This document is descriptive, not a future plan or API proposal.

## Scope

`BoardGeneration` is responsible for:

- Filling a board with generated arrows.
- Generating up to a requested number of additional arrows.
- Building arrow shapes by extending from length-2 head candidates.
- Rejecting candidates that would introduce cyclic clear dependencies.

It is not responsible for gameplay removal logic or rendering.

## Data Structures

### `boardCacheDict`

- Type: `static Dictionary<Board, BoardCacheData>`
- Keyed by board instance reference.
- Persists generation state across multiple `GenerateArrows` calls on the same board.

### `BoardCacheData` (class)

- `version`: last known `Board.Version` at time of sync. Compared against `board.Version` on every `GetOrCreateCache` call; throws `InvalidOperationException` on mismatch to catch external mutation early.
- `availableArrowHeads`: pool of remaining unpruned 2-cell head candidates.
- `occupancy`: `Arrow?[,]` grid lookup — maps each cell to the arrow occupying it, or `null`.
- `candidateLookup`: `List<ArrowHeadData>[,]` reverse-lookup — maps each cell to the candidates whose `head` or `next` touches it. Used to efficiently prune stale candidates on arrow placement.

### `ArrowHeadData`

- `head`: candidate `Cells[0]`.
- `next`: candidate `Cells[1]`.
- `direction`: candidate head direction.
- `Body`: convenience `HashSet<Cell>` `{head, next}` — used as the initial `currentBody` for cycle detection.

## Public Entry Points

### `FillBoard(Board board, int minLength, int maxLength, Random random, int deadEndLimit = 10)`

- Computes `maxPossibleArrows = width * height / 2`.
- Delegates to `GenerateArrows(...)` with that amount.

### `GenerateArrows(Board board, int minLength, int maxLength, int amount, Random random, out int createdArrows, int deadEndLimit = 10)`

Flow:

1. Fetch or create cache via `GetOrCreateCache(board)`.
2. Repeatedly call `TryGenerateArrow(...)` until `createdArrows == amount` or candidates are exhausted.
3. On each successful arrow:
   - call `board.AddArrow(arrow)` (increments `board.Version`)
   - sync `boardCache.version = board.Version`
   - stamp every arrow cell into `cache.occupancy`
   - collect stale candidates via `candidateLookup` and prune them from `availableArrowHeads`
4. Return `createdArrows == amount`.

## Head Candidate Enumeration

`CreateInitialArrowHeads(board)` precomputes all valid adjacent `(head, next)` pairs for all 4 directions. These are geometric candidates only — filtered later by occupancy checks, cycle detection, and tail length.

Y-up coordinate convention: `Direction.Up → dy = +1`, `Direction.Down → dy = -1`.

## Single Arrow Construction

### `TryGenerateArrow(...)`

1. Sample one target length in `[minLength, maxLength]`.
2. While head candidates remain:
   - pick a random candidate from `availableArrowHeads`
   - reject and remove if head or next cell is occupied
   - reject and remove if 2-cell form causes a cycle (`DoesArrowCandidateCauseCycle`)
   - call `CompleteArrowTail(...)` to grow the body
   - reject and remove candidate if resulting tail is shorter than `minLength`
   - otherwise return the arrow
3. Return false if all candidates are exhausted.

### `CompleteArrowTail(...)`

DFS+backtracking from the fixed 2-cell start `[head, next]`.

State:

- `path`: current arrow cells (ordered)
- `visited`: `HashSet<Cell>` of cells in `path` — used for O(1) membership and passed as `currentBody` to cycle detection
- `best`: longest cycle-free path found so far
- `deadEnds`: count of DFS nodes where no valid neighbor could be taken

Neighbor filtering per step:

- already in `visited`
- out of bounds
- lies on the candidate's head ray (`IsInRay`)
- occupied by an existing arrow

After adding a neighbor, `DoesArrowCandidateCauseCycle` runs on the current `visited` set. Only cycle-free extensions are recursed into.

Early exits:

- `path.Count == targetLength` — exact match found
- `deadEnds >= deadEndLimit` — search budget exhausted

The dead-end limit (default `10`) prevents exhaustive search in dense boards. Empirically, limits beyond ~20 yield negligible density gain at increasing cost; limits below ~5 may shorten arrows on very constrained boards.

Backtracking removes the neighbor from both `visited` and `path`.

Returns `best`, which may be shorter than `targetLength` if the DFS could not reach it within the budget.

## Candidate Pruning on Placement

When an arrow is placed, every cell it occupies is looked up in `candidateLookup`. All candidates touching those cells are collected into a `HashSet<ArrowHeadData>` (deduplicating candidates that appear in multiple cells' lists), then removed from `availableArrowHeads` in a single `RemoveAll` pass. The lookup entries are cleared as candidates are collected.

This keeps the candidate pool clean without a full linear scan on every placement.

## Cycle Detection

### `DoesArrowCandidateCauseCycle(Board board, Cell head, HashSet<Cell> currentBody, Arrow.Direction direction, BoardCacheData cache)`

Determines whether placing the candidate body would create a cyclic clear dependency — i.e., a loop in the "blocks" graph where arrow A's ray is blocked by arrow B, B's ray is blocked by C, and so on back to A.

Algorithm:

1. Start ray traversal at `head` in `direction`.
2. Walk the ray one step at a time.
3. If any traversed cell is in `currentBody`, return `true` (the candidate's own ray hits its own body — immediate cycle).
4. If the ray exits the board without hitting anything, return `false`.
5. If the ray hits a cell occupied by an existing arrow, jump: set `rayOrigin` to that arrow's head, `rayDirection` to that arrow's `HeadDirection`, and continue.
6. `visitedArrows` prevents infinite loops: if the same existing arrow is encountered twice, return `true`.

## Supporting Helpers

### `IsInRay(Cell target, Cell head, Arrow.Direction direction)`

Returns whether `target` lies strictly forward of `head` along `direction`. Used to prevent a tail cell from falling on the arrow's own forward ray.

### `GetNeighbors(Cell cell)`

Returns the 4 orthogonal neighbors in fixed order (right, left, up, down).

## Performance

Measured on .NET 8, Debug build, single-threaded, seed 0.

| Board | len=[2,5] | len=[5,20] | len=[10,50] |
|-------|-----------|------------|-------------|
| 10×10 | <1ms | <1ms | 2ms |
| 20×20 | 1ms | 1ms | 1ms |
| 50×50 | ~56ms | ~30ms | ~20ms |

Key optimisations applied (in order of impact):

1. **`currentBody` as `HashSet<Cell>`** — cycle detection previously called `List.Contains` per ray step (O(n) per step). Passing `visited` (already a `HashSet`) makes it O(1). Reduced 50×50 VeryLong from ~5s to ~1.7s.
2. **Dead-end limit** — DFS previously exhausted the full search tree before giving up. Capping at `DefaultDeadEndLimit = 10` dead ends per candidate reduced 50×50 VeryLong from ~1.7s to ~20ms with no meaningful density loss.
3. **Eager candidate pruning via `candidateLookup`** — stale candidates are removed immediately on placement rather than discovered one-by-one at use-time. Keeps `availableArrowHeads` lean across the full fill.
4. **Occupancy guard on head/next before cycle check** — avoids a full ray walk for candidates whose start cells are already taken.

## Design History

The current implementation is a rewrite (`generation-rewrite` branch) of an earlier `BoardGenerator` (sealed class, instance-based). Key decisions made during that history:

- **Original `BoardGenerator`** (pre-rewrite): used `BoardModel`'s `CanPlaceArrow` for legality, recursive DFS+backtracking for tail growth. Correct but very slow on large boards.
- **Optimization pass** (`15c8f90`, external contributor): replaced recursive DFS with bounded stochastic constructive growth; cached `_occupiedCells`, `_arrowHeads`, and `_occupiedOwnerHeads` maps; introduced `_possibleStarters` list with lazy pruning; added failure budgeting and greedy fallback. Much faster, but non-exhaustive and relied on magic thresholds.
- **Rewrite rationale**: move to a static class with minimal model classes; abandon instance state and move cache to a static `boardCacheDict`; restore exhaustive DFS+backtracking (now feasible because expensive legality checks are replaced with direct occupancy lookup); drop the length-sweep-downward strategy in favour of random sampling with best-effort tail extension.
- **Surfaces concept** (attempted in early rewrite, then abandoned): `OccupantRef[,]` occupancy tracked whether each cell belonged to a `Surface` (free region) or `Arrow`. Surfaces were intended to be split by flood-fill after each placement to prune candidates too small for the target length. `SplitSurfaceByArrow` was stubbed and the concept was removed in favour of the simpler flat `availableArrowHeads` pool.
