using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// PlayMode tests verifying that GameTimerView uses real time (realtimeSinceStartupAsDouble)
/// rather than Unity game time (timeAsDouble), so the timer is not paused when the player
/// tabs out of the window.
///
/// Time.timeScale = 0 is used as a proxy for focus loss: it freezes Time.timeAsDouble
/// while Time.realtimeSinceStartupAsDouble continues to advance, which is exactly
/// the divergence that occurs when the application loses focus in WebGL.
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
    /// Solve timer must keep running while Time.timeScale is 0 (simulates tab-out).
    /// If GameTimerView used Time.timeAsDouble, SolveElapsed would stay near zero
    /// during the freeze.
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
            "Solve timer must not pause when Time.timeScale is 0 (simulates window focus loss)"
        );
    }

    /// <summary>
    /// Inspection countdown must keep running while Time.timeScale is 0.
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
            "Inspection countdown must not pause when Time.timeScale is 0 (simulates window focus loss)"
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
        timerView = _host.AddComponent<GameTimerView>();
        timerView.Init(timer, doc);
        return timer;
    }
}
