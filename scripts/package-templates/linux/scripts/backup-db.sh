#!/usr/bin/env bash
# =====================================================================
#  FloV:MP — резервная копия базы данных
#  Использование: ./scripts/backup-db.sh
#  Копии кладутся в backups/ и хранятся 14 дней (FLOVMP_BACKUP_KEEP_DAYS).
# =====================================================================
set -euo pipefail

ROOT="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")/.." && pwd)"
. "$ROOT/scripts/lib-env.sh"
flovmp_load_env "$ROOT/config/flovmp.env"

DB_NAME="${FLOVMP_DB_NAME:-flovmp_server}"
DB_USER="${FLOVMP_DB_USER:-flovmp}"
DB_HOST="${FLOVMP_DB_HOST:-127.0.0.1}"
DB_PORT="${FLOVMP_DB_PORT:-3306}"
KEEP_DAYS="${FLOVMP_BACKUP_KEEP_DAYS:-14}"

if [ -z "${FLOVMP_DB_PASSWORD:-}" ]; then
  echo "[FloV:MP] База не настроена (нет FLOVMP_DB_PASSWORD в config/flovmp.env) — копировать нечего." >&2
  exit 1
fi

BACKUP_DIR="$ROOT/backups"
mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"
DUMP="$BACKUP_DIR/db_${DB_NAME}_$(date +%Y-%m-%d_%H-%M-%S).sql.gz"

# Пароль через окружение, а не аргументом: аргументы видны всем в ps.
# pipefail обязателен: без него ошибка mysqldump терялась бы за успешным gzip,
# и в папке копий лежал бы пустой архив, выглядящий как нормальный.
export MYSQL_PWD="$FLOVMP_DB_PASSWORD"
if mysqldump -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" \
     --single-transaction --routines --triggers "$DB_NAME" | gzip -c > "$DUMP"; then
  chmod 600 "$DUMP"
  echo "[FloV:MP] Копия сохранена: $DUMP ($(du -h "$DUMP" | cut -f1))"
else
  rm -f "$DUMP"
  echo "[FloV:MP] ОШИБКА: не удалось снять копию базы $DB_NAME." >&2
  exit 1
fi

find "$BACKUP_DIR" -name "db_${DB_NAME}_*.sql.gz" -mtime +"$KEEP_DAYS" -delete 2>/dev/null || true
