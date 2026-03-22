#!/bin/bash
# Sets up /home/deploy/arrow-thing/ from the repo deploy configs.
# Run from the repo root: ./server/deploy/setup.sh
# Secrets (.env, origin certs) must be placed manually afterward.

set -euo pipefail

DEPLOY_DIR="/home/deploy/arrow-thing"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

mkdir -p "$DEPLOY_DIR/nginx/certs"

cp "$SCRIPT_DIR/docker-compose.yml" "$DEPLOY_DIR/"
cp "$SCRIPT_DIR/init-db.sh" "$DEPLOY_DIR/"
chmod +x "$DEPLOY_DIR/init-db.sh"
cp "$SCRIPT_DIR/nginx/nginx.conf" "$DEPLOY_DIR/nginx/"
cp "$SCRIPT_DIR/nginx/cloudflare-origin-pull.pem" "$DEPLOY_DIR/nginx/certs/"

echo "Deploy configs copied to $DEPLOY_DIR"
echo ""
echo "Manual steps remaining:"
[ ! -f "$DEPLOY_DIR/.env" ] && echo "  - Create $DEPLOY_DIR/.env (see server/.env.sample)"
[ ! -f "$DEPLOY_DIR/nginx/certs/origin.pem" ] && echo "  - Place origin.pem in $DEPLOY_DIR/nginx/certs/"
[ ! -f "$DEPLOY_DIR/nginx/certs/origin-key.pem" ] && echo "  - Place origin-key.pem in $DEPLOY_DIR/nginx/certs/"
echo "Done."
