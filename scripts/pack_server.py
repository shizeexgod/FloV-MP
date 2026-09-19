#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Сборка серверного пакета FloV:MP для установки у клиента.

    python scripts/pack_server.py                 # Linux и Windows
    python scripts/pack_server.py --os linux
    python scripts/pack_server.py --skip-build    # без dotnet publish (уже собрано)

Результат — dist/server/:
    flovmp-server-<версия>-linux.tar.gz   (+ .sha256)
    flovmp-server-<версия>-windows.zip    (+ .sha256)

Единственный источник пакета. Раньше их было три (assemble-linux-server.ps1,
pack-scaffold.ps1, install-scaffold.ps1) с разной раскладкой, и каждый ломал
установку по-своему: sh-скрипты с CRLF, admins.json владельца внутри пакета,
подключение к базе root без пароля, поддельная лицензия.

Пакет делится на файлы платформы (перечислены в manifest.txt с SHA-256,
обновление их заменяет) и файлы клиента (server.toml, voice.toml, flovmp.env,
права, данные, свои ресурсы). Файлов клиента в пакете нет вообще — есть только
шаблоны *.example, из которых установщик создаёт их один раз. Поэтому
распаковка новой версии поверх старой не может затереть настройки.
"""

import argparse
import datetime
import hashlib
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import zipfile

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEMPLATES = os.path.join(REPO, "scripts", "package-templates")

# Файлы клиента: в пакет не попадают никогда, обновление их не трогает.
USER_OWNED = [
    "config/flovmp.env",
    "server/server.toml",
    "voice/voice.toml",
    "server/config/",
    "server/flovmp-data/",
    "server/resources/<свои ресурсы>",
    "sql/migrations/100+",
    "license.flv",
    "gamemode/",
    "server/resources/gamemode/",
    "backups/",
]

FORBIDDEN_IN_PACKAGE = [
    "config/flovmp.env", "server/server.toml", "voice/voice.toml",
    "server/config/admins.json", "config/admins.json", "license.flv",
]

def load_private_markers():
    """Частный брендинг и идентификаторы владельца — из scripts/private-markers.txt
    (файл не входит в исходники для клиентов)."""
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "private-markers.txt")
    if not os.path.isfile(path):
        return []
    with open(path, encoding="utf-8") as fh:
        return [l.strip() for l in fh if l.strip() and not l.startswith("#")]


PRIVATE_MARKERS = load_private_markers()

PLACEHOLDERS = [
    "__FLOVMP_NAME__", "__FLOVMP_PORT__", "__FLOVMP_PLAYERS__",
    "__FLOVMP_VOICE_SECRET__", "__FLOVMP_VOICE_PORT__",
    "__FLOVMP_VOICE_PUBLIC_HOST__", "__FLOVMP_VOICE_PUBLIC_PORT__",
]

LINUX_EXECUTABLES = {
    "install.sh", "start.sh", "start-voice.sh",
    "scripts/lib-env.sh", "scripts/backup-db.sh", "scripts/build-gamemode.sh",
    "sdk/template/build.sh",
    "server/flovmp-server", "server/flovmp-crash-handler",
    "voice/altv-voice-server", "voice/altv-crash-handler",
}


def log(msg):
    print(msg, flush=True)


def fail(msg):
    print("\nОШИБКА: " + msg, file=sys.stderr, flush=True)
    sys.exit(1)


def run(cmd, cwd=REPO):
    log("  $ " + " ".join(cmd))
    r = subprocess.run(cmd, cwd=cwd)
    if r.returncode != 0:
        fail("команда завершилась с кодом {}".format(r.returncode))


def copy(src, dst):
    if not os.path.isfile(src):
        fail("нет файла: " + src)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(src, dst)


def copy_tree(src, dst, skip_dirs=("node_modules", "tests", "test", ".git")):
    if not os.path.isdir(src):
        fail("нет папки: " + src)
    for root, dirs, files in os.walk(src):
        dirs[:] = [d for d in dirs if d not in skip_dirs]
        for f in files:
            if f.endswith((".pdb", ".map")) or f.startswith("."):
                continue
            s = os.path.join(root, f)
            copy(s, os.path.join(dst, os.path.relpath(s, src)))


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def git_commit():
    try:
        return subprocess.check_output(["git", "rev-parse", "--short", "HEAD"], cwd=REPO,
                                       text=True).strip()
    except Exception:
        return "unknown"


# ---------------------------------------------------------------------
# Шаблон server.toml
# ---------------------------------------------------------------------
def make_server_toml_example():
    """
    Шаблон из config/server.toml — того же файла, что проверен живым запуском.
    Каждая замена обязана сработать ровно один раз: если формат конфига
    поменяется, сборка упадёт, а не выпустит пакет с неподставленным значением.
    """
    text = io.open(os.path.join(REPO, "config", "server.toml"), encoding="utf-8-sig").read()

    # Внутренние комментарии (пути репозитория, стенды) клиенту не нужны.
    first_key = re.search(r"(?m)^name\s*=", text)
    if not first_key:
        fail("config/server.toml: не найден ключ name")
    header = (
        "# =====================================================================\n"
        "#  FloV:MP — настройки игрового сервера\n"
        "#  Файл ваш: обновление платформы его не меняет. После правки —\n"
        "#  перезапуск сервера.\n"
        "# =====================================================================\n\n"
    )
    text = header + text[first_key.start():]

    voice_idx = text.find("# --- Голосовой чат")
    voice_table = re.search(r"(?m)^\[voice\]", text)
    if voice_idx < 0 or not voice_table:
        fail("config/server.toml: не найдена секция голосового чата")
    text = (text[:voice_idx] +
            "# --- Голосовой чат ----------------------------------------------------\n"
            "# externalSecret совпадает с secret в voice/voice.toml.\n"
            "# externalPublicHost — адрес, который получают ИГРОКИ: внешний IP или домен.\n"
            "# externalPublicPort откройте в файрволе (UDP); externalPort — внутренний.\n"
            + text[voice_table.start():])

    rules = [
        (r'(?m)^(name\s*=\s*)"[^"]*"', r'\1"__FLOVMP_NAME__"'),
        (r"(?m)^(port\s*=\s*)\d+", r"\g<1>__FLOVMP_PORT__"),
        (r"(?m)^(players\s*=\s*)\d+", r"\g<1>__FLOVMP_PLAYERS__"),
        (r"(?m)^(externalSecret\s*=\s*)\d+", r"\g<1>__FLOVMP_VOICE_SECRET__"),
        (r"(?m)^(externalPort\s*=\s*)\d+", r"\g<1>__FLOVMP_VOICE_PORT__"),
        (r'(?m)^(externalPublicHost\s*=\s*)"[^"]*"', r'\1"__FLOVMP_VOICE_PUBLIC_HOST__"'),
        (r"(?m)^(externalPublicPort\s*=\s*)\d+", r"\g<1>__FLOVMP_VOICE_PUBLIC_PORT__"),
        (r"(?m)^(syncSend\s*=\s*)\d+", r"\g<1>__FLOVMP_SYNC_SEND__"),
        (r"(?m)^(syncReceive\s*=\s*)\d+", r"\g<1>__FLOVMP_SYNC_RECEIVE__"),
        (r'(?m)^(description\s*=\s*)"[^"]*"', r'\1"FloV:MP Server"'),
        (r'(?m)^(website\s*=\s*)"[^"]*"', r'\1""'),
    ]
    for pattern, repl in rules:
        text, n = re.subn(pattern, repl, text)
        if n != 1:
            fail("config/server.toml: правило {!r} сработало {} раз вместо 1".format(pattern, n))
    return text


# ---------------------------------------------------------------------
# Сборка
# ---------------------------------------------------------------------
def publish_dotnet(publish_root, skip_build, with_connector, with_host):
    starter = os.path.join(publish_root, "flovmp-starter")
    connector = os.path.join(publish_root, "connector")
    host = os.path.join(publish_root, "server-host")
    if skip_build and os.path.isdir(starter):
        log("[build] пропущено (--skip-build)")
        return (starter, (connector if with_connector and os.path.isdir(connector) else None),
                (host if os.path.isdir(host) else None))

    shutil.rmtree(publish_root, ignore_errors=True)
    log("[build] FloVMP.Starter (Release)")
    run(["dotnet", "publish", "server/src/FloVMP.Starter/FloVMP.Starter.csproj",
         "-c", "Release", "-o", starter, "--nologo", "-v", "q"])
    if with_connector:
        log("[build] FloVMP.Connect (Release)")
        run(["dotnet", "publish", "launcher/src/FloVMP.Connect/FloVMP.Connect.csproj",
             "-c", "Release", "-o", connector, "--nologo", "-v", "q"])
    if with_host:
        # Один exe без распаковки: .NET 8 и так нужен серверу C#.
        log("[build] FloVMP.ServerHost (FloVMP-Server.exe)")
        run(["dotnet", "publish", "server/src/FloVMP.ServerHost/FloVMP.ServerHost.csproj",
             "-c", "Release", "-r", "win-x64", "--self-contained", "false",
             "-p:PublishSingleFile=true", "-p:DebugType=none", "-o", host, "--nologo", "-v", "q"])
    return starter, (connector if with_connector else None), (host if with_host else None)


def stage_package(target_os, stage, args, version, starter_dir, connector_dir, host_dir=None):
    altv = args.altv_backup
    br = args.branch
    plat = "x64_linux" if target_os == "linux" else "x64_win32"
    exe = "" if target_os == "linux" else ".exe"

    shutil.rmtree(stage, ignore_errors=True)
    os.makedirs(stage)
    S = lambda *p: os.path.join(stage, *p)

    log("[{}] движок alt:V {} ({})".format(target_os, br, plat))
    copy(os.path.join(altv, "server", br, plat, "altv-server" + exe), S("server", "flovmp-server" + exe))
    copy(os.path.join(altv, "server", br, plat, "altv-crash-handler" + exe), S("server", "flovmp-crash-handler" + exe))
    copy(os.path.join(altv, "coreclr-module", br, plat, "AltV.Net.Host.dll"), S("server", "FloV.Net.Host.dll"))
    copy(os.path.join(altv, "coreclr-module", br, plat, "AltV.Net.Host.runtimeconfig.json"),
         S("server", "FloV.Net.Host.runtimeconfig.json"))
    if target_os == "linux":
        copy(os.path.join(altv, "coreclr-module", br, plat, "modules", "libcsharp-module.so"),
             S("server", "modules", "libcsharp-module.so"))
        copy(os.path.join(altv, "js-module", br, plat, "modules", "js-module", "libjs-module.so"),
             S("server", "modules", "js-module", "libjs-module.so"))
        copy(os.path.join(altv, "js-module", br, plat, "modules", "js-module", "libnode.so"),
             S("server", "modules", "js-module", "libnode.so"))
    else:
        copy(os.path.join(altv, "coreclr-module", br, plat, "modules", "csharp-module.dll"),
             S("server", "modules", "csharp-module.dll"))
        copy(os.path.join(altv, "js-module", br, plat, "modules", "js-module", "js-module.dll"),
             S("server", "modules", "js-module", "js-module.dll"))
        copy(os.path.join(altv, "js-module", br, plat, "modules", "js-module", "libnode.dll"),
             S("server", "modules", "js-module", "libnode.dll"))
    for f in sorted(os.listdir(os.path.join(altv, "data", br, "data"))):
        if f.endswith(".bin"):
            copy(os.path.join(altv, "data", br, "data", f), S("server", "data", f))
    copy(os.path.join(altv, "voice-server", br, plat, "altv-voice-server" + exe), S("voice", "altv-voice-server" + exe))
    copy(os.path.join(altv, "server", br, plat, "altv-crash-handler" + exe), S("voice", "altv-crash-handler" + exe))
    with open(S("server", "update.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump({"version": version, "branch": br, "platform": "FloV:MP"}, fh)

    # Модуль C# ищет хост по имени — переименовали хост, правим и ссылку.
    sys.path.insert(0, os.path.join(REPO, "scripts"))
    import patch_coreclr_host
    import patch_exe_icon
    module = S("server", "modules", "libcsharp-module.so" if target_os == "linux" else "csharp-module.dll")
    if not patch_coreclr_host.patch_coreclr_module(module):
        fail("не удалось пропатчить " + module)
    with open(module, "rb") as fh:
        if b"AltV.Net.Host" in fh.read():
            fail("в модуле C# осталась ссылка на AltV.Net.Host — сервер не загрузит ресурсы")
    patch_exe_icon.patch_binary_logic(S("server", "flovmp-server" + exe))
    if target_os == "windows":
        cwd = os.getcwd()
        os.chdir(REPO)
        try:
            for f in ("flovmp-server.exe", "flovmp-crash-handler.exe"):
                patch_exe_icon.patch_exe_icon(S("server", f), os.path.join("assets", "branding", "app.ico"))
        finally:
            os.chdir(cwd)

    log("[{}] ресурсы".format(target_os))
    copy_tree(starter_dir, S("server", "resources", "flovmp-starter"))
    copy(os.path.join(REPO, "server", "resources", "flovmp-starter", "resource.toml"),
         S("server", "resources", "flovmp-starter", "resource.toml"))
    copy_tree(os.path.join(REPO, "client", "resources", "flovmp-client"),
              S("server", "resources", "flovmp-client"))

    # SDK для своего сервера (папка gamemode): сборки для компиляции — ровно те,
    # что загружает сервер, иначе мод соберётся против другой версии API.
    for dll in ("AltV.Net.dll", "AltV.Net.Shared.dll", "AltV.Net.CApi.dll", "FloVMP.Core.dll",
                "MySqlConnector.dll", "Microsoft.Extensions.Logging.Abstractions.dll"):
        copy(os.path.join(starter_dir, dll), S("sdk", "ref", dll))

    log("[{}] шаблоны и база".format(target_os))
    with open(S("server", "server.toml.example"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(make_server_toml_example())
    copy_tree(os.path.join(TEMPLATES, "common"), stage)
    copy_tree(os.path.join(TEMPLATES, target_os), stage)
    mig_src = os.path.join(REPO, "sql", "migrations")
    migs = sorted(f for f in os.listdir(mig_src) if f.endswith(".sql"))
    if not migs:
        fail("нет миграций в sql/migrations")
    for f in migs:
        if not re.match(r"^0\d\d_", f):
            fail("миграция платформы вне диапазона 001–099: " + f)
        copy(os.path.join(mig_src, f), S("sql", "migrations", f))

    if target_os == "linux":
        copy(os.path.join(REPO, "scripts", "install.sh"), S("install.sh"))
    else:
        if not host_dir or not os.path.isfile(os.path.join(host_dir, "FloVMP-Server.exe")):
            fail("нет FloVMP-Server.exe — соберите без --skip-build")
        copy(os.path.join(host_dir, "FloVMP-Server.exe"), S("FloVMP-Server.exe"))
        if connector_dir:
            copy_tree(connector_dir, S("tools", "connector"))

    with open(S("VERSION"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write(version + "\n")


def normalize_text_files(stage):
    """sh — строго LF (с CRLF bash на Linux не запустит скрипт вообще),
    cmd/ps1 — CRLF, ps1 — с BOM (Windows PowerShell 5.1 без BOM читает
    кириллицу как ANSI и ломает строки)."""
    for root, _, files in os.walk(stage):
        for f in files:
            p = os.path.join(root, f)
            ext = os.path.splitext(f)[1].lower()
            if ext not in (".sh", ".cmd", ".ps1", ".md", ".toml", ".example", ".sql", ".json") and f != "VERSION":
                continue
            if ext == ".json" and "resources" in p:
                continue
            raw = open(p, "rb").read()
            if raw.startswith(b"\xef\xbb\xbf"):
                raw = raw[3:]
            if b"\x00" in raw:
                continue
            text = raw.replace(b"\r\n", b"\n")
            if ext in (".cmd", ".ps1"):
                text = text.replace(b"\n", b"\r\n")
            if ext == ".ps1":
                text = b"\xef\xbb\xbf" + text
            if text != raw or raw != open(p, "rb").read():
                open(p, "wb").write(text)


def write_manifests(stage, target_os, version):
    entries = []
    for root, _, files in os.walk(stage):
        for f in files:
            p = os.path.join(root, f)
            rel = os.path.relpath(p, stage).replace(os.sep, "/")
            if rel in ("manifest.txt", "manifest.json"):
                continue
            if " " in rel:
                fail("пробел в пути файла пакета (установщик это не поддерживает): " + rel)
            entries.append((rel, sha256_file(p), os.path.getsize(p)))
    entries.sort()
    with open(os.path.join(stage, "manifest.txt"), "w", encoding="utf-8", newline="\n") as fh:
        for rel, digest, _ in entries:
            fh.write("{}  {}\n".format(digest, rel))
    manifest = {
        "product": "FloV:MP Server",
        "version": version,
        "os": target_os,
        "commit": git_commit(),
        "builtAt": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "files": [{"path": r, "sha256": d, "size": s} for r, d, s in entries],
        "userOwned": USER_OWNED,
    }
    with open(os.path.join(stage, "manifest.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=2)
    return entries


def find_bash():
    """На Windows «bash» в PATH нередко оказывается заглушкой WSL без
    дистрибутива — она падает на любой команде. Берём bash из Git."""
    if os.name != "nt":
        return shutil.which("bash")
    git = shutil.which("git")
    candidates = []
    if git:
        # git.exe лежит в Git/cmd или Git/mingw64/bin — поднимаемся до корня Git.
        base = os.path.realpath(git)
        for _ in range(3):
            base = os.path.dirname(base)
            candidates += [os.path.join(base, "bin", "bash.exe"), os.path.join(base, "usr", "bin", "bash.exe")]
    candidates += [os.path.join(os.environ.get("ProgramFiles", "C:" + os.sep + "Program Files"), "Git", "bin", "bash.exe")]
    for c in candidates:
        if os.path.isfile(c):
            return c
    return None


def verify_stage(stage, target_os, entries):
    problems = []
    rels = {e[0] for e in entries}
    for f in FORBIDDEN_IN_PACKAGE:
        if f in rels:
            problems.append("файл клиента попал в пакет: " + f)
    required = ["VERSION", "README.md", "server/server.toml.example", "voice/voice.toml.example",
                "config/flovmp.env.example", "sql/README.md",
                "server/resources/flovmp-starter/FloVMP.Starter.dll",
                "server/resources/flovmp-starter/resource.toml",
                "server/resources/flovmp-client/resource.toml",
                "sdk/ref/AltV.Net.dll", "sdk/ref/FloVMP.Core.dll", "sdk/template/Gamemode.csproj",
                "sdk/template/src/GamemodeResource.cs", "sdk/template/README.md"]
    if target_os == "linux":
        required += ["install.sh", "start.sh", "start-voice.sh", "scripts/lib-env.sh", "scripts/build-gamemode.sh",
                     "server/flovmp-server", "voice/altv-voice-server", "server/modules/libcsharp-module.so"]
    else:
        required += ["FloVMP-Server.exe", "install.ps1", "install.cmd", "scripts/lib.ps1", "scripts/build-gamemode.ps1",
                     "server/flovmp-server.exe", "voice/altv-voice-server.exe", "server/modules/csharp-module.dll"]
    for r in required:
        if r not in rels:
            problems.append("нет обязательного файла: " + r)

    toml = open(os.path.join(stage, "server", "server.toml.example"), encoding="utf-8").read()
    voice = open(os.path.join(stage, "voice", "voice.toml.example"), encoding="utf-8").read()
    for ph in PLACEHOLDERS:
        if ph not in toml + voice:
            problems.append("в шаблонах нет подстановки " + ph)
    if "flovmp-starter" not in toml:
        problems.append("server.toml.example не загружает flovmp-starter")
    # modules/resources обязаны стоять до первой [таблицы] — иначе TOML
    # отнесёт их к ней и сервер стартует без ресурсов.
    first_table = re.search(r"(?m)^\[", toml)
    for key in ("modules", "resources"):
        m = re.search(r"(?m)^" + key + r"\s*=", toml)
        if not m or (first_table and m.start() > first_table.start()):
            problems.append("server.toml.example: {} после первой таблицы".format(key))

    for rel, _, _ in entries:
        p = os.path.join(stage, rel)
        if rel.endswith(".sh") and b"\r\n" in open(p, "rb").read():
            problems.append("CRLF в скрипте: " + rel)
        if rel.endswith((".sh", ".ps1", ".cmd", ".md", ".toml", ".example", ".json", ".sql", ".js", ".html")):
            text = open(p, "rb").read().decode("utf-8", errors="ignore")
            for term in PRIVATE_MARKERS:
                if term in text:
                    problems.append("частные данные «{}» в {}".format(term, rel))

    if target_os == "linux":
        bash = find_bash()
        if bash is None:
            log("  ! bash не найден — синтаксис install.sh не проверен")
        else:
            for script in ("install.sh", "start.sh", "start-voice.sh", "scripts/lib-env.sh", "scripts/backup-db.sh"):
                r = subprocess.run([bash, "-n", script], cwd=stage, capture_output=True,
                                   text=True, encoding="utf-8", errors="replace")
                if r.returncode != 0:
                    problems.append("{}: синтаксическая ошибка: {}".format(script, (r.stderr or r.stdout).strip()))

    if problems:
        fail("пакет {} не прошёл проверку:\n  - ".format(target_os) + "\n  - ".join(problems))
    log("[{}] проверка пакета: OK ({} файлов)".format(target_os, len(entries)))


def archive(stage, target_os, out_dir):
    name = os.path.basename(stage)
    if target_os == "linux":
        path = os.path.join(out_dir, name + ".tar.gz")

        def fix(ti):
            rel = ti.name.split("/", 1)[1] if "/" in ti.name else ""
            ti.uid = ti.gid = 0
            ti.uname = ti.gname = "root"
            if ti.isdir():
                ti.mode = 0o755
            else:
                ti.mode = 0o755 if rel in LINUX_EXECUTABLES else 0o644
            return ti

        with tarfile.open(path, "w:gz", compresslevel=9) as tar:
            tar.add(stage, arcname=name, filter=fix)
    else:
        path = os.path.join(out_dir, name + ".zip")
        with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
            for root, _, files in os.walk(stage):
                for f in sorted(files):
                    p = os.path.join(root, f)
                    z.write(p, os.path.join(name, os.path.relpath(p, stage)).replace(os.sep, "/"))
    digest = sha256_file(path)
    with open(path + ".sha256", "w", encoding="utf-8", newline="\n") as fh:
        fh.write("{}  {}\n".format(digest, os.path.basename(path)))
    log("[{}] архив: {} ({:.1f} МБ)\n        sha256 {}".format(
        target_os, path, os.path.getsize(path) / 1048576, digest))
    return path


def main():
    ap = argparse.ArgumentParser(description="Сборка серверного пакета FloV:MP")
    ap.add_argument("--os", choices=["linux", "windows", "all"], default="all")
    # Движок: папка engine/ рядом с исходниками, иначе локальный бэкап.
    default_engine = os.path.join(REPO, "engine")
    if not os.path.isdir(default_engine):
        default_engine = r"C:\ViMP backup\backup-altv"
    ap.add_argument("--altv-backup", default=default_engine)
    ap.add_argument("--branch", default="release")
    ap.add_argument("--out", default=os.path.join(REPO, "dist", "server"))
    ap.add_argument("--version", default=None)
    ap.add_argument("--skip-build", action="store_true")
    ap.add_argument("--with-connector", action="store_true",
                    help="включить внутренний FloVMP.Connect (по умолчанию исключён из продаваемого пакета)")
    ap.add_argument("--no-archive", action="store_true")
    args = ap.parse_args()

    version = args.version or open(os.path.join(REPO, "VERSION"), encoding="utf-8").read().strip()
    if not re.match(r"^\d+\.\d+\.\d+([-+][0-9A-Za-z.-]+)?$", version):
        fail("версия должна быть вида 1.2.3: " + version)
    if not os.path.isdir(args.altv_backup):
        fail("не найден бэкап движка: " + args.altv_backup)

    targets = ["linux", "windows"] if args.os == "all" else [args.os]
    connector_project = os.path.join(REPO, "launcher", "src", "FloVMP.Connect", "FloVMP.Connect.csproj")
    if args.with_connector and not os.path.isfile(connector_project):
        log("[build] коннектор игрока (launcher/src/FloVMP.Connect) отсутствует — пакет без tools/connector")
        args.with_connector = False
    os.makedirs(args.out, exist_ok=True)
    starter, connector, host = publish_dotnet(os.path.join(args.out, ".publish"), args.skip_build,
                                              "windows" in targets and args.with_connector,
                                              "windows" in targets)

    for t in targets:
        stage = os.path.join(args.out, "flovmp-server-{}-{}".format(version, t))
        stage_package(t, stage, args, version, starter, connector, host)
        normalize_text_files(stage)
        entries = write_manifests(stage, t, version)
        verify_stage(stage, t, entries)
        if not args.no_archive:
            archive(stage, t, args.out)
    log("\nГотово: " + args.out)


if __name__ == "__main__":
    main()
