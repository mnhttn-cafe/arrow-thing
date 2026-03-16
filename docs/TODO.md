# Feature: Board Clear Screen

## Goal

When the player clears all arrows, show a victory popup with a playful message and options to play again or return to the menu. The grid dots fade out before the popup appears.

---

## Design

### Trigger

`BoardView.TryClearArrow` already removes the arrow from the board. After a successful clear, if `_board.Arrows.Count == 0`, the board is fully cleared. The last arrow's pull-out animation should finish before the clear sequence starts.

### Clear Sequence

1. **Grid fade-out** — all grid dot `SpriteRenderer`s fade alpha to 0 over ~0.5s.
2. **Victory popup** — UI Toolkit overlay appears after the fade completes. Contains:
   - A randomized playful message (e.g. "Nice!", "Nailed it!", "That was smooth.", "Arrows fear you.", "Flawless.", "Too easy.")
   - **Play Again** button — regenerates a board with the same `GameSettings` preset and a new random seed. No scene reload needed if we just reset in-place, but `SceneManager.LoadScene("Game")` is simpler and avoids teardown bugs.
   - **Menu** button — loads `MainMenu` scene.

### UI Approach

Reuse the existing UI Toolkit setup from the menu. The Game scene needs its own `UIDocument` + `PanelSettings` reference. The victory popup is a simple overlay UXML/USS, similar to the quit modal.

### File Layout

```
Assets/
  UI/
    VictoryPopup.uss     # popup-specific styles
    VictoryPopup.uxml    # popup markup
  Scripts/
    View/
      VictoryController.cs  # listens for board clear, drives fade + popup
```

### Play Again Flow

`SceneManager.LoadScene("Game")` — `GameSettings.IsSet` is still true from the menu, so `GameController.Awake` picks up the same preset with a new random seed. Simple, no state management needed.

---

## Implementation Plan

- [ ] **1. Grid fade support** — Add a `FadeOut(float duration, System.Action onComplete)` method to `BoardGridRenderer`. Collects all child `SpriteRenderer`s and fades their alpha over `duration` using a coroutine.
- [ ] **2. Victory UXML/USS** — `VictoryPopup.uxml` with a centered box containing a message label and two buttons (Play Again, Menu). `VictoryPopup.uss` for styling. Dark overlay background similar to quit modal.
- [ ] **3. VictoryController** — MonoBehaviour on the Game scene. Holds a reference to `UIDocument` (for the popup), `BoardGridRenderer` (for the fade), and a `string[]` of messages. Exposes `OnBoardCleared()` which starts the fade, then shows the popup with a random message. Play Again reloads Game scene; Menu loads MainMenu scene.
- [ ] **4. Wire into BoardView** — After a successful `TryClearArrow` where `_board.Arrows.Count == 0`, notify `VictoryController`. Use a callback/event so `BoardView` doesn't depend on `VictoryController` directly.
- [ ] **5. Wire in GameController** — Create the `UIDocument` for the popup, instantiate `VictoryController`, connect the board-cleared event.
- [ ] **6. Timing** — The victory sequence should wait for the last arrow's pull-out animation to finish before starting the grid fade. `ArrowView.PlayPullOut` already has an `onComplete` callback — use it.
- [ ] **7. Manual test cases** — Add test cases to this TODO covering clear sequence timing, popup content, Play Again, Menu, edge cases.
- [ ] **8. Docs cleanup** — Update TechnicalDesign.md, CLAUDE.md. Delete this TODO.

---

## Manual Test Cases

_To be filled after implementation._

---

## Open Questions

_None._
