#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

if [ -f "${ROOT_DIR}/license.flv" ]; then
    echo "[ИНФО] Файл лицензии license.flv присутствует:"
    cat "${ROOT_DIR}/license.flv"
else
    echo "[ВНИМАНИЕ] Файл license.flv не найден."
    echo "Поместите файл лицензии в корень каталога проекта."
fi
echo ""
