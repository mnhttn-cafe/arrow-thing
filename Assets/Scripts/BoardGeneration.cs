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
            Cell arrowHead = arrow.HeadCell;
            int surfaceIndex = boardCache.occupancy[arrowHead.X, arrowHead.Y].Index;
            int arrowIndex = board.Arrows.Count;
            board.Arrows.Add(arrow);
            createdArrows++;
            UpdateBoardCache(boardCache, arrow, surfaceIndex, arrowIndex);
        }
        return createdArrows == amount;
    }

    private static bool TryGenerateArrow(Board board, int minLength, int maxLength, Random random, BoardCacheData cache, out Arrow arrow)
    {
        arrow = null;
        while (minLength <= maxLength)
        {
            int targetLength = maxLength;
            List<ArrowHeadData> validHeads = GetValidArrowHeads(board, targetLength, cache);

            Shuffle(validHeads, random);
            while (validHeads.Count > 0)
            {
                ArrowHeadData candidateArrowHead = validHeads[^1];
                List<Cell> head = new()
                {
                    candidateArrowHead.head,
                    candidateArrowHead.next
                };
                if (TryCompleteArrowTail(board, targetLength, head, candidateArrowHead, random, cache, out arrow)) return true;
                validHeads.RemoveAt(validHeads.Count - 1);
            }
            maxLength = targetLength - 1;
        }
        return false;
    }

    private static List<ArrowHeadData> GetValidArrowHeads(Board board, int targetLength, BoardCacheData cache)
    {
        List<ArrowHeadData> validArrowHeads = new();
        foreach (Surface surface in cache.surfaces)
        {
            if (surface.cells.Count >= targetLength)
            {
                validArrowHeads.AddRange(surface.arrowHeads);
            }
        }
        return validArrowHeads;
    }

    private static bool TryCompleteArrowTail(Board board, int targetLength, List<Cell> currentBody, ArrowHeadData headData, Random random, BoardCacheData cache, out Arrow arrow)
    {
        arrow = null;

        if (targetLength == 2)
        {
            arrow = new(currentBody);
            return true;
        }

        List<Cell> neighbors = GetNeighbors(currentBody[^1]);
        Shuffle(neighbors, random);

        foreach (Cell cell in neighbors)
        {
            if (!board.Contains(cell)) continue;
            if (IsInRay(cell, currentBody[0], headData.direction)) continue;
            if (cache.occupancy[cell.X, cell.Y].Kind == OccupantKind.Arrow) continue;
            currentBody.Add(cell);
            bool causesCycle = DoesArrowCandidateCauseCycle(board, currentBody, headData.direction, null, cache);
            if (!causesCycle && TryCompleteArrowTail(board, targetLength, currentBody, headData, random, cache, out arrow))
                return true;
            currentBody.RemoveAt(currentBody.Count - 1);
        }

        return false;
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
            List<ArrowHeadData> validArrowHeads = CreateInitialArrowHeads(board);
            List<Surface> surfaces = CreateInitialSurface(board, validArrowHeads);
            OccupantRef[,] occupancy = CreateInitialOccupancyMap(board);
            cache = new BoardCacheData
            {
                validArrowHeads = validArrowHeads,
                surfaces = surfaces,
                occupancy = occupancy
            };
            boardCacheDict[board] = cache;
        }

        return cache;
    }

    private static List<ArrowHeadData> CreateInitialArrowHeads(Board board)
    {
        List<ArrowHeadData> arrowHeads = new();
        List<Cell> currentHeadRay = new(Math.Max(board.Width, board.Height));

        // right-facing arrows
        for (int x = 0; x < board.Width - 1; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int h = x + 2; h < board.Width; h++)
                {
                    currentHeadRay[h - x - 2] = new(h, y);
                }
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
                for (int h = x - 1; h > 0; h--)
                {
                    currentHeadRay[x - h - 1] = new(h, y);
                }
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
                for (int h = y - 1; h > 0; h--)
                {
                    currentHeadRay[y - h - 1] = new(h, y);
                }
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
                for (int h = y + 2; h < board.Height; h++)
                {
                    currentHeadRay[h - x - 2] = new(h, y);
                }
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

    private static List<Surface> CreateInitialSurface(Board board, List<ArrowHeadData> arrowHeads)
    {
        List<Cell> cells = new();
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                cells.Add(new(x, y));
            }
        }

        Surface surface = new()
        {
            arrowHeads = arrowHeads,
            cells = cells
        };
        return new List<Surface>() { surface };
    }

    private static OccupantRef[,] CreateInitialOccupancyMap(Board board)
    {
        OccupantRef[,] occupancy = new OccupantRef[board.Width, board.Height];
        OccupantRef surface0 = OccupantRef.Surface(0);

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                occupancy[x, y] = surface0;
            }
        }
        return occupancy;
    }

    private static void UpdateBoardCache(BoardCacheData boardCache, Arrow arrow, int surfaceIndex, int arrowIndex)
    {
        Surface surface = boardCache.surfaces[surfaceIndex];

        // Re-run flood fill on only the surface in which the arrow was generated
        List<Surface> newSurfaces = SplitSurfaceByArrow(surface, arrow);
        boardCache.surfaces.RemoveAt(surfaceIndex);
        boardCache.surfaces.AddRange(newSurfaces);

        // Update occupancy map in the newly generated surfaces and the arrow
        foreach (Cell c in arrow.Cells)
        {
            boardCache.occupancy[c.X, c.Y] = OccupantRef.Arrow(arrowIndex);
        }

        foreach (Surface s in newSurfaces)
        {
            foreach (Cell c in s.cells)
            {
                boardCache.occupancy[c.X, c.Y] = OccupantRef.Surface(surfaceIndex);
            }
        }

        // Remove all valid arrow heads that overlap or cause a cycle with the new arrow
        RemoveInvalidArrowHeads(boardCache, arrow);
    }

    private static void RemoveInvalidArrowHeads(BoardCacheData boardCacheData, Arrow arrow)
    {
        // TODO: Remove all currently valid arrow heads that either intersect the target arrow or cause a cycle.
    }

    private static List<Surface> SplitSurfaceByArrow(Surface surface, Arrow arrow)
    {
        return new() { surface };
    }

    private static bool DoesArrowCandidateCauseCycle(Board board, List<Cell> currentBody, Arrow.Direction direction, Arrow safeArrow, BoardCacheData cache)
    {
        return true;
    }

    private struct BoardCacheData
    {
        public List<ArrowHeadData> validArrowHeads;
        public List<Surface> surfaces;
        public OccupantRef[,] occupancy;
    }

    private struct ArrowHeadData
    {
        public Cell head;
        public Cell next;
        public Arrow.Direction direction;
        public int surfaceIndex;
    }

    private struct Surface
    {
        public List<ArrowHeadData> arrowHeads;
        public List<Cell> cells;
    }

    private enum OccupantKind : byte
    {
        None,
        Arrow,
        Surface
    }

    private readonly struct OccupantRef
    {
        public OccupantKind Kind { get; }
        public int Index { get; } // Arrow index in board.Arrows, or Surface index in cache.surfaces

        private OccupantRef(OccupantKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public static OccupantRef None => new(OccupantKind.None, -1);
        public static OccupantRef Arrow(int i) => new(OccupantKind.Arrow, i);
        public static OccupantRef Surface(int i) => new(OccupantKind.Surface, i);
    }
}