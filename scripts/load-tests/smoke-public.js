/**
 * SMOKE TEST — Public endpoints only
 * ==================================================================
 * No-auth probe of the public endpoints (Home, Brands, Categories,
 * Geography). Used to validate that the load test pipeline is wired
 * correctly and the API responds at all under modest load.
 *
 * Run:
 *   k6 run -e BASE_URL=http://localhost:5000 scripts/load-tests/smoke-public.js
 */

import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import { randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const START_RATE = numberEnv('START_RATE', 5);
const WARM_RATE = numberEnv('WARM_RATE', 20);
const RAMP_RATE = numberEnv('RAMP_RATE', 100);
const SUSTAIN_RATE = numberEnv('SUSTAIN_RATE', 200);
const PRE_ALLOCATED_VUS = numberEnv('PRE_ALLOCATED_VUS', 20);
const MAX_VUS = numberEnv('MAX_VUS', 100);
const WARMUP_DURATION = __ENV.WARMUP_DURATION || '15s';
const RAMP_DURATION = __ENV.RAMP_DURATION || '30s';
const SUSTAIN_DURATION = __ENV.SUSTAIN_DURATION || '30s';
const COOLDOWN_DURATION = __ENV.COOLDOWN_DURATION || '15s';

const errorRate = new Rate('errors');
const latency = new Trend('latency_ms', true);

export const options = {
  insecureSkipTLSVerify: true,
  discardResponseBodies: true,

  scenarios: {
    public_browse: {
      executor: 'ramping-arrival-rate',
      exec: 'browse',
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
    'http_req_failed': ['rate<0.05'],
    'latency_ms': ['p(95)<500', 'p(99)<1500'],
    'errors': ['rate<0.05'],
  },
};

const PUBLIC_PATHS = [
  '/api/home',
  '/api/home/banners',
  '/api/home/categories',
  '/api/home/brands',
  '/api/brands',
  '/api/categories/subcategories',
  '/api/geography/regions',
];

const headers = {
  'Accept': 'application/json',
  'Accept-Language': 'ar',
  'Accept-Encoding': 'gzip, br',
};

export function browse() {
  const path = PUBLIC_PATHS[randomIntBetween(0, PUBLIC_PATHS.length - 1)];
  const res = http.get(`${BASE_URL}${path}`, { headers, timeout: '15s' });
  check(res, {
    '2xx/304': r => r.status < 400 || r.status === 304,
  });
  errorRate.add(res.status >= 400);
  latency.add(res.timings.duration);
}

function numberEnv(name, fallback) {
  const raw = __ENV[name];
  if (raw === undefined || raw === null || raw === '') {
    return fallback;
  }

  const value = Number(raw);
  return Number.isFinite(value) && value >= 0 ? value : fallback;
}
