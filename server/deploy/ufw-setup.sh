#!/bin/bash
# Configures UFW for the Arrow Thing VPS.
# - Ports 80/443: Cloudflare IPs only (fetched from cloudflare.com/ips-v4 and ips-v6)
# - Port 22: open to all (required for GitHub Actions CD)
# - Everything else: denied
#
# Re-run this script whenever Cloudflare's IP ranges change (rare, but happens).
# Cloudflare publishes changes at: https://www.cloudflare.com/ips/

set -euo pipefail

echo "Resetting UFW..."
ufw --force reset

ufw default deny incoming
ufw default allow outgoing

echo "Allowing SSH (port 22) from anywhere..."
ufw allow 22/tcp

echo "Fetching Cloudflare IP ranges..."
CF_V4=$(curl -fsSL https://www.cloudflare.com/ips-v4)
CF_V6=$(curl -fsSL https://www.cloudflare.com/ips-v6)

echo "Allowing ports 80 and 443 from Cloudflare IPs..."
for ip in $CF_V4 $CF_V6; do
    ufw allow from "$ip" to any port 80 proto tcp
    ufw allow from "$ip" to any port 443 proto tcp
done

echo "Enabling UFW..."
ufw --force enable

echo ""
ufw status verbose
echo ""
echo "Done."
