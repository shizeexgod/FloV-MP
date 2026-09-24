#!/usr/bin/env bash
# =====================================================================
#  FloV:MP — установка и обновление сервера одной командой
#  (Linux: Ubuntu / Debian)
#
#  Установка с GitHub (основной канал — скачивается быстро и без обрывов):
#    curl -fsSLo flovmp-get.sh https://github.com/<owner>/<repo>/releases/latest/download/get.sh
#    sudo bash flovmp-get.sh --github <owner>/<repo> --key FLV-XXXX-XXXX-XXXX-XXXX
#
#  Установка с сервера раздачи FloV:MP:
#    curl -fsSLo flovmp-get.sh http://<адрес раздачи>/cdn/get.sh
#    sudo bash flovmp-get.sh --key FLV-XXXX-XXXX-XXXX-XXXX
#
#  Обновление потом — одна команда без параметров (ключ и источник
#  запоминаются при установке):
#    sudo bash /opt/flovmp/update.sh
#
#  Что делает: получает описание релиза, проверяет его подпись ключом релизов
#  FloV:MP (вшит ниже), скачивает пакет, сверяет SHA-256 и запускает
#  установщик из пакета (install.sh) с вашими параметрами. Подмена пакета —
#  на GitHub, на сервере раздачи или по дороге — не пройдёт.
#
#  --github owner/repo  брать релиз с GitHub (без --tag — последний стабильный)
#  --tag v1.2.3         конкретный релиз с GitHub, например бету
#  --dist http://...    свой адрес раздачи (или FLOVMP_DIST_URL)
#  --reinstall          поставить заново ту же версию
#  Все остальные параметры install.sh передаются как есть (./install.sh --help).
# =====================================================================
set -euo pipefail

DIST="${FLOVMP_DIST_URL:-http://188.127.229.224}"

# BEGIN RELEASE_PUBKEY_PEM
RELEASE_PUBKEY_PEM='-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEApmynUrPAKz17KYFCg3UR
y5BgBanGtIDxbLIExHQ59tdxAcUN2uPMN7Wu51TSkvit5kKxZvDWF9MSll2sLCXU
JMpX9Lxa1GFLpmx6axrrw44z4id0ESrb1C7kqyOYu54lBVdBVyCup09Kgyfrc1vE
9J7LRTUD+9DaahJ1CVkcg4uopbBItVqywb4UuOlbGuAf1x/ocgO3hrKv9e6R+LN3
3EH3udfMlEcq7GWVN5/GW0709KWF21zemEm4wS3NRUWnAGvwBcwgOIQdKoybjZtM
gY6rpKCganSdpcGthunHnAOddbPSoerR6imGgqL2zTzKMFsPpxxOz1Mop3MdqkDb
M5g2laOt8CyfcTwYgQi2OUf7K3MmXqkR1sv8duIlRRW1GOEaMg7M2zvm4MwVe4qM
Ims7ME+fREEIAE4UmdP8v5Pc8fhEQNl5WD2eTXY57DC08IrRE0GbwAg1R99C9Du3
mtDfUsL1Uo+uwzLny0LnQuhILpMotkDPe7arcMbsSVQpAgMBAAE=
-----END PUBLIC KEY-----'
# END RELEASE_PUBKEY_PEM

die() { echo "ОШИБКА: $*" >&2; exit 1; }

KEY=""
DIR="/opt/flovmp"
DIR_GIVEN=0
GITHUB=""
TAG=""
REINSTALL=0
EXTRA=0   # прочие параметры установщика: с ними пропуск обновления не делаем
PASS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --key|-k|-Key) [ $# -ge 2 ] || die "после $1 нужен ключ"; KEY="$2"; shift 2 ;;
    --dir|-InstallDir) [ $# -ge 2 ] || die "после $1 нужен путь"; DIR="${2%/}"; DIR_GIVEN=1; shift 2 ;;
    --dist|-Dist)  [ $# -ge 2 ] || die "после $1 нужен адрес"; DIST="${2%/}"; shift 2 ;;
    --github|-GitHub) [ $# -ge 2 ] || die "после $1 нужно owner/repo"; GITHUB="$2"; shift 2 ;;
    --tag|-Tag)    [ $# -ge 2 ] || die "после $1 нужен тег релиза"; TAG="$2"; shift 2 ;;
    --reinstall|-Reinstall) REINSTALL=1; shift ;;
    *)             PASS+=("$1"); EXTRA=$((EXTRA + 1)); shift ;;
  esac
