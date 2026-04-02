using System.Collections.Generic;

namespace V4
{
    public sealed class Board
    {
        private readonly List<Arrow> _arrows = new();
        private readonly HashSet<Arrow> _arrowSet = new();
        internal readonly Arrow[,] _occupancy;
        private readonly Dictionary<Arrow, HashSet<Arrow>> _dependsOn = new();
        private readonly Dictionary<Arrow, HashSet<Arrow>> _dependedOnBy = new();

        // Spatial ray index
        private readonly List<Arrow>[] _rightHeadsByRow;
        private readonly List<Arrow>[] _leftHeadsByRow;
        private readonly List<Arrow>[] _upHeadsByCol;
        private readonly List<Arrow>[] _downHeadsByCol;

        internal List<ArrowHeadData> _availableArrowHeads;

        // Bitset-based dependency storage for generation
        internal int _bitsetWords;
        internal ulong[] _depsBitsFlat;
        internal bool[] _hasAnyDeps;
        internal const int MaxNonZeroTracked = 16;
        internal int[] _depsNonZeroWords;
        internal int[] _depsNonZeroCount;
        internal int _nextGenIndex;

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
            _availableArrowHeads = CreateInitialArrowHeads();

            int maxArrows = Width * Height / 2;
            int estimatedCapacity = System.Math.Max(64, maxArrows / 4);
            _bitsetWords = (estimatedCapacity + 63) >> 6;
            _depsBitsFlat = new ulong[maxArrows * _bitsetWords];
            _hasAnyDeps = new bool[maxArrows];
            _depsNonZeroWords = new int[maxArrows * MaxNonZeroTracked];
            _depsNonZeroCount = new int[maxArrows];
            _nextGenIndex = 0;
        }

        internal void GrowBitsetCapacity()
        {
            int oldWords = _bitsetWords;
            int maxArrows = Width * Height / 2;
            _bitsetWords = System.Math.Min(oldWords * 2, (maxArrows + 63) >> 6);
            ulong[] newFlat = new ulong[maxArrows * _bitsetWords];
            for (int i = 0; i < _nextGenIndex; i++)
                System.Array.Copy(_depsBitsFlat, i * oldWords, newFlat, i * _bitsetWords, oldWords);
            _depsBitsFlat = newFlat;
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

            if (_depsBitsFlat != null && arrow._generationIndex < 0)
                arrow._generationIndex = _nextGenIndex++;

            _arrows.Add(arrow);
            _arrowSet.Add(arrow);
            foreach (Cell c in arrow.Cells)
                _occupancy[c.X, c.Y] = arrow;
            OccupiedCellCount += arrow.Cells.Count;

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

            if (_depsBitsFlat != null)
            {
                int nIdx = arrow._generationIndex;
                int offset = nIdx * _bitsetWords;
                foreach (Arrow dep in deps)
                {
                    int dIdx = dep._generationIndex;
                    if (dIdx >= 0)
                        _depsBitsFlat[offset + (dIdx >> 6)] |= 1UL << (dIdx & 63);
                }
            }

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

            if (_depsBitsFlat != null)
            {
                int nIdx = arrow._generationIndex;
                foreach (Arrow a in revDeps)
                {
                    int aIdx = a._generationIndex;
                    if (aIdx >= 0)
                        _depsBitsFlat[aIdx * _bitsetWords + (nIdx >> 6)] |= 1UL << (nIdx & 63);
                }
            }

            AddToRayIndex(arrow);
        }

