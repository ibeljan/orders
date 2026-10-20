# Orders API (C# / .NET 10)

REST API of the sample app. Stores orders in PostgreSQL; the Python worker processes them, the Node.js web UI shows them.

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/orders` | All orders (newest first) |
| GET | `/api/orders?status=Pending` | Filter by status (`Pending`, `Completed`) — used by the worker |
| GET | `/api/orders/{id}` | One order |
| POST | `/api/orders` | Create — body `{"product":"Laptop","quantity":2}` |
| POST | `/api/orders/{id}/complete` | Mark processed — body `{"processedBy":"worker-1"}` (409 if already completed) |
| DELETE | `/api/orders/{id}` | Delete |
| GET | `/api/info` | Service name, version, hostname (which container/pod answered) |
| GET | `/health/live` | Liveness probe |
| GET | `/health/ready` | Readiness probe (checks database) |

## Configuration (environment variables)

| Variable | Default | Purpose |
|----------|---------|---------|
| `ConnectionStrings__OrdersDb` | *(empty → in-memory DB)* | PostgreSQL connection string, e.g. `Host=db;Database=orders;Username=orders;Password=...` |
| `APP_VERSION` | `dev` | Shown in `/api/info` |
| `ASPNETCORE_URLS` | `http://+:8080` in containers | Listen address |

## Run & test locally

```bash
dotnet test
dotnet run --project OrdersApi
```

API listens on http://localhost:8080. Without a connection string it uses an in-memory database (data is lost on restart).
