# VPS IP Rotation Playbook

Use this when provisioning a new server — whether rotating after an IP exposure or migrating to new hardware.

Once you've SSH'd into the new server as root, `provision.sh` handles everything automatically. The only manual steps are provisioning the server in Hetzner, copying three secret files, and updating three external config locations after the stack is healthy.

---

## Convention: never commit real IPs or hostnames

Infrastructure docs use `<vps-ip>`, `<vps-ipv4>`, `<vps-ipv6>` style placeholders. Real values live only in GitHub secrets and the Hetzner/Cloudflare consoles. The pre-commit hook will block commits containing raw IPv4 addresses (excluding private ranges) to enforce this.

---

## What to prepare before starting

You need access to:
- Hetzner Cloud console
- Cloudflare dashboard (`api.arrow-thing.com` DNS)
- GitHub → Settings → Secrets (`VPS_HOST`, `VPS_SSH_KEY`)
- SSH to the current server (to copy secrets off it)

---

## Step 1 — Copy secrets off the old server

```bash
scp deploy@<old-server>:/home/deploy/arrow-thing/.env ./env-backup
scp deploy@<old-server>:/home/deploy/arrow-thing/nginx/certs/origin.pem ./origin.pem
scp deploy@<old-server>:/home/deploy/arrow-thing/nginx/certs/origin-key.pem ./origin-key.pem
```

Keep these locally until the new server is verified healthy, then delete them.

---

## Step 2 — Provision the new server in Hetzner

1. Hetzner Cloud console → **Add Server**
2. **Location**: Ashburn (US East)
3. **Type**: CCX13 (2 vCPU, 8 GB RAM)
4. **Image**: Ubuntu 24.04 LTS
5. **IPv6**: optional — can be disabled if you only need IPv4
6. **SSH key**: add the same public key already on the old server. If rotating after a key compromise, generate a new key pair (`ssh-keygen -t ed25519`), add the new public key here, and update `VPS_SSH_KEY` in GitHub secrets with the new private key.
7. **Firewall**: none yet — the provision script handles UFW
8. Note the new server's **IPv4** (and IPv6 if enabled)

---

## Step 3 — Run the provision script

SSH in as root and pipe the script in directly:

```bash
ssh root@<new-server> "bash -s" < server/deploy/provision.sh
```

The script will:
- Create the `deploy` user and copy your SSH key to it
- Install Docker, git, ufw, fail2ban, unattended-upgrades
- Disable root SSH login
- Clone the repo
- Run `setup.sh` (copies configs, downloads Cloudflare CA cert, installs backup/monitoring cron jobs)
- Run `ufw-setup.sh` (configures firewall — ports 80/443 Cloudflare-only, port 22 open)
- **Pause and ask you to copy the three secret files** (see prompt in the script)
- Start the Docker Compose stack
- Wait for the health check to pass

When the script pauses, open a second terminal and copy the secrets:

```bash
scp ./env-backup deploy@<new-server>:/home/deploy/arrow-thing/.env
scp ./origin.pem deploy@<new-server>:/home/deploy/arrow-thing/nginx/certs/origin.pem
scp ./origin-key.pem deploy@<new-server>:/home/deploy/arrow-thing/nginx/certs/origin-key.pem
```

Then press Enter in the provision script terminal to continue. The script verifies all three files are present before proceeding.

Database migrations run automatically on startup — no manual step needed.

---

## Step 4 — Update Cloudflare DNS

In Cloudflare dashboard for `arrow-thing.com`:

- `api` **A** record → new IPv4 → **Proxied** (orange cloud)
- `api` **AAAA** record → new IPv6 (if enabled) → **Proxied**

Changes take effect immediately through Cloudflare's proxy.

---

## Step 5 — Update GitHub secrets

GitHub → repository → Settings → Secrets and variables → Actions:

- `VPS_HOST` → new server IP
- `VPS_SSH_KEY` → update only if you generated a new key in Step 2

---

## Step 6 — Update Hetzner Cloud Firewall

In Hetzner Cloud console, attach the existing firewall to the new server, or verify the inbound rules (TCP 22, 80, 443 from any) are applied to it.

---

## Step 7 — Verify end-to-end and decommission

```bash
# Production health check via Cloudflare
curl -f https://api.arrow-thing.com/health

# Trigger a manual CD deploy to confirm the full pipeline works
# GitHub → Actions → "Deploy Server" → Run workflow
```

Once the CD deploy succeeds and the health check passes:

1. Hetzner Cloud console → old server → **Delete**
2. Delete your local secret copies: `rm env-backup origin.pem origin-key.pem`
