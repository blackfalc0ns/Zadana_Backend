/**
 * STEADY-STATE LOAD TEST — Zadana realistic peak hour
 * ==================================================================
 * Models the workload you described:
 *   - 1000 customers (≈300 active concurrently)
 *   - 500 drivers   (≈300 actively pinging location)
 *   - 1000 active orders
 *   - 200 vendors   (≈50 watching their panel)
 *   - 10 admins
 *
 * Unlike stress-extreme.js, this one runs at realistic rates for 10
 * minutes to validate the *steady* p50/p95/p99 budget. Use it for
 * the "is the API ready to launch?" decision.
 *
 * Run:
 *   k6 run -e BASE_URL=https://staging.zadana.com `
 *          -e CUSTOMER_TOKEN=... -e DRIVER_TOKEN=... `
 *          -e ADMIN_TOKEN=... -e VENDOR_TOKEN=... `
 *          scripts/load-tests/steady-realistic.js
 */

import http from 'k6/http';
import { check, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import { randomString, randomIntBetween, uuidv4 } from
  'https://jslib.k6.io/k6-utils/1.4.0/index.js';

const BASE_URL       = __ENV.BASE_URL       || 'https://localhost:5001';
const ADMIN_TOKEN    = __ENV.ADMIN_TOKEN    || '';
const CUSTOMER_TOKEN = __ENV.CUSTOMER_TOKEN || '';
const DRIVER_TOKEN   = __ENV.DRIVER_TOKEN   || '';
const VENDOR_TOKEN   = __ENV.VENDOR_TOKEN   || '';

const errorRate = new Rate('errors');
const slowReqs  = new Rate('slow_requests');
const browseLat = new Trend('browse_ms', true);
const trackLat  = new Trend('track_ms', true);
const driverLat = new Trend('driver_ping_ms', true);
const adminLat  = new Trend('admin_ms', true);
const vendorLat = new Trend('vendor_ms', true);

export const options = {
  insecureSkipTLSVerify: true,
  discardResponseBodies: true,

  scenarios: {
    customer_browse: {
      executor: 'constant-arrival-rate',
      exec: 'customerBrowse',
      rate: 10,           // ~10 req/s — 300 active × 1 nav per 30s
      timeUnit: '1s',
      duration: '10m',
      preAllocatedVUs: 30,
      maxVUs: 80,
    },

    customer_tracking: {
      executor: 'constant-arrival-rate',
      exec: 'customerOrderFlow',
      rate: 5,            // 5 req/s — order detail/tracking
      timeUnit: '1s',
      duration: '10m',
      preAllocatedVUs: 20,
      maxVUs: 50,
    },

    driver_pings: {
      executor: 'constant-arrival-rate',
      exec: 'driverPing',
      rate: 30,           // 300 drivers × 1 ping per 10s
      timeUnit: '1s',
      duration: '10m',
      preAllocatedVUs: 50,
      maxVUs: 150,
    },

    admin_dashboard: {
      executor: 'constant-arrival-rate',
      exec: 'adminDashboard',
      rate: 1,            // 10 admins × 1 refresh per 10s
      timeUnit: '1s',
      duration: '10m',
      preAllocatedVUs: 5,
      maxVUs: 15,
    },

    vendor_panel: {
      executor: 'constant-arrival-rate',
      exec: 'vendorPanel',
      rate: 2,            // 50 vendors × 1 refresh per 30s
      timeUnit: '1s',
      duration: '10m',
      preAllocatedVUs: 10,
      maxVUs: 30,
    },
  },

  // Tighter budgets — this is the production SLO check.
  thresholds: {
    'http_req_failed': ['rate<0.005'],   // < 0.5%
    'browse_ms':       ['p(95)<150', 'p(99)<300'],
    'track_ms':        ['p(95)<200', 'p(99)<400'],
    'driver_ping_ms':  ['p(95)<100', 'p(99)<200'],
    'admin_ms':        ['p(95)<300', 'p(99)<600'],
    'vendor_ms':       ['p(95)<200', 'p(99)<400'],
    'errors':          ['rate<0.005'],
  },
};

function authHeaders(token) {
  return {
    headers: {
      'Authorization': 'Bearer ' + token,
      'Accept': 'application/json',
      'Accept-Language': 'ar',
      'Accept-Encoding': 'gzip, br',
    },
    timeout: '15s',
  };
}

export function customerBrowse() {
  const page = randomIntBetween(1, 5);
  const res = http.get(
    `${BASE_URL}/api/products/search?page=${page}&per_page=20`,
    authHeaders(CUSTOMER_TOKEN));
  errorRate.add(res.status >= 400);
  slowReqs.add(res.timings.duration > 500);
  browseLat.add(res.timings.duration);
}

export function customerOrderFlow() {
  const res = http.get(`${BASE_URL}/api/customer/orders?bucket=Active&page=1&per_page=20`,
    authHeaders(CUSTOMER_TOKEN));
  errorRate.add(res.status >= 400);
  trackLat.add(res.timings.duration);
}

export function driverPing() {
  const lat = 24.6 + Math.random() * 0.4;
  const lng = 46.5 + Math.random() * 0.5;
  const res = http.post(
    `${BASE_URL}/api/driver/location`,
    JSON.stringify({ latitude: lat, longitude: lng, accuracyMeters: 10 }),
    {
      headers: {
        'Authorization': 'Bearer ' + DRIVER_TOKEN,
        'Content-Type': 'application/json',
      },
      timeout: '10s',
    });
  errorRate.add(res.status >= 400);
  driverLat.add(res.timings.duration);
}

export function adminDashboard() {
  const res = http.get(`${BASE_URL}/api/admin/orders?page=1&pageSize=20`,
    authHeaders(ADMIN_TOKEN));
  errorRate.add(res.status >= 400);
  adminLat.add(res.timings.duration);
}

export function vendorPanel() {
  const res = http.get(`${BASE_URL}/api/vendor/orders?page=1&pageSize=20`,
    authHeaders(VENDOR_TOKEN));
  errorRate.add(res.status >= 400);
  vendorLat.add(res.timings.duration);
}
