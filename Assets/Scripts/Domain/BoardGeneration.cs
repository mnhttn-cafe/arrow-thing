using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using static ListUtils;

public static class BoardGeneration
{
    /// <summary>
    /// Default cap on DFS dead ends per arrow candidate. Beyond this the DFS returns
    /// the best path found so far rather than exhausting the search tree.
    /// </summary>
    private const int DefaultDeadEndLimit = 10;

    /// <summary>
    /// Incremental version of <see cref="FillBoard"/>. Places as many arrows as possible
    /// within <paramref name="frameBudgetMs"/> per frame, then yields to let the caller
    /// (e.g. a Unity coroutine) process the next frame. Repeats until generation is complete.
    /// </summary>
    public static IEnumerator FillBoardIncremental(
        Board board,
        int minLength,
        int maxLength,
        Random random,
        long frameBudgetMs = 12,
        int deadEndLimit = DefaultDeadEndLimit
    )
    {
        board.InitializeForGeneration();
        int maxPossibleArrows = board.Width * board.Height / 2;
        int created = 0;
        var sw = new Stopwatch();

        while (created < maxPossibleArrows)
        {
            sw.Restart();
            while (
                created < maxPossibleArrows
                && sw.ElapsedMilliseconds < frameBudgetMs
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
                board.AddArrow(arrow!);
                created++;
            }

            if (
                created >= maxPossibleArrows
                || board._availableArrowHeads == null
                || board._availableArrowHeads.Count == 0
            )
                break;

            yield return null;
        }
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
            && TryGenerateArrow(board, minLength, maxLength, random, out Arrow arrow, deadEndLimit)
        )
        {
            board.AddArrow(arrow!);
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
        var candidates = board._availableArrowHeads!;

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

            // Compute reachability set: all arrows transitively reachable from the
            // candidate's forward deps through the committed dependency graph.
            // A cycle exists iff any arrow whose ray crosses a candidate cell is in this set.
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

    /// <summary>Collects all distinct arrows in the forward ray from <paramref name="head"/> in <paramref name="direction"/>.</summary>
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

    /// <summary>BFS from <paramref name="startSet"/> through committed dependsOn edges. Returns all transitively reachable arrows.</summary>
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

    /// <summary>
    /// Returns true if placing a candidate cell at <paramref name="cell"/> would create a cycle.
    /// A cycle exists when an existing arrow whose ray crosses the cell is already in the
    /// candidate's transitive dependency set — meaning the candidate would depend on that arrow
    /// (through forward deps) while that arrow simultaneously depends on the candidate.
    /// </summary>
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
}

sealed class ArrowHeadData
{
    public Cell head;
    public Cell next;
    public Arrow.Direction direction;
}
