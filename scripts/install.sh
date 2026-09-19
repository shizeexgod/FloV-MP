#!/usr/bin/env bash
# =====================================================================
#  FloV:MP — установщик игрового сервера (Linux: Ubuntu / Debian)
#
#  Из распакованного пакета (рекомендуется):
#     tar -xzf flovmp-server-<версия>-linux.tar.gz
#     cd flovmp-server-<версия>
#     sudo ./install.sh --public-host <ваш IP> --owner-sc <ваш SocialClubId>
#
#  Одной командой (скачает пакет с портала):
#     curl -fsSL https://<портал>/install.sh | sudo bash -s -- --owner-sc <SocialClubId>
#
#  Повторный запуск из пакета новой версии = обновление: файлы платформы
#  заменяются (с резервной копией и откатом при неудачном старте), а ваши
#  файлы — server.toml, config/flovmp.env, права, данные, свои ресурсы —
#  не трогаются никогда.
#
#  Полный список параметров: ./install.sh --help
# =====================================================================
set -euo pipefail
shopt -u patsub_replacement 2>/dev/null || true

ORIG_ARGS=("$@")

# ---------------------------------------------------------------------
# Вывод
# ---------------------------------------------------------------------
if [ -t 1 ]; then
  C_RED=$'\033[31m'; C_GREEN=$'\033[32m'; C_YELLOW=$'\033[33m'; C_CYAN=$'\033[36m'; C_BOLD=$'\033[1m'; C_OFF=$'\033[0m'
else
  C_RED=""; C_GREEN=""; C_YELLOW=""; C_CYAN=""; C_BOLD=""; C_OFF=""
fi
step() { echo; echo "${C_CYAN}${C_BOLD}==> $*${C_OFF}"; }
ok()   { echo "${C_GREEN}  ✓ $*${C_OFF}"; }
info() { echo "    $*"; }
warn() { echo "${C_YELLOW}  ! $*${C_OFF}"; }
die()  { echo; echo "${C_RED}${C_BOLD}ОШИБКА: $*${C_OFF}" >&2; exit 1; }

