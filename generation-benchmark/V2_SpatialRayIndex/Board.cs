using System.Collections.Generic;

namespace V2
{
    public sealed class Board
    {
        private readonly List<Arrow> _arrows = new();
        private readonly HashSet<Arrow> _arrowSet = new();
        private readonly Arrow[,] _occupancy;
        private readonly Dictionary<Arrow, HashSet<Arrow>> _dependsOn = new();
        private readonly Dictionary<Arrow, HashSet<Arrow>> _dependedOnBy = new();

        // Spatial ray index
        private readonly List<Arrow>[] _rightHeadsByRow;
        private readonly List<Arrow>[] _leftHeadsByRow;
        private readonly List<Arrow>[] _upHeadsByCol;
        private readonly List<Arrow>[] _downHeadsByCol;

        internal List<ArrowHeadData> _availableArrowHeads;
        internal List<ArrowHeadData>[,] _candidateLookup;

        public IReadOnlyList<Arrow> Arrows => _arrows;
        public int Width { get; }
        public int Height { get; }
        public int OccupiedCellCount { get; private set; }

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
        }

        public void AddArrow(Arrow arrow)
        {
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

            _arrows.Add(arrow);
            _arrowSet.Add(arrow);
            foreach (Cell c in arrow.Cells)
                _occupancy[c.X, c.Y] = arrow;
            OccupiedCellCount += arrow.Cells.Count;

            // Forward deps
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

            // Reverse deps via spatial ray index
            var revDeps = new HashSet<Arrow>();
            foreach (Cell c in arrow.Cells)
            {
                int cx = c.X,
                    cy = c.Y;
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

            // Prune candidates
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

        public Arrow GetArrowAt(Cell cell) => Contains(cell) ? _occupancy[cell.X, cell.Y] : null;

        public bool IsClearable(Arrow arrow) => _dependsOn[arrow].Count == 0;

        internal HashSet<Arrow> GetDependencies(Arrow arrow) => _dependsOn[arrow];

        public static bool IsInRay(Cell target, Cell head, Arrow.Direction direction) =>
            direction switch
            {
                Arrow.Direction.Up => target.X == head.X && target.Y > head.Y,
                Arrow.Direction.Down => target.X == head.X && target.Y < head.Y,
                Arrow.Direction.Right => target.Y == head.Y && target.X > head.X,
                Arrow.Direction.Left => target.Y == head.Y && target.X < head.X,
                _ => false,
            };

        internal bool AnyArrowWithRayThroughMatches(Cell cell, HashSet<Arrow> set)
        {
            int cx = cell.X,
                cy = cell.Y;

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

    sealed class ArrowHeadData
    {
        public Cell head;
        public Cell next;
        public Arrow.Direction direction;
    }
}
