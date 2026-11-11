#!/usr/bin/env bash
# Dump PostgreSQL, upload to S3 (via the instance role, no access keys on disk) and prune old local dumps.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
set -a; source .env.production; set +a
: "${BACKUP_BUCKET:?Set BACKUP_BUCKET in .env.production}"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
FILE="/srv/mieu/backups/mieu-$STAMP.sql.gz"

docker compose --env-file .env.production -f docker-compose.prod.yml exec -T db \
  pg_dump -U "${POSTGRES_USER:-mieu}" -d "${POSTGRES_DB:-mieu_pos}" --no-owner | gzip > "$FILE"
[[ -s "$FILE" ]] || { echo "Empty dump, aborting"; rm -f "$FILE"; exit 1; }

aws s3 cp "$FILE" "s3://$BACKUP_BUCKET/db/$(basename "$FILE")" --only-show-errors
find /srv/mieu/backups -name 'mieu-*.sql.gz' -mtime +"${BACKUP_RETENTION_DAYS:-30}" -delete
echo "Backup uploaded: $(basename "$FILE")"
