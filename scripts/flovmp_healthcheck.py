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
import os
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
    # Открытый CORS на API, где живёт POST /api/auth/login: любой сайт мог бы
    # подбирать пароли браузерами посетителей, размазывая перебор по чужим IP
    # и обходя ограничение частоты. Браузерный доступ сюда никому не нужен.
    ("Access-Control-Allow-Origin\", \"*\"",
     "открытый CORS на auth-API - подбор паролей чужими браузерами"),
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
    (r'Set(Stream)?SyncedMetaData\("(adminLevel|admin_level|username)"',
     "уровень админа / логин в meta, видимой всем клиентам (карта администрации для читеров)"),
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

    # События от клиента — то, что может прислать поддельный клиент. Каждый
    # обработчик обязан либо проверять права на сервере, либо быть в списке
    # заведомо общедоступных. Проверка появилась после реального случая:
    # правка сняла проверку прав с обработчика телепорта, и телепортироваться
    # смог любой игрок — ни в одном логе это не отражалось.
    PUBLIC_CLIENT_EVENTS = {
        "OnChatMessage": "чат доступен всем игрокам",
        "OnClientReady": "сигнал готовности клиента, защищён от повторов",
    }
    starter_src = ROOT / "server/src/FloVMP.Starter/StarterResource.cs"
    if starter_src.exists():
        src = starter_src.read_text(encoding="utf-8", errors="ignore")
        handlers = set(re.findall(r'Alt\.OnClient<[^>]*>\("[^"]+",\s*([A-Za-z_][A-Za-z0-9_]*)\)', src))
        unguarded = []
        for h in sorted(handlers):
            if h in PUBLIC_CLIENT_EVENTS:
                continue
            m = re.search(r'private\s+(?:async\s+)?void\s+' + re.escape(h) + r'\s*\([^)]*\)\s*\{', src)
            if not m:
                unguarded.append(h + " (тело не найдено)")
                continue
            # Тело метода: до начала следующего члена класса. Иначе окно
            # захватывает соседний метод, и проверка «видит» чужую защиту.
            tail = src[m.end():]
            nxt = re.search(r"\n    (?:private|public|internal|protected)\s", tail)
            body = tail[:nxt.start()] if nxt else tail[:2000]
            if "MayUse(" not in body and "IsAdmin(" not in body:
                unguarded.append(h)
        rep.add(PASS if not unguarded else FAIL,
                "обработчики событий клиента проверяют права",
                ", ".join(unguarded) if unguarded else
                "проверено обработчиков: {}".format(len(handlers)))

    # Команда, которую сервер знает, но реестр прав не описывает, проверяется
    # только внутри обработчика — то есть её уровень нельзя настроить файлом.
    reg = ROOT / "server/src/FloVMP.Core/Admin/AdminCommandRegistry.cs"
    starter = ROOT / "server/src/FloVMP.Starter/StarterResource.cs"
    if reg.exists() and starter.exists():
        registered = set(re.findall(r'Register\("([^"]+)"', reg.read_text(encoding="utf-8", errors="ignore")))
        s_txt = starter.read_text(encoding="utf-8", errors="ignore")
        marker = "private void HandleCommand"
        handled = set(re.findall(r'case "([a-z0-9_]+)":', s_txt[s_txt.find(marker):])) if marker in s_txt else set()
        ghost = sorted(registered - handled)
        rep.add(PASS if not ghost else FAIL, "у каждой команды из реестра есть обработчик",
                ", ".join(ghost) if ghost else "чисто")
        leftover = sorted(set(re.findall(r'IsAdmin\(player, (\d+)\)', s_txt)))
        rep.add(PASS if not leftover else WARN, "нет проверок уровня мимо реестра",
                "" if not leftover else "уровни зашиты в обработчиках: " + ", ".join(leftover))



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

    # Регистрация не должна переписывать accounts.json целиком: стоимость
    # растёт с числом учёток (замерено 9.21 мс при 1500, 0.16 мс с журналом).
    journal = "AppendToJournalLocked" in txt and "_journalPath" in txt
    rep.add(PASS if journal else FAIL, "регистрация не переписывает весь файл",
            "" if journal else "Create() делает полный сброс -> O(n) на каждую регистрацию")

    # Вывод Core мимо Alt.Log. В server.log alt:V попадает только то, что
    # прошло через Alt.Log, а обычный Console.WriteLine из ресурса теряется
    # целиком. Так терялись ошибки записи аккаунтов, восстановление из журнала,
    # ошибки сохранения админов и сигнал о переборе токена владельца.
    raw_console = []
    for f in (ROOT / "server/src/FloVMP.Core").rglob("*.cs"):
        if f.name == "CoreConsole.cs" or "obj" in f.parts or "bin" in f.parts:
            continue
        for i, line in enumerate(f.read_text(encoding="utf-8", errors="ignore").splitlines(), 1):
            if re.search(r"(?<![\w.])Console\.(Error\.)?Write", line):
                raw_console.append("{}:{}".format(f.name, i))
    rep.add(PASS if not raw_console else FAIL, "вывод Core идёт в лог сервера",
            "" if not raw_console else
            "мимо Alt.Log (не попадёт в server.log): " + ", ".join(raw_console[:5]))

    # Запросы соседей делаются для каждого игрока каждый тик: перегрузка с
    # буфером убирает десятки мегабайт мусора и паузы сборщика в тике.
    grid = ROOT / "server/src/FloVMP.Core/Spatial/SpatialHashGrid.cs"
    if grid.exists():
        g = grid.read_text(encoding="utf-8", errors="ignore")
        buffered = "FindInRadiusWithPositions" in g and "List<T> results" in g
        rep.add(PASS if buffered else FAIL, "запросы соседей без аллокаций",
                "" if buffered else "каждый запрос выделяет новый список -> паузы GC в игровом тике")


