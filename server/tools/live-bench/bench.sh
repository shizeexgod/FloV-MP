#!/usr/bin/env bash
# Живой стенд FloV:MP: настоящий alt:V + платформа из исходников + тестовые
# ресурсы probe-a/b/c в одном сервере. Для всего, где участвуют два ресурса:
# юнит-тесты не видят, как движок доставляет события и экспорты (7d932ab).
#
#   server/tools/live-bench/bench.sh <папка установки> <секунд> [задержка команда]...
#   bench.sh /opt/flovmp-test 20 12 "restart probe-c"
#
# Папка установки — сервер, поставленный install.sh/get.sh (лучше отдельный,
# тестовый: стенд заменяет в нём платформу и добавляет ресурсы). Лицензия не
# нужна — игроки в проверке не участвуют. Вывод — строки [PROBE] из журнала.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
INST="$(cd "${1:?папка установки}" && pwd)"; shift
SECONDS_TO_RUN="${1:-20}"; shift || true
SRV="$INST/server"
[ -x "$SRV/flovmp-server" ] || { echo "нет $SRV/flovmp-server — это не установка FloV:MP" >&2; exit 1; }
OUT="$(mktemp -d)"; trap 'rm -rf "$OUT"' EXIT

echo "==> сборка платформы и проб"
dotnet publish "$REPO/server/src/FloVMP.Starter/FloVMP.Starter.csproj" -c Release -o "$OUT/starter" --nologo -v q
for p in ProbeA ProbeB ProbeC; do dotnet publish "$HERE/probes/$p/$p.csproj" -c Release -o "$OUT/$p" --nologo -v q; done

echo "==> раскладка в $SRV"
rm -rf "$SRV/resources/flovmp-starter"; mkdir -p "$SRV/resources/flovmp-starter"
cp -a "$OUT/starter/." "$SRV/resources/flovmp-starter/"
cp "$REPO/server/resources/flovmp-starter/resource.toml" "$SRV/resources/flovmp-starter/"
for p in a b c; do
  P="Probe${p^^}"; rm -rf "$SRV/resources/probe-$p"; cp -a "$OUT/$P" "$SRV/resources/probe-$p"
  printf 'type = "csharp"\nmain = "%s.dll"\n' "$P" > "$SRV/resources/probe-$p/resource.toml"
done
python3 - "$SRV" <<'PY'
import json, re, sys
srv = sys.argv[1]
p = srv + "/FloV.Net.Host.runtimeconfig.json"; j = json.load(open(p))
j["runtimeOptions"]["framework"]["version"] = "10.0.0"; j["runtimeOptions"]["rollForward"] = "LatestMajor"
json.dump(j, open(p, "w"), indent=2)
t = open(srv + "/server.toml", encoding="utf-8").read()
t = re.sub(r'resources = \[.*?\]', 'resources = [\n    "flovmp-starter",\n    "flovmp-client",\n    "probe-a",\n    "probe-b",\n    "probe-c",\n]', t, flags=re.S)
open(srv + "/server.toml", "w", encoding="utf-8").write(t)
PY

# В контейнерах без IPv6 движок не открывает сокет — прослойка AF_INET6 → AF_INET.
PRELOAD=""
if [ ! -e /proc/net/if_inet6 ]; then
  gcc -shared -fPIC -O2 -o "$OUT/v6shim.so" "$HERE/v6shim.c" -ldl
  PRELOAD="$OUT/v6shim.so"
  echo "  IPv6 нет — запуск через v6shim"
fi

echo "==> запуск на $SECONDS_TO_RUN с"
DOTNET_ROOT="${DOTNET_ROOT:-$(dirname "$(readlink -f "$(command -v dotnet)")")}"
SHARED="$(ls -d "$DOTNET_ROOT"/shared/Microsoft.NETCore.App/10.* | sort -V | tail -1)"
FIFO="$OUT/stdin"; mkfifo "$FIFO"
( cd "$SRV" && DOTNET_ROOT="$DOTNET_ROOT" LD_LIBRARY_PATH="$SHARED:modules:modules/js-module" \
  FLOVMP_NATIVE_PORT="${FLOVMP_NATIVE_PORT:-7610}" LD_PRELOAD="$PRELOAD" \
  timeout "$SECONDS_TO_RUN" ./flovmp-server < "$FIFO" > "$OUT/server.log" 2>&1 ) &
PID=$!
exec 3>"$FIFO"
while [ $# -ge 2 ]; do sleep "$1"; echo "$2" >&3; shift 2; done
wait $PID || true
exec 3>&-
sed 's/\x1b\[[0-9;]*m//g' "$OUT/server.log" > "$INST/live-bench.log"
grep -a -E "\[PROBE\]|Unhandled|Exception" "$INST/live-bench.log" || true
echo "(полный журнал: $INST/live-bench.log)"
