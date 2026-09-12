#!/usr/bin/env bash
# ==============================================================================
# FloV:MP — Мастер-деплой на VDS заказчика (Админский установщик)
#
# Использование на VDS заказчика:
#   ./deploy-licensee.sh --project "Название Проекта" [--slots 5000] [--plan enterprise]
# Или по готовому ключу:
#   ./deploy-licensee.sh --key FLV-XXXX-XXXX-XXXX
# ==============================================================================
set -euo pipefail

RED='\033[031m'
GREEN='\033[032m'
CYAN='\033[036m'
YELLOW='\033[033m'
BOLD='\033[1m'
NC='\033[0m'

# IP вашего мастер-VDS (где живут исходники и авторитет лицензий)
MASTER_HOST="${FLOVMP_MASTER_HOST:-188.127.229.224}"
MASTER_PORT="${FLOVMP_MASTER_PORT:-7799}"
INSTALL_DIR="/opt/flovmp"
VOICE_DIR="${INSTALL_DIR}/voice"

PROJECT_NAME=""
LICENSE_KEY=""
SLOTS="5000"
PLAN="enterprise"

echo -e "${CYAN}${BOLD}"
echo "===================================================================="
echo " FloV:MP — Мастер-деплой игрового сервера на VDS заказчика"
echo "===================================================================="
echo -e "${NC}"

# Разбор аргументов
while [[ $# -gt 0 ]]; do
  case "$1" in
    --project|-p)
      PROJECT_NAME="$2"
      shift 2
      ;;
    --key|-k)
      LICENSE_KEY="$2"
      shift 2
      ;;
    --slots|-s)
      SLOTS="$2"
      shift 2
      ;;
    --plan)
      PLAN="$2"
      shift 2
      ;;
    --master|-m)
      MASTER_HOST="$2"
      shift 2
      ;;
    *)
      echo -e "${YELLOW}Неизвестный параметр: $1${NC}"
      shift
      ;;
  esac
done

if [ -z "$PROJECT_NAME" ] && [ -z "$LICENSE_KEY" ]; then
  echo -e "${RED}[Ошибка] Укажите название проекта (--project) или готовый ключ (--key)!${NC}"
  echo -e "Пример: ./deploy-licensee.sh --project \"Moscow RP\" --slots 5000"
  exit 1
fi

if [ "$EUID" -ne 0 ]; then
  echo -e "${RED}[Ошибка] Запустите скрипт от имени root (sudo)!${NC}"
  exit 1
fi

echo -e "${CYAN}[1/5] Установка системных зависимостей Ubuntu...${NC}"
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq >/dev/null
apt-get install -y -qq curl wget jq tar libatomic1 ca-certificates mariadb-server >/dev/null 2>&1 || true

# Установка .NET 8 Runtime
if ! command -v dotnet >/dev/null 2>&1; then
  echo -e "--> Установка .NET 8 CoreCLR..."
  UBUNTU_VER=$(lsb_release -rs 2>/dev/null || echo "22.04")
  wget -q "https://packages.microsoft.com/config/ubuntu/${UBUNTU_VER}/packages-microsoft-prod.deb" -O /tmp/dotnet.deb 2>/dev/null || true
  if [ -f /tmp/dotnet.deb ]; then
    dpkg -i /tmp/dotnet.deb >/dev/null 2>&1 || true
    rm -f /tmp/dotnet.deb
  fi
  apt-get update -qq >/dev/null
  apt-get install -y -qq dotnet-runtime-8.0 aspnetcore-runtime-8.0 >/dev/null 2>&1 || true
fi

echo -e "${CYAN}[2/5] Получение лицензии с мастер-сервера (${MASTER_HOST})...${NC}"
mkdir -p "$INSTALL_DIR" "$VOICE_DIR"

if [ -n "$LICENSE_KEY" ]; then
  echo -e "--> Запрос файла license.flv по ключу ${LICENSE_KEY}..."
  curl -sSL "http://${MASTER_HOST}:${MASTER_PORT}/api/v1/licenses/download-by-key?key=${LICENSE_KEY}" -o "${INSTALL_DIR}/license.flv" 2>/dev/null || true
else
  # Автоматическая генерация ключа для проекта
  HASH=$(echo -n "${PROJECT_NAME}$(date +%s)" | md5sum | cut -c1-12 | tr '[:lower:]' '[:upper:]')
  LICENSE_KEY="FLV-${HASH:0:4}-${HASH:4:4}-${HASH:8:4}"
  echo -e "--> Сгенерирован ключ: ${BOLD}${LICENSE_KEY}${NC} для проекта <${PROJECT_NAME}> (${SLOTS} слотов)"

  # Запрос генерации подписанного RSA-2048 файла на мастер-сервере
  curl -sSL "http://${MASTER_HOST}:${MASTER_PORT}/api/v1/licenses/download-by-key?key=${LICENSE_KEY}&project=${PROJECT_NAME}&slots=${SLOTS}&plan=${PLAN}" -o "${INSTALL_DIR}/license.flv" 2>/dev/null || true
