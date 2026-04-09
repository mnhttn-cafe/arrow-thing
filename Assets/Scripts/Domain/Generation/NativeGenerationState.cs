using System;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// All generation-time state stored as NativeArrays for Burst compatibility.
/// Mirrors the flat arrays on <see cref="Board"/> (_depsBitsFlat, _genHeadX, etc.)
/// plus occupancy and spatial ray index in Burst-friendly layouts.
/// Disposable — caller must call <see cref="Dispose"/> when done.
/// </summary>
public struct NativeGenerationState : IDisposable
{
    public int boardWidth;
    public int boardHeight;
    public int maxArrows;
    public int bitsetWords;
    public int nextGenIndex;

    // Occupancy: flat [y * width + x], stores gen index (-1 = empty)
    public NativeArray<int> occupancy;

    // Candidate pool
    public NativeList<NativeArrowHeadData> candidates;

    // Bitset dependency storage
    public NativeArray<ulong> depsBitsFlat; // [arrowIndex * bitsetWords + word]
    public NativeArray<bool> hasAnyDeps;

    // Sparse nonzero word tracking
    public NativeArray<int> depsNonZeroWords; // [idx * MaxNonZeroTracked + i]
    public NativeArray<int> depsNonZeroCount; // [idx]

    // Arrow head/direction flat arrays
    public NativeArray<int> genHeadX;
    public NativeArray<int> genHeadY;
    public NativeArray<int> genDir; // Arrow.Direction cast to int

    // Spatial ray index: flat arrays with per-row/col counts
    // rightByRow[row * width + i] = genIndex, rightByRowCount[row] = count
    public NativeArray<int> rightByRow;
    public NativeArray<int> rightByRowCount;
    public NativeArray<int> leftByRow;
    public NativeArray<int> leftByRowCount;
    public NativeArray<int> upByCol;
    public NativeArray<int> upByColCount;
    public NativeArray<int> downByCol;
    public NativeArray<int> downByColCount;

    // Working context (reused across candidates)
    public NativeArray<ulong> ctxReachable;
    public NativeArray<ulong> ctxForwardDeps;
    public NativeArray<ulong> ctxFrontier;
    public NativeArray<bool> ctxVisited; // flat [y * width + x]
    public NativeArray<int> ctxDirOrder; // [4]

    // Scratch output for last generated arrow
    public NativeArray<int> scratchCellsX; // capacity = max possible arrow length
    public NativeArray<int> scratchCellsY;
    public int lastArrowCellCount;

    public NativeGenerationState(int width, int height, Allocator allocator)
    {
        boardWidth = width;
        boardHeight = height;
        maxArrows = width * height / 2;
        int estimatedCapacity = Math.Max(64, maxArrows / 4);
        bitsetWords = (estimatedCapacity + 63) >> 6;
        nextGenIndex = 0;
        lastArrowCellCount = 0;

        occupancy = new NativeArray<int>(width * height, allocator);
        for (int i = 0; i < occupancy.Length; i++)
            occupancy[i] = -1;

        candidates = new NativeList<NativeArrowHeadData>(width * height * 2, allocator);

        depsBitsFlat = new NativeArray<ulong>(maxArrows * bitsetWords, allocator);
        hasAnyDeps = new NativeArray<bool>(maxArrows, allocator);
        depsNonZeroWords = new NativeArray<int>(maxArrows * Board.MaxNonZeroTracked, allocator);
        depsNonZeroCount = new NativeArray<int>(maxArrows, allocator);

        genHeadX = new NativeArray<int>(maxArrows, allocator);
        genHeadY = new NativeArray<int>(maxArrows, allocator);
        genDir = new NativeArray<int>(maxArrows, allocator);

        // Spatial ray index: each row can hold at most 'width' arrow heads per direction
        rightByRow = new NativeArray<int>(height * width, allocator);
        rightByRowCount = new NativeArray<int>(height, allocator);
        leftByRow = new NativeArray<int>(height * width, allocator);
        leftByRowCount = new NativeArray<int>(height, allocator);
        upByCol = new NativeArray<int>(width * height, allocator);
        upByColCount = new NativeArray<int>(width, allocator);
        downByCol = new NativeArray<int>(width * height, allocator);
        downByColCount = new NativeArray<int>(width, allocator);

        // Working context
        ctxReachable = new NativeArray<ulong>(bitsetWords, allocator);
        ctxForwardDeps = new NativeArray<ulong>(bitsetWords, allocator);
        ctxFrontier = new NativeArray<ulong>(bitsetWords, allocator);
        ctxVisited = new NativeArray<bool>(width * height, allocator);
        ctxDirOrder = new NativeArray<int>(4, allocator);

        // Scratch output (max arrow length = min(width, height) * 2 is generous)
        int maxLen = width * height;
        scratchCellsX = new NativeArray<int>(maxLen, allocator);
        scratchCellsY = new NativeArray<int>(maxLen, allocator);
    }

