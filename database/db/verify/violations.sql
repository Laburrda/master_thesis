-- Correctness checks, run after every test run (see scripts/verify_db.sh).
-- Every row must report violations = 0; anything else is a broken invariant.

-- Never sell more than the initial stock, and keep stock_available consistent
-- with the log of confirmed orders.
SELECT 'oversell_or_stock_mismatch' AS check_name, count(*) AS violations FROM (
    SELECT p.id
    FROM products p
    LEFT JOIN orders o ON o.product_id = p.id AND o.status = 'CONFIRMED'
    GROUP BY p.id
    HAVING COALESCE(SUM(o.quantity), 0) > p.stock_initial
        OR p.stock_available <> p.stock_initial - COALESCE(SUM(o.quantity), 0)
        OR p.stock_available < 0
) s
UNION ALL
-- Event-driven variant: nothing may stay unprocessed once the run is over.
SELECT 'pending_left', count(*) FROM orders WHERE status = 'PENDING'
UNION ALL
-- The unique constraint should make this impossible; kept as a safety net.
SELECT 'duplicate_user_key', count(*) FROM (
    SELECT 1 FROM orders GROUP BY user_id, idempotency_key HAVING count(*) > 1
) d
UNION ALL
-- orders.product_id has no foreign key, so a confirmed order for a product
-- that does not exist has to be caught here.
SELECT 'confirmed_without_product', count(*) FROM orders o
WHERE o.status = 'CONFIRMED'
  AND NOT EXISTS (SELECT 1 FROM products p WHERE p.id = o.product_id)
UNION ALL
-- PRODUCT_NOT_FOUND is only valid when the product really is missing.
SELECT 'not_found_but_product_exists', count(*) FROM orders o
WHERE o.rejection_reason = 'PRODUCT_NOT_FOUND'
  AND EXISTS (SELECT 1 FROM products p WHERE p.id = o.product_id);
