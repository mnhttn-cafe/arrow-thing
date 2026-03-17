# v0.2 — Online & Polish

## Version Scheme

- **v0.1** — Current MVP (single-player, local-only, WebGL). Tag current `main`.
- **v0.1.1** — Map-coloring arrow tinting for readability. Client-only, no server.
- **v0.2** — Online release. Leaderboards, replays, accounts. This plan.
- **v1.0** — PvP (real-time garbage mechanics, matchmaking). The original vision fulfilled. Builds on v0.2 server.

## Features

1. **Authoritative server** — ASP.NET Core, shares domain C# code with client.
2. **Input-based replay system** — Record input events during play, verify server-side.
3. **Size-partitioned leaderboards** — Per board config (10×10, 20×20, 40×40).
4. **Simple account system** — Username/password, JWT auth.
5. **Offline-first client** — Game is always playable. Server connection is optional; offline games skip leaderboard submission.

> **Map-coloring arrow tinting** is scoped to **v0.1.1** (client-only, no server dependency). See Phase 2 below.

---

## Architecture

### High-Level Flow

```
Client                              Server
──────                              ──────
1. Request board         ────────►  Generate seed, store pending game
   (size preset)         ◄────────  Return { gameId, seed, width, height }
   OR: server unreachable →
       generate seed locally,
       mark game as offline

2. Generate board locally           (deterministic — same seed = same board)
   from seed

3. Play game, record input
   events: [{ seq, t, cell }]

4. [online only]
   Submit replay         ────────►  Regenerate board from seed
   { gameId, events }               Simulate all clears in order
                                    Verify: all clears valid, board empty
                         ◄────────  Accept/reject score

5. View leaderboards    ────────►   Query by board config
                         ◄────────  Return ranked entries
```

### Why This Works

- **Deterministic generation**: `Board` + `BoardGeneration` + seeded `Random` = identical boards on client and server. No board state needs to be streamed.
- **Minimal bandwidth**: Only seed + input events travel over the wire. A full game replay is ~50-200 input events (one per arrow cleared).
- **Offline verification**: Server replays inputs asynchronously. No real-time connection needed.
- **Cheat resistance**: Server is authoritative — it regenerates the board and simulates the solve. Fabricated replays that skip arrows or claim impossible clears are rejected. Timing is validated against input event timestamps.
- **Shared code**: The domain layer (`Assets/Scripts/Domain/`) is already Unity-independent pure C#. The server references the same source files directly (shared project). Zero duplication of game logic.
- **Offline-first**: The client can always generate boards locally. Server connection is a bonus (enables leaderboard submission), not a requirement. The game never blocks on network.

### What Can't Be Prevented (v0.2)

- **Bots/automation**: A bot could solve boards optimally. Mitigation is deferred to v1.0 (pattern analysis, statistical outlier detection). For v0.2, this is acceptable — the leaderboard is a friendly competition, not a ranked ladder.
- **Timing manipulation**: Client reports input timestamps. A modified client could lie about timing. Mitigation: server can reject replays where inter-event gaps are implausibly small (< human reaction time). Full solution requires server-witnessed timing (v1.0 WebSocket).

---

## Replay Format

JSON. One file per game session.

```json
{
  "version": 1,
  "gameId": "uuid",
  "seed": 12345,
  "boardWidth": 20,
  "boardHeight": 20,
  "maxArrowLength": 40,
  "events": [
    { "seq": 0, "t": 0.000, "type": "start_solve", "pos": { "x": 5.23, "y": 12.41 } },
    { "seq": 1, "t": 0.000, "type": "clear", "pos": { "x": 5.23, "y": 12.41 } },
    { "seq": 2, "t": 0.342, "type": "clear", "pos": { "x": 3.10, "y": 7.67 } },
    { "seq": 3, "t": 0.781, "type": "reject", "pos": { "x": 10.48, "y": 4.15 } },
    ...
    { "seq": N, "t": 14.529, "type": "clear", "pos": { "x": 8.31, "y": 1.72 } }
  ],
  "finalTime": 14.529
}
```

