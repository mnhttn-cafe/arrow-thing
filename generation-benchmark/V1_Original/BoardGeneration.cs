using System;
using System.Collections.Generic;

namespace V1
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
            GenerateArrows(
                board,
                minLength,
                maxLength,
                maxPossibleArrows,
                random,
                out _,
                deadEndLimit
            );
        }

        public static bool GenerateArrows(
            Board board,
            int minLength,
            int maxLength,
            int amount,
            Random random,
            out int createdArrows,
            int deadEndLimit = DefaultDeadEndLimit
        )
        {
            createdArrows = 0;
            if (board._availableArrowHeads == null)
                board.InitializeForGeneration();

            while (
                createdArrows < amount
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
                createdArrows++;
            }
            return createdArrows == amount;
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
            List<Cell> path = new() { headData.head, headData.next };
            HashSet<Cell> visited = new(path);
            List<Cell> best = new(path);
            int deadEnds = 0;

            void Dfs(Cell current)
            {
                if (deadEnds >= deadEndLimit)
                    return;
                if (path.Count == targetLength)
                {
                    best = new(path);
                    return;
                }

                bool anyValid = false;
                foreach (Cell neighbor in Shuffle(GetNeighbors(current), random))
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
                        best = new(path);
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
            foreach (Arrow arrow in board.Arrows)
            {
                if (
                    Board.IsInRay(cell, arrow.HeadCell, arrow.HeadDirection)
                    && reachable.Contains(arrow)
                )
                    return true;
            }
            return false;
        }

        private static List<Cell> GetNeighbors(Cell cell)
        {
            return new List<Cell>(4)
            {
                new(cell.X + 1, cell.Y),
                new(cell.X - 1, cell.Y),
                new(cell.X, cell.Y + 1),
                new(cell.X, cell.Y - 1),
            };
        }

        private static List<T> Shuffle<T>(List<T> list, Random random)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
            return list;
        }
    }
}