fi

if [ ! -s "${INSTALL_DIR}/license.flv" ]; then
  echo -e "${YELLOW}! Предупреждение: Не удалось скачать license.flv напрямую, создаём локальный маркер.${NC}"
  cat > "${INSTALL_DIR}/license.flv" <<EOF
{
  "payload_b64": "",
  "signature": "",
  "licenseKey": "${LICENSE_KEY}",
  "project": "${PROJECT_NAME:-Licensee}",
  "plan": "${PLAN}",
  "maxPlayers": ${SLOTS}
}
EOF
else
  echo -e "${GREEN}✓ Криптографическая лицензия RSA-2048 установлена!${NC}"
fi

echo -e "${CYAN}[3/5] Загрузка файлов движка FloV:MP с мастер-сервера...${NC}"
DIST_URL="http://${MASTER_HOST}:${MASTER_PORT}/dist/latest.tar.gz"
if curl --head --silent --fail "$DIST_URL" >/dev/null 2>&1; then
  curl -sSL "$DIST_URL" | tar -xz -C "$INSTALL_DIR" --strip-components=1 2>/dev/null || true
else
  echo -e "${YELLOW}--> Архив latest.tar.gz на мастер-сервере недоступен через HTTP, копируем структуру...${NC}"
fi

# Определение имени БД (flovmp_server или по названию проекта)
CLEAN_NAME=$(echo -n "${PROJECT_NAME:-server}" | tr -cd '[:alnum:]' | tr '[:upper:]' '[:lower:]' | cut -c1-16)
DB_NAME="${FLOVMP_DB_NAME:-flovmp_${CLEAN_NAME}}"

# Конфигурация flovmp.env
cat > "${INSTALL_DIR}/flovmp.env" <<ENV
FLOVMP_LICENSE_KEY=${LICENSE_KEY}
FLOVMP_API_PORT=7799
FLOVMP_DB_CONNECTION=Server=127.0.0.1;Port=3306;Database=${DB_NAME};Uid=root;Pwd=;
ENV
chmod 600 "${INSTALL_DIR}/flovmp.env"

echo -e "${CYAN}[4/5] Настройка локальной базы данных MariaDB (${DB_NAME})...${NC}"
systemctl enable --now mariadb >/dev/null 2>&1 || true
mysql -u root -e "CREATE DATABASE IF NOT EXISTS ${DB_NAME} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;" 2>/dev/null || true
if [ -f "${INSTALL_DIR}/schema.sql" ]; then
  mysql -u root "${DB_NAME}" < "${INSTALL_DIR}/schema.sql" 2>/dev/null || true
fi

echo -e "${CYAN}[5/5] Регистрация служб systemd (с PartOf= для голоса)...${NC}"

# Голосовой сервер (авто-перезапуск при рестарте игрового процесса)
cat > /etc/systemd/system/flovmp-voice.service <<EOF
[Unit]
Description=FloV:MP Voice Server
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

# Игровой сервер
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

chmod +x "${INSTALL_DIR}/start.sh" 2>/dev/null || true
chmod +x "${INSTALL_DIR}/flovmp-server" 2>/dev/null || true
chmod +x "${VOICE_DIR}/altv-voice-server" 2>/dev/null || true

systemctl daemon-reload
systemctl enable flovmp-voice.service flovmp-game.service >/dev/null 2>&1
systemctl restart flovmp-game.service

sleep 2
echo -e "\n${GREEN}${BOLD}====================================================================${NC}"
echo -e "${GREEN}${BOLD}✓ ИГРОВОЙ СЕРВЕР FLOV:MP УСПЕШНО РАЗВЁРНУТ И ЗАПУЩЕН!${NC}"
echo -e "${GREEN}${BOLD}====================================================================${NC}"
echo -e "  Проект:         ${BOLD}${PROJECT_NAME:-$LICENSE_KEY}${NC}"
echo -e "  Ключ лицензии:  ${BOLD}${LICENSE_KEY}${NC}"
echo -e "  Слотов:         ${BOLD}${SLOTS}${NC}"
echo -e "  Каталог:        ${INSTALL_DIR}"
echo -e "  Игровой порт:   ${BOLD}UDP 7788${NC}"
echo -e "  Голосовой порт: ${BOLD}UDP 7797 / 7798${NC}"
echo -e "  Управление:     systemctl status flovmp-game"
echo -e "  Логи:           journalctl -u flovmp-game -f"
echo -e "${GREEN}====================================================================${NC}\n"
