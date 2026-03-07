# TODO

Tracked fixes and improvements for future implementation sessions.

## ~~Coordinate System: Flip Y to Positive-Up~~ ✓ Done

Y now increases upward. `Direction.Up → dy = +1`, `Direction.Down → dy = -1`. `DeriveHeadDirection`, `IsInRay`, and `CreateInitialArrowHeads` updated accordingly.

## ~~Bug: `CompleteArrowTail` DFS Doesn't Filter Occupied Cells~~ ✓ Done

Added `cache.occupancy[neighbor.X, neighbor.Y] != null` guard in the DFS neighbor loop.

## ~~Bug: No Occupancy Check on Candidate Head/Next in `TryGenerateArrow`~~ ✓ Done

Added occupancy checks on `candidateArrowHead.head` and `candidateArrowHead.next` before the cycle check; stale candidates are removed on detection.

## ~~Efficiency: Prune `availableArrowHeads` When an Arrow Is Placed~~ ✓ Done

Added `List<ArrowHeadData>[,] candidateLookup` (2D array, same dimensions as the board) to `BoardCacheData`. Each candidate registers in the lookup for both its `head` and `next` cells on cache init. On arrow placement, all affected candidates are collected via the lookup into a `HashSet`, the lookup entries are cleared, and a single `RemoveAll` pass purges them from `availableArrowHeads`. This is O(n) once per placement rather than O(k·n). Used a 2D array instead of a `Dictionary<Cell, …>` to avoid hashing overhead and index-invalidation complexity.

## ~~Design: Cache Invalidation in `boardCacheDict`~~ ✓ Done

`Board.Arrows` is now private; mutations route through `AddArrow`/`RemoveArrow`, each incrementing `Board.Version`. `BoardCacheData` stores the version it was last synced to. `GetOrCreateCache` throws `InvalidOperationException` on version mismatch, making external desync loud rather than silent. `BoardCacheData` changed from `struct` to `class` so the cached version int stays live through the reference.

## ~~Recreate Test Project~~ ✓ Done

Created `tests/ArrowThing.Tests/` — plain .NET 8 NUnit project, no Unity dependency. Domain source files linked via glob. Coverage:
- **`ArrowDirectionTests`** — `DeriveHeadDirection` for all 4 directions (Y-up), `GetDirectionStep`, constructor guards.
- **`BoardTests`** — `Contains` bounds, `Version` increments on `AddArrow`/`RemoveArrow`, `Arrows` readonly list.
- **`GenerationTests`** — determinism under fixed seed, no cell overlap, all cells in bounds, min-length respected, no tail in own ray, `GenerateArrows` exact-count contract, desync `InvalidOperationException`, 100× 10×10 fill under 5s.

Benchmark results will determine whether further optimization is needed. If generation becomes a real server-side bottleneck at scale, a Rust reimplementation is a viable escape hatch — generation is a clean single-operation boundary (parameters in, board state out), Yuki has existing context on the algorithm from the optimization PR, and the C# implementation can serve as the correctness reference.
