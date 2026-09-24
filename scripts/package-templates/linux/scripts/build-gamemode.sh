#!/usr/bin/env bash
# Сборка вашего сервера (папка gamemode) в server/resources/gamemode.
#
#   ./scripts/build-gamemode.sh                 собрать
#   ./scripts/build-gamemode.sh --install-sdk   сначала поставить .NET SDK 8 (нужен root)
#   ./scripts/build-gamemode.sh --restart       после сборки перезапустить службу
set -euo pipefail

ROOT="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
GAMEMODE="$ROOT/gamemode"
TOML="$ROOT/server/server.toml"
INSTALL_SDK=0
RESTART=0

for arg in "$@"; do
  case "$arg" in
    --install-sdk) INSTALL_SDK=1 ;;
    --restart) RESTART=1 ;;
    -h|--help) sed -n '2,7p' "$0"; exit 0 ;;
    *) echo "Неизвестный параметр: $arg" >&2; exit 2 ;;
  esac
done

for d in /usr/share/dotnet /usr/lib/dotnet /opt/dotnet; do
  [ -x "$d/dotnet" ] && export PATH="$d:$PATH" && export DOTNET_ROOT="$d" && break
done

has_sdk() {
  command -v dotnet >/dev/null 2>&1 || return 1
  dotnet --list-sdks 2>/dev/null | awk -F. '$1 >= 8 { found=1 } END { exit found ? 0 : 1 }'
}

if ! has_sdk; then
  if [ "$INSTALL_SDK" -eq 1 ]; then
    [ "$(id -u)" -eq 0 ] || { echo "Установка SDK требует root: sudo $0 --install-sdk" >&2; exit 1; }
    echo "==> Установка .NET SDK 8 в /usr/share/dotnet"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
    rm -f /tmp/dotnet-install.sh
    export PATH="/usr/share/dotnet:$PATH" DOTNET_ROOT=/usr/share/dotnet
  else
    echo "Для сборки нужен .NET SDK 8 (Runtime, который ставит установщик, собирать не умеет)." >&2
    echo "Поставить автоматически: sudo $0 --install-sdk" >&2
    exit 1
  fi
fi

if [ ! -f "$GAMEMODE/Gamemode.csproj" ]; then
  [ -d "$ROOT/sdk/template" ] || { echo "Нет sdk/template — распакуйте пакет полностью." >&2; exit 1; }
  cp -r "$ROOT/sdk/template" "$GAMEMODE"
  chmod +x "$GAMEMODE/build.sh"
  echo "==> Папка gamemode создана из шаблона"
fi

echo "==> Сборка gamemode"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet build "$GAMEMODE/Gamemode.csproj" -c Release -nologo

# Подключаем ресурс в server.toml, если его там нет.
if [ -f "$TOML" ] && ! awk '/^resources[[:space:]]*=[[:space:]]*\[/{f=1} f&&/"gamemode"/{found=1} f&&/^[[:space:]]*\]/{f=0} END{exit found?0:1}' "$TOML"; then
  awk '/^resources[[:space:]]*=[[:space:]]*\[/{f=1} f&&/^[[:space:]]*\]/{print "    \"gamemode\","; f=0} {print}' "$TOML" > "$TOML.tmp"
  cat "$TOML.tmp" > "$TOML" && rm -f "$TOML.tmp"
  echo "==> Ресурс gamemode подключён в server/server.toml"
fi

# Файлы сервера принадлежат пользователю службы — отдаём ему и результат сборки.
OWNER="$(stat -c %U "$ROOT/server" 2>/dev/null || echo root)"
if [ "$(id -u)" -eq 0 ] && [ "$OWNER" != "root" ]; then
  chown -R "$OWNER:$OWNER" "$ROOT/server/resources/gamemode" "$GAMEMODE"
fi

echo "==> Готово: server/resources/gamemode"
if [ "$RESTART" -eq 1 ]; then
  # Служба именно этой установки: на машине может быть несколько серверов.
  SERVICE="$(grep -lsx "WorkingDirectory=$ROOT/server" /etc/systemd/system/*.service 2>/dev/null | head -1 | xargs -r basename)"
  if [ -n "$SERVICE" ]; then
    systemctl restart "$SERVICE" && echo "==> Служба $SERVICE перезапущена"
  else
    echo "Служба не найдена — перезапустите сервер вручную." >&2
  fi
else
  echo "Перезапустите сервер (или соберите с --restart)."
fi
