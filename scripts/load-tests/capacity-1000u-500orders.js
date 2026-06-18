/**
 * CAPACITY TARGET TEST - 1000 customers / 500 drivers / 30 stores /
 *                        20 admins / 500 active orders
 * ===================================================================
 * Models the requested launch-capacity shape:
 *   - 1000 active customer sessions
 *   - 500 active order tracking sessions
 *   - 30 active vendor stores
 *   - 20 admin/reviewer users
 *   - 500 active drivers
 *
 * This test is intentionally safe by default: it reads hot paths and
 * writes only driver GPS pings. It does not mutate order statuses.
 *
 * Run:
 *   k6 run -e BASE_URL=https://staging.example.com `
 *          -e CUSTOMER_TOKENS="token1,token2" `
 *          -e DRIVER_TOKENS="token1,token2" `
 *          -e VENDOR_TOKENS="token1,token2" `
 *          -e REVIEWER_TOKENS="token1,token2" `
 *          -e ORDER_IDS="guid1,guid2,..." `
 *          -e VENDOR_IDS="guid1,guid2,..." `
 *          -e ADDRESS_IDS="guid1,guid2,..." `
 *          scripts/load-tests/capacity-1000u-500orders.js
 */

import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5000').replace(/\/+$/, '');
const REQUIRE_AUTH = (__ENV.REQUIRE_AUTH || 'true').toLowerCase() !== 'false';
const PROFILE = (__ENV.PROFILE || 'realistic').toLowerCase();

const CUSTOMER_VUS = numberEnv('CUSTOMER_VUS', 1000);
const ORDER_VUS = numberEnv('ORDER_VUS', 500);
const VENDOR_VUS = numberEnv('VENDOR_VUS', 30);
const REVIEWER_VUS = numberEnv('REVIEWER_VUS', 20);
const DRIVER_VUS = numberEnv('DRIVER_VUS', 500);

const RAMP_UP = __ENV.RAMP_UP || '2m';
const STEADY = __ENV.STEADY || '15m';
const RAMP_DOWN = __ENV.RAMP_DOWN || '1m';
const REALISTIC_DURATION = __ENV.REALISTIC_DURATION || '15m';

const CUSTOMER_TOKENS = listEnv('CUSTOMER_TOKENS', __ENV.CUSTOMER_TOKEN);
const DRIVER_TOKENS = listEnv('DRIVER_TOKENS', __ENV.DRIVER_TOKEN);
const VENDOR_TOKENS = listEnv('VENDOR_TOKENS', __ENV.VENDOR_TOKEN);
const REVIEWER_TOKENS = listEnv('REVIEWER_TOKENS', __ENV.REVIEWER_TOKEN || __ENV.ADMIN_TOKEN);

const ORDER_IDS = listEnv('ORDER_IDS', __ENV.SAMPLE_ORDER_ID);
const VENDOR_IDS = listEnv('VENDOR_IDS', __ENV.VENDOR_ID);
const ADDRESS_IDS = listEnv('ADDRESS_IDS', __ENV.ADDRESS_ID);

const capacityErrors = new Rate('capacity_errors');
const authErrors = new Rate('auth_errors');
const serverErrors = new Rate('server_errors');

const customerMs = new Trend('customer_ms', true);
const orderMs = new Trend('active_order_ms', true);
const driverMs = new Trend('driver_ms', true);
const vendorMs = new Trend('vendor_ms', true);
const reviewerMs = new Trend('reviewer_ms', true);

const driverPings = new Counter('driver_location_pings');
const orderPolls = new Counter('active_order_polls');

export const options = {
  insecureSkipTLSVerify: true,
  discardResponseBodies: true,
  userAgent: 'k6-zadana-capacity-target/1.0',

  scenarios: buildScenarios(),

  thresholds: {
    http_req_failed: ['rate<0.02'],
    capacity_errors: ['rate<0.02'],
    auth_errors: ['rate<0.001'],
    server_errors: ['rate<0.005'],
    customer_ms: ['p(95)<700', 'p(99)<1500'],
    active_order_ms: ['p(95)<600', 'p(99)<1500'],
    driver_ms: ['p(95)<250', 'p(99)<700'],
    vendor_ms: ['p(95)<700', 'p(99)<1500'],
    reviewer_ms: ['p(95)<900', 'p(99)<2000'],
  },
};

