# Save-Game QoL Fixes

Feedback from manual testing of PR #30. Redesigns the save/leave flow and fixes several bugs.

## Changes

### 1. Redesign leave-game modal → "Save game?" prompt

**Current**: "Leave game?" with Yes/No buttons.
**New**: Two variants depending on game state:

**If at least one arrow has been cleared (solve started + clear exists):**
- Title: "Save game?"
- Sub-text (only if a prior save exists on disk): "This will replace your current save."
- **Yes**: Save current game state, return to main menu.
- **No**: Discard progress, return to main menu without saving (prior save untouched).
- **X** (top-right close button): Cancel — close modal, resume playing.

**If no arrows have been cleared yet (still in inspection or solve started but nothing cleared):**
- Title: "Leave game?"
- **Yes**: Return to main menu (no save created — nothing meaningful to preserve).
- **No**: Cancel — close modal, resume playing.
- No X button needed (Yes/No covers it), but can keep for consistency.

### 2. Remove new-board modal from main menu

**Current**: When a save exists, "Play" becomes "New Board" and shows a confirmation modal. Confirming deletes the save.
**New**: Remove the modal entirely. Simplify:
- Save exists → show both "Play" and "Continue". "Play" text stays "Play" (not "New Board").
- No save → show only "Play", hide "Continue".
- "Play" always goes straight to mode select. No modal, no save deletion.
- "Continue" loads the save as before.
- Save is only overwritten when the player explicitly saves a different game via the leave modal.
- Save is only deleted when the board is fully cleared.

### 3. Remove auto-save on focus loss

Delete `OnApplicationFocus` handler entirely. Save only happens via the explicit leave-game modal "Yes" button.

### 4. Fix timer restoration on resume

**Bug**: On resume, timer shows inspection countdown and solve elapsed resets to 0.
**Fix**: Ensure `resumeSolving` and `resumeSolveElapsed` are correctly extracted from replay events and passed to `GameTimer.Resume()`.

### 5. Loading screen HUD cleanup

- Hide `timer-label` and `trail-toggle-btn` during generation (`display: none`, restore after).
- Remove `cancel-generation-btn` from the loading overlay.
- Wire the existing `back-to-menu-btn` (X, top-left) to cancel generation during loading — no modal, just immediate cancel.
- After generation completes, re-wire `back-to-menu-btn` to its normal leave-modal behavior.

### 6. Log save path for debugging

Log the full file path on save/load so developers can find and inspect the JSON.

## Implementation Plan

### UI changes
- [ ] `GameHud.uxml` — Remove `cancel-generation-btn`. Add X close button to leave modal. Add `modal-sublabel` for "replace save" warning.
- [ ] `GameHud.uss` — Remove cancel button styles. Add close-button style. Style the two modal variants.
- [ ] `Root.uxml` — Remove `new-board-modal`. Remove `continue-btn` `screen--hidden` class (visibility managed by controller).
- [ ] `MainMenu.uss` — Remove `modal-sublabel` style if it was only for the new-board modal (check if reused).

### View layer
- [ ] `MainMenuController` — Remove new-board modal logic. "Play" always shows "Play" text. Remove `OnNewBoardConfirm`/`OnNewBoardCancel`. Continue button visibility based on `SaveManager.HasSave()`.
- [ ] `GameController` — Redesign leave modal: check if any clears happened → show appropriate variant. Wire save/no-save/cancel. Remove `OnApplicationFocus`. Hide timer/trail during generation. Wire X button for cancel during generation, then re-wire for leave modal after. Fix timer resume. Add save path logging.
- [ ] `SaveManager` — Add path logging on save/load.

### Tests
- [ ] `UILayoutTests` — Remove `MainMenu_NewBoardModal` test. Remove cancel-generation button from loading overlay test. Update leave modal test if structure changed. Add test for leave modal with sublabel variant.
- [ ] `ReplayRecorderTests` — Verify existing tests still pass (no domain changes expected).

### Documentation
- [ ] `TechnicalDesign.md` — Update MainMenuController, GameController, and PlayMode test descriptions.

## Manual Test Cases

| # | Test Case | Expected |
|---|-----------|----------|
| 1 | Start game, clear some arrows, press X | "Save game?" modal with Yes/No/X-close |
| 2 | Save game modal → Yes | Returns to menu, Continue visible, resume works with timer |
| 3 | Save game modal → No | Returns to menu, prior save (if any) untouched |
| 4 | Save game modal → X (close) | Modal closes, resume playing |
| 5 | Start game, DON'T clear arrows, press X | "Leave game?" modal with Yes/No only |
| 6 | Leave (no clears) → Yes | Returns to menu, no save created |
| 7 | Main menu with save: Play | Goes to mode select directly (no modal) |
| 8 | Main menu with save: Continue | Loads saved game correctly |
| 9 | New game while save exists → clear arrows → leave → Yes | Old save replaced, Continue loads new game |
| 10 | Clear entire board → menu | No Continue button |
| 11 | During generation: timer/trail hidden, X cancels | Returns to menu |
| 12 | Check console for save path on save/load | Path logged |