# ------------- 3b. Блокировки: обещание команды = реальность -------------

def check_bans(rep):
    section("3b. Блокировки (бан по IP/HWID должен реально банить)")

    svc = ROOT / "server/src/FloVMP.Core/Security/MultiTierBanService.cs"
    if not svc.exists():
        rep.add(FAIL, "MultiTierBanService", "файл не найден")
        return

    s_txt = svc.read_text(encoding="utf-8", errors="ignore")
    persisted = "IBanStore" in s_txt and "Persist(" in s_txt
    rep.add(PASS if persisted else FAIL, "баны сохраняются в хранилище",
            "" if persisted else "баны только в памяти -> рестарт снимает все блокировки")

    refresh = "RefreshFromStore" in s_txt
    rep.add(PASS if refresh else WARN, "дозагрузка банов с других инстансов",
            "" if refresh else "при горизонтали бан с соседнего сервера не дойдёт")

    # Главное обещание: команды с идентификаторами обязаны создавать запись
    # с этими идентификаторами, а не только ставить флаг на аккаунте.
    for path, label in (
        ("server/src/FloVMP.Starter/StarterResource.cs", "базовая платформа"),
    ):
        f = ROOT / path
        if not f.exists():
            rep.add(SKIP, "команды бана ({})".format(label), "файл не найден")
            continue
        t = f.read_text(encoding="utf-8", errors="ignore")
        real = "CreateBan(" in t and "hwidHash:" in t
        rep.add(PASS if real else FAIL, "команды бана пишут идентификаторы ({})".format(label),
                "" if real else "бан по IP/HWID ставит флаг только на аккаунте - читер вернётся с новой учётки")

        enforced = "RejectIfBanned" in t or "CheckConnection(" in t
        rep.add(PASS if enforced else FAIL, "бан проверяется при входе ({})".format(label),
                "" if enforced else "блокировка записывается, но вход не проверяется - бан не работает")


# ------------- 3f. Контракт событий клиент <-> сервер -------------

# События, которые сервер шлёт, но клиент намеренно не обрабатывает. Каждое
# обязано иметь причину: иначе это та самая молчаливая дыра.
KNOWN_UNHANDLED_SERVER_EVENTS = {}


