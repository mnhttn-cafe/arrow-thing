/// <summary>
/// Test utilities for board generation. Drains <see cref="BoardGeneration.FillBoardIncremental"/>
/// synchronously so tests exercise the same code path as production.
/// </summary>
public static class TestBoardHelper
{
    public static void FillBoard(
        Board board,
        int minLength,
        int maxLength,
        System.Random random,
        int deadEndLimit = 10
    )
    {
        var enumerator = BoardGeneration.FillBoardIncremental(
            board,
            minLength,
            maxLength,
            random,
            deadEndLimit: deadEndLimit
        );
        while (enumerator.MoveNext()) { }
    }
}
