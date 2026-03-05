using System;
using System.Collections.Generic;
using static ListUtils;

public static class BoardGeneration
{
    private static readonly Dictionary<Board, BoardCacheData> boardCacheDict = new();

    public static void FillBoard(Board board, int minLength, int maxLength, Random random)
    {
        int maxPossibleArrows = board.Width * board.Height / 2;
        GenerateArrows(board, minLength, maxLength, maxPossibleArrows, random, out _);
    }

    public static bool GenerateArrows(Board board, int minLength, int maxLength, int amount, Random random, out int createdArrows)
    {
        createdArrows = 0;
        BoardCacheData boardCache = GetOrCreateCache(board);
        while (createdArrows < amount && TryGenerateArrow(board, minLength, maxLength, random, boardCache, out Arrow arrow))
        {
            board.Arrows.Add(arrow);
            foreach (Cell c in arrow.Cells)
            {
                boardCache.occupancy[c.X, c.Y] = arrow;
            }
            createdArrows++;
        }
        return createdArrows == amount;
    }

    private static bool TryGenerateArrow(Board board, int minLength, int maxLength, Random random, BoardCacheData cache, out Arrow arrow)
    {
        arrow = null;
        int targetLength = random.Next(minLength, maxLength + 1);

        while (cache.availableArrowHeads.Count > 0)
        {
            int headIndex = random.Next(cache.availableArrowHeads.Count);
            ArrowHeadData candidateArrowHead = cache.availableArrowHeads[headIndex];

            if (DoesArrowCandidateCauseCycle(board, candidateArrowHead.Body, candidateArrowHead.direction, cache))
            {
                cache.availableArrowHeads.RemoveAt(headIndex);
                continue;
            }

            arrow = new(CompleteArrowTail(board, targetLength, candidateArrowHead, random, cache));
            return true;
        }

        return false;
    }

    private static List<Cell> CompleteArrowTail(Board board, int targetLength, ArrowHeadData headData, Random random, BoardCacheData cache)
    {
        List<Cell> path = new() { headData.head, headData.next };
        HashSet<Cell> visited = new(path);
        List<Cell> best = new(path);

        void Dfs(Cell current)
        {
            if (path.Count == targetLength)
            {
                best = new(path);
                return;
            }

            foreach (Cell neighbor in Shuffle(GetNeighbors(current), random))
            {
                if (visited.Contains(neighbor)) continue;
                if (!board.Contains(neighbor)) continue;
                if (IsInRay(neighbor, headData.head, headData.direction)) continue;

                path.Add(neighbor);
                visited.Add(neighbor);

                if (!DoesArrowCandidateCauseCycle(board, path, headData.direction, cache))
                {
                    if (path.Count > best.Count) best = new(path);
                    Dfs(neighbor);
                    if (best.Count == targetLength) return; // early exit once exact length found
                }

                visited.Remove(neighbor);                // backtrack
                path.RemoveAt(path.Count - 1);           // backtrack
            }
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
            return target.X == head.X && target.Y < head.Y;
        }
        if (direction == Arrow.Direction.Down)
        {
            return target.X == head.X && target.Y > head.Y;
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
            cache = new BoardCacheData
            {
                availableArrowHeads = candidateArrowHeads,
                occupancy = new Arrow[board.Width, board.Height]
            };
            boardCacheDict[board] = cache;
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
                    head = new(x, y),
                    next = new(x, y + 1),
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
                    head = new(x, y + 1),
                    next = new(x, y),
                    direction = Arrow.Direction.Down
                });
            }
        }
        return arrowHeads;
    }

    private static bool DoesArrowCandidateCauseCycle(Board board, List<Cell> currentBody, Arrow.Direction direction, BoardCacheData cache)
    {
        Cell rayOrigin = currentBody[0];
        Arrow.Direction rayDirection = direction;
        HashSet<Arrow> visitedArrows = new();

        while (true)
        {
            (int dx, int dy) = Arrow.GetDirectionStep(rayDirection);
            Cell cursor = new(rayOrigin.X + dx, rayOrigin.Y + dy);
            Arrow hitArrow = null;

            while (board.Contains(cursor))
            {
                if (currentBody.Contains(cursor))
                {
                    return true;
                }

                hitArrow = cache.occupancy[cursor.X, cursor.Y];

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

    private struct BoardCacheData
    {
        public List<ArrowHeadData> availableArrowHeads;
        public Arrow[,] occupancy;
    }

    private sealed class ArrowHeadData
    {
        public Cell head;
        public Cell next;
        public List<Cell> Body => new() { head, next };
        public Arrow.Direction direction;
    }
}
