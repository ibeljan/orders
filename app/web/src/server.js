import { createApp } from './app.js';

const port = Number(process.env.PORT ?? 3000);
const apiUrl = (process.env.API_URL ?? 'http://localhost:8080').replace(/\/$/, '');
const version = process.env.APP_VERSION ?? 'dev';

const server = createApp({ apiUrl, version }).listen(port, () => {
  console.log(`orders-web ${version} listening on port ${port}, using Orders API at ${apiUrl}`);
});

// Kubernetes sends SIGTERM before stopping a pod: stop accepting new connections, then exit.
for (const signal of ['SIGTERM', 'SIGINT']) {
  process.on(signal, () => {
    console.log(`Received ${signal}, shutting down`);
    server.close(() => process.exit(0));
  });
}
