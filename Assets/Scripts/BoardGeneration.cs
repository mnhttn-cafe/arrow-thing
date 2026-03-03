using System.Collections.Generic;

public static class BoardGeneration
{
    public static void FillBoard(Board board, int minLength, int maxLength)
    {
        int maxPossibleArrows = board.Width * board.Height / 2;
        GenerateArrows(board, minLength, maxLength, maxPossibleArrows, out _);
    }

    public static bool GenerateArrows(Board board, int minLength, int maxLength, int amount, out int createdArrows)
    {
        createdArrows = 0;
        while (createdArrows < amount && TryGenerateArrow(board, minLength, maxLength, out Arrow arrow))
        {
            board.Arrows.Add(arrow);
            createdArrows++;
        }
        return createdArrows == amount;
    }

    public static bool TryGenerateArrow(Board board, int minLength, int maxLength, out Arrow arrow)
    {
        arrow = null;
        while (minLength <= maxLength)
        {
            int targetLength = maxLength;
            List<Arrow> validHeads = GetValidArrowHeads(board, targetLength);
            if (validHeads.Count == 0)
            {
                maxLength = targetLength - 1;
                continue;
            }

            Shuffle(validHeads);
            while (validHeads.Count > 0)
            {
                Arrow candidateArrowHead = validHeads[^1];
                if (TryCompleteArrowTail(board, targetLength, candidateArrowHead, out arrow)) return true;
                validHeads.RemoveAt(validHeads.Count - 1);
            }
        }
        arrow = null;
        return false;
    }

    public static List<Arrow> GetValidArrowHeads(Board board, int targetLength)
    {
        return new();
    }

    public static bool TryCompleteArrowTail(Board board, int targetLength, Arrow head, out Arrow arrow)
    {
        arrow = head;
        return false;
    }

    public static void Shuffle<T>(List<T> list)
    {
    }
}