- `seq` — monotonically increasing sequence number. **Defines event order.** Timestamps can tie (see below), but `seq` never does.
- `t` — seconds since solve start (input-precision, from `InputAction.canceled` timestamps). Used for timing, not ordering.
- `type` — `start_solve`, `clear`, `reject`. Rejects are recorded for replay playback fidelity but don't affect verification.
- `pos` — world-space coordinates of the tap (float pair). The cell is derived via `BoardCoords` (floor to grid cell). Storing the exact position enables the replay viewer to show a tap indicator at the precise location the player tapped, making replays feel authentic rather than snapping to cell centers.
- `finalTime` — last clear event's `t`. Server verifies this matches.

#### Timestamp Ties: `start_solve` + `clear`

The first arrow clear also starts the solve timer (see `InputHandler` / `GameTimer.StartSolve`). This means a single input event produces two replay events with **identical timestamps**: `start_solve` (seq N) followed by `clear` (seq N+1). The verifier must process events by `seq`, not `t`. Timestamps are strictly for timing measurement and replay playback pacing.

### Domain Changes for Replay

- **`ReplayEvent`** (new, domain) — immutable record: `int Seq`, `double Timestamp`, `ReplayEventType Type`, `float PosX`, `float PosY`. Seq is auto-assigned by the recorder.
- **`ReplayRecorder`** (new, domain) — accumulates events during play. `Record(type, posX, posY, timestamp)` auto-increments seq. `ToReplayData()` returns the serializable replay.
- **`ReplayVerifier`** (new, domain) — static class. Takes seed + board config + events, regenerates board, derives cells from positions via `BoardCoords`, simulates clears in order, returns `VerificationResult` (valid/invalid + reason).
- **`InputHandler`** changes — call `ReplayRecorder.Record()` on each tap (clear or reject), passing world-space tap position.

### Replay Viewer

A dedicated scene for watching replays. Accessed via the play button on leaderboard entries.

- **Board setup**: regenerates the board from seed using the same generation path as the game scene. `BoardView` renders the board but user input is disabled.
- **Playback**: `ReplayPlayer` iterates events by `t`, waiting real-time between events to match the original pacing. On each event, it calls the appropriate `BoardView` method (clear or reject) exactly as `InputHandler` would during live play. All existing animations (pull-out, bump, reject flash) play normally.
- **Tap indicator**: `TapIndicator` spawns a brief visual pulse (expanding ring that fades out) at the exact world-space `pos` from each event. This shows where the player actually tapped, not just which cell was resolved.
- **Timer**: `GameTimerView` displays the replay time, driven by event timestamps rather than real time input.
- **Controls**: back button to return to the leaderboard. No pause/scrub for v0.2 — just real-time playback.

---

## Server

### Technology

- **ASP.NET Core 8 Minimal API** — lightweight, C#, shares domain code.
- **Entity Framework Core** — ORM. PostgreSQL for production, SQLite for dev/testing.
- **BCrypt** — password hashing.
- **JWT** — stateless auth tokens.
- **Hosting**: TBD (Fly.io, Railway, or a VPS). Must support PostgreSQL.

### Project Structure

```
server/
├── ArrowThing.Server/           # ASP.NET Core web API
│   ├── Program.cs               # Minimal API endpoints
│   ├── Auth/
│   │   ├── AuthService.cs       # Registration, login, JWT issuance
│   │   └── PasswordHasher.cs    # BCrypt wrapper
│   ├── Games/
│   │   ├── GameService.cs       # Create game, verify replay, submit score
│   │   └── GameSession.cs       # Pending game tracking
│   ├── Leaderboards/
│   │   └── LeaderboardService.cs
│   ├── Data/
│   │   ├── AppDbContext.cs      # EF Core context
│   │   └── Migrations/
│   └── Models/
│       ├── User.cs              # Id, Username, PasswordHash, CreatedAt
│       ├── Score.cs             # Id, UserId, GameId, Time, Seed, BoardConfig, Verified, CreatedAt
│       └── BoardConfig.cs       # Width, Height (value object for partitioning)
├── ArrowThing.Domain/           # Shared domain code (project reference or symlink)
└── ArrowThing.Server.Tests/     # Server integration tests
```

### API Endpoints

