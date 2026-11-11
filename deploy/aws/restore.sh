#!/usr/bin/env bash
# Restore a dump into the running database. DESTRUCTIVE: replaces current data.
# Usage: restore.sh s3://bucket/db/mieu-XXXX.sql.gz   (or a local .sql.gz path)
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
set -a; source .env.production; set +a
SRC="${1:?Usage: restore.sh <s3://... or local .sql.gz>}"
read -r -p "This REPLACES all data in ${POSTGRES_DB:-mieu_pos}. Type RESTORE to continue: " answer
[[ "$answer" == "RESTORE" ]] || { echo "Cancelled"; exit 1; }

TMP="$(mktemp)"; trap 'rm -f "$TMP"' EXIT
if [[ "$SRC" == s3://* ]]; then aws s3 cp "$SRC" "$TMP" --only-show-errors; else cp "$SRC" "$TMP"; fi

DC=(docker compose --env-file .env.production -f docker-compose.prod.yml)
"${DC[@]}" stop app
"${DC[@]}" exec -T db psql -U "${POSTGRES_USER:-mieu}" -d postgres -c "DROP DATABASE IF EXISTS \"${POSTGRES_DB:-mieu_pos}\" WITH (FORCE);" \
  -c "CREATE DATABASE \"${POSTGRES_DB:-mieu_pos}\";"
gunzip -c "$TMP" | "${DC[@]}" exec -T db psql -U "${POSTGRES_USER:-mieu}" -d "${POSTGRES_DB:-mieu_pos}" -v ON_ERROR_STOP=1 >/dev/null
"${DC[@]}" start app
echo "Restored from $SRC"
