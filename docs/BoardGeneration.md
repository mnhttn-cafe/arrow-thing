# Board Generation (Current Implementation)

## Purpose

Describe how board generation currently works in `Assets/Scripts/BoardGeneration.cs`.

This document is descriptive, not a future plan or API proposal.

## Scope

The implementation in `BoardGeneration` is responsible for:

- Filling a board with generated arrows.
- Generating up to a requested number of additional arrows.
- Building arrow shapes by extending from length-2 head candidates.
- Rejecting candidates that would introduce cyclic clear dependencies.

It is not responsible for gameplay removal logic or rendering.

## Data Structures

### `boardCacheDict`

- Type: `Dictionary<Board, BoardCacheData>`
- Keyed by board instance.
- Stores generation-time cache per board.

### `BoardCacheData`

- `availableArrowHeads`: precomputed length-2 head candidates.
- `occupancy`: `Arrow[,]` lookup used by cycle checks.

### `ArrowHeadData`

- `head`: candidate `Cells[0]`.
- `next`: candidate `Cells[1]`.
- `direction`: candidate head direction.
- `Body`: convenience list `[head, next]`.

## Public Entry Points

### `FillBoard(Board board, int minLength, int maxLength, Random random)`

- Computes `maxPossibleArrows = width * height / 2`.
- Calls `GenerateArrows(...)` with that amount.
- Discards created count.

### `GenerateArrows(Board board, int minLength, int maxLength, int amount, Random random, out int createdArrows)`

Flow:

1. Fetch cache via `GetOrCreateCache(board)`.
2. Repeatedly call `TryGenerateArrow(...)` until:
   - `createdArrows == amount`, or
   - no more valid arrow can be generated.
3. On each success:
   - append arrow to `board.Arrows`
   - stamp every arrow cell into `cache.occupancy`
4. Return `createdArrows == amount`.

## Head Candidate Enumeration

`CreateInitialArrowHeads(board)` precomputes all adjacent head/next pairs that define a direction:

- Right-facing candidates
- Left-facing candidates
- Up-facing candidates
- Down-facing candidates

These are geometric candidates only. They are filtered later by cycle checks and tail construction.

## Single Arrow Construction

### `TryGenerateArrow(...)`

1. Sample one target length in `[minLength, maxLength]`.
2. While head candidates remain:
   - pick random candidate from `availableArrowHeads`
   - reject and remove candidate if `DoesArrowCandidateCauseCycle(...)` is true for `[head,next]`
   - otherwise build arrow via `CompleteArrowTail(...)` and return success
3. If all candidates are exhausted, return false.

### `CompleteArrowTail(...)`

Uses DFS from the fixed 2-cell start `[head, next]`.

State:

- `path`: current arrow cells
- `visited`: cells already in `path`
- `best`: longest valid path found so far

Neighbor filtering in DFS:

- skip already visited cells
- skip out-of-bounds cells
- skip cells that lie on the candidate head ray (`IsInRay(...)`)

After adding a neighbor:

- run `DoesArrowCandidateCauseCycle(...)` on current `path`
- only recurse when no cycle is detected
- update `best` when path grows
- early exit when exact `targetLength` is reached

Backtracking removes the neighbor from both `visited` and `path`.

Returned value is `best`, which may be shorter than `targetLength` if DFS cannot reach it.

## Cycle Detection

### `DoesArrowCandidateCauseCycle(Board board, List<Cell> currentBody, Arrow.Direction direction, BoardCacheData cache)`

Goal: detect whether adding the candidate path would create cyclic clear dependencies.

Algorithm:

1. Start ray traversal at candidate head (`currentBody[0]`) with candidate `direction`.
2. Step along ray until out of bounds or hitting occupied cell from `cache.occupancy`.
3. If any traversed cell is in `currentBody`, return `true` (cycle).
4. If ray exits board without a hit, return `false` (no cycle).
5. If ray hits an existing arrow:
   - jump origin to that arrow's head
   - set direction to that arrow's `HeadDirection`
   - continue traversal
6. `visitedArrows` guards against infinite loops in malformed/pre-cyclic states:
   - re-visiting an already traversed arrow returns `true`.

## Supporting Helpers

### `GetNeighbors(Cell cell)`

Returns orthogonal neighbors in fixed order:

- right, left, down, up

### `IsInRay(Cell target, Cell head, Arrow.Direction direction)`

Returns whether `target` lies on the forward ray from `head` for the given direction.

Used to prevent constructing an arrow whose own body blocks its head ray.

## Behavioral Notes

- Candidate heads are removed from cache only when the minimal 2-cell form is already cycle-invalid.
- `occupancy` is updated only through `GenerateArrows` when arrows are appended.
- This aligns with the current assumption that board mutation for generation flows through this API.