```
POST   /api/auth/register        { username, password, displayName } → { token, displayName }
POST   /api/auth/login           { username, password } → { token, displayName }

PATCH  /api/auth/me              [auth] { displayName } → { displayName }

POST   /api/games                [auth] { width, height } → { gameId, seed, maxArrowLength }
POST   /api/games/{id}/submit    [auth] { events, finalTime } → { verified, rank, isPersonalBest }

GET    /api/leaderboards/{w}x{h}?limit=50          → { entries: [{ rank, displayName, time, gameId }] }
GET    /api/leaderboards/{w}x{h}/me                [auth] → { rank, time, personalBest }

GET    /api/replays/{gameId}                        → { seed, width, height, events, finalTime }
```

### Domain Code Sharing

The domain layer (`Cell`, `Arrow`, `Board`, `BoardGeneration`, `ReplayVerifier`) must compile without Unity references. Current state: already true — all domain code is pure C#.

**Approach**: Monorepo with a shared `ArrowThing.Domain.csproj` that compiles the domain source files via relative paths. No symlinks, no NuGet packages, no file copies. Unity continues using the loose `.cs` files directly; the server references the shared project.

```
arrow-thing/                              # monorepo root
├── Assets/Scripts/Domain/                # source of truth (Unity uses directly)
├── server/
│   ├── ArrowThing.Domain/
│   │   └── ArrowThing.Domain.csproj      # netstandard2.1, <Compile Include="../../Assets/Scripts/Domain/**/*.cs" />
│   ├── ArrowThing.Server/                # ASP.NET Core API, <ProjectReference> to Domain
│   │   └── ArrowThing.Server.csproj
│   └── ArrowThing.Server.Tests/
│       └── ArrowThing.Server.Tests.csproj
```

The domain `.csproj` targets `netstandard2.1` for compatibility with both Unity's C# 9 and .NET 8. No code duplication — one source of truth, two consumers.

---

## Account System

### Scope (v0.2)

Minimal. No email verification, no OAuth, no password reset. Just:

- Register with username + password + display name.
- Login → receive JWT.
- JWT included in `Authorization: Bearer` header for authenticated endpoints.
- **Usernames**: unique, case-insensitive, 3-20 chars, alphanumeric + underscores. Used for login only.
- **Display names**: shown on leaderboards. 2-24 chars, allows spaces and Unicode. Changeable anytime. Not required to be unique.
- Passwords: minimum 8 chars, BCrypt hashed.

### Why Not Anonymous-First?

Anonymous accounts (play without registering, claim a name later) add complexity: merging scores, handling name collisions, persistent device tokens. For v0.2, requiring registration before submitting scores is simpler and sufficient. Anonymous play (without leaderboard submission) still works — just don't call the server.

### Client UI

- **Account icon button** in the **top-right** of the main menu. Always visible.
  - **Not logged in**: opens a register/login panel.
  - **Logged in**: opens account management (display name change, logout).
- **`AccountManager`** (new, view layer) — handles registration/login UI, stores JWT in `PlayerPrefs` (WebGL) or platform keychain.
- **`ApiClient`** (new, view layer) — HTTP client wrapper. Attaches JWT. Handles errors.
- No separate "Online" gate — the game is always playable. Logged-in users automatically submit scores; logged-out users play offline.

---

## Leaderboards

### Partitioning

One leaderboard per board configuration:
- **Small** — 10×10
- **Medium** — 20×20
- **Large** — 40×40

Future board sizes automatically create new partitions (no code change needed — partitioning is by `(width, height)` tuple).

### Display

Two contexts: dedicated leaderboard scene and victory screen inline.

#### Dedicated Leaderboard Scene

Accessed via a button in the **top-right of the mode select screen**.

- **Top 50** entries per partition, showing rank, display name, and time.
- **Tabs** for each board size (Small / Medium / Large).
- **Toggle**: Local vs Global leaderboards.
  - **Global**: fetched from server. Only verified online scores.
  - **Local**: stored on-device (`PlayerPrefs` or local JSON). Includes both online and offline scores. Not synced to server.
- **Play replay button** on each entry — loads the replay for that score. Replays for local scores are stored locally alongside the leaderboard data. Global replays are fetched via `GET /api/replays/{gameId}`.

