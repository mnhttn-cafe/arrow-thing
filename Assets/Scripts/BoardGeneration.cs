using System;
using System.Collections.Generic;
using static ListUtils;

public static class BoardGeneration
{
    private static readonly Dictionary<Board, BoardCacheData> boardCacheDict = new();

    /// <summary>
    /// Default cap on DFS dead ends per arrow candidate. Beyond this the DFS returns
    /// the best path found so far rather than exhausting the search tree. Chosen empirically:
    /// at 10, a 50×50 board with arrows up to length 50 fills in ~20ms with no meaningful
    /// loss in density compared to higher limits.
    /// </summary>
    private const int DefaultDeadEndLimit = 10;

    public static void FillBoard(Board board, int minLength, int maxLength, Random random, int deadEndLimit = DefaultDeadEndLimit)
    {
        int maxPossibleArrows = board.Width * board.Height / 2;
        GenerateArrows(board, minLength, maxLength, maxPossibleArrows, random, out _, deadEndLimit);
    }

    public static bool GenerateArrows(Board board, int minLength, int maxLength, int amount, Random random, out int createdArrows, int deadEndLimit = DefaultDeadEndLimit)
    {
        createdArrows = 0;
        BoardCacheData boardCache = GetOrCreateCache(board);
        while (createdArrows < amount && TryGenerateArrow(board, minLength, maxLength, random, boardCache, out Arrow? arrow, deadEndLimit))
        {
            board.AddArrow(arrow!);
            boardCache.version = board.Version;
            HashSet<ArrowHeadData> toRemove = new();
            foreach (Cell c in arrow!.Cells)
            {
                foreach (ArrowHeadData stale in boardCache.candidateLookup[c.X, c.Y])
                    toRemove.Add(stale);
                boardCache.candidateLookup[c.X, c.Y].Clear();
            }
            boardCache.availableArrowHeads.RemoveAll(toRemove.Contains);
            createdArrows++;
        }
        return createdArrows == amount;
    }

    private static bool TryGenerateArrow(Board board, int minLength, int maxLength, Random random, BoardCacheData cache, out Arrow? arrow, int deadEndLimit)
    {
        arrow = null;
        int targetLength = random.Next(minLength, maxLength + 1);

        while (cache.availableArrowHeads.Count > 0)
        {
            int headIndex = random.Next(cache.availableArrowHeads.Count);
            ArrowHeadData candidateArrowHead = cache.availableArrowHeads[headIndex];

            if (board.GetArrowAt(candidateArrowHead.head) != null ||
                board.GetArrowAt(candidateArrowHead.next) != null ||
                DoesArrowCandidateCauseCycle(board, candidateArrowHead.head, candidateArrowHead.Body, candidateArrowHead.direction))
            {
                cache.availableArrowHeads.RemoveAt(headIndex);
                continue;
            }

            List<Cell> tail = CompleteArrowTail(board, targetLength, candidateArrowHead, random, deadEndLimit);
            if (tail.Count < minLength)
            {
                cache.availableArrowHeads.RemoveAt(headIndex);
                continue;
            }

            arrow = new(tail);
            return true;
        }

        return false;
    }

    private static List<Cell> CompleteArrowTail(Board board, int targetLength, ArrowHeadData headData, Random random, int deadEndLimit)
    {
        List<Cell> path = new() { headData.head, headData.next };
        HashSet<Cell> visited = new(path);
        List<Cell> best = new(path);
        int deadEnds = 0;

        void Dfs(Cell current)
        {
            if (deadEnds >= deadEndLimit) return;
            if (path.Count == targetLength)
            {
                best = new(path);
                return;
            }

            bool anyValid = false;
            foreach (Cell neighbor in Shuffle(GetNeighbors(current), random))
            {
                if (visited.Contains(neighbor)) continue;
                if (!board.Contains(neighbor)) continue;
                if (IsInRay(neighbor, headData.head, headData.direction)) continue;
                if (board.GetArrowAt(neighbor) != null) continue;

                path.Add(neighbor);
                visited.Add(neighbor);

                if (!DoesArrowCandidateCauseCycle(board, headData.head, visited, headData.direction))
                {
                    if (path.Count > best.Count) best = new(path);
                    anyValid = true;
                    Dfs(neighbor);
                    if (best.Count == targetLength || deadEnds >= deadEndLimit) return;
                }

                visited.Remove(neighbor);
                path.RemoveAt(path.Count - 1);
            }

            if (!anyValid) deadEnds++;
        }

        Dfs(headData.next);
        return best;
    }

