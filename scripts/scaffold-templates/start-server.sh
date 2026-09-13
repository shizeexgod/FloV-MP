#!/usr/bin/env bash
set -e
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

echo "========================================================"
echo " FloV:MP Dedicated Server - RolePlay Engine (Linux)"
echo "========================================================"
echo " Server Root: $ROOT_DIR"
echo "========================================================"

if [ ! -f "server/flovmp-server" ]; then
    echo "[ERROR] server/flovmp-server executable not found!"
    exit 1
fi

chmod +x server/flovmp-server server/flovmp-crash-handler 2>/dev/null || true

if [ -f "config/flovmp.env" ]; then
    echo "[INFO] Loading config/flovmp.env ..."
    export $(grep -v '^#' config/flovmp.env | xargs -d '\n')
fi

cd server
exec ./flovmp-server "$@"
