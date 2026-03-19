using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// PlayMode tests verifying that GameTimerView uses DateTimeOffset.UtcNow (wall-clock
/// time) rather than Unity internal time, so the timer advances correctly regardless of
/// how Unity's time progresses.
///
/// In WebGL, Unity's requestAnimationFrame-based loop is suspended when the browser tab
/// is hidden, which freezes both Time.timeAsDouble and Time.realtimeSinceStartupAsDouble.
/// DateTimeOffset.UtcNow maps to JS Date.now() and continues to advance during tab-out,
/// correctly expiring the inspection countdown and accumulating solve time.
///
/// Time.timeScale = 0 freezes Time.timeAsDouble to confirm that the timer is unaffected
/// by Unity's internal time scale. The WebGL-specific suspended-loop scenario requires
/// browser testing.
/// </summary>
[TestFixture]
public class GameTimerViewTests
{
    private const string GameHudUxmlPath = "Assets/UI/GameHud.uxml";
    private const string PanelSettingsPath = "Assets/Settings/UI/PanelSettings.asset";
    private const float RealWaitSeconds = 0.2f;

    private GameObject _host;
    private float _originalTimeScale;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _originalTimeScale = Time.timeScale;
        _host = new GameObject("GameTimerViewTestHost");
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = _originalTimeScale;
        if (_host != null)
            Object.DestroyImmediate(_host);
        yield return null;
    }

    /// <summary>
    /// Solve timer must keep running while Time.timeScale is 0.
    /// DateTimeOffset.UtcNow is unaffected by Unity's time scale, so SolveElapsed
    /// continues to grow even when Unity internal time is frozen.
    /// </summary>
    [UnityTest]
    public IEnumerator SolveTimer_ContinuesAdvancing_WhenTimescaleIsZero()
    {
        var timer = CreateAndInitTimer(inspectionDuration: 0.0, out _);

        yield return null; // First Update: zero-duration inspection expires → Solving

        Assert.That(
            timer.CurrentPhase,
            Is.EqualTo(GameTimer.Phase.Solving),
            "Timer should enter Solving phase immediately after zero-duration inspection"
        );

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(RealWaitSeconds);
        Time.timeScale = 1f;

        yield return null; // One more Update to tick

        Assert.That(
            timer.SolveElapsed,
            Is.GreaterThan(RealWaitSeconds * 0.5),
            "Solve timer must not pause when Time.timeScale is 0"
        );
    }

    /// <summary>
    /// Inspection countdown must keep running while Time.timeScale is 0.
    /// DateTimeOffset.UtcNow is unaffected by Unity's time scale, so the inspection
    /// remaining continues to decrease even when Unity internal time is frozen.
    /// </summary>
    [UnityTest]
    public IEnumerator InspectionTimer_ContinuesCountingDown_WhenTimescaleIsZero()
    {
        // Long inspection so we stay in Inspection phase throughout the test.
        var timer = CreateAndInitTimer(inspectionDuration: 60.0, out _);

        yield return null; // First Update ticks inspection

        Assert.That(timer.CurrentPhase, Is.EqualTo(GameTimer.Phase.Inspection));
        double remainingBefore = timer.InspectionRemaining;

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(RealWaitSeconds);
        Time.timeScale = 1f;

        yield return null; // One more Update to tick

        double consumed = remainingBefore - timer.InspectionRemaining;
        Assert.That(
            consumed,
            Is.GreaterThan(RealWaitSeconds * 0.5),
            "Inspection countdown must not pause when Time.timeScale is 0"
        );
    }

    // ───────── Helpers ─────────

    private GameTimer CreateAndInitTimer(double inspectionDuration, out GameTimerView timerView)
    {
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        Assert.IsNotNull(panelSettings, "PanelSettings asset not found at " + PanelSettingsPath);
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GameHudUxmlPath);
        Assert.IsNotNull(uxml, "GameHud UXML not found at " + GameHudUxmlPath);

        var doc = _host.AddComponent<UIDocument>();
        doc.panelSettings = panelSettings;
        doc.visualTreeAsset = uxml;

        var timer = new GameTimer(inspectionDuration);
        double wallNow = (double)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        timer.Start(wallNow);
        timerView = _host.AddComponent<GameTimerView>();
        timerView.Init(timer, doc);
        return timer;
    }
}
