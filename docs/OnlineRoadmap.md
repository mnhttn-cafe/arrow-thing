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

### Server Foundation

- **ASP.NET Core 9 Minimal API** — lightweight (~30-50 MB idle RAM in Docker), C#, shares domain code. Use Workstation GC in container (`<ServerGarbageCollection>false</ServerGarbageCollection>`) for lower memory on small VPS.
- **Entity Framework Core** — ORM. PostgreSQL for production, SQLite for dev/testing.
- **BCrypt** — password hashing.
- **JWT** — stateless auth tokens.

### VPS Hosting

**Provider**: Hetzner Cloud CCX13 — 2 dedicated vCPU (AMD), 8 GB RAM, 80 GB SSD, ~$14.49/mo ($19.99/mo after April 1 2026 price adjustment). Ashburn (US East) datacenter. IPv6-only (no IPv4 add-on); Cloudflare proxy provides IPv4 reachability for clients.

**Stack**: Docker Compose (ASP.NET API + PostgreSQL) behind Nginx reverse proxy, fronted by Cloudflare for TLS termination, IPv4→IPv6 translation, and DDoS protection.

**Current state**: VPS provisioned and hardened (SSH key-only, UFW, fail2ban, unattended-upgrades, Docker). Origin certs and `.env` in place. Backup and disk monitoring cron jobs installed. CI SSH key authorized. Deploy configs version-controlled in `server/deploy/`.

#### VPS Layout

```
/home/deploy/
├── arrow-thing/                        # live deployment directory
│   ├── docker-compose.yml              # from repo (server/deploy/)
│   ├── init-db.sh                      # from repo (server/deploy/)
│   ├── .env                            # manual (secrets, not in repo)
│   └── nginx/
│       ├── nginx.conf                  # from repo (server/deploy/nginx/)
│       └── certs/
│           ├── origin.pem              # manual (Cloudflare origin cert)
│           ├── origin-key.pem          # manual (origin private key)
│           └── cloudflare-origin-pull.pem  # downloaded by setup.sh
├── repo/                               # git clone of the project
└── backups/                            # daily pg_dump output
```

Run `server/deploy/setup.sh` from the repo root to sync configs, validate nginx, and install cron jobs.

#### Docker Compose — three services

- **api** — ASP.NET Core app. Exposes port 5000 internally only. Reads connection string and JWT secret from environment.
- **db** — PostgreSQL 16. Named volume for data persistence (`pgdata`). Not exposed to host network. Init script grants DML-only privileges to the app user.
- **nginx** — Reverse proxy. Only service with published ports (80, 443). Cloudflare Origin cert for Full (Strict) TLS. Authenticated origin pulls verify requests come from Cloudflare. Rate limiting on auth endpoints (5 req/min) and general API (30 req/min), keyed on `CF-Connecting-IP`. CORS restricted to `https://arrow-thing.com`.

Docker bypasses UFW for published ports — this is why only nginx has `ports:` and all inter-container communication uses Docker's internal DNS.

#### Cloudflare Configuration (arrow-thing.com)

| Setting | Value |
|---------|-------|
| **DNS**: `api` AAAA | `2a01:4ff:f0:4178::1`, proxied |
| **DNS**: `@`, `www` | Cloudflare Pages (automatic) |
| **Pages project** | `arrow-thing`, deployed via `cloudflare/wrangler-action@v3` |
| **Pages custom domains** | `arrow-thing.com`, `www.arrow-thing.com` |
| **SSL/TLS mode** | Full (Strict) |
| **Origin certificate** | ECC, 15-year, `api.arrow-thing.com` |
| **Authenticated Origin Pulls** | Enabled (zone-level) |
| **Redirect Rule** | `www.arrow-thing.com*` → `https://arrow-thing.com${1}` (301) |
| **Cache Rule** | `api.arrow-thing.com` → bypass cache |
| **Rate Limiting Rule** | `/api/*` → block 10s after 60 req/10s per IP |

**Hetzner Cloud Firewall**: inbound TCP 22, 80, 443 from any; default-allow outbound.

#### CI/CD Deployment

