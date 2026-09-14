#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
FloV:MP - пост-апдейт health-check / регрессионный тестер.

Зачем: после обновления (движка, клиента, гейммода, alt:V) быстро ответить на
вопрос "ничего не отвалилось?" - до того, как это найдут игроки.

Запуск:
    python scripts/flovmp_healthcheck.py                     # быстрые проверки
    python scripts/flovmp_healthcheck.py --build             # + сборка и юнит-тесты
    python scripts/flovmp_healthcheck.py --server 1.2.3.4    # + живой сервер
    python scripts/flovmp_healthcheck.py --all --server 1.2.3.4:7788

Код возврата: 0 - критичных провалов нет; 1 - есть FAIL (годится для CI).
Зависимостей нет: только стандартная библиотека.
"""
from __future__ import annotations

import argparse
import json
import re
import socket
import subprocess
import sys
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path

# Консоль Windows по умолчанию cp1251 - принудительно переводим вывод в UTF-8,
# иначе русский текст отчёта превращается в кракозябры.
try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except (AttributeError, OSError):
    pass

ROOT = Path(__file__).resolve().parent.parent

PASS, WARN, FAIL, SKIP = "PASS", "WARN", "FAIL", "SKIP"


@dataclass
class Report:
    results: list = field(default_factory=list)

    def add(self, status, name, detail=""):
        self.results.append((status, name, detail))
        line = "  [{:<4}] {}".format(status, name)
        if detail:
            line += " - " + detail
        print(line)

    @property
    def failures(self):
        return sum(1 for r in self.results if r[0] == FAIL)

    @property
    def warnings(self):
        return sum(1 for r in self.results if r[0] == WARN)


def section(title):
    print("\n=== {} ===".format(title))


# --------------------------- 1. Структура и файлы ---------------------------

REQUIRED_PATHS = [
    "server/src/FloVMP.Core/FloVMP.Core.csproj",
    "server/src/FloVMP.Gamemode/FloVMP.Gamemode.csproj",
    "server/src/FloVMP.Starter/FloVMP.Starter.csproj",
    "server/tests/FloVMP.Core.Tests/FloVMP.Core.Tests.csproj",
    "launcher/src/FloVMP.Connect/FloVMP.Connect.csproj",
    "client/resources/flovmp-client/client/index.js",
]


def check_structure(rep):
    section("1. Структура проекта")
    for rel in REQUIRED_PATHS:
        p = ROOT / rel
        rep.add(PASS if p.exists() else FAIL, "есть " + rel,
                "" if p.exists() else "ФАЙЛ ОТСУТСТВУЕТ")

    nui = ROOT / "client/resources/flovmp-client/client/html"
    if nui.is_dir():
        pages = sorted(p.name for p in nui.iterdir() if p.is_dir())
        rep.add(PASS if pages else WARN, "NUI-страницы", ", ".join(pages) or "не найдены")
    else:
        rep.add(WARN, "NUI-страницы", "каталог html отсутствует")


# ------------------- 2. Регрессии безопасности (главное) -------------------

# Каждый паттерн когда-то был РЕАЛЬНОЙ дырой. Проверка не даёт им вернуться
# при рефакторинге, мерже или копипасте из старой ветки.
SECURITY_PATTERNS = [
    (r'player\.Name\s*,\s*"shize5"',
     "захардкоженный ник даёт права Основателя"),
    (r'SocialClubId\s*==\s*\d{6,}',
     "захардкоженный SocialClubId = бэкдор в поставляемом клиентам коде"),
    (r'\?\?\s*"flovmp2026"',
     "дефолтный админ-пароль в исходниках"),
    (r'AutoClaimFirstPlayer\s*\{\s*get;\s*set;\s*\}\s*=\s*true',
     "первый зашедший игрок автоматически становится Основателем"),
    (r'AllowNameBasedAdmin\s*\{\s*get;\s*set;\s*\}\s*=\s*true',
     "права по нику (ник задаётся клиентом и подделывается)"),
]

SKIP_DIRS = {"obj", "bin", "node_modules", ".git", "runtime", "dist", "native-dist"}


def iter_sources(exts=(".cs", ".js")):
    for base in ("server/src", "launcher/src", "client"):
        root = ROOT / base
        if not root.exists():
            continue
        for p in root.rglob("*"):
            if p.suffix in exts and not any(s in p.parts for s in SKIP_DIRS):
                yield p


def check_security_regressions(rep):
    section("2. Регрессии безопасности (бэкдоры не должны вернуться)")
    sources = list(iter_sources())
    rep.add(PASS if sources else WARN, "просканировано файлов", str(len(sources)))

    for pattern, why in SECURITY_PATTERNS:
        rx = re.compile(pattern)
        hits = []
        for p in sources:
            try:
                text = p.read_text(encoding="utf-8", errors="ignore")
            except OSError:
                continue
            for i, line in enumerate(text.splitlines(), 1):
                stripped = line.strip()
                # комментарии с описанием прошлых дыр - не находка
                if stripped.startswith("//") or stripped.startswith("*") or stripped.startswith("///"):
                    continue
                if rx.search(line):
                    hits.append("{}:{}".format(p.relative_to(ROOT), i))
        rep.add(FAIL if hits else PASS, why, "; ".join(hits[:3]) if hits else "чисто")

    starter = ROOT / "server/src/FloVMP.Starter/StarterResource.cs"
    if starter.exists():
        txt = starter.read_text(encoding="utf-8", errors="ignore")
        tail = txt.split("FLOVMP_ADMIN_PASSWORD", 1)[1][:80] if "FLOVMP_ADMIN_PASSWORD" in txt else ""
        ok = bool(tail) and '?? "' not in tail
        rep.add(PASS if ok else FAIL, "админ-пароль без небезопасного дефолта",
                "" if ok else "есть дефолт - /alogin открыт всем, кто знает строку")


# ------------- 2b. Согласованность админ-команд -------------

def check_admin_commands(rep):
    """Обработчик без регистрации = команда мертва ('неизвестная команда').
    Регистрация без обработчика = админ вводит, молча ничего не происходит.
    И то, и другое всплывает в бою, когда админ пытается снять нарушителя."""
    section("2b. Согласованность админ-команд")
    chat_p = ROOT / "server/src/FloVMP.Gamemode/Systems/Chat/ChatSystem.cs"
    reg_p = ROOT / "server/src/FloVMP.Core/Admin/AdminCommandRegistry.cs"
    if not (chat_p.exists() and reg_p.exists()):
        rep.add(SKIP, "реестр команд", "файлы не найдены")
        return

    chat = chat_p.read_text(encoding="utf-8", errors="ignore")
    reg = reg_p.read_text(encoding="utf-8", errors="ignore")
    registered = set(re.findall(r'^\s*Register\("([^"]+)"', reg, re.M))
    marker = "private void HandleAdminCommand"
    if marker not in chat:
        rep.add(WARN, "HandleAdminCommand", "не найден - структура изменилась")
        return
    cases = set(re.findall(r'case\s+"([a-z0-9_]+)"\s*:', chat[chat.find(marker):]))

    dead = sorted(cases - registered)
    noimpl = sorted(registered - cases)
    rep.add(PASS if not dead else FAIL, "нет мёртвых команд (обработчик без регистрации)",
            ", ".join(dead) if dead else "чисто")
    rep.add(PASS if not noimpl else FAIL, "нет команд без обработчика (реклама в /ahelp впустую)",
            ", ".join(noimpl) if noimpl else "чисто")
    rep.add(PASS, "всего админ-команд", str(len(registered)))


# ------------------- 3. Горячий путь (блокировка тика) -------------------

def check_hot_path(rep):
    section("3. Горячий путь (не блокирует ли игровой тик)")
    store = ROOT / "server/src/FloVMP.Core/Auth/JsonAccountStore.cs"
    if not store.exists():
        rep.add(SKIP, "JsonAccountStore", "файл не найден")
        return
    txt = store.read_text(encoding="utf-8", errors="ignore")
    debounced = "_dirty" in txt and "FlushIfDirty" in txt
    rep.add(PASS if debounced else FAIL, "запись аккаунтов асинхронная",
            "" if debounced else "Update() пишет файл синхронно -> фризы тика при тысячах игроков")
    has_flush = "public void Flush()" in txt and "Dispose" in txt
    rep.add(PASS if has_flush else FAIL, "есть Flush/Dispose (нет потери данных)",
            "" if has_flush else "фоновая запись без гарантии сброса на остановке")

    # Вход/регистрация: чтение БД + PBKDF2 (120k итераций) не должны считаться
    # на главном потоке - это почти целый тик на каждый вход.
    auth = ROOT / "server/src/FloVMP.Gamemode/Systems/Auth/AuthSystem.cs"
    if auth.exists():
        a = auth.read_text(encoding="utf-8", errors="ignore")
        offloaded = "Task.Run" in a and "_completed" in a and "public void Pump()" in a
        rep.add(PASS if offloaded else FAIL, "вход/регистрация не на главном потоке",
                "" if offloaded else "PBKDF2+БД считаются в обработчике alt:V -> фриз тика на каждый вход")
        gm = ROOT / "server/src/FloVMP.Gamemode/GamemodeResource.cs"
        pumped = gm.exists() and "_auth?.Pump()" in gm.read_text(encoding="utf-8", errors="ignore")
        rep.add(PASS if pumped else FAIL, "Pump() подключён к OnTick",
                "" if pumped else "результаты входа никогда не применятся - игроки зависнут на авторизации")


# --------------------------- 4. Сборка и тесты ---------------------------

def run_cmd(cmd, timeout=900):
    try:
        r = subprocess.run(cmd, cwd=str(ROOT), capture_output=True, text=True,
                           timeout=timeout, encoding="utf-8", errors="replace")
        return r.returncode, (r.stdout or "") + (r.stderr or "")
    except FileNotFoundError:
        return 127, "команда не найдена (нет dotnet в PATH?)"
    except subprocess.TimeoutExpired:
        return 124, "таймаут"


def check_build_and_tests(rep):
    section("4. Сборка и юнит-тесты")
    projects = [
        "server/src/FloVMP.Core/FloVMP.Core.csproj",
        "server/src/FloVMP.Gamemode/FloVMP.Gamemode.csproj",
        "server/src/FloVMP.Starter/FloVMP.Starter.csproj",
    ]
    for proj in projects:
        code, out = run_cmd(["dotnet", "build", proj, "-c", "Release", "--nologo", "-v", "q"])
        errs = [l for l in out.splitlines() if "error CS" in l]
        rep.add(PASS if code == 0 else FAIL, "сборка " + Path(proj).stem,
                "" if code == 0 else (errs[0][:160] if errs else out.strip()[:160]))

    code, out = run_cmd(["dotnet", "test",
                         "server/tests/FloVMP.Core.Tests/FloVMP.Core.Tests.csproj",
                         "-c", "Release", "--nologo", "-v", "q"])
    # ВНИМАНИЕ: "пройдено" встречается и внутри "не пройдено" — берём только
    # число, перед которым НЕТ "не ", иначе отчёт врёт про 0 тестов.
    passed = re.search(r"(?<!не )пройдено\s+(\d+)", out) or re.search(r"Passed:\s+(\d+)", out)
    failed = re.search(r"не пройдено\s+(\d+)", out) or re.search(r"Failed:\s+(\d+)", out)
    detail = "пройдено {}{}".format(
        passed.group(1) if passed else "?",
        ", провалено " + failed.group(1) if failed and failed.group(1) != "0" else ""
    ) if (passed or failed) else out.strip()[:160]
    rep.add(PASS if code == 0 else FAIL, "юнит-тесты", detail)


# ------------- 5. Согласованность версий клиента и сервера -------------

def check_version_consistency(rep):
    section("5. Согласованность версий клиента и сервера")
    # Историческая грабля: клиент и сервер разных билдов -> WRONG_STABLE_BUILD
    upd = ROOT / "runtime/client/update.json"
    if not upd.exists():
        rep.add(SKIP, "runtime/client/update.json", "нет локального рантайма клиента")
        return
    try:
        data = json.loads(upd.read_text(encoding="utf-8", errors="ignore"))
    except json.JSONDecodeError as e:
        rep.add(FAIL, "update.json разбирается", str(e))
        return

    ver = data.get("version")
    rep.add(PASS if ver else WARN, "версия клиента (runtime/client)", str(ver))

    dll = ROOT / "runtime/client/altv-client.dll"
    if dll.exists():
        size = dll.stat().st_size
        want = (data.get("sizeList") or {}).get("altv-client.dll")
        ok = want is None or size == want
        rep.add(PASS if ok else FAIL, "altv-client.dll соответствует манифесту",
                "{} байт".format(size) if ok else
                "{} != ожидалось {} - клиент не той версии (риск WRONG_STABLE_BUILD)".format(size, want))
    else:
        rep.add(WARN, "altv-client.dll", "не найден локально")


# --------------------------- 6. Живой сервер ---------------------------

def check_live_server(rep, target):
    section("6. Живой сервер ({})".format(target))
    host, _, port_s = target.partition(":")
    game_port = int(port_s) if port_s else 7788

    try:
        with socket.create_connection((host, game_port), timeout=5):
            rep.add(PASS, "игровой порт {} принимает TCP".format(game_port))
    except OSError as e:
        rep.add(FAIL, "игровой порт {}".format(game_port), "недоступен: {}".format(e))

    for api_port in (7799, 80):
        url = ("http://{}:{}/info".format(host, api_port) if api_port != 80
               else "http://{}/info".format(host))
        try:
            with urllib.request.urlopen(url, timeout=6) as r:
                payload = json.loads(r.read().decode("utf-8", "replace"))
            rep.add(PASS, "/info отвечает ({})".format(api_port),
                    "online={} players={}/{}".format(payload.get("online"),
                                                     payload.get("players"),
                                                     payload.get("maxPlayers")))
            if payload.get("online") is not True:
                rep.add(FAIL, "сервер сообщает online=false")
            return
        except Exception:
            continue
    rep.add(WARN, "/info", "не ответил ни на :7799, ни на :80 "
                           "(если API закрыт снаружи за nginx - это нормально)")


# --------------------------------- main ---------------------------------

def main():
    ap = argparse.ArgumentParser(description="FloV:MP health-check после обновления")
    ap.add_argument("--build", action="store_true", help="собрать проекты и прогнать юнит-тесты")
    ap.add_argument("--server", metavar="HOST[:PORT]", help="проверить живой сервер")
    ap.add_argument("--all", action="store_true", help="все проверки")
    args = ap.parse_args()

    print("FloV:MP health-check")
    print("Корень проекта: {}".format(ROOT))

    rep = Report()
    check_structure(rep)
    check_security_regressions(rep)
    check_admin_commands(rep)
    check_hot_path(rep)
    check_version_consistency(rep)

    if args.build or args.all:
        check_build_and_tests(rep)
    else:
        section("4. Сборка и юнит-тесты")
        rep.add(SKIP, "пропущено", "добавь --build")

    if args.server:
        check_live_server(rep, args.server)
    else:
        section("6. Живой сервер")
        rep.add(SKIP, "пропущено", "добавь --server <host>")

    print("\n" + "=" * 62)
    print("ИТОГ: {} проверок, провалов: {}, предупреждений: {}".format(
        len(rep.results), rep.failures, rep.warnings))
    if rep.failures:
        print("\nПРОВАЛЕНО (чинить до запуска):")
        for st, name, detail in rep.results:
            if st == FAIL:
                print("  - {}{}".format(name, " - " + detail if detail else ""))
        return 1
    print("Критичных провалов нет.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
