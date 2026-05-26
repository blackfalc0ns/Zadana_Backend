/**
 * SPIKE TEST — Write Storm
 * ==================================================================
 * Targets the most painful single endpoint: driver location updates.
 * Simulates a city-wide event (national holiday, Friday lunch rush)
 * where every active driver pings location simultaneously.
 *
 * Goal: discover the maximum sustainable write rate before:
 *   - SQL Server connection pool exhausts
 *   - SignalR backplane backs up
 *   - p99 latency crosses 1 second
 *
 * Run:
 *   k6 run -e BASE_URL=https://staging.zadana.com `
 *          -e DRIVER_TOKEN=eyJ... `
 *          scripts/load-tests/spike-write-storm.js
 */

import http from 'k6/http';
import { Rate, Trend } from 'k6/metrics';

const BASE_URL     = __ENV.BASE_URL     || 'https://localhost:5001';
const DRIVER_TOKEN = __ENV.DRIVER_TOKEN || '';

const writeErrors = new Rate('write_errors');
const writeP99    = new Trend('write_latency_ms', true);

export const options = {
  insecureSkipTLSVerify: true,
  discardResponseBodies: true,
  scenarios: {
    write_storm: {
      executor: 'ramping-arrival-rate',
      exec: 'pingStorm',
      startRate: 100,
      timeUnit: '1s',
      preAllocatedVUs: 200,
      maxVUs: 2000,
      stages: [
        { duration: '20s', target: 200 },     // baseline
        { duration: '30s', target: 1000 },    // realistic peak
        { duration: '1m',  target: 3000 },    // 6× normal — stress
        { duration: '1m',  target: 6000 },    // 12× normal — break
        { duration: '30s', target: 10000 },   // tsunami
        { duration: '20s', target: 0 },       // cooldown
      ],
    },
  },
  thresholds: {
    'write_errors':     ['rate<0.10'],     // tolerate 10% under tsunami
    'write_latency_ms': ['p(95)<500', 'p(99)<2000'],
    'http_req_failed':  ['rate<0.10'],
  },
};

export function pingStorm() {
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
  writeErrors.add(res.status >= 400);
  writeP99.add(res.timings.duration);
}
