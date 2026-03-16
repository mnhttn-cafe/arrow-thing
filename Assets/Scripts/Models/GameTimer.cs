using System;

/// <summary>
/// Two-phase timer: inspection countdown followed by solve timer.
/// Pure C# — no Unity dependency. All timestamps use the same clock
/// (caller's responsibility). Input-precision timestamps are stored
/// separately for the final solve time only.
/// </summary>
public sealed class GameTimer
{
    public enum Phase
    {
        Inspection,
        Solving,
        Finished,
    }

    private readonly double _inspectionDuration;
    private double _inspectionStart;
    private double _solveStart;

    // Input-precision timestamps for final result
    private double _inputStartTime;
    private double _inputFinishTime;
    private bool _hasInputStart;

    public Phase CurrentPhase { get; private set; }
    public bool IsSolving => CurrentPhase == Phase.Solving;
    public double InspectionRemaining { get; private set; }
    public double SolveElapsed { get; private set; }

    public event Action<Phase> PhaseChanged;

    public GameTimer(double inspectionDuration = 15.0)
    {
        _inspectionDuration = inspectionDuration;
        InspectionRemaining = inspectionDuration;
        CurrentPhase = Phase.Inspection;
    }

    /// <summary>
    /// Begin inspection countdown. Call once at game start.
    /// </summary>
    public void Start(double current)
    {
        _inspectionStart = current;
        InspectionRemaining = _inspectionDuration;
        CurrentPhase = Phase.Inspection;
    }

    /// <summary>
    /// Call every frame with the current time to update display values.
    /// </summary>
    public void Tick(double current)
    {
        switch (CurrentPhase)
        {
            case Phase.Inspection:
                InspectionRemaining = Math.Max(
                    0.0,
                    _inspectionDuration - (current - _inspectionStart)
                );
                if (InspectionRemaining <= 0.0)
                    StartSolve(current);
                break;

            case Phase.Solving:
                SolveElapsed = current - _solveStart;
                break;
        }
    }

    /// <summary>
    /// Transition to solving. Uses the same clock as Tick.
    /// Optionally stores an input-precision timestamp for the final result.
    /// </summary>
    public void StartSolve(double current, double inputTimestamp = -1.0)
    {
        if (CurrentPhase != Phase.Inspection)
            return;

        _solveStart = current;
        SolveElapsed = 0.0;
        InspectionRemaining = 0.0;
        CurrentPhase = Phase.Solving;

        if (inputTimestamp >= 0.0)
        {
            _inputStartTime = inputTimestamp;
            _hasInputStart = true;
        }

        PhaseChanged?.Invoke(Phase.Solving);
    }

    /// <summary>
    /// End the solve. If both input timestamps are available, uses them
    /// for precise final time. Otherwise falls back to frame time.
    /// </summary>
    public void Finish(double current, double inputTimestamp = -1.0)
    {
        if (CurrentPhase != Phase.Solving)
            return;

        if (_hasInputStart && inputTimestamp >= 0.0)
        {
            _inputFinishTime = inputTimestamp;
            SolveElapsed = _inputFinishTime - _inputStartTime;
        }
        else
        {
            SolveElapsed = current - _solveStart;
        }

        CurrentPhase = Phase.Finished;
        PhaseChanged?.Invoke(Phase.Finished);
    }
}
