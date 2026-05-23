// Connects as the same driver, subscribes to its order, and sends 5 location
// updates with progressively different coordinates. Confirms each one arrives
// back over SignalR and prints the broadcast latency.

import * as signalR from '@microsoft/signalr';

const API_BASE = 'http://localhost:5298/api';
const HUB_URL = 'http://localhost:5298/hubs/order-tracking';

const log = (...args) => console.log(`[${new Date().toISOString()}]`, ...args);

async function login(identifier, password) {
  const res = await fetch(`${API_BASE}/drivers/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ identifier, password })
  });
  if (!res.ok) throw new Error(`login failed: ${res.status} ${await res.text()}`);
  const data = await res.json();
  return data.tokens.accessToken;
}

async function getCurrentAssignment(token) {
  const res = await fetch(`${API_BASE}/drivers/assignments/current`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  if (!res.ok) throw new Error(`current assignment failed: ${res.status}`);
  const data = await res.json();
  return data;
}

async function postLocation(token, lat, lng, accuracy) {
  const res = await fetch(`${API_BASE}/drivers/location`, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ latitude: lat, longitude: lng, accuracyMeters: accuracy })
  });
  if (!res.ok) throw new Error(`location post failed: ${res.status} ${await res.text()}`);
}

(async () => {
  const token = await login('bano77nabil77@gmail.com', 'Yahya123!');
  log('Logged in.');

  const assignment = await getCurrentAssignment(token);
  if (!assignment.hasAssignment) {
    log('Driver has no current assignment. Aborting.');
    process.exit(2);
  }
  const orderId = assignment.assignment.orderId;
  log(`Active order: ${orderId} (status=${assignment.assignment.status})`);

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  const received = [];
  connection.on('ReceiveDriverLocation', (payload) => {
    received.push({ at: Date.now(), payload });
    log(`✅ Got location lat=${payload.latitude}, lng=${payload.longitude}`);
  });

  await connection.start();
  log('Connected.');
  await connection.invoke('SubscribeToOrder', orderId);
  log('Subscribed.');

  // 5 distinct coordinates simulating a moving driver
  const points = [
    [24.7136, 46.6753],
    [24.7140, 46.6760],
    [24.7150, 46.6770],
    [24.7160, 46.6780],
    [24.7170, 46.6790]
  ];

  for (let i = 0; i < points.length; i++) {
    const [lat, lng] = points[i];
    log(`POST location #${i + 1} (${lat}, ${lng})`);
    const sentAt = Date.now();
    await postLocation(token, lat, lng, 10);
    // wait briefly for broadcast
    await new Promise(r => setTimeout(r, 1500));
    const lastReceived = received[received.length - 1];
    if (lastReceived && Math.abs(Number(lastReceived.payload.latitude) - lat) < 0.0001) {
      log(`   → Latency ~${lastReceived.at - sentAt}ms`);
    } else {
      log(`   ⚠ no matching broadcast received yet`);
    }
  }

  log('--- SUMMARY ---');
  log(`Total broadcasts received: ${received.length} of ${points.length}`);
  await connection.stop();
  process.exit(received.length === points.length ? 0 : 3);
})().catch(err => {
  log('ERROR:', err?.message ?? err);
  if (err?.stack) log(err.stack);
  process.exit(1);
});
