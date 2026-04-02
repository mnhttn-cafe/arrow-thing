using System;
using System.Collections.Generic;

namespace V2
{
    public static class BoardGeneration
    {
        private const int DefaultDeadEndLimit = 10;

        public static void FillBoard(
            Board board,
            int minLength,
            int maxLength,
            Random random,
            int deadEndLimit = DefaultDeadEndLimit
        )
        {
            board.InitializeForGeneration();
            int maxPossibleArrows = board.Width * board.Height / 2;
            int created = 0;

            while (
                created < maxPossibleArrows
                && board._availableArrowHeads != null
                && board._availableArrowHeads.Count > 0
                && TryGenerateArrow(
                    board,
                    minLength,
                    maxLength,
                    random,
                    out Arrow arrow,
                    deadEndLimit
                )
            )
            {
                board.AddArrow(arrow);
                created++;
            }
        }

        private static bool TryGenerateArrow(
            Board board,
            int minLength,
            int maxLength,
            Random random,
            out Arrow arrow,
            int deadEndLimit
        )
        {
            arrow = null;
            int targetLength = random.Next(minLength, maxLength + 1);
            var candidates = board._availableArrowHeads;

            while (candidates.Count > 0)
            {
                int headIndex = random.Next(candidates.Count);
                ArrowHeadData candidate = candidates[headIndex];

                if (
                    board.GetArrowAt(candidate.head) != null
                    || board.GetArrowAt(candidate.next) != null
                )
                {
                    candidates.RemoveAt(headIndex);
                    continue;
                }

                HashSet<Arrow> forwardDeps = ComputeForwardDeps(
                    board,
                    candidate.head,
                    candidate.direction
                );
                HashSet<Arrow> reachable = ComputeReachableSet(board, forwardDeps);

                if (
                    WouldCellCauseCycle(board, candidate.head, reachable)
                    || WouldCellCauseCycle(board, candidate.next, reachable)
                )
                {
                    candidates.RemoveAt(headIndex);
                    continue;
                }

                List<Cell> tail = CompleteArrowTail(
                    board,
                    targetLength,
                    candidate,
                    random,
                    deadEndLimit,
                    reachable
                );
                if (tail.Count < minLength)
                {
                    candidates.RemoveAt(headIndex);
                    continue;
                }

                arrow = new(tail);
                return true;
            }

            return false;
        }

        private static List<Cell> CompleteArrowTail(
            Board board,
            int targetLength,
            ArrowHeadData headData,
            Random random,
            int deadEndLimit,
            HashSet<Arrow> reachable
        )
        {
            List<Cell> path = new(targetLength) { headData.head, headData.next };
            HashSet<Cell> visited = new(path);
            List<Cell> best = new(targetLength);
            best.Add(headData.head);
            best.Add(headData.next);
            int deadEnds = 0;

            void Dfs(Cell current)
            {
                if (deadEnds >= deadEndLimit)
                    return;
                if (path.Count == targetLength)
                {
                    best.Clear();
                    best.AddRange(path);
                    return;
                }

                bool anyValid = false;
                Cell[] neighbors = GetNeighbors(current);
                Shuffle(neighbors, random);
                foreach (Cell neighbor in neighbors)
                {
                    if (visited.Contains(neighbor))
                        continue;
                    if (!board.Contains(neighbor))
                        continue;
                    if (Board.IsInRay(neighbor, headData.head, headData.direction))
                        continue;
                    if (board.GetArrowAt(neighbor) != null)
                        continue;
                    if (WouldCellCauseCycle(board, neighbor, reachable))
                        continue;

                    path.Add(neighbor);
                    visited.Add(neighbor);
                    if (path.Count > best.Count)
                    {
                        best.Clear();
                        best.AddRange(path);
                    }
                    anyValid = true;
                    Dfs(neighbor);
                    if (best.Count == targetLength || deadEnds >= deadEndLimit)
                        return;

                    visited.Remove(neighbor);
                    path.RemoveAt(path.Count - 1);
                }

                if (!anyValid)
                    deadEnds++;
            }

            Dfs(headData.next);
            return best;
        }

        private static HashSet<Arrow> ComputeForwardDeps(
            Board board,
            Cell head,
            Arrow.Direction direction
        )
        {
            var deps = new HashSet<Arrow>();
            (int dx, int dy) = Arrow.GetDirectionStep(direction);
            Cell cursor = new(head.X + dx, head.Y + dy);
            while (board.Contains(cursor))
            {
                Arrow hit = board.GetArrowAt(cursor);
                if (hit != null)
                    deps.Add(hit);
                cursor = new(cursor.X + dx, cursor.Y + dy);
            }
            return deps;
        }

        private static HashSet<Arrow> ComputeReachableSet(Board board, HashSet<Arrow> startSet)
        {
            var reachable = new HashSet<Arrow>(startSet);
            var queue = new Queue<Arrow>(startSet);
            while (queue.Count > 0)
            {
                Arrow current = queue.Dequeue();
                foreach (Arrow dep in board.GetDependencies(current))
                {
                    if (reachable.Add(dep))
                        queue.Enqueue(dep);
                }
            }
            return reachable;
        }

        private static bool WouldCellCauseCycle(Board board, Cell cell, HashSet<Arrow> reachable)
        {
            return board.AnyArrowWithRayThroughMatches(cell, reachable);
        }

        private static Cell[] GetNeighbors(Cell cell)
        {
            return new Cell[]
            {
                new(cell.X + 1, cell.Y),
                new(cell.X - 1, cell.Y),
                new(cell.X, cell.Y + 1),
                new(cell.X, cell.Y - 1),
            };
        }

        private static void Shuffle(Cell[] array, Random random)
        {
            int n = array.Length;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                (array[n], array[k]) = (array[k], array[n]);
            }
        }
    }
}
