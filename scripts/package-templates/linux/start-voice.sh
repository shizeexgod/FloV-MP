#!/usr/bin/env bash
# =====================================================================
#  FloV:MP — запуск голосового сервера (Linux)
#  Читает voice/voice.toml. Секрет в нём обязан совпадать с externalSecret
#  в server/server.toml — оба файла создаёт install.sh.
# =====================================================================
set -euo pipefail

ROOT="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"

if [ ! -f "$ROOT/voice/voice.toml" ]; then
  echo "[FloV:MP] Нет voice/voice.toml. Сначала выполните установку: sudo ./install.sh" >&2
  exit 1
fi

cd "$ROOT/voice"
chmod +x altv-voice-server altv-crash-handler 2>/dev/null || true
exec ./altv-voice-server "$@"