    private static List<Cell> GetNeighbors(Cell cell)
    {
        List<Cell> neighbors = new(4)
        {
            new(cell.X + 1, cell.Y),
            new(cell.X - 1, cell.Y),
            new(cell.X, cell.Y + 1),
            new(cell.X, cell.Y - 1)
        };
        return neighbors;
    }

    private static bool IsInRay(Cell target, Cell head, Arrow.Direction direction)
    {
        if (direction == Arrow.Direction.Up)
        {
            return target.X == head.X && target.Y > head.Y;
        }
        if (direction == Arrow.Direction.Down)
        {
            return target.X == head.X && target.Y < head.Y;
        }
        if (direction == Arrow.Direction.Right)
        {
            return target.Y == head.Y && target.X > head.X;
        }
        if (direction == Arrow.Direction.Left)
        {
            return target.Y == head.Y && target.X < head.X;
        }
        return false;
    }

    private static BoardCacheData GetOrCreateCache(Board board)
    {
        if (!boardCacheDict.TryGetValue(board, out var cache))
        {
            List<ArrowHeadData> candidateArrowHeads = CreateInitialArrowHeads(board);

            List<ArrowHeadData>[,] lookup = new List<ArrowHeadData>[board.Width, board.Height];
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    lookup[x, y] = new List<ArrowHeadData>();
            foreach (ArrowHeadData candidate in candidateArrowHeads)
            {
                lookup[candidate.head.X, candidate.head.Y].Add(candidate);
                lookup[candidate.next.X, candidate.next.Y].Add(candidate);
            }

            cache = new BoardCacheData
            {
                version = board.Version,
                availableArrowHeads = candidateArrowHeads,
                candidateLookup = lookup
            };
            boardCacheDict[board] = cache;
        }
        else if (cache.version != board.Version)
        {
            throw new InvalidOperationException(
                $"Board was mutated outside of BoardGeneration (cache version {cache.version}, board version {board.Version}). " +
                "Only use Board.AddArrow / Board.RemoveArrow through BoardGeneration.");
        }

        return cache;
    }

    private static List<ArrowHeadData> CreateInitialArrowHeads(Board board)
    {
        List<ArrowHeadData> arrowHeads = new();

        // right-facing arrows
        for (int x = 0; x < board.Width - 1; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                arrowHeads.Add(new ArrowHeadData
                {
                    head = new(x + 1, y),
                    next = new(x, y),
                    direction = Arrow.Direction.Right
                });
            }
        }

        // left-facing arrows
        for (int x = 0; x < board.Width - 1; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                arrowHeads.Add(new ArrowHeadData
                {
                    head = new(x, y),
                    next = new(x + 1, y),
                    direction = Arrow.Direction.Left
                });
            }
        }

        // up-facing arrows
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height - 1; y++)
            {
                arrowHeads.Add(new ArrowHeadData
                {
                    head = new(x, y + 1),
                    next = new(x, y),
                    direction = Arrow.Direction.Up
                });
            }
        }

        // down-facing arrows
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height - 1; y++)
            {
                arrowHeads.Add(new ArrowHeadData
                {
                    head = new(x, y),
                    next = new(x, y + 1),
                    direction = Arrow.Direction.Down
                });
            }
        }
        return arrowHeads;
    }

    private static bool DoesArrowCandidateCauseCycle(Board board, Cell head, HashSet<Cell> currentBody, Arrow.Direction direction)
    {
        Cell rayOrigin = head;
        Arrow.Direction rayDirection = direction;
        HashSet<Arrow> visitedArrows = new();

        while (true)
        {
            (int dx, int dy) = Arrow.GetDirectionStep(rayDirection);
            Cell cursor = new(rayOrigin.X + dx, rayOrigin.Y + dy);
            Arrow? hitArrow = null;

            while (board.Contains(cursor))
            {
                if (currentBody.Contains(cursor))
                {
                    return true;
                }

                hitArrow = board.GetArrowAt(cursor);

                if (hitArrow != null)
                {
                    break;
                }

                cursor = new Cell(cursor.X + dx, cursor.Y + dy);
            }

            if (hitArrow == null)
            {
                return false;
            }

            if (!visitedArrows.Add(hitArrow))
            {
                return true;
            }

            rayOrigin = hitArrow.HeadCell;
            rayDirection = hitArrow.HeadDirection;
        }
    }

    private class BoardCacheData
    {
        public int version;
        public List<ArrowHeadData> availableArrowHeads = null!;
        public List<ArrowHeadData>[,] candidateLookup = null!;
    }

    private sealed class ArrowHeadData
    {
        public Cell head;
        public Cell next;
        public HashSet<Cell> Body => new() { head, next };
        public Arrow.Direction direction;
    }
}
