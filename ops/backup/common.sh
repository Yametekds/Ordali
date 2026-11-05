# Shared settings for the backup scripts. SQL Server writes the .bak files
# itself, so BACKUP_DIR must be the same path in this container and in sqlserver.
BACKUP_DIR="${BACKUP_DIR:-/var/opt/mssql/backup}"
DB_NAME="${DB_NAME:-Ordali}"
DB_SERVER="${DB_SERVER:-sqlserver}"
export SQLCMDPASSWORD="$SA_PASSWORD"

# Prints the query result; on failure prints SQL Server's messages to stderr.
sql() {
  local out
  if out=$(/opt/mssql-tools18/bin/sqlcmd -C -b -S "$DB_SERVER" -U sa -h -1 -W -Q "SET NOCOUNT ON; $1" 2>&1); then
    echo "$out"
  else
    echo "$out" >&2
    return 1
  fi
}

log() {
  echo "$(date '+%Y-%m-%d %H:%M:%S') $*"
}