done

# Запуск как update.sh из папки установленного сервера: обновляем именно её.
SELF_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
if [ "$DIR_GIVEN" -eq 0 ] && [ -f "$SELF_DIR/manifest.txt" ] && [ -f "$SELF_DIR/config/flovmp.env" ]; then
  DIR="$SELF_DIR"
fi
DIR="$(readlink -m "$DIR")"

# Источник обновлений запоминается при установке (config/update.env), чтобы
# обновление было одной командой без параметров. Тег не запоминается: иначе
# сервер навсегда остался бы на одной бете.
SOURCE_FILE="$DIR/config/update.env"
if [ -z "$GITHUB" ] && [ -f "$SOURCE_FILE" ]; then
  GITHUB="$(sed -n 's/^FLOVMP_UPDATE_GITHUB=//p' "$SOURCE_FILE" | head -1)"
  [ -n "$GITHUB" ] && echo "  источник обновлений: GitHub $GITHUB (из $SOURCE_FILE)"
fi
if [ -n "$GITHUB" ]; then
  GITHUB="${GITHUB#https://github.com/}"; GITHUB="${GITHUB%/}"; GITHUB="${GITHUB%.git}"
  [[ "$GITHUB" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || die "--github ждёт owner/repo, получено: $GITHUB"
fi
[ -z "$TAG" ] || [[ "$TAG" =~ ^[A-Za-z0-9._-]+$ ]] || die "неверный тег: $TAG"
[ -z "$TAG" ] || [ -n "$GITHUB" ] || die "--tag работает только вместе с --github owner/repo"

# Обновление уже установленного сервера: ключ лежит в его настройках, второй
# раз его вводить не нужно — команда обновления получается короткой.
if [ -z "$KEY" ] && [ -f "$DIR/config/flovmp.env" ]; then
  KEY="$(sed -n 's/^[[:space:]]*FLOVMP_LICENSE_KEY[[:space:]]*=[[:space:]]*//p' "$DIR/config/flovmp.env" | head -1)"
  [ -n "$KEY" ] && echo "  ключ лицензии взят из $DIR/config/flovmp.env"
fi
[ -n "$KEY" ] || die "нужен ключ лицензии: --key FLV-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX"
KEY="$(printf '%s' "$KEY" | tr 'a-z' 'A-Z' | tr -cd 'A-Z0-9-')"
[[ "$KEY" =~ ^FLV(-[0-9A-F]{8}){4}$ ]] || die "ключ не похож на лицензионный: $KEY"
PASS+=(--key "$KEY" --dir "$DIR")
[ "$(id -u)" -eq 0 ] || die "запустите через sudo"
[ -n "$RELEASE_PUBKEY_PEM" ] || die "в загрузчике нет ключа релизов — скачайте get.sh заново"

for tool in curl openssl sha256sum tar base64; do
  command -v "$tool" >/dev/null 2>&1 || { apt-get update -qq && apt-get install -y -qq curl openssl coreutils tar >/dev/null; break; }
done
for tool in curl openssl sha256sum tar base64; do command -v "$tool" >/dev/null 2>&1 || die "нужен $tool"; done

WORK="$(mktemp -d /tmp/flovmp-get.XXXXXX)"
trap 'rm -rf "$WORK"' EXIT
Q="os=linux&key=$KEY"
if [ -n "$GITHUB" ]; then
  if [ -n "$TAG" ]; then GH="https://github.com/$GITHUB/releases/download/$TAG"
  else GH="https://github.com/$GITHUB/releases/latest/download"; fi
fi

fetch() { # url файл — ответ сервера раздачи с понятной ошибкой
  local code
  # -L: GitHub отдаёт файлы релиза через перенаправление на своё хранилище.
  code="$(curl -sSL --retry 5 --retry-delay 3 --connect-timeout 15 -o "$2" -w '%{http_code}' "$1")" \
    || die "не удалось скачать $1"
  [ "$code" = "200" ] || die "$1 ответил $code: $(head -c 300 "$2" 2>/dev/null)"
}

echo "==> Релиз FloV:MP"
if [ -n "$GITHUB" ]; then
  echo "  источник: GitHub $GITHUB${TAG:+, релиз $TAG}"
  fetch "$GH/release-linux.txt" "$WORK/release.txt"
  fetch "$GH/release-linux.txt.sig" "$WORK/release.sig"
else
  echo "  источник: $DIST"
  fetch "$DIST/api/v1/distribution/release?$Q" "$WORK/release.txt"
  fetch "$DIST/api/v1/distribution/release.sig?$Q" "$WORK/release.sig"
fi
printf '%s\n' "$RELEASE_PUBKEY_PEM" > "$WORK/pub.pem"
base64 -d "$WORK/release.sig" > "$WORK/release.sig.bin" 2>/dev/null || die "повреждённая подпись релиза"
openssl dgst -sha256 -verify "$WORK/pub.pem" -signature "$WORK/release.sig.bin" "$WORK/release.txt" >/dev/null 2>&1 \
  || die "подпись релиза НЕ верна — описание подменено. Установка остановлена"
echo "  ✓ подпись релиза верна"

field() { sed -n "s/^$1=//p" "$WORK/release.txt" | head -1; }
VERSION="$(field version)"; FILE="$(field file)"; SHA="$(field sha256)"; OS="$(field os)"
[ "$OS" = "linux" ] || die "релиз не для Linux ($OS)"
[[ "$FILE" =~ ^[A-Za-z0-9._-]+\.tar\.gz$ ]] || die "неверное имя пакета в релизе: $FILE"
[[ "$SHA" =~ ^[0-9a-f]{64}$ ]] || die "неверный SHA-256 в релизе"
echo "  версия $VERSION, пакет $FILE"

INSTALLED="$(cat "$DIR/VERSION" 2>/dev/null || true)"
if [ -n "$INSTALLED" ]; then
  echo "  установлено сейчас: $INSTALLED"
  if [ "$INSTALLED" = "$VERSION" ] && [ "$REINSTALL" -eq 0 ] && [ "$EXTRA" -eq 0 ]; then
    echo "Обновление не требуется: установлена та же версия. Поставить заново — --reinstall"
    exit 0
  fi
fi

echo "==> Скачивание пакета"
if [ -n "$GITHUB" ]; then fetch "$GH/$FILE" "$WORK/$FILE"
else fetch "$DIST/api/v1/distribution/download?$Q" "$WORK/$FILE"; fi
GOT="$(sha256sum "$WORK/$FILE" | cut -d' ' -f1)"
[ "$GOT" = "$SHA" ] || die "SHA-256 пакета не совпал (ожидали $SHA, получили $GOT) — пакет повреждён или подменён"
echo "  ✓ SHA-256 совпал"

# Установщик берём из самого (уже проверенного) пакета: он сам распакует и
# проверит архив, а его версия всегда совпадает с версией пакета.
tar -xzf "$WORK/$FILE" -C "$WORK" --wildcards '*/install.sh' || die "в пакете нет install.sh"
INSTALLER="$(find "$WORK" -mindepth 2 -maxdepth 2 -name install.sh | head -1)"
[ -n "$INSTALLER" ] || die "в пакете нет install.sh"
bash "$INSTALLER" --package "$WORK/$FILE" --sha256 "$SHA" "${PASS[@]}"

# Запомнить источник для короткой команды обновления (sudo bash $DIR/update.sh).
if [ -d "$DIR/config" ]; then
  { echo "# Откуда берутся обновления FloV:MP (пишет get.sh). Пусто — сервер раздачи."
    echo "FLOVMP_UPDATE_GITHUB=$GITHUB"; } > "$SOURCE_FILE"
fi
