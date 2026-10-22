#!/usr/bin/env bash
# Smoke test after a deployment (M6). Used by the Jenkins pipeline and by hand.
#   1. /health/ready returns 200
#   2. /api/info reports the version we just deployed
#   3. an order placed now is Completed by the worker within 30 s
# Usage:  ./smoke-test.sh <namespace> <expected-version>
# Needs only: get pods + create pods/portforward.
set -euo pipefail
NS="${1:?usage: smoke-test.sh <namespace> <expected-version>}"
VERSION="${2:?expected version missing}"
PORT=18080

kubectl -n "$NS" port-forward svc/orders-api "$PORT:8080" >/dev/null 2>&1 &
pf=$!
trap 'kill $pf 2>/dev/null || true' EXIT
for _ in $(seq 1 20); do curl -fs "localhost:$PORT/health/live" >/dev/null && break; sleep 0.5; done

echo "1/3 readiness"
curl -fsS "localhost:$PORT/health/ready" >/dev/null

echo "2/3 version"
actual="$(curl -fsS "localhost:$PORT/api/info" | sed -n 's/.*"version":"\([^"]*\)".*/\1/p')"
[[ "$actual" == "$VERSION" ]] || { echo "FAIL: expected version $VERSION, got '$actual'"; exit 1; }

echo "3/3 an order is processed end to end"
id="$(curl -fsS -X POST "localhost:$PORT/api/orders" -H 'Content-Type: application/json' \
      -d "{\"product\":\"smoke-test $VERSION\",\"quantity\":1}" | sed -n 's/.*"id":\([0-9]*\).*/\1/p')"
for _ in $(seq 1 30); do
  if curl -fsS "localhost:$PORT/api/orders/$id" | grep -q '"status":"Completed"'; then
    curl -fsS -X DELETE "localhost:$PORT/api/orders/$id" >/dev/null
    echo "PASS: $NS runs $VERSION and processes orders"
    exit 0
  fi
  sleep 1
done
echo "FAIL: order $id was not completed within 30 s"
exit 1