    public void InitializeCandidates()
    {
        int w = boardWidth,
            h = boardHeight;
        // Right-facing: head at (x+1, y), next at (x, y)
        for (int x = 0; x < w - 1; x++)
        for (int y = 0; y < h; y++)
            candidates.Add(
                new NativeArrowHeadData
                {
                    headX = x + 1,
                    headY = y,
                    nextX = x,
                    nextY = y,
                    direction = (int)Arrow.Direction.Right,
                }
            );

        // Left-facing: head at (x, y), next at (x+1, y)
        for (int x = 0; x < w - 1; x++)
        for (int y = 0; y < h; y++)
            candidates.Add(
                new NativeArrowHeadData
                {
                    headX = x,
                    headY = y,
                    nextX = x + 1,
                    nextY = y,
                    direction = (int)Arrow.Direction.Left,
                }
            );

        // Up-facing: head at (x, y+1), next at (x, y)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h - 1; y++)
            candidates.Add(
                new NativeArrowHeadData
                {
                    headX = x,
                    headY = y + 1,
                    nextX = x,
                    nextY = y,
                    direction = (int)Arrow.Direction.Up,
                }
            );

        // Down-facing: head at (x, y), next at (x, y+1)
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h - 1; y++)
            candidates.Add(
                new NativeArrowHeadData
                {
                    headX = x,
                    headY = y,
                    nextX = x,
                    nextY = y + 1,
                    direction = (int)Arrow.Direction.Down,
                }
            );
    }

    /// <summary>
    /// Doubles bitset stride capacity, reallocating the flat dep arrays.
    /// </summary>
    public void GrowBitsetCapacity(Allocator allocator)
    {
        int oldWords = bitsetWords;
        bitsetWords = Math.Min(oldWords * 2, (maxArrows + 63) >> 6);

        var newFlat = new NativeArray<ulong>(maxArrows * bitsetWords, allocator);
        for (int i = 0; i < nextGenIndex; i++)
        {
            for (int w = 0; w < oldWords; w++)
                newFlat[i * bitsetWords + w] = depsBitsFlat[i * oldWords + w];
        }
        depsBitsFlat.Dispose();
        depsBitsFlat = newFlat;

        // Grow context arrays
        var newReachable = new NativeArray<ulong>(bitsetWords, allocator);
        var newForward = new NativeArray<ulong>(bitsetWords, allocator);
        var newFrontier = new NativeArray<ulong>(bitsetWords, allocator);
        ctxReachable.Dispose();
        ctxForwardDeps.Dispose();
        ctxFrontier.Dispose();
        ctxReachable = newReachable;
        ctxForwardDeps = newForward;
        ctxFrontier = newFrontier;
    }

    public void Dispose()
    {
        occupancy.Dispose();
        candidates.Dispose();
        depsBitsFlat.Dispose();
        hasAnyDeps.Dispose();
        depsNonZeroWords.Dispose();
        depsNonZeroCount.Dispose();
        genHeadX.Dispose();
        genHeadY.Dispose();
        genDir.Dispose();
        rightByRow.Dispose();
        rightByRowCount.Dispose();
        leftByRow.Dispose();
        leftByRowCount.Dispose();
        upByCol.Dispose();
        upByColCount.Dispose();
        downByCol.Dispose();
        downByColCount.Dispose();
        ctxReachable.Dispose();
        ctxForwardDeps.Dispose();
        ctxFrontier.Dispose();
        ctxVisited.Dispose();
        ctxDirOrder.Dispose();
        scratchCellsX.Dispose();
        scratchCellsY.Dispose();
    }
}

/// <summary>Blittable mirror of <see cref="ArrowHeadData"/> for use in NativeContainers.</summary>
public struct NativeArrowHeadData
{
    public int headX,
        headY;
    public int nextX,
        nextY;
    public int direction; // Arrow.Direction cast to int
}
