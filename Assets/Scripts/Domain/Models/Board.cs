using System.Collections.Generic;

public sealed class Board
{
    private readonly List<Arrow> _arrows = new();
    private readonly HashSet<Arrow> _arrowSet = new();
    private readonly Arrow[,] _occupancy;
    private readonly Dictionary<Arrow, HashSet<Arrow>> _dependsOn = new();
    private readonly Dictionary<Arrow, HashSet<Arrow>> _dependedOnBy = new();

    // Spatial ray index: arrows grouped by head direction and row/column.
    // For a cell (cx, cy), arrows whose ray crosses it are:
    //   Right-facing on row cy with head.X < cx, Left-facing on row cy with head.X > cx,
    //   Up-facing on col cx with head.Y < cy, Down-facing on col cx with head.Y > cy.
    private readonly List<Arrow>[] _rightHeadsByRow;
    private readonly List<Arrow>[] _leftHeadsByRow;
    private readonly List<Arrow>[] _upHeadsByCol;
    private readonly List<Arrow>[] _downHeadsByCol;

    // Generation candidate pool (null until InitializeForGeneration)
    internal List<ArrowHeadData> _availableArrowHeads;
    internal List<ArrowHeadData>[,] _candidateLookup;

    public IReadOnlyList<Arrow> Arrows => _arrows;
    public int Width { get; }
    public int Height { get; }
    public int OccupiedCellCount { get; private set; }

    /// <summary>Number of candidates at the time InitializeForGeneration was called.</summary>
    public int InitialCandidateCount { get; private set; }

    /// <summary>Number of remaining unpruned head candidates. 0 before initialization.</summary>
    public int RemainingCandidateCount =>
        _availableArrowHeads != null ? _availableArrowHeads.Count : 0;

    public Board(int width, int height)
    {
        Width = width;
        Height = height;
        _occupancy = new Arrow[width, height];

        _rightHeadsByRow = new List<Arrow>[height];
        _leftHeadsByRow = new List<Arrow>[height];
        for (int y = 0; y < height; y++)
        {
            _rightHeadsByRow[y] = new List<Arrow>();
            _leftHeadsByRow[y] = new List<Arrow>();
        }

        _upHeadsByCol = new List<Arrow>[width];
        _downHeadsByCol = new List<Arrow>[width];
        for (int x = 0; x < width; x++)
        {
            _upHeadsByCol[x] = new List<Arrow>();
            _downHeadsByCol[x] = new List<Arrow>();
        }
    }

    public void InitializeForGeneration()
    {
        _candidateLookup = new List<ArrowHeadData>[Width, Height];
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
            _candidateLookup[x, y] = new List<ArrowHeadData>();
        _availableArrowHeads = CreateInitialArrowHeads();
        InitialCandidateCount = _availableArrowHeads.Count;
    }

    /// <summary>
    /// Incrementally restores arrows from a snapshot. Places each arrow into occupancy
    /// and yields the count placed so far (for progress reporting). After all arrows are
    /// placed, builds the dependency graph in one forward-ray pass. Much faster than
    /// calling AddArrow individually because it avoids the O(n²) reverse-dependency scan.
    /// Caller must exhaust the enumerator for the board to be usable.
    /// </summary>
    public IEnumerator<int> RestoreArrowsIncremental(IReadOnlyList<Arrow> arrows)
    {
        // Phase 1: place arrows into occupancy, yielding after each
        foreach (Arrow arrow in arrows)
        {
            _arrows.Add(arrow);
            _arrowSet.Add(arrow);
            foreach (Cell c in arrow.Cells)
                _occupancy[c.X, c.Y] = arrow;
            OccupiedCellCount += arrow.Cells.Count;
            _dependsOn[arrow] = new HashSet<Arrow>();
            _dependedOnBy[arrow] = new HashSet<Arrow>();
            AddToRayIndex(arrow);
            yield return _arrows.Count;
        }

        // Phase 2: build dependency graph — each arrow's forward ray hits are its deps
        // Yields after each arrow so the caller's time budget can break between iterations.
        int depsBuilt = 0;
        foreach (Arrow arrow in arrows)
        {
            (int dx, int dy) = Arrow.GetDirectionStep(arrow.HeadDirection);
            Cell cursor = new(arrow.HeadCell.X + dx, arrow.HeadCell.Y + dy);
            while (Contains(cursor))
            {
                Arrow hit = _occupancy[cursor.X, cursor.Y];
                if (hit != null && hit != arrow)
                {
                    _dependsOn[arrow].Add(hit);
                    _dependedOnBy[hit].Add(arrow);
                }
                cursor = new(cursor.X + dx, cursor.Y + dy);
            }
            depsBuilt++;
            yield return arrows.Count + depsBuilt;
        }
    }

