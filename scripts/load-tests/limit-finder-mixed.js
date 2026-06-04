/**
 * MIXED LIMIT FINDER - Zadana API
 * ==================================================================
 * Finds the practical breaking point by ramping multiple workloads
 * together. It is safe by default:
 *   - Always runs public browse.
 *   - Protected workloads run only when their tokens/ids are supplied.
 *   - Driver GPS writes are disabled unless ALLOW_DRIVER_WRITES=true.
 *
 * Quick public-only run:
 *   k6 run -e BASE_URL=http://localhost:5298 scripts/load-tests/limit-finder-mixed.js
 *
 * Full staging run:
 *   k6 run -e BASE_URL=https://staging.example.com `
 *          -e CUSTOMER_TOKENS="token1,token2" `
 *          -e DRIVER_TOKENS="token1,token2" `
 *          -e VENDOR_TOKENS="token1,token2" `
 *          -e REVIEWER_TOKENS="token1,token2" `
 *          -e ORDER_IDS="guid1,guid2" `
 *          -e VENDOR_IDS="guid1,guid2" `
 *          -e ADDRESS_IDS="guid1,guid2" `
 *          -e ALLOW_DRIVER_WRITES=true `
 *          scripts/load-tests/limit-finder-mixed.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5298').replace(/\/+$/, '');
const SCALE = numberEnv('SCALE', 1);
const BUST_CACHE = boolEnv('BUST_CACHE', true);
const ALLOW_DRIVER_WRITES = boolEnv('ALLOW_DRIVER_WRITES', false);

const WARMUP = __ENV.WARMUP || '30s';
const RAMP_1 = __ENV.RAMP_1 || '45s';
const RAMP_2 = __ENV.RAMP_2 || '45s';
const RAMP_3 = __ENV.RAMP_3 || '45s';
const BREAK = __ENV.BREAK || '45s';
const COOLDOWN = __ENV.COOLDOWN || '20s';

const CUSTOMER_TOKENS = listEnv('CUSTOMER_TOKENS', __ENV.CUSTOMER_TOKEN);
const DRIVER_TOKENS = listEnv('DRIVER_TOKENS', __ENV.DRIVER_TOKEN);
const VENDOR_TOKENS = listEnv('VENDOR_TOKENS', __ENV.VENDOR_TOKEN);
const REVIEWER_TOKENS = listEnv('REVIEWER_TOKENS', __ENV.REVIEWER_TOKEN || __ENV.ADMIN_TOKEN);

const ORDER_IDS = listEnv('ORDER_IDS', __ENV.SAMPLE_ORDER_ID);
const VENDOR_IDS = listEnv('VENDOR_IDS', __ENV.VENDOR_ID);
const ADDRESS_IDS = listEnv('ADDRESS_IDS', __ENV.ADDRESS_ID);

const publicMs = new Trend('public_ms', true);
const customerMs = new Trend('customer_ms', true);
const orderMs = new Trend('order_tracking_ms', true);
const driverMs = new Trend('driver_ms', true);
const vendorMs = new Trend('vendor_ms', true);
const reviewerMs = new Trend('reviewer_ms', true);

const serverErrors = new Rate('server_errors');
const authErrors = new Rate('auth_errors');
const capacityErrors = new Rate('capacity_errors');
const requestsByRole = new Counter('requests_by_role');
const SCENARIOS = buildScenarios();

export const options = {
  insecureSkipTLSVerify: true,
  discardResponseBodies: true,
  noConnectionReuse: false,
  userAgent: 'k6-zadana-limit-finder/1.0',
  scenarios: SCENARIOS,
  thresholds: {
    http_req_failed: ['rate<0.10'],
    server_errors: ['rate<0.02'],
    auth_errors: ['rate<0.02'],
    capacity_errors: ['rate<0.10'],
    public_ms: ['p(95)<1000', 'p(99)<2500'],
    customer_ms: ['p(95)<1200', 'p(99)<3000'],
    order_tracking_ms: ['p(95)<1200', 'p(99)<3000'],
    driver_ms: ['p(95)<800', 'p(99)<2000'],
    vendor_ms: ['p(95)<1500', 'p(99)<3500'],
    reviewer_ms: ['p(95)<1800', 'p(99)<4000'],
  },
};

export function setup() {
  const enabled = Object.keys(SCENARIOS);
  console.log(`Limit finder target: ${BASE_URL}`);
  console.log(`Enabled scenarios: ${enabled.join(', ')}`);
  console.log(`Scale: ${SCALE}; cache busting: ${BUST_CACHE}; driver writes: ${ALLOW_DRIVER_WRITES}`);

  if (!CUSTOMER_TOKENS.length) console.warn('Customer scenarios disabled: CUSTOMER_TOKENS missing.');
  if (!DRIVER_TOKENS.length) console.warn('Driver scenarios disabled: DRIVER_TOKENS missing.');
  if (!VENDOR_TOKENS.length) console.warn('Vendor scenarios disabled: VENDOR_TOKENS missing.');
  if (!REVIEWER_TOKENS.length) console.warn('Reviewer scenarios disabled: REVIEWER_TOKENS missing.');
  if (!ORDER_IDS.length) console.warn('Order tracking/details reduced: ORDER_IDS missing.');

  return {
    customerTokens: CUSTOMER_TOKENS,
    driverTokens: DRIVER_TOKENS,
    vendorTokens: VENDOR_TOKENS,
    reviewerTokens: REVIEWER_TOKENS,
    orderIds: ORDER_IDS,
    vendorIds: VENDOR_IDS,
    addressIds: ADDRESS_IDS,
  };
}

export function publicBrowse() {
  const paths = [
    '/api/home',
    '/api/home/banners',
    '/api/home/categories',
    '/api/home/brands',
    '/api/brands',
    '/api/categories/subcategories',
    '/api/geography/regions',
    `/api/products/search?query=${randomSearch()}&page=${randomInt(1, 10)}&per_page=20`,
  ];

  request('public', 'GET', randomItem(paths), null, publicMs);
}

export function customerBrowse(data) {
  const token = randomItem(data.customerTokens);
  const paths = [
    '/api/home',
    `/api/products/search?query=${randomSearch()}&page=${randomInt(1, 8)}&per_page=20`,
    '/api/orders/active?page=1&per_page=20',
  ];

  if (data.addressIds.length) {
    const addressId = randomItem(data.addressIds);
    paths.push(`/api/cart/delivery-check?address_id=${addressId}`);
    paths.push(`/api/checkout/summary?address_id=${addressId}`);
  }

  request('customer', 'GET', randomItem(paths), token, customerMs);
}

export function orderTracking(data) {
  const token = randomItem(data.customerTokens);
  const orderId = randomItem(data.orderIds);
  const paths = [
    '/api/orders/active?page=1&per_page=20',
    `/api/orders/${orderId}`,
    `/api/orders/${orderId}/tracking`,
  ];

  request('orders', 'GET', randomItem(paths), token, orderMs);
}

export function driverOps(data) {
  const token = randomItem(data.driverTokens);
  if (ALLOW_DRIVER_WRITES && randomInt(1, 100) <= 70) {
    request(
      'driver',
      'POST',
      '/api/drivers/location',
      token,
      driverMs,
      JSON.stringify({
        latitude: 26.4207 + randomOffset(),
        longitude: 50.1033 + randomOffset(),
        accuracy_meters: randomInt(5, 25),
      }),
    );
    return;
  }

  request('driver', 'GET', '/api/drivers/assignments/current', token, driverMs);
}

export function vendorOps(data) {
  const token = randomItem(data.vendorTokens);
  const paths = [
    '/api/vendor/orders?page=1&pageSize=20&status=Accepted',
    '/api/vendor/dashboard/overview',
    `/api/vendor/products?page=1&pageSize=20&search=${randomSearch()}`,
  ];

  request('vendor', 'GET', randomItem(paths), token, vendorMs);
}

export function reviewerOps(data) {
  const token = randomItem(data.reviewerTokens);
  const paths = [
    '/api/admin/dashboard/overview',
    '/api/admin/orders?queueView=ACTIVE&page=1&pageSize=20',
    '/api/admin/product-requests/pending?page=1&pageSize=20',
    '/api/admin/order-cases/stats',
  ];

  request('reviewer', 'GET', randomItem(paths), token, reviewerMs);
}

function buildScenarios() {
  const scenarios = {
    public_browse_limit: scenario('publicBrowse', 'public', numberEnv('PUBLIC_MAX_RPS', 5000)),
  };

  if (CUSTOMER_TOKENS.length) {
    scenarios.customer_browse_limit = scenario('customerBrowse', 'customer', numberEnv('CUSTOMER_MAX_RPS', 1000));
  }

  if (CUSTOMER_TOKENS.length && ORDER_IDS.length) {
    scenarios.order_tracking_limit = scenario('orderTracking', 'orders', numberEnv('ORDERS_MAX_RPS', 800));
  }

  if (DRIVER_TOKENS.length) {
    scenarios.driver_limit = scenario('driverOps', 'driver', numberEnv('DRIVER_MAX_RPS', 800));
  }

  if (VENDOR_TOKENS.length) {
    scenarios.vendor_limit = scenario('vendorOps', 'vendor', numberEnv('VENDOR_MAX_RPS', 300));
  }

  if (REVIEWER_TOKENS.length) {
    scenarios.reviewer_limit = scenario('reviewerOps', 'reviewer', numberEnv('REVIEWER_MAX_RPS', 150));
  }

  return scenarios;
}

function scenario(exec, key, maxRps) {
  const target = Math.max(1, Math.round(maxRps * SCALE));
  const preVus = numberEnv(`${key.toUpperCase()}_PRE_VUS`, Math.max(50, Math.ceil(target / 8)));
  const maxVUs = numberEnv(`${key.toUpperCase()}_MAX_VUS`, Math.max(200, Math.ceil(target / 2)));

  return {
    executor: 'ramping-arrival-rate',
    exec,
    startRate: Math.max(1, Math.round(target * 0.05)),
    timeUnit: '1s',
    preAllocatedVUs: preVus,
    maxVUs,
    stages: [
      { duration: WARMUP, target: Math.round(target * 0.10) },
      { duration: RAMP_1, target: Math.round(target * 0.30) },
      { duration: RAMP_2, target: Math.round(target * 0.60) },
      { duration: RAMP_3, target: target },
      { duration: BREAK, target: Math.round(target * 1.25) },
      { duration: COOLDOWN, target: 0 },
    ],
  };
}

function request(role, method, path, token, trend, body = null) {
  const headers = {
    Accept: 'application/json',
    'Accept-Language': 'ar',
  };

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  if (body !== null) {
    headers['Content-Type'] = 'application/json';
  }

  const url = `${BASE_URL}${withCacheBust(path)}`;
  const params = {
    headers,
    timeout: '15s',
    tags: { name: path.split('?')[0], role },
  };

  const res = method === 'POST'
    ? http.post(url, body, params)
    : http.get(url, params);

  const isServerError = res.status >= 500 || res.error_code !== 0;
  const isAuthError = res.status === 401 || res.status === 403;
  const isCapacityError = res.status === 0 || res.status === 408 || res.status === 429 || res.status >= 500;

  serverErrors.add(isServerError, { role });
  authErrors.add(isAuthError, { role });
  capacityErrors.add(isCapacityError, { role });
  trend.add(res.timings.duration, { role });
  requestsByRole.add(1, { role });

  check(res, {
    [`${role}: no 5xx/timeout`]: r => r.status > 0 && r.status < 500,
    [`${role}: authorized if protected`]: r => !token || (r.status !== 401 && r.status !== 403),
  });

  if (res.status === 429) {
    sleep(0.2);
  }
}

function withCacheBust(path) {
  if (!BUST_CACHE) return path;
  const separator = path.includes('?') ? '&' : '?';
  return `${path}${separator}_cb=${__VU}-${__ITER}-${Date.now()}`;
}

function listEnv(name, fallback) {
  const raw = __ENV[name] || fallback || '';
  return raw
    .split(',')
    .map(x => x.trim())
    .filter(Boolean);
}

function numberEnv(name, fallback) {
  const raw = __ENV[name];
  if (raw === undefined || raw === null || raw === '') return fallback;
  const value = Number(raw);
  return Number.isFinite(value) && value >= 0 ? value : fallback;
}

function boolEnv(name, fallback) {
  const raw = __ENV[name];
  if (raw === undefined || raw === null || raw === '') return fallback;
  return ['1', 'true', 'yes', 'on'].includes(raw.toLowerCase());
}

function randomItem(items) {
  return items[randomInt(0, items.length - 1)];
}

function randomInt(min, max) {
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

function randomSearch() {
  return randomItem(['rice', 'water', 'milk', 'bread', 'oil', 'sugar', 'tea', 'coffee', 'yogurt', 'dates']);
}

function randomOffset() {
  return (Math.random() - 0.5) / 1000;
}
