# Zadana API — Load Testing

Four k6 scenarios covering the full performance envelope:

| File | Purpose | Duration | Peak RPS |
|------|---------|----------|----------|
| `capacity-1000u-500orders.js` | Validate the requested target shape (1k active customers / 500 active orders / 20 vendors / 10 reviewers / 200 drivers) | configurable, default 18 min | workload-shaped |
| `steady-realistic.js` | Validate SLO at expected peak (1k users / 500 drivers / 1k orders / 200 vendors) | 10 min | ~50 |
| `stress-extreme.js`   | Find the breaking point across every hot path simultaneously | 8 min | ~17,000 combined |
| `spike-write-storm.js` | Worst-case write storm on driver location endpoint | 4 min | 10,000 |

## Prerequisites

1. **k6** installed: <https://k6.io/docs/get-started/installation/>
   - Windows: `winget install k6`
   - macOS: `brew install k6`
   - Linux: see docs
2. **Staging environment** with the latest build deployed.
   - Do NOT run against production without rate-limiter exclusions.
   - The default tokens in your dev seed are NOT valid for staging — generate fresh ones.
3. **Test machine** with at least 4 vCPU and 8 GB RAM. k6 is a network-bound load generator; if it's CPU-pinned, your "API capacity" measurement will actually be measuring k6.

## Get fresh tokens

```powershell
# Customer
$customerLogin = Invoke-RestMethod -Method Post -Uri "$BASE_URL/api/customer/auth/login" `
  -ContentType "application/json" -Body '{"email":"customer@zadana.test","password":"Pass123!"}'
$env:CUSTOMER_TOKEN = $customerLogin.accessToken

# Driver, Vendor, Admin — same pattern against /api/driver/auth/login etc.
# Pick a real order id for the customer to track:
$env:SAMPLE_ORDER_ID = "<a known order GUID>"
```

## Run the steady-state SLO check (start here)

For the exact capacity target you asked for:

```powershell
k6 run `
  -e BASE_URL=https://staging.zadana.com `
  -e CUSTOMER_TOKENS=$env:CUSTOMER_TOKENS `
  -e DRIVER_TOKENS=$env:DRIVER_TOKENS `
  -e VENDOR_TOKENS=$env:VENDOR_TOKENS `
  -e REVIEWER_TOKENS=$env:REVIEWER_TOKENS `
  -e ORDER_IDS=$env:ORDER_IDS `
  -e VENDOR_IDS=$env:VENDOR_IDS `
  -e ADDRESS_IDS=$env:ADDRESS_IDS `
  scripts/load-tests/capacity-1000u-500orders.js
```

This profile ramps to:
- `1000` active customer sessions
- `500` active order polling sessions
- `200` active driver GPS sessions
- `20` vendor panel sessions
- `10` reviewer/admin sessions

It writes only driver location pings by default. It does not mutate order statuses, approve records, or create/cancel orders.

```powershell
k6 run `
  -e BASE_URL=https://staging.zadana.com `
  -e CUSTOMER_TOKEN=$env:CUSTOMER_TOKEN `
  -e DRIVER_TOKEN=$env:DRIVER_TOKEN `
  -e ADMIN_TOKEN=$env:ADMIN_TOKEN `
  -e VENDOR_TOKEN=$env:VENDOR_TOKEN `
  scripts/load-tests/steady-realistic.js
```

**Pass criteria** (built into the thresholds):
- error rate < 0.5%
- browse p95 < 150 ms, p99 < 300 ms
- driver ping p95 < 100 ms, p99 < 200 ms
- admin p95 < 300 ms, p99 < 600 ms

If any threshold fails, k6 exits non-zero and prints which one breached.

## Push it until it breaks (stress)

```powershell
k6 run `
  -e BASE_URL=https://staging.zadana.com `
  -e CUSTOMER_TOKEN=$env:CUSTOMER_TOKEN `
  -e DRIVER_TOKEN=$env:DRIVER_TOKEN `
  -e ADMIN_TOKEN=$env:ADMIN_TOKEN `
  -e VENDOR_TOKEN=$env:VENDOR_TOKEN `
  -e WEBHOOK_SECRET=$env:WEBHOOK_SECRET `
  -e SAMPLE_ORDER_ID=$env:SAMPLE_ORDER_ID `
  scripts/load-tests/stress-extreme.js
```

This one is meant to fail at some point. Watch the output for the stage where:
- `http_req_failed` jumps above 5%, OR
- p99 doubles between two consecutive stages, OR
- you see `connection refused` / `i/o timeout` in stderr

That's your ceiling. Note the RPS at which it happened.

## Write tsunami

```powershell
k6 run `
  -e BASE_URL=https://staging.zadana.com `
  -e DRIVER_TOKEN=$env:DRIVER_TOKEN `
  scripts/load-tests/spike-write-storm.js
```

This isolates the driver-location write path. Pair it with watching:
- SQL Server: `sys.dm_exec_requests`, `sys.dm_os_wait_stats`
- Application Insights / Prometheus: `dotnet_threadpool_queue_length`, EF command duration histogram
- Redis: `INFO clients`, `INFO stats`

## What to record

For each run, capture:

```text
Run date    : 2026-05-23 21:30
Branch / SHA: main @ <sha>
Test        : steady-realistic
RPS reached : 50 (target hit)
p50 / p95 / p99 by scenario:
  customer browse: 12 / 45 / 92 ms
  driver ping    : 8  / 28 / 70 ms
  admin orders   : 60 / 190 / 410 ms
  ...
errors      : 0.02%
CPU peak    : 28%
Memory peak : 720 MB
DB conns peak: 38
Notes       : KPI cache cold for first 30s, warmed up cleanly.
```

Drop these in `scripts/load-tests/results/<date>.md` so we can track regressions over time.

## Reading the results

- `iteration_duration` and `http_req_duration` are k6 totals (DNS + TLS + write + read). For backend latency, look at `http_req_waiting` (TTFB) instead.
- `vus` (virtual users) is allocation-side; what matters is `iterations` × time → effective RPS.
- The `discardResponseBodies: true` flag in our scripts means k6 doesn't keep response bodies in memory. Disable it locally if you need to debug a specific failing response.

## Common gotchas

- **Self-signed cert on staging** → keep `insecureSkipTLSVerify: true` (already set).
- **Rate limiter blocks k6** → flip the kill-switch in `appsettings.Staging.json` (or `appsettings.Local.json`):
  ```json
  "RateLimiter": {
    "DisableGlobal": true,
    "GlobalPermitsPerSecond": 200
  }
  ```
  Or raise the cap without disabling, e.g. `"GlobalPermitsPerSecond": 5000`. The switch only applies in non-Production environments — Production always enforces the global limiter, no matter what the config says.
- **k6 itself becomes the bottleneck** → if `iteration_duration` floors at ~1 ms with idle CPU on the API host, you've maxed out k6's HTTP client. Split across two machines.
- **Hot warmup** → the first 10-15 seconds always look bad because of EF query cache, JIT, and HybridCache cold tier. Discard them when reading p99.

## Beyond k6

For sustained chaos / soak testing, consider:
- **Artillery** — easier YAML scenarios, weaker stats.
- **NBomber** — .NET native, runs in-process for tight feedback loops.
- **Locust** — Python, friendlier for non-engineers writing scenarios.

For the Zadana stack as it stands, k6 + the three scripts above cover the regression-test, SLO-check, and capacity-planning needs.
