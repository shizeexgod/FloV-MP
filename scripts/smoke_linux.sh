#!/usr/bin/env bash
# Дымовой тест Linux-пакета на живой машине: установка отдельным инстансом,
# сборка своего сервера, обновление поверх, удаление. Ничего не трогает у
# уже установленных серверов: своя папка, своя служба, свои порты, без базы.
#
#   sudo ./smoke_linux.sh /путь/к/flovmp-server-<версия>-linux.tar.gz [порт]
#
# Код возврата: 0 — всё прошло, 1 — есть провалы.
set -u

PKG="${1:-}"
PORT="${2:-7999}"
[ -f "$PKG" ] || { echo "Использование: sudo $0 <пакет .tar.gz> [порт]"; exit 1; }
[ "$(id -u)" -eq 0 ] || { echo "Запустите от root: sudo $0 ..."; exit 1; }

SERVICE="flovmp-smoke"
DIR="/opt/flovmp-smoke"
WORK="$(mktemp -d)"
FAILS=0

check() {
  if [ "$1" -eq 0 ]; then echo "  [PASS] $2"; else echo "  [FAIL] $2"; FAILS=$((FAILS + 1)); fi
}

cleanup() {
  if [ -x "$DIR/install.sh" ]; then
    "$DIR/install.sh" --uninstall --purge --yes --dir "$DIR" --service "$SERVICE" >/dev/null 2>&1 || true
  fi
  rm -rf "$WORK" /opt/flovmp-removed-"$SERVICE"-*.tar.gz
}
trap cleanup EXIT

echo "=== Распаковка"
tar -xzf "$PKG" -C "$WORK"
SRC="$(find "$WORK" -maxdepth 1 -type d -name 'flovmp-server-*' | head -1)"
[ -n "$SRC" ]; check $? "пакет распакован"
(cd "$SRC" && sha256sum --quiet -c manifest.txt); check $? "файлы пакета совпадают с manifest.txt"

ARGS=(--dir "$DIR" --service "$SERVICE" --port "$PORT"
      --voice-port $((PORT - 5)) --voice-internal-port $((PORT - 6)) --no-db)

echo "=== Установка"
(cd "$SRC" && ./install.sh "${ARGS[@]}") > "$WORK/install.log" 2>&1
check $? "установка завершилась без ошибок"
systemctl is-active --quiet "$SERVICE"; check $? "служба сервера активна"
grep -q "Платформа запущена" "$DIR/server/server.log"; check $? "платформа поднялась"
[ -f "$DIR/server/config/admin-commands.cfg" ]; check $? "создан server/config/admin-commands.cfg"
[ -f "$DIR/gamemode/build.sh" ]; check $? "создана папка gamemode"

echo "=== Сборка своего сервера"
"$DIR/gamemode/build.sh" --restart > "$WORK/build.log" 2>&1
check $? "gamemode собирается и сервер перезапускается"
sleep 12
grep -q "Loaded resource gamemode" "$DIR/server/server.log"; check $? "свой ресурс gamemode загружен"

echo "=== Обновление поверх"
echo "// правка владельца" >> "$DIR/gamemode/src/GamemodeResource.cs"
BEFORE="$(sha256sum "$DIR/gamemode/src/GamemodeResource.cs" "$DIR/server/server.toml" | cut -c1-64)"
(cd "$SRC" && ./install.sh --dir "$DIR" --service "$SERVICE") > "$WORK/upgrade.log" 2>&1
check $? "обновление завершилось без ошибок"
AFTER="$(sha256sum "$DIR/gamemode/src/GamemodeResource.cs" "$DIR/server/server.toml" | cut -c1-64)"
[ "$BEFORE" = "$AFTER" ]; check $? "код владельца и server.toml не тронуты обновлением"
systemctl is-active --quiet "$SERVICE"; check $? "служба активна после обновления"

echo "=== Удаление"
"$DIR/install.sh" --uninstall --purge --yes --dir "$DIR" --service "$SERVICE" > "$WORK/purge.log" 2>&1
check $? "удаление завершилось без ошибок"
[ ! -d "$DIR" ]; check $? "папка установки удалена"
sleep 2
! ss -lunp 2>/dev/null | grep -q ":$((PORT - 5)) "; check $? "порты освобождены"

echo
if [ "$FAILS" -eq 0 ]; then echo "Дымовой тест пройден."; exit 0; fi
echo "Провалов: $FAILS. Логи: $WORK (удаляются при выходе — скопируйте, если нужны)"
exit 1
