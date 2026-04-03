# Global Leaderboards

## Overview

Wire up server-side score submission and expose global leaderboards in-client. Players with verified accounts submit scores silently when online; offline games stay local-only. All games are always generated locally — the server only receives completed replays for verification and storage.

**Scope**: `ReplayVerifier` domain class → server score/leaderboard/replay endpoints → client score submission → global leaderboard display (dedicated scene).

**Out of scope (separate PRs)**: server-assigned seeds / `GameSession` pre-registration.

---

## Resolved Design Decisions

- **No server-side game pre-registration**: The client always generates the board locally from its own seed. On victory, if eligible, it submits the completed replay to the server. The server verifies by regenerating the board deterministically and simulating the events. No round-trip before play.
- **Replay storage — hybrid snapshot strategy**:
  - Top-50 scores: store the full replay. The `boardSnapshot` field is stored gzip-compressed and base64-encoded within the JSON so it doesn't dominate the plaintext size. Events and metadata remain readable plaintext.
  - Non-top-50 scores: store replay without the `boardSnapshot` field (events + metadata only).
  - Rationale: top-50 replays are public and played by anyone — smooth instantaneous load is important. Non-top-50 replays are only accessed by their owner, who either has the replay locally or can have the board regenerated from seed + params (same path as `BoardSetupHelper` restore).
  - When a score is submitted and lands in top-50, the snapshot is stored. When a submission pushes another score out of the top-50, the displaced score's snapshot is immediately stripped. This is an active, eager cleanup on every submission — not lazy. The operation is cheap (one row update) and keeps storage bounded even as the playerbase grows.
  - `ReplayViewController` already supports board restoration from snapshot. It needs to also support restoration from seed+params for the no-snapshot path (global non-top-50 replay viewing).
- **Submission gate**: The client always attempts submission when logged in, email-verified, and the server is reachable. No local-PB gate on the client — the local PB may not be a submitted score (e.g., played offline on this device), so the client can't reliably know what the server's current best is. The server handles the "is this better?" check and returns appropriately. The cost is one network round-trip per completed online game, which is acceptable.
  - Note: the victory screen's "New Best!" / gold timer logic is entirely independent of submission — it only compares the current time against local scores. So if the player's local best is an unsubmitted offline score and they finish a game that would be second-best locally but first globally, the victory screen won't go gold (it's not a local best), but the score is still submitted, and the global leaderboard will correctly show it as their personal best.
- **Best score only per player per board size**: Server stores exactly one `Score` per `(UserId, BoardWidth, BoardHeight)` — the personal best. Updated in-place when a better verified time arrives.
- **Submission deduplication**: Idempotent on `gameId`. If the existing `Score` for this user+size already has `GameId == gameId`, return the existing result without re-verifying.
- **Display names**: The local `LeaderboardEntry` name is fixed at submission time. The global leaderboard always reads `User.DisplayName` live at query time — renames reflect immediately on global, never on local.
- **Victory screen philosophy**: Submission is silent; victory modal stays entirely local. "New Best!" / gold timer / "View Leaderboard" all compare against local scores only. Global leaderboard is always the player's express navigation choice. If submission fails or times out, a small low-prominence note appears with a retry button — enough to avoid the score silently disappearing, not prominent enough to affect the post-game mood.
- **"All" tab global ranking**: Ranked by biggest board cleared first (area DESC), then fastest time within that size. Each player's representative score is their best time on their largest completed board.
- **Rate limiting**: Count-based. Cap at 10 verified score updates per user per hour. Empirically, beating your own 10×10 PB more than ~2 times in an hour is already very hard in practice — 10 is a generous ceiling that no legitimate player should hit.
- **Replay storage format**: PostgreSQL `TEXT` column — TOAST handles large values transparently (up to 1GB). No separate blob storage needed. SQLite likewise.

---

## Open Questions

None. All design decisions are resolved above.

---

## Implementation Plan

### Phase 1: Domain — ReplayVerifier

