#!/usr/bin/env bash
# =====================================================================
#  FloV:MP — установка сервера одной командой (Linux: Ubuntu / Debian)
#
#    curl -fsSLo flovmp-get.sh http://<адрес раздачи>/cdn/get.sh
#    sudo bash flovmp-get.sh --key FLV-XXXX-XXXX-XXXX --owner-sc <ваш SocialClubId>
#
#  Что делает: по ключу лицензии получает описание последнего релиза,
#  проверяет его подпись ключом релизов FloV:MP (вшит ниже), скачивает пакет,
#  сверяет SHA-256 и запускает установщик из пакета (install.sh) с вашими
#  параметрами. Подмена пакета — на сервере раздачи или по дороге — не пройдёт.
#
#  Все параметры install.sh передаются как есть (./install.sh --help).
#  Свой адрес раздачи: --dist http://... или FLOVMP_DIST_URL.
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
PASS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --key|-k) [ $# -ge 2 ] || die "после $1 нужен ключ"; KEY="$2"; PASS+=("$1" "$2"); shift 2 ;;
    --dist)   [ $# -ge 2 ] || die "после --dist нужен адрес"; DIST="${2%/}"; shift 2 ;;
    *)        PASS+=("$1"); shift ;;
  esac
done
[ -n "$KEY" ] || die "нужен ключ лицензии: --key FLV-XXXX-XXXX-XXXX (личный кабинет FloV:MP)"
[ "$(id -u)" -eq 0 ] || die "запустите через sudo"
[ -n "$RELEASE_PUBKEY_PEM" ] || die "в загрузчике нет ключа релизов — скачайте get.sh заново"

for tool in curl openssl sha256sum tar base64; do
  command -v "$tool" >/dev/null 2>&1 || { apt-get update -qq && apt-get install -y -qq curl openssl coreutils tar >/dev/null; break; }
done
for tool in curl openssl sha256sum tar base64; do command -v "$tool" >/dev/null 2>&1 || die "нужен $tool"; done

WORK="$(mktemp -d /tmp/flovmp-get.XXXXXX)"
trap 'rm -rf "$WORK"' EXIT
Q="os=linux&key=$(printf '%s' "$KEY" | tr -cd 'A-Za-z0-9-')"

fetch() { # url файл — ответ сервера раздачи с понятной ошибкой
  local code
  code="$(curl -sS --retry 3 --connect-timeout 15 -o "$2" -w '%{http_code}' "$1")" || die "сервер раздачи недоступен: $DIST"
  [ "$code" = "200" ] || die "сервер раздачи ответил $code: $(head -c 300 "$2" 2>/dev/null)"
}

echo "==> Релиз FloV:MP"
fetch "$DIST/api/v1/distribution/release?$Q" "$WORK/release.txt"
fetch "$DIST/api/v1/distribution/release.sig?$Q" "$WORK/release.sig"
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

echo "==> Скачивание пакета"
fetch "$DIST/api/v1/distribution/download?$Q" "$WORK/$FILE"
GOT="$(sha256sum "$WORK/$FILE" | cut -d' ' -f1)"
[ "$GOT" = "$SHA" ] || die "SHA-256 пакета не совпал (ожидали $SHA, получили $GOT) — пакет повреждён или подменён"
echo "  ✓ SHA-256 совпал"

# Установщик берём из самого (уже проверенного) пакета: он сам распакует и
# проверит архив, а его версия всегда совпадает с версией пакета.
tar -xzf "$WORK/$FILE" -C "$WORK" --wildcards '*/install.sh' || die "в пакете нет install.sh"
INSTALLER="$(find "$WORK" -mindepth 2 -maxdepth 2 -name install.sh | head -1)"
[ -n "$INSTALLER" ] || die "в пакете нет install.sh"
bash "$INSTALLER" --package "$WORK/$FILE" --sha256 "$SHA" "${PASS[@]}"
