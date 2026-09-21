#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
FloV:MP — раздача серверных пакетов покупателям по ключу лицензии.

Работает на VDS платформы за nginx (location /api/ → 127.0.0.1:7799).
Только стандартная библиотека Python 3.8+.

  flovmp-dist serve                              # сервис (systemd: flovmp-dist.service)
  flovmp-dist keys add FLV-XXXX [--note "Проект"] [--expires 2027-09-21] [--os linux,windows]
  flovmp-dist keys remove FLV-XXXX
  flovmp-dist keys list
  flovmp-dist publish <папка релиза>             # release.txt + release.txt.sig + пакеты
  flovmp-dist status

Раздача:
  GET /api/v1/distribution/release?os=linux&key=K     — release.txt (подписанное описание)
  GET /api/v1/distribution/release.sig?os=linux&key=K — подпись release.txt (base64)
  GET /api/v1/distribution/download?os=linux&key=K    — сам пакет (nginx отдаёт файл, X-Accel-Redirect)
  GET /api/v1/distribution/health                     — проверка живости

Целостность не зависит от HTTPS: release.txt подписан ключом релизов владельца
(закрытый ключ только на его ПК, scripts/distribution/sign_release.py), загрузчики
get.sh / get.ps1 проверяют подпись встроенным открытым ключом и SHA-256 пакета.
Ключ лицензии нужен, чтобы пакет не раздавался всем подряд.
"""

import argparse
import datetime
import hashlib
import hmac
import json
import os
import re
import shutil
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

DATA = os.environ.get("FLOVMP_DIST_DATA", "/var/lib/flovmp-dist")
KEYS_FILE = os.path.join(DATA, "keys.json")
RELEASES = os.path.join(DATA, "releases")
CURRENT = os.path.join(RELEASES, "current")          # симлинк на папку текущего релиза
LOG_FILE = os.path.join(DATA, "downloads.log")
LISTEN = os.environ.get("FLOVMP_DIST_LISTEN", "127.0.0.1:7799")
# nginx: internal-location, из которой он сам отдаёт файл (см. nginx-flovmp-dist.conf).
ACCEL_PREFIX = "/_flovmp_dist_files/"

KEY_RE = re.compile(r"^FLV-(?:[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}|[A-Z0-9]{8}-[A-Z0-9]{8}-[A-Z0-9]{8}-[A-Z0-9]{8})$")
OSES = ("linux", "windows")

# Подбор ключей: после 20 неверных попыток за 10 минут IP получает отказ до
# конца окна (nginx дополнительно режет частоту запросов).
FAIL_LIMIT = 20
FAIL_WINDOW = 600
_fails = {}
_fails_lock = threading.Lock()
_keys_lock = threading.Lock()


def now_utc():
    return datetime.datetime.now(datetime.timezone.utc)


def load_keys():
    try:
        with open(KEYS_FILE, encoding="utf-8") as fh:
            return json.load(fh)
    except FileNotFoundError:
        return {}


def save_keys(keys):
    os.makedirs(DATA, exist_ok=True)
    tmp = KEYS_FILE + ".tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(keys, fh, ensure_ascii=False, indent=2, sort_keys=True)
    os.chmod(tmp, 0o600)
    os.replace(tmp, KEYS_FILE)


def normalize_key(raw):
    key = (raw or "").strip().upper()
    return key if KEY_RE.match(key) else None


def check_key(raw, os_name):
    """(ok, причина). Сравнение без утечки по времени."""
    key = normalize_key(raw)
    if not key:
        return False, "неверный формат ключа (FLV-XXXX-XXXX-XXXX)"
    keys = load_keys()
    found = None
    for k, info in keys.items():
        if hmac.compare_digest(k, key):
            found = info
    if found is None:
        return False, "ключ не найден — проверьте ключ в личном кабинете"
    if found.get("disabled"):
        return False, "ключ отключён"
    exp = found.get("expires")
    if exp and now_utc().date() > datetime.date.fromisoformat(exp):
        return False, "срок ключа истёк {}".format(exp)
    allowed = found.get("os") or list(OSES)
    if os_name not in allowed:
        return False, "для этого ключа нет пакета {}".format(os_name)
    return True, key


def release_info(os_name):
    """Описание текущего релиза из release.txt (ключ=значение)."""
    path = os.path.join(CURRENT, "release-{}.txt".format(os_name))
    if not os.path.isfile(path):
        return None, None
    info = {}
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            if "=" in line:
                k, v = line.rstrip("\n").split("=", 1)
                info[k] = v
    return path, info


def log_download(ip, key, os_name, what, result):
    try:
        with open(LOG_FILE, "a", encoding="utf-8") as fh:
            # Сам ключ в журнал не пишем — только отпечаток.
            mark = hashlib.sha256((key or "").strip().upper().encode()).hexdigest()[:10] if key else "-"
            fh.write("{} {} key:{} {} {} {}\n".format(now_utc().isoformat(timespec="seconds"), ip,
                                                   mark, os_name, what, result))
    except OSError:
        pass


def too_many_failures(ip):
    with _fails_lock:
        now = time.time()
        rec = [t for t in _fails.get(ip, []) if now - t < FAIL_WINDOW]
        _fails[ip] = rec
        return len(rec) >= FAIL_LIMIT


def note_failure(ip):
    with _fails_lock:
        _fails.setdefault(ip, []).append(time.time())


class Handler(BaseHTTPRequestHandler):
    server_version = "flovmp-dist"
    sys_version = ""

    def log_message(self, fmt, *args):  # в journald — кратко
        sys.stderr.write("{} {}\n".format(self.client_ip(), fmt % args))

    def client_ip(self):
        return self.headers.get("X-Real-IP") or self.client_address[0]

    def reply(self, code, body, ctype="text/plain; charset=utf-8", extra=None):
        data = body.encode("utf-8") if isinstance(body, str) else body
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        for k, v in (extra or {}).items():
            self.send_header(k, v)
        self.end_headers()
        if self.command != "HEAD":
            self.wfile.write(data)

    def do_HEAD(self):
        self.do_GET()

    def do_GET(self):
        url = urlparse(self.path)
        q = parse_qs(url.query)
        route = url.path.rstrip("/")
        if route == "/api/v1/distribution/health":
            _, lin = release_info("linux")
            return self.reply(200, json.dumps({"ok": True, "version": (lin or {}).get("version")}), "application/json")
        if route not in ("/api/v1/distribution/release", "/api/v1/distribution/release.sig",
                         "/api/v1/distribution/download", "/api/v1/distribution/download-latest"):
            return self.reply(404, "нет такого адреса\n")

        ip = self.client_ip()
        os_name = (q.get("os") or ["linux"])[0]
        if os_name not in OSES:
            return self.reply(400, "os: linux или windows\n")
        if too_many_failures(ip):
            return self.reply(429, "слишком много попыток с неверным ключом — подождите 10 минут\n")
        ok, key_or_why = check_key((q.get("key") or [""])[0], os_name)
        if not ok:
            note_failure(ip)
            log_download(ip, (q.get("key") or [""])[0], os_name, route.rsplit("/", 1)[-1], "отказ: " + key_or_why)
            return self.reply(403, "доступ запрещён: {}\n".format(key_or_why))

        path, info = release_info(os_name)
        if not info:
            return self.reply(503, "релиз ещё не опубликован\n")
        what = route.rsplit("/", 1)[-1]
        if what == "release":
            with open(path, "rb") as fh:
                return self.reply(200, fh.read())
        if what == "release.sig":
            with open(path + ".sig", "rb") as fh:
                return self.reply(200, fh.read())
        # download / download-latest: отдаёт nginx (sendfile, докачка), мы только разрешаем.
        name = info.get("file", "")
        if not re.match(r"^[A-Za-z0-9._-]+$", name) or not os.path.isfile(os.path.join(CURRENT, name)):
            return self.reply(503, "файл релиза не найден\n")
        log_download(ip, key_or_why, os_name, "download " + name, "ok")
        self.send_response(200)
        self.send_header("X-Accel-Redirect", ACCEL_PREFIX + name)
        self.send_header("Content-Disposition", 'attachment; filename="{}"'.format(name))
        self.send_header("X-Checksum-Sha256", info.get("sha256", ""))
        self.send_header("Content-Type", "application/octet-stream")
        self.end_headers()


def cmd_serve(_args):
    host, port = LISTEN.rsplit(":", 1)
    os.makedirs(DATA, exist_ok=True)
    srv = ThreadingHTTPServer((host, int(port)), Handler)
    srv.daemon_threads = True
    print("flovmp-dist: {} (данные {})".format(LISTEN, DATA), flush=True)
    srv.serve_forever()


def cmd_keys(args):
    with _keys_lock:
        keys = load_keys()
        if args.action == "list":
            if not keys:
                print("ключей нет — добавьте: flovmp-dist keys add FLV-XXXX-XXXX-XXXX --note \"Проект\"")
            for k, v in sorted(keys.items()):
                print("{}  {}  до {}  {}{}".format(k, ",".join(v.get("os") or OSES), v.get("expires") or "—",
                                                  v.get("note", ""), "  (отключён)" if v.get("disabled") else ""))
            return
        key = normalize_key(args.key)
        if not key:
            sys.exit("неверный формат ключа: {}".format(args.key))
        if args.action == "add":
            entry = {"note": args.note or "", "added": now_utc().date().isoformat()}
            if args.expires:
                datetime.date.fromisoformat(args.expires)
                entry["expires"] = args.expires
            if args.os:
                oses = [o.strip() for o in args.os.split(",") if o.strip()]
                bad = [o for o in oses if o not in OSES]
                if bad:
                    sys.exit("неизвестная ОС: " + ", ".join(bad))
                entry["os"] = oses
            keys[key] = entry
            save_keys(keys)
            print("добавлен {}".format(key))
        elif args.action == "remove":
            if keys.pop(key, None) is None:
                sys.exit("нет такого ключа")
            save_keys(keys)
            print("удалён {}".format(key))
        elif args.action in ("disable", "enable"):
            if key not in keys:
                sys.exit("нет такого ключа")
            keys[key]["disabled"] = args.action == "disable"
            save_keys(keys)
            print("{} {}".format("отключён" if args.action == "disable" else "включён", key))


def cmd_publish(args):
    """Папка релиза (от sign_release.py): release-<os>.txt, .sig и пакеты."""
    src = os.path.abspath(args.folder)
    need = []
    for os_name in OSES:
        txt = os.path.join(src, "release-{}.txt".format(os_name))
        if not os.path.isfile(txt):
            continue
        if not os.path.isfile(txt + ".sig"):
            sys.exit("нет подписи {}.sig".format(txt))
        info = dict(l.rstrip("\n").split("=", 1) for l in open(txt, encoding="utf-8") if "=" in l)
        pkg = os.path.join(src, info["file"])
        h = hashlib.sha256()
        with open(pkg, "rb") as fh:
            for chunk in iter(lambda: fh.read(1 << 20), b""):
                h.update(chunk)
        if h.hexdigest() != info["sha256"]:
            sys.exit("SHA-256 {} не совпадает с release-{}.txt".format(info["file"], os_name))
        need.append((os_name, info))
    if not need:
        sys.exit("в папке нет release-linux.txt / release-windows.txt")
    version = need[0][1]["version"]
    dest = os.path.join(RELEASES, version)
    os.makedirs(dest, exist_ok=True)
    for name in os.listdir(src):
        shutil.copy2(os.path.join(src, name), os.path.join(dest, name))
    tmp = CURRENT + ".new"
    if os.path.lexists(tmp):
        os.remove(tmp)
    os.symlink(dest, tmp)
    os.replace(tmp, CURRENT)
    for os_name, info in need:
        print("опубликован {} {} ({}, sha256 {}…)".format(os_name, version, info["file"], info["sha256"][:16]))


def cmd_status(_args):
    for os_name in OSES:
        _, info = release_info(os_name)
        print("{}: {}".format(os_name, "{} {}".format(info["version"], info["file"]) if info else "не опубликован"))
    print("ключей: {}".format(len(load_keys())))


def main():
    ap = argparse.ArgumentParser(description="Раздача пакетов FloV:MP по ключу лицензии")
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("serve")
    sub.add_parser("status")
    k = sub.add_parser("keys")
    k.add_argument("action", choices=["add", "remove", "list", "disable", "enable"])
    k.add_argument("key", nargs="?")
    k.add_argument("--note")
    k.add_argument("--expires", help="ГГГГ-ММ-ДД")
    k.add_argument("--os", help="linux,windows (по умолчанию обе)")
    p = sub.add_parser("publish")
    p.add_argument("folder")
    args = ap.parse_args()
    if args.cmd == "keys" and args.action != "list" and not args.key:
        ap.error("укажите ключ")
    {"serve": cmd_serve, "keys": cmd_keys, "publish": cmd_publish, "status": cmd_status}[args.cmd](args)


if __name__ == "__main__":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    main()
