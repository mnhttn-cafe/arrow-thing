#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

# Create .env from sample if it doesn't exist
if [ ! -f .env ]; then
    cp .env.sample .env
    sed -i 's/^POSTGRES_PASSWORD=$/POSTGRES_PASSWORD=localdev/' .env
    sed -i 's/^JWT_SECRET=$/JWT_SECRET=local-dev-jwt-secret-not-for-production/' .env
    sed -i 's/^ADMIN_API_KEY=$/ADMIN_API_KEY=local-dev-admin-key/' .env
    sed -i 's/^ASPNETCORE_ENVIRONMENT=Production$/ASPNETCORE_ENVIRONMENT=Development/' .env
    sed -i 's/^GRAFANA_ADMIN_PASSWORD=$/GRAFANA_ADMIN_PASSWORD=admin/' .env
    echo "Created .env with dev defaults. Edit if needed."
fi

# Start PostgreSQL
docker compose -f docker-compose.dev.yml up -d
echo "Waiting for PostgreSQL..."
until docker exec arrowthing-dev pg_isready -U arrowthing -q 2>/dev/null; do
    sleep 1
done
echo "PostgreSQL ready."

# Run the API (applies migrations on startup)
dotnet run --project ArrowThing.Server
