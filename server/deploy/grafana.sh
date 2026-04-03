#!/usr/bin/env bash
# Opens a Grafana dashboard via SSH tunnel to the VPS.
# Reads VPS_HOST and VPS_USER from server/.env (or environment).
#
# Usage: ./server/deploy/grafana.sh
#
# Add to server/.env:
#   VPS_HOST=your-vps-ip-or-hostname
#   VPS_USER=deploy

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="$SCRIPT_DIR/../.env"

if [ -f "$ENV_FILE" ]; then
    # Source only VPS_HOST and VPS_USER, ignoring other vars
    VPS_HOST="${VPS_HOST:-$(grep '^VPS_HOST=' "$ENV_FILE" | cut -d= -f2-)}"
    VPS_USER="${VPS_USER:-$(grep '^VPS_USER=' "$ENV_FILE" | cut -d= -f2-)}"
fi

VPS_HOST="${VPS_HOST:?Set VPS_HOST in server/.env or environment}"
VPS_USER="${VPS_USER:-deploy}"

echo "Opening SSH tunnel to $VPS_USER@$VPS_HOST:3000 → localhost:3000"
echo "Grafana will be available at http://localhost:3000"
echo "Press Ctrl+C to close the tunnel."
echo ""

ssh -N -L 3000:localhost:3000 "$VPS_USER@$VPS_HOST"
