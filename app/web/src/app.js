// Express app: serves the UI and forwards /api/* calls to the Orders API.
// The browser only talks to this web server, so the API never has to be exposed publicly.

import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import express from 'express';

const publicDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'public');

export function createApp({ apiUrl, version = 'dev', fetchImpl = fetch }) {
  const app = express();
  app.use(express.json());
  app.use(express.static(publicDir));

  // Forwards a request to the Orders API and passes its status code and body back to the browser.
  async function forward(res, apiPath, options = {}) {
    try {
      const response = await fetchImpl(`${apiUrl}${apiPath}`, {
        ...options,
        headers: { 'Content-Type': 'application/json' },
        signal: AbortSignal.timeout(5000),
      });
      const body = await response.text();
      res.status(response.status).type('application/json').send(body || undefined);
    } catch (err) {
      console.error(`Orders API call failed: ${apiPath}: ${err.message}`);
      res.status(502).json({ error: 'Orders API unavailable' });
    }
  }

  // Liveness/readiness for Docker HEALTHCHECK and Kubernetes probes.
  // Deliberately does not check the API: a broken API should not restart the web pods.
  app.get('/health', (req, res) => res.json({ status: 'ok' }));

  // Which version and container/pod served the page — and the same for the API behind it.
  app.get('/api/info', async (req, res) => {
    const web = { service: 'orders-web', version, hostname: os.hostname() };
    try {
      const response = await fetchImpl(`${apiUrl}/api/info`, { signal: AbortSignal.timeout(5000) });
      res.json({ web, api: response.ok ? await response.json() : null });
    } catch {
      res.json({ web, api: null });
    }
  });

  app.get('/api/orders', (req, res) => forward(res, '/api/orders'));

  app.post('/api/orders', (req, res) =>
    forward(res, '/api/orders', { method: 'POST', body: JSON.stringify(req.body) }));

  app.delete('/api/orders/:id', (req, res) =>
    forward(res, `/api/orders/${encodeURIComponent(req.params.id)}`, { method: 'DELETE' }));

  return app;
}
