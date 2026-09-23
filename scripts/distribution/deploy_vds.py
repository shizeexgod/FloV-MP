#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Установить сервер лицензий на VDS через REDL и проверить его на живой базе.

  $env:REDL_TOKEN = "redl_pat_..."        # PowerShell (токен из панели redl.io)
  python scripts/distribution/deploy_vds.py

Что делает:
  1. заливает dist/vds (make_vds_bundle.py) в /root/flovmp-vds, сверяя SHA-256;
  2. запускает setup-vds.sh: .NET 8, база flovmp_licensing, служба, nginx;
  3. проверяет весь путь ключа на живой базе: выдан → активация сервера →
     второй сервер упирается в лимит → приостановка → проверка отклонена →
     возобновление → отзыв. Тестовый ключ остаётся в базе отозванным
     (проект «ПРОВЕРКА УСТАНОВКИ»), чтобы было видно, что проверка шла.
"""
import base64
import hashlib
import json
import os
import re
import sys
import time
import urllib.request
from concurrent.futures import ThreadPoolExecutor

MACHINE = os.environ.get("REDL_MACHINE", "avds-rg1s7j")
TOKEN = os.environ.get("REDL_TOKEN", "").strip()
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BUNDLE = os.path.join(REPO, "dist", "vds")
REMOTE = "/root/flovmp-vds"
# ВАЖНО: /write выше ~50 КБ отвечает «bytes: N», но файла на машине не создаёт
# (замерено 23.09.2026: 40 000 — есть, 60 000 — ответ есть, файла нет). Поэтому
# кусок держим заведомо ниже порога, а каждый кусок после заливки сверяем по
# размеру — молчаливая потеря куска иначе выглядит как «SHA-256 не совпал».
CHUNK = 40_000


def call(path, payload, timeout=180):
    req = urllib.request.Request("https://redl.io/api/ext" + path, data=json.dumps(payload).encode(),
                                 headers={"Authorization": "Bearer " + TOKEN, "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read().decode())


def run(cmd, t=600, quiet=False):
    for attempt in range(6):
        try:
            r = call("/run", {"machine": MACHINE, "command": cmd, "timeoutSec": t}, timeout=t + 30)
            # REDL always includes exitCode.  A null value means the command
            # never reached a shell (for example while SSH is temporarily
            # unavailable) and must not be treated as a completed command.
            if isinstance(r.get("exitCode"), int):
                if not quiet:
                    out = (r.get("output") or "") + (r.get("stderr") or "")
                    if out.strip():
                        print(out.rstrip())
                return r
            detail = r.get("error") or r.get("output") or "команда не завершилась"
            print("  REDL: {} — повтор".format(str(detail).strip()[:200]), flush=True)
        except Exception as e:
            print("  REDL: {} — повтор".format(e), flush=True)
        time.sleep(3 + attempt * 3)
    sys.exit("REDL не отвечает")


def upload(local, remote, attempts=3):
    """Повтор всей заливки: по нестабильному каналу кусок доходит повреждённым."""
    for attempt in range(1, attempts + 1):
        if upload_once(local, remote):
            return
        print("  повтор заливки {} ({}/{})".format(os.path.basename(local), attempt, attempts), flush=True)
        time.sleep(5)
    sys.exit("не удалось залить {}".format(local))


def missing_parts(stage, parts, already):
    """Куски, которых на машине нет или они короче отправленных.

    Ответ /write про число байт ничего не гарантирует, поэтому смотрим сами."""
    out = run("for f in {}.d/*; do echo \"$(basename $f) $(wc -c < $f)\"; done 2>/dev/null".format(stage),
              t=60, quiet=True).get("output") or ""
    have = {}
    for line in out.splitlines():
        name, _, size = line.strip().partition(" ")
        if size.isdigit():
            have[name] = int(size)
    return [i for i in range(len(parts))
            if i not in already and have.get("{:05d}".format(i)) != len(parts[i])]


def upload_once(local, remote):
    data = open(local, "rb").read()
    digest = hashlib.sha256(data).hexdigest()
    # Уже залит (повторный запуск после обрыва) — не гоняем заново.
    have = ""
    try:
        probe = call("/run", {"machine": MACHINE,
                              "command": "sha256sum '{}' 2>/dev/null | cut -d' ' -f1".format(remote),
                              "timeoutSec": 20}, timeout=35)
        if isinstance(probe.get("exitCode"), int):
            have = (probe.get("output") or "").strip()
    except Exception:
        # Файловый канал REDL может работать, пока SSH машины перегружен.
        # В таком случае безопасно перезаписываем временные куски и сверяем
        # итоговый файл, когда командный канал восстановится.
        pass
    if have == digest:
        print("  {} — уже на VDS".format(os.path.relpath(local, BUNDLE)), flush=True)
        return True
    b64 = base64.b64encode(data).decode()
    parts = [b64[i:i + CHUNK] for i in range(0, len(b64), CHUNK)] or [""]
    # Digest в имени делает staging неизменяемым: старые куски другого файла
    # не попадут в сборку, а /write сам создаёт промежуточную папку.
    stage = "/tmp/fu-{}-{}".format(hashlib.sha1(remote.encode()).hexdigest()[:12], digest[:12])

    # Остатки прерванной заливки того же файла: лишние куски попадут в склейку
    # и SHA-256 не сойдётся сколько ни повторяй.
    run("rm -rf {s}.d {s}.bin".format(s=stage), quiet=True)

    last_error = [""]

    def put(i):
        for attempt in range(5):
            try:
                r = call("/write", {"machine": MACHINE, "path": "{}.d/{:05d}".format(stage, i), "content": parts[i]})
                if "bytes" in r:
                    return True
                last_error[0] = json.dumps(r, ensure_ascii=False)[:200]
            except Exception as e:
                last_error[0] = str(e)[:200]
            time.sleep(1 + attempt * 2)
        return False

    todo = list(range(len(parts)))
    for _ in range(6):
        with ThreadPoolExecutor(3) as ex:
            results = list(ex.map(put, todo))
        todo = [i for i, uploaded in zip(todo, results) if not uploaded]
        todo += missing_parts(stage, parts, todo)
        if not todo:
            break
    if todo:
        print("  не залито кусков: {} из {} ({})".format(len(todo), len(parts), last_error[0]), flush=True)
        return False
    got = (run("cat {s}.d/* | base64 -d > {s}.bin && sha256sum {s}.bin | cut -d' ' -f1".format(s=stage), quiet=True).get("output") or "").strip()
    if got != digest:
        print("  SHA-256 не совпал: {}".format(os.path.basename(local)), flush=True)
        run("rm -rf {s}.d {s}.bin".format(s=stage), quiet=True)
        return False
    run("mkdir -p '{d}' && mv {s}.bin '{r}' && rm -rf {s}.d".format(d=os.path.dirname(remote), s=stage, r=remote), quiet=True)
    print("  {} ({} КБ)".format(os.path.relpath(local, BUNDLE), len(data) // 1024), flush=True)
    return True


def step(title):
    print("\n==> " + title, flush=True)


def check(cond, ok, fail):
    print(("  ✓ " if cond else "  ✗ ") + (ok if cond else fail), flush=True)
    return cond


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    if not re.fullmatch(r"redl_pat_[0-9a-f]{20,}", TOKEN.strip()):
        sys.exit("REDL_TOKEN — токен из панели redl.io вида redl_pat_... (сейчас задано что-то другое)")
    if not os.path.isfile(os.path.join(BUNDLE, "flovmp-license", "flovmp-license.dll")):
        sys.exit("нет комплекта: python scripts/distribution/make_vds_bundle.py --with-key")

    step("Заливка комплекта на VDS")
    for root, _, files in os.walk(BUNDLE):
        for f in files:
            local = os.path.join(root, f)
            upload(local, REMOTE + "/" + os.path.relpath(local, BUNDLE).replace(os.sep, "/"))

    step("Установка службы")
    # Код возврата — самой установки, а не tail: провал nginx не должен сойти за успех.
    r = run("bash {0}/setup-vds.sh {0} > /tmp/flovmp-setup.log 2>&1; rc=$?; tail -40 /tmp/flovmp-setup.log; exit $rc".format(REMOTE), t=900)
    if r.get("exitCode") != 0:
        sys.exit("setup-vds.sh завершился с ошибкой")
    if os.path.exists(os.path.join(BUNDLE, "authority.pem")):
        os.remove(os.path.join(BUNDLE, "authority.pem"))  # ключ уже на VDS, копия в %USERPROFILE%\.flovmp

    step("Проверка на живой базе")
    ok = True
    out = run("flovmp-license new --project 'ПРОВЕРКА УСТАНОВКИ' --owner 'deploy_vds.py' --servers 1 --days 1", quiet=True).get("output") or ""
    m = re.search(r"FLV-[0-9A-F]{8}-[0-9A-F]{8}-[0-9A-F]{8}-[0-9A-F]{8}", out)
    if not check(m is not None, "ключ выдан", "ключ не выдан: " + out[-300:]):
        sys.exit(1)
    key = m.group(0)
    base = "http://127.0.0.1"

    def http(cmd):
        o = run(cmd + " -s -o /tmp/la-body -w '%{http_code}'; echo; cat /tmp/la-body", quiet=True).get("output") or ""
        code, _, body = o.partition("\n")
        return code.strip(), body

    code, body = http("curl '{}/api/v1/licenses/download-by-key?key={}&server=deploy-test-a'".format(base, key))
    ok &= check(code == "200" and "payload_b64" in body, "сервер A активировал ключ и получил license.flv", "активация: {} {}".format(code, body))
    status = run("flovmp-license show {} | head -3".format(key), quiet=True).get("output") or ""
    ok &= check("активирован" in status, "статус в базе — «активирован»", "статус: " + status)

    code, body = http("curl '{}/api/v1/licenses/download-by-key?key={}&server=deploy-test-b'".format(base, key))
    ok &= check(code == "403" and "1 из 1" in body, "сервер B отклонён: лимит серверов", "лимит: {} {}".format(code, body))

    verify = "curl -X POST -H 'Content-Type: application/json' -d '{{\"licenseKey\":\"{}\",\"serverId\":\"deploy-test-a\",\"slots\":10}}' '{}/api/v1/license/verify'".format(key, base)
    code, body = http(verify)
    ok &= check(code == "200" and "leaseSignature" in body, "проверка сервера A — подтверждение выдано", "verify: {} {}".format(code, body))

    run("flovmp-license suspend {} --reason 'проверка установки'".format(key), quiet=True)
    code, body = http(verify)
    ok &= check(code == "403" and "приостановлен" in body, "после suspend проверка отклонена", "suspend: {} {}".format(code, body))

    run("flovmp-license resume {}".format(key), quiet=True)
    code, _ = http(verify)
    ok &= check(code == "200", "после resume ключ снова действует", "resume: " + code)

    run("flovmp-license revoke {} --reason 'тестовый ключ проверки установки'".format(key), quiet=True)
    code, body = http(verify)
    ok &= check(code == "403" and "отозван" in body, "после revoke проверка отклонена", "revoke: {} {}".format(code, body))

    code, body = http("curl '{}/api/v1/licenses/download-by-key?key=FLV-00000000-00000000-00000000-00000000&server=x'".format(base))
    ok &= check(code == "403" and "не найден" in body, "неизвестный ключ отклонён", "unknown: {} {}".format(code, body))

    # Снаружи — с этого ПК: сам VDS до своего внешнего IP часто не достаёт.
    try:
        with urllib.request.urlopen("http://188.127.229.224/api/v1/license/health", timeout=15) as r:
            code = str(r.status)
    except Exception as e:
        code = str(e)
    ok &= check(code == "200", "служба доступна снаружи: http://188.127.229.224", "снаружи: " + code)

    step("Журнал тестового ключа")
    run("flovmp-license events {} --limit 20".format(key))
    print("\n" + ("ВСЁ ПРОШЛО. " if ok else "ЕСТЬ ОШИБКИ (см. ✗ выше). ") +
          "Выдать ключ клиенту: flovmp-license new --project \"...\" --owner \"...\" --days 365")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