    public void AddArrow(Arrow arrow)
    {
        // Validations
        if (arrow == null)
            throw new System.ArgumentNullException(nameof(arrow));
        if (_arrowSet.Contains(arrow))
            throw new System.InvalidOperationException("Arrow is already on the board.");
        foreach (Cell c in arrow.Cells)
        {
            if (!Contains(c))
                throw new System.ArgumentException(
                    $"Cell ({c.X}, {c.Y}) is out of bounds for board {Width}x{Height}."
                );
            if (_occupancy[c.X, c.Y] != null)
                throw new System.InvalidOperationException(
                    $"Cell ({c.X}, {c.Y}) is already occupied."
                );
        }

        // Set occupancy
        _arrows.Add(arrow);
        _arrowSet.Add(arrow);
        foreach (Cell c in arrow.Cells)
            _occupancy[c.X, c.Y] = arrow;
        OccupiedCellCount += arrow.Cells.Count;

        // Forward deps: this arrow depends on all existing arrows in its ray
        var deps = new HashSet<Arrow>();
        (int dx, int dy) = Arrow.GetDirectionStep(arrow.HeadDirection);
        Cell cursor = new(arrow.HeadCell.X + dx, arrow.HeadCell.Y + dy);
        while (Contains(cursor))
        {
            Arrow hit = _occupancy[cursor.X, cursor.Y];
            if (hit != null && hit != arrow)
                deps.Add(hit);
            cursor = new(cursor.X + dx, cursor.Y + dy);
        }
        _dependsOn[arrow] = deps;
        foreach (Arrow dep in deps)
            _dependedOnBy[dep].Add(arrow);

        // Reverse deps: existing arrows whose rays pass through this arrow's cells
        // Uses the spatial ray index for O(crossing) instead of O(N) scan.
        // Must run BEFORE AddToRayIndex so the new arrow doesn't match itself.
        var revDeps = new HashSet<Arrow>();
        foreach (Cell c in arrow.Cells)
        {
            int cx = c.X, cy = c.Y;
            foreach (Arrow a in _rightHeadsByRow[cy])
                if (a.HeadCell.X < cx && revDeps.Add(a))
                    _dependsOn[a].Add(arrow);
            foreach (Arrow a in _leftHeadsByRow[cy])
                if (a.HeadCell.X > cx && revDeps.Add(a))
                    _dependsOn[a].Add(arrow);
            foreach (Arrow a in _upHeadsByCol[cx])
                if (a.HeadCell.Y < cy && revDeps.Add(a))
                    _dependsOn[a].Add(arrow);
            foreach (Arrow a in _downHeadsByCol[cx])
                if (a.HeadCell.Y > cy && revDeps.Add(a))
                    _dependsOn[a].Add(arrow);
        }
        _dependedOnBy[arrow] = revDeps;

        AddToRayIndex(arrow);

        // Prune candidates whose head/next cell is now occupied
        if (_candidateLookup != null)
        {
            HashSet<ArrowHeadData> toRemove = new();
            foreach (Cell c in arrow.Cells)
            {
                foreach (ArrowHeadData stale in _candidateLookup[c.X, c.Y])
                    toRemove.Add(stale);
                _candidateLookup[c.X, c.Y].Clear();
            }
            _availableArrowHeads.RemoveAll(toRemove.Contains);
        }
    }

    public void RemoveArrow(Arrow arrow)
    {
        if (arrow == null)
            throw new System.ArgumentNullException(nameof(arrow));
        if (!_arrowSet.Contains(arrow))
            throw new System.InvalidOperationException("Arrow is not on the board.");
        if (!IsClearable(arrow))
            throw new System.InvalidOperationException(
                "Arrow is not clearable — it has unresolved dependencies."
            );

        _arrows.Remove(arrow);
        _arrowSet.Remove(arrow);
        foreach (Cell c in arrow.Cells)
            _occupancy[c.X, c.Y] = null;
        OccupiedCellCount -= arrow.Cells.Count;

        _dependsOn.Remove(arrow);

        // Remove reverse edges: these depended on arrow
        if (_dependedOnBy.TryGetValue(arrow, out var revDeps))
        {
            foreach (Arrow depBy in revDeps)
                _dependsOn[depBy].Remove(arrow);
            _dependedOnBy.Remove(arrow);
        }

        RemoveFromRayIndex(arrow);
    }

    public bool Contains(Cell cell)
    {
        return cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
    }

    /// <summary>Returns the arrow occupying <paramref name="cell"/>, or null if empty or out of bounds.</summary>
    public Arrow GetArrowAt(Cell cell) => Contains(cell) ? _occupancy[cell.X, cell.Y] : null;

    /// <summary>
    /// Returns the first arrow hit by walking the ray from <paramref name="arrow"/>'s head
    /// in its <see cref="Arrow.HeadDirection"/>, or null if the ray is clear.
    /// </summary>
    public Arrow GetFirstInRay(Arrow arrow)
    {
        (int dx, int dy) = Arrow.GetDirectionStep(arrow.HeadDirection);
        Cell cursor = new(arrow.HeadCell.X + dx, arrow.HeadCell.Y + dy);
        while (Contains(cursor))
        {
            Arrow hit = _occupancy[cursor.X, cursor.Y];
            if (hit != null && hit != arrow)
                return hit;
            cursor = new(cursor.X + dx, cursor.Y + dy);
        }
        return null;
    }

