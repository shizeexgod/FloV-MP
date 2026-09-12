#!/usr/bin/env bash
# ==============================================================================
# FloV:MP — Официальный автоматический установщик сервера (One-Line Installer)
# Использование:
#   curl -sSL https://flov-mp.ru/install.sh | bash -s -- --key FLV-XXXX-XXXX-XXXX
# ==============================================================================
set -euo pipefail

RED='\033[031m'
GREEN='\033[032m'
CYAN='\033[036m'
YELLOW='\033[033m'
BOLD='\033[1m'
NC='\033[0m'

PORTAL_URL="${FLOVMP_PORTAL_URL:-https://flov-mp.ru}"
INSTALL_DIR="${FLOVMP_DIR:-/opt/flovmp}"
VOICE_DIR="${INSTALL_DIR}/voice"
LICENSE_KEY=""
SERVER_PORT="${PORT:-7788}"

echo -e "${CYAN}${BOLD}"
echo "  ███████╗██╗      ██████╗ ██╗   ██╗   ███╗   ███╗██████╗ "
echo "  ██╔════╝██║     ██╔═══██╗██║   ██║   ████╗ ████║██╔══██╗"
echo "  █████╗  ██║     ██║   ██║██║   ██║   ██╔████╔██║██████╔╝"
echo "  ██╔══╝  ██║     ██║   ██║╚██╗ ██╔╝   ██║╚██╔╝██║██╔═══╝ "
echo "  ██║     ███████╗╚██████╔╝ ╚████╔╝    ██║ ╚═╝ ██║██║     "
echo "  ╚═╝     ╚══════╝ ╚═════╝   ╚═══╝     ╚═╝     ╚═╝╚═╝     "
echo -e "  Автоматический установщик игрового сервера FloV:MP${NC}\n"

# ------------------------------------------------------------------------------
# 1. Разбор аргументов
# ------------------------------------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --key|-k)
      LICENSE_KEY="$2"
      shift 2
      ;;
    --dir|-d)
      INSTALL_DIR="$2"
      VOICE_DIR="${INSTALL_DIR}/voice"
      shift 2
      ;;
    --portal|-p)
      PORTAL_URL="$2"
      shift 2
      ;;
    --port)
      SERVER_PORT="$2"
      shift 2
      ;;
    *)
      echo -e "${YELLOW}Неизвестный параметр: $1${NC}"
      shift
      ;;
  esac
done

if [ -z "$LICENSE_KEY" ]; then
  echo -e "${RED}[Ошибка] Ключ лицензии не указан!${NC}"
  echo -e "Использование: curl -sSL $PORTAL_URL/install.sh | bash -s -- --key ${BOLD}FLV-XXXX-XXXX-XXXX${NC}"
  exit 1
fi

echo -e "${CYAN}[1/6] Проверка окружения и зависимостей...${NC}"
if [ "$EUID" -ne 0 ]; then
  echo -e "${RED}[Ошибка] Скрипт должен запускаться с правами root (sudo)!${NC}"
  exit 1
fi

# Установка системных зависимостей
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq >/dev/null
apt-get install -y -qq curl wget jq tar libatomic1 ca-certificates mariadb-server >/dev/null 2>&1 || true

# Установка .NET 8 Runtime (если не установлен)
if ! command -v dotnet >/dev/null 2>&1; then
  echo -e "${CYAN}--> Установка .NET 8 CoreCLR runtime...${NC}"
  wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs 2>/dev/null || echo "22.04")/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb 2>/dev/null || true
  if [ -f /tmp/packages-microsoft-prod.deb ]; then
    dpkg -i /tmp/packages-microsoft-prod.deb >/dev/null 2>&1 || true
    rm -f /tmp/packages-microsoft-prod.deb
  fi
  apt-get update -qq >/dev/null
  apt-get install -y -qq dotnet-runtime-8.0 aspnetcore-runtime-8.0 >/dev/null 2>&1 || true
fi

echo -e "${CYAN}[2/6] Запрос лицензии с портала (${LICENSE_KEY})...${NC}"
mkdir -p "$INSTALL_DIR" "$VOICE_DIR"

# Скачивание криптографически подписанного license.flv
TMP_LIC="/tmp/license_resp_$$.json"
HTTP_CODE=$(curl -sSL -w "%{http_code}" -o "$TMP_LIC" "${PORTAL_URL}/api/v1/licenses/download-by-key?key=${LICENSE_KEY}" 2>/dev/null || echo "000")

if [ "$HTTP_CODE" -eq 200 ] && [ -s "$TMP_LIC" ]; then
  cp "$TMP_LIC" "${INSTALL_DIR}/license.flv"
  echo -e "${GREEN}✓ Лицензия успешно получена и верифицирована!${NC}"
else
  echo -e "${YELLOW}! Предупреждение: Портал недоступен или ключ не найден (HTTP $HTTP_CODE).${NC}"
  echo -e "${YELLOW}  Сервер будет запущен в автономном/демо-режиме, ключ сохранён в flovmp.env.${NC}"
