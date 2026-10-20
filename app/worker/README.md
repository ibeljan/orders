# Orders Worker (Python 3.13)

Background worker of the sample app. Polls the Orders API for *Pending* orders, "processes" them (simulated with a short sleep) and marks them *Completed* with its own hostname — so you can see which container / pod did the work.

## How it works

1. Every `POLL_INTERVAL_SECONDS`: `GET /api/orders?status=Pending`
2. For each order (oldest first): simulate work, then `POST /api/orders/{id}/complete` with `processedBy = hostname`
3. If another worker replica completed the order first (HTTP 409), it skips it — normal when scaled out
4. If the API is unreachable, it logs a warning and retries next loop (does not crash)
5. Touches a heartbeat file every loop — usable for a Docker `HEALTHCHECK` / Kubernetes `livenessProbe`
6. On `SIGTERM` (Kubernetes stopping the pod) it finishes the current loop and exits cleanly

## Configuration (environment variables)

| Variable | Default | Purpose |
|----------|---------|---------|
| `API_URL` | `http://localhost:8080` | Orders API base URL |
| `POLL_INTERVAL_SECONDS` | `5` | Pause between polls |
| `PROCESSING_SECONDS` | `1` | Simulated work per order |
| `WORKER_NAME` | hostname | Name written to `processedBy` |
| `HEARTBEAT_FILE` | `/tmp/worker-heartbeat` | File touched every loop |
| `LOG_LEVEL` | `INFO` | Python log level |

## Run & test locally

```bash
python3.13 -m venv .venv
.venv/bin/pip install -r requirements-dev.txt
.venv/bin/python -m pytest
.venv/bin/python -m worker.main     # needs the API running on localhost:8080
```
