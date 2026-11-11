#!/bin/bash
# Checks correctness after a test run and prints a short summary.
# Exits with 1 if any invariant is broken. Run it before resetting the database.
set -euo pipefail
cd "$(dirname "$0")/.."

PSQL=(docker compose -f docker-compose.db.yml exec -T postgres psql -U shop -d shop -At -F '|')

echo "== Checks =="
OUT=$("${PSQL[@]}" < db/verify/violations.sql)
echo "$OUT"

echo "== Summary =="
"${PSQL[@]}" < db/verify/summary.sql

# each line of the checks looks like "name|count"; any non-zero count is a failure
if echo "$OUT" | awk -F'|' '$2 != 0 { bad = 1 } END { exit !bad }'; then
  echo "FAILED: a correctness invariant was violated" >&2
  exit 1
fi
echo "OK: all invariants hold"
