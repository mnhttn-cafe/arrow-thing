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

- [x] **1. Grid fade support** — `BoardGridRenderer.FadeOut` with coroutine-driven alpha fade.
- [x] **2. Victory UXML/USS** — centered popup box with message, Play Again, Menu. Dark overlay.
- [x] **3. VictoryController** — drives fade → popup sequence, random message with font scaling for long text.
- [x] **4. Wire into BoardView** — `BoardCleared` event fires after last arrow's pull-out animation.
- [x] **5. Wire in GameController** — UIDocument reference, VictoryController init, event subscription.
- [x] **6. Timing** — uses `PlayPullOut` onComplete callback to trigger clear sequence after animation.
- [x] **7. Manual test cases** — see below.
- [ ] **8. Docs cleanup** — Update TechnicalDesign.md, CLAUDE.md. Delete this TODO.

---

## Manual Test Cases

### Clear Sequence Timing

- [ ] **CS-1: Grid fade starts after animation** — Clear the last arrow. The pull-out animation should fully complete before the grid dots begin fading.
- [ ] **CS-2: Grid fade duration** — Grid dots fade smoothly to transparent over ~0.5s (no pop/snap).
- [ ] **CS-3: Popup appears after fade** — Victory popup appears only after grid dots are fully transparent, not during the fade.

### Victory Popup Content

- [ ] **VP-1: Message displayed** — A message is visible in the popup after clearing the board.
- [ ] **VP-2: Message randomization** — Clear the board multiple times. Different messages should appear (not always the same one).
- [ ] **VP-3: Short message font** — Short messages (e.g. "Nice!", "woa") display at a large, readable font size.
- [ ] **VP-4: Long message font** — Very long messages (e.g. the "DONT PRESS THAT BUTTON..." message) display at a smaller font size and wrap within the box without pushing buttons off-screen.
- [ ] **VP-5: Buttons visible** — Both "Play Again" and "Menu" buttons are always visible and clickable, regardless of message length.

### Play Again

- [ ] **PA-1: Reloads with same preset** — Click Play Again. A new board loads with the same dimensions as the previous game.
- [ ] **PA-2: New random seed** — Click Play Again twice. The two boards should be different.
- [ ] **PA-3: Full loop** — Clear a board → Play Again → clear again → Play Again. Each cycle works without errors.

### Menu Button

- [ ] **MB-1: Returns to main menu** — Click Menu. The MainMenu scene loads with all screens functional.
- [ ] **MB-2: Preset preserved** — After returning to menu via the victory screen, the previously selected preset should still be highlighted in Mode Select.

### Input During Clear Sequence

- [ ] **ID-1: No interaction during fade** — While the grid is fading, tapping the screen should not trigger any arrow interactions (there are no arrows, but ensure no errors).
- [ ] **ID-2: No interaction behind popup** — While the victory popup is visible, camera pan/zoom should not respond.

### Editor Workflow

- [ ] **EW-1: No victoryUIDocument assigned** — Play the Game scene directly without assigning the victory UIDocument. Board should clear normally with no popup and no errors (graceful skip).

---

## Open Questions

_None._
