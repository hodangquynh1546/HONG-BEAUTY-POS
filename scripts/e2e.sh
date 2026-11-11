#!/usr/bin/env bash
# End-to-end run: disposable PostgreSQL (Docker) + real API serving the production Angular build + Playwright.
# Usage: from HongBeautyPOSUI run `npm run e2e` (extra args are passed to Playwright, e.g. --project=desktop).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UI="$ROOT/HongBeautyPOSUI"
API="$ROOT/HongBeautyPOSAPI/src/HongBeautyPOS.Api"
PG_PORT="${E2E_PG_PORT:-55433}"
API_PORT="${E2E_API_PORT:-5290}"
DB_PASSWORD="$(openssl rand -hex 12)"
NAME="mieu-e2e-pg-$$"
API_PID=""
LOG="$(mktemp -t mieu-e2e-api.XXXXXX)"

cleanup() {
  [[ -n "$API_PID" ]] && kill "$API_PID" 2>/dev/null || true
  docker stop "$NAME" >/dev/null 2>&1 || true
  rm -rf "$API/wwwroot"
}
trap cleanup EXIT

docker run -d --rm --name "$NAME" -e POSTGRES_PASSWORD="$DB_PASSWORD" -e POSTGRES_DB=mieu_e2e \
  -p "127.0.0.1:$PG_PORT:5432" postgres:16-alpine >/dev/null
for _ in $(seq 1 40); do docker exec "$NAME" pg_isready -U postgres -d mieu_e2e >/dev/null 2>&1 && break; sleep 1; done
sleep 2

(cd "$UI" && npx ng build >/dev/null)
rm -rf "$API/wwwroot" && cp -R "$UI/dist/HongBeautyPOSUI/browser" "$API/wwwroot"
dotnet build "$ROOT/HongBeautyPOSAPI/HongBeautyPOS.slnx" -v q >/dev/null

export E2E_OWNER_EMAIL="owner-e2e@example.invalid"
export E2E_OWNER_PASSWORD="E2e!$(openssl rand -hex 10)Aa"
DATABASE_URL="Host=127.0.0.1;Port=$PG_PORT;Database=mieu_e2e;Username=postgres;Password=$DB_PASSWORD" \
JWT_SECRET="$(openssl rand -base64 48)" \
OWNER_EMAIL="$E2E_OWNER_EMAIL" OWNER_NAME="Chu cua hang E2E" OWNER_PASSWORD="$E2E_OWNER_PASSWORD" \
RUN_MIGRATIONS_ON_START=true BOOTSTRAP_OWNER_ON_START=true LOGIN_RATE_LIMIT_PER_MINUTE=1000 \
ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="http://127.0.0.1:$API_PORT" \
  dotnet run --project "$API" --no-build --no-launch-profile >"$LOG" 2>&1 &
API_PID=$!

for _ in $(seq 1 60); do curl -fs "http://127.0.0.1:$API_PORT/health" >/dev/null && break; sleep 1; done
curl -fs "http://127.0.0.1:$API_PORT/health" >/dev/null || { echo "API did not become healthy:"; tail -30 "$LOG"; exit 1; }

cd "$UI"
E2E_BASE_URL="http://127.0.0.1:$API_PORT" npx playwright test "$@"
