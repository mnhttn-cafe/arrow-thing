#!/usr/bin/env bash
# provision.sh — Run as root on a fresh Ubuntu 24.04 server.
# Hardens the OS, installs dependencies, clones the repo, places
# the stack, and starts the containers. Pauses once for you to copy
# secrets before bringing the stack up.
#
# Usage:
#   ssh root@<new-server> "bash -s" < server/deploy/provision.sh

set -euo pipefail

REPO_URL="https://github.com/vicplusplus/arrow-thing"
DEPLOY_USER="deploy"
DEPLOY_DIR="/home/deploy/arrow-thing"
REPO_DIR="/home/deploy/repo"

# ── 1. Create deploy user ────────────────────────────────────────────────────

echo "==> Creating $DEPLOY_USER user"
if id "$DEPLOY_USER" &>/dev/null; then
    echo "    User already exists, skipping"
else
    useradd -m -s /bin/bash "$DEPLOY_USER"
fi

mkdir -p "/home/$DEPLOY_USER/.ssh"
# Copy root's authorized_keys so the same SSH key works for deploy user
cp ~/.ssh/authorized_keys "/home/$DEPLOY_USER/.ssh/authorized_keys"
chown -R "$DEPLOY_USER:$DEPLOY_USER" "/home/$DEPLOY_USER/.ssh"
chmod 700 "/home/$DEPLOY_USER/.ssh"
chmod 600 "/home/$DEPLOY_USER/.ssh/authorized_keys"

# ── 2. Install dependencies ──────────────────────────────────────────────────

echo "==> Installing packages"
apt-get update -qq
apt-get install -y -qq ca-certificates curl git ufw fail2ban unattended-upgrades

# Add Docker's official apt repo (docker-compose-plugin is not in the default Ubuntu repos)
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    | tee /etc/apt/sources.list.d/docker.list > /dev/null
apt-get update -qq
apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-compose-plugin

usermod -aG docker "$DEPLOY_USER"

# ── 3. Harden SSH ────────────────────────────────────────────────────────────

echo "==> Hardening SSH (disabling root login)"
sed -i 's/^PermitRootLogin.*/PermitRootLogin no/' /etc/ssh/sshd_config
# If the line doesn't exist at all, append it
grep -q "^PermitRootLogin" /etc/ssh/sshd_config || echo "PermitRootLogin no" >> /etc/ssh/sshd_config
systemctl restart ssh

# ── 4. Clone repo ────────────────────────────────────────────────────────────

echo "==> Cloning repo to $REPO_DIR"
if [ -d "$REPO_DIR/.git" ]; then
    echo "    Repo already exists, pulling latest"
    sudo -u "$DEPLOY_USER" git -C "$REPO_DIR" pull
else
    sudo -u "$DEPLOY_USER" git clone "$REPO_URL" "$REPO_DIR"
fi

# ── 5. Run setup.sh ──────────────────────────────────────────────────────────

echo "==> Running setup.sh (copies configs, downloads Cloudflare CA cert, installs cron jobs)"
sudo -u "$DEPLOY_USER" bash "$REPO_DIR/server/deploy/setup.sh"

# ── 6. Run ufw-setup.sh ──────────────────────────────────────────────────────

echo "==> Running ufw-setup.sh (configures firewall)"
bash "$REPO_DIR/server/deploy/ufw-setup.sh"

# ── 7. Pause for secrets ─────────────────────────────────────────────────────

cat <<'MSG'

==> ACTION REQUIRED — copy secrets to the server before continuing.

From a separate local terminal, run:

    scp ./env-backup deploy@<this-server>:/home/deploy/arrow-thing/.env
    scp ./origin.pem deploy@<this-server>:/home/deploy/arrow-thing/nginx/certs/origin.pem
    scp ./origin-key.pem deploy@<this-server>:/home/deploy/arrow-thing/nginx/certs/origin-key.pem

Press Enter here once all three files are in place.
MSG
read -r

# Verify secrets are present before continuing
MISSING=0
for f in "$DEPLOY_DIR/.env" \
         "$DEPLOY_DIR/nginx/certs/origin.pem" \
         "$DEPLOY_DIR/nginx/certs/origin-key.pem"; do
    if [ ! -f "$f" ]; then
        echo "    MISSING: $f"
        MISSING=1
    fi
done
if [ "$MISSING" -eq 1 ]; then
    echo "ERROR: One or more secrets are missing. Re-run this script from step 7 once they are in place."
    exit 1
fi

# Fix ownership in case scp ran as root
chown -R "$DEPLOY_USER:$DEPLOY_USER" "$DEPLOY_DIR"

# ── 8. Start the stack ───────────────────────────────────────────────────────

echo "==> Starting Docker Compose stack"
sudo -u "$DEPLOY_USER" docker compose -f "$DEPLOY_DIR/docker-compose.yml" up -d

# ── 9. Health check ──────────────────────────────────────────────────────────

echo "==> Waiting for stack to become healthy"
for i in $(seq 1 12); do
    if curl -sf http://localhost:5000/health > /dev/null; then
        echo "    Health check passed (attempt $i)"
        break
    fi
    if [ "$i" -eq 12 ]; then
        echo "ERROR: Health check failed after 12 attempts. Check logs:"
        echo "    docker compose -f $DEPLOY_DIR/docker-compose.yml logs api --tail=50"
        exit 1
    fi
    echo "    Attempt $i failed, retrying in 5s..."
    sleep 5
done

# ── Done ─────────────────────────────────────────────────────────────────────

cat <<'MSG'

==> Server is up and healthy.

Remaining steps (done from your local machine):
  1. Update Cloudflare DNS — api A and AAAA records to this server's IPs
  2. Update GitHub secret VPS_HOST to this server's IP
  3. Update Hetzner Cloud Firewall to cover this server
  4. Verify end-to-end: curl -f https://api.arrow-thing.com/health
  5. Trigger a manual CD deploy: GitHub → Actions → "Deploy Server" → Run workflow
  6. Once confirmed working, delete the old server in Hetzner
MSG
