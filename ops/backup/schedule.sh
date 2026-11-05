#!/bin/bash
# Runs backup.sh on a schedule: one copy as soon as the database exists, then
# every day at BACKUP_TIME (HH:MM, TZ time zone), or every BACKUP_EVERY_MINUTES
# minutes if that is set.
set -uo pipefail
. "$(dirname "$0")/common.sh"

# The volume is created empty and owned by root; SQL Server runs as mssql.
mkdir -p "$BACKUP_DIR" && chown 10001:0 "$BACKUP_DIR"

log "Waiting for database $DB_NAME"
until [ "$(sql "SELECT CASE WHEN DB_ID(N'$DB_NAME') IS NULL THEN 0 ELSE 1 END;" 2>/dev/null)" = "1" ]; do
  sleep 10
done

next_run() {
  if [ -n "${BACKUP_EVERY_MINUTES:-}" ]; then
    echo $(( $(date +%s) + BACKUP_EVERY_MINUTES * 60 ))
  else
    local at
    at=$(date -d "today ${BACKUP_TIME:-03:00}" +%s)
    [ "$at" -le "$(date +%s)" ] && at=$(date -d "tomorrow ${BACKUP_TIME:-03:00}" +%s)
    echo "$at"
  fi
}

while true; do
  bash "$(dirname "$0")/backup.sh" || log "Backup FAILED, will try again at the next scheduled time"
  at=$(next_run)
  log "Next backup at $(date -d "@$at" '+%Y-%m-%d %H:%M')"
  sleep $(( at - $(date +%s) ))
done