- [ ] **`ReplayVerifier`** (`Assets/Scripts/Domain/ReplayVerifier.cs`)
  - Static class. Takes `ReplayData` and returns `VerificationResult`.
  - Algorithm:
    1. Regenerate board from `seed`, `boardWidth`, `boardHeight`, `maxArrowLength` via `Board` + `BoardGeneration.FillBoardIncremental`. Compare generated arrows against `boardSnapshot` — mismatch → return `Invalid` (detects tampered board state).
    2. Walk `clear` events in `seq` order. For each: convert `posX`/`posY` → cell via `new Cell((int)Math.Round(posX), (int)Math.Round(posY))`. Get arrow via `Board.GetArrowAt(cell)`. Verify `Board.IsClearable(arrow)`. Call `Board.RemoveArrow(arrow)`. No arrow at cell or not clearable → return `Invalid` with reason.
    3. Verify `board.Arrows.Count == 0` after all events — board must be fully cleared.
    4. Compute solve time from event timestamps, excluding `session_leave`→`session_rejoin` gaps. Store as `VerifiedTime`.
  - `VerificationResult`: `bool IsValid`, `string Reason` (null on success), `double VerifiedTime`.
  - Never throw on malformed input — catch everything, return `Invalid` with a reason.

- [ ] **NUnit tests** (`Assets/Tests/EditMode/ReplayVerifierTests.cs`)
  - Valid replay passes; `VerifiedTime` derived from event timestamps.
  - Snapshot mismatch (tampered board) → invalid.
  - Clear at a non-clearable arrow → invalid.
  - Tap on empty cell → invalid.
  - Board not empty at end → invalid.
  - Pause/rejoin: solve time excludes the gap correctly.
  - Truncated event stream (missing `end_solve`) → invalid.

### Phase 2: Server — Data Model & Migration

- [ ] **`Score` model** (`server/ArrowThing.Server/Models/Score.cs`)
  One row per `(UserId, BoardWidth, BoardHeight)`. Updated in-place on improvement.
  ```csharp
  public class Score
  {
      public Guid Id { get; set; }
      public Guid UserId { get; set; }
      public User User { get; set; }
      public Guid GameId { get; set; }          // client-generated UUID for the game that produced this PB
      public int Seed { get; set; }
      public int BoardWidth { get; set; }
      public int BoardHeight { get; set; }
      public int MaxArrowLength { get; set; }
      public double Time { get; set; }           // server-verified solve time (seconds)
      public string ReplayJson { get; set; }     // events + metadata; boardSnapshot present only if top-50
  }
  ```

- [ ] **`AppDbContext` update** (`server/ArrowThing.Server/Data/AppDbContext.cs`)
  - Add `DbSet<Score> Scores`.
  - Unique index on `(UserId, BoardWidth, BoardHeight)` — enforces best-only, one row per player per size.
  - Index on `(BoardWidth, BoardHeight, Time)` — fast top-N leaderboard queries.
  - Rate-limit tracking TBD in Phase 3 (simple counter; no index needed on `Score` — see rate limit decision).

- [ ] **EF migration**: `Add-Migration AddScores` → review generated SQL → commit.

### Phase 3: Server — Endpoints

- [ ] **`GameService`** (`server/ArrowThing.Server/Games/GameService.cs`)

  `SubmitReplay(userId, replayJson)` → `SubmitResultDto`
  1. Deserialize `replayJson`. Malformed → `{ verified: false, reason: "malformed replay" }`.
  2. Extract `GameId`, `BoardWidth`, `BoardHeight` from the deserialized `ReplayData`.
  3. Load existing `Score` for `(UserId, BoardWidth, BoardHeight)`.
  4. Idempotency: if existing `Score.GameId == replayData.GameId` → return `{ verified: true, rank: existingRank, isPersonalBest: false }` without re-verifying.
  5. Rate limit: count verified score updates for this user in the past hour. If ≥ 10 → 429.
  6. Call `ReplayVerifier.Verify(replayData)`. Invalid → `{ verified: false, reason }`.
  7. If existing score is faster → no update; return `{ verified: true, rank: existingRank, isPersonalBest: false }`.
  8. Compute new rank: count rows with `Time < result.VerifiedTime` for this board size + 1.
  9. Prepare `storedReplayJson`: if rank ≤ 50, include boardSnapshot (gzip-compress it, base64-encode, replace the field in the JSON); if rank > 50, strip the boardSnapshot field.
  10. Upsert `Score` with new values (`GameId`, `Seed`, `Time`, `storedReplayJson`).
  11. If this score entered top-50 (rank ≤ 50), find the score displaced to position 51 and strip its snapshot: rewrite its `ReplayJson` without the boardSnapshot field.
  12. Return `{ verified: true, rank, isPersonalBest: true }`.

