#!/usr/bin/env bash
set -euo pipefail

umask 077
BACKUP_DIR="${BACKUP_DIR:?Set a dedicated absolute RinconV2 backup directory}"
UPLOADS_DIR="${UPLOADS_DIR:?Set the absolute RinconV2 uploads directory}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:?Set the RinconV2 database name}"
DB_USER="${DB_USER:?Set the RinconV2 backup user}"
[[ "$BACKUP_DIR" = /* && "$BACKUP_DIR" != / ]] || { echo "Invalid backup directory" >&2; exit 1; }
[[ "$UPLOADS_DIR" = /* && "$UPLOADS_DIR" != / && -d "$UPLOADS_DIR" ]] || { echo "Invalid uploads directory" >&2; exit 1; }
[[ "$DB_NAME" =~ ^[a-zA-Z0-9_]+$ && "$RETENTION_DAYS" =~ ^[0-9]+$ ]] || { echo "Invalid database name or retention" >&2; exit 1; }

if [[ -z "${PGPASSWORD:-}" ]]; then
  echo "ERROR: PGPASSWORD is required." >&2
  exit 1
fi

timestamp="$(date +%Y%m%d_%H%M%S)"
backup_file="${BACKUP_DIR}/${DB_NAME}_${timestamp}.dump"
uploads_file="${BACKUP_DIR}/${DB_NAME}_uploads_${timestamp}.tar.gz"

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

pg_dump \
  --host "$DB_HOST" \
  --port "$DB_PORT" \
  --username "$DB_USER" \
  --format custom \
  --blobs \
  --no-owner \
  --no-privileges \
  --file "${backup_file}.partial" \
  "$DB_NAME"

tar -C "$UPLOADS_DIR" -czf "${uploads_file}.partial" .
pg_restore --list "${backup_file}.partial" >/dev/null
tar -tzf "${uploads_file}.partial" >/dev/null
mv "${backup_file}.partial" "$backup_file"
mv "${uploads_file}.partial" "$uploads_file"
chmod 600 "$backup_file" "$uploads_file"
find "$BACKUP_DIR" -type f -name "${DB_NAME}_*.dump" -mtime +"$RETENTION_DAYS" -delete
find "$BACKUP_DIR" -type f -name "${DB_NAME}_uploads_*.tar.gz" -mtime +"$RETENTION_DAYS" -delete

echo "Database backup created: $backup_file"
echo "Uploads backup created: $uploads_file"
