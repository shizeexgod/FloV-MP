#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo "==================================================="
echo " Запуск игрового сервера RolePlay (FloV:MP Runtime)"
echo "==================================================="

if [ ! -f "${ROOT_DIR}/server/altv-server" ]; then
    echo "[ОШИБКА] ${ROOT_DIR}/server/altv-server не найден!"
    exit 1
fi

chmod +x "${ROOT_DIR}/server/altv-server" "${ROOT_DIR}/server/altv-crash-handler" 2>/dev/null || true

# Загрузка переменных окружения
if [ -f "${ROOT_DIR}/config/flovmp.env" ]; then
    export $(grep -v '^#' "${ROOT_DIR}/config/flovmp.env" | xargs)
fi

cd "${ROOT_DIR}/server"
export LD_LIBRARY_PATH="modules:modules/js-module:${LD_LIBRARY_PATH}"
./altv-server