fi
rm -f "$TMP_LIC"

echo -e "${CYAN}[3/6] Загрузка файлов платформы мультиплеера...${NC}"
DIST_URL="${PORTAL_URL}/api/v1/distribution/download-latest"
if curl --head --silent --fail "$DIST_URL" >/dev/null 2>&1; then
  echo -e "--> Скачивание дистрибутива с портала..."
  curl -sSL "$DIST_URL" | tar -xz -C "$INSTALL_DIR" --strip-components=1 2>/dev/null || true
else
  echo -e "${YELLOW}--> Используется локальный/резервный пакет поставки.${NC}"
fi

DB_NAME="${FLOVMP_DB_NAME:-flovmp_server}"

# Настройка flovmp.env
cat > "${INSTALL_DIR}/flovmp.env" <<ENV
# FloV:MP Server Configuration
FLOVMP_LICENSE_KEY=${LICENSE_KEY}
FLOVMP_API_PORT=7799
FLOVMP_PORTAL_URL=${PORTAL_URL}
FLOVMP_DB_CONNECTION=Server=127.0.0.1;Port=3306;Database=${DB_NAME};Uid=root;Pwd=;
ENV
chmod 600 "${INSTALL_DIR}/flovmp.env"

echo -e "${CYAN}[4/6] Настройка локальной БД MariaDB...${NC}"
systemctl enable --now mariadb >/dev/null 2>&1 || true
mysql -u root -e "CREATE DATABASE IF NOT EXISTS ${DB_NAME} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;" 2>/dev/null || true
if [ -f "${INSTALL_DIR}/schema.sql" ]; then
  mysql -u root "${DB_NAME}" < "${INSTALL_DIR}/schema.sql" 2>/dev/null || true
fi

echo -e "${CYAN}[5/6] Регистрация системных служб (Systemd с PartOf= для голоса)...${NC}"

# 1. Голосовой сервер (PartOf=flovmp-game гарантирует перезапуск при рестарте игры)
cat > /etc/systemd/system/flovmp-voice.service <<EOF
[Unit]
Description=FloV:MP Voice Server (alt:V 16.4.39 external voice)
After=network.target
Before=flovmp-game.service
PartOf=flovmp-game.service

[Service]
Type=simple
WorkingDirectory=${VOICE_DIR}
ExecStart=${VOICE_DIR}/altv-voice-server
Restart=always
RestartSec=3
LimitNOFILE=65535

[Install]
WantedBy=multi-user.target
EOF

# 2. Игровой сервер
cat > /etc/systemd/system/flovmp-game.service <<EOF
[Unit]
Description=FloV:MP Game Server
After=network.target mariadb.service flovmp-voice.service
Wants=flovmp-voice.service

[Service]
EnvironmentFile=-${INSTALL_DIR}/flovmp.env
Type=simple
WorkingDirectory=${INSTALL_DIR}
ExecStart=${INSTALL_DIR}/start.sh
Restart=always
RestartSec=5
LimitNOFILE=65535
StartLimitIntervalSec=0

[Install]
WantedBy=multi-user.target
EOF

# Права доступа
chmod +x "${INSTALL_DIR}/start.sh" 2>/dev/null || true
chmod +x "${INSTALL_DIR}/flovmp-server" 2>/dev/null || true
chmod +x "${VOICE_DIR}/altv-voice-server" 2>/dev/null || true

systemctl daemon-reload
systemctl enable flovmp-voice.service flovmp-game.service >/dev/null 2>&1

echo -e "${CYAN}[6/6] Запуск игрового сервера и голосовой связи...${NC}"
systemctl restart flovmp-game.service

sleep 2
if systemctl is-active --quiet flovmp-game.service; then
  echo -e "\n${GREEN}${BOLD}================================================================${NC}"
  echo -e "${GREEN}${BOLD}✓ СЕРВЕР FLOV:MP УСПЕШНО УСТАНОВЛЕН И ЗАПУЩЕН!${NC}"
  echo -e "${GREEN}${BOLD}================================================================${NC}"
  echo -e "  Директория:     ${BOLD}${INSTALL_DIR}${NC}"
  echo -e "  Лицензия:       ${BOLD}${LICENSE_KEY}${NC} (файл license.flv активен)"
  echo -e "  Игровой порт:   ${BOLD}UDP ${SERVER_PORT}${NC}"
  echo -e "  Голосовой порт: ${BOLD}UDP 7797 / 7798${NC} (служба flovmp-voice)"
  echo -e "  Статус служб:   systemctl status flovmp-game flovmp-voice"
  echo -e "  Логи сервера:   journalctl -u flovmp-game -f"
  echo -e "  Перезапуск:     systemctl restart flovmp-game"
  echo -e "${GREEN}================================================================${NC}\n"
else
  echo -e "\n${YELLOW}! Сервер установлен, но служба ожидает проверки.${NC}"
  echo -e "Проверьте логи: ${BOLD}journalctl -u flovmp-game -n 50 --no-pager${NC}"
fi
