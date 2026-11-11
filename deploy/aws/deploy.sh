#!/usr/bin/env bash
# Pull the latest code, rebuild and restart. Safe to repeat. Run on the server from deploy/aws.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
[[ -f .env.production ]] || { echo "Missing .env.production (copy .env.production.example)"; exit 1; }

git -C ../.. pull --ff-only
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build --remove-orphans

DOMAIN="$(grep -E '^DOMAIN=' .env.production | cut -d= -f2-)"
echo "Waiting for https://$DOMAIN/health ..."
for _ in $(seq 1 60); do
  if curl -fsS "https://$DOMAIN/health" >/dev/null 2>&1; then echo "Healthy."; docker image prune -f >/dev/null; exit 0; fi
  sleep 3
done
echo "Not healthy yet. Inspect:  docker compose -f docker-compose.prod.yml logs --tail=80 app caddy"; exit 1
