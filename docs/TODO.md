# Feature: Solve Timer (Cubing-style inspection + solve)

## Goal

Add a two-phase timer to the gameplay HUD, inspired by competitive Rubik's cube solving:

1. **Inspection phase** — fixed-length countdown (configurable, default 15s). Does not count toward the final time. Gives the player time to study the board.
2. **Solve phase** — starts when the first arrow is cleared OR the inspection timer runs out (whichever comes first). Ends when the board is fully cleared.

The final time displayed on the victory screen is the solve phase duration only.

## Design Decisions

### Domain Model (`GameTimer`)
- Pure C# class, no Unity dependency. Testable in EditMode.
- Tracks phase: `Inspection → Solving → Finished`.
- Uses **absolute `double` timestamps** (not accumulated deltas) for solve duration: `finishTime - startTime`. No floating-point drift.
- Inspection countdown still ticks via `Tick(double currentTime)` for display, but solve precision comes from input timestamps.
- Fires `PhaseChanged` event so the view can react to transitions.

### Input-Precision Timing
- Solve timer start/stop uses `InputAction.CallbackContext.time` — the actual input event timestamp, not frame time.
- **First arrow clear release** → `GameTimer.StartSolve(double timestamp)`
- **Last arrow clear release** → `GameTimer.Finish(double timestamp)`
- **Inspection expiry** → frame-timed from Update: `GameTimer.StartSolve(Time.timeAsDouble)`

### ClearResult Enum
`BoardView.TryClearArrow` changes return type from `bool` to `ClearResult`:
```csharp
public enum ClearResult
{
    Blocked = 0,    // not clearable
    Cleared,        // cleared, not first or last
    ClearedFirst,   // first arrow cleared
    ClearedLast     // board fully cleared
}
```
- `Blocked = 0` so all success values are nonzero. Existing boolean checks become `!= ClearResult.Blocked`.
- `ClearedFirst` and `ClearedLast` are mutually exclusive — a 1-arrow board returns `ClearedLast`, and `InputHandler` calls `StartSolve` then `Finish` if solve hasn't started yet.

### InputHandler Timer Wiring
```csharp
var result = _boardView.TryClearArrow(arrow);
switch (result)
{
    case ClearResult.ClearedFirst:
        _timer.StartSolve(inputTimestamp);
        break;
    case ClearResult.ClearedLast:
        if (!_timer.IsSolving)
            _timer.StartSolve(inputTimestamp);
        _timer.Finish(inputTimestamp);
        break;
}
```

### Visual Treatment
- **Inspection phase** — timer label is grey, shows countdown.
- **Inspection ending** — faint red flash as it expires.
- **Solve phase** — timer label turns solid white, counts up.
- **Victory screen** — final solve time displayed alongside the message.

## Implementation

- [x] `Assets/Scripts/Models/GameTimer.cs` — domain timer model
- [x] `Assets/Scripts/Models/ClearResult.cs` — enum definition
- [x] `Assets/Tests/EditMode/GameTimerTests.cs` — NUnit tests for timer logic
- [x] Update `Assets/Scripts/View/BoardView.cs` — return `ClearResult` from `TryClearArrow`
- [x] Update `Assets/UI/GameHud.uxml` + `GameHud.uss` — timer label + leave confirmation modal
- [x] `Assets/Scripts/View/GameTimerView.cs` — MonoBehaviour driving timer display
- [x] Update `Assets/Scripts/View/InputHandler.cs` — pass input timestamps to timer on clear
- [x] Update `Assets/Scripts/View/GameController.cs` — wire timer, HUD, leave modal
- [x] Update `Assets/Scripts/View/VictoryController.cs` + `Assets/UI/VictoryPopup.uxml` — display final time, hide HUD on popup
- [x] `Assets/Tests/PlayMode/UILayoutTests.cs` — GameHud, leave modal, victory time layout tests
- [x] `docs/TechnicalDesign.md` — updated with new types and view components
- [ ] Manual test cases
