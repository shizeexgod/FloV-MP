#!/usr/bin/env bash
# FloV:MP — проверка сервера лицензий на VDS на живой базе (после setup-vds.sh).
#   sudo bash check-vds.sh
# Путь ключа: выдан → сервер A активировал → сервер B упёрся в лимит →
# подтверждение → приостановка → возобновление → отзыв; неизвестный ключ.
# Тестовый ключ остаётся в базе отозванным (проект «ПРОВЕРКА УСТАНОВКИ»).
set -u
BASE=http://127.0.0.1
FAIL=0
ok()  { echo "  ✓ $*"; }
bad() { echo "  ✗ $*"; FAIL=1; }

# code body <- curl
req() { CODE="$(curl -s -o /tmp/la-body -w '%{http_code}' "$@")"; BODY="$(head -c 400 /tmp/la-body)"; }

echo "==> Служба"
systemctl is-active --quiet flovmp-license && ok "flovmp-license работает" || bad "flovmp-license не запущена: journalctl -u flovmp-license -n 50"

echo "==> Путь ключа на живой базе"
OUT="$(flovmp-license new --project 'ПРОВЕРКА УСТАНОВКИ' --owner 'check-vds.sh' --servers 1 --days 1 2>&1)"
KEY="$(printf '%s' "$OUT" | grep -oE 'FLV-[0-9A-F]{8}-[0-9A-F]{8}-[0-9A-F]{8}-[0-9A-F]{8}' | head -1)"
[ -n "$KEY" ] && ok "ключ выдан: $KEY" || { bad "ключ не выдан: $OUT"; exit 1; }

req "$BASE/api/v1/licenses/download-by-key?key=$KEY&server=check-a"
[ "$CODE" = 200 ] && [[ "$BODY" == *payload_b64* ]] && ok "сервер A активировал ключ, получил license.flv" || bad "активация: $CODE $BODY"
flovmp-license show "$KEY" | grep -q "активирован" && ok "статус в базе — «активирован»" || bad "статус не сменился"

req "$BASE/api/v1/licenses/download-by-key?key=$KEY&server=check-b"
[ "$CODE" = 403 ] && [[ "$BODY" == *"1 из 1"* ]] && ok "сервер B отклонён: лимит серверов" || bad "лимит: $CODE $BODY"

VERIFY=(-X POST -H 'Content-Type: application/json' -d "{\"licenseKey\":\"$KEY\",\"serverId\":\"check-a\",\"slots\":10}" "$BASE/api/v1/license/verify")
req "${VERIFY[@]}"
[ "$CODE" = 200 ] && [[ "$BODY" == *leaseSignature* ]] && ok "проверка сервера A — подтверждение выдано" || bad "verify: $CODE $BODY"

flovmp-license suspend "$KEY" --reason "проверка установки" >/dev/null
req "${VERIFY[@]}"
[ "$CODE" = 403 ] && [[ "$BODY" == *приостановлен* ]] && ok "после suspend проверка отклонена" || bad "suspend: $CODE $BODY"

flovmp-license resume "$KEY" >/dev/null
req "${VERIFY[@]}"
[ "$CODE" = 200 ] && ok "после resume ключ снова действует" || bad "resume: $CODE"

flovmp-license revoke "$KEY" --reason "тестовый ключ проверки установки" >/dev/null
req "${VERIFY[@]}"
[ "$CODE" = 403 ] && [[ "$BODY" == *отозван* ]] && ok "после revoke проверка отклонена" || bad "revoke: $CODE $BODY"

req "$BASE/api/v1/licenses/download-by-key?key=FLV-00000000-00000000-00000000-00000000&server=x"
[ "$CODE" = 403 ] && [[ "$BODY" == *"не найден"* ]] && ok "неизвестный ключ отклонён" || bad "unknown: $CODE $BODY"

req "http://188.127.229.224/api/v1/license/health"
[ "$CODE" = 200 ] && ok "служба доступна снаружи" || bad "снаружи: $CODE"

echo "==> Журнал тестового ключа"
flovmp-license events "$KEY" --limit 20
echo
[ "$FAIL" = 0 ] && echo "ВСЁ ПРОШЛО." || echo "ЕСТЬ ОШИБКИ (см. ✗ выше)."
exit "$FAIL"