def check_event_contract(rep):
    """
    Сверка «что шлёт одна сторона» против «что слушает другая».

    Событие, которое одна сторона шлёт, а другая не слушает, не даёт ошибки
    ни в логе сервера, ни в логе клиента — функция просто молча не работает.
    """
    section("3f. Контракт событий клиент <-> сервер")

    client_dir = ROOT / "client/resources/flovmp-client/client"
    server_dir = ROOT / "server/src"
    if not client_dir.is_dir() or not server_dir.is_dir():
        rep.add(SKIP, "контракт событий", "каталоги клиента или сервера не найдены")
        return

    def scan(root, exts, pattern):
        found = set()
        for f in root.rglob("*"):
            if f.suffix not in exts or "node_modules" in f.parts or "obj" in f.parts:
                continue
            try:
                found.update(re.findall(pattern, f.read_text(encoding="utf-8", errors="ignore")))
            except OSError:
                continue
        return found

    # Alt.Emit — событие между серверными ресурсами (API для модов), не клиенту.
    server_emits = scan(server_dir, {".cs"}, r'(?<!Alt)\.Emit\(\s*"([^"]+)"')
    server_listens = scan(server_dir, {".cs"}, r'OnClient(?:<[^>]*>)?\(\s*"([^"]+)"')
    client_emits = scan(client_dir, {".js"}, r"emitServer\(\s*'([^']+)'")
    client_listens = scan(client_dir, {".js"}, r"onServer\(\s*'([^']+)'")

    rep.add(PASS, "событий найдено",
            "сервер шлёт {}, слушает {}; клиент шлёт {}, слушает {}".format(
                len(server_emits), len(server_listens), len(client_emits), len(client_listens)))

    # Клиент шлёт, сервер не слушает: действие игрока уходит в пустоту.
    lost_actions = sorted(client_emits - server_listens)
    rep.add(PASS if not lost_actions else FAIL,
            "каждое действие клиента обрабатывается сервером",
            "" if not lost_actions else "уходят в пустоту: " + ", ".join(lost_actions))

    # Сервер шлёт, клиент не слушает: данные не доходят до игрока.
    unheard = sorted(server_emits - client_listens)
    unexplained = [e for e in unheard if e not in KNOWN_UNHANDLED_SERVER_EVENTS]
    known = [e for e in unheard if e in KNOWN_UNHANDLED_SERVER_EVENTS]

    rep.add(PASS if not unexplained else FAIL,
            "каждое событие сервера доходит до клиента",
            "" if not unexplained else "клиент не слушает: " + ", ".join(unexplained))

    for e in known:
        rep.add(WARN, "известная недоделка: " + e, KNOWN_UNHANDLED_SERVER_EVENTS[e])

    # Исключение, которое больше не нужно, — тоже ошибка: список должен
    # отражать реальность, а не прошлые долги.
    stale = sorted(k for k in KNOWN_UNHANDLED_SERVER_EVENTS if k not in unheard)
    rep.add(PASS if not stale else WARN, "список известных недоделок актуален",
            "" if not stale else "уже обрабатываются, убрать из списка: " + ", ".join(stale))


# ------------- 3e. Порядок ключей в server.toml (тихий убийца) -------------

def check_server_toml_order(rep):
    """
    В TOML всё, что идёт ПОСЛЕ заголовка [table], принадлежит этой таблице.
    Если modules/resources оказались ниже [threads], сервер читает их как
    threads.modules и threads.resources — то есть не читает вовсе.

    Чем это опасно: сервер стартует УСПЕШНО. В логе ни одной ошибки, "Server
    started", порт слушается — и ни одного ресурса. Пустой мир, игрок заходит
    и висит. Именно так и было в runtime/server/server.toml: C#-ресурс не
    загружался вообще, и это невозможно заметить по логу.
    """
    section("3e. Порядок ключей в server.toml")

    TOP_LEVEL = ("modules", "resources", "name", "host", "port", "players",
                 "gamemode", "announce", "debug")

    checked = 0
    for rel in ("config/server.toml", "runtime/server/server.toml"):
        f = ROOT / rel
        if not f.exists():
            continue
        checked += 1

        first_table = None
        misplaced = []
        for i, raw in enumerate(f.read_text(encoding="utf-8", errors="ignore").splitlines(), 1):
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            if line.startswith("["):
                if first_table is None:
                    first_table = (i, line)
                continue
            if first_table is None:
                continue
            key = line.split("=")[0].strip()
            if key in TOP_LEVEL:
                misplaced.append((i, key, first_table[1]))

        if misplaced:
            detail = "; ".join(
                "{} (строка {}) попал внутрь {}".format(k, i, t) for i, k, t in misplaced)
            rep.add(FAIL, "{}: ключи верхнего уровня на месте".format(rel),
                    detail + " - сервер стартует БЕЗ них и не пишет ни одной ошибки")
        else:
            rep.add(PASS, "{}: ключи верхнего уровня на месте".format(rel))

    if checked == 0:
        rep.add(SKIP, "server.toml", "ни одного файла не найдено")


# ------------- 3d. Голосовой чат: три места, где он отваливался -------------

