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

- **ASP.NET Core 8 Minimal API** — lightweight (~30-50 MB idle RAM in Docker), C#, shares domain code. Use Workstation GC in container (`<ServerGarbageCollection>false</ServerGarbageCollection>`) for lower memory on small VPS.
- **Entity Framework Core** — ORM. PostgreSQL for production, SQLite for dev/testing.
- **BCrypt** — password hashing.
- **JWT** — stateless auth tokens.

### VPS Hosting

**Provider**: Hetzner Cloud CCX13 — 2 dedicated vCPU (AMD), 8 GB RAM, 80 GB SSD, ~$14.49/mo ($19.99/mo after April 1 2026 price adjustment). Ashburn (US East) datacenter. IPv6-only (no IPv4 add-on); Cloudflare proxy provides IPv4 reachability for clients.

**Stack**: Docker Compose (ASP.NET API + PostgreSQL) behind Nginx reverse proxy, fronted by Cloudflare for TLS termination, IPv4→IPv6 translation, and DDoS protection.

#### Initial Server Setup

1. **Create the VPS** — Hetzner Cloud Console. Dedicated CPU CCX13 (AMD), Ashburn location, Ubuntu 24.04 LTS image, IPv6-only networking. Add SSH key during creation (no password-only access from the start).

2. **First login & system update**
   - SSH in as `root` with the key added at creation.
   - `apt update && apt upgrade -y` — patch everything immediately.
   - `apt install -y ufw fail2ban curl git unattended-upgrades` — baseline tools.

3. **Create a deploy user**
   - `adduser deploy` — dedicated non-root user.
   - `usermod -aG sudo deploy` — sudo access for administration.
   - Copy the SSH authorized key to the deploy user: `rsync --archive --chown=deploy:deploy ~/.ssh /home/deploy/`.
   - Verify SSH login as `deploy` before proceeding.

4. **Harden SSH** (`/etc/ssh/sshd_config`)
   - `PermitRootLogin no` — disable root SSH entirely.
   - `PasswordAuthentication no` — key-only auth.
   - `PubkeyAuthentication yes`
   - `MaxAuthTries 3`
   - `AllowUsers deploy`
   - Restart sshd: `systemctl restart sshd`.