- [ ] **`LeaderboardService`** (`server/ArrowThing.Server/Leaderboards/LeaderboardService.cs`)

  `GetTopEntries(width, height, limit)` → `List<LeaderboardEntryDto>`
  - Top `limit` `Score` rows for `(width, height)`, ordered by `Time ASC`. Join `User` for live `DisplayName`.
  - Each entry: `rank`, `displayName`, `time`, `gameId`.

  `GetTopEntriesAll(limit)` → `List<LeaderboardEntryDto>`
  - For each user, select the `Score` with max area (`BoardWidth * BoardHeight`), breaking ties by min `Time`.
  - Rank by `(area DESC, time ASC)`. Return top `limit`. Include `boardWidth`/`boardHeight` in each entry.

  `GetPlayerEntry(userId, width, height)` → `PlayerEntryDto?`
  - User's `Score` for this size. `rank` = count of users with better time + 1. Null if no score.

  `GetPlayerEntryAll(userId)` → `PlayerEntryDto?`
  - User's representative score (max area, then min time). `rank` in the all-sizes ranking. Null if no scores.

- [ ] **New endpoints** in `Program.cs`
  ```
  POST  /api/scores                  [auth]     { replayJson }
                                                 → { verified, rank?, isPersonalBest?, reason? }
  GET   /api/leaderboards/{w}x{h}    [no auth]  ?limit=50
                                                 → { totalEntries, entries: [{ rank, displayName, time, gameId }] }
  GET   /api/leaderboards/all        [no auth]  ?limit=50
                                                 → { totalEntries, entries: [{ rank, displayName, time, gameId, boardWidth, boardHeight }] }
  GET   /api/leaderboards/{w}x{h}/me [auth]     → { rank, time, gameId } | 404
  GET   /api/leaderboards/all/me     [auth]     → { rank, time, gameId, boardWidth, boardHeight } | 404
  GET   /api/replays/{gameId}        [no auth]  → { replayJson } | 404 (verified scores only)
  ```
  - `GET /api/replays/{gameId}`: if score is top-50, returns replay with gzip-base64 snapshot; if not, returns replay without snapshot. Client handles both cases.

### Phase 4: Server Tests

- [ ] **xUnit tests** (`server/ArrowThing.Server.Tests/ScoresTests.cs`, `LeaderboardTests.cs`)
  - Submit valid replay → `verified = true`, correct rank, `isPersonalBest = true`.
  - Submit faster second game → score updated, `isPersonalBest = true`, rank improves.
  - Submit slower second game → score unchanged, `isPersonalBest = false`, original rank returned.
  - Submit same `gameId` → idempotent, same result returned.
  - Submit malformed JSON → `verified = false`, reason present.
  - Submit invalid replay (non-clearable clear) → `verified = false`, reason present.
  - Rate limit: verified score updates over threshold in 1 hour → 429.
  - Top-50 replay includes snapshot field; non-top-50 does not.
  - Score displaced from top-50 by a new submission has its snapshot immediately stripped.
  - Leaderboard by size: correct ordering and rank numbers.
  - Leaderboard "all": area-first then speed ordering.
  - Player entry: correct rank; 404 when no score for this size.
  - Replay fetch: returns JSON for verified score; 404 for unknown gameId.
  - Display name: rename user, re-query leaderboard → new name in response.

### Phase 5: Client — ApiClient Extensions