        internal void AddArrowForGeneration(Arrow arrow)
        {
            arrow._generationIndex = _nextGenIndex++;
            if (arrow._generationIndex >= _bitsetWords * 64)
                GrowBitsetCapacity();

            _arrows.Add(arrow);
            _arrowSet.Add(arrow);
            foreach (Cell c in arrow.Cells)
                _occupancy[c.X, c.Y] = arrow;
            OccupiedCellCount += arrow.Cells.Count;

            int nIdx = arrow._generationIndex;
            int stride = _bitsetWords;

            bool hasDeps = false;
            (int dx, int dy) = Arrow.GetDirectionStep(arrow.HeadDirection);
            int cx = arrow.HeadCell.X + dx,
                cy = arrow.HeadCell.Y + dy;
            while (cx >= 0 && cx < Width && cy >= 0 && cy < Height)
            {
                Arrow hit = _occupancy[cx, cy];
                if (hit != null)
                {
                    int dIdx = hit._generationIndex;
                    if (dIdx >= 0)
                    {
                        int word = dIdx >> 6;
                        SetDepBit(nIdx, word, 1UL << (dIdx & 63));
                        hasDeps = true;
                    }
                }
                cx += dx;
                cy += dy;
            }
            _hasAnyDeps[nIdx] = hasDeps;

            ulong nBit = 1UL << (nIdx & 63);
            int nWord = nIdx >> 6;
            foreach (Cell c in arrow.Cells)
            {
                int cellX = c.X,
                    cellY = c.Y;
                foreach (Arrow a in _rightHeadsByRow[cellY])
                    if (a.HeadCell.X < cellX && a._generationIndex >= 0)
                        SetDepBit(a._generationIndex, nWord, nBit);
                foreach (Arrow a in _leftHeadsByRow[cellY])
                    if (a.HeadCell.X > cellX && a._generationIndex >= 0)
                        SetDepBit(a._generationIndex, nWord, nBit);
                foreach (Arrow a in _upHeadsByCol[cellX])
                    if (a.HeadCell.Y < cellY && a._generationIndex >= 0)
                        SetDepBit(a._generationIndex, nWord, nBit);
                foreach (Arrow a in _downHeadsByCol[cellX])
                    if (a.HeadCell.Y > cellY && a._generationIndex >= 0)
                        SetDepBit(a._generationIndex, nWord, nBit);
            }

            AddToRayIndex(arrow);
        }

        private void SetDepBit(int arrowIdx, int word, ulong bit)
        {
            int offset = arrowIdx * _bitsetWords + word;
            bool wasZero = _depsBitsFlat[offset] == 0;
            _depsBitsFlat[offset] |= bit;
            _hasAnyDeps[arrowIdx] = true;

            if (wasZero)
            {
                int count = _depsNonZeroCount[arrowIdx];
                if (count >= 0 && count < MaxNonZeroTracked)
                {
                    _depsNonZeroWords[arrowIdx * MaxNonZeroTracked + count] = word;
                    _depsNonZeroCount[arrowIdx] = count + 1;
                }
                else if (count >= 0)
                {
                    _depsNonZeroCount[arrowIdx] = -1;
                }
            }
        }

        internal void FinalizeGeneration()
        {
            foreach (Arrow arrow in _arrows)
            {
                _dependsOn[arrow] = new HashSet<Arrow>();
                _dependedOnBy[arrow] = new HashSet<Arrow>();
            }

            foreach (Arrow arrow in _arrows)
            {
                (int dx, int dy) = Arrow.GetDirectionStep(arrow.HeadDirection);
                int cx = arrow.HeadCell.X + dx,
                    cy = arrow.HeadCell.Y + dy;
                while (cx >= 0 && cx < Width && cy >= 0 && cy < Height)
                {
                    Arrow hit = _occupancy[cx, cy];
                    if (hit != null && hit != arrow)
                    {
                        _dependsOn[arrow].Add(hit);
                        _dependedOnBy[hit].Add(arrow);
                    }
                    cx += dx;
                    cy += dy;
                }
            }

            _depsBitsFlat = null;
            _hasAnyDeps = null;
            _depsNonZeroWords = null;
            _depsNonZeroCount = null;
            _availableArrowHeads = null;
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

        internal bool AnyArrowWithRayThroughBitset(Cell cell, ulong[] bitset)
        {
            int cx = cell.X,
                cy = cell.Y;

            foreach (Arrow a in _rightHeadsByRow[cy])
            {
                int idx = a._generationIndex;
                if (a.HeadCell.X < cx && idx >= 0 && (bitset[idx >> 6] & (1UL << (idx & 63))) != 0)
                    return true;
            }
            foreach (Arrow a in _leftHeadsByRow[cy])
            {
                int idx = a._generationIndex;
                if (a.HeadCell.X > cx && idx >= 0 && (bitset[idx >> 6] & (1UL << (idx & 63))) != 0)
                    return true;
            }
            foreach (Arrow a in _upHeadsByCol[cx])
            {
                int idx = a._generationIndex;
                if (a.HeadCell.Y < cy && idx >= 0 && (bitset[idx >> 6] & (1UL << (idx & 63))) != 0)
                    return true;
            }
            foreach (Arrow a in _downHeadsByCol[cx])
            {
                int idx = a._generationIndex;
                if (a.HeadCell.Y > cy && idx >= 0 && (bitset[idx >> 6] & (1UL << (idx & 63))) != 0)
                    return true;
            }

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

    struct ArrowHeadData
    {
        public Cell head;
        public Cell next;
        public Arrow.Direction direction;
    }
}
