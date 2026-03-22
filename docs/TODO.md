# Remaining Setup Steps

Everything below picks up from the current state. Steps 1–8 of the VPS setup are complete (server created, hardened, Docker installed, `.env` written). Domain `arrow-thing.com` is purchased on Cloudflare. Old GitHub Pages CNAME records have been deleted. Deploy workflow has been updated to use Cloudflare Wrangler action.

---

## Part 1: Cloudflare Pages (game hosting)

### 1. Create the Pages project
- Cloudflare dashboard → Workers & Pages → Create → Pages → **Direct Upload** (not "Connect to Git" — the build happens in GitHub Actions, not Cloudflare).
- Project name: `arrow-thing`.
- Upload any placeholder file to complete creation (it'll be overwritten on first deploy).

### 2. Set custom domain
- In the Pages project → Custom domains → Add `arrow-thing.com`.
- Cloudflare automatically creates the DNS records needed. Verify they appear in the DNS tab.
- Also add `www.arrow-thing.com` and set up a redirect rule (Cloudflare dashboard → Rules → Redirect Rules) to redirect `www` to the apex domain.

### 3. Add GitHub secrets
- Repo → Settings → Secrets and variables → Actions → New repository secret:
  - `CLOUDFLARE_API_TOKEN` — Cloudflare dashboard → My Profile → API Tokens → Create Token → "Edit Cloudflare Workers" template. Permissions needed: `Cloudflare Pages: Edit`, `Account Settings: Read`.
  - `CLOUDFLARE_ACCOUNT_ID` — Cloudflare dashboard → Workers & Pages overview → right sidebar, "Account ID".

### 4. Test the deploy
- Trigger the workflow manually (Actions → Deploy WebGL to Cloudflare Pages → Run workflow) or create a release.
- Verify `arrow-thing.com` serves the game.

### 5. Disable GitHub Pages
- Repo → Settings → Pages → set source to "None" (or delete the `gh-pages` branch if one exists).
- Remove the `github-pages` environment from repo settings if present.

---

## Part 2: Cloudflare DNS & TLS (API)

### 6. API DNS record
- Cloudflare DNS tab → Add record:
  - Type: `AAAA`, Name: `api`, Content: `2a01:4ff:f0:4178::1`, Proxy: **on** (orange cloud).

### 7. SSL/TLS mode
- Cloudflare → SSL/TLS → set to **Full (Strict)**.
- This works for both the Pages domain (Cloudflare-to-Cloudflare, no origin cert needed) and the API subdomain (Cloudflare-to-origin, requires origin cert — next step).

### 8. Generate origin certificate
- Cloudflare → SSL/TLS → Origin Server → Create Certificate.
- Hostnames: `api.arrow-thing.com`.
- Key type: RSA (2048) or ECDSA — either is fine.
- Validity: 15 years.
- **Save both the certificate and the private key.** Cloudflare only shows the private key once.

### 9. Install origin cert on VPS
- SSH into the VPS as `deploy`.
- Create the cert directory and write the files:
  ```
  mkdir -p /home/deploy/arrow-thing/nginx/certs
  nano /home/deploy/arrow-thing/nginx/certs/origin.pem      # paste certificate
  nano /home/deploy/arrow-thing/nginx/certs/origin-key.pem   # paste private key
  chmod 600 /home/deploy/arrow-thing/nginx/certs/origin-key.pem
  ```

### 10. Cloudflare cache & security rules
- **Cache rule**: Cloudflare → Caching → Cache Rules → Create rule.
  - Match: `Hostname equals api.arrow-thing.com`
  - Action: Bypass cache.
  - All API responses must not be cached.
- **Rate limiting** (optional first layer): Cloudflare → Security → WAF → Rate limiting rules.
  - `/api/auth/login` and `/api/auth/register`: 5 req/min per IP.
  - Can be added later — nginx rate limiting (step 13) is the primary layer.

---

## Part 3: Docker Compose & Nginx (VPS)

### 11. Docker Compose file
- Create `/home/deploy/arrow-thing/docker-compose.yml` on the VPS:
  ```yaml
  services:
    api:
      image: ghcr.io/vicplusplus/arrow-thing-api:latest
      # Or build from source:
      # build: .
      restart: unless-stopped
      expose:
        - "5000"
      environment:
        - ConnectionStrings__Default=Host=db;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
        - Jwt__Secret=${JWT_SECRET}
        - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
        - ASPNETCORE_URLS=http://+:5000
      depends_on:
        db:
          condition: service_healthy
      logging:
        driver: json-file
        options:
          max-size: "10m"
          max-file: "3"

    db:
      image: postgres:16-alpine
      restart: unless-stopped
      # No 'ports:' — only reachable within Docker network.
      # Docker bypasses UFW/iptables for published ports, so never
      # publish database ports to the host.
      expose:
        - "5432"
      environment:
        - POSTGRES_USER=${POSTGRES_USER}
        - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
        - POSTGRES_DB=${POSTGRES_DB}
      volumes:
        - pgdata:/var/lib/postgresql/data
        - ./init-db.sh:/docker-entrypoint-initdb.d/init-db.sh:ro
      healthcheck:
        test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
        interval: 5s
        timeout: 3s
        retries: 5
      logging:
        driver: json-file
        options:
          max-size: "10m"
          max-file: "3"

    nginx:
      image: nginx:alpine
      restart: unless-stopped
      ports:
        - "80:80"
        - "443:443"
      volumes:
        - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
        - ./nginx/certs:/etc/nginx/certs:ro
      depends_on:
        - api
      logging:
        driver: json-file
        options:
          max-size: "10m"
          max-file: "3"

  volumes:
    pgdata:
  ```
- **Critical**: only `nginx` has `ports:`. The `api` and `db` services use `expose:` only (internal to the Docker network). This ensures Docker doesn't bypass the firewall for those services.

### 12. PostgreSQL init script
- Create `/home/deploy/arrow-thing/init-db.sh` — runs once on first database creation:
  ```bash
  #!/bin/bash
  set -e

  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
      -- Revoke default public schema access
      REVOKE ALL ON SCHEMA public FROM PUBLIC;
      GRANT USAGE, CREATE ON SCHEMA public TO $POSTGRES_USER;

      -- The application user gets DML only (EF Core migrations need CREATE for now,
      -- so we grant CREATE on schema. If a separate migration user is added later,
      -- tighten this to DML-only.)
      GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO $POSTGRES_USER;
      ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO $POSTGRES_USER;
  EOSQL
  ```
- `chmod +x /home/deploy/arrow-thing/init-db.sh`

### 13. Nginx config
- Create `/home/deploy/arrow-thing/nginx/nginx.conf`:
  ```nginx
  worker_processes auto;

  events {
      worker_connections 512;
  }

  http {
      # Rate limiting zones — keyed on real client IP from Cloudflare
      limit_req_zone $http_cf_connecting_ip zone=auth:10m rate=5r/m;
      limit_req_zone $http_cf_connecting_ip zone=api:10m rate=30r/m;

      # Timeouts
      proxy_connect_timeout 10s;
      proxy_read_timeout 30s;
      proxy_send_timeout 30s;

      # Redirect HTTP to HTTPS
      server {
          listen 80;
          listen [::]:80;
          server_name api.arrow-thing.com;
          return 301 https://$host$request_uri;
      }

      # HTTPS — Cloudflare Origin cert
      server {
          listen 443 ssl;
          listen [::]:443 ssl;
          server_name api.arrow-thing.com;

          ssl_certificate     /etc/nginx/certs/origin.pem;
          ssl_certificate_key /etc/nginx/certs/origin-key.pem;
          ssl_protocols       TLSv1.2 TLSv1.3;

          client_max_body_size 1m;

          # Security headers
          add_header X-Content-Type-Options nosniff always;
          add_header X-Frame-Options DENY always;
          add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

          # CORS — allow only the game's domain
          set $cors_origin "https://arrow-thing.com";

          # Auth endpoints — strict rate limit
          location ~ ^/api/auth/(register|login)$ {
              limit_req zone=auth burst=3 nodelay;

              # CORS headers
              add_header Access-Control-Allow-Origin $cors_origin always;
              add_header Access-Control-Allow-Methods "GET, POST, PATCH, OPTIONS" always;
              add_header Access-Control-Allow-Headers "Authorization, Content-Type" always;

              if ($request_method = OPTIONS) {
                  add_header Access-Control-Allow-Origin $cors_origin;
                  add_header Access-Control-Allow-Methods "GET, POST, PATCH, OPTIONS";
                  add_header Access-Control-Allow-Headers "Authorization, Content-Type";
                  add_header Access-Control-Max-Age 86400;
                  return 204;
              }

              proxy_pass http://api:5000;
              proxy_set_header Host $host;
              proxy_set_header X-Forwarded-For $http_cf_connecting_ip;
              proxy_set_header X-Forwarded-Proto $scheme;
          }

          # All other API routes
          location /api/ {
              limit_req zone=api burst=10 nodelay;

              # CORS headers
              add_header Access-Control-Allow-Origin $cors_origin always;
              add_header Access-Control-Allow-Methods "GET, POST, PATCH, OPTIONS" always;
              add_header Access-Control-Allow-Headers "Authorization, Content-Type" always;

              if ($request_method = OPTIONS) {
                  add_header Access-Control-Allow-Origin $cors_origin;
                  add_header Access-Control-Allow-Methods "GET, POST, PATCH, OPTIONS";
                  add_header Access-Control-Allow-Headers "Authorization, Content-Type";
                  add_header Access-Control-Max-Age 86400;
                  return 204;
              }

              proxy_pass http://api:5000;
              proxy_set_header Host $host;
              proxy_set_header X-Forwarded-For $http_cf_connecting_ip;
              proxy_set_header X-Forwarded-Proto $scheme;
          }

          # Health check (no rate limit)
          location = /health {
              proxy_pass http://api:5000;
              proxy_set_header Host $host;
          }

          # Deny everything else
          location / {
              return 404;
          }
      }
  }
  ```

---

## Part 4: CI/CD for API deployment

### 14. Generate CI SSH key
- On your local machine:
  ```
  ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/arrowthing_ci
  ```
- No passphrase (GitHub Actions can't enter one).
- Copy the public key to the VPS:
  ```
  ssh deploy@2a01:4ff:f0:4178::1 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys" < ~/.ssh/arrowthing_ci.pub
  ```
- Add the **private key** as a GitHub Actions secret: `VPS_SSH_KEY`.
- Add the VPS address as a secret: `VPS_HOST` = `2a01:4ff:f0:4178::1`.

### 15. API deploy workflow
- Create `.github/workflows/deploy-api.yml` in the repo (will be written when the server project is built). Key steps:
  - Trigger on push to `main` (paths: `server/**`) or manual dispatch.
  - Build Docker image and push to `ghcr.io`.
  - SSH into VPS: `docker compose pull api && docker compose up -d api`.
  - Run health check: `curl -f https://api.arrow-thing.com/health`.

---

## Part 5: PostgreSQL backups

### 16. Backup cron job
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

### 17. Test a restore (do this after first real data exists)
  ```
  gunzip -c /home/deploy/backups/arrowthing_YYYY-MM-DD.sql.gz | docker compose exec -T db psql -U arrowthing arrowthing
  ```

---

## Part 6: Monitoring & reliability

### 18. Verify Docker restart policy
- Already set in `docker-compose.yml` (`restart: unless-stopped`).
- Test: `docker compose up -d`, then `docker kill arrow-thing-api-1` — it should restart automatically.
- Verify containers start on reboot: `sudo reboot`, SSH back in, `docker ps`.

### 19. Disk monitoring
- Add to crontab:
  ```
  # Alert if disk usage > 80%
  0 */6 * * * df -h / | awk 'NR==2 && int($5) > 80 {print "DISK WARNING: " $5 " used"}' >> /home/deploy/disk-alerts.log
  ```
- Check `disk-alerts.log` periodically, or later pipe to a notification service.

### 20. External uptime monitoring
- Sign up for UptimeRobot (free tier) or similar.
- Add a monitor for `https://api.arrow-thing.com/health`, check every 5 minutes.
- Configure email alerts for downtime.

---

## Part 7: Post-setup hardening (optional, do after everything works)

### 21. Restrict origin to Cloudflare only
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
- This means the VPS only accepts web traffic from Cloudflare — direct access by IP is blocked. Scanners and bots that bypass Cloudflare are rejected at the firewall.

### 22. Review and tighten
- Verify no unnecessary ports are open: `sudo ufw status verbose`.
- Verify Docker is not publishing unexpected ports: `docker ps --format "{{.Ports}}"`.
- Verify PostgreSQL is not reachable from host: `curl -v localhost:5432` should fail.
