using System.Collections.Generic;

namespace V1
{
    public sealed class Board
    {
        private readonly List<Arrow> _arrows = new();
        private readonly Arrow[,] _occupancy;
        private readonly Dictionary<Arrow, HashSet<Arrow>> _dependsOn = new();
        private readonly Dictionary<Arrow, HashSet<Arrow>> _dependedOnBy = new();

        // Generation candidate pool (null until InitializeForGeneration)
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
        }

        public void InitializeForGeneration()
        {
            _availableArrowHeads = CreateInitialArrowHeads();
            _candidateLookup = new List<ArrowHeadData>[Width, Height];
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                _candidateLookup[x, y] = new List<ArrowHeadData>();
            foreach (ArrowHeadData candidate in _availableArrowHeads)
            {
                _candidateLookup[candidate.head.X, candidate.head.Y].Add(candidate);
                _candidateLookup[candidate.next.X, candidate.next.Y].Add(candidate);
            }
        }

        public void AddArrow(Arrow arrow)
        {
            if (arrow == null)
                throw new System.ArgumentNullException(nameof(arrow));
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

            // Reverse deps: existing arrows whose rays pass through this arrow's cells now depend on it
            var revDeps = new HashSet<Arrow>();
            foreach (Arrow existing in _arrows)
            {
                if (existing == arrow)
                    continue;
                foreach (Cell c in arrow.Cells)
                {
                    if (IsInRay(c, existing.HeadCell, existing.HeadDirection))
                    {
                        _dependsOn[existing].Add(arrow);
                        revDeps.Add(existing);
                        break;
                    }
                }
            }
            _dependedOnBy[arrow] = revDeps;

            // Prune candidates if generation was initialized
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
            if (!IsClearable(arrow))
                throw new System.InvalidOperationException(
                    "Arrow is not clearable — it has unresolved dependencies."
                );

            _arrows.Remove(arrow);
            foreach (Cell c in arrow.Cells)
                _occupancy[c.X, c.Y] = null;
            OccupiedCellCount -= arrow.Cells.Count;

            if (_dependsOn.TryGetValue(arrow, out var deps))
            {
                foreach (Arrow dep in deps)
                    _dependedOnBy[dep].Remove(arrow);
                _dependsOn.Remove(arrow);
            }

            if (_dependedOnBy.TryGetValue(arrow, out var revDeps))
            {
                foreach (Arrow depBy in revDeps)
                    _dependsOn[depBy].Remove(arrow);
                _dependedOnBy.Remove(arrow);
            }
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

        private List<ArrowHeadData> CreateInitialArrowHeads()
        {
            List<ArrowHeadData> arrowHeads = new();

            for (int x = 0; x < Width - 1; x++)
            for (int y = 0; y < Height; y++)
                arrowHeads.Add(
                    new ArrowHeadData
                    {
                        head = new(x + 1, y),
                        next = new(x, y),
                        direction = Arrow.Direction.Right,
                    }
                );

            for (int x = 0; x < Width - 1; x++)
            for (int y = 0; y < Height; y++)
                arrowHeads.Add(
                    new ArrowHeadData
                    {
                        head = new(x, y),
                        next = new(x + 1, y),
                        direction = Arrow.Direction.Left,
                    }
                );

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height - 1; y++)
                arrowHeads.Add(
                    new ArrowHeadData
                    {
                        head = new(x, y + 1),
                        next = new(x, y),
                        direction = Arrow.Direction.Up,
                    }
                );

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height - 1; y++)
                arrowHeads.Add(
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