def check_voice(rep):
    section("3d. Голосовой чат (сквозная связность)")

    # 1. Конфиг игрового сервера. Секции [voice] тут не было вовсе: голосовой
    # сервер разворачивался и запускался службой, а игровой о нём не знал.
    cfg = ROOT / "config/server.toml"
    if cfg.exists():
        c = cfg.read_text(encoding="utf-8", errors="ignore")
        has_section = "[voice]" in c
        rep.add(PASS if has_section else FAIL, "секция [voice] в server.toml",
                "" if has_section else "игровой сервер не знает про голосовой -> голос не работает нигде")
        if has_section:
            keys_ok = all(k in c for k in ("externalHost", "externalPort", "externalSecret"))
            rep.add(PASS if keys_ok else FAIL, "ключи внешнего голосового сервера",
                    "" if keys_ok else "нужны externalHost/externalPort/externalSecret")
    else:
        rep.add(SKIP, "config/server.toml", "файл не найден")

    # 2. Клиент. Жёсткое voiceEnabled = false сводило на нет любую настройку
    # сервера: канал есть, а клиенту голос выключен.
    toml = ROOT / "launcher/src/FloVMP.Connect/AltvToml.cs"
    if toml.exists():
        t = toml.read_text(encoding="utf-8", errors="ignore")
        hard_off = "voiceEnabled = false" in t
        rep.add(FAIL if hard_off else PASS, "голос не выключен жёстко в клиенте",
                "voiceEnabled = false зашит -> игроки не услышат друг друга" if hard_off else "")
    else:
        rep.add(SKIP, "AltvToml.cs", "файл не найден")

    # 3. Сервер обязан создавать голосовой канал.
    for path, label in (
        ("server/src/FloVMP.Starter/StarterResource.cs", "базовая платформа"),
    ):
        f = ROOT / path
        if not f.exists():
            rep.add(SKIP, "голосовой канал ({})".format(label), "файл не найден")
            continue
        t = f.read_text(encoding="utf-8", errors="ignore")
        creates = "CreateVoiceChannel" in t
        joins = "AddPlayer" in t and "RemovePlayer" in t
        rep.add(PASS if creates else FAIL, "голосовой канал создаётся ({})".format(label),
                "" if creates else "в этом режиме голоса нет вообще")
        rep.add(PASS if joins else FAIL, "игроки входят и выходят из канала ({})".format(label),
                "" if joins else "без RemovePlayer отключившиеся копятся в канале")

    # 4. Мут голоса. Без него замученный за оскорбления игрок спокойно
    # продолжает кричать в микрофон, и мут выглядит нерабочим.
    starter = ROOT / "server/src/FloVMP.Starter/StarterResource.cs"
    muted = starter.exists() and "MutePlayer" in starter.read_text(encoding="utf-8", errors="ignore")
    rep.add(PASS if muted else FAIL, "голос можно заглушить",
            "" if muted else "мут глушит только текст - нарушитель продолжает говорить")

    # 5. Установщик обязан связать оба конца: одинаковый секрет в server.toml и
    # voice.toml, и ПУБЛИЧНЫЙ адрес для клиента (с 127.0.0.1 голоса не будет).
    dep = ROOT / "scripts/install.sh"
    if dep.exists():
        d = dep.read_text(encoding="utf-8", errors="ignore")
        tpl = ROOT / "scripts/package-templates/common/voice/voice.toml.example"
        t = tpl.read_text(encoding="utf-8", errors="ignore") if tpl.exists() else ""
        wired = "voice.toml" in d and "__FLOVMP_VOICE_SECRET__" in d and "secret = __FLOVMP_VOICE_SECRET__" in t
        rep.add(PASS if wired else FAIL, "установщик связывает игровой и голосовой серверы",
                "" if wired else "голосовой сервер ставится, но не подключается к игровому")
        public = "__FLOVMP_VOICE_PUBLIC_HOST__" in d and "api.ipify.org" in d
        rep.add(PASS if public else WARN, "клиенту отдаётся публичный адрес голоса",
                "" if public else "с 127.0.0.1 игроки молча останутся без голоса")


# ------------- 3d. Пакет и установщик -------------

