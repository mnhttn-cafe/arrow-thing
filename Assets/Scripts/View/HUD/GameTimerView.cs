using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Drives a GameTimer each frame and updates the HUD timer label.
/// Inspection: grey countdown (whole seconds), turns red near expiry.
/// Solving: white count-up (whole seconds).
/// Finished: white, precise millisecond time.
/// </summary>
public sealed class GameTimerView : MonoBehaviour
{
    private GameTimer _timer;
    private Label _label;
    private double _warningThreshold;

    public GameTimer Timer => _timer;

    public void Init(GameTimer timer, UIDocument hudDocument, double warningThreshold = 5.0)
    {
        _timer = timer;
        _warningThreshold = warningThreshold;
        _label = hudDocument.rootVisualElement.Q<Label>("timer-label");

        _timer.PhaseChanged += OnPhaseChanged;

        // Timer is already started/resumed by GameController before Init is called.
        // Apply the current phase styling in case we're resuming into Solving.
        if (_timer.CurrentPhase == GameTimer.Phase.Solving)
            OnPhaseChanged(GameTimer.Phase.Solving);

        UpdateLabel();
    }

    private void Update()
    {
        if (_timer == null)
            return;

        _timer.Tick(GetWallTime());
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        switch (_timer.CurrentPhase)
        {
            case GameTimer.Phase.Inspection:
                int ceilSeconds = Mathf.CeilToInt((float)_timer.InspectionRemaining);
                _label.text = ceilSeconds.ToString();

                if (_timer.InspectionRemaining <= _warningThreshold)
                    _label.AddToClassList("timer-label--warning");
                break;

            case GameTimer.Phase.Solving:
                int wholeSeconds = Mathf.FloorToInt((float)_timer.SolveElapsed);
                _label.text = FormatWholeSeconds(wholeSeconds);
                break;

            case GameTimer.Phase.Finished:
                _label.text = FormatPrecise(_timer.SolveElapsed);
                break;
        }
    }

    private void OnPhaseChanged(GameTimer.Phase newPhase)
    {
        switch (newPhase)
        {
            case GameTimer.Phase.Solving:
                _label.RemoveFromClassList("timer-label--warning");
                _label.AddToClassList("timer-label--solving");
                break;
        }
    }

    private static string FormatWholeSeconds(int totalSeconds)
    {
        int hours = totalSeconds / 3600;
        int mins = (totalSeconds % 3600) / 60;
        int secs = totalSeconds % 60;

        if (hours > 0)
            return $"{hours}:{mins:D2}:{secs:D2}";
        if (mins > 0)
            return $"{mins}:{secs:D2}";
        return secs.ToString();
    }

    private static string FormatPrecise(double seconds)
    {
        if (seconds < 0)
            seconds = 0;
        int totalMillis = (int)(seconds * 1000);
        int hours = totalMillis / 3600000;
        int mins = totalMillis % 3600000 / 60000;
        int secs = totalMillis % 60000 / 1000;
        int millis = totalMillis % 1000;

        if (hours > 0)
            return $"{hours}:{mins:D2}:{secs:D2}.{millis:D3}";
        if (mins > 0)
            return $"{mins}:{secs:D2}.{millis:D3}";
        return $"{secs}.{millis:D3}";
    }

    private static double GetWallTime() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    private void OnDestroy()
    {
        if (_timer != null)
            _timer.PhaseChanged -= OnPhaseChanged;
    }
}
