#!/bin/bash
# Takes one full backup of the database, checks it can be restored and removes
# copies older than BACKUP_KEEP_DAYS.
set -euo pipefail
. "$(dirname "$0")/common.sh"

keep_days="${BACKUP_KEEP_DAYS:-14}"
file="$BACKUP_DIR/${DB_NAME}_$(date '+%Y%m%d_%H%M%S').bak"

log "Backing up $DB_NAME to $file"
sql "BACKUP DATABASE [$DB_NAME] TO DISK = N'$file' WITH INIT, CHECKSUM, NAME = N'$DB_NAME full backup';" > /dev/null
sql "RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM;" > /dev/null
log "Backup verified ($(du -h "$file" | cut -f1))"

find "$BACKUP_DIR" -maxdepth 1 -name "${DB_NAME}_*.bak" -mtime "+$keep_days" -print -delete \
  | while read -r old; do log "Removed $old (older than $keep_days days)"; done
