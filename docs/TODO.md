# Remaining Setup Steps

Everything below picks up from the current state:
- **VPS**: Steps 1–8 complete (Hetzner CCX13 created, hardened, Docker installed, `.env` written).
- **Cloudflare**: All configuration complete (see below).
- **Deploy configs**: `docker-compose.yml`, `nginx.conf`, `init-db.sh`, and Cloudflare origin pull CA cert are version-controlled in `server/deploy/`.

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

## Part 1: Install origin cert on VPS

### 1. Write cert files
- SSH into the VPS as `deploy`:
  ```
  mkdir -p /home/deploy/arrow-thing/nginx/certs
  nano /home/deploy/arrow-thing/nginx/certs/origin.pem      # paste certificate
  nano /home/deploy/arrow-thing/nginx/certs/origin-key.pem   # paste private key
  chmod 600 /home/deploy/arrow-thing/nginx/certs/origin-key.pem
  ```
- These are the only secret files on the VPS besides `.env`. Everything else comes from the repo.

---

## Part 2: Deploy configs to VPS

### 2. Sync repo configs to VPS
- On the VPS, clone or pull the repo and copy deploy configs into place:
  ```
  cd /home/deploy
  git clone https://github.com/vicplusplus/arrow-thing.git repo
  cp repo/server/deploy/docker-compose.yml arrow-thing/
  cp repo/server/deploy/init-db.sh arrow-thing/
  chmod +x arrow-thing/init-db.sh
  cp repo/server/deploy/nginx/nginx.conf arrow-thing/nginx/
  cp repo/server/deploy/nginx/cloudflare-origin-pull.pem arrow-thing/nginx/certs/
  ```
- Final layout on VPS:
  ```
  /home/deploy/arrow-thing/
  ├── docker-compose.yml          # from repo
  ├── init-db.sh                  # from repo
  ├── .env                        # manual (secrets)
  └── nginx/
      ├── nginx.conf              # from repo
      └── certs/
          ├── origin.pem          # manual (Cloudflare origin cert)
          ├── origin-key.pem      # manual (origin private key)
          └── cloudflare-origin-pull.pem  # from repo
  ```

### 3. Test nginx config (before API exists)
- Can't run the full stack yet (no API image), but can validate nginx syntax:
  ```
  docker run --rm -v /home/deploy/arrow-thing/nginx/nginx.conf:/etc/nginx/nginx.conf:ro \
    -v /home/deploy/arrow-thing/nginx/certs:/etc/nginx/certs:ro \
    nginx:alpine nginx -t
  ```

---

## Part 3: CI/CD for API deployment

### 4. Generate CI SSH key
- On your local machine:
  ```
  ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/arrowthing_ci
  ```
- No passphrase (GitHub Actions can't enter one).
- Copy the public key to the VPS:
  ```
  ssh deploy@2a01:4ff:f0:4178::1 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys" < ~/.ssh/arrowthing_ci.pub
  ```
- Add GitHub Actions secrets:
  - `VPS_SSH_KEY` — the private key contents
  - `VPS_HOST` — `2a01:4ff:f0:4178::1`

### 5. API deploy workflow
- Create `.github/workflows/deploy-api.yml` (will be written when the server project is built). Key steps:
  - Trigger on push to `main` (paths: `server/**`) or manual dispatch.
  - Build Docker image and push to `ghcr.io`.
  - SSH into VPS: pull latest image, sync deploy configs from repo, `docker compose up -d api`.
  - Health check: `curl -f https://api.arrow-thing.com/health`.

---

## Part 4: PostgreSQL backups

### 6. Backup cron job
- On the VPS as `deploy`:
  ```
  mkdir -p /home/deploy/backups
  crontab -e
  ```
- Add:
  ```
  # Daily DB backup at 04:00 UTC
  0 4 * * * cd /home/deploy/arrow-thing && docker compose exec -T db pg_dump -U arrowthing arrowthing | gzip > /home/deploy/backups/arrowthing_$(date +\%F).sql.gz

  # Delete backups older than 14 days
  10 4 * * * find /home/deploy/backups -name "*.sql.gz" -mtime +14 -delete
  ```

### 7. Test a restore (do this after first real data exists)
  ```
  gunzip -c /home/deploy/backups/arrowthing_YYYY-MM-DD.sql.gz | docker compose exec -T db psql -U arrowthing arrowthing
  ```

---

## Part 5: Monitoring & reliability

### 8. Verify Docker restart policy
- Already set in `docker-compose.yml` (`restart: unless-stopped`).
- Test: `docker compose up -d`, then `docker kill arrow-thing-api-1` — it should restart automatically.
- Verify containers start on reboot: `sudo reboot`, SSH back in, `docker ps`.

### 9. Disk monitoring
- Add to crontab:
  ```
  # Alert if disk usage > 80%
  0 */6 * * * df -h / | awk 'NR==2 && int($5) > 80 {print "DISK WARNING: " $5 " used"}' >> /home/deploy/disk-alerts.log
  ```
- Check `disk-alerts.log` periodically, or later pipe to a notification service.

### 10. External uptime monitoring
- Sign up for UptimeRobot (free tier) or similar.
- Add a monitor for `https://api.arrow-thing.com/health`, check every 5 minutes.
- Configure email alerts for downtime.

---

## Part 6: Post-setup hardening (do after everything works)

### 11. Restrict origin to Cloudflare only
- Update UFW to allow ports 80/443 only from [Cloudflare's IP ranges](https://www.cloudflare.com/ips/):
  ```bash
  # Remove broad rules
  sudo ufw delete allow 80/tcp
  sudo ufw delete allow 443/tcp

  # Add Cloudflare IPv6 ranges (check the link for current list)
  for ip in 2400:cb00::/32 2606:4700::/32 2803:f800::/32 2405:b500::/32 2405:8100::/32 2a06:98c0::/29 2c0f:f248::/32; do
      sudo ufw allow from $ip to any port 80,443 proto tcp
  done
  ```
- This means the VPS only accepts web traffic from Cloudflare — direct access by IP is blocked.

### 12. Review and tighten
- Verify no unnecessary ports are open: `sudo ufw status verbose`.
- Verify Docker is not publishing unexpected ports: `docker ps --format "{{.Ports}}"`.
- Verify PostgreSQL is not reachable from host: `curl -v localhost:5432` should fail.
