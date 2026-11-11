#!/bin/bash
# Seeds the products table. Runs once, when the container starts with an empty volume.
# The data is deterministic (no random()), so every run and both variants get the same rows.
#
# Parameters (environment variables, defaults in brackets):
#   SEED_TOTAL_PRODUCTS  size of the whole catalogue          [2000]
#   SEED_HOT_COUNT       products 1..N are the "hot" ones     [100]
#   SEED_HOT_STOCK       stock of each hot product            [100]
#   SEED_COLD_STOCK      stock of every other product         [100000]
set -euo pipefail

TOTAL="${SEED_TOTAL_PRODUCTS:-2000}"
HOT="${SEED_HOT_COUNT:-100}"
HOT_STOCK="${SEED_HOT_STOCK:-100}"
COLD_STOCK="${SEED_COLD_STOCK:-100000}"

if [ "$HOT" -gt "$TOTAL" ]; then
  echo "SEED_HOT_COUNT ($HOT) must not exceed SEED_TOTAL_PRODUCTS ($TOTAL)" >&2
  exit 1
fi

echo "Seeding: total=$TOTAL hot=$HOT hot_stock=$HOT_STOCK cold_stock=$COLD_STOCK"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v total="$TOTAL" -v hot="$HOT" -v hot_stock="$HOT_STOCK" -v cold_stock="$COLD_STOCK" <<'SQL'
INSERT INTO products (id, name, category, price, stock_initial, stock_available)
SELECT
    i,
    'Product ' || i,
    (ARRAY['electronics', 'home', 'garden', 'sports', 'fashion',
           'toys', 'books', 'automotive', 'health', 'groceries'])[(i % 10) + 1],
    round((5 + ((i * 7919) % 99500) / 100.0)::numeric, 2),
    CASE WHEN i <= :hot THEN :hot_stock ELSE :cold_stock END,
    CASE WHEN i <= :hot THEN :hot_stock ELSE :cold_stock END
FROM generate_series(1, :total) AS i;

-- ids were inserted explicitly, so move the identity sequence past them
SELECT setval(pg_get_serial_sequence('products', 'id'), (SELECT max(id) FROM products));

ANALYZE products;
SQL
