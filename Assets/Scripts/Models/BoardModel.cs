using System.Collections.Generic;

/// <summary>
/// Authoritative board state for arrow placement/removal rules.
/// </summary>
/// <remarks>
/// The board models a directional dependency graph:
/// each arrow "points" from its head toward the first occupied cell on its head ray.
/// Placement rejects arrows that would create a cycle in that graph.
/// </remarks>
public sealed class BoardModel
{
    /// <summary>
    /// Mutable arrow storage, exposed read-only through <see cref="Arrows"/>.
    /// </summary>
    private readonly List<ArrowModel> _arrows = new();

    /// <summary>
    /// Fast occupancy index from board cell to owning arrow.
    /// </summary>
    private readonly Dictionary<BoardCell, ArrowModel> _occupancy = new();

    /// <summary>
    /// Reused scratch set for candidate cells during placement validation to avoid per-call allocations.
    /// </summary>
    private readonly HashSet<BoardCell> _candidateCells = new();

    /// <summary>
    /// Gets board width in cells.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets board height in cells.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets arrows currently placed on the board.
    /// </summary>
    public IReadOnlyList<ArrowModel> Arrows => _arrows;

    /// <summary>
    /// Initializes a board with fixed dimensions.
    /// </summary>
    /// <param name="width">Board width in cells.</param>
    /// <param name="height">Board height in cells.</param>
    public BoardModel(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Returns whether a cell is within board bounds.
    /// </summary>
    /// <param name="cell">Cell to test.</param>
    /// <returns><see langword="true"/> when the cell is inside <c>[0, Width) x [0, Height)</c>; otherwise <see langword="false"/>.</returns>
    public bool Contains(BoardCell cell)
    {
        return cell.X >= 0
            && cell.X < Width
            && cell.Y >= 0
            && cell.Y < Height;
    }

    /// <summary>
    /// Returns whether a cell is occupied by any arrow.
    /// </summary>
    /// <param name="cell">Cell to query.</param>
    /// <returns><see langword="true"/> if occupied; otherwise <see langword="false"/>.</returns>
    public bool IsOccupied(BoardCell cell)
    {
        return _occupancy.ContainsKey(cell);
    }

    /// <summary>
    /// Attempts to place an arrow after validating all placement rules.
    /// </summary>
    /// <param name="arrow">Arrow to place.</param>
    /// <returns><see langword="true"/> if placement succeeded; otherwise <see langword="false"/>.</returns>
    public bool TryAddArrow(ArrowModel arrow)
    {
        if (!CanPlaceArrow(arrow))
        {
            return false;
        }

        _arrows.Add(arrow);
        IReadOnlyList<BoardCell> cells = arrow.Cells;
        for (int i = 0; i < cells.Count; i++)
        {
            _occupancy[cells[i]] = arrow;
        }

        return true;
    }

    /// <summary>
    /// Attempts to remove an arrow if it is currently on the board and removable by rule.
    /// </summary>
    /// <param name="arrow">Arrow to remove.</param>
    /// <returns><see langword="true"/> if removal succeeded; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Membership is verified by reference identity at the arrow's head cell.
    /// This avoids accidentally removing a different instance that has identical geometry.
    /// </remarks>
    public bool TryRemoveArrow(ArrowModel arrow)
    {
        if (arrow == null)
        {
            return false;
        }

        if (!_occupancy.TryGetValue(arrow.HeadCell, out ArrowModel? occupyingArrow)
            || !ReferenceEquals(occupyingArrow, arrow))
        {
            return false;
        }

        if (!CanRemoveArrow(arrow))
        {
            return false;
        }

        _arrows.Remove(arrow);
        IReadOnlyList<BoardCell> cells = arrow.Cells;
        for (int i = 0; i < cells.Count; i++)
        {
            _occupancy.Remove(cells[i]);
        }

        return true;
    }

    /// <summary>
    /// Returns whether an arrow can be legally placed on the current board.
    /// </summary>
    /// <param name="arrow">Candidate arrow.</param>
    /// <returns><see langword="true"/> if legal; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Validation happens in two phases:
    /// 1) Every candidate cell must be in bounds and unoccupied.
    /// 2) A head-ray dependency walk must not re-enter candidate cells.
    ///
    /// Phase 2 is intentionally iterative and follows existing blocking arrows one-by-one instead of building
    /// a full graph. This keeps validation local and cheap for generation hot paths.
    ///
    /// A visited-head set is intentionally omitted: board state is expected to be cycle-free because all existing
    /// arrows were previously accepted by this same rule, so re-checking historic cycles would be redundant here.
    /// </remarks>
    public bool CanPlaceArrow(ArrowModel arrow)
    {
        if (arrow == null)
        {
            return false;
        }

        IReadOnlyList<BoardCell> cells = arrow.Cells;
        if (cells.Count == 0)
        {
            return false;
        }

        _candidateCells.Clear();
        for (int i = 0; i < cells.Count; i++)
        {
            BoardCell cell = cells[i];
            if (!Contains(cell))
            {
                return false;
            }

            if (_occupancy.ContainsKey(cell))
            {
                return false;
            }

            _candidateCells.Add(cell);
        }

        BoardCell currentHead = arrow.HeadCell;
        ArrowDirection currentDirection = arrow.HeadDirection;
        while (true)
        {
            // Walk along the current head ray to the first blocker.
            if (!TryFindFirstBlockingArrowOnRay(currentHead, currentDirection, _candidateCells, out ArrowModel? blockingArrow, out bool blocksByCandidate))
            {
                // The chain exits the board: no cycle can be formed by this candidate.
                return true;
            }

            if (blocksByCandidate)
            {
                // Candidate cells on the forward ray imply self-blocking/cyclic dependency.
                return false;
            }

            if (blockingArrow == null)
            {
                return false;
            }

            // Continue traversal from the blocking arrow's own head/direction.
            currentHead = blockingArrow.HeadCell;
            currentDirection = blockingArrow.HeadDirection;
        }
    }

    /// <summary>
    /// Returns whether an arrow can be removed under the clearability rule.
    /// </summary>
    /// <param name="arrow">Arrow to check.</param>
    /// <returns>
    /// <see langword="true"/> when no occupied cell exists on the arrow's forward head ray; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Only the first blocker matters: if one exists, the arrow is blocked.
    /// </remarks>
    public bool CanRemoveArrow(ArrowModel arrow)
    {
        if (arrow == null)
        {
            return false;
        }

        return !TryFindFirstBlockingArrowOnRay(arrow.HeadCell, arrow.HeadDirection, out _);
    }

    /// <summary>
    /// Enumerates all currently unoccupied cells in deterministic X-then-Y order.
    /// </summary>
    /// <returns>Sequence of free board cells.</returns>
    /// <remarks>
    /// Deterministic ordering keeps generator behavior stable when consuming this enumeration.
    /// </remarks>
    public IEnumerable<BoardCell> GetFreeBoardCells()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                BoardCell cell = new BoardCell(x, y);
                if (!IsOccupied(cell)) yield return cell;
            }
        }
    }

    /// <summary>
    /// Finds the first occupied blocker on a head ray.
    /// </summary>
    /// <param name="head">Ray origin (arrow head).</param>
    /// <param name="direction">Ray direction.</param>
    /// <param name="blockingArrow">Existing arrow hit by the ray, when any.</param>
    /// <returns><see langword="true"/> when a blocker is found; otherwise <see langword="false"/> when ray exits board.</returns>
    private bool TryFindFirstBlockingArrowOnRay(
        BoardCell head,
        ArrowDirection direction,
        out ArrowModel? blockingArrow)
    {
        return TryFindFirstBlockingArrowOnRay(head, direction, null, out blockingArrow, out _);
    }

    /// <summary>
    /// Finds the first blocker on a head ray, checking candidate cells before existing occupancy.
    /// </summary>
    /// <param name="head">Ray origin (arrow head).</param>
    /// <param name="direction">Ray direction.</param>
    /// <param name="candidateCells">Cells of the arrow being validated, or <see langword="null"/> when not applicable.</param>
    /// <param name="blockingArrow">Existing arrow hit by the ray, when any.</param>
    /// <param name="blocksByCandidate">Whether the blocker came from <paramref name="candidateCells"/>.</param>
    /// <returns><see langword="true"/> when a blocker is found; otherwise <see langword="false"/> when ray exits board.</returns>
    private bool TryFindFirstBlockingArrowOnRay(
        BoardCell head,
        ArrowDirection direction,
        HashSet<BoardCell>? candidateCells,
        out ArrowModel? blockingArrow,
        out bool blocksByCandidate)
    {
        (int dx, int dy) = ArrowModel.GetDirectionStep(direction);
        BoardCell cell = head;

        while (true)
        {
            cell = new BoardCell(cell.X + dx, cell.Y + dy);
            if (!Contains(cell))
            {
                blockingArrow = null;
                blocksByCandidate = false;
                return false;
            }

            if (candidateCells != null && candidateCells.Contains(cell))
            {
                blockingArrow = null;
                blocksByCandidate = true;
                return true;
            }

            if (_occupancy.TryGetValue(cell, out ArrowModel? occupyingArrow))
            {
                blockingArrow = occupyingArrow;
                blocksByCandidate = false;
                return true;
            }
        }
    }

}