validate_manifest_file() {
  local file="$1" line digest rel part
  [ -f "$file" ] || die "не найден manifest.txt: $file"
  declare -A seen=()
  while IFS= read -r line || [ -n "$line" ]; do
    [ -z "$line" ] && continue
    if [[ "$line" =~ ^([0-9a-fA-F]{64})[[:space:]]{2,}(.+)$ ]]; then
      digest="${BASH_REMATCH[1]}"; rel="${BASH_REMATCH[2]}"
    else
      die "неверная строка manifest.txt: $line"
    fi
    [[ "$rel" != *" "* ]] || die "пробел в пути manifest.txt запрещён: $rel"
    [[ "$rel" != /* && "$rel" != *:* && "$rel" != -* ]] || die "абсолютный/ADS/option-путь manifest.txt запрещён: $rel"
    IFS='/' read -r -a parts <<< "$rel"
    for part in "${parts[@]}"; do
      [[ -n "$part" && "$part" != "." && "$part" != ".." ]] || die "небезопасный путь manifest.txt: $rel"
    done
    [ -z "${seen[$rel]+x}" ] || die "повторный путь manifest.txt: $rel"
    seen["$rel"]="$digest"
  done < "$file"
  [ "${#seen[@]}" -gt 0 ] || die "manifest.txt пуст: $file"
}

usage() {
  cat <<'USAGE'
FloV:MP — установщик игрового сервера

Использование: sudo ./install.sh [параметры]

Основное:
  --dir <путь>              Папка установки (по умолчанию /opt/flovmp)
  --name "<название>"       Название сервера (только при первой установке)
  --slots <число>           Максимум игроков (по умолчанию 1000)
  --port <порт>             Игровой порт, UDP+TCP (по умолчанию 7788)
  --public-host <IP|домен>  Внешний адрес для голосового чата (определяется сам)
  --voice-port <порт>       Публичный порт голоса, UDP+TCP (по умолчанию 7895)
  --voice-internal-port <п> Внутренний порт голоса (по умолчанию 7896)

Администратор:
  --owner-sc <SocialClubId> Выдать уровень 8 (основатель) этому SocialClubId

База данных (MariaDB ставится и настраивается автоматически):
  --db-name <имя>           Имя базы (по умолчанию flovmp_server)
  --db-user <имя>           Пользователь базы (по умолчанию flovmp)
  --no-db                   Без базы: права, баны и аккаунты в файлах

Службы:
  --service <имя>           Имя службы systemd (по умолчанию flovmp)
  --run-as <пользователь>   От чьего имени работает сервер (по умолчанию flovmp)
  --no-start                Не запускать сервер после установки
  --no-firewall             Не открывать порты в ufw/firewalld

Лицензия и пакет:
  --key <ключ>              Ключ лицензии (сохраняется в config/flovmp.env)
  --portal <URL>            Адрес портала (для скачивания пакета/лицензии)
  --package <файл.tar.gz>   Установить из указанного архива
  --package-url <URL>       Скачать архив по ссылке
  --sha256 <хэш>            Проверить хэш скачанного архива

Прочее:
  --uninstall               Удалить службы (файлы и база остаются)
  --purge                   Вместе с --uninstall: удалить также файлы и базу
  --force                   Установить поверх папки, не похожей на установку FloV:MP
  -y, --yes                 Не задавать вопросов (нужно для --purge)
  -h, --help                Эта справка
USAGE
}

# ---------------------------------------------------------------------
# Параметры
# ---------------------------------------------------------------------
INSTALL_DIR="/opt/flovmp"
SERVER_NAME="FloV:MP Server"
SLOTS="1000"
# Потоки отправки и приёма синхронизации: на одном потоке сервер упирается в
# несколько сотен игроков раньше, чем в процессор. Берём половину ядер (1..4):
# оставить ядра под стример и голосовой сервер важнее, чем занять все.
CORES="$(nproc 2>/dev/null || echo 2)"
SYNC_SEND="$(( CORES / 2 ))"; [ "$SYNC_SEND" -lt 1 ] && SYNC_SEND=1; [ "$SYNC_SEND" -gt 4 ] && SYNC_SEND=4
SYNC_RECEIVE="$SYNC_SEND"

GAME_PORT="7788"
VOICE_PUBLIC_PORT="7895"
VOICE_INTERNAL_PORT="7896"
PUBLIC_HOST=""
OWNER_SC=""
DB_NAME="flovmp_server"
DB_USER="flovmp"
USE_DB=1
SERVICE="flovmp"
RUN_AS="flovmp"
DO_START=1
DO_FIREWALL=1
LICENSE_KEY=""
PORTAL_URL="${FLOVMP_PORTAL_URL:-https://flov-mp.ru}"
PACKAGE_FILE=""
PACKAGE_URL=""
PACKAGE_SHA256=""
UNINSTALL=0
PURGE=0
FORCE=0
ASSUME_YES=0

need_value() { [ $# -ge 2 ] && [ -n "$2" ] || die "параметру $1 нужно значение (см. --help)"; }

while [ $# -gt 0 ]; do
  case "$1" in
    --dir)                 need_value "$@"; INSTALL_DIR="$2"; shift 2 ;;
    --name)                need_value "$@"; SERVER_NAME="$2"; shift 2 ;;
    --slots)               need_value "$@"; SLOTS="$2"; shift 2 ;;
    --port)                need_value "$@"; GAME_PORT="$2"; shift 2 ;;
    --public-host)         need_value "$@"; PUBLIC_HOST="$2"; shift 2 ;;
    --voice-port)          need_value "$@"; VOICE_PUBLIC_PORT="$2"; shift 2 ;;
    --voice-internal-port) need_value "$@"; VOICE_INTERNAL_PORT="$2"; shift 2 ;;
    --owner-sc)            need_value "$@"; OWNER_SC="$2"; shift 2 ;;
    --db-name)             need_value "$@"; DB_NAME="$2"; shift 2 ;;
    --db-user)             need_value "$@"; DB_USER="$2"; shift 2 ;;
    --no-db)               USE_DB=0; shift ;;
    --service)             need_value "$@"; SERVICE="$2"; shift 2 ;;
    --run-as)              need_value "$@"; RUN_AS="$2"; shift 2 ;;
    --no-start)            DO_START=0; shift ;;
    --no-firewall)         DO_FIREWALL=0; shift ;;
    --key|-k)              need_value "$@"; LICENSE_KEY="$2"; shift 2 ;;
    --portal|-p)           need_value "$@"; PORTAL_URL="${2%/}"; shift 2 ;;
    --package)             need_value "$@"; PACKAGE_FILE="$2"; shift 2 ;;
    --package-url)         need_value "$@"; PACKAGE_URL="$2"; shift 2 ;;
    --sha256)              need_value "$@"; PACKAGE_SHA256="$2"; shift 2 ;;
    --uninstall)           UNINSTALL=1; shift ;;
    --purge)               PURGE=1; shift ;;
    --force)               FORCE=1; shift ;;
    -y|--yes)              ASSUME_YES=1; shift ;;
    -h|--help)             usage; exit 0 ;;
    *)                     die "неизвестный параметр: $1 (см. --help)" ;;
  esac
done

is_port() { [[ "$1" =~ ^[0-9]+$ ]] && [ "$1" -ge 1024 ] && [ "$1" -le 65535 ]; }
is_port "$GAME_PORT"           || die "--port: нужен порт 1024..65535, получено «$GAME_PORT»"
is_port "$VOICE_PUBLIC_PORT"   || die "--voice-port: нужен порт 1024..65535, получено «$VOICE_PUBLIC_PORT»"
is_port "$VOICE_INTERNAL_PORT" || die "--voice-internal-port: нужен порт 1024..65535, получено «$VOICE_INTERNAL_PORT»"
[ "$GAME_PORT" != "$VOICE_PUBLIC_PORT" ] && [ "$GAME_PORT" != "$VOICE_INTERNAL_PORT" ] && \
  [ "$VOICE_PUBLIC_PORT" != "$VOICE_INTERNAL_PORT" ] || die "игровой и голосовые порты должны различаться"
[[ "$SLOTS" =~ ^[0-9]+$ ]] && [ "$SLOTS" -ge 1 ] && [ "$SLOTS" -le 4096 ] || die "--slots: нужно число 1..4096"
[ -z "$OWNER_SC" ] || [[ "$OWNER_SC" =~ ^[0-9]{1,20}$ ]] || \
  die "--owner-sc: SocialClubId — только цифры (ник не подходит: его подделывает клиент)"
[[ "$DB_NAME" =~ ^[A-Za-z0-9_]{1,48}$ ]] || die "--db-name: только латиница, цифры и _"
[[ "$DB_USER" =~ ^[A-Za-z0-9_]{1,32}$ ]] || die "--db-user: только латиница, цифры и _"
[[ "$SERVICE" =~ ^[A-Za-z0-9_-]{1,40}$ ]] || die "--service: только латиница, цифры, _ и -"
[[ "$RUN_AS" =~ ^[a-z_][a-z0-9_-]{0,31}$ ]] || die "--run-as: недопустимое имя пользователя"
[ -z "$PUBLIC_HOST" ] || [[ "$PUBLIC_HOST" =~ ^[A-Za-z0-9.:-]{1,253}$ ]] || die "--public-host: недопустимый адрес"
[[ "$INSTALL_DIR" == /* ]] || die "--dir: нужен абсолютный путь"
INSTALL_DIR="${INSTALL_DIR%/}"
case "$INSTALL_DIR" in
  ""|/|/bin|/boot|/dev|/etc|/home|/lib|/lib64|/proc|/root|/run|/sbin|/sys|/tmp|/usr|/var)
    die "--dir: нельзя устанавливать в $INSTALL_DIR" ;;
esac
# Название уходит в TOML-строку: кавычки, обратные слеши и управляющие символы убираем.
SERVER_NAME="$(printf '%s' "$SERVER_NAME" | tr -d '"\\' | tr -d '[:cntrl:]' | cut -c1-64)"
[ -n "$SERVER_NAME" ] || SERVER_NAME="FloV:MP Server"

[ "$(id -u)" -eq 0 ] || die "запустите установщик от root: sudo ./install.sh"

# Службы: настоящий systemd или совместимая замена systemctl, которую ставят
# некоторые хостинги в контейнерных VDS (там нет /run/systemd/system и части
# команд, например daemon-reload). Поэтому признак — отвечает ли systemctl.
HAS_SYSTEMD=0
if command -v systemctl >/dev/null 2>&1 &&    { [ -d /run/systemd/system ] || systemctl list-units >/dev/null 2>&1; }; then
  HAS_SYSTEMD=1
fi
reload_units() { systemctl daemon-reload >/dev/null 2>&1 || true; }

# ---------------------------------------------------------------------
# Удаление
# ---------------------------------------------------------------------
if [ "$UNINSTALL" -eq 1 ]; then
  # Проверки ДО первого действия. Раньше службы успевали удалиться, а затем
  # установщик отказывался от --purge без --yes: сервер уже остановлен и
  # снят с автозапуска, хотя пользователя об этом не спрашивали.
  if [ "$PURGE" -eq 1 ]; then
    [ "$ASSUME_YES" -eq 1 ] || die "--purge удаляет файлы и базу безвозвратно; повторите с --yes"
    [ -f "$INSTALL_DIR/manifest.txt" ] || die "$INSTALL_DIR не похож на установку FloV:MP — ничего не удалено"
  fi
  step "Удаление FloV:MP ($SERVICE)"
  if [ "$HAS_SYSTEMD" -eq 1 ]; then
    systemctl stop "$SERVICE.service" "$SERVICE-voice.service" >/dev/null 2>&1 || true
    systemctl disable "$SERVICE.service" "$SERVICE-voice.service" >/dev/null 2>&1 || true
    systemctl disable --now "$SERVICE-backup.timer" >/dev/null 2>&1 || true
    rm -f "/etc/systemd/system/$SERVICE.service" "/etc/systemd/system/$SERVICE-voice.service" \
          "/etc/systemd/system/$SERVICE-backup.service" "/etc/systemd/system/$SERVICE-backup.timer"
    reload_units
    ok "службы $SERVICE и $SERVICE-voice удалены"
  fi

  # Снятие службы не всегда убивает процессы: на части контейнерных VDS
  # systemctl — совместимая замена, которая не трогает потомков. Тогда после
  # удаления игровой и голосовой серверы продолжают работать и держат порты,
  # а новая установка на те же порты падает с «порт занят».
  # Добиваем только то, что запущено ИЗ ЭТОЙ папки.
  # Совпадение по исполняемому файлу И по аргументам: обработчик сбоев движка
  # запускается системным бинарником, но держит папку в аргументах — и, что
  # важнее, наследует сетевые сокеты. Пока жив он, порт остаётся занятым, хотя
  # сам сервер уже остановлен.
  belongs_to_dir() {
    local pid="$1" dir="$2" exe args
    exe="$(readlink -f "/proc/$pid/exe" 2>/dev/null || true)"
    case "$exe" in "$dir"/*) return 0 ;; esac
    # tr -d, а не замена на пробел: подстановка команды в bash ругается на
    # нулевые байты, а для поиска подстроки с путём склейка аргументов не мешает.
    args="$(tr -d '\000' < "/proc/$pid/cmdline" 2>/dev/null || true)"
    case "$args" in *"$dir/"*) return 0 ;; esac
    return 1
  }

  stop_leftovers() {
    local dir="$1" pid found=0 signal
    for signal in TERM KILL; do
      for pid in $(ls /proc 2>/dev/null | grep -E '^[0-9]+$'); do
        [ "$pid" = "$$" ] && continue
        belongs_to_dir "$pid" "$dir" || continue
        kill "-$signal" "$pid" 2>/dev/null && found=1
      done
      [ "$found" -eq 1 ] || return 0
      sleep 2
    done
    ok "остановлены процессы из $dir (служба их не сняла)"
  }
  stop_leftovers "$INSTALL_DIR"
  if [ "$PURGE" -eq 1 ]; then
    # Удаляется только база, которую завёл установщик ЭТОЙ установки
    # (отметка config/.db-created). Всё остальное — чужое: сервер мог быть
    # подключён к общей базе проекта или к базе соседнего инстанса.
    ENV_DB_NAME=""; ENV_DB_USER=""
    if [ -f "$INSTALL_DIR/config/.db-created" ]; then
      ENV_DB_NAME="$(sed -n '1p' "$INSTALL_DIR/config/.db-created")"
      ENV_DB_USER="$(sed -n '2p' "$INSTALL_DIR/config/.db-created")"
    fi
    # Перед удалением — архив всего, что принадлежит владельцу: код своего
    # сервера (gamemode), настройки, лицензия, данные и дамп базы. Потерять
    # их одной командой слишком дорого.
    SAVE_DIR="$(mktemp -d)"
    SAVE_ARCHIVE="$(dirname "$INSTALL_DIR")/flovmp-removed-$SERVICE-$(date +%Y%m%d-%H%M%S).tar.gz"
    for keep in gamemode config license.flv server/server.toml voice/voice.toml server/config server/flovmp-data server/resources/gamemode; do
      [ -e "$INSTALL_DIR/$keep" ] && mkdir -p "$SAVE_DIR/$(dirname "$keep")" && cp -a "$INSTALL_DIR/$keep" "$SAVE_DIR/$keep"
    done
    find "$INSTALL_DIR/sql/migrations" -maxdepth 1 -name '[1-9][0-9][0-9]_*.sql' -exec sh -c 'mkdir -p "$1/sql/migrations" && cp -a "$2" "$1/sql/migrations/"' _ "$SAVE_DIR" {} \; 2>/dev/null || true
    if [ -n "$ENV_DB_NAME" ] && command -v mysqldump >/dev/null 2>&1 && [[ "$ENV_DB_NAME" =~ ^[A-Za-z0-9_]+$ ]]; then
      mysqldump -u root --single-transaction --routines --triggers "$ENV_DB_NAME" 2>/dev/null | gzip > "$SAVE_DIR/database-$ENV_DB_NAME.sql.gz" || true
    fi
    if tar -czf "$SAVE_ARCHIVE" -C "$SAVE_DIR" . 2>/dev/null; then
      chmod 600 "$SAVE_ARCHIVE"
      ok "копия ваших файлов и базы: $SAVE_ARCHIVE"
    else
      rm -rf "$SAVE_DIR"
      die "не удалось сохранить копию ваших файлов перед удалением — ничего не удалено"
    fi
    rm -rf "$SAVE_DIR"

    if [ -z "$ENV_DB_NAME" ]; then
      info "база данных не тронута: её заводил не этот установщик (нет config/.db-created)"
    elif command -v mysql >/dev/null 2>&1 && [[ "$ENV_DB_NAME" =~ ^[A-Za-z0-9_]+$ ]] && [[ "$ENV_DB_USER" =~ ^[A-Za-z0-9_]+$ ]]; then
      mysql -u root <<SQL >/dev/null 2>&1 && ok "база $ENV_DB_NAME и пользователь $ENV_DB_USER удалены" || warn "базу удалить не удалось — удалите вручную"
DROP DATABASE IF EXISTS \`$ENV_DB_NAME\`;
DROP USER IF EXISTS '$ENV_DB_USER'@'localhost';
DROP USER IF EXISTS '$ENV_DB_USER'@'127.0.0.1';
SQL
    fi
    rm -rf "$INSTALL_DIR"
    ok "папка $INSTALL_DIR удалена"
  else
    info "Файлы в $INSTALL_DIR и база данных сохранены (полное удаление: --uninstall --purge --yes)."
  fi
  exit 0
fi

# ---------------------------------------------------------------------
# Где пакет
# ---------------------------------------------------------------------
SCRIPT_PATH="${BASH_SOURCE[0]:-}"
SRC_DIR=""
if [ -n "$SCRIPT_PATH" ] && [ -f "$SCRIPT_PATH" ]; then
  CANDIDATE="$(cd "$(dirname "$(readlink -f "$SCRIPT_PATH")")" && pwd)"
  if [ -f "$CANDIDATE/manifest.txt" ] && [ -f "$CANDIDATE/server/flovmp-server" ]; then
    SRC_DIR="$CANDIDATE"
  fi
fi

if [ -z "$SRC_DIR" ] || [ -n "$PACKAGE_FILE" ] || [ -n "$PACKAGE_URL" ]; then
  # Режим «одной командой»: скачать пакет, распаковать и запустить его
  # собственный install.sh. Установщик и пакет всегда одной версии.
  step "Получение пакета FloV:MP"
  command -v tar >/dev/null 2>&1 || die "нужен tar (apt-get install tar)"
  WORK="$(mktemp -d /tmp/flovmp-install.XXXXXX)"
  trap 'rm -rf "$WORK"' EXIT
  if [ -n "$PACKAGE_FILE" ]; then
    [ -f "$PACKAGE_FILE" ] || die "архив не найден: $PACKAGE_FILE"
    cp "$PACKAGE_FILE" "$WORK/package.tar.gz"
  else
    command -v curl >/dev/null 2>&1 || { apt-get update -qq && apt-get install -y -qq curl ca-certificates >/dev/null; } || die "нужен curl"
    URL="${PACKAGE_URL:-$PORTAL_URL/api/v1/distribution/download-latest?os=linux}"
    info "Скачивание: $URL"
    curl -fL --retry 3 --connect-timeout 15 -o "$WORK/package.tar.gz" "$URL" || \
      die "не удалось скачать пакет. Скачайте архив сервера из личного кабинета, распакуйте и запустите ./install.sh из распакованной папки"
  fi
  if [ -n "$PACKAGE_SHA256" ]; then
    GOT="$(sha256sum "$WORK/package.tar.gz" | cut -d' ' -f1)"
    [ "$GOT" = "${PACKAGE_SHA256,,}" ] || die "хэш архива не совпал (ожидали $PACKAGE_SHA256, получили $GOT) — архив повреждён или подменён"
    ok "хэш архива совпал"
  fi
  tar -xzf "$WORK/package.tar.gz" -C "$WORK" || die "архив повреждён — не распаковывается"
  INNER="$(find "$WORK" -maxdepth 2 -name manifest.txt -printf '%h\n' | head -1)"
  [ -n "$INNER" ] && [ -f "$INNER/install.sh" ] || die "в архиве нет пакета сервера FloV:MP (manifest.txt/install.sh)"
  validate_manifest_file "$INNER/manifest.txt"
  # Без --package/--package-url, иначе вложенный установщик снова уйдёт качать.
  PASS_ARGS=()
  skip=0
  for a in "${ORIG_ARGS[@]}"; do
    if [ "$skip" -eq 1 ]; then skip=0; continue; fi
    case "$a" in --package|--package-url|--sha256) skip=1; continue ;; esac
    PASS_ARGS+=("$a")
  done
  bash "$INNER/install.sh" ${PASS_ARGS[@]+"${PASS_ARGS[@]}"}
  exit $?
fi

PKG_VERSION="$(cat "$SRC_DIR/VERSION" 2>/dev/null || echo unknown)"
echo "${C_CYAN}${C_BOLD}"
echo "  FloV:MP — установка игрового сервера, версия $PKG_VERSION"
echo "${C_OFF}"

# ---------------------------------------------------------------------
# 1. Целостность пакета
# ---------------------------------------------------------------------
step "1/8 Проверка целостности пакета"
command -v sha256sum >/dev/null 2>&1 || die "нужен sha256sum (coreutils)"
validate_manifest_file "$SRC_DIR/manifest.txt"
if ! (cd "$SRC_DIR" && sha256sum --quiet -c manifest.txt); then
  die "файлы пакета повреждены или неполные (см. список выше). Распакуйте архив заново"
fi
ok "все файлы платформы на месте ($(wc -l < "$SRC_DIR/manifest.txt") шт.)"

# ---------------------------------------------------------------------
# 2. Режим: новая установка или обновление
# ---------------------------------------------------------------------
UPGRADE=0
OLD_VERSION=""
SAME_DIR=0
[ "$(readlink -f "$SRC_DIR")" = "$(readlink -f "$INSTALL_DIR" 2>/dev/null || echo "$INSTALL_DIR")" ] && SAME_DIR=1

if [ -f "$INSTALL_DIR/manifest.txt" ] && [ "$SAME_DIR" -eq 0 ]; then
  UPGRADE=1
  OLD_VERSION="$(cat "$INSTALL_DIR/VERSION" 2>/dev/null || echo unknown)"
elif [ -d "$INSTALL_DIR" ] && [ "$SAME_DIR" -eq 0 ] && [ -n "$(ls -A "$INSTALL_DIR" 2>/dev/null)" ] && [ "$FORCE" -eq 0 ]; then
  die "папка $INSTALL_DIR не пуста и не похожа на установку этого пакета (нет manifest.txt).
       Это может быть сервер, установленный вручную. Чтобы ничего не сломать, установка остановлена.
       Выберите другую папку (--dir) или подтвердите установку поверх (--force)"
fi

if [ "$UPGRADE" -eq 1 ]; then
  step "2/8 Обновление $OLD_VERSION → $PKG_VERSION"
else
  step "2/8 Новая установка в $INSTALL_DIR"
  # Порты новой установки не должны быть заняты (например, другим сервером на
  # этой машине): иначе сервер не стартует, и ошибка видна только в логе.
  if command -v ss >/dev/null 2>&1; then
    for p in "$GAME_PORT" "$VOICE_PUBLIC_PORT" "$VOICE_INTERNAL_PORT"; do
      if ss -H -lnu "sport = :$p" 2>/dev/null | grep -q . || ss -H -lnt "sport = :$p" 2>/dev/null | grep -q .; then
        die "порт $p уже занят другой программой (возможно, другим сервером). Укажите свободные: --port, --voice-port, --voice-internal-port"
      fi
    done
    ok "порты $GAME_PORT, $VOICE_PUBLIC_PORT, $VOICE_INTERNAL_PORT свободны"
  fi
fi

# ---------------------------------------------------------------------
# 3. Системные зависимости
# ---------------------------------------------------------------------
step "3/8 Системные зависимости"
if command -v apt-get >/dev/null 2>&1; then
  export DEBIAN_FRONTEND=noninteractive
  PKGS=(ca-certificates curl tar gzip libatomic1 coreutils)
  ICU="$(apt-cache pkgnames 2>/dev/null | grep -E '^libicu[0-9]+$' | sort -V | tail -1 || true)"
  [ -n "$ICU" ] && PKGS+=("$ICU")
  [ "$USE_DB" -eq 1 ] && PKGS+=(mariadb-server mariadb-client)
  apt-get update -qq >/dev/null 2>&1 || warn "apt-get update завершился с ошибкой — пробую ставить из кэша"
  if apt-get install -y -qq "${PKGS[@]}" >/dev/null 2>&1; then
    ok "пакеты: ${PKGS[*]}"
  else
    die "не удалось установить пакеты: ${PKGS[*]}. Проверьте apt и повторите"
  fi
else
  warn "не Debian/Ubuntu: установите вручную libatomic, libicu, curl$([ "$USE_DB" -eq 1 ] && echo ", MariaDB")"
fi

find_dotnet() {
  local d
  for d in "${DOTNET_ROOT:-}" /usr/share/dotnet /usr/lib/dotnet /opt/dotnet /usr/local/share/dotnet; do
    [ -n "$d" ] || continue
    ls -d "$d"/shared/Microsoft.NETCore.App/8.* >/dev/null 2>&1 && { echo "$d"; return 0; }
  done
  return 1
}
if DOTNET_DIR="$(find_dotnet)"; then
  ok ".NET 8 Runtime: $DOTNET_DIR"
else
  info "Установка .NET 8 Runtime (официальный скрипт Microsoft)..."
  curl -fsSL --retry 3 https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh || die "не удалось скачать dotnet-install.sh"
  bash /tmp/dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir /usr/share/dotnet >/dev/null || die "не удалось установить .NET 8"
  rm -f /tmp/dotnet-install.sh
  ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet 2>/dev/null || true
  DOTNET_DIR="$(find_dotnet)" || die ".NET 8 установлен, но не найден — проверьте /usr/share/dotnet"
  ok ".NET 8 Runtime: $DOTNET_DIR"
fi

# ---------------------------------------------------------------------
# 4. Файлы платформы
# ---------------------------------------------------------------------
step "4/8 Файлы платформы"
BACKUP_ARCHIVE=""
if [ "$HAS_SYSTEMD" -eq 1 ] && [ "$UPGRADE" -eq 1 ]; then
  systemctl stop "$SERVICE.service" >/dev/null 2>&1 || true
fi

mkdir -p "$INSTALL_DIR"
if [ "$SAME_DIR" -eq 0 ]; then
  NEW_LIST="$(mktemp)"; OLD_LIST="$(mktemp)"
  cut -d' ' -f3- "$SRC_DIR/manifest.txt" | sort > "$NEW_LIST"
  restore_platform() {
    # Called only after a failed replacement. Restore the old platform without
    # touching user-owned config, gamemode, data, or resources.
    [ "$UPGRADE" -eq 1 ] && [ -n "$BACKUP_ARCHIVE" ] || return 0
    if [ -f "$INSTALL_DIR/manifest.txt" ]; then
      while IFS= read -r f; do
        [ -n "$f" ] && rm -f -- "$INSTALL_DIR/$f"
      done < "$NEW_LIST"
    fi
    tar -xzpf "$BACKUP_ARCHIVE" -C "$INSTALL_DIR" || die "не удалось восстановить резервную копию платформы"
  }
  if [ "$UPGRADE" -eq 1 ]; then
    validate_manifest_file "$INSTALL_DIR/manifest.txt"
    cut -d' ' -f3- "$INSTALL_DIR/manifest.txt" | sort > "$OLD_LIST"
    mkdir -p "$INSTALL_DIR/backups"
    chmod 700 "$INSTALL_DIR/backups"
    BACKUP_ARCHIVE="$INSTALL_DIR/backups/platform-${OLD_VERSION}-$(date +%Y%m%d-%H%M%S).tar.gz"
    EXISTING="$(mktemp)"
    (cd "$INSTALL_DIR" && while IFS= read -r f; do [ -e "$f" ] && printf '%s\n' "$f"; done < "$OLD_LIST") > "$EXISTING"
    [ -f "$INSTALL_DIR/manifest.json" ] && echo manifest.json >> "$EXISTING"
    tar -czf "$BACKUP_ARCHIVE" -C "$INSTALL_DIR" -T "$EXISTING" manifest.txt || die "не удалось сделать резервную копию — обновление отменено, ничего не изменено"
    rm -f "$EXISTING"
    ok "резервная копия прежней версии: $BACKUP_ARCHIVE"
    # Файлы, которые были в прежней версии платформы и исчезли в новой.
    comm -23 "$OLD_LIST" "$NEW_LIST" | while IFS= read -r f; do
      [ -n "$f" ] && rm -f "$INSTALL_DIR/$f"
    done
  fi
  if ! (cd "$SRC_DIR" && tar --verbatim-files-from -cf - -T "$NEW_LIST" manifest.txt manifest.json) | tar -xpf - -C "$INSTALL_DIR"; then
    restore_platform
    die "не удалось скопировать файлы в $INSTALL_DIR — прежняя версия восстановлена"
  fi
  rm -f "$NEW_LIST" "$OLD_LIST"
fi
if ! (cd "$INSTALL_DIR" && sha256sum --quiet -c manifest.txt); then
  if [ "$SAME_DIR" -eq 0 ] && [ "$UPGRADE" -eq 1 ] && [ -n "$BACKUP_ARCHIVE" ]; then
    # NEW_LIST is already removed after a successful copy, so derive the list
    # from the current manifest before restoring the saved old manifest.
    FAILED_LIST="$(mktemp)"
    cut -d' ' -f3- "$INSTALL_DIR/manifest.txt" | sort > "$FAILED_LIST"
    while IFS= read -r f; do [ -n "$f" ] && rm -f -- "$INSTALL_DIR/$f"; done < "$FAILED_LIST"
    rm -f "$FAILED_LIST"
    tar -xzpf "$BACKUP_ARCHIVE" -C "$INSTALL_DIR" || die "не удалось восстановить резервную копию платформы"
    die "после копирования файлы не совпали — прежняя версия восстановлена"
  fi
  die "после копирования файлы не совпали с пакетом — проверьте место на диске"
fi
chmod +x "$INSTALL_DIR/install.sh" "$INSTALL_DIR/start.sh" "$INSTALL_DIR/start-voice.sh" \
         "$INSTALL_DIR/server/flovmp-server" "$INSTALL_DIR/server/flovmp-crash-handler" \
         "$INSTALL_DIR/voice/altv-voice-server" "$INSTALL_DIR"/scripts/*.sh 2>/dev/null || true
[ ! -f "$INSTALL_DIR/voice/altv-crash-handler" ] || chmod +x "$INSTALL_DIR/voice/altv-crash-handler"
ok "платформа $PKG_VERSION в $INSTALL_DIR"

# ---------------------------------------------------------------------
# 5. Ваши настройки (создаются один раз, дальше не трогаются)
# ---------------------------------------------------------------------
step "5/8 Настройки сервера"
. "$INSTALL_DIR/scripts/lib-env.sh"
ENV_FILE="$INSTALL_DIR/config/flovmp.env"
mkdir -p "$INSTALL_DIR/config" "$INSTALL_DIR/server/config" "$INSTALL_DIR/server/flovmp-data"

set_env_var() {
  # Заменить КЛЮЧ=... или дописать в конец. Значение без перевода строки.
  local key="$1" val="$2" tmp
  tmp="$(mktemp)"
  if [ -f "$ENV_FILE" ] && grep -q "^${key}=" "$ENV_FILE"; then
    awk -v k="$key" -v v="$val" 'BEGIN{FS=OFS="="} $1==k {print k "=" v; next} {print}' "$ENV_FILE" > "$tmp"
  else
    { [ -f "$ENV_FILE" ] && cat "$ENV_FILE"; printf '%s=%s\n' "$key" "$val"; } > "$tmp"
  fi
  cat "$tmp" > "$ENV_FILE"
  rm -f "$tmp"
}
random_hex() { od -An -N"$1" -tx1 /dev/urandom | tr -d ' \n'; }

if [ ! -f "$ENV_FILE" ]; then
  cp "$INSTALL_DIR/config/flovmp.env.example" "$ENV_FILE"
  chmod 600 "$ENV_FILE"
  set_env_var FLOVMP_DB_NAME "$DB_NAME"
  set_env_var FLOVMP_DB_USER "$DB_USER"
  if [ "$USE_DB" -eq 1 ]; then
    set_env_var FLOVMP_DB_PASSWORD "$(random_hex 24)"
  fi
  SETUP_TOKEN="FLV-$(random_hex 8 | tr 'a-f' 'A-F' | sed 's/.\{4\}/&-/g; s/-$//')"
  set_env_var FLOVMP_SETUP_TOKEN "$SETUP_TOKEN"
  ok "создан config/flovmp.env (пароль базы сгенерирован)"
else
  ok "config/flovmp.env уже есть — сохранён"
fi
chmod 600 "$ENV_FILE"
[ -z "$OWNER_SC" ]    || { set_env_var FLOVMP_OWNER_SC "$OWNER_SC"; ok "основатель (уровень 8): SocialClubId $OWNER_SC"; }
[ -z "$LICENSE_KEY" ] || set_env_var FLOVMP_LICENSE_KEY "$LICENSE_KEY"
if [ "$USE_DB" -eq 0 ]; then
  set_env_var FLOVMP_DB_PASSWORD ""
fi

# Значения из файла — источник правды при повторном запуске.
( flovmp_load_env "$ENV_FILE"; printf '%s\n%s\n%s\n%s\n' "${FLOVMP_DB_NAME:-$DB_NAME}" "${FLOVMP_DB_USER:-$DB_USER}" "${FLOVMP_DB_PASSWORD:-}" "${FLOVMP_SETUP_TOKEN:-}" ) > "$INSTALL_DIR/.install-env.tmp"
{ read -r DB_NAME; read -r DB_USER; read -r DB_PASSWORD; read -r SETUP_TOKEN; } < "$INSTALL_DIR/.install-env.tmp"
rm -f "$INSTALL_DIR/.install-env.tmp"

if [ -n "$LICENSE_KEY" ] && [ ! -s "$INSTALL_DIR/license.flv" ]; then
  if curl -fsS --max-time 20 -o "$INSTALL_DIR/license.flv.tmp" \
       "$PORTAL_URL/api/v1/licenses/download-by-key?key=$LICENSE_KEY" 2>/dev/null && [ -s "$INSTALL_DIR/license.flv.tmp" ]; then
    mv "$INSTALL_DIR/license.flv.tmp" "$INSTALL_DIR/license.flv"
    ok "файл лицензии получен с портала"
  else
    rm -f "$INSTALL_DIR/license.flv.tmp"
    warn "файл лицензии с портала получить не удалось — ключ сохранён, работе сервера это не мешает"
  fi
fi

if [ -z "$PUBLIC_HOST" ]; then
  PUBLIC_HOST="$(curl -fsS --max-time 5 https://api.ipify.org 2>/dev/null || true)"
  [[ "$PUBLIC_HOST" =~ ^[0-9a-fA-F.:]{3,45}$ ]] || PUBLIC_HOST=""
  [ -n "$PUBLIC_HOST" ] || PUBLIC_HOST="$(hostname -I 2>/dev/null | awk '{print $1}')"
fi

SERVER_TOML="$INSTALL_DIR/server/server.toml"
VOICE_TOML="$INSTALL_DIR/voice/voice.toml"
if [ ! -f "$SERVER_TOML" ] || [ ! -f "$VOICE_TOML" ]; then
  if [ -z "$PUBLIC_HOST" ]; then
    warn "внешний адрес не определён — голос будет работать только локально. Укажите --public-host"
    PUBLIC_HOST="127.0.0.1"
  fi
  VOICE_SECRET=$(( $(od -An -N4 -tu4 /dev/urandom | tr -d ' ') % 2147483646 + 1 ))
  render() {
    local src="$1" dst="$2" content
    content="$(cat "$src")"
    content="${content//__FLOVMP_NAME__/$SERVER_NAME}"
    content="${content//__FLOVMP_PORT__/$GAME_PORT}"
    content="${content//__FLOVMP_PLAYERS__/$SLOTS}"
    content="${content//__FLOVMP_VOICE_SECRET__/$VOICE_SECRET}"
    content="${content//__FLOVMP_VOICE_PORT__/$VOICE_INTERNAL_PORT}"
    content="${content//__FLOVMP_VOICE_PUBLIC_HOST__/$PUBLIC_HOST}"
    content="${content//__FLOVMP_VOICE_PUBLIC_PORT__/$VOICE_PUBLIC_PORT}"
    content="${content//__FLOVMP_SYNC_SEND__/$SYNC_SEND}"
    content="${content//__FLOVMP_SYNC_RECEIVE__/$SYNC_RECEIVE}"
    printf '%s\n' "$content" > "$dst"
    chmod 600 "$dst"
  }
  # Оба файла создаются вместе: секрет голоса в них обязан совпадать.
  render "$INSTALL_DIR/server/server.toml.example" "$SERVER_TOML"
  render "$INSTALL_DIR/voice/voice.toml.example" "$VOICE_TOML"
  ok "server.toml: «$SERVER_NAME», порт $GAME_PORT, слотов $SLOTS"
  ok "голос: игроки подключаются к $PUBLIC_HOST:$VOICE_PUBLIC_PORT"
else
  ok "server.toml и voice.toml уже есть — сохранены"
  GAME_PORT="$(sed -n 's/^port[[:space:]]*=[[:space:]]*\([0-9]*\).*/\1/p' "$SERVER_TOML" | head -1)"; GAME_PORT="${GAME_PORT:-7788}"
  VOICE_PUBLIC_PORT="$(sed -n 's/^externalPublicPort[[:space:]]*=[[:space:]]*\([0-9]*\).*/\1/p' "$SERVER_TOML" | head -1)"; VOICE_PUBLIC_PORT="${VOICE_PUBLIC_PORT:-7895}"
  PUBLIC_HOST="$(sed -n 's/^externalPublicHost[[:space:]]*=[[:space:]]*"\(.*\)".*/\1/p' "$SERVER_TOML" | head -1)"
fi

# ---------------------------------------------------------------------
# 6. База данных
# ---------------------------------------------------------------------
step "6/8 База данных"
DB_OK=0
if [ "$USE_DB" -eq 0 ] || [ -z "$DB_PASSWORD" ]; then
  warn "без базы данных: права и баны хранятся в файлах"
else
  if [ "$HAS_SYSTEMD" -eq 1 ]; then
    systemctl enable mariadb >/dev/null 2>&1 || systemctl enable mysql >/dev/null 2>&1 || true
    mysqladmin ping >/dev/null 2>&1 || systemctl start mariadb >/dev/null 2>&1 || systemctl start mysql >/dev/null 2>&1 || true
  else
    service mariadb start >/dev/null 2>&1 || service mysql start >/dev/null 2>&1 || true
  fi
  for _ in $(seq 1 30); do
    mysqladmin ping >/dev/null 2>&1 && break
    sleep 1
  done
  if ! mysql -u root -e "SELECT 1" >/dev/null 2>&1; then
    warn "нет доступа к MariaDB от root через сокет (у root задан пароль?)."
    warn "Создайте пользователя вручную (см. sql/README.md) — до этого сервер работает на файлах"
  else
    # SQL идёт через stdin: пароль в аргументах был бы виден в списке процессов.
    # ALTER USER — чтобы повторный запуск чинил расхождение пароля с flovmp.env.
    if mysql -u root <<SQL
CREATE DATABASE IF NOT EXISTS \`$DB_NAME\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '$DB_USER'@'localhost' IDENTIFIED BY '$DB_PASSWORD';
CREATE USER IF NOT EXISTS '$DB_USER'@'127.0.0.1' IDENTIFIED BY '$DB_PASSWORD';
ALTER USER '$DB_USER'@'localhost' IDENTIFIED BY '$DB_PASSWORD';
ALTER USER '$DB_USER'@'127.0.0.1' IDENTIFIED BY '$DB_PASSWORD';
GRANT ALL PRIVILEGES ON \`$DB_NAME\`.* TO '$DB_USER'@'localhost';
GRANT ALL PRIVILEGES ON \`$DB_NAME\`.* TO '$DB_USER'@'127.0.0.1';
FLUSH PRIVILEGES;
SQL
    then
      if MYSQL_PWD="$DB_PASSWORD" mysql -h 127.0.0.1 -u "$DB_USER" "$DB_NAME" -e "SELECT 1" >/dev/null 2>&1; then
        DB_OK=1
        # Отметка «эту базу и пользователя завёл установщик именно этой
        # установки». Только их потом удаляет --purge. Без отметки удаление
        # шло по имени из flovmp.env и сносило базу, которую сервер лишь
        # использует: на машине с несколькими серверами удаление тестового
        # инстанса уносило чужого пользователя вместе с доступом.
        printf '%s\n%s\n' "$DB_NAME" "$DB_USER" > "$INSTALL_DIR/config/.db-created"
        chmod 600 "$INSTALL_DIR/config/.db-created" 2>/dev/null || true
        ok "MariaDB: база $DB_NAME, пользователь $DB_USER — подключение проверено"
        info "Таблицы (аккаунты, баны, администраторы) создаст сам сервер при старте — sql/migrations"
      else
        warn "пользователь создан, но подключиться под ним не удалось — сервер уйдёт на файлы"
      fi
    else
      warn "не удалось создать базу/пользователя — сервер уйдёт на файлы"
    fi
  fi
fi

# Папка gamemode — сервер владельца. Создаётся из шаблона один раз и дальше
# принадлежит ему: обновления платформы её не трогают.
if [ ! -e "$INSTALL_DIR/gamemode" ] && [ -d "$INSTALL_DIR/sdk/template" ]; then
  cp -r "$INSTALL_DIR/sdk/template" "$INSTALL_DIR/gamemode"
  chmod +x "$INSTALL_DIR/gamemode/build.sh" 2>/dev/null || true
  ok "создана папка gamemode — здесь пишется ваш сервер (см. gamemode/README.md)"
fi

# ---------------------------------------------------------------------
# 7. Службы и файрвол
# ---------------------------------------------------------------------
step "7/8 Службы"
if [ "$RUN_AS" != "root" ]; then
  if ! id "$RUN_AS" >/dev/null 2>&1; then
    useradd --system --home-dir "$INSTALL_DIR" --no-create-home --shell /usr/sbin/nologin "$RUN_AS" 2>/dev/null || \
      useradd -r -d "$INSTALL_DIR" -M -s /bin/false "$RUN_AS" || die "не удалось создать пользователя $RUN_AS"
    ok "создан системный пользователь $RUN_AS"
  fi
  chown -R "$RUN_AS:$RUN_AS" "$INSTALL_DIR"
fi
chmod 700 "$INSTALL_DIR/config"

if [ "$HAS_SYSTEMD" -eq 1 ]; then
  cat > "/etc/systemd/system/$SERVICE-voice.service" <<UNIT
[Unit]
Description=FloV:MP Voice Server ($SERVICE)
After=network-online.target
Wants=network-online.target
Before=$SERVICE.service
# голосовой сервер держит одно соединение с игровым: рестарт игры перезапускает и голос
PartOf=$SERVICE.service

[Service]
Type=simple
User=$RUN_AS
WorkingDirectory=$INSTALL_DIR/voice
ExecStart=$INSTALL_DIR/start-voice.sh
Restart=always
RestartSec=3
LimitNOFILE=65535

[Install]
WantedBy=multi-user.target
UNIT

  cat > "/etc/systemd/system/$SERVICE.service" <<UNIT
[Unit]
Description=FloV:MP Game Server ($SERVICE)
After=network-online.target mariadb.service $SERVICE-voice.service
Wants=network-online.target $SERVICE-voice.service
StartLimitIntervalSec=0

[Service]
Type=simple
User=$RUN_AS
WorkingDirectory=$INSTALL_DIR/server
ExecStart=$INSTALL_DIR/start.sh
Restart=always
RestartSec=5
LimitNOFILE=65535
TimeoutStopSec=30

[Install]
WantedBy=multi-user.target
UNIT
  reload_units
  systemctl enable "$SERVICE-voice.service" >/dev/null 2>&1 || warn "не удалось включить автозапуск $SERVICE-voice"
  systemctl enable "$SERVICE.service" >/dev/null 2>&1 || warn "не удалось включить автозапуск $SERVICE"
  ok "службы: $SERVICE, $SERVICE-voice (автозапуск включён)"

  # Ежедневная копия базы. Без неё копий на сервере нет, пока о них не вспомнят.
  if [ "$DB_OK" -eq 1 ]; then
    cat > "/etc/systemd/system/$SERVICE-backup.service" <<UNIT
[Unit]
Description=FloV:MP database backup ($SERVICE)
After=mariadb.service

[Service]
Type=oneshot
User=$RUN_AS
ExecStart=$INSTALL_DIR/scripts/backup-db.sh
UNIT
    cat > "/etc/systemd/system/$SERVICE-backup.timer" <<UNIT
[Unit]
Description=FloV:MP daily database backup ($SERVICE)

[Timer]
OnCalendar=*-*-* 04:30:00
RandomizedDelaySec=15min
Persistent=true

[Install]
WantedBy=timers.target
UNIT
    reload_units
    if systemctl enable "$SERVICE-backup.timer" >/dev/null 2>&1 && systemctl start "$SERVICE-backup.timer" >/dev/null 2>&1; then
      ok "ежедневная копия базы: 04:30, хранится 14 дней ($INSTALL_DIR/backups)"
    else
      warn "таймер копий базы не включён — запускайте $INSTALL_DIR/scripts/backup-db.sh вручную или из cron"
    fi
  fi
else
  warn "systemd не найден — службы не созданы. Запуск вручную: $INSTALL_DIR/start-voice.sh & $INSTALL_DIR/start.sh"
fi

if [ "$DO_FIREWALL" -eq 1 ]; then
  if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -q "Status: active"; then
    for p in "$GAME_PORT" "$VOICE_PUBLIC_PORT"; do ufw allow "$p/udp" >/dev/null; ufw allow "$p/tcp" >/dev/null; done
    ok "ufw: открыты порты $GAME_PORT и $VOICE_PUBLIC_PORT (UDP+TCP)"
  elif command -v firewall-cmd >/dev/null 2>&1 && firewall-cmd --state >/dev/null 2>&1; then
    for p in "$GAME_PORT" "$VOICE_PUBLIC_PORT"; do
      firewall-cmd --permanent --add-port="$p/udp" >/dev/null; firewall-cmd --permanent --add-port="$p/tcp" >/dev/null
    done
    firewall-cmd --reload >/dev/null
    ok "firewalld: открыты порты $GAME_PORT и $VOICE_PUBLIC_PORT (UDP+TCP)"
  else
    info "Файрвол не активен. Если у хостинга есть внешний файрвол — откройте $GAME_PORT и $VOICE_PUBLIC_PORT (UDP+TCP)"
  fi
fi

# ---------------------------------------------------------------------
# 8. Запуск и проверка
# ---------------------------------------------------------------------
STARTED_OK=0
LOG="$INSTALL_DIR/server/server.log"
LOG_FROM=0
LOG_HEAD=""
# Только строки этого запуска: старое «Main thread started» от прошлого запуска
# не должно сойти за успех. Движок при старте перезаписывает лог с нуля — это
# видно по первой строке (в ней время старта). Раньше смотрели только на число
# строк: если новый лог успевал дорасти до длины старого, его строки
# пропускались, и исправное обновление откатывалось как «не запустилось».
new_log() {
  local total
  [ -f "$LOG" ] || return 0
  total="$(wc -l < "$LOG")"
  if [ "$total" -lt "$LOG_FROM" ] || [ "$(head -n 1 "$LOG")" != "$LOG_HEAD" ]; then
    LOG_FROM=0
  fi
  tail -n +"$((LOG_FROM + 1))" "$LOG"
}
check_started() {
  for _ in $(seq 1 90); do
    if ! systemctl is-active "$SERVICE.service" >/dev/null 2>&1; then
      sleep 1
      systemctl is-active "$SERVICE.service" >/dev/null 2>&1 || return 1
    fi
    if new_log | grep -q "Main thread started"; then
      new_log | grep -q "Loaded resource .*flovmp-starter" || return 1
      return 0
    fi
    sleep 1
  done
  return 1
}

if [ "$DO_START" -eq 1 ] && [ "$HAS_SYSTEMD" -eq 1 ]; then
  step "8/8 Запуск и проверка"
  LOG_FROM="$( [ -f "$LOG" ] && wc -l < "$LOG" || echo 0 )"
  LOG_HEAD="$( [ -f "$LOG" ] && head -n 1 "$LOG" || true )"
  systemctl restart "$SERVICE-voice.service"
  systemctl restart "$SERVICE.service"
  if check_started; then
    STARTED_OK=1
    ok "сервер запущен, ресурсы загружены"
    sleep 5
    # Подключения к базе мало: упавшая миграция оставляет её без таблиц, и
    # баны с правами молча уходят в файлы.
    if new_log | grep -q "Миграции не выполнены"; then
      warn "база подключена, но миграция схемы упала — таблицы не созданы:"
      new_log | grep -a "Миграции не выполнены" | sed 's/\x1b\[[0-9;]*m//g' | head -3 | sed 's/^/      /'
      warn "пришлите server/server.log в поддержку. Пока права и баны хранятся в файлах"
    elif new_log | grep -q "Права администраторов берутся из базы"; then
      ok "база данных подключена, таблицы созданы, права администраторов — из базы"
    elif [ "$DB_OK" -eq 1 ]; then
      warn "сервер не подключился к базе — см. строки [DB] в server/server.log"
    fi
    if new_log | grep -q "Connected to voice server"; then ok "голосовой сервер подключён"
    else warn "голосовой сервер пока не подключён — проверьте: systemctl status $SERVICE-voice"
    fi
  else
    echo
    warn "сервер не запустился. Последние строки лога:"
    tail -n 30 "$INSTALL_DIR/server/server.log" 2>/dev/null || journalctl -u "$SERVICE" -n 30 --no-pager 2>/dev/null || true
    if [ "$UPGRADE" -eq 1 ] && [ -n "$BACKUP_ARCHIVE" ]; then
      echo
      warn "откат на прежнюю версию $OLD_VERSION..."
      systemctl stop "$SERVICE.service" >/dev/null 2>&1 || true
      cut -d' ' -f3- "$INSTALL_DIR/manifest.txt" | while IFS= read -r f; do [ -n "$f" ] && rm -f "$INSTALL_DIR/$f"; done
      tar -xzpf "$BACKUP_ARCHIVE" -C "$INSTALL_DIR"
      [ "$RUN_AS" = "root" ] || chown -R "$RUN_AS:$RUN_AS" "$INSTALL_DIR"
      systemctl restart "$SERVICE-voice.service" >/dev/null 2>&1 || true
      systemctl restart "$SERVICE.service" >/dev/null 2>&1 || true
      die "обновление до $PKG_VERSION не удалось, сервер возвращён на $OLD_VERSION. Пришлите server/server.log в поддержку"
    fi
    die "установка завершена, но сервер не стартовал. Лог: $INSTALL_DIR/server/server.log"
  fi
else
  step "8/8 Запуск пропущен"
  [ "$HAS_SYSTEMD" -eq 1 ] && info "Запуск: systemctl start $SERVICE-voice $SERVICE"
fi

# ---------------------------------------------------------------------
# Итог
# ---------------------------------------------------------------------
echo
echo "${C_GREEN}${C_BOLD}=====================================================================${C_OFF}"
if [ "$UPGRADE" -eq 1 ]; then
  echo "${C_GREEN}${C_BOLD}  FloV:MP обновлён: $OLD_VERSION → $PKG_VERSION${C_OFF}"
else
  echo "${C_GREEN}${C_BOLD}  FloV:MP $PKG_VERSION установлен${C_OFF}"
fi
echo "${C_GREEN}${C_BOLD}=====================================================================${C_OFF}"
echo "  Адрес для подключения:  ${C_BOLD}${PUBLIC_HOST:-<IP сервера>}:$GAME_PORT${C_OFF}"
echo "  Порты (UDP+TCP):        $GAME_PORT (игра), $VOICE_PUBLIC_PORT (голос)"
echo "  Папка:                  $INSTALL_DIR"
echo "  Настройки:              $INSTALL_DIR/server/server.toml, $INSTALL_DIR/config/flovmp.env"
echo "  Лог:                    tail -f $INSTALL_DIR/server/server.log"
if [ "$HAS_SYSTEMD" -eq 1 ]; then
echo "  Управление:             systemctl restart|stop|status $SERVICE"
fi
echo
echo "  ${C_BOLD}Как стать администратором (любой способ):${C_OFF}"
if [ -n "$OWNER_SC" ]; then
echo "   • уже выдано: SocialClubId $OWNER_SC — уровень 8"
fi
if [ -n "${SETUP_TOKEN:-}" ] && [ -z "$OWNER_SC" ]; then
echo "   • в игре: /claimowner $SETUP_TOKEN   (одноразовый токен)"
fi
if [ "$DB_OK" -eq 1 ]; then
echo "   • в базе: mysql $DB_NAME -e \"INSERT INTO admins (social_club, level, is_founder) VALUES ('<SocialClubId>', 8, 1)\""
echo "     права применяются в течение 30 секунд, без перезапуска"
fi
echo "   • повторно: sudo $INSTALL_DIR/install.sh --owner-sc <SocialClubId>"
echo "   SocialClubId игрока виден в логе при его подключении."
echo
echo "  Ваш сервер (код):       $INSTALL_DIR/gamemode — сборка: sudo $INSTALL_DIR/gamemode/build.sh --install-sdk --restart"
echo "  Резервная копия базы:   $INSTALL_DIR/scripts/backup-db.sh"
echo "  Обновление:             распакуйте новый пакет и запустите его ./install.sh"
echo "${C_GREEN}=====================================================================${C_OFF}"
