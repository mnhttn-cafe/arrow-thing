# Board Generation Algorithm Plan (Head-First Naive v2)

## Purpose
Define a generator that supports both:
- Full board generation.
- Single-arrow generation (needed for garbage insertion).

The algorithm prioritizes correctness and pruning over packing optimality.

## Core Rule Clarifications
- Minimum arrow length is `2`.
- Arrow head is always `Cells[0]`.
- Head direction is not sampled independently.
- Head direction is derived from `Cells[0] -> Cells[1]` and points in the opposite direction.
- If any arrow body cell lies on the forward ray from the head, the arrow is impossible.

Example:
- `Cells[0] = (0,0)`, `Cells[1] = (1,0)` means the first segment goes right.
- Head direction must be `Left`.

## Scope
- Orthogonal, contiguous, non-overlapping arrow cells.
- Placement validated by `BoardModel.CanPlaceArrow` / `TryAddArrow`.
- Deterministic when seeded.
- No attempt to guarantee perfect fill.

## API Shape
```csharp
public sealed class BoardGenerator
{
    public BoardGenerator(int? seed = null);

    public BoardModel GenerateBoard(
        int width,
        int height,
        int arrowCount,
        int minLength,
        int maxLength,
        int maxPlacementAttemptsPerArrow = 128);

    public int FillBoard(
        BoardModel board,
        int arrowCount,
        int minLength,
        int maxLength,
        int maxPlacementAttemptsPerArrow = 128);

    public bool TryGenerateSingleArrow(
        BoardModel board,
        int minLength,
        int maxLength,
        out ArrowModel arrow,
        int maxAttempts = 128);
}
```

Notes:
- `TryGenerateSingleArrow` is the primary garbage-facing API.
- Board fill should call the single-arrow API repeatedly.

## High-Level Flow
For each arrow attempt:
1. Choose target length.
2. Build a candidate set of valid head pairs `(headCell, secondCell)`.
3. Pick one candidate head pair.
4. Grow the tail with DFS from `secondCell` until target length.
5. Build arrow with derived head direction.
6. Validate/commit via board placement checks.

If any step fails, retry until budget is exhausted.

## Step 1: Candidate Head Pair Pruning
A head pair is `(h, n)` where:
- `h` is intended `Cells[0]` (head cell).
- `n` is intended `Cells[1]` (first body cell).

Pruning rules:
1. `h` must be free.
2. `n` must be orthogonally adjacent to `h`.
3. `n` must be in bounds and free.
4. Build minimal arrow `[h, n]`, derive head direction from `h -> n`, then call `CanPlaceArrow`.
5. If minimal arrow is invalid, prune this head pair entirely.

Rationale:
- If `(h, n)` fails for length `2`, any longer arrow sharing the same `(h, n)` also fails.
- This removes a large set of impossible starts before DFS.

## Step 2: DFS Tail Growth (after head pair is valid)
Given fixed `(h, n)` and target length `L`:
- Start path as `[h, n]`.
- DFS expands from current tail (`path[^1]`) by shuffled orthogonal neighbors.
- Skip neighbor if out of bounds, occupied, or already in current path.
- Precompute `headRaySet` once for `(h, n)` (all board cells forward from the head along head direction).
- During expansion, reject a neighbor immediately if `headRaySet.Contains(neighbor)` is true.
- Backtrack until either:
  - `path.Count == L` and full arrow passes `CanPlaceArrow`, or
  - all branches fail.

Important:
- A valid head pair does not guarantee a valid final arrow.
- Full-arrow validation is still required after DFS reaches length `L`.

## Self-Blocking Head-Ray Rule (Incremental)
For each chosen head pair `(h, n)`:
1. Derive head direction from `h -> n`.
2. Trace once from `h + step(headDirection)` to board edge and store cells in `headRaySet`.
3. In DFS, when evaluating a candidate `next` tail cell, do one extra check:
   - `if (headRaySet.Contains(next))` then reject `next`.

Why this is the right shape:
- This rule is part of normal DFS neighbor filtering, not a separate full-path validation pass.
- It is a single set lookup per attempted neighbor.

## Head Direction Derivation
Never choose head direction randomly.

Helper definition:
1. Compute `dx = second.X - head.X`, `dy = second.Y - head.Y`.
2. Map opposite vector `(-dx, -dy)` to enum:
   - `(0,-1) => Up`
   - `(1,0) => Right`
   - `(0,1) => Down`
   - `(-1,0) => Left`

Equivalent direct mapping from `(head -> second)`:
- Up segment => head faces Down
- Right segment => head faces Left
- Down segment => head faces Up
- Left segment => head faces Right

## Single Arrow Generation Pseudocode
```text
TryGenerateSingleArrow(board, minLength, maxLength):
  repeat maxAttempts:
    L <- sample length in [max(2,minLength), maxLength]
    heads <- all valid head pairs after minimal-arrow pruning
    if heads empty: return false

    pick (h,n) from heads
    path <- [h,n]

    if L == 2:
      arrow <- BuildArrow(path) // derive head direction
      if board.CanPlaceArrow(arrow): return arrow
      continue

    if DFS_Grow(path, L, headRaySet):
      arrow <- BuildArrow(path)
      if board.CanPlaceArrow(arrow): return arrow

  return false
```

```text
DFS_Grow(path, L, headRaySet):
  if path.Count == L:
    return true

  for each shuffled neighbor of path.last:
    if invalid basic checks: continue
    if headRaySet.Contains(neighbor): continue
    add neighbor
    if DFS_Grow(path, L, headRaySet):
      return true
    remove neighbor

  return false
```

Note:
- `visited` is intentionally omitted as a separate set in DFS.
- The algorithm only forbids reusing cells already in the current `path`.
- A global/cross-branch visited set would over-prune valid winding attempts that revisit cells explored by prior failed branches.

## Board Fill Strategy
`FillBoard`:
1. Loop `arrowCount` times.
2. Call `TryGenerateSingleArrow`.
3. On success, `TryAddArrow`.
4. On repeated failure, stop early and return placed count.

This gives one shared path for initial generation and garbage insertion logic.

## Complexity Notes
- Candidate head filtering is `O(cells * 4)` plus minimal placement checks.
- DFS still has exponential worst case in tail length.
- In practice, head pruning removes many impossible branches before recursion.

## Failure Handling
- If no valid head pairs exist, fail fast.
- If length `L` is too large for current free-space structure, retries naturally fall back through randomization.
- Return partial results for board-fill requests.

## Test Plan
Unit tests:
- `TryGenerateSingleArrow` never returns length `< 2`.
- `HeadDirection` always matches opposite of `Cells[0] -> Cells[1]`.
- Every generated arrow is contiguous and non-overlapping.
- DFS neighbor filter rejects any candidate tail cell that lies in the precomputed head ray set.
- Minimal-arrow pruning is sound:
  - Any rejected head pair also fails explicit `CanPlaceArrow` at length `2`.
- Seeded runs are reproducible.

Integration tests:
- Repeatedly fill boards across many seeds.
- Assert all placements succeed through `TryAddArrow` only.
- Assert no cyclic dependency violations appear.
