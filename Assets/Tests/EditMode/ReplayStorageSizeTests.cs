using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

[TestFixture]
public class ReplayStorageSizeTests
{
    /// <summary>
    /// Generates a board, simulates a full clear sequence, serializes the resulting
    /// ReplayData to JSON, and logs the byte size. Run manually via Test Runner to
    /// inform compression decisions.
    /// </summary>
    [Explicit]
    [TestCase(10, 10)]
    [TestCase(20, 20)]
    [TestCase(40, 40)]
    [TestCase(100, 100)]
    [TestCase(200, 200)]
    public void MeasureReplaySize(int width, int height)
    {
        var random = new System.Random(42);
        var board = new Board(width, height);
        TestBoardHelper.FillBoard(board, 2, width > 20 ? 10 : 5, random);

        int arrowCount = board.Arrows.Count;

        // Build board snapshot
        var snapshot = new List<List<Cell>>();
        foreach (var arrow in board.Arrows)
            snapshot.Add(new List<Cell>(arrow.Cells));

        // Simulate clear sequence and build replay events
        var recorder = new ReplayRecorder();
        recorder.RecordSessionStart();
        recorder.RecordStartSolve();

        // Clear all arrows in a valid order
        int cleared = 0;
        while (board.Arrows.Count > 0)
        {
            Arrow toClear = null;
            foreach (var arrow in board.Arrows)
            {
                if (board.IsClearable(arrow))
                {
                    toClear = arrow;
                    break;
                }
            }
            Assert.IsNotNull(toClear, "Board should be fully solvable");

            var head = toClear.HeadCell;
            recorder.RecordClear(head.X, head.Y);
            board.RemoveArrow(toClear);
            cleared++;
        }

        recorder.RecordEndSolve();

        var data = recorder.ToReplayData(
            "test-game",
            42,
            width,
            height,
            width > 20 ? 10 : 5,
            15f,
            boardSnapshot: snapshot,
            finalTime: 60.0,
            gameVersion: "1.0.0"
        );

        string json = data.ToJson();
        int byteSize = Encoding.UTF8.GetByteCount(json);
        double kb = byteSize / 1024.0;
        double mb = kb / 1024.0;

        // Log results
        TestContext.WriteLine($"Board: {width}x{height}");
        TestContext.WriteLine($"  Arrows: {arrowCount}");
        TestContext.WriteLine($"  Clear events: {cleared}");
        TestContext.WriteLine($"  Snapshot cells: {snapshot.Count} arrows");
        TestContext.WriteLine($"  JSON size: {byteSize:N0} bytes ({kb:F1} KB / {mb:F3} MB)");
        TestContext.WriteLine($"  Per 50 entries: {kb * 50:F1} KB ({mb * 50:F3} MB)");

        // Sanity check — just ensure it serialized
        Assert.Greater(byteSize, 0);
    }
}
