# Production performance rollout

The application is prepared for CDN caching, Redis-backed output/data cache,
SignalR scale-out, compressed responses, and resilient read retries. The
following infrastructure settings must still be applied outside the repo.

## SQL connection pool

Keep SQL connection pooling enabled and size it to match the API concurrency.
The production connection string should include:

```text
Min Pool Size=10;Max Pool Size=200;
```

The API already uses a pooled EF Core `DbContext` with a pool size of 256.
The SQL pool is separate; its limit must not be left too low when the API is
allowed to process around 200 concurrent database-backed requests. Monitor
database CPU and active sessions before raising `Max Pool Size` beyond 200.

Keep the API and SQL Server in the same datacenter whenever the hosting
provider allows it. Connection pooling reduces connection setup cost, but it
cannot remove network latency between two distant servers.

## Optional edge cache

Cloudflare is optional. If it is not used, the origin output cache, response
compression, persistent HTTP connections, and client polling reductions still
apply. The physical round-trip time from Egypt to a distant server will remain.

If Cloudflare is used later:

Proxy the `api.zadna0.com` DNS record (orange cloud), then create a Cache Rule
for anonymous `GET` requests under:

```text
/api/home
/api/brands
/api/categories
/api/geography
/api/products
```

Use "Eligible for cache" and respect the existing origin TTL. Include the query
string and `Accept-Language` in the cache key.

Create a higher-priority bypass rule for:

```text
/api/orders
/api/cart
/api/checkout
/api/admin
/api/vendor
/api/drivers
*/auth*
/hubs
```

Keep WebSockets enabled. The API emits `X-Zadana-Edge-Cache: eligible` and safe
`Cache-Control` headers only for anonymous public reads.

## Redis

Use a managed Redis instance close to the API. Configure it through hosting
environment variables:

```text
Caching__Redis__ConnectionString=<host:port,password=...,ssl=True,abortConnect=False>
Caching__Redis__InstanceName=zadana-prod
Caching__Redis__RequireInProduction=true
```

Redis automatically backs the distributed application cache, output cache,
and SignalR backplane. Never commit its connection string.

## Rate limits

```text
RateLimiter__GlobalPermitsPerSecond=200
RateLimiter__PublicReadPermitsPerSecond=250
```

Public cacheable reads use a separate ceiling. Raise these limits only after
Redis or another shared cache is active and a staged load test passes.

Do not use the rate-limit number as the server capacity number. Anonymous load
tests from one machine share one IP bucket, so a single load generator will be
throttled before it accurately represents thousands of authenticated users.

## Runtime

The API explicitly uses server and concurrent GC. System audit logs are queued
off the request path and flushed in batches of up to 100 entries or after one
second, whichever comes first. This reduces SQL write round-trips without
changing request behavior.

## Client behavior

- SignalR is the primary live-update path.
- Order polling is a 30-second fallback while tracking and 60 seconds otherwise.
- Vendor notification polling stops while SignalR is connected and falls back
  to 60 seconds while disconnected.
- Dashboard polling is 60 seconds.
- GET requests retry transient `0/429/502/503/504` responses twice with
  exponential delay. Writes are never retried automatically.

## Verification

```bash
curl -sS -D - -o /dev/null https://api.zadna0.com/api/home
```

Expected origin headers:

```text
Cache-Control: public, max-age=30, s-maxage=120, ...
X-Zadana-Edge-Cache: eligible
```

After Cloudflare caches the response:

```text
CF-Cache-Status: HIT
Age: <seconds>
```
