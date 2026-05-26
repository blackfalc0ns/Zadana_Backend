/**
 * EXTREME STRESS TEST — Zadana API
 * ==================================================================
 * Hits every hot path simultaneously with worst-case patterns:
 *   - Customer browse  (cache-busting query params)
 *   - Customer order detail / tracking
 *   - Driver location pings (write-heavy)
 *   - Admin orders dashboard (KPI + filters)
 *   - Vendor orders panel (search with random terms)
 *   - Webhook bursts (Moyasar callback simulation)
 *   - Order status changes (write + SignalR fanout)
 *
 * Goal: find the breaking point — NOT validate steady-state.
 * Expect: 5xx rate climbing past stage 4. That's the answer
 *         we're looking for. The thresholds at the bottom flag
 *         "still healthy" so the test fails CI when capacity drops.
 *
 * Run:
 *   k6 run -e BASE_URL=https://staging.zadana.com `
 *          -e ADMIN_TOKEN=eyJ... `
 *          -e CUSTOMER_TOKEN=eyJ... `
 *          -e DRIVER_TOKEN=eyJ... `
 *          -e VENDOR_TOKEN=eyJ... `
 *          -e WEBHOOK_SECRET=changeme `
 *          scripts/load-tests/stress-extreme.js
 *
 * Recommended: run from a machine OUTSIDE the API host, with at
 * least 4 vCPU available for k6 itself, otherwise k6 becomes the
 * bottleneck before the API does.
 */

