#!/usr/bin/env python3
"""
Выгрузка исходного кода FloV:MP для передачи по лицензии (без лаунчера и сайта).

    python scripts/export_source.py --out "C:\\...\\FloV-MP Source"
    python scripts/export_source.py --out ... --no-verify    # без сборки и тестов

Что делает:
  1. копирует только перечисленные ниже пути (без bin/obj, служебных файлов,
     истории git);
  2. по явному флагу добавляет локальную копию стороннего движка alt:V;
  3. кладёт README, LICENSE и docs из source-kit/;
  4. проверяет, что нет упоминаний ИИ-инструментов и частных данных
     (scripts/private-markers.txt);
  5. собирает и прогоняет тесты прямо в выгрузке; runtime-пакеты собираются
     только если явно добавлен разрешённый сторонний engine alt:V;
  6. добавляет SOURCE-MANIFEST.json с SHA-256 файлов и упаковывает в zip
     с отдельным .sha256 рядом.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import zipfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

INCLUDE = [
    "VERSION",
    "server/FloVMP.sln",
    "server/Directory.Build.props",
    "server/src/FloVMP.Core",
    "server/src/FloVMP.Starter",
    "server/src/FloVMP.ServerHost",
    "server/tests/FloVMP.Core.Tests",
    "server/tools/FloVMP.LoadTest",
    "server/resources/flovmp-starter",
    "client/resources/flovmp-client",
    "native/legacy-3889/client",
    "native/legacy-3889/README.md",
    "docs/engine/native-adapter-contract.md",
    "docs/engine/native-client-gap.md",
    "docs/engine/legacy-3889-plan.md",
    "docs/engine/player-sync-contract.md",
    "docs/installation-and-delivery.md",
    "docs/ecosystem-control-plane-plan.md",
    "sql/migrations",
    "config/server.toml",
    "config/client-profiles/legacy-3889.json",
    "config/client-profiles/enhanced-1158.json",
    "assets/branding/app.ico",
    "scripts/pack_server.py",
    "scripts/prepare_engine.py",
    "scripts/flo_update.py",
    "scripts/test_flo_update.py",
    "scripts/install.sh",
    "scripts/bootstrap-windows.ps1",
    "scripts/package-templates",
    "scripts/patch_coreclr_host.py",
    "scripts/patch_exe_icon.py",
    "scripts/verify-legacy-3889.ps1",
    "scripts/verify-enhanced-1158.ps1",
    "scripts/verify-windows-native.ps1",
    "scripts/collect-legacy-3889-e2e.ps1",
    "scripts/collect-enhanced-1158-e2e.ps1",
    "scripts/verify-legacy-3889-contract.py",
    "scripts/verify-enhanced-1158-contract.py",
    "scripts/verify-player-sync-contract.py",
    "scripts/verify_source.py",
    "scripts/smoke_windows.ps1",
    "scripts/smoke_linux.sh",
    "scripts/client-sim",
    "runtime/compat/legacy-3889",
    "runtime/compat/enhanced-1158",
]

SKIP_DIRS = {"bin", "obj", "build", ".vs", ".idea", "node_modules", "__pycache__", "TestResults"}
SKIP_FILES = {".DS_Store", "Thumbs.db"}
FORBIDDEN_TOP_LEVEL = {"web", "launcher", "archive"}

ENGINE_FILES = {
    "x64_win32": [
        "server/{br}/x64_win32/altv-server.exe",
        "server/{br}/x64_win32/altv-crash-handler.exe",
        "coreclr-module/{br}/x64_win32/AltV.Net.Host.dll",
        "coreclr-module/{br}/x64_win32/AltV.Net.Host.runtimeconfig.json",
        "coreclr-module/{br}/x64_win32/modules/csharp-module.dll",
        "js-module/{br}/x64_win32/modules/js-module/js-module.dll",
        "js-module/{br}/x64_win32/modules/js-module/libnode.dll",
        "voice-server/{br}/x64_win32/altv-voice-server.exe",
    ],
    "x64_linux": [
        "server/{br}/x64_linux/altv-server",
        "server/{br}/x64_linux/altv-crash-handler",
        "coreclr-module/{br}/x64_linux/AltV.Net.Host.dll",
        "coreclr-module/{br}/x64_linux/AltV.Net.Host.runtimeconfig.json",
        "coreclr-module/{br}/x64_linux/modules/libcsharp-module.so",
        "js-module/{br}/x64_linux/modules/js-module/libjs-module.so",
        "js-module/{br}/x64_linux/modules/js-module/libnode.so",
        "voice-server/{br}/x64_linux/altv-voice-server",
    ],
}

# Упоминания ИИ-инструментов и служебных файлов разработки.
FORBIDDEN_TEXT = [
    r"(?i)\bclaude\b", r"(?i)anthropic", r"(?i)\bopus\s*\d", r"(?i)chatgpt", r"(?i)openai",
    r"(?i)\bcopilot\b", r"(?i)\bLLM\b", r"нейросет", r"\bИИ\b", r"(?i)co-authored-by",
    r"CLAUDE\.md", r"AGENTS\.md", r"agent_state",
]
TEXT_EXT = {".cs", ".csproj", ".props", ".slnx", ".js", ".mjs", ".html", ".css", ".json", ".md", ".toml",
            ".sql", ".py", ".sh", ".ps1", ".cmd", ".example", ".txt", ".xml", ""}


def log(msg):
    print(msg, flush=True)


def fail(msg):
    log("ОШИБКА: " + msg)
    sys.exit(1)


def copy_path(rel, dest_root):
    src = os.path.join(REPO, rel)
    if not os.path.exists(src):
        fail("нет пути в репозитории: " + rel)
    if os.path.isfile(src):
        dst = os.path.join(dest_root, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copy2(src, dst)
        return
    for root, dirs, files in os.walk(src):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for f in files:
            if f in SKIP_FILES:
                continue
            s = os.path.join(root, f)
            d = os.path.join(dest_root, os.path.relpath(s, REPO))
            os.makedirs(os.path.dirname(d), exist_ok=True)
            shutil.copy2(s, d)


def copy_engine(engine_src, dest_root, branch):
    for plat, files in ENGINE_FILES.items():
        for tmpl in files:
            rel = tmpl.format(br=branch)
            src = os.path.join(engine_src, rel)
            if not os.path.isfile(src):
                fail("в бэкапе движка нет " + rel)
            dst = os.path.join(dest_root, "engine", rel)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(src, dst)
    data_src = os.path.join(engine_src, "data", branch, "data")
    for f in sorted(os.listdir(data_src)):
        if f.endswith(".bin"):
            dst = os.path.join(dest_root, "engine", "data", branch, "data", f)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(os.path.join(data_src, f), dst)


def private_markers():
    path = os.path.join(REPO, "scripts", "private-markers.txt")
    if not os.path.isfile(path):
        return []
    with open(path, encoding="utf-8") as fh:
        return [l.strip() for l in fh if l.strip() and not l.startswith("#")]


def scan_text(dest_root):
    problems = []
    markers = private_markers()
    patterns = [re.compile(p) for p in FORBIDDEN_TEXT]
    for root, dirs, files in os.walk(dest_root):
        if os.path.relpath(root, dest_root).startswith("engine"):
            continue
        for f in files:
            p = os.path.join(root, f)
            if os.path.splitext(f)[1].lower() not in TEXT_EXT:
                continue
            try:
                text = open(p, "rb").read().decode("utf-8")
            except UnicodeDecodeError:
                continue
            rel = os.path.relpath(p, dest_root)
            for rx in patterns:
                m = rx.search(text)
                if m:
                    line = text.count("\n", 0, m.start()) + 1
                    problems.append("{}:{}: «{}»".format(rel, line, m.group(0)))
            for mk in markers:
                if mk in text:
                    problems.append("{}: частные данные «{}»".format(rel, mk))
    return problems


def run(cmd, cwd):
    log("  $ " + " ".join(cmd))
    r = subprocess.run(cmd, cwd=cwd)
    if r.returncode != 0:
        fail("команда завершилась с кодом {}".format(r.returncode))


def sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_source_manifest(dest, version):
    entries = []
    for root, _, files in os.walk(dest):
        for name in files:
            path = os.path.join(root, name)
            rel = os.path.relpath(path, dest).replace(os.sep, "/")
            if rel == "SOURCE-MANIFEST.json":
                continue
            entries.append({
                "path": rel,
                "sha256": sha256_file(path),
                "size": os.path.getsize(path),
            })
    entries.sort(key=lambda item: item["path"])
    manifest = {
        "product": "FloV:MP source kit",
        "deliveryMode": "source-kit",
        "sourceIncluded": True,
        "runtimeBinariesIncluded": bool(os.path.isdir(os.path.join(dest, "engine"))),
        "launcherIncluded": False,
        "webIncluded": False,
        "version": version,
        "files": entries,
    }
    with open(os.path.join(dest, "SOURCE-MANIFEST.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=2)
        fh.write("\n")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", required=True, help="папка, в которую выгрузить")
    ap.add_argument("--altv-backup", default=None,
                    help="локальный runtime alt:V; используется только вместе с --include-altv-engine")
    ap.add_argument("--branch", default="release")
    ap.add_argument("--include-altv-engine", action="store_true",
                    help="включить сторонний alt:V engine/ только при наличии прав на его передачу")
    ap.add_argument("--no-verify", action="store_true")
    ap.add_argument("--no-zip", action="store_true")
    args = ap.parse_args()

    version = open(os.path.join(REPO, "VERSION"), encoding="utf-8").read().strip()
    name = "flovmp-source-" + version
    dest = os.path.join(args.out, name)
    if os.path.exists(dest):
        shutil.rmtree(dest)
    os.makedirs(dest)

    log("[1/5] исходники")
    for rel in INCLUDE:
        copy_path(rel, dest)
    kit = os.path.join(REPO, "source-kit")
    for root, _, files in os.walk(kit):
        for f in files:
            s = os.path.join(root, f)
            d = os.path.join(dest, os.path.relpath(s, kit))
            os.makedirs(os.path.dirname(d), exist_ok=True)
            shutil.copy2(s, d)
    # Сборка из исходников — Source Kit: свой бренд (заголовок окна игры,
    # название на загрузочном экране) разрешён без отдельного тарифа.
    edition = os.path.join(dest, "server", "src", "FloVMP.Core", "Licensing", "Edition.cs")
    with open(edition, encoding="utf-8") as fh:
        text = fh.read()
    marker = "public const bool SourceKitBuild = false;"
    if marker not in text:
        fail("Edition.cs: не найден флаг SourceKitBuild")
    with open(edition, "w", encoding="utf-8", newline="
") as fh:
        fh.write(text.replace(marker, "public const bool SourceKitBuild = true;"))
    leaked = sorted(name for name in FORBIDDEN_TOP_LEVEL if os.path.exists(os.path.join(dest, name)))
    if leaked:
        fail("в source-kit попали внутренние компоненты: " + ", ".join(leaked))
    with open(os.path.join(dest, ".gitignore"), "w", encoding="utf-8", newline="\n") as fh:
        fh.write("bin/\nobj/\ndist/\n.vs/\n*.user\n__pycache__/\n")

    if args.include_altv_engine:
        if not args.altv_backup:
            fail("для --include-altv-engine укажите --altv-backup с runtime, который разрешено передавать")
        log("[2/5] сторонний движок alt:V ({})".format(args.branch))
        copy_engine(args.altv_backup, dest, args.branch)
    else:
        log("[2/5] сторонний движок alt:V не включён (нужны отдельные права/дистрибутив)")

    log("[3/5] проверка текста")
    problems = scan_text(dest)
    if problems:
        for p in problems[:50]:
            log("  " + p)
        fail("найдено {} запрещённых упоминаний".format(len(problems)))
    log("  чисто")

    if not args.no_verify:
        log("[4/5] сборка и тесты исходников")
        run(["dotnet", "build", "server/FloVMP.sln", "-c", "Release", "-nologo", "-v", "q"], dest)
        run(["dotnet", "test", "server/FloVMP.sln", "-c", "Release", "-nologo", "-v", "q"], dest)
        if args.include_altv_engine:
            run([sys.executable, "scripts/pack_server.py", "--os", "all", "--no-archive"], dest)
        else:
            log("  ! сборка runtime-пакета пропущена: сторонний alt:V engine не включён")
        for d in ("dist",):
            shutil.rmtree(os.path.join(dest, d), ignore_errors=True)
        for root, dirs, _ in os.walk(dest, topdown=True):
            for dname in list(dirs):
                if dname in ("bin", "obj", "__pycache__"):
                    shutil.rmtree(os.path.join(root, dname), ignore_errors=True)
                    dirs.remove(dname)
    else:
        log("[4/5] проверка сборки пропущена (--no-verify)")

    write_source_manifest(dest, version)

    if not args.no_zip:
        log("[5/5] архив")
        zpath = os.path.join(args.out, name + ".zip")
        with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED) as z:
            for root, _, files in os.walk(dest):
                for f in files:
                    p = os.path.join(root, f)
                    z.write(p, os.path.join(name, os.path.relpath(p, dest)))
        digest = sha256_file(zpath)
        with open(zpath + ".sha256", "w", encoding="utf-8", newline="\n") as fh:
            fh.write("{}  {}\n".format(digest, os.path.basename(zpath)))
        log("  sha256 {}".format(digest))
        log("  " + zpath)

    log("Готово: " + dest)


if __name__ == "__main__":
    main()
