/**
 * DRIVER API PRESSURE TEST — Zadana API
 * ==================================================================
 * Focuses on the hot mobile-driver read paths that are called when the
 * driver app opens or refreshes:
 *   - GET /api/drivers/home
 *   - GET /api/drivers/assignments/current
 *   - optional POST /api/drivers/location when ALLOW_WRITES=true
 *
 * Read-only by default. Do not enable writes against production unless you are
 * using a dedicated test driver account.
 *
 * Example:
 *   k6 run `
 *     -e BASE_URL=https://api.zadna0.com `
 *     -e DRIVER_TOKEN=$env:DRIVER_TOKEN `
 *     -e START_RATE=10 -e WARM_RATE=50 -e RAMP_RATE=150 -e SUSTAIN_RATE=300 `
 *     scripts/load-tests/driver-api-pressure.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

const BASE_URL = (__ENV.BASE_URL || 'http://127.0.0.1:5000').replace(/\/+$/, '');
const DRIVER_TOKEN = __ENV.DRIVER_TOKEN || '';
const START_RATE = numberEnv('START_RATE', 5);
const WARM_RATE = numberEnv('WARM_RATE', 25);
const RAMP_RATE = numberEnv('RAMP_RATE', 100);
const SUSTAIN_RATE = numberEnv('SUSTAIN_RATE', 200);
const PRE_ALLOCATED_VUS = numberEnv('PRE_ALLOCATED_VUS', 100);
const MAX_VUS = numberEnv('MAX_VUS', 600);
const WARMUP_DURATION = __ENV.WARMUP_DURATION || '20s';
const RAMP_DURATION = __ENV.RAMP_DURATION || '1m';
const SUSTAIN_DURATION = __ENV.SUSTAIN_DURATION || '2m';
const COOLDOWN_DURATION = __ENV.COOLDOWN_DURATION || '20s';
const ALLOW_WRITES = (__ENV.ALLOW_WRITES || 'false').toLowerCase() === 'true';

const driver5xx = new Rate('driver_5xx');
const driver4xx = new Rate('driver_4xx');
const driverHomeLatency = new Trend('driver_home_latency_ms', true);
const driverCurrentLatency = new Trend('driver_current_assignment_latency_ms', true);
const driverLocationLatency = new Trend('driver_location_latency_ms', true);
const unexpectedStatuses = new Counter('driver_unexpected_statuses');

export const options = {
  insecureSkipTLSVerify: true,
  discardResponseBodies: true,
  noConnectionReuse: false,
  userAgent: 'k6-zadana-driver-api-pressure/1.0',

  scenarios: {
    driver_reads: {
      executor: 'ramping-arrival-rate',
      exec: 'driverReadPressure',
      startRate: START_RATE,
      timeUnit: '1s',
      preAllocatedVUs: PRE_ALLOCATED_VUS,
      maxVUs: MAX_VUS,
      stages: [
        { duration: WARMUP_DURATION, target: WARM_RATE },
        { duration: RAMP_DURATION, target: RAMP_RATE },
        { duration: SUSTAIN_DURATION, target: SUSTAIN_RATE },
        { duration: COOLDOWN_DURATION, target: 0 },
      ],
    },
  },

  thresholds: {
    http_req_failed: ['rate<0.05'],
    driver_5xx: ['rate<0.01'],
    driver_home_latency_ms: ['p(95)<700', 'p(99)<1500'],
    driver_current_assignment_latency_ms: ['p(95)<700', 'p(99)<1500'],
  },
};

export function setup() {
  if (!DRIVER_TOKEN) {
    throw new Error('DRIVER_TOKEN is required for /api/drivers pressure testing.');
  }

  return {};
}

export function driverReadPressure() {
  const headers = {
    Authorization: `Bearer ${DRIVER_TOKEN}`,
    Accept: 'application/json',
    'Accept-Language': 'ar',
    'Accept-Encoding': 'gzip, br',
  };

  const home = http.get(`${BASE_URL}/api/drivers/home`, {
    headers,
    timeout: '20s',
    tags: { name: '/api/drivers/home' },
  });
  record(home, driverHomeLatency);
  check(home, {
    'home is 2xx': response => response.status >= 200 && response.status < 300,
  });

  const current = http.get(`${BASE_URL}/api/drivers/assignments/current`, {
    headers,
    timeout: '20s',
    tags: { name: '/api/drivers/assignments/current' },
  });
  record(current, driverCurrentLatency);
  check(current, {
    'current assignment is 2xx': response => response.status >= 200 && response.status < 300,
  });

  if (ALLOW_WRITES) {
    const location = http.post(
      `${BASE_URL}/api/drivers/location`,
      JSON.stringify(randomLocation()),
      {
        headers: {
          ...headers,
          'Content-Type': 'application/json',
        },
        timeout: '20s',
        tags: { name: '/api/drivers/location' },
      });

    record(location, driverLocationLatency);
    check(location, {
      'location is 2xx': response => response.status >= 200 && response.status < 300,
    });
  }

  sleep(0.05);
}

function record(response, latencyMetric) {
  latencyMetric.add(response.timings.duration);
  driver5xx.add(response.status >= 500);
  driver4xx.add(response.status >= 400 && response.status < 500);

  if (response.status < 200 || response.status >= 300) {
    unexpectedStatuses.add(1);
  }
}

function randomLocation() {
  const jitterLat = (__VU % 100) / 100000;
  const jitterLng = (__ITER % 100) / 100000;
  return {
    latitude: 26.4207 + jitterLat,
    longitude: 50.0888 + jitterLng,
    accuracyMeters: 8 + ((__VU + __ITER) % 12),
  };
}

function numberEnv(name, fallback) {
  const raw = __ENV[name];
  if (raw === undefined || raw === null || raw === '') {
    return fallback;
  }

  const value = Number(raw);
  return Number.isFinite(value) && value >= 0 ? value : fallback;
}