#### Victory Screen Leaderboard

Shown inline within the victory modal.

- **Top 10** (compact), same local/global toggle.
- If the score is a **new personal best**:
  - Timer text turns **bright gold** during the board-clear sequence (before the modal appears).
  - Personal best entry is **highlighted gold** in the leaderboard.
  - Text indicator: "New Best!" next to the time.
- Player's own entry is always visible (scrolled to if outside top 10).

### Score Model

```
User:
  Id            GUID
  Username      string (unique, case-insensitive, login only)
  DisplayName   string (shown on leaderboards, changeable)
  PasswordHash  string (BCrypt)
  CreatedAt     DateTime

Score:
  Id          GUID
  UserId      FK → User
  GameId      GUID (matches the game session)
  Seed        int
  BoardWidth  int
  BoardHeight int
  Time        double (seconds, millisecond precision)
  Verified    bool
  ReplayData  JSON (stored for replay playback)
  CreatedAt   DateTime
```

Only `Verified = true` scores appear on leaderboards. Verification runs on submission (synchronous for v0.2 — board regeneration + replay simulation is fast enough).

---

## Map-Coloring Arrow Tinting

### Goal

Tint adjacent arrows with different colors so players can visually distinguish arrow boundaries at a glance. Currently all arrows are the same color, making dense boards hard to read.

### Algorithm

- **Adjacency**: Two arrows are adjacent if any of their cells are orthogonal neighbors (share an edge). This is stricter than the dependency graph — two arrows can be adjacent without one blocking the other.
- **Graph coloring**: Greedy coloring with 4-6 colors. The four-color theorem guarantees 4 suffices for planar graphs, and arrow adjacency on a grid is always planar.
- **Implementation**: Build adjacency graph after generation. Greedy assign colors in arrow order (first available color not used by any neighbor). If all 4 are used by neighbors (rare on grids), use a 5th.

### Domain Changes

- **`ArrowColoring`** (new, domain) — static class. `static int[] AssignColors(Board board, int maxColors = 4)`. Returns array indexed by arrow index in `board.Arrows`, values are color indices 0–3.
- **`VisualSettings`** — add `Color[] arrowPalette` (4-6 colors). `ArrowView` reads its color index from the coloring result.

### Integration

- `BoardView` calls `ArrowColoring.AssignColors()` after spawning arrows.
- Each `ArrowView` receives its color index and applies `arrowPalette[colorIndex]` to its material.
- Colors are purely cosmetic — no gameplay effect.

---

## Client-Side Changes Summary

### New Scripts

| Script | Layer | Purpose |
|--------|-------|---------|
| `ReplayEvent` | Domain | Immutable event record (seq, timestamp, type, world-space pos) |
| `ReplayRecorder` | Domain | Accumulates events during play, auto-increments seq |
| `ReplayVerifier` | Domain | Simulates replay for verification (derives cells from positions) |
| `ArrowColoring` | Domain | Graph coloring for arrow tints |
| `ApiClient` | View | HTTP client, JWT, error handling, offline detection |
| `AccountManager` | View | Account icon UI (login/register/display name/logout), token storage |
| `LocalLeaderboard` | View | On-device leaderboard storage (JSON), includes offline scores |
| `LeaderboardView` | View | Leaderboard display UI (top 50 dedicated / top 10 victory inline) |
| `LeaderboardSceneController` | View | Dedicated leaderboard scene: tabs, local/global toggle, replay buttons |
| `OnlineController` | View | Coordinates online flow (request game → play → submit) |
| `ReplayPlayer` | View | Drives replay playback: schedules events by timestamp, triggers clears/rejects on `BoardView` |
| `ReplaySceneController` | View | Replay viewer scene: board + timer + tap indicator + playback controls |
| `TapIndicator` | View | Visual pulse at tap position during replay playback |

### Modified Scripts