import http from 'k6/http';
import ws from 'k6/ws';
import { check, group, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { randomString, randomIntBetween, uuidv4 } from
  'https://jslib.k6.io/k6-utils/1.4.0/index.js';

// ---------- Config ----------
const BASE_URL        = __ENV.BASE_URL        || 'https://localhost:5001';
const ADMIN_TOKEN     = __ENV.ADMIN_TOKEN     || '';
const CUSTOMER_TOKEN  = __ENV.CUSTOMER_TOKEN  || '';
const DRIVER_TOKEN    = __ENV.DRIVER_TOKEN    || '';
const VENDOR_TOKEN    = __ENV.VENDOR_TOKEN    || '';
const WEBHOOK_SECRET  = __ENV.WEBHOOK_SECRET  || '';
const SAMPLE_ORDER_ID = __ENV.SAMPLE_ORDER_ID || ''; // for tracking calls

// ---------- Custom metrics (per scenario) ----------
const customer5xx   = new Rate('customer_5xx');
const driver5xx     = new Rate('driver_5xx');
const admin5xx      = new Rate('admin_5xx');
const vendor5xx     = new Rate('vendor_5xx');
const webhook5xx    = new Rate('webhook_5xx');

const customerLatency = new Trend('customer_latency_ms', true);
const driverLatency   = new Trend('driver_latency_ms', true);
const adminLatency    = new Trend('admin_latency_ms', true);
const vendorLatency   = new Trend('vendor_latency_ms', true);
const webhookLatency  = new Trend('webhook_latency_ms', true);

const wsConnects    = new Counter('ws_connections_opened');
const wsErrors      = new Counter('ws_connection_errors');

// ---------- Test profile ----------
// Six scenarios run in parallel. Each ramps independently to its
// peak so we observe how the API behaves when *every* hot path is
// hammered simultaneously, not just one in isolation.
export const options = {
  // Resilient HTTP defaults — we want to count timeouts as failures,
  // not artificially trim them by the client.
  insecureSkipTLSVerify: true,
  noConnectionReuse: false,
  userAgent: 'k6-zadana-stress/1.0',
  discardResponseBodies: true, // saves k6 memory; we only need status + timing

  scenarios: {
    customer_browse: {
      executor: 'ramping-arrival-rate',
      exec: 'customerBrowse',
      startRate: 50,
      timeUnit: '1s',
      preAllocatedVUs: 200,
      maxVUs: 800,
      stages: [
        { duration: '30s', target: 200 },   // warm-up
        { duration: '1m',  target: 800 },   // ramp
        { duration: '2m',  target: 2000 },  // SUSTAIN: 2000 RPS browse alone
        { duration: '2m',  target: 4000 },  // BREAK: 4000 RPS browse
        { duration: '30s', target: 0 },     // cooldown
      ],
    },

    customer_order_tracking: {
      executor: 'ramping-arrival-rate',
      exec: 'customerOrderFlow',
      startRate: 20,
      timeUnit: '1s',
      preAllocatedVUs: 100,
      maxVUs: 400,
      stages: [
        { duration: '30s', target: 100 },
        { duration: '1m',  target: 400 },
        { duration: '2m',  target: 1000 },
        { duration: '2m',  target: 2000 },
        { duration: '30s', target: 0 },
      ],
    },

    driver_location_pings: {
      executor: 'ramping-arrival-rate',
      exec: 'driverLocationPing',
      startRate: 50,
      timeUnit: '1s',
      preAllocatedVUs: 300,
      maxVUs: 1200,
      stages: [
        { duration: '30s', target: 300 },
        { duration: '1m',  target: 1200 },  // 500 drivers × 2.4 pings/sec
        { duration: '2m',  target: 3000 },  // SUSTAIN write storm
        { duration: '2m',  target: 6000 },  // BREAK: write tsunami
        { duration: '30s', target: 0 },
      ],
    },

    admin_dashboard: {
      executor: 'ramping-arrival-rate',
      exec: 'adminDashboard',
      startRate: 5,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 200,
      stages: [
        { duration: '30s', target: 20 },
        { duration: '1m',  target: 100 },
        { duration: '2m',  target: 300 },
        { duration: '2m',  target: 600 },   // hammers KPI cache + admin queries
        { duration: '30s', target: 0 },
      ],
    },

    vendor_panel: {
      executor: 'ramping-arrival-rate',
      exec: 'vendorPanel',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 80,
      maxVUs: 300,
      stages: [
        { duration: '30s', target: 50 },
        { duration: '1m',  target: 200 },
        { duration: '2m',  target: 500 },
        { duration: '2m',  target: 1000 },
        { duration: '30s', target: 0 },
      ],
    },

    webhook_burst: {
      executor: 'ramping-arrival-rate',
      exec: 'webhookBurst',
      startRate: 20,
      timeUnit: '1s',
      preAllocatedVUs: 100,
      maxVUs: 400,
      stages: [
        { duration: '30s', target: 50 },
        { duration: '30s', target: 200 },
        { duration: '1m',  target: 800 },   // payment provider replay storm
        { duration: '30s', target: 0 },
      ],
    },

    // Optional WebSocket pressure: opens 1500 concurrent SignalR
    // connections to NotificationHub and OrderTrackingHub. Keeps
    // them open for the whole test to exercise the Redis backplane
    // and SignalR memory.
    signalr_connections: {
      executor: 'ramping-vus',
      exec: 'signalrIdle',
      startVUs: 0,
      gracefulStop: '20s',
      stages: [
        { duration: '30s', target: 200 },
        { duration: '1m',  target: 800 },
        { duration: '2m',  target: 1500 },
        { duration: '2m',  target: 1500 },
        { duration: '30s', target: 0 },
      ],
    },
  },

  // Thresholds = pass/fail criteria. If any of these breach, the
  // exit code is non-zero and CI fails the build.
  thresholds: {
    // Global error rate stays under 5% even under stress
    'http_req_failed': ['rate<0.05'],

    // Per-scenario p95 budgets (relaxed for stress, tightened for
    // steady-state in a separate steady.js file)
    'customer_latency_ms{stage:steady}': ['p(95)<400'],
    'driver_latency_ms{stage:steady}':   ['p(95)<300'],
    'admin_latency_ms{stage:steady}':    ['p(95)<800'],
    'vendor_latency_ms{stage:steady}':   ['p(95)<500'],
    'webhook_latency_ms{stage:steady}':  ['p(95)<400'],

    // 5xx specifically (separate from 4xx-driven failures)
    'customer_5xx': ['rate<0.02'],
    'driver_5xx':   ['rate<0.02'],
    'admin_5xx':    ['rate<0.02'],
    'vendor_5xx':   ['rate<0.02'],
    'webhook_5xx':  ['rate<0.02'],
  },
};

// ---------- Helpers ----------
function authHeaders(token) {
  return {
    headers: {
      'Authorization': 'Bearer ' + token,
      'Accept': 'application/json',
      'Accept-Language': 'ar',
      'Accept-Encoding': 'gzip, br',
    },
    timeout: '30s',
  };
}

function recordResponse(res, latencyMetric, errorMetric) {
  latencyMetric.add(res.timings.duration);
  errorMetric.add(res.status >= 500);
}

// ==================================================================
//                       SCENARIO IMPLEMENTATIONS
// ==================================================================

/**
 * Customer browse — cache-busting on purpose so we hit DB every time.
 * Real users would share OutputCache hits, but a stress test should
 * exercise the worst case where every request misses cache.
 */
export function customerBrowse() {
  // Random params force unique OutputCache keys
  const page = randomIntBetween(1, 50);
  const minPrice = randomIntBetween(0, 200);
  const maxPrice = minPrice + randomIntBetween(50, 1000);
  const q = randomString(randomIntBetween(2, 6));

  group('customer:search', () => {
    const res = http.get(
      `${BASE_URL}/api/products/search?query=${q}&min_price=${minPrice}&max_price=${maxPrice}&page=${page}&per_page=20`,
      authHeaders(CUSTOMER_TOKEN));
    check(res, { 'browse 2xx/3xx': r => r.status < 400 });
    recordResponse(res, customerLatency, customer5xx);
  });

  // Burst-style: each VU iteration also hits home content + brands
  group('customer:home', () => {
    const r1 = http.get(`${BASE_URL}/api/home`, authHeaders(CUSTOMER_TOKEN));
    recordResponse(r1, customerLatency, customer5xx);

    const r2 = http.get(`${BASE_URL}/api/brands`, authHeaders(CUSTOMER_TOKEN));
    recordResponse(r2, customerLatency, customer5xx);
  });
}

/**
 * Customer order flow — heavy reads (detail + tracking) with the
 * AsSplitQuery includes that exercise the most expensive code path.
 */
export function customerOrderFlow() {
  if (!SAMPLE_ORDER_ID) {
    // Without a known order id we still hit the list endpoint
    const res = http.get(`${BASE_URL}/api/customer/orders?bucket=Active&page=1&per_page=20`,
      authHeaders(CUSTOMER_TOKEN));
    recordResponse(res, customerLatency, customer5xx);
    return;
  }

  group('customer:order:detail', () => {
    const res = http.get(`${BASE_URL}/api/customer/orders/${SAMPLE_ORDER_ID}`,
      authHeaders(CUSTOMER_TOKEN));
    recordResponse(res, customerLatency, customer5xx);
  });

  group('customer:order:tracking', () => {
    const res = http.get(`${BASE_URL}/api/customer/orders/${SAMPLE_ORDER_ID}/tracking`,
      authHeaders(CUSTOMER_TOKEN));
    recordResponse(res, customerLatency, customer5xx);
  });
}

/**
 * Driver location storm — writes are the most painful path because:
 *   - INSERT into DriverLocations
 *   - UPSERT into DriverLatestLocations
 *   - SignalR fanout to all subscribed customers/admins for active orders
 *
 * We push way past the realistic 1-ping-per-10s/driver to find the wall.
 */
export function driverLocationPing() {
  // Random plausible Riyadh coordinates
  const lat = 24.6 + Math.random() * 0.4;
  const lng = 46.5 + Math.random() * 0.5;
  const acc = randomIntBetween(5, 30);

  const res = http.post(
    `${BASE_URL}/api/driver/location`,
    JSON.stringify({ latitude: lat, longitude: lng, accuracyMeters: acc }),
    {
      headers: {
        'Authorization': 'Bearer ' + DRIVER_TOKEN,
        'Content-Type': 'application/json',
        'Accept-Language': 'ar',
      },
      timeout: '15s',
    });

  check(res, { 'driver ping 2xx': r => r.status >= 200 && r.status < 300 });
  recordResponse(res, driverLatency, driver5xx);
}

/**
 * Admin dashboard — the heaviest aggregate query in the whole API.
 * We rotate filters / queue views / search terms so the KPI cache
 * stays warm for the unfiltered view but the per-filter list still
 * runs against SQL.
 */
export function adminDashboard() {
  const queueViews = ['ALL', 'ACTIVE', 'LATE', 'PAYMENT_ISSUES', 'REFUNDS'];
  const queue = queueViews[randomIntBetween(0, queueViews.length - 1)];
  const page = randomIntBetween(1, 10);
  const search = Math.random() < 0.3 ? `&search=${randomString(4)}` : '';

  group('admin:orders', () => {
    const res = http.get(
      `${BASE_URL}/api/admin/orders?queueView=${queue}&page=${page}&pageSize=20${search}`,
      authHeaders(ADMIN_TOKEN));
    recordResponse(res, adminLatency, admin5xx);
  });

  group('admin:dashboard', () => {
    const res = http.get(`${BASE_URL}/api/admin/dashboard/overview?period=today`,
      authHeaders(ADMIN_TOKEN));
    recordResponse(res, adminLatency, admin5xx);
  });
}

/**
 * Vendor panel — random search terms exercise the LIKE patterns
 * we replaced ToLower().Contains() with. Index seeks must hold.
 */
export function vendorPanel() {
  const term = randomString(randomIntBetween(2, 5));
  const page = randomIntBetween(1, 5);

  const res = http.get(
    `${BASE_URL}/api/vendor/orders?search=${term}&page=${page}&pageSize=20`,
    authHeaders(VENDOR_TOKEN));
  recordResponse(res, vendorLatency, vendor5xx);
}

/**
 * Webhook burst — simulates a Moyasar settlement replay where
 * thousands of payment events hit the inbox in seconds. Each one
 * must dedupe by (provider, eventId) and enqueue.
 */
export function webhookBurst() {
  const eventId = `evt_${uuidv4()}`;
  const paymentId = `pay_${uuidv4()}`;
  const body = {
    id: eventId,
    type: 'payment_paid',
    data: {
      object: {
        id: paymentId,
        amount: 10000,
        status: 'paid',
        currency: 'SAR',
      },
    },
  };

  const res = http.post(
    `${BASE_URL}/api/payments/moyasar/webhook`,
    JSON.stringify(body),
    {
      headers: {
        'Content-Type': 'application/json',
        'X-Moyasar-Signature': WEBHOOK_SECRET,
      },
      timeout: '15s',
    });
  recordResponse(res, webhookLatency, webhook5xx);
}

/**
 * SignalR idle connections — opens a connection and keeps it alive
 * with periodic pings. Tests Redis backplane and per-connection RAM
 * cost without flooding the hub with messages.
 */
export function signalrIdle() {
  const url = `${BASE_URL.replace(/^http/, 'ws')}/hubs/notifications?access_token=${CUSTOMER_TOKEN}`;
  const params = { tags: { hub: 'notifications' } };

  const res = ws.connect(url, params, function (socket) {
    wsConnects.add(1);

    socket.on('open', () => {
      // SignalR handshake (JSON protocol)
      socket.send('{"protocol":"json","version":1}\u001E');
    });

    socket.on('error', () => wsErrors.add(1));

    // Hold the connection for ~3 minutes, send a periodic ping
    socket.setInterval(() => {
      socket.send('{"type":6}\u001E'); // SignalR ping
    }, 15000);

    socket.setTimeout(() => socket.close(), 180000);
  });

  check(res, { 'ws upgraded': (r) => r && r.status === 101 });
}

// ---------- Lifecycle ----------
export function setup() {
  console.log('=== Zadana Stress Test ===');
  console.log(`Target: ${BASE_URL}`);
  console.log('Profile: ramp to break point across all hot paths.');
  console.log('Watch for the moment 5xx > 1% — that is your ceiling.');
  return {};
}

export function teardown(_data) {
  console.log('=== Done ===');
}