- **WebGL**: GitHub Actions builds Unity, deploys to Cloudflare Pages via Wrangler. Split into build + deploy jobs.
- **API**: GitHub Actions workflow (to be created with server project). Build Docker image → push to `ghcr.io` → SSH to VPS → pull + `docker compose up -d api` → health check.
- **GitHub secrets**: `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_API_TOKEN`, `DISCORD_WEBHOOK_URL`, `UNITY_EMAIL`, `UNITY_LICENSE`, `UNITY_PASSWORD`, `VPS_HOST`, `VPS_SSH_KEY`.

#### Backups & Monitoring

- **Backups**: daily `pg_dump` at 04:00 UTC, gzipped, 14-day retention. Installed via `setup.sh`.
- **Disk monitoring**: cron alert every 6 hours if usage exceeds 80%.
- **Docker restart policy**: `restart: unless-stopped` on all services.
- **Logging**: JSON log driver, `max-size: 10m`, `max-file: 3`.
- **External uptime**: UptimeRobot (free tier) on `https://api.arrow-thing.com/health` (to be set up after first deploy).
- **Database connection pooling**: EF Core default; PgBouncer sidecar if needed later.

#### Post-First-Deploy Checklist

- Verify all three containers start: `docker compose up -d`, check `docker ps`.
- Health check: `curl -f https://api.arrow-thing.com/health` returns 200.
- Restart policy: `docker kill arrow-thing-api-1` → auto-restart. Reboot → all containers running.
- Test backup restore after first real data.
- Restrict UFW ports 80/443 to [Cloudflare IP ranges](https://www.cloudflare.com/ips/) only.
- Final review: `sudo ufw status verbose`, `docker ps --format "{{.Ports}}"`, verify Postgres not reachable from host.

### Accounts

Email-based authentication with verification, password reset, and email change flows. No OAuth, no usernames — email is the sole login identifier.

- **Register** with email + password + display name → receive JWT. Verification email sent via Resend.
- **Login** with email + password → receive JWT. Locked accounts receive 403.
- JWT included in `Authorization: Bearer` header for authenticated endpoints.
- **Email**: unique, case-insensitive, used for login. Must be verified to submit scores to leaderboards.
- **Display names**: shown on leaderboards. 2-24 chars, allows spaces and Unicode. Changeable anytime. Not required to be unique.
- **Passwords**: minimum 8 chars, BCrypt hashed.
- **SecurityStamp**: included in JWT, validated on every authenticated request. Bumping invalidates all existing tokens.

**Email flows** (via Resend HTTP API) — all flows use 6-digit codes entered in-app (no browser pages):
- **Email verification**: 6-digit code emailed on registration. 10-minute expiry. Resend with 5-minute cooldown.
- **Password reset**: 6-digit code emailed on forgot-password request. 10-minute expiry. 5-minute cooldown.
- **Email change**: requires current password. 6-digit code sent to new email (10-min expiry). Notification sent to old email referencing Discord for support. Race-condition safe (checks uniqueness at confirmation time).

**Admin tooling** (protected by `X-Admin-Key` header, not JWT):
- **Lock account**: sets `LockedAt`, clears all tokens, reverts pending email changes, bumps SecurityStamp (invalidates all JWTs). Locked accounts cannot log in (403).
- **Unlock account**: clears `LockedAt`, generates password reset code, sends reset email.

**Client UI**:
- **Account icon button** in the **top-right** of the main menu. Always visible.
  - **Not logged in**: full-screen account panel with login (default), register, forgot password forms.
  - **Logged in**: account info (masked email, verify status, display name change, change email, logout).
- **`AccountManager`** (view layer) — manages 10 forms: login, register, verify code, forgot password, reset password, account info, change email, confirm email code, change password, change display name. Calls `GetMeAsync()` on account info show to refresh state. All forms clear fields on navigation.
- **`ApiClient`** (view layer) — HTTP client wrapper. Attaches JWT. Handles errors. Stores token/display name/email verified in `PlayerPrefs`.
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
├── ArrowThing.Server/           # ASP.NET Core web API (net9.0)
│   ├── Program.cs               # Minimal API endpoints, DB + JWT middleware wiring
│   ├── Auth/                    # (implemented)
│   │   ├── AuthService.cs       # All auth operations (register, login, verify, reset, email change, lock/unlock)
│   │   ├── AuthDtos.cs          # Request/response records
│   │   ├── PasswordHasher.cs    # BCrypt wrapper
│   │   ├── JwtHelper.cs         # HMAC-SHA256 token generation + validation (SecurityStamp claim)
│   │   ├── IEmailService.cs     # Email service interface
│   │   └── EmailService.cs      # Resend HTTP API wrapper
│   ├── Games/                   # (planned)
│   │   ├── GameService.cs       # Create game, verify replay, submit score
│   │   └── GameSession.cs       # Pending game tracking
│   ├── Leaderboards/            # (planned)
│   │   └── LeaderboardService.cs
│   ├── Data/                    # (implemented)
│   │   ├── AppDbContext.cs      # EF Core context with User DbSet (unique email index)
│   │   └── Migrations/          # CreateUsers, AddEmailAndTokens, AddPendingEmailChange, RemoveUsername
│   └── Models/                  # (implemented for User, planned for Score)
│       ├── User.cs              # Id, Email, DisplayName, PasswordHash, SecurityStamp, verification/reset/email-change code fields, lock fields
│       ├── Score.cs             # Id, UserId, GameId, Time, Seed, BoardConfig, Verified, CreatedAt (planned)
│       └── BoardConfig.cs       # Width, Height (value object for partitioning) (planned)
├── ArrowThing.Domain/           # Shared domain code (netstandard2.1, C# 9)
└── ArrowThing.Server.Tests/     # xUnit integration tests (37 auth tests)
```

### API Endpoints

```
GET    /health                                                       → 200 OK                                          [implemented]

POST   /api/auth/register        { email, password, displayName }    → { message }                                     [implemented]
POST   /api/auth/login           { email, password }                 → { token, displayName, emailVerified }           [implemented]
GET    /api/auth/me              [auth]                              → { email, displayName, emailVerified }           [implemented]
PATCH  /api/auth/me              [auth] { displayName }              → { displayName }                                 [implemented]

POST   /api/auth/verify-code            { email, code }              → { token, displayName, emailVerified }           [implemented]
POST   /api/auth/resend-verification    { email }                    → { message }                                     [implemented]
POST   /api/auth/forgot-password        { email }                    → { message }                                     [implemented]
POST   /api/auth/reset-password         { email, code, newPassword } → { message }                                     [implemented]
POST   /api/auth/change-password [auth] { currentPassword, newPwd }  → { message }                                     [implemented]
POST   /api/auth/change-email    [auth] { newEmail, currentPassword }→ { message }                                     [implemented]
POST   /api/auth/confirm-email-change [auth] { email, code }         → { message }                                     [implemented]

POST   /api/admin/lock-account   [admin] { email }                   → { message }                                     [implemented]
POST   /api/admin/unlock-account [admin] { email }                   → { message }                                     [implemented]

POST   /api/games                [auth] { width, height }           → { gameId, seed, maxArrowLength }                [planned]
POST   /api/games/{id}/submit    [auth] { events, finalTime }       → { verified, rank, isPersonalBest }              [planned]

GET    /api/leaderboards/{w}x{h}?limit=50                           → { entries: [{ rank, displayName, time, gameId }] } [planned]
GET    /api/leaderboards/{w}x{h}/me                [auth]           → { rank, time, personalBest }                    [planned]

GET    /api/replays/{gameId}                                         → { seed, width, height, events, finalTime }      [planned]
```

### Domain Code Sharing

The domain layer (`Cell`, `Arrow`, `Board`, `BoardGeneration`, `ReplayVerifier`) must compile without Unity references. Current state: already true — all domain code is pure C#.

**Approach**: Monorepo with a shared `ArrowThing.Domain.csproj` that compiles the domain source files via relative paths. No symlinks, no NuGet packages, no file copies. Unity continues using the loose `.cs` files directly; the server references the shared project. **Implemented** — domain builds clean against Unity sources, pinned to C# 9 / netstandard2.1 for Unity compatibility. Newtonsoft.Json added as NuGet dependency (Unity ships it natively).

```
arrow-thing/                              # monorepo root
├── Assets/Scripts/Domain/                # source of truth (Unity uses directly)
├── server/
│   ├── ArrowThing.sln                    # solution file for all server projects
│   ├── ArrowThing.Domain/
│   │   └── ArrowThing.Domain.csproj      # netstandard2.1 C# 9, <Compile Include="../../Assets/Scripts/Domain/**/*.cs" />
│   ├── ArrowThing.Server/                # ASP.NET Core net9.0, <ProjectReference> to Domain
│   │   └── ArrowThing.Server.csproj
│   └── ArrowThing.Server.Tests/          # xUnit integration tests, <ProjectReference> to Server
│       └── ArrowThing.Server.Tests.csproj
```

The domain `.csproj` targets `netstandard2.1` with `LangVersion 9` for compatibility with Unity's C# 9 compiler. The server targets `net9.0`. No code duplication — one source of truth, two consumers.

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
  Id                          GUID
  Email                       string (unique, case-insensitive, login identifier)
  DisplayName                 string (shown on leaderboards, changeable)
  PasswordHash                string (BCrypt)
  SecurityStamp               string (GUID, included in JWT, bumped to invalidate sessions)
  CreatedAt                   DateTime
  EmailVerifiedAt             DateTime? (null = unverified)
  VerificationCode            string? (6-digit code)
  VerificationCodeExpiresAt   DateTime?
  LastVerificationEmailAt     DateTime?
  PasswordResetCode           string? (6-digit code)
  PasswordResetCodeExpiresAt  DateTime?
  LastPasswordResetEmailAt    DateTime?
  PendingEmail                string? (new email awaiting confirmation)
  PendingEmailCode            string? (6-digit code)
  PendingEmailCodeExpiresAt   DateTime?
  LockedAt                    DateTime? (non-null = locked, blocks login)

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
| `ApiClient` | View | Done | HTTP client, JWT attachment, all auth endpoints (register/login/me/display name/forgot password/resend verification/change email), token storage in PlayerPrefs |
| `AccountManager` | View | Done | Full-screen account panel with 6 forms (login/register/verify/forgot password/account info/change email), masked email display |
| `ConfirmModal` | View | Done | Reusable confirm modal wrapper (configures ConfirmModal.uxml template) |
| `OnlineController` | View | Planned | Coordinates online flow (request game → play → submit) |
| `ServerHealthCheck` | Editor | Done | Editor menu item (Tools > Arrow Thing) to test server connectivity |

## Modified Scripts

| Script | Changes |
|--------|---------|
| `InputHandler` | ~~Record events to `ReplayRecorder` on each tap~~ (done) |
| `MainMenuController` | Trophy button on mode select (done). Account icon button (done). Reusable ConfirmModal for quit/clear-scores (done) |
| `VictoryController` | Personal best gold highlight, "New Best!" indicator, "View Leaderboard" button (done). Inline top-10 leaderboard (planned) |
| `GameController` | Refactored to use `BoardSetupHelper` (done). Wire `OnlineController` (planned) |
| `GameSettings` | `StartReplay`/`ClearReplay` for replay scene transition, `LeaderboardFocusGameId` for auto-scroll (done). Server `Seed` field (planned) |
| `BoardView` | `ClearArrowAnimated`, `UpdateClearableHighlights`, `ClearAllHighlights` (done) |
| `ArrowView` | `SetHighlight(bool)` for clearable highlighting (done) |

---

## Testing Plan

### Automated (NUnit EditMode)
- `ReplayVerifier`: valid replays pass, invalid replays (wrong cell, skipped arrow, bad order) fail

### Automated (Server Integration — 32 tests)
- Auth: register, login (email-based), duplicate email rejection, validation errors
- Auth: display name change (`PATCH /api/auth/me`), `GET /api/auth/me`
- Email verification: verify token, resend with rate limiting
- Password reset: forgot password, reset with token, expired token
- Email change: request, confirm, wrong password, same email, invalid token, race condition (email taken)
- Account lock/unlock: lock invalidates sessions + blocks login, unlock sends reset email + allows recovery
- Admin: valid/invalid/missing X-Admin-Key
- Game sessions: create, submit valid replay (with `isPersonalBest` check), reject invalid replay (planned)
- Leaderboards: ranking correctness, partitioning by board size, personal best query (planned)
- Replays: fetch stored replay by gameId (planned)

### Manual
- **Full online flow**: register → play → submit → see score on leaderboard → play replay
- **Offline play**: server unreachable → game works normally → score appears on local leaderboard only → no errors
- **Account UI**: account icon (top-right) → full-screen login/register → email verification → display name change → email change → logout
- **Victory personal best**: gold timer during board-clear, gold highlight in inline leaderboard, "New Best!" text
- **Leaderboard scene**: tabs, local/global toggle, top 50, replay playback
- **Error handling**: server down mid-game, network timeout, expired token
