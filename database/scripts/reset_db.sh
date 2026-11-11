#!/bin/bash
# Resets the database to a clean, freshly seeded state.
# Removing the volume makes the postgres image run db/init/* again on the next start.
set -euo pipefail
cd "$(dirname "$0")/.."

docker compose -f docker-compose.db.yml down -v
docker compose -f docker-compose.db.yml up -d --wait
echo "Database reset and seeded."
