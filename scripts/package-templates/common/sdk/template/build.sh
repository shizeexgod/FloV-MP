#!/usr/bin/env bash
# Сборка вашего сервера (Linux). Подробности — ../scripts/build-gamemode.sh --help
exec "$(dirname "$(readlink -f "$0")")/../scripts/build-gamemode.sh" "$@"
