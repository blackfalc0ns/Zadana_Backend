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

const errorRate = new Rate('errors');
const latency = new Trend('latency_ms', true);

export const options = {
  insecureSkipTLSVerify: true,
  discardResponseBodies: true,

  scenarios: {
    public_browse: {
      executor: 'ramping-arrival-rate',
      exec: 'browse',
      startRate: 5,
      timeUnit: '1s',
      preAllocatedVUs: 20,
      maxVUs: 100,
      stages: [
        { duration: '15s', target: 20 },   // warmup
        { duration: '30s', target: 100 },  // ramp
        { duration: '30s', target: 200 },  // sustain
        { duration: '15s', target: 0 },    // cooldown
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
