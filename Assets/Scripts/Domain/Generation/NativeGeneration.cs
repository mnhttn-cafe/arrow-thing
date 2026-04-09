using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Burst-compiled board generation kernels. All methods operate on
/// <see cref="NativeGenerationState"/> and produce arrow cell data in
/// the state's scratch buffers for the managed caller to consume.
/// </summary>
[BurstCompile]
public static class NativeGeneration
{
    private const int MinArrowLength = 2;

    /// <summary>
    /// Attempts to generate one arrow. On success, the arrow's cells are written to
    /// <c>state.scratchCellsX/Y[0..state.lastArrowCellCount]</c> and the arrow is
    /// placed into native state (occupancy, bitsets, ray index). Returns true on success.
    /// </summary>
    [BurstCompile]
    public static bool TryGenerateArrow(
        ref NativeGenerationState state,
        int maxLength,
        ref Random rng
    )
    {
        int targetLength = rng.NextInt(MinArrowLength, maxLength + 1);
        int w = state.boardWidth,
            h = state.boardHeight;

        while (state.candidates.Length > 0)
        {
            int headIndex = rng.NextInt(0, state.candidates.Length);
            NativeArrowHeadData candidate = state.candidates[headIndex];

            // Reject if head or next occupied
            if (
                state.occupancy[candidate.headY * w + candidate.headX] >= 0
                || state.occupancy[candidate.nextY * w + candidate.nextX] >= 0
            )
            {
                SwapRemoveCandidates(ref state, headIndex);
                continue;
            }

            // Compute forward deps as bitset
            int activeWords = (state.nextGenIndex + 63) >> 6;
            ClearWords(state.ctxForwardDeps, activeWords);
            ClearWords(state.ctxReachable, activeWords);
            int depCount = ComputeForwardDeps(
                ref state,
                candidate.headX,
                candidate.headY,
                candidate.direction,
                activeWords
            );

            bool hasReachable = depCount > 0;
            if (hasReachable)
            {
                // Check if any forward dep has deps — if all are leaves, skip BFS
                bool needBFS = false;
                for (int iw = 0; iw < activeWords && !needBFS; iw++)
                {
                    ulong bits = state.ctxForwardDeps[iw];
                    while (bits != 0)
                    {
                        int bit = math.tzcnt(bits);
                        if (state.hasAnyDeps[(iw << 6) | bit])
                        {
                            needBFS = true;
                            break;
                        }
                        bits &= bits - 1;
                    }
                }

                bool hasCycle;
                if (needBFS)
                {
                    hasCycle = ComputeReachableSetEarlyAbort(
                        ref state,
                        candidate.headX,
                        candidate.headY,
                        candidate.nextX,
                        candidate.nextY,
                        activeWords
                    );
                }
                else
                {
                    // Reachable = forward deps (all leaves, no BFS needed)
                    for (int iw = 0; iw < activeWords; iw++)
                        state.ctxReachable[iw] = state.ctxForwardDeps[iw];
                    hasCycle =
                        AnyArrowWithRayThroughBitset(
                            ref state,
                            candidate.headX,
                            candidate.headY,
                            state.ctxReachable,
                            activeWords
                        )
                        || AnyArrowWithRayThroughBitset(
                            ref state,
                            candidate.nextX,
                            candidate.nextY,
                            state.ctxReachable,
                            activeWords
                        );
                }

                if (hasCycle)
                {
                    SwapRemoveCandidates(ref state, headIndex);
                    continue;
                }
            }

            int cellCount = GreedyWalk(
                ref state,
                targetLength,
                candidate,
                ref rng,
                hasReachable,
                activeWords
            );
            if (cellCount < MinArrowLength)
            {
                SwapRemoveCandidates(ref state, headIndex);
                continue;
            }

            // Place arrow in native state
            PlaceArrow(ref state, cellCount, candidate.direction);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Greedy random walk to build an arrow tail. Writes cells to state scratch buffers.
    /// Returns the number of cells in the path.
    /// </summary>
    private static int GreedyWalk(
        ref NativeGenerationState state,
        int targetLength,
        NativeArrowHeadData headData,
        ref Random rng,
        bool hasReachable,
        int activeWords
    )
    {
        int w = state.boardWidth,
            h = state.boardHeight;

        // Initialize path with head and next
        state.scratchCellsX[0] = headData.headX;
        state.scratchCellsY[0] = headData.headY;
        state.scratchCellsX[1] = headData.nextX;
        state.scratchCellsY[1] = headData.nextY;
        int pathLen = 2;

        state.ctxVisited[headData.headY * w + headData.headX] = true;
        state.ctxVisited[headData.nextY * w + headData.nextX] = true;

        // Pre-mark ray cells in visited
        GetDirectionStep(headData.direction, out int rdx, out int rdy);
        int rx = headData.headX + rdx,
            ry = headData.headY + rdy;
        while (rx >= 0 && rx < w && ry >= 0 && ry < h)
        {
            state.ctxVisited[ry * w + rx] = true;
            rx += rdx;
            ry += rdy;
        }

        int curX = headData.nextX,
            curY = headData.nextY;

        while (pathLen < targetLength)
        {
            // Fisher-Yates shuffle on 4 directions
            state.ctxDirOrder[0] = 0;
            state.ctxDirOrder[1] = 1;
            state.ctxDirOrder[2] = 2;
            state.ctxDirOrder[3] = 3;
            for (int i = 3; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                int tmp = state.ctxDirOrder[i];
                state.ctxDirOrder[i] = state.ctxDirOrder[j];
                state.ctxDirOrder[j] = tmp;
            }

            bool found = false;
            for (int i = 0; i < 4; i++)
            {
                int nx,
                    ny;
                switch (state.ctxDirOrder[i])
                {
                    case 0:
                        nx = curX + 1;
                        ny = curY;
                        break;
                    case 1:
                        nx = curX - 1;
                        ny = curY;
                        break;
                    case 2:
                        nx = curX;
                        ny = curY + 1;
                        break;
                    default:
                        nx = curX;
                        ny = curY - 1;
                        break;
                }

                if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                    continue;
                int flatIdx = ny * w + nx;
                if (state.ctxVisited[flatIdx])
                    continue;
                if (state.occupancy[flatIdx] >= 0)
                    continue;
                if (
                    hasReachable
                    && AnyArrowWithRayThroughBitset(
                        ref state,
                        nx,
                        ny,
                        state.ctxReachable,
                        activeWords
                    )
                )
                    continue;

                state.scratchCellsX[pathLen] = nx;
                state.scratchCellsY[pathLen] = ny;
                pathLen++;
                state.ctxVisited[flatIdx] = true;
                curX = nx;
                curY = ny;
                found = true;
                break;
            }

            if (!found)
                break;
        }

        // Clean up visited: path cells
        for (int i = 0; i < pathLen; i++)
            state.ctxVisited[state.scratchCellsY[i] * w + state.scratchCellsX[i]] = false;
        // Clean up visited: ray cells
        rx = headData.headX + rdx;
        ry = headData.headY + rdy;
        while (rx >= 0 && rx < w && ry >= 0 && ry < h)
        {
            state.ctxVisited[ry * w + rx] = false;
            rx += rdx;
            ry += rdy;
        }

        return pathLen;
    }

    /// <summary>
    /// Collects all distinct arrows in the forward ray into the ctxForwardDeps bitset.
    /// Returns the count.
    /// </summary>
    private static int ComputeForwardDeps(
        ref NativeGenerationState state,
        int headX,
        int headY,
        int direction,
        int activeWords
    )
    {
        int count = 0;
        GetDirectionStep(direction, out int dx, out int dy);
        int cx = headX + dx,
            cy = headY + dy;
        int w = state.boardWidth,
            h = state.boardHeight;

        while (cx >= 0 && cx < w && cy >= 0 && cy < h)
        {
            int hitIdx = state.occupancy[cy * w + cx];
            if (hitIdx >= 0)
            {
                int word = hitIdx >> 6;
                ulong bit = 1UL << (hitIdx & 63);
                if ((state.ctxForwardDeps[word] & bit) == 0)
                {
                    state.ctxForwardDeps[word] = state.ctxForwardDeps[word] | bit;
                    count++;
                }
            }
            cx += dx;
            cy += dy;
        }
        return count;
    }

    /// <summary>
    /// BFS transitive closure with inline early cycle detection.
    /// Returns true if any reachable arrow's ray crosses head or next (cycle).
    /// </summary>
    private static bool ComputeReachableSetEarlyAbort(
        ref NativeGenerationState state,
        int headX,
        int headY,
        int nextX,
        int nextY,
        int activeWords
    )
    {
        int stride = state.bitsetWords;

        for (int iw = 0; iw < activeWords; iw++)
        {
            state.ctxReachable[iw] = state.ctxForwardDeps[iw];
            state.ctxFrontier[iw] = state.ctxForwardDeps[iw];
        }

        // Check level 0 (forward deps themselves)
        if (
            AnyArrowWithRayThroughBitset(ref state, headX, headY, state.ctxReachable, activeWords)
            || AnyArrowWithRayThroughBitset(
                ref state,
                nextX,
                nextY,
                state.ctxReachable,
                activeWords
            )
        )
            return true;

        while (true)
        {
            bool hasNext = false;
            for (int iw = 0; iw < activeWords; iw++)
            {
                ulong bits = state.ctxFrontier[iw];
                if (bits == 0)
                    continue;
                state.ctxFrontier[iw] = 0;

                while (bits != 0)
                {
                    int bit = math.tzcnt(bits);
                    int idx = (iw << 6) | bit;
                    int offset = idx * stride;
                    int nzCount = state.depsNonZeroCount[idx];

                    if (nzCount >= 0 && nzCount <= Board.MaxNonZeroTracked)
                    {
                        int nzBase = idx * Board.MaxNonZeroTracked;
                        for (int i = 0; i < nzCount; i++)
                        {
                            int ww = state.depsNonZeroWords[nzBase + i];
                            ulong newBits =
                                state.depsBitsFlat[offset + ww] & ~state.ctxReachable[ww];
                            if (newBits != 0)
                            {
                                state.ctxReachable[ww] = state.ctxReachable[ww] | newBits;
                                state.ctxFrontier[ww] = state.ctxFrontier[ww] | newBits;
                                hasNext = true;
                                if (
                                    CheckNewBitsForCycle(
                                        ref state,
                                        newBits,
                                        ww,
                                        headX,
                                        headY,
                                        nextX,
                                        nextY
                                    )
                                )
                                    return true;
                            }
                        }
                    }
                    else
                    {
                        for (int ww = 0; ww < activeWords; ww++)
                        {
                            ulong newBits =
                                state.depsBitsFlat[offset + ww] & ~state.ctxReachable[ww];
                            if (newBits != 0)
                            {
                                state.ctxReachable[ww] = state.ctxReachable[ww] | newBits;
                                state.ctxFrontier[ww] = state.ctxFrontier[ww] | newBits;
                                hasNext = true;
                                if (
                                    CheckNewBitsForCycle(
                                        ref state,
                                        newBits,
                                        ww,
                                        headX,
                                        headY,
                                        nextX,
                                        nextY
                                    )
                                )
                                    return true;
                            }
                        }
                    }
                    bits &= bits - 1;
                }
            }

            if (!hasNext)
                break;
        }

        return false;
    }

    /// <summary>
    /// Checks each newly discovered arrow in the BFS for a cycle
    /// (its ray crosses head or next).
    /// </summary>
    private static bool CheckNewBitsForCycle(
        ref NativeGenerationState state,
        ulong newBits,
        int word,
        int headX,
        int headY,
        int nextX,
        int nextY
    )
    {
        while (newBits != 0)
        {
            int b = math.tzcnt(newBits);
            int newIdx = (word << 6) | b;
            if (
                IsInRayOf(
                    state.genHeadX[newIdx],
                    state.genHeadY[newIdx],
                    state.genDir[newIdx],
                    headX,
                    headY
                )
                || IsInRayOf(
                    state.genHeadX[newIdx],
                    state.genHeadY[newIdx],
                    state.genDir[newIdx],
                    nextX,
                    nextY
                )
            )
                return true;
            newBits &= newBits - 1;
        }
        return false;
    }

    /// <summary>
    /// Returns true if any arrow in the bitset has a ray passing through (cx, cy).
    /// Uses the spatial ray index for efficient lookup.
    /// </summary>
    private static bool AnyArrowWithRayThroughBitset(
        ref NativeGenerationState state,
        int cx,
        int cy,
        NativeArray<ulong> bitset,
        int activeWords
    )
    {
        int w = state.boardWidth;

        // Right-facing arrows on this row with headX < cx
        int rowBase = cy * w;
        int count = state.rightByRowCount[cy];
        for (int i = 0; i < count; i++)
        {
            int idx = state.rightByRow[rowBase + i];
            if (state.genHeadX[idx] < cx && (bitset[idx >> 6] & (1UL << (idx & 63))) != 0)
                return true;
        }

        // Left-facing arrows on this row with headX > cx
        count = state.leftByRowCount[cy];
        for (int i = 0; i < count; i++)
        {
            int idx = state.leftByRow[rowBase + i];
            if (state.genHeadX[idx] > cx && (bitset[idx >> 6] & (1UL << (idx & 63))) != 0)
                return true;
        }

        // Up-facing arrows on this column with headY < cy
        int colBase = cx * state.boardHeight;
        count = state.upByColCount[cx];
        for (int i = 0; i < count; i++)
        {
            int idx = state.upByCol[colBase + i];
            if (state.genHeadY[idx] < cy && (bitset[idx >> 6] & (1UL << (idx & 63))) != 0)
                return true;
        }

        // Down-facing arrows on this column with headY > cy
        count = state.downByColCount[cx];
        for (int i = 0; i < count; i++)
        {
            int idx = state.downByCol[colBase + i];
            if (state.genHeadY[idx] > cy && (bitset[idx >> 6] & (1UL << (idx & 63))) != 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Places a generated arrow into native state: occupancy, bitsets, ray index.
    /// Reads cells from scratchCellsX/Y[0..cellCount].
    /// </summary>
    private static void PlaceArrow(ref NativeGenerationState state, int cellCount, int direction)
    {
        int nIdx = state.nextGenIndex++;
        if (nIdx >= state.bitsetWords * 64)
            state.GrowBitsetCapacity(Allocator.Persistent);

        int w = state.boardWidth,
            h = state.boardHeight;
        int headX = state.scratchCellsX[0];
        int headY = state.scratchCellsY[0];

        state.genHeadX[nIdx] = headX;
        state.genHeadY[nIdx] = headY;
        state.genDir[nIdx] = direction;

        // Set occupancy
        for (int i = 0; i < cellCount; i++)
            state.occupancy[state.scratchCellsY[i] * w + state.scratchCellsX[i]] = nIdx;

        // Forward deps: walk ray, set bits
        bool hasDeps = false;
        GetDirectionStep(direction, out int dx, out int dy);
        int cx = headX + dx,
            cy = headY + dy;
        while (cx >= 0 && cx < w && cy >= 0 && cy < h)
        {
            int hitIdx = state.occupancy[cy * w + cx];
            if (hitIdx >= 0 && hitIdx != nIdx)
            {
                int dWord = hitIdx >> 6;
                SetDepBit(ref state, nIdx, dWord, 1UL << (hitIdx & 63));
                hasDeps = true;
            }
            cx += dx;
            cy += dy;
        }
        state.hasAnyDeps[nIdx] = hasDeps;

        // Reverse deps: existing arrows whose rays cross this arrow's cells
        ulong nBit = 1UL << (nIdx & 63);
        int nWord = nIdx >> 6;
        for (int i = 0; i < cellCount; i++)
        {
            int cellX = state.scratchCellsX[i];
            int cellY = state.scratchCellsY[i];

            int rowBase = cellY * w;
            int count = state.rightByRowCount[cellY];
            for (int j = 0; j < count; j++)
            {
                int aIdx = state.rightByRow[rowBase + j];
                if (state.genHeadX[aIdx] < cellX)
                    SetDepBit(ref state, aIdx, nWord, nBit);
            }

            count = state.leftByRowCount[cellY];
            for (int j = 0; j < count; j++)
            {
                int aIdx = state.leftByRow[rowBase + j];
                if (state.genHeadX[aIdx] > cellX)
                    SetDepBit(ref state, aIdx, nWord, nBit);
            }

            int colBase = cellX * h;
            count = state.upByColCount[cellX];
            for (int j = 0; j < count; j++)
            {
                int aIdx = state.upByCol[colBase + j];
                if (state.genHeadY[aIdx] < cellY)
                    SetDepBit(ref state, aIdx, nWord, nBit);
            }

            count = state.downByColCount[cellX];
            for (int j = 0; j < count; j++)
            {
                int aIdx = state.downByCol[colBase + j];
                if (state.genHeadY[aIdx] > cellY)
                    SetDepBit(ref state, aIdx, nWord, nBit);
            }
        }

        // Add to ray index
        AddToRayIndex(ref state, nIdx, headX, headY, direction);

        state.lastArrowCellCount = cellCount;
    }

    private static void SetDepBit(
        ref NativeGenerationState state,
        int arrowIdx,
        int word,
        ulong bit
    )
    {
        int offset = arrowIdx * state.bitsetWords + word;
        bool wasZero = state.depsBitsFlat[offset] == 0;
        state.depsBitsFlat[offset] = state.depsBitsFlat[offset] | bit;
        state.hasAnyDeps[arrowIdx] = true;

        if (wasZero)
        {
            int count = state.depsNonZeroCount[arrowIdx];
            if (count >= 0 && count < Board.MaxNonZeroTracked)
            {
                state.depsNonZeroWords[arrowIdx * Board.MaxNonZeroTracked + count] = word;
                state.depsNonZeroCount[arrowIdx] = count + 1;
            }
            else if (count >= 0)
            {
                state.depsNonZeroCount[arrowIdx] = -1;
            }
        }
    }

    private static void AddToRayIndex(
        ref NativeGenerationState state,
        int genIdx,
        int headX,
        int headY,
        int direction
    )
    {
        int w = state.boardWidth,
            h = state.boardHeight;
        switch (direction)
        {
            case 0: // Up
            {
                int col = headX;
                int count = state.upByColCount[col];
                state.upByCol[col * h + count] = genIdx;
                state.upByColCount[col] = count + 1;
                break;
            }
            case 1: // Right
            {
                int row = headY;
                int count = state.rightByRowCount[row];
                state.rightByRow[row * w + count] = genIdx;
                state.rightByRowCount[row] = count + 1;
                break;
            }
            case 2: // Down
            {
                int col = headX;
                int count = state.downByColCount[col];
                state.downByCol[col * h + count] = genIdx;
                state.downByColCount[col] = count + 1;
                break;
            }
            case 3: // Left
            {
                int row = headY;
                int count = state.leftByRowCount[row];
                state.leftByRow[row * w + count] = genIdx;
                state.leftByRowCount[row] = count + 1;
                break;
            }
        }
    }

    private static bool IsInRayOf(int ax, int ay, int dir, int cx, int cy)
    {
        switch (dir)
        {
            case 0:
                return cx == ax && cy > ay; // Up
            case 1:
                return cy == ay && cx > ax; // Right
            case 2:
                return cx == ax && cy < ay; // Down
            case 3:
                return cy == ay && cx < ax; // Left
            default:
                return false;
        }
    }

    private static void GetDirectionStep(int direction, out int dx, out int dy)
    {
        switch (direction)
        {
            case 0:
                dx = 0;
                dy = 1;
                break; // Up
            case 1:
                dx = 1;
                dy = 0;
                break; // Right
            case 2:
                dx = 0;
                dy = -1;
                break; // Down
            case 3:
                dx = -1;
                dy = 0;
                break; // Left
            default:
                dx = 0;
                dy = 0;
                break;
        }
    }

    private static void SwapRemoveCandidates(ref NativeGenerationState state, int index)
    {
        state.candidates.RemoveAtSwapBack(index);
    }

    private static void ClearWords(NativeArray<ulong> arr, int count)
    {
        for (int i = 0; i < count; i++)
            arr[i] = 0;
    }
}