| Script | Changes |
|--------|---------|
| `InputHandler` | Record events to `ReplayRecorder` on each tap |
| `BoardView` | Apply arrow coloring after spawn |
| `ArrowView` | Accept color index, apply palette color |
| `VisualSettings` | Add `arrowPalette` color array |
| `MainMenuController` | Add account icon button (top-right), leaderboard button on mode select |
| `VictoryController` | Inline top-10 leaderboard, personal best gold highlight, "New Best!" indicator |
| `GameTimerView` | Gold color on personal best during board-clear sequence |
| `GameController` | Wire `ReplayRecorder`, `OnlineController` |
| `GameSettings` | Add `Seed` field (received from server for online games) |

---

## Implementation Plan

### Phase 1: Map Coloring (v0.1.1 — standalone, no server dependency)
- [ ] Implement `ArrowColoring.AssignColors()` in domain layer
- [ ] Write NUnit tests: adjacency detection, coloring validity (no adjacent same-color)
- [ ] Add `arrowPalette` to `VisualSettings`
- [ ] Wire `BoardView` to call coloring after spawn
- [ ] Wire `ArrowView` to apply palette color
- [ ] Manual test: verify visual distinction on all 3 board sizes
- [ ] Tune palette colors for readability and aesthetics
- [ ] Tag `v0.1.1`

### Phase 2: Local Leaderboards & Personal Best (client-only, no server)
- [ ] Build `LocalLeaderboard` — on-device JSON storage for local scores, partitioned by board size
- [ ] Build dedicated leaderboard scene (`LeaderboardSceneController`)
  - [ ] Board size tabs (Small / Medium / Large)
  - [ ] Local / Global toggle (global shows "Coming Soon" placeholder)
  - [ ] Top 50 list: rank, display name (local default: "You"), time
- [ ] Build inline victory screen leaderboard (`LeaderboardView`)
  - [ ] Top 10 compact, local/global toggle
  - [ ] Gold highlight + "New Best!" for personal best
  - [ ] Player's own entry always visible
- [ ] Wire `GameTimerView` to turn gold during board-clear on personal best
- [ ] Wire leaderboard button into mode select screen (top-right)
- [ ] Manual test: play multiple games, verify local ranking, personal best detection, gold highlights

### Phase 3: Replay System (client-only, no server)
- [ ] Define `ReplayEvent` (with `Seq`) and `ReplayRecorder` in domain layer
- [ ] Define `ReplayVerifier` in domain layer
- [ ] Write NUnit tests: record events → verify replay → assert valid
- [ ] Write NUnit tests: `start_solve` + `clear` same-timestamp ordering via `seq`
- [ ] Wire `InputHandler` to record events during play (pass world-space tap position)
- [ ] Wire `GameController` to create recorder, pass to input handler
- [ ] Store local replays alongside local leaderboard entries
- [ ] Build replay viewer scene (`ReplaySceneController`)
  - [ ] Regenerate board from seed, spawn `BoardView` (read-only, no user input)
  - [ ] `ReplayPlayer` schedules events by `t`, triggers clears/rejects on `BoardView` in real time
  - [ ] `TapIndicator` — visual pulse (expanding ring or dot) at exact tap `pos` on each event
  - [ ] Timer display synced to replay playback
  - [ ] Back button to return to leaderboard
- [ ] Add play replay button to leaderboard entries (local replays from disk)
- [ ] Test: play a game, verify replay passes `ReplayVerifier`, watch it back in viewer

### Phase 4: Server Foundation
- [ ] Create `server/` directory with ASP.NET Core project
- [ ] Set up `ArrowThing.Domain` shared project referencing domain source
- [ ] Verify domain code compiles in both Unity and .NET 8 contexts
- [ ] Set up EF Core with SQLite (dev) and PostgreSQL (prod) providers
- [ ] Implement User model (username, display name, password hash) and auth endpoints (register, login, JWT)
- [ ] Implement display name change endpoint (`PATCH /api/auth/me`)
- [ ] Write integration tests for auth flow (including display name changes)

### Phase 5: Game Sessions & Verification
- [ ] Implement game session creation endpoint (generate seed, store pending)
- [ ] Implement replay submission endpoint (verify, store score + replay data)
- [ ] Return `isPersonalBest` in submission response
- [ ] Wire `ReplayVerifier` into submission flow
- [ ] Implement replay fetch endpoint (`GET /api/replays/{gameId}`)
- [ ] Write integration tests: valid replay accepted, tampered replay rejected
- [ ] Add timing sanity checks (minimum inter-event gap)

