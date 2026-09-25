#!/usr/bin/env bash
# =====================================================================
#  FloV:MP — запуск игрового сервера (Linux)
#
#  Можно запускать руками из папки установки (./start.sh) — так же его
#  запускает служба systemd, которую создаёт install.sh.
#  Настройки берутся из config/flovmp.env.
# =====================================================================
set -euo pipefail

ROOT="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
# shellcheck source=scripts/lib-env.sh
. "$ROOT/scripts/lib-env.sh"
flovmp_load_env "$ROOT/config/flovmp.env"

if [ ! -f "$ROOT/server/server.toml" ]; then
  echo "[FloV:MP] Нет server/server.toml. Сначала выполните установку: sudo ./install.sh" >&2
  exit 1
fi

# .NET 10 нужен серверным ресурсам на C#. Ищем там, куда его кладут и
# официальный скрипт Microsoft, и пакеты дистрибутивов.
DOTNET_FOUND=""
for d in "${DOTNET_ROOT:-}" /usr/share/dotnet /usr/lib/dotnet /opt/dotnet /usr/local/share/dotnet; do
  [ -n "$d" ] || continue
  if ls -d "$d"/shared/Microsoft.NETCore.App/10.* >/dev/null 2>&1; then
    DOTNET_FOUND="$d"
    break
  fi
done
if [ -z "$DOTNET_FOUND" ]; then
  echo "[FloV:MP] Не найден .NET 10 Runtime. Установите его: sudo ./install.sh (или вручную dotnet-runtime-10.0)." >&2
  exit 1
fi
export DOTNET_ROOT="$DOTNET_FOUND"
DOTNET_SHARED="$(ls -d "$DOTNET_FOUND"/shared/Microsoft.NETCore.App/10.* | sort -V | tail -1)"

cd "$ROOT/server"
chmod +x flovmp-server flovmp-crash-handler 2>/dev/null || true
export LD_LIBRARY_PATH="${DOTNET_SHARED}:modules:modules/js-module:${LD_LIBRARY_PATH:-}"
exec ./flovmp-server "$@"
