# TODO — Save Game + Cancel Generation

## Overview

Two QoL features to support large boards that take a long time to clear.

---

## Feature 1: Save Game / Resume

### Design

Save an in-progress game as a **replay event log** (JSON). This doubles as the groundwork for the v0.4 replay system (see `OnlineRoadmap.md`).

The save file records:
- Board parameters (seed, dimensions, max arrow length, inspection duration)
- A `game_id` UUID for future server compatibility
- All input events in sequence (session_start, session_leave, session_rejoin, start_solve, clear, reject)
- Session events carry wall-clock timestamps (ISO 8601); gameplay events carry solve-relative timestamps
- `session_leave` events include a `solveElapsed` snapshot for timer restoration

One save slot. Starting a new board overwrites (with confirmation modal).

#### Resume flow
1. Regenerate the board from seed (deterministic → identical board)
2. Walk `Clear` events in seq order, remove each arrow with no animation
3. If any `StartSolve` event exists: restore timer from the last `SessionLeave.solveElapsed` via `GameTimer.Resume()`; otherwise restart inspection normally
4. Continue recording new events into the restored recorder

#### UI changes
- Main menu: "Continue" button below "Play" (hidden when no save)
- When save exists: "Play" button text → "New Board"
- Clicking "New Board" shows a confirmation modal ("Your current game will be lost")
- In-game: Leave-Yes button saves before scene transition
- Auto-save on app focus loss (`OnApplicationFocus(false)`) for WebGL tab-switching; matching `session_rejoin` on focus regain

#### Save trigger
- Explicit Leave (via Leave modal Yes button)
- `OnApplicationFocus(false)` / `OnApplicationPause(true)` — covers WebGL tab close
- Delete on board cleared (completed games don't have a save to continue)

---

## Feature 2: Cancel Board Generation

Large boards (e.g., 200×200) can take 30+ seconds to generate. Players should be able to cancel and return to the menu.

A **Cancel** button is shown inside the loading overlay during generation. When clicked, the generation coroutine aborts and the main menu loads.

---

## Implementation Plan

### Domain layer (pure C#, no Unity dependency)

- [x] `ReplayEventType` — string constants: `session_start`, `session_leave`, `session_rejoin`, `start_solve`, `clear`, `reject`
- [x] `ReplayEvent` — `[Serializable]` record with all fields; unused fields are 0/null per event type
- [x] `ReplayData` — `[Serializable]` save/replay format: game_id, seed, board params, event list, finalTime (-1 = incomplete)
- [x] `ReplayRecorder` — accumulates events, auto-increments seq; supports initialization from prior events for resume
- [x] `GameTimer.Resume(double current, double priorElapsed)` — restores solve state from saved elapsed
- [x] `GameSettings.IsResuming`, `GameSettings.ResumeData`, `GameSettings.Resume(ReplayData)`, update `Reset()`

### View layer

- [x] `SaveManager` — static class: `HasSave()`, `Load()`, `Save(ReplayData)`, `Delete()` via `Application.persistentDataPath`
- [x] `MainMenuController` — Continue button wiring, dynamic "Play"/"New Board" text, new-board-modal
- [x] `GameController` — resume logic (apply clears, restore timer), recorder creation, record session events, save on leave, auto-save on focus loss, cancel generation support
- [x] `InputHandler` — accepts optional `ReplayRecorder`, records `start_solve`/`clear`/`reject` events on each tap

### UI files

- [x] `Root.uxml` — `continue-btn`, `new-board-modal`
- [x] `MainMenu.uss` — `continue-btn` distinct teal style, `modal-sublabel` for secondary modal text
- [x] `GameHud.uxml` — `cancel-generation-btn` inside `loading-overlay`
- [x] `GameHud.uss` — `cancel-generation-btn` style (centered, below label)

### Tests

- [x] `ReplayRecorderTests.cs` (EditMode) — seq ordering, event types, resume continuity, ToReplayData, start_solve+clear same timestamp
- [x] `UILayoutTests.cs` (PlayMode) — `MainMenu_WithSave`, `MainMenu_NewBoardModal`, `GameHud_CancelGeneration`

### Documentation

- [x] `TechnicalDesign.md` — update with new domain classes, save manager, modified scripts, decision log entry

---

## Open Questions

*Resolved before implementation:*

1. **Single save slot?** Yes. One active game at a time.
2. **Where to store?** `Application.persistentDataPath/savegame.json` (IndexedDB on WebGL).
3. **Serializer?** `UnityEngine.JsonUtility` — no external dependency. Fields must be public and `[Serializable]`.
4. **Resume inspection or skip?** If solve hasn't started (no `start_solve` in log): restart inspection. If started: skip inspection, restore solve elapsed.
5. **Cancel during generation that yields no arrows?** Generation abort returns to menu normally (same as the empty-board guard that already returns to menu).
6. **Auto-save paired events?** `OnApplicationFocus(false)` → `session_leave`; `OnApplicationFocus(true)` → `session_rejoin`. Properly paired even if player switches apps mid-game.

---

## Testing (Manual)

After implementation, verify the following before marking complete:

| # | Test Case | Expected |
|---|-----------|----------|
| 1 | Launch game with no save → Main menu | "Play" button shows; no "Continue" |
| 2 | Play a game, clear a few arrows, press Leave → Main menu | "Continue" button appears; "Play" reads "New Board" |
| 3 | Press "Continue" | Same board with same arrows missing; timer continues from where it left off |
| 4 | Press "New Board" (with save) | Confirmation modal appears |
| 5 | Confirm new board | Save deleted; fresh board generation |
| 6 | Cancel new board | Returns to main menu unchanged |
| 7 | Complete a game (all arrows cleared) → Main menu | "Continue" absent; "Play" shows |
| 8 | Large board generation (100×100) | Cancel button visible during generation |
| 9 | Click Cancel during generation | Returns to main menu |
| 10 | Switch tabs in WebGL mid-game | On return, can still use Leave to save |
