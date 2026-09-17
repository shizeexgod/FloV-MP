#!/usr/bin/env bash
# Общая загрузка config/flovmp.env для скриптов FloV:MP.
#
# Файл НЕ исполняется через source: пароль или название проекта с пробелом,
# «;» или «$» сломали бы запуск либо выполнились как команда. Читаем строки
# вида КЛЮЧ=значение, как это делает systemd, и снимаем внешние кавычки.

flovmp_load_env() {
  local file="$1" line key val
  [ -f "$file" ] || return 0
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line%$'\r'}"
    case "$line" in
      ''|'#'*) continue ;;
    esac
    [[ "$line" =~ ^[[:space:]]*([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]] || continue
    key="${BASH_REMATCH[1]}"
    val="${BASH_REMATCH[2]}"
    if [[ "$val" =~ ^\"(.*)\"$ ]] || [[ "$val" =~ ^\'(.*)\'$ ]]; then
      val="${BASH_REMATCH[1]}"
    fi
    export "$key=$val"
  done < "$file"
}
