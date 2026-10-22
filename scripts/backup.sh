#!/usr/bin/env bash
# Backs up the CareerPilot database (everything you entered: profile, jobs, applications, documents).
# Usage: scripts/backup.sh [backup-dir]      (default: ./backups)
# Keeps the newest $KEEP backups (default 14). Run it from the repository folder where docker-compose.yml is.
set -euo pipefail

dir="${1:-backups}"
keep="${KEEP:-14}"
umask 077                       # backups contain personal data: owner-only
mkdir -p "$dir"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
file="$dir/careerpilot-$stamp.dump"

# Custom format (-Fc): compressed, and restorable table by table.
docker compose exec -T db pg_dump -U careerpilot -d careerpilot -Fc > "$file.partial"

# A truncated or empty dump is worse than none: check that PostgreSQL can read it before keeping it.
if ! docker compose exec -T db pg_restore --list < "$file.partial" > /dev/null; then
  rm -f "$file.partial"
  echo "Backup failed: the dump could not be read back." >&2
  exit 1
fi
mv "$file.partial" "$file"
echo "Backup written: $file ($(du -h "$file" | cut -f1))"

# Retention: delete everything but the newest $keep.
ls -1t "$dir"/careerpilot-*.dump 2>/dev/null | tail -n +"$((keep + 1))" | while read -r old; do
  rm -f "$old"
  echo "Removed old backup: $old"
done
