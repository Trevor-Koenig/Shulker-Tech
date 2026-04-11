#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR=/backups

do_backup() {
    mkdir -p "$BACKUP_DIR"
    local ts
    ts=$(date -u +%Y%m%d_%H%M%S)
    local file="$BACKUP_DIR/backup_${ts}.sql.gz"
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Backing up ${POSTGRES_DB} → ${file}"
    pg_dump -h db -U "$POSTGRES_USER" "$POSTGRES_DB" | gzip > "$file"
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Done ($(du -sh "$file" | cut -f1))"
    # Prune backups older than 14 days
    find "$BACKUP_DIR" -name "backup_*.sql.gz" -mtime +14 -delete
}

shutdown() {
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Shutdown signal — running final backup"
    do_backup
    exit 0
}

trap shutdown SIGTERM SIGINT

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Backup service started"
do_backup

while true; do
    sleep 86400 &
    wait $!
    do_backup
done