function buildScenarios() {
  if (PROFILE === 'concurrent') {
    return buildConcurrentScenarios();
  }

  return {
    customers_1000_realistic: arrivalScenario(
      'activeCustomer',
      numberEnv('CUSTOMER_RATE', 34),
      numberEnv('CUSTOMER_PRE_VUS', 30),
      numberEnv('CUSTOMER_MAX_VUS', 200),
    ),
    orders_500_realistic: arrivalScenario(
      'activeOrderWatcher',
      numberEnv('ORDER_RATE', 34),
      numberEnv('ORDER_PRE_VUS', 25),
      numberEnv('ORDER_MAX_VUS', 150),
    ),
    drivers_500_realistic: arrivalScenario(
      'activeDriver',
      numberEnv('DRIVER_RATE', 50),
      numberEnv('DRIVER_PRE_VUS', 35),
      numberEnv('DRIVER_MAX_VUS', 200),
    ),
    stores_30_realistic: arrivalScenario(
      'vendorStore',
      numberEnv('VENDOR_RATE', 3),
      numberEnv('VENDOR_PRE_VUS', 5),
      numberEnv('VENDOR_MAX_VUS', 30),
    ),
    admins_20_realistic: arrivalScenario(
      'reviewerDesk',
      numberEnv('REVIEWER_RATE', 1),
      numberEnv('REVIEWER_PRE_VUS', 3),
      numberEnv('REVIEWER_MAX_VUS', 20),
    ),
  };
}

function arrivalScenario(exec, rate, preAllocatedVUs, maxVUs) {
  return {
    executor: 'constant-arrival-rate',
    exec,
    rate,
    timeUnit: '1s',
    duration: REALISTIC_DURATION,
    preAllocatedVUs,
    maxVUs,
    gracefulStop: '30s',
  };
}

function buildConcurrentScenarios() {
  return {
    active_customers_1000: {
      executor: 'ramping-vus',
      exec: 'activeCustomer',
      startVUs: 0,
      gracefulRampDown: '30s',
      stages: [
        { duration: RAMP_UP, target: CUSTOMER_VUS },
        { duration: STEADY, target: CUSTOMER_VUS },
        { duration: RAMP_DOWN, target: 0 },
      ],
    },
    active_orders_500: {
      executor: 'ramping-vus',
      exec: 'activeOrderWatcher',
      startVUs: 0,
      gracefulRampDown: '30s',
      stages: [
        { duration: RAMP_UP, target: ORDER_VUS },
        { duration: STEADY, target: ORDER_VUS },
        { duration: RAMP_DOWN, target: 0 },
      ],
    },
    active_drivers_500: {
      executor: 'ramping-vus',
      exec: 'activeDriver',
      startVUs: 0,
      gracefulRampDown: '30s',
      stages: [
        { duration: RAMP_UP, target: DRIVER_VUS },
        { duration: STEADY, target: DRIVER_VUS },
        { duration: RAMP_DOWN, target: 0 },
      ],
    },
    vendor_stores_30: {
      executor: 'ramping-vus',
      exec: 'vendorStore',
      startVUs: 0,
      gracefulRampDown: '30s',
      stages: [
        { duration: RAMP_UP, target: VENDOR_VUS },
        { duration: STEADY, target: VENDOR_VUS },
        { duration: RAMP_DOWN, target: 0 },
      ],
    },
    reviewers_20: {
      executor: 'ramping-vus',
      exec: 'reviewerDesk',
      startVUs: 0,
      gracefulRampDown: '30s',
      stages: [
        { duration: RAMP_UP, target: REVIEWER_VUS },
        { duration: STEADY, target: REVIEWER_VUS },
        { duration: RAMP_DOWN, target: 0 },
      ],
    },
  };
}

export function setup() {
  if (PROFILE !== 'realistic' && PROFILE !== 'concurrent') {
    fail(`Unsupported PROFILE=${PROFILE}. Use realistic or concurrent.`);
  }

  console.log(`Capacity profile: ${PROFILE}`);

  if (!REQUIRE_AUTH) {
    return;
  }

  const missing = [];
  if (CUSTOMER_TOKENS.length === 0) missing.push('CUSTOMER_TOKENS');
  if (DRIVER_TOKENS.length === 0) missing.push('DRIVER_TOKENS');
  if (VENDOR_TOKENS.length === 0) missing.push('VENDOR_TOKENS');
  if (REVIEWER_TOKENS.length === 0) missing.push('REVIEWER_TOKENS or ADMIN_TOKEN');

  if (missing.length > 0) {
    fail(`Missing required auth env vars: ${missing.join(', ')}. Set REQUIRE_AUTH=false only for public smoke runs.`);
  }
}

export function activeCustomer() {
  const token = pick(CUSTOMER_TOKENS);
  const vendorId = pick(VENDOR_IDS);
  const addressId = pick(ADDRESS_IDS);
  const page = randomInt(1, 8);
  const q = randomTerm();

  request('GET', '/api/home', null, auth(token), customerMs);
  request('GET', `/api/products/search?query=${encodeURIComponent(q)}&page=${page}&per_page=20`, null, auth(token), customerMs);

  if (vendorId && addressId) {
    request('GET', `/api/cart/delivery-check?vendor_id=${vendorId}&address_id=${addressId}`, null, auth(token), customerMs);
    request('GET', `/api/checkout/summary?vendor_id=${vendorId}&address_id=${addressId}&payment_method=cash`, null, auth(token), customerMs);
  } else {
    request('GET', '/api/orders/active?page=1&per_page=10', null, auth(token), customerMs);
  }

  pace(8, 20);
}

