# Local Replay Submission

## Overview

Allow players to submit locally-saved replays to the global leaderboard from the local leaderboard screen. Covers the case where submission failed at victory (server down, offline, timeout) or the player wasn't logged in when they played.

**Scope**: Submit button on local leaderboard entries → `ScoreSubmitter.TrySubmitAsync` → feedback.

---

## Design Decisions

- **Submit button visibility**: Show on local leaderboard entry rows when the player is logged in. Hide if the entry has already been submitted globally.
- **Submission state tracking**: Add an `isSubmitted` field to `LeaderboardEntry`. Set to `true` after a successful submission. Persisted in `leaderboard.json`.
- **Auto-mark on victory submission**: When `VictoryController` submits successfully, mark the corresponding local entry as submitted.
- **Bulk submission**: Not in scope — one entry at a time via a button tap. A "Submit All" feature could come later.
- **Feedback**: Reuse the same toast pattern from victory, or show inline feedback on the row (e.g., brief checkmark or "Submitted!" text that fades).
- **Stale replays**: The server verifies replays statelessly (regenerates board from seed). Old replays work fine as long as the replay format hasn't changed incompatibly.

---

## Open Questions

- Should the submit button be an icon (upload arrow) or text ("Submit")?
- Should we show inline row feedback (checkmark) or a toast notification?
- Should entries that were played while not logged in be submittable, or only entries that failed submission?

---

## Implementation Plan

### Phase 1: Domain — `LeaderboardEntry.isSubmitted`

- [ ] Add `isSubmitted` field (default `false`) to `LeaderboardEntry`.
- [ ] Ensure it serializes/deserializes in `LeaderboardStore` JSON.
- [ ] Add `MarkSubmitted(gameId)` method to `LeaderboardStore` (sets `isSubmitted = true`, returns bool).
- [ ] Add `MarkSubmitted(gameId)` to `LeaderboardManager` (delegates to store + saves index).

### Phase 2: Victory — Mark on successful submission

- [ ] In `VictoryController`, after `RecordToLeaderboard` + successful submission, call `LeaderboardManager.MarkSubmitted(gameId)`.

### Phase 3: Leaderboard UI — Submit button

- [ ] In `LeaderboardScreenController.CreateEntryRow`, add a submit button (visible when `api.IsLoggedIn && !entry.isSubmitted`).
- [ ] On click: load replay via `LeaderboardManager.LoadReplay(gameId)`, call `ScoreSubmitter.TrySubmitAsync`, handle result.
- [ ] On success: mark entry as submitted, refresh the row (remove submit button, optionally show brief feedback).
- [ ] On failure: show feedback (toast or inline).

### Phase 4: Tests

- [ ] `LeaderboardStoreTests`: `isSubmitted` serialization round-trip, `MarkSubmitted` sets flag.
- [ ] Manual: play game offline → log in → open local leaderboard → submit → verify appears on global.

---

## Manual Test Cases

| # | Scenario | Expected |
|---|----------|----------|
| 1 | Logged in, local entry not submitted → tap Submit | Replay submitted; button disappears; entry marked as submitted |
| 2 | Submission fails (server down) → tap Submit | Error feedback shown; button stays |
| 3 | Not logged in → local leaderboard | No submit buttons visible |
| 4 | Entry already submitted → local leaderboard | No submit button on that entry |
| 5 | Play game while logged in, submission succeeds at victory | Entry auto-marked as submitted; no submit button in leaderboard |
| 6 | Play game offline, log in later, submit from leaderboard | Works; score appears on global |