    /// <summary>
    /// Returns true if <paramref name="arrow"/> can be cleared — no other arrow blocks its forward ray.
    /// </summary>
    public bool IsClearable(Arrow arrow) => _dependsOn[arrow].Count == 0;

    /// <summary>Returns the set of arrows that block <paramref name="arrow"/> from being cleared.</summary>
    internal HashSet<Arrow> GetDependencies(Arrow arrow) => _dependsOn[arrow];

    /// <summary>Returns the set of arrows that depend on <paramref name="arrow"/> (i.e., arrows that
    /// become clearable candidates when <paramref name="arrow"/> is removed).</summary>
    public IReadOnlyCollection<Arrow> GetDependents(Arrow arrow) =>
        _dependedOnBy.TryGetValue(arrow, out var deps)
            ? deps
            : (IReadOnlyCollection<Arrow>)System.Array.Empty<Arrow>();

    /// <summary>Returns whether <paramref name="target"/> lies strictly forward of <paramref name="head"/> along <paramref name="direction"/>.</summary>
    public static bool IsInRay(Cell target, Cell head, Arrow.Direction direction) =>
        direction switch
        {
            Arrow.Direction.Up => target.X == head.X && target.Y > head.Y,
            Arrow.Direction.Down => target.X == head.X && target.Y < head.Y,
            Arrow.Direction.Right => target.Y == head.Y && target.X > head.X,
            Arrow.Direction.Left => target.Y == head.Y && target.X < head.X,
            _ => false,
        };

    private void AddToRayIndex(Arrow arrow)
    {
        switch (arrow.HeadDirection)
        {
            case Arrow.Direction.Right:
                _rightHeadsByRow[arrow.HeadCell.Y].Add(arrow);
                break;
            case Arrow.Direction.Left:
                _leftHeadsByRow[arrow.HeadCell.Y].Add(arrow);
                break;
            case Arrow.Direction.Up:
                _upHeadsByCol[arrow.HeadCell.X].Add(arrow);
                break;
            case Arrow.Direction.Down:
                _downHeadsByCol[arrow.HeadCell.X].Add(arrow);
                break;
        }
    }

    private void RemoveFromRayIndex(Arrow arrow)
    {
        switch (arrow.HeadDirection)
        {
            case Arrow.Direction.Right:
                _rightHeadsByRow[arrow.HeadCell.Y].Remove(arrow);
                break;
            case Arrow.Direction.Left:
                _leftHeadsByRow[arrow.HeadCell.Y].Remove(arrow);
                break;
            case Arrow.Direction.Up:
                _upHeadsByCol[arrow.HeadCell.X].Remove(arrow);
                break;
            case Arrow.Direction.Down:
                _downHeadsByCol[arrow.HeadCell.X].Remove(arrow);
                break;
        }
    }

    /// <summary>
    /// Returns true if any arrow whose forward ray passes through <paramref name="cell"/>
    /// is contained in <paramref name="set"/>. Used by cycle detection during generation.
    /// </summary>
    internal bool AnyArrowWithRayThroughMatches(Cell cell, HashSet<Arrow> set)
    {
        int cx = cell.X, cy = cell.Y;

        foreach (Arrow a in _rightHeadsByRow[cy])
            if (a.HeadCell.X < cx && set.Contains(a))
                return true;
        foreach (Arrow a in _leftHeadsByRow[cy])
            if (a.HeadCell.X > cx && set.Contains(a))
                return true;
        foreach (Arrow a in _upHeadsByCol[cx])
            if (a.HeadCell.Y < cy && set.Contains(a))
                return true;
        foreach (Arrow a in _downHeadsByCol[cx])
            if (a.HeadCell.Y > cy && set.Contains(a))
                return true;

        return false;
    }

    private List<ArrowHeadData> CreateInitialArrowHeads()
    {
        List<ArrowHeadData> arrowHeads = new();

        void Add(ArrowHeadData candidate)
        {
            arrowHeads.Add(candidate);
            _candidateLookup[candidate.head.X, candidate.head.Y].Add(candidate);
            _candidateLookup[candidate.next.X, candidate.next.Y].Add(candidate);
        }

        for (int x = 0; x < Width - 1; x++)
        for (int y = 0; y < Height; y++)
            Add(
                new ArrowHeadData
                {
                    head = new(x + 1, y),
                    next = new(x, y),
                    direction = Arrow.Direction.Right,
                }
            );

        for (int x = 0; x < Width - 1; x++)
        for (int y = 0; y < Height; y++)
            Add(
                new ArrowHeadData
                {
                    head = new(x, y),
                    next = new(x + 1, y),
                    direction = Arrow.Direction.Left,
                }
            );

        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height - 1; y++)
            Add(
                new ArrowHeadData
                {
                    head = new(x, y + 1),
                    next = new(x, y),
                    direction = Arrow.Direction.Up,
                }
            );

        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height - 1; y++)
            Add(
                new ArrowHeadData
                {
                    head = new(x, y),
                    next = new(x, y + 1),
                    direction = Arrow.Direction.Down,
                }
            );

        return arrowHeads;
    }
}