- [ ] **`ApiClient` additions** (`Assets/Scripts/View/ApiClient.cs`)
  - `SubmitScoreAsync(replayJson)` → `SubmitResultResponse?` (`verified`, `rank`, `isPersonalBest`, `reason`)
  - `GetLeaderboardAsync(width, height, limit)` → `GlobalLeaderboardResponse?` (`totalEntries`, `entries`)
  - `GetLeaderboardAllAsync(limit)` → `GlobalLeaderboardResponse?` (`totalEntries`, `entries`)
  - `GetPlayerEntryAsync(width, height)` → `PlayerEntryResponse?`
  - `GetPlayerEntryAllAsync()` → `PlayerEntryResponse?`
  - `GetReplayAsync(gameId)` → `string?` (raw replayJson; caller handles snapshot presence/absence)
  - All return null on any error. Never throw.

### Phase 6: Client — Score Submission

- [ ] **`ScoreSubmitter`** (`Assets/Scripts/View/ScoreSubmitter.cs`)
  Static helper class. No MonoBehaviour, no component wiring.

  **`static async Task<SubmitResultResponse?> TrySubmitAsync(ReplayData replay)`**
  - Not logged in or email not verified → return null immediately (no network call).
  - Serialize `ReplayData` to JSON via `JsonConvert.SerializeObject`.
  - Call `ApiClient.SubmitScoreAsync(replayJson)` with a 5-second timeout.
  - Return response or null on failure/timeout.

### Phase 7: Client — VictoryController Submission

- [ ] **`VictoryController` changes** (`Assets/Scripts/View/VictoryController.cs`)
  - At the start of the victory sequence: fire `ScoreSubmitter.TrySubmitAsync(replay)` as a background task. Let it run while the zoom/fade animation plays. Await it just before the modal appears.
  - "New Best!" / gold timer / "View Leaderboard" remain purely local — no global comparison anywhere in the victory flow.
  - If submission returns null (failed/timed out): show a small, low-prominence note below the victory modal content ("Could not submit score") and a "Retry" button. Tapping Retry calls `TrySubmitAsync` again; on success, hide the note and button.
  - If submission succeeds: no visible indication beyond the note/button being absent. The player can check the global leaderboard themselves.

### Phase 8: Client — Leaderboard Scene Global Tab

- [ ] **`LeaderboardScreenController` global tab** (`Assets/Scripts/View/LeaderboardScreenController.cs`)
  - On switch to Global for a specific size tab: call `ApiClient.GetLeaderboardAsync(width, height, 50)` and `ApiClient.GetPlayerEntryAsync(width, height)` in parallel.
  - On "All" tab: call `GetLeaderboardAllAsync(50)` and `GetPlayerEntryAllAsync()` in parallel.
  - Show spinner while in flight. Refresh button re-fetches — no caching between navigations.

  **Top list (entries 1–50)**
  - Same rank/name/time layout as local entries.
  - Player's own entry highlighted if present.
  - Replay button: calls `ApiClient.GetReplayAsync(gameId)` → deserialize → if snapshot is absent, pass seed+params to `ReplayViewController` to restore via `BoardSetupHelper` (seed path). If snapshot present (base64-gzip), decompress and attach before launching.
  - Empty list: show "No scores yet — be the first!" in the list area.

  **Player panel** (pinned below the list, more visually prominent than list rows)

  | State | Content |
  |-------|---------|
  | Server unreachable | "Can't connect to the server. Scores are only saved locally." — replaces list + panel |
  | Not logged in | "Register or log in to appear on the global leaderboard." — "Register or log in" is a tappable link that opens Settings scrolled to Account |
  | Logged in, unverified email | "Verify your email to submit scores to the global leaderboard." |
  | Logged in, verified, no score for this size | "No scores yet for this board size. Play a game to enter the leaderboard." |
  | Logged in, verified, rank ≤ 50 | "Your best: #N of T · M:SS.mmm" — corresponding row in top list is highlighted |
  | Logged in, verified, rank > 50 | "Your best: #N of T · M:SS.mmm" — outside visible top 50, rank still shown |

