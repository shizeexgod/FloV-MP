#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
BACKUP_DIR="${ROOT_DIR}/backups"
mkdir -p "${BACKUP_DIR}"

TIMESTAMP=$(date +"%Y-%m-%d_%H-%M-%S")
DUMP_FILE="${BACKUP_DIR}/db_backup_${TIMESTAMP}.sql"
DB_NAME="${FLOVMP_DB_NAME:-flovmp_rp}"
DB_USER="${FLOVMP_DB_USER:-root}"

echo "Создание резервной копии базы данных ${DB_NAME}..."
mysqldump -u "${DB_USER}" --databases "${DB_NAME}" > "${DUMP_FILE}"

if [ $? -eq 0 ]; then
    gzip -f "${DUMP_FILE}"
    echo "[УСПЕХ] Резервная копия сохранена: ${DUMP_FILE}.gz"
else
    echo "[ОШИБКА] Не удалось создать резервную копию базы данных!"
    exit 1
fi