def check_client_natives(rep):
    """Нативы alt:V чувствительны к регистру, а вызовы в клиенте обёрнуты в try:
    опечатка в имени не даёт ошибки — функция просто молча не работает
    (так /weather годами не менял погоду). Сверяем с полным списком имён."""
    section("5b. Клиент: только существующие нативы alt:V")
    names_p = ROOT / "scripts/data/altv-natives.txt"
    if not names_p.exists():
        rep.add(SKIP, "список нативов", "нет scripts/data/altv-natives.txt")
        return
    known = {l.strip() for l in names_p.read_text(encoding="utf-8").splitlines()
             if l.strip() and not l.startswith("#")}
    client_dir = ROOT / "client/resources/flovmp-client/client"
    used = {}
    for f in client_dir.rglob("*.js"):
        for m in re.finditer(r"\bnative\.([A-Za-z0-9_]+)\s*\(", f.read_text(encoding="utf-8", errors="ignore")):
            used.setdefault(m.group(1), f.name)
    unknown = sorted(n for n in used if n not in known)
    rep.add(PASS if not unknown else FAIL, "вызываемые нативы существуют ({} шт.)".format(len(used)),
            ", ".join(unknown) if unknown else "")


def check_installer(rep):
    section("3d. Пакет и установщик")
    shell = [ROOT / "scripts/install.sh"] + sorted((ROOT / "scripts/package-templates").rglob("*.sh"))
    crlf = [str(p.relative_to(ROOT)) for p in shell if p.exists() and b"\r\n" in p.read_bytes()]
    rep.add(PASS if not crlf else FAIL, "sh-скрипты без CRLF",
            "" if not crlf else "на Linux не запустятся: " + ", ".join(crlf))

    inst = ROOT / "scripts/install.sh"
    if not inst.exists():
        rep.add(FAIL, "scripts/install.sh", "установщик не найден")
        return
    t = inst.read_text(encoding="utf-8", errors="ignore")
    rep.add(PASS if "Uid=root" not in t and "Pwd=;" not in t else FAIL,
            "установщик не подключает сервер к базе как root без пароля",
            "" if "Uid=root" not in t else "root по TCP с пустым паролем не пускается - сервер уходит на файлы")
    rep.add(PASS if "IDENTIFIED BY" in t and "random_hex" in t else FAIL,
            "установщик создаёт отдельного пользователя базы со случайным паролем")
    rep.add(PASS if "sha256sum --quiet -c manifest.txt" in t else FAIL,
            "установщик проверяет целостность пакета")
    rep.add(PASS if "Миграции не выполнены" in t else FAIL,
            "установщик ловит упавшие миграции",
            "" if "Миграции не выполнены" in t else "«база подключена» при несозданных таблицах")
    rep.add(PASS if "RSA2048_OFFLINE_VERIFIED" not in t else FAIL, "нет поддельной лицензии в установщике")

    pack = ROOT / "scripts/pack_server.py"
    if pack.exists():
        pt = pack.read_text(encoding="utf-8", errors="ignore")
        guard = "FORBIDDEN_IN_PACKAGE" in pt and "admins.json" in pt
        rep.add(PASS if guard else FAIL, "admins.json и настройки владельца не попадают в пакет")
    else:
        rep.add(FAIL, "scripts/pack_server.py", "сборщик пакета не найден")


# ------------- 3c. Миграции схемы БД -------------