- [ ] **`ReplayViewController` no-snapshot path** (`Assets/Scripts/View/ReplayViewController.cs`)
  - When launching from a global replay without a snapshot: pass `seed`, `width`, `height`, `maxArrowLength` to `BoardSetupHelper` for board regeneration instead of snapshot restoration. The rest of playback is unchanged.
  - `GameSettings` or a new `ReplaySource` field will need to carry these params through the scene transition if not already present.
  - After board regeneration completes, write the resulting snapshot back into the player's local replay file (via `LeaderboardManager`) so subsequent views load instantly. This path only occurs when a player views their own non-top-50 score — you can only view someone else's replay if they're in the top 50, which is guaranteed to already have a snapshot.

### Phase 9: Docs & TechnicalDesign Update

- [ ] Update `docs/TechnicalDesign.md`:
  - Add `ReplayVerifier` to Domain types.
  - Add `ScoreSubmitter` to View layer scripts.
  - Update `VictoryController` (background submission, retry note).
  - Update `LeaderboardScreenController` (global tab, player panel states, refresh button).
  - Update `ReplayViewController` (no-snapshot seed-restore path).

- [ ] Update `docs/OnlineRoadmap.md`:
  - Mark new server endpoints as implemented.
  - Mark `ReplayVerifier`, `ScoreSubmitter` as done.
  - Move global leaderboards from Planned to Current State.
  - Record best-only score model and snapshot storage strategy.

---

## Manual Test Cases

Run after implementation. Record pass/fail.

| # | Scenario | Expected |
|---|----------|----------|
| 1 | Verified account + server reachable → complete game | Submission fires silently during victory animation |
| 2 | Scenario 1 succeeds → open leaderboard, switch to Global | Score appears, correctly ranked; player panel shows rank |
| 3 | Submission times out → victory modal appears | "Could not submit score" note + Retry button shown |
| 4 | Hit Retry from scenario 3 → server reachable | Submission succeeds; note and Retry button disappear |
| 5 | Hit Retry from scenario 3 → still unreachable | Note remains; Retry stays available |
| 6 | Submit same gameId twice (Retry after success) | Idempotent; same result returned; no duplicate on leaderboard |
| 7 | Submit slower time on same board size | Server keeps faster existing score; no update |
| 8 | Submit faster time on same board size | Server updates score; rank improves |
| 9 | Unverified account → complete game | No submission attempted; no note shown; local-only |
| 10 | Not logged in → complete game | No submission; local-only |
| 11 | Server unreachable at submission | Fails with note + Retry; local score still recorded |
| 12 | Global tab → not logged in | "Register or log in" link; tap → Settings opens at Account |
| 13 | Global tab → unverified | "Verify your email..." in player panel |
| 14 | Global tab → verified, no score for this size | "No scores yet..." in player panel |
| 15 | Global tab → rank ≤ 50 | Player panel shows rank; corresponding row highlighted in list |
| 16 | Global tab → rank > 50 | Player panel shows actual rank (e.g., #87); not in top-50 list |
| 17 | Global tab → server unreachable | Full-panel error; local tab unaffected |
| 18 | Empty global leaderboard (no entries for size) | "No scores yet — be the first!"; player panel correct for their state |
| 19 | Rename account → refresh global leaderboard | New name shown; local leaderboard still shows old submission-time name |
| 20 | Hit Refresh on global tab | Spinner shown; updated data rendered |
| 21 | "All" tab → Global | Ranked by biggest board first, then speed; player panel shows representative score |
| 22 | Global entry → play replay (top-50, has snapshot) | Snapshot decoded; board loads instantly; replay plays correctly |
| 23 | Global entry → play replay (non-top-50, no snapshot) | Board regenerated from seed; replay plays correctly; snapshot written back to local file |
| 23b | Play same non-top-50 replay a second time | Board loads from local snapshot; no regeneration |
| 24 | Tampered replay submitted (invalid events) | Server rejects; `verified: false`; score not stored |
| 25 | Tampered replay submitted (modified board snapshot) | Snapshot mismatch detected; `verified: false`; score not stored |
| 26 | Rate limit: >10 verified score updates in 1 hour | 429 returned; client shows failure note + Retry |
| 27 | Multi-device: no local PB on device B, finish game | Submission always attempted; server updates if better than stored global best |
