// Seeds the local dev server with 60 dummy users and 10x10 scores.
// Usage: node seed-leaderboard.js
//
// Registers 60 accounts, verifies them via the fake email capture,
// then submits a valid 10x10 replay for each.
// Your real account will be pushed outside the top 50.

const BASE = "http://localhost:5000";

async function post(path, body, token) {
  const headers = { "Content-Type": "application/json" };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  const res = await fetch(`${BASE}${path}`, {
    method: "POST",
    headers,
    body: JSON.stringify(body),
  });
  return { status: res.status, data: await res.json() };
}

async function get(path, token) {
  const headers = {};
  if (token) headers["Authorization"] = `Bearer ${token}`;
  const res = await fetch(`${BASE}${path}`, { headers });
  return { status: res.status, data: await res.json() };
}

// Build a minimal valid replay JSON for a given seed.
// We can't run BoardGeneration from JS, but the server will verify it.
// So we need to use the /api/scores endpoint which runs ReplayVerifier.
// Instead, we'll insert scores directly into the DB via a special admin endpoint,
// or we can just use the real flow: register, verify, submit.
//
// Problem: we can't build valid replays from JS. The server verifies them.
// Solution: use a psql command to insert dummy scores directly.

const { execSync } = require("child_process");

function psql(sql) {
  const cmd = `docker exec arrowthing-dev psql -U postgres -d arrowthing -c "${sql.replace(/"/g, '\\"')}"`;
  return execSync(cmd, { encoding: "utf8" });
}

async function main() {
  const COUNT = 60;

  console.log(`Seeding ${COUNT} dummy scores into 10x10 leaderboard...`);

  for (let i = 0; i < COUNT; i++) {
    const userId = crypto.randomUUID();
    const gameId = crypto.randomUUID();
    const displayName = `Player${String(i + 1).padStart(3, "0")}`;
    const email = `dummy${i + 1}@test.local`;
    // Scores from 5.0 to 35.0 seconds, evenly spaced
    const time = 5.0 + (i * 30.0) / (COUNT - 1);
    const now = new Date().toISOString();

    // Insert dummy user (no password hash needed, they won't log in)
    psql(`INSERT INTO "Users" ("Id", "Email", "DisplayName", "PasswordHash", "SecurityStamp", "CreatedAt", "FailedLoginAttempts") VALUES ('${userId}', '${email}', '${displayName}', 'dummy', '${crypto.randomUUID()}', '${now}', 0) ON CONFLICT DO NOTHING`);

    // Insert dummy score with minimal replay JSON (no snapshot needed for display)
    const replayJson = JSON.stringify({
      version: 3,
      gameId,
      seed: 12345 + i,
      boardWidth: 10,
      boardHeight: 10,
      maxArrowLength: 5,
      events: [],
      finalTime: time,
    }).replace(/'/g, "''");

    psql(`INSERT INTO "Scores" ("Id", "UserId", "GameId", "Seed", "BoardWidth", "BoardHeight", "MaxArrowLength", "Time", "ReplayJson", "CreatedAt", "UpdatedAt") VALUES ('${crypto.randomUUID()}', '${userId}', '${gameId}', ${12345 + i}, 10, 10, 5, ${time}, '${replayJson}', '${now}', '${now}') ON CONFLICT DO NOTHING`);

    process.stdout.write(`\r  ${i + 1}/${COUNT}`);
  }

  console.log("\nDone. Checking leaderboard...");

  const res = await get("/api/leaderboards/10x10?limit=5");
  console.log(`Top 5 of ${res.data.totalEntries} entries:`);
  for (const e of res.data.entries) {
    console.log(`  #${e.rank} ${e.displayName} — ${e.time.toFixed(3)}s`);
  }
}

main().catch(console.error);
