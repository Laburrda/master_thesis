-- Descriptive statistics for a finished run; these are not pass/fail checks.

-- Outcome mix. Reported next to throughput and latency, because the share of
-- rejections grows with the number of users.
SELECT status, COALESCE(rejection_reason, '-') AS reason, count(*) AS orders
FROM orders
GROUP BY 1, 2
ORDER BY 1, 2;

-- End-to-end time to the final state, in milliseconds (processed_at - created_at).
-- Read from the database after the run, so no polling disturbs the load.
SELECT count(*) AS n,
       round(percentile_cont(0.50) WITHIN GROUP (ORDER BY e)::numeric, 1) AS p50_ms,
       round(percentile_cont(0.95) WITHIN GROUP (ORDER BY e)::numeric, 1) AS p95_ms,
       round(percentile_cont(0.99) WITHIN GROUP (ORDER BY e)::numeric, 1) AS p99_ms
FROM (SELECT extract(epoch FROM (processed_at - created_at)) * 1000 AS e
      FROM orders
      WHERE processed_at IS NOT NULL) t;
