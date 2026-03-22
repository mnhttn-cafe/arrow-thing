# Remaining Setup Steps

Everything below picks up from the current state:
- **VPS**: Steps 1–8 complete (Hetzner CCX13 created, hardened, Docker installed, `.env` written). Origin certs installed. Cron jobs installed (backups, disk monitoring). CI SSH key authorized.
- **Cloudflare**: All configuration complete (see below).
- **Deploy configs**: `docker-compose.yml`, `nginx.conf`, `init-db.sh`, `setup.sh`, and Cloudflare origin pull CA cert are version-controlled in `server/deploy/`.
- **GitHub secrets**: `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_API_TOKEN`, `DISCORD_WEBHOOK_URL`, `UNITY_EMAIL`, `UNITY_LICENSE`, `UNITY_PASSWORD`, `VPS_HOST`, `VPS_SSH_KEY`.

## Cloudflare Configuration (completed)

All changes made on the Cloudflare dashboard for `arrow-thing.com`:

### DNS Records
| Type | Name | Content | Proxy |
|------|------|---------|-------|
| AAAA | `api` | `2a01:4ff:f0:4178::1` | On (orange cloud) |
| *(Pages-managed)* | `@` | Cloudflare Pages | Automatic |
| *(Pages-managed)* | `www` | Cloudflare Pages | Automatic |

### Pages
- **Project**: `arrow-thing` (direct upload via GitHub Actions)
- **Custom domains**: `arrow-thing.com`, `www.arrow-thing.com`
- **Deployed via**: `cloudflare/wrangler-action@v3` in `.github/workflows/deploy.yml`

### SSL/TLS
- **Mode**: Full (Strict)
- **Origin certificate**: ECC, 15-year validity, hostname `api.arrow-thing.com`. Cert and key stored on VPS at `/home/deploy/arrow-thing/nginx/certs/`.
- **Authenticated Origin Pulls**: Enabled (zone-level). Nginx verifies Cloudflare's client cert using the public CA cert (`server/deploy/nginx/cloudflare-origin-pull.pem`).

### Rules
- **Redirect Rule**: `www.arrow-thing.com*` → `https://arrow-thing.com${1}` (301). Wildcard redirect preserving path and query string.
- **Cache Rule**: Hostname equals `api.arrow-thing.com` → Bypass cache. All API responses are never cached.
- **Rate Limiting Rule**: `(http.request.uri.path eq "/api/auth/login") or (http.request.uri.path eq "/api/auth/register")` → Block for 10 seconds after 5 requests per 10 seconds per IP.

### Hetzner Cloud Firewall
- Inbound: TCP 22, 80, 443 from any IPv4/IPv6
- Outbound: no rules (default allow)

---

## Remaining Steps (blocked on server project)

### 1. API deploy workflow
- Create `.github/workflows/deploy-api.yml` when the server project is built. Key steps:
  - Trigger on push to `main` (paths: `server/**`) or manual dispatch.
  - Build Docker image and push to `ghcr.io`.
  - SSH into VPS: pull latest image, sync deploy configs from repo, `docker compose up -d api`.
  - Health check: `curl -f https://api.arrow-thing.com/health`.

### 2. First deploy & smoke test
- `docker compose up -d` on VPS. Verify all three containers start.
- Test: `curl -f https://api.arrow-thing.com/health` returns 200.

### 3. Verify Docker restart policy
- `docker kill arrow-thing-api-1` — should restart automatically.
- `sudo reboot`, SSH back in, `docker ps` — all containers should be running.

### 4. Test backup restore (after first real data)
  ```
  gunzip -c /home/deploy/backups/arrowthing_YYYY-MM-DD.sql.gz | docker compose exec -T db psql -U arrowthing arrowthing
  ```

### 5. External uptime monitoring
- Sign up for UptimeRobot (free tier) or similar.
- Add a monitor for `https://api.arrow-thing.com/health`, check every 5 minutes.

### 6. Restrict origin to Cloudflare only (after everything works)
- Update UFW to allow ports 80/443 only from [Cloudflare's IP ranges](https://www.cloudflare.com/ips/):
  ```bash
  sudo ufw delete allow 80/tcp
  sudo ufw delete allow 443/tcp
  for ip in 2400:cb00::/32 2606:4700::/32 2803:f800::/32 2405:b500::/32 2405:8100::/32 2a06:98c0::/29 2c0f:f248::/32; do
      sudo ufw allow from $ip to any port 80,443 proto tcp
  done
  ```

### 7. Final review
- `sudo ufw status verbose` — no unnecessary ports.
- `docker ps --format "{{.Ports}}"` — only nginx publishes ports.
- `curl -v localhost:5432` — should fail (Postgres not exposed).
