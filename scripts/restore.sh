#!/usr/bin/env bash
# Restores a backup made by scripts/backup.sh into the running database. THIS REPLACES THE CURRENT DATA.
# Usage: scripts/restore.sh backups/careerpilot-YYYYMMDDTHHMMSSZ.dump
set -euo pipefail

file="${1:?Usage: scripts/restore.sh <backup-file>}"
[ -f "$file" ] || { echo "No such file: $file" >&2; exit 1; }

echo "This will REPLACE all current CareerPilot data with the contents of $file."
read -r -p "Type RESTORE to continue: " answer
[ "$answer" = "RESTORE" ] || { echo "Cancelled."; exit 1; }

# Stop the API so nothing writes while the database is replaced.
docker compose stop api
docker compose exec -T db pg_restore -U careerpilot -d careerpilot --clean --if-exists --no-owner < "$file"
docker compose start api
echo "Restored from $file. Open the app and check your profile and applications."
