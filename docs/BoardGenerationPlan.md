# Board Generation Algorithm Notes (Head-First Naive v2)

## Purpose
Define a generator that supports both:
- Full board generation.
- Single-arrow generation (needed for garbage insertion).

These are high-level design notes used to explain the approach.

The algorithm prioritizes correctness and pruning over packing optimality, and may evolve as optimization work continues.

## Status
- Updated to match current implementation in `Assets/Scripts/Models/BoardGenerator.cs`.
- This document is explanatory, not a strict API contract.

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

    public int FillBoard(
        BoardModel board,
        int arrowCount,
        int minLength,
        int maxLength);

    public bool TryGenerateSingleArrow(
        BoardModel board,
        int minLength,
        int maxLength,
        out ArrowModel arrow);
}
```

Notes:
- `TryGenerateSingleArrow` is the primary garbage-facing API.
- Board fill should call the single-arrow API repeatedly.
- There is currently no explicit max-attempts parameter in the public API.

## High-Level Flow
`FillBoard`:
1. Validate inputs.
2. Loop until `arrowCount` arrows are placed.
3. Call `TryGenerateSingleArrow(...)`.
4. On success, place with `TryAddArrow(...)` and increment placed count.
5. On failure, stop early and return partial count.

`TryGenerateSingleArrow`:
1. Build a candidate set of valid minimal arrows (length 2).
2. Randomly pick one candidate.
3. Build its head-ray set.
4. Try to grow that candidate with DFS at sampled lengths, backing off max length when growth fails.
5. Return first valid grown arrow or fallback length-2 arrow.
6. If all lengths fail, remove that candidate and try another.
7. Return false if no candidates remain.

## Step 1: Candidate Head Pair Pruning
A head pair is `(h, n)` where:
- `h` is intended `Cells[0]` (head cell).
- `n` is intended `Cells[1]` (first body cell).

Current implementation shape:
1. Enumerate `h` from `board.GetFreeBoardCells()`.
2. Enumerate orthogonal neighbors of `h` as `n`.
3. Build minimal arrow `[h, n]`.
4. Keep it only if `board.CanPlaceArrow(...)` succeeds.

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

## Single Arrow Generation Pseudocode (Current Shape)
```text
TryGenerateSingleArrow(board, minLength, maxLength):
  heads <- all valid minimal arrows

  while heads not empty:
    pick candidate from heads
    headRaySet <- forward ray from candidate head
    currentMax <- maxLength

    while currentMax >= minLength:
      L <- sample length in [max(2,minLength), currentMax]
      if L == 2:
        return candidate

      path <- copy(candidate.Cells)
      if DFS_Grow(path, L, headRaySet):
        return BuildArrow(path)
      else:
        currentMax <- currentMax - 1

    remove candidate from heads

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
4. On failure, stop early and return placed count.

This gives one shared path for initial generation and garbage insertion logic.

## Complexity Notes
- Candidate head filtering is `O(cells * 4)` plus minimal placement checks.
- DFS still has exponential worst case in tail length.
- In practice, head pruning removes many impossible branches before recursion.

## Failure Handling
- If no valid head pairs exist, fail fast.
- If candidate growth fails at longer lengths, the current length ceiling backs off and retries.
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
