// Tests use Node's built-in test runner (node --test) and a fake fetch instead of a real Orders API.

import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { createApp } from '../src/app.js';

const servers = [];
after(() => servers.forEach((s) => s.close()));

// Starts the web app on a random free port with a fake Orders API.
// fakeApi(url, options) returns { status, body } or throws to simulate the API being down.
async function startWeb(fakeApi) {
  const calls = [];
  const fetchImpl = async (url, options = {}) => {
    calls.push({ url, method: options.method ?? 'GET', body: options.body });
    const { status = 200, body } = await fakeApi(url, options);
    const text = body === undefined ? '' : JSON.stringify(body);
    return new Response(status === 204 ? null : text, { status });
  };
  const server = createApp({ apiUrl: 'http://api:8080', version: '1.2.3', fetchImpl }).listen(0);
  servers.push(server);
  await new Promise((resolve) => server.once('listening', resolve));
  return { baseUrl: `http://localhost:${server.address().port}`, calls };
}

const apiDown = () => { throw new Error('connect ECONNREFUSED'); };

test('health endpoint returns ok', async () => {
  const { baseUrl } = await startWeb(apiDown);

  const response = await fetch(`${baseUrl}/health`);

  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { status: 'ok' });
});

test('serves the UI', async () => {
  const { baseUrl } = await startWeb(apiDown);

  const response = await fetch(`${baseUrl}/`);

  assert.equal(response.status, 200);
  assert.match(await response.text(), /<title>Orders/);
});

test('GET /api/orders forwards to the Orders API', async () => {
  const orders = [{ id: 1, product: 'Laptop', quantity: 2, status: 'Pending' }];
  const { baseUrl, calls } = await startWeb(() => ({ body: orders }));

  const response = await fetch(`${baseUrl}/api/orders`);

  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), orders);
  assert.equal(calls[0].url, 'http://api:8080/api/orders');
});

test('POST /api/orders forwards body and passes through 201', async () => {
  const { baseUrl, calls } = await startWeb(() => ({ status: 201, body: { id: 5 } }));

  const response = await fetch(`${baseUrl}/api/orders`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ product: 'Monitor', quantity: 1 }),
  });

  assert.equal(response.status, 201);
  assert.equal(calls[0].method, 'POST');
  assert.deepEqual(JSON.parse(calls[0].body), { product: 'Monitor', quantity: 1 });
});

test('validation errors from the API are passed through as 400', async () => {
  const problem = { errors: { Product: ['Product is required.'] } };
  const { baseUrl } = await startWeb(() => ({ status: 400, body: problem }));

  const response = await fetch(`${baseUrl}/api/orders`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ product: '', quantity: 1 }),
  });

  assert.equal(response.status, 400);
  assert.deepEqual(await response.json(), problem);
});

test('DELETE /api/orders/:id forwards to the API', async () => {
  const { baseUrl, calls } = await startWeb(() => ({ status: 204 }));

  const response = await fetch(`${baseUrl}/api/orders/7`, { method: 'DELETE' });

  assert.equal(response.status, 204);
  assert.equal(calls[0].url, 'http://api:8080/api/orders/7');
  assert.equal(calls[0].method, 'DELETE');
});

test('returns 502 when the Orders API is down', async () => {
  const { baseUrl } = await startWeb(apiDown);

  const response = await fetch(`${baseUrl}/api/orders`);

  assert.equal(response.status, 502);
  assert.deepEqual(await response.json(), { error: 'Orders API unavailable' });
});

test('info shows web and API versions', async () => {
  const apiInfo = { service: 'orders-api', version: '2.0.0', hostname: 'api-pod-1' };
  const { baseUrl } = await startWeb(() => ({ body: apiInfo }));

  const info = await (await fetch(`${baseUrl}/api/info`)).json();

  assert.equal(info.web.service, 'orders-web');
  assert.equal(info.web.version, '1.2.3');
  assert.deepEqual(info.api, apiInfo);
});

test('info still works when the API is down', async () => {
  const { baseUrl } = await startWeb(apiDown);

  const info = await (await fetch(`${baseUrl}/api/info`)).json();

  assert.equal(info.web.version, '1.2.3');
  assert.equal(info.api, null);
});