export function activeOrderWatcher() {
  const token = pick(CUSTOMER_TOKENS);
  const orderId = pick(ORDER_IDS);

  if (orderId) {
    request('GET', `/api/orders/${orderId}`, null, auth(token), orderMs);
    request('GET', `/api/orders/${orderId}/tracking`, null, auth(token), orderMs);
    orderPolls.add(2);
  } else {
    request('GET', '/api/orders/active?page=1&per_page=20', null, auth(token), orderMs);
    orderPolls.add(1);
  }

  pace(10, 18);
}

export function activeDriver() {
  const token = pick(DRIVER_TOKENS);
  const location = movingLocation();

  request(
    'POST',
    '/api/drivers/location',
    JSON.stringify(location),
    auth(token, true),
    driverMs,
  );
  driverPings.add(1);

  if (__ITER % 6 === 0) {
    request('GET', '/api/drivers/assignments/current', null, auth(token), driverMs);
  }

  pace(8, 12);
}

export function vendorStore() {
  const token = pick(VENDOR_TOKENS);
  const status = pick(['', 'Placed', 'Accepted', 'Preparing', 'ReadyForPickup']);
  const statusQuery = status ? `&status=${status}` : '';

  request('GET', `/api/vendor/orders?page=1&pageSize=20${statusQuery}`, null, auth(token), vendorMs);
  request('GET', '/api/vendor/dashboard/overview', null, auth(token), vendorMs);

  if (__ITER % 4 === 0) {
    request('GET', '/api/vendor/products?page=1&pageSize=20', null, auth(token), vendorMs);
  }

  pace(8, 15);
}

export function reviewerDesk() {
  const token = pick(REVIEWER_TOKENS);
  const orderId = pick(ORDER_IDS);

  request('GET', '/api/admin/dashboard/overview', null, auth(token), reviewerMs);
  request('GET', '/api/admin/orders?queueView=ACTIVE&page=1&pageSize=20', null, auth(token), reviewerMs);
  request('GET', '/api/admin/product-requests/pending?page=1&pageSize=20', null, auth(token), reviewerMs);

  if (orderId && __ITER % 3 === 0) {
    request('GET', `/api/admin/orders/${orderId}`, null, auth(token), reviewerMs);
  }

  if (__ITER % 5 === 0) {
    request('GET', '/api/admin/order-cases/stats', null, auth(token), reviewerMs);
  }

  pace(12, 24);
}

function request(method, path, body, params, trend) {
  const res = method === 'POST'
    ? http.post(`${BASE_URL}${path}`, body, params)
    : http.get(`${BASE_URL}${path}`, params);

  record(res, trend);
  check(res, {
    [`${method} ${path} returned < 500`]: r => r.status < 500,
    [`${method} ${path} authorized/configured`]: r => r.status !== 401 && r.status !== 403,
  });

  return res;
}

function record(res, trend) {
  const isAuthError = res.status === 401 || res.status === 403;
  const isServerError = res.status >= 500;
  const isCapacityError = isServerError || res.error_code !== 0;

  trend.add(res.timings.duration);
  authErrors.add(isAuthError);
  serverErrors.add(isServerError);
  capacityErrors.add(isCapacityError || res.status >= 400);
}

function auth(token, json = false) {
  const headers = {
    Accept: 'application/json',
    'Accept-Language': 'ar',
    'Accept-Encoding': 'gzip, br',
  };

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  if (json) {
    headers['Content-Type'] = 'application/json';
  }

  return { headers, timeout: '20s' };
}

function movingLocation() {
  // Eastern Province-ish path. Keep values realistic but varied.
  return {
    latitude: 26.25 + Math.random() * 0.25,
    longitude: 50.05 + Math.random() * 0.25,
    accuracyMeters: randomInt(5, 30),
  };
}

function pace(min, max) {
  if (PROFILE === 'concurrent') {
    sleep(randomFloat(min, max));
  }
}

function randomTerm() {
  const terms = ['milk', 'oil', 'rice', 'tea', 'yogurt', 'water', 'bread', 'coffee', 'cheese'];
  return pick(terms);
}

function listEnv(name, fallback) {
  const raw = __ENV[name] || fallback || '';
  return raw
    .split(/[\s,;|]+/)
    .map(x => x.trim())
    .filter(Boolean);
}

function numberEnv(name, fallback) {
  const value = Number(__ENV[name] || fallback);
  return Number.isFinite(value) && value >= 0 ? value : fallback;
}

function pick(items) {
  if (!items || items.length === 0) {
    return '';
  }
  return items[(__VU + __ITER) % items.length];
}

function randomInt(min, max) {
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

function randomFloat(min, max) {
  return Math.random() * (max - min) + min;
}
