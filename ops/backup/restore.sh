#!/bin/bash
# Restores a backup over the current database. Without an argument it uses the
# newest one. Open connections are closed, so the API reconnects by itself.
#   docker compose exec backup restore.sh            newest copy
#   docker compose exec backup restore.sh Ordali_20261105_030000.bak
set -euo pipefail
. "$(dirname "$0")/common.sh"

if [ $# -gt 0 ]; then
  file="$BACKUP_DIR/$(basename "$1")"
else
  file=$(ls -1 "$BACKUP_DIR"/"${DB_NAME}"_*.bak 2>/dev/null | sort | tail -n 1 || true)
fi
if [ -z "$file" ] || [ ! -f "$file" ]; then
  echo "No backup found. Available copies:" >&2
  ls -1 "$BACKUP_DIR" >&2 || true
  exit 1
fi

log "Restoring $DB_NAME from $file"
# A copy brought in with "docker compose cp" belongs to root; SQL Server runs as mssql.
chown 10001:10001 "$file"
sql "RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM;" > /dev/null
sql "IF DB_ID(N'$DB_NAME') IS NOT NULL ALTER DATABASE [$DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
     RESTORE DATABASE [$DB_NAME] FROM DISK = N'$file' WITH REPLACE, CHECKSUM;
     ALTER DATABASE [$DB_NAME] SET MULTI_USER;" > /dev/null
log "Restore finished"