### Phase 6: Global Leaderboards & Online Integration
- [ ] Implement leaderboard query endpoints (by board config, personal best, limit param)
- [ ] Write integration tests for leaderboard ranking
- [ ] Build `ApiClient` (HTTP via `UnityWebRequest`, JWT, error handling, timeout/failure detection)
- [ ] Build `AccountManager` — account icon button (top-right of main menu)
  - [ ] Not logged in: register/login panel (username, password, display name)
  - [ ] Logged in: display name change, logout
  - [ ] Token persistence via `PlayerPrefs`
- [ ] Build `OnlineController` (request board from server → play → submit flow)
- [ ] Implement offline fallback: server unreachable → local seed generation, mark game offline, skip submission
- [ ] Wire global leaderboard data into existing leaderboard UI (replace "Coming Soon")
  - [ ] Global replay playback via `GET /api/replays/{gameId}`
- [ ] Wire into `MainMenuController` and `GameController`
- [ ] Manual test: full end-to-end online flow (register → login → play → submit → leaderboard)
- [ ] Manual test: offline flow (server down → local generation → play → local leaderboard only → no errors)
- [ ] Manual test: account management (register, login, change display name, logout, re-login)

### Phase 7: Deployment & CI
- [ ] Choose hosting provider (Fly.io / Railway / VPS)
- [ ] Set up server CI pipeline (build, test, deploy)
- [ ] Configure production PostgreSQL
- [ ] Add server URL configuration to Unity build (environment-based)
- [ ] Update WebGL deploy pipeline to point to production server
- [ ] Tag `v0.1` on current main before merging v0.2 work
- [ ] Tag `v0.2` on release

---

## Open Questions

1. ~~**Domain code sharing mechanism**~~ — **Resolved**: monorepo with shared `.csproj` using relative `<Compile Include>` paths. No symlinks, no NuGet.
2. **Hosting provider**: Fly.io (free tier, good DX), Railway (simple), or self-hosted VPS? Cost and latency considerations.
3. **WebGL HTTP client**: Unity's `UnityWebRequest` works in WebGL but has CORS constraints. Server must set appropriate CORS headers. Any concerns with the target hosting setup?
4. ~~**Leaderboard pagination**~~ — **Resolved**: Top 50 for dedicated view, top 10 inline on victory screen. No cursor pagination for v0.2.
5. ~~**Replay storage**~~ — **Resolved**: Store full replay JSON in the database. Replays are viewable via play buttons on leaderboard entries. Local replays stored on-device alongside local leaderboard data.
6. ~~**Version tagging**~~ — **Resolved**: tag current main as `v0.1`, this work is `v0.2`, PvP is `v1.0`.

---

## Testing Plan

### Automated (NUnit EditMode)
- `ReplayEvent` / `ReplayRecorder`: event accumulation, seq auto-increment, serialization
- `ReplayRecorder`: `start_solve` + `clear` same-timestamp produces correct seq ordering
- `ReplayVerifier`: valid replays pass, invalid replays (wrong cell, skipped arrow, bad order) fail
- `ArrowColoring`: no two adjacent arrows share a color, all arrows colored, deterministic under same board

### Automated (Server Integration)
- Auth: register, login, duplicate username rejection, bad password rejection
- Auth: display name change (`PATCH /api/auth/me`)
- Game sessions: create, submit valid replay (with `isPersonalBest` check), reject invalid replay
- Leaderboards: ranking correctness, partitioning by board size, personal best query
- Replays: fetch stored replay by gameId

### Manual
- **Map coloring** (v0.1.1): visually verify on Small, Medium, Large boards
- **Full online flow**: register → play → submit → see score on leaderboard → play replay
- **Offline play**: server unreachable → game works normally → score appears on local leaderboard only
- **Account UI**: account icon (top-right) → register/login panel → display name change → logout
- **Victory personal best**: gold timer during board-clear, gold highlight in inline leaderboard, "New Best!" text
- **Leaderboard scene**: tabs, local/global toggle, top 50, replay playback
- **Error handling**: server down mid-game, network timeout, expired token
