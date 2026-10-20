# Orders Web (Node.js 22+)

Web frontend of the sample app. Serves a small UI and forwards `/api/*` calls to the Orders API — the browser only talks to this server, so the API does not need to be exposed publicly.

The page refreshes every 2 seconds, so you can watch the worker move orders from *Pending* to *Completed*. The footer shows the **version and hostname** of both the web and API container that answered — useful for scaling and rolling-update demos.

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/` | UI |
| GET | `/health` | Liveness/readiness probe (does not depend on the API) |
| GET | `/api/info` | Version + hostname of web and API |
| GET / POST | `/api/orders` | Forwarded to the Orders API |
| DELETE | `/api/orders/:id` | Forwarded to the Orders API |

If the API is unreachable, `/api/orders` returns `502` and the UI shows "Orders API is unavailable".

## Configuration (environment variables)

| Variable | Default | Purpose |
|----------|---------|---------|
| `API_URL` | `http://localhost:8080` | Orders API base URL |
| `PORT` | `3000` | Listen port |
| `APP_VERSION` | `dev` | Shown in the footer |

## Run & test locally

```bash
npm install
npm test
npm start          # http://localhost:3000 — needs the API on localhost:8080
```