5. **Firewall (UFW)**
   - `ufw default deny incoming && ufw default allow outgoing`
   - `ufw allow OpenSSH` (port 22)
   - `ufw allow 80/tcp` (HTTP)
   - `ufw allow 443/tcp` (HTTPS)
   - `ufw enable`
   - PostgreSQL (5432) is **not** exposed — only reachable within the Docker network.
   - Optional: restrict ports 80/443 to [Cloudflare's IP ranges](https://www.cloudflare.com/ips/) only, so the origin server is not directly accessible. Reduces exposure to scanners.

6. **Fail2Ban**
   - Default config protects SSH out of the box. Enable and start: `systemctl enable fail2ban && systemctl start fail2ban`.
   - Bans IPs after repeated failed SSH attempts.

7. **Automatic security updates**
   - `dpkg-reconfigure --priority=low unattended-upgrades` — enable automatic security patches.
   - Keeps the OS patched without manual intervention.

#### Docker Setup

8. **Install Docker Engine** — follow the official Docker docs for Ubuntu (apt repository method, not snap).
   - Add `deploy` to the `docker` group: `usermod -aG docker deploy`. Log out and back in.
   - Install Docker Compose plugin (`docker compose`).

9. **Docker Compose layout** on the VPS:
   ```
   /home/deploy/arrow-thing/
   ├── docker-compose.yml
   ├── .env                    # secrets (not in repo)
   └── nginx/
       └── nginx.conf
   ```

10. **Docker Compose file** — three services:
    - **api** — ASP.NET Core app. Built from repo Dockerfile. Exposes port 5000 internally only. Reads connection string and JWT secret from environment.
    - **db** — PostgreSQL 16. Named volume for data persistence (`pgdata`). Not exposed to host network. Configured via `.env` (password, database name).
    - **nginx** — Reverse proxy. Exposed on ports 80 and 443. Proxies to `api:5000`. TLS is terminated at Cloudflare; nginx receives plain HTTP from Cloudflare (or encrypted if using Full (Strict) mode — see TLS section).

11. **`.env` file** (on host, never in repo):
    ```
    POSTGRES_USER=arrowthing
    POSTGRES_PASSWORD=<generated-strong-password>
    POSTGRES_DB=arrowthing
    JWT_SECRET=<generated-256-bit-key>
    ASPNETCORE_ENVIRONMENT=Production
    ```

#### Cloudflare & Domain

12. **Domain** — point a subdomain (e.g., `api.arrowthing.com`) AAAA record to the VPS IPv6 address. Proxy through Cloudflare (orange cloud icon enabled).

13. **Cloudflare configuration**
    - **SSL/TLS mode**: "Full (Strict)" — Cloudflare encrypts to the origin. Requires a valid cert on the origin server.
    - **Origin certificate**: generate a free Cloudflare Origin CA cert (valid 15 years, trusted only by Cloudflare — not browsers). Install on nginx. No certbot or renewal needed.
    - **IPv4 reachability**: Cloudflare's edge has IPv4 addresses. Browsers connect to Cloudflare over IPv4 or IPv6; Cloudflare proxies to the origin over IPv6. Players never need IPv6 support.
    - **DDoS protection**: included on free tier. Automatic L3/L4/L7 mitigation.
    - **Caching**: disable caching for API routes (set a Page Rule or Cache Rule: `api.arrowthing.com/api/*` → Cache Level: Bypass). API responses must not be cached.
    - **Rate limiting**: Cloudflare's free tier includes basic rate limiting rules. Use these for auth endpoints as a first layer; nginx rate limiting remains as a second layer.
    - **WebSocket support**: enabled by default on free tier. Required for future PvP.

#### Nginx Configuration

14. **Reverse proxy config** (`nginx/nginx.conf`):
    - Listen on 443 with the Cloudflare Origin CA cert + key (for Full Strict mode). Listen on 80 and redirect to 443.
    - Proxy `/api/*` to `http://api:5000`.
    - Set `X-Forwarded-For` from Cloudflare's `CF-Connecting-IP` header, `X-Forwarded-Proto`.
    - `client_max_body_size 1m` — replays are small; reject large payloads early.
    - **Rate limiting**: `limit_req_zone` on `/api/auth/register` and `/api/auth/login` (e.g., 5 req/min per IP) to slow brute-force and spam registration. General API routes: 30 req/min per IP. Use `CF-Connecting-IP` as the key (not `$remote_addr`, which would be Cloudflare's edge IP).
    - CORS headers: `Access-Control-Allow-Origin` set to the GitHub Pages domain only (not `*`).
    - Security headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Strict-Transport-Security`.

#### PostgreSQL Hardening

15. **No external access** — PostgreSQL listens only on the Docker internal network. No port binding to the host.

16. **Dedicated database user** — the `.env` credentials are for the application user, not the PostgreSQL superuser. The app user has `CONNECT`, `SELECT`, `INSERT`, `UPDATE`, `DELETE` on application tables only. Create via an init script mounted into the Postgres container.

17. **Backups**
    - Daily `pg_dump` via cron on the host: `docker compose exec db pg_dump -U arrowthing arrowthing | gzip > /home/deploy/backups/arrowthing_$(date +%F).sql.gz`
    - Retain last 14 days, delete older. Simple cron + `find -mtime +14 -delete`.
    - **Test restores periodically** — a backup you haven't tested is not a backup.
    - Optional: upload to Hetzner Object Storage (S3-compatible) or offsite for disaster recovery.

#### CI/CD Deployment

18. **GitHub Actions workflow** — triggers on push to `main` (or a `deploy` tag):
    - Build the Docker image for the API.
    - Push to GitHub Container Registry (ghcr.io) or build on-server.
    - SSH into VPS as `deploy` (using a GitHub Actions secret for the SSH private key).
    - Pull the latest image and `docker compose up -d --build api`.
    - Run EF Core migrations as part of app startup (or a separate migration step).
    - Health check: `curl -f https://api.arrowthing.com/health` after deploy.

19. **SSH key for CI** — generate a dedicated key pair for GitHub Actions. Add the public key to `deploy`'s `authorized_keys`. Store the private key as a GitHub Actions secret. This key is scoped to the `deploy` user only.

#### Monitoring & Reliability

20. **Health endpoint** — `/health` returns 200 OK. Used by CI and optionally by an external uptime monitor (e.g., UptimeRobot free tier, or Hetzner's built-in monitoring).

21. **Docker restart policy** — `restart: unless-stopped` on all services. Containers recover from crashes and survive host reboots.

22. **Logging** — Docker's default JSON log driver. Set `max-size: 10m` and `max-file: 3` in `docker-compose.yml` to prevent disk exhaustion. Application logs to stdout (ASP.NET default).

23. **Disk monitoring** — set up a simple cron alert if disk usage exceeds 80%. `df -h / | awk 'NR==2 {print $5}'` piped to a notification (or just check Hetzner Cloud Console metrics).

24. **Database connection pooling** — EF Core's default pooling is sufficient for low traffic. If needed later, add PgBouncer as a sidecar container.

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
