# Roadmap

## Current State

- **Arrow coloring** — implemented. `ArrowColoring.AssignColors()` in domain layer; `BoardView` applies palette colors after spawn.
- **Replay recording** — implemented. `ReplayEvent`, `ReplayRecorder`, `ReplayData` exist in domain layer. Events are recorded during play and persisted in save files. Replay viewer built (see below). Server submission not yet built.
- **Local saves / autosave** — implemented. Initial board snapshot persisted in save file; resumes without re-generation.
- **Local leaderboards & personal best** — implemented. `LeaderboardStore` (domain) + `LeaderboardManager` (view) with per-config/global caps, favorites, 3 sort criteria. Dedicated leaderboard scene with 5 size tabs, Local/Global toggle. Victory screen records results, detects personal best.
- **Replay viewer** — implemented. Dedicated scene with `ReplayViewController`, `ReplayPlayer` (domain), seek/speed/play-pause controls, tap indicators, clearable highlighting (electric cyan). Accessed via play button on leaderboard entries.

Versions are tagged when a coherent chunk of work lands, not on a fixed schedule.

---

## Planned Features

### Replay Viewer (Implemented)

A dedicated scene for watching replays. Accessed via the play button on leaderboard entries.

- **Board setup**: restores the board from the initial arrow configuration stored in the replay data (no generation step) via `BoardSetupHelper`. Camera pan/zoom enabled.
- **Playback**: `ReplayPlayer` (domain, pure C#) provides time-based playback with 0.5s lead-in and 1.0s exit padding. `ReplayViewController` advances the player each frame, executes clear (animated pull-out) and reject (bump) events on `BoardView`, and spawns tap indicators.
- **Tap indicator**: `TapIndicatorPool` spawns expanding/fading ring sprites (procedurally generated, no asset) at exact world-space tap positions. White for clears, red for rejects.
- **Controls**: seek slider (drag to scrub forward/backward), play/pause, speed cycle (0.5×/1×/2×/4×), exit button, controls bar toggle (show/hide), clearable highlighting toggle (electric cyan tint on clearable arrows).
- **Seek**: forward seek applies clears incrementally; backward seek rebuilds from snapshot. Pauses playback during drag, resumes on release.

### Server Foundation

- **ASP.NET Core 8 Minimal API** — lightweight, C#, shares domain code.
- **Entity Framework Core** — ORM. PostgreSQL for production, SQLite for dev/testing.
- **BCrypt** — password hashing.
- **JWT** — stateless auth tokens.
- **Hosting**: Self-hosted VPS (Hetzner/DigitalOcean). Docker Compose (ASP.NET + PostgreSQL). Dedicated non-root user with scoped privileges runs the server. SSH key + password auth, no root SSH. Credentials and secrets via `.env` file on the host (not in repo). Deploy via GitHub Actions SSH step.

### Accounts

Minimal. No email verification, no OAuth, no password reset. Just:

- Register with username + password + display name.
- Login → receive JWT.
- JWT included in `Authorization: Bearer` header for authenticated endpoints.
- **Usernames**: unique, case-insensitive, 3-20 chars, alphanumeric + underscores. Used for login only.
- **Display names**: shown on leaderboards. 2-24 chars, allows spaces and Unicode. Changeable anytime. Not required to be unique.
- Passwords: minimum 8 chars, BCrypt hashed.

**Client UI**:
- **Account icon button** in the **top-right** of the main menu. Always visible.
  - **Not logged in**: opens a register/login panel.
  - **Logged in**: opens account management (display name change, logout).
- **`AccountManager`** (new, view layer) — handles registration/login UI, stores JWT in `PlayerPrefs` (WebGL) or platform keychain.
- **`ApiClient`** (new, view layer) — HTTP client wrapper. Attaches JWT. Handles errors.
- No separate "Online" gate — the game is always playable. Logged-in users automatically submit scores; logged-out users play offline.

### Server-Side Verification & Global Leaderboards

Online flow: request game from server → play locally (recording events) → submit replay for verification → score appears on global leaderboard.

**Known limitations** (acceptable for initial release, deferred to later):
- **Bots/automation**: a bot could solve boards optimally. Mitigation deferred — statistical outlier detection is a future concern. The leaderboard is friendly competition, not a ranked ladder.
- **Timing manipulation**: client reports input timestamps. A modified client could lie. Server can reject implausibly fast inter-event gaps as a basic sanity check. Full solution requires server-witnessed timing (future WebSocket path).

### PvP

Real-time garbage mechanics, matchmaking. The replay viewer is essentially a live opponent board — the framework from the replay and server work carries over directly.

---

## Architecture

### High-Level Online Flow

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

---

## Replay Format

JSON. One file per game session.

```jsonc
{
  "version": 2,
  "gameId": "uuid",
  "seed": 12345,
  "boardWidth": 20,
  "boardHeight": 20,
  "maxArrowLength": 40,
  "inspectionDuration": 15.0,
  "boardSnapshot": [
    [{ "X": 0, "Y": 0 }, { "X": 0, "Y": 1 }, { "X": 0, "Y": 2 }],
    // ... one sub-array per arrow (head-to-tail cell order)
  ],
  "events": [
    { "seq": 0, "type": "session_start", "timestamp": "2026-03-19T12:00:00.000Z" },
    { "seq": 1, "type": "start_solve",   "timestamp": "2026-03-19T12:00:15.000Z" },
    { "seq": 2, "type": "clear",         "posX": 5.23, "posY": 12.41, "timestamp": "2026-03-19T12:00:15.000Z" },
    { "seq": 3, "type": "clear",         "posX": 3.10, "posY": 7.67,  "timestamp": "2026-03-19T12:00:15.342Z" },
    { "seq": 4, "type": "reject",        "posX": 10.48, "posY": 4.15, "timestamp": "2026-03-19T12:00:15.781Z" },
    // ...
    { "seq": N, "type": "clear",         "posX": 8.31, "posY": 1.72,  "timestamp": "2026-03-19T12:00:29.529Z" },
    { "seq": N+1, "type": "end_solve",   "timestamp": "2026-03-19T12:00:29.529Z" }
  ],
  "finalTime": 14.529
}
```

- `seq` — monotonically increasing sequence number. **Defines event order.** Timestamps can tie (see below), but `seq` never does.
- `type` — `session_start`, `session_leave`, `session_rejoin`, `start_solve`, `clear`, `reject`, `end_solve`. Rejects are recorded for replay playback fidelity but don't affect verification. Session events are for save/resume bookkeeping.
- `posX`, `posY` — world-space coordinates of the tap. **Present only on `clear` and `reject` events** (omitted from JSON for other types via Newtonsoft `NullValueHandling.Ignore`). The cell is derived via `BoardCoords` (floor to grid cell). Storing the exact position enables the replay viewer to show a tap indicator at the precise location the player tapped.
- `timestamp` — wall-clock time in ISO 8601 UTC. Present on all events. Solve-relative timing is derived by subtracting the `start_solve` timestamp, excluding any `session_leave`→`session_rejoin` gaps.
- `boardSnapshot` — the initial arrow configuration (all arrows before any clears). Each sub-array is one arrow's cells in head-to-tail order. Used for fast resume and replay playback without regeneration.
- `finalTime` — solve time in seconds, derived from event timestamps. Server verifies this matches.

#### Timestamp Ties: `start_solve` + `clear`

The first arrow clear also starts the solve timer (see `InputHandler` / `GameTimer.StartSolve`). This means a single input event produces two replay events with **identical timestamps**: `start_solve` (seq N) followed by `clear` (seq N+1). The verifier must process events by `seq`, not `timestamp`. Timestamps are for timing measurement and replay playback pacing.

### Domain Types for Replay

- **`ReplayEvent`** (domain, implemented) — `int seq`, `string type`, nullable `float? posX`/`posY` (clear/reject only), `string timestamp` (ISO 8601 UTC). Seq is auto-assigned by the recorder. Serialized via Newtonsoft; null fields omitted from JSON.
- **`ReplayRecorder`** (domain, implemented) — accumulates events during play. `Record(type, posX, posY, timestamp)` auto-increments seq. `ToReplayData()` returns the serializable replay.
- **`ReplayVerifier`** (domain, planned) — static class. Takes seed + board config + events, regenerates board, derives cells from positions via `BoardCoords`, simulates clears in order, returns `VerificationResult` (valid/invalid + reason).
- **`InputHandler`** changes (implemented) — calls `ReplayRecorder.Record()` on each tap (clear or reject), passing world-space tap position.

---

## Server

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
  - **Local**: stored on-device via `PlayerPrefs` (backed by `IndexedDB` on WebGL). Includes both online and offline scores. Not synced to server. Capped at top 50 entries + replays per board size to keep storage bounded.
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

Only `Verified = true` scores appear on leaderboards. Verification runs on submission (synchronous for initial release — board regeneration + replay simulation is fast enough).

---

## New Scripts

| Script | Layer | Status | Purpose |
|--------|-------|--------|---------|
| `LeaderboardEntry` | Domain | Done | One leaderboard entry (metadata, no replay data) |
| `LeaderboardStore` | Domain | Done | Pure C# leaderboard storage with caps, sorting, favorites |
| `ReplayPlayer` | Domain | Done | Time-based replay playback engine with speed/seek |
| `LeaderboardManager` | View | Done | Singleton persistence layer (file I/O, GZip replays) |
| `LeaderboardScreenController` | View | Done | Dedicated leaderboard scene: tabs, sorts, context menu, auto-scroll |
| `BoardSetupHelper` | View | Done | Shared board/view/camera setup (extracted from GameController) |
| `ReplayViewController` | View | Done | Replay viewer scene: playback, seek, controls, highlighting |
| `TapIndicator` | View | Done | Expanding/fading ring at tap position during replay |
| `TapIndicatorPool` | View | Done | Object pool for tap indicators with procedural ring sprite |
| `ReplayVerifier` | Domain | Planned | Simulates replay for server-side verification |
| `ApiClient` | View | Planned | HTTP client, JWT, error handling, offline detection |
| `AccountManager` | View | Planned | Account icon UI (login/register/display name/logout), token storage |
| `OnlineController` | View | Planned | Coordinates online flow (request game → play → submit) |

## Modified Scripts

| Script | Changes |
|--------|---------|
| `InputHandler` | ~~Record events to `ReplayRecorder` on each tap~~ (done) |
| `MainMenuController` | Trophy button on mode select (done). Account icon button (planned) |
| `VictoryController` | Personal best gold highlight, "New Best!" indicator, "View Leaderboard" button (done). Inline top-10 leaderboard (planned) |
| `GameController` | Refactored to use `BoardSetupHelper` (done). Wire `OnlineController` (planned) |
| `GameSettings` | `StartReplay`/`ClearReplay` for replay scene transition, `LeaderboardFocusGameId` for auto-scroll (done). Server `Seed` field (planned) |
| `BoardView` | `ClearArrowAnimated`, `UpdateClearableHighlights`, `ClearAllHighlights` (done) |
| `ArrowView` | `SetHighlight(bool)` for clearable highlighting (done) |

---

## Open Questions

1. **WebGL HTTP client**: Unity's `UnityWebRequest` works in WebGL but has CORS constraints. Server must set appropriate CORS headers. Any concerns with the target hosting setup?

---

## Testing Plan

### Automated (NUnit EditMode)
- `ReplayVerifier`: valid replays pass, invalid replays (wrong cell, skipped arrow, bad order) fail

### Automated (Server Integration)
- Auth: register, login, duplicate username rejection, bad password rejection
- Auth: display name change (`PATCH /api/auth/me`)
- Game sessions: create, submit valid replay (with `isPersonalBest` check), reject invalid replay
- Leaderboards: ranking correctness, partitioning by board size, personal best query
- Replays: fetch stored replay by gameId

### Manual
- **Full online flow**: register → play → submit → see score on leaderboard → play replay
- **Offline play**: server unreachable → game works normally → score appears on local leaderboard only → no errors
- **Account UI**: account icon (top-right) → register/login panel → display name change → logout
- **Victory personal best**: gold timer during board-clear, gold highlight in inline leaderboard, "New Best!" text
- **Leaderboard scene**: tabs, local/global toggle, top 50, replay playback
- **Error handling**: server down mid-game, network timeout, expired token