def check_migrations(rep):
    section("3c. Миграции схемы БД")

    mig_dir = ROOT / "sql/migrations"
    if not mig_dir.is_dir():
        rep.add(FAIL, "каталог sql/migrations", "схема не накатывается из кода")
        return

    files = sorted(mig_dir.glob("*.sql"))
    rep.add(PASS if files else FAIL, "миграции найдены ({})".format(len(files)),
            "" if files else "каталог пуст")

    # CREATE DATABASE / USE внутри миграции увели бы накат в чужую базу мимо
    # FLOVMP_DB_NAME, а сервер продолжил бы работать с пустой.
    bad = []
    versions = {}
    for f in files:
        t = f.read_text(encoding="utf-8", errors="ignore")
        for line in t.splitlines():
            head = line.strip().upper()
            if head.startswith("USE ") or head.startswith("CREATE DATABASE"):
                bad.append(f.name)
                break
        digits = ""
        for ch in f.name:
            if ch.isdigit():
                digits += ch
            else:
                break
        versions.setdefault(digits, []).append(f.name)

    rep.add(PASS if not bad else FAIL, "нет CREATE DATABASE/USE в миграциях",
            "" if not bad else "увели бы накат в чужую базу: " + ", ".join(bad))

    # Дубль колонки в CREATE TABLE валит миграцию на ЧИСТОЙ базе (код 1060),
    # а на уже заполненной CREATE TABLE IF NOT EXISTS её не выполняет — так
    # ошибка прожила до первой настоящей установки у клиента.
    dup_cols = []
    for f in files:
        if not f.exists():
            continue
        t = f.read_text(encoding="utf-8", errors="ignore")
        for m in re.finditer(r"CREATE TABLE IF NOT EXISTS `(\w+)` \((.*?)\n\)\s*ENGINE", t, re.S):
            cols = re.findall(r"(?m)^\s*`(\w+)`\s+[A-Za-z]", m.group(2))
            for c in sorted({c for c in cols if cols.count(c) > 1}):
                dup_cols.append("{}: {}.{}".format(f.name, m.group(1), c))
    rep.add(PASS if not dup_cols else FAIL, "нет повторяющихся колонок в CREATE TABLE",
            "" if not dup_cols else "миграция упадёт на чистой базе: " + ", ".join(dup_cols))

    dupes = [v for v, names in versions.items() if len(names) > 1]
    rep.add(PASS if not dupes else FAIL, "номера миграций уникальны",
            "" if not dupes else "дубли номеров: " + ", ".join(dupes))

    runner = ROOT / "server/src/FloVMP.Core/Database/MigrationRunner.cs"
    if runner.exists():
        r = runner.read_text(encoding="utf-8", errors="ignore")
        locked = "GET_LOCK" in r
        rep.add(PASS if locked else FAIL, "накат защищён блокировкой",
                "" if locked else "два инстанса мигрируют одновременно -> гонка на схеме")
        wired = "RunMigrations" in (ROOT / "server/src/FloVMP.Core/Database/AccountStoreFactory.cs")\
            .read_text(encoding="utf-8", errors="ignore")
        rep.add(PASS if wired else FAIL, "миграции подключены к старту",
                "" if wired else "раннер есть, но никто его не вызывает")
    else:
        rep.add(FAIL, "MigrationRunner", "файл не найден - схема накатывается руками")

    # Миграции обязаны доезжать до сервера: раннер ищет sql/migrations рядом
    # с рабочей папкой, и без копии в сборочном скрипте схема не накатится.
    for script, label in (("scripts/assemble-runtime.ps1", "локальный runtime"),
                          ("scripts/pack_server.py", "пакет для клиента")):
        sc = ROOT / script
        if not sc.exists():
            rep.add(SKIP, "миграции в пакете ({})".format(label), "скрипт не найден")
            continue
        packed = "sql" in sc.read_text(encoding="utf-8", errors="ignore") and \
                 "migrations" in sc.read_text(encoding="utf-8", errors="ignore")
        rep.add(PASS if packed else FAIL, "миграции кладутся в пакет ({})".format(label),
                "" if packed else "сервер стартует без миграций и молча уходит на JSON")


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
            "server/src/FloVMP.Starter/FloVMP.Starter.csproj",
    ]
    for proj in projects:
        code, out = run_cmd(["dotnet", "build", proj, "-c", "Release", "--nologo", "-v", "q"])
        errs = [l for l in out.splitlines() if "error CS" in l]
        rep.add(PASS if code == 0 else FAIL, "сборка " + Path(proj).stem,
                "" if code == 0 else (errs[0][:160] if errs else out.strip()[:160]))

    # Оба набора: серверный и лаунчерный. Лаунчерный раньше не запускался
    # здесь вовсе, хотя именно он проверяет генерацию altv.toml — одна кривая
    # строка там означает, что клиент не стартует вообще.
    suites = [
        ("юнит-тесты сервера", "server/tests/FloVMP.Core.Tests/FloVMP.Core.Tests.csproj"),
        ("юнит-тесты лаунчера", "launcher/tests/FloVMP.Launcher.Tests/FloVMP.Launcher.Tests.csproj"),
    ]
    # JS-сценарии: процесс захода в игру (клиентский скрипт) и безопасность
    # авторизации лаунчера. Оба грузят НАСТОЯЩИЙ код с подменёнными модулями
    # alt:V / Electron и сетью.
    js_suites = [
        ("симуляция входа в игру", "scripts/client-sim/entry_flow.test.mjs"),
        ("клавиши администратора в клиенте", "scripts/client-sim/admin_keys.test.mjs"),
        ("безопасность входа в лаунчере", "launcher/electron/tests/auth-security.test.cjs"),
    ]
    for label, script in js_suites:
        if not (ROOT / script).exists():
            rep.add(SKIP, label, "не найден")
            continue
        code, out = run_cmd(["node", script], timeout=180)
        m = re.search(r"Пройдено:\s*(\d+),\s*провалов:\s*(\d+)", out)
        detail = "пройдено {}, провалов {}".format(m.group(1), m.group(2)) if m else out.strip()[-160:]
        rep.add(PASS if code == 0 else FAIL, label, detail)

    for label, proj in suites:
        if not (ROOT / proj).exists():
            rep.add(SKIP, label, "проект не найден")
            continue
        code, out = run_cmd(["dotnet", "test", proj, "-c", "Release", "--nologo", "-v", "q"])
        # ВНИМАНИЕ: "пройдено" встречается и внутри "не пройдено" — берём только
        # число, перед которым НЕТ "не ", иначе отчёт врёт про 0 тестов.
        passed = re.search(r"(?<!не )пройдено\s+(\d+)", out) or re.search(r"Passed:\s+(\d+)", out)
        failed = re.search(r"не пройдено\s+(\d+)", out) or re.search(r"Failed:\s+(\d+)", out)
        detail = "пройдено {}{}".format(
            passed.group(1) if passed else "?",
            ", провалено " + failed.group(1) if failed and failed.group(1) != "0" else ""
        ) if (passed or failed) else out.strip()[:160]
        rep.add(PASS if code == 0 else FAIL, label, detail)


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

    # Голосовой порт. Игроки подключаются к нему напрямую, и закрытый файрвол
    # здесь означает «голоса нет» при идеально настроенном конфиге — причём
    # молча, без единой ошибки где-либо.
    # Голосовой сервер слушает ТОЛЬКО UDP: проверка по TCP давала ложное
    # «недоступен». По UDP явный отказ (ICMP port unreachable) виден как
    # ошибка сокета; молчание — порт открыт (голосовой сервер на мусор не отвечает).
    voice_port = int(os.environ.get("FLOVMP_VOICE_PUBLIC_PORT", "7895"))
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp:
            udp.settimeout(2)
            udp.connect((host, voice_port))
            udp.send(b"\x00")
            udp.recv(16)
        rep.add(PASS, "голосовой порт {} (UDP) отвечает".format(voice_port))
    except socket.timeout:
        rep.add(PASS, "голосовой порт {} (UDP) не отклонён".format(voice_port),
                "отказа нет; снаружи дополнительно проверьте, что UDP {} открыт в файрволе".format(voice_port))
    except OSError as e:
        rep.add(WARN, "голосовой порт {} (UDP)".format(voice_port),
                "отклонён ({}) - голосовой сервер не запущен или порт другой".format(e))

    info = None
    for api_port in (7799, 80):
        url = ("http://{}:{}/info".format(host, api_port) if api_port != 80
               else "http://{}/info".format(host))
        try:
            with urllib.request.urlopen(url, timeout=6) as r:
                info = json.loads(r.read().decode("utf-8", "replace"))
            rep.add(PASS, "/info отвечает ({})".format(api_port),
                    "online={} players={}/{}".format(info.get("online"),
                                                     info.get("players"),
                                                     info.get("maxPlayers")))
            if info.get("online") is not True:
                rep.add(FAIL, "сервер сообщает online=false")
            break
        except Exception:
            continue

    if info is None:
        rep.add(WARN, "/info", "не ответил ни на :7799, ни на :80 "
                               "(если API закрыт снаружи за nginx - это нормально)")
        return

    # Главная проверка ночи: сервер может рапортовать online=true, слушать порт
    # и при этом не загрузить НИ ОДНОГО ресурса (ключи modules/resources
    # съедены секцией в TOML). Снаружи это неотличимо от рабочего сервера,
    # пока игрок не зайдёт в пустой мир. /info отдаёт версию гейммода только
    # если C#-ресурс действительно стартовал.
    gm = info.get("gamemode") or ""
    rep.add(PASS if gm else FAIL, "C#-ресурс загружен (гейммод отвечает)",
            "гейммод: {}".format(gm) if gm else
            "сервер online, но гейммод не представился - похоже, ресурсы не загрузились")


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
    check_hot_path(rep)
    check_bans(rep)
    check_voice(rep)
    check_server_toml_order(rep)
    check_event_contract(rep)
    check_migrations(rep)
    check_installer(rep)
    check_client_natives(rep)
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
