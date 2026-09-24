#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Выпуск релиза FloV:MP одной командой (на ПК владельца: здесь ключ подписи,
движок alt:V и Visual Studio для клиента).

    cd C:\\FloV-MP
    git pull
    $env:REDL_TOKEN = "redl_pat_..."      # для VDS; без него VDS пропускается
    python scripts/release.py

Шаги (любой можно пропустить флагом, см. --help):
  1. проверки: VERSION, чистое дерево git, ключ подписи, gh, openssl;
  2. тесты сервера (dotnet test);
  3. сборка пакетов Windows + Linux (pack_server.py, вместе с клиентом ASI);
  4. проверка на утечку брендинга (check-package-clean.py);
  5. подпись (sign_release.py) и архив установки flovmp-setup.zip;
  6. проверка распаковки: каждый архив распаковывается во временную папку,
     сверяются SHA-256 всех файлов по manifest.txt, подпись и версия;
  7. GitHub: релиз v<версия> как последний (latest) — главная ссылка
     releases/latest ведёт на него, а не на старую версию;
  8. главная страница репозитория релизов: README + flovmp-setup.zip;
  9. VDS: публикация релиза и загрузчиков в /cdn/ (publish_release_vds.py);
 10. итоговая проверка: с GitHub скачивается ровно новая версия.

Повторный запуск безопасен: релиз на GitHub обновляется, а не дублируется.
"""
import argparse
import base64
import hashlib
import io
import os
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile
import urllib.request
import zipfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GITHUB = "shizeexgod/FloV-MP-releases"
DIST = os.path.join(REPO, "dist")
RELEASE = os.path.join(DIST, "release")
PUB = os.path.join(REPO, "scripts", "distribution", "release-signing.pub.pem")
KEY = os.path.join(os.path.expanduser("~"), ".flovmp", "release-signing.pem")
README_SRC = os.path.join(REPO, "scripts", "distribution", "releases-repo", "README.md")

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass


def step(n, text):
    print("\n==> {} {}".format(n, text), flush=True)


def ok(text):
    print("  ✓ " + text, flush=True)


def fail(text):
    print("\nОШИБКА: " + text, file=sys.stderr, flush=True)
    sys.exit(1)


def run(cmd, cwd=REPO, capture=False, check=True):
    print("  $ " + " ".join(cmd), flush=True)
    r = subprocess.run(cmd, cwd=cwd, capture_output=capture, text=capture, encoding="utf-8" if capture else None)
    if check and r.returncode != 0:
        if capture:
            print((r.stdout or "") + (r.stderr or ""))
        fail("команда завершилась с кодом {}".format(r.returncode))
    return r


def ensure_openssl():
    """openssl нужен подписи; на Windows он есть в Git for Windows, но не всегда в PATH."""
    if shutil.which("openssl"):
        return
    for d in (r"C:\Program Files\Git\usr\bin", r"C:\Program Files\Git\mingw64\bin"):
        if os.path.isfile(os.path.join(d, "openssl.exe")):
            os.environ["PATH"] = d + os.pathsep + os.environ.get("PATH", "")
            return
    fail("нет openssl — поставьте Git for Windows или добавьте openssl в PATH")


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def verify_signature(txt, sig):
    raw = os.path.join(tempfile.mkdtemp(prefix="flovmp-sig-"), "sig.bin")
    with open(sig, encoding="ascii") as fh, open(raw, "wb") as out:
        out.write(base64.b64decode(fh.read().strip()))
    r = subprocess.run(["openssl", "dgst", "-sha256", "-verify", PUB, "-signature", raw, txt], capture_output=True)
    shutil.rmtree(os.path.dirname(raw), ignore_errors=True)
    return r.returncode == 0


def fields(text):
    return dict(l.split("=", 1) for l in text.splitlines() if "=" in l)


def check_unpack(os_name, version):
    """Распаковать пакет как клиент и сверить каждый файл с manifest.txt."""
    txt = os.path.join(RELEASE, "release-{}.txt".format(os_name))
    info = fields(open(txt, encoding="utf-8").read())
    if info.get("version") != version:
        fail("release-{}.txt: версия {}, ожидалась {}".format(os_name, info.get("version"), version))
    if not verify_signature(txt, txt + ".sig"):
        fail("подпись release-{}.txt не проверяется открытым ключом из репозитория".format(os_name))
    pkg = os.path.join(RELEASE, info["file"])
    if sha256(pkg) != info["sha256"]:
        fail("SHA-256 {} не совпадает с release-{}.txt".format(info["file"], os_name))

    work = tempfile.mkdtemp(prefix="flovmp-unpack-")
    try:
        if os_name == "linux":
            with tarfile.open(pkg, "r:gz") as t:
                modes = {m.name: m.mode for m in t.getmembers() if m.isfile()}
                # filter="data" — безопасная распаковка (Python 3.12+), без него предупреждение.
                if hasattr(tarfile, "data_filter"):
                    t.extractall(work, filter="data")
                else:
                    t.extractall(work)
        else:
            with zipfile.ZipFile(pkg) as z:
                z.extractall(work)
                modes = {}
        roots = [d for d in os.listdir(work) if os.path.isfile(os.path.join(work, d, "manifest.txt"))]
        if len(roots) != 1:
            fail("{}: в архиве должен быть ровно один каталог с manifest.txt".format(info["file"]))
        root = os.path.join(work, roots[0])
        lines = [l for l in open(os.path.join(root, "manifest.txt"), encoding="utf-8").read().splitlines() if l.strip()]
        for line in lines:
            digest, rel = line.split("  ", 1)
            p = os.path.join(root, *rel.split("/"))
            if not os.path.isfile(p) or sha256(p) != digest:
                fail("{}: файл {} не совпадает с manifest.txt".format(info["file"], rel))
        if open(os.path.join(root, "VERSION"), encoding="utf-8").read().strip() != version:
            fail("{}: VERSION внутри пакета не {}".format(info["file"], version))
        if os_name == "linux":
            for rel in ("install.sh", "update.sh", "start.sh", "server/flovmp-server"):
                mode = modes.get("{}/{}".format(roots[0], rel), 0)
                if not mode & 0o100:
                    fail("{}: {} без права на запуск".format(info["file"], rel))
            for rel in ("install.sh", "update.sh", "start.sh"):
                if b"\r\n" in open(os.path.join(root, rel), "rb").read():
                    fail("{}: {} с CRLF — bash на Linux его не запустит".format(info["file"], rel))
            if "--github" not in open(os.path.join(root, "update.sh"), encoding="utf-8").read():
                fail("{}: update.sh без режима GitHub — собран из старого get.sh".format(info["file"]))
        else:
            for rel in ("install.ps1", "FloVMP-Server.exe", "client-b3889/FloVMP.asi"):
                if not os.path.isfile(os.path.join(root, *rel.split("/"))):
                    fail("{}: нет {}".format(info["file"], rel))
        ok("{}: подпись, SHA-256, распаковка и {} файлов по manifest.txt".format(info["file"], len(lines)))
    finally:
        shutil.rmtree(work, ignore_errors=True)


def gh_release(tag, title, notes, files):
    exists = run(["gh", "release", "view", tag, "--repo", GITHUB], capture=True, check=False).returncode == 0
    if exists:
        run(["gh", "release", "upload", tag, "--repo", GITHUB, "--clobber", *files])
        run(["gh", "release", "edit", tag, "--repo", GITHUB, "--title", title, "--notes-file", notes,
             "--prerelease=false", "--draft=false", "--latest"])
    else:
        run(["gh", "release", "create", tag, "--repo", GITHUB, "--title", title, "--notes-file", notes,
             "--latest", *files])
    ok("релиз {} опубликован как последний: https://github.com/{}/releases/latest".format(tag, GITHUB))


def update_releases_repo(version):
    work = tempfile.mkdtemp(prefix="flovmp-releases-")
    try:
        run(["gh", "repo", "clone", GITHUB, work, "--", "--depth", "1"])
        shutil.copy2(README_SRC, os.path.join(work, "README.md"))
        shutil.copy2(os.path.join(RELEASE, "flovmp-setup.zip"), os.path.join(work, "flovmp-setup.zip"))
        run(["git", "add", "README.md", "flovmp-setup.zip"], cwd=work)
        if run(["git", "diff", "--cached", "--quiet"], cwd=work, check=False).returncode == 0:
            ok("главная страница уже актуальна")
            return
        run(["git", "commit", "-m", "Версия {}: README (Windows и Linux) и flovmp-setup.zip".format(version)], cwd=work)
        run(["git", "push"], cwd=work)
        ok("главная страница https://github.com/{} обновлена".format(GITHUB))
    finally:
        shutil.rmtree(work, ignore_errors=True)


def final_check(version):
    base = "https://github.com/{}/releases/latest/download/".format(GITHUB)
    work = tempfile.mkdtemp(prefix="flovmp-final-")
    try:
        for os_name in ("linux", "windows"):
            for name in ("release-{}.txt".format(os_name), "release-{}.txt.sig".format(os_name)):
                with urllib.request.urlopen(base + name, timeout=60) as r, open(os.path.join(work, name), "wb") as fh:
                    fh.write(r.read())
            txt = os.path.join(work, "release-{}.txt".format(os_name))
            got = fields(open(txt, encoding="utf-8").read()).get("version")
            if got != version:
                fail("releases/latest отдаёт {} {} вместо {}".format(os_name, got, version))
            if not verify_signature(txt, txt + ".sig"):
                fail("подпись release-{}.txt на GitHub не проверяется".format(os_name))
        with urllib.request.urlopen(base + "get.sh", timeout=60) as r:
            if b"--github" not in r.read():
                fail("releases/latest/download/get.sh — старый загрузчик без --github")
        ok("с GitHub (releases/latest) скачивается {} — Windows и Linux, подпись верна".format(version))
    finally:
        shutil.rmtree(work, ignore_errors=True)


def main():
    ap = argparse.ArgumentParser(description="Выпуск релиза FloV:MP")
    ap.add_argument("--skip-tests", action="store_true")
    ap.add_argument("--skip-build", action="store_true", help="не собирать, взять готовые пакеты из dist/server")
    ap.add_argument("--no-github", action="store_true", help="не публиковать на GitHub")
    ap.add_argument("--no-vds", action="store_true", help="не публиковать на VDS")
    ap.add_argument("--allow-dirty", action="store_true", help="разрешить незакоммиченные правки")
    args = ap.parse_args()

    version = open(os.path.join(REPO, "VERSION"), encoding="utf-8").read().strip()
    tag = "v" + version
    base, _, suffix = version.partition("-")
    title = "FloV:MP {}{}".format(base, " - " + suffix.capitalize() if suffix else "")
    notes = os.path.join(REPO, "docs", "releases", version + ".md")

    step("1/10", "Проверки — релиз {} ({})".format(version, title))
    if not os.path.isfile(notes):
        fail("нет описания релиза: " + notes)
    if not os.path.isfile(KEY):
        fail("нет ключа подписи релизов: " + KEY)
    ensure_openssl()
    if not args.no_github:
        if not shutil.which("gh"):
            fail("нет gh (GitHub CLI): winget install GitHub.cli, затем gh auth login")
        run(["gh", "auth", "status"], capture=True)
    dirty = run(["git", "status", "--porcelain"], capture=True).stdout.strip()
    if dirty and not args.allow_dirty:
        fail("есть незакоммиченные правки — релиз должен совпадать с коммитом в master:\n" + dirty)
    head = run(["git", "rev-parse", "--short", "HEAD"], capture=True).stdout.strip()
    ok("VERSION {}, коммит {}, ключ подписи на месте".format(version, head))

    step("2/10", "Тесты сервера")
    if args.skip_tests:
        print("  пропущено (--skip-tests)")
    else:
        run(["dotnet", "test", "server/tests/FloVMP.Core.Tests/FloVMP.Core.Tests.csproj", "-c", "Release", "--nologo"])
        ok("тесты прошли")

    step("3/10", "Сборка пакетов Windows и Linux")
    pack = [sys.executable, "scripts/pack_server.py", "--os", "all"]
    run(pack + (["--skip-build"] if args.skip_build else []))

    step("4/10", "Проверка на утечку брендинга")
    for os_name in ("linux", "windows"):
        run([sys.executable, "scripts/check-package-clean.py", "--fail-on-leak",
             os.path.join(DIST, "server", "flovmp-server-{}-{}".format(version, os_name))])

    step("5/10", "Подпись и архив установки")
    run([sys.executable, "scripts/distribution/sign_release.py", "--out", RELEASE])
    run([sys.executable, "scripts/distribution/make_setup_zip.py", "--github", GITHUB,
         "--out", os.path.join(RELEASE, "flovmp-setup.zip")])

    step("6/10", "Проверка распаковки (как у клиента)")
    for os_name in ("linux", "windows"):
        check_unpack(os_name, version)
    files = sorted(os.path.join(RELEASE, f) for f in os.listdir(RELEASE))

    if args.no_github:
        step("7-8/10", "GitHub пропущен (--no-github)")
    else:
        step("7/10", "Релиз на GitHub")
        gh_release(tag, title, notes, files)
        step("8/10", "Главная страница репозитория релизов")
        update_releases_repo(version)

    step("9/10", "VDS")
    if args.no_vds:
        print("  пропущено (--no-vds)")
    elif not os.environ.get("REDL_TOKEN"):
        print("  ! нет REDL_TOKEN — VDS пропущен. Позже: python scripts/distribution/publish_release_vds.py dist/release")
    else:
        run([sys.executable, "scripts/distribution/publish_release_vds.py", RELEASE],
            cwd=os.path.join(REPO, "scripts", "distribution"))

    step("10/10", "Итоговая проверка")
    if args.no_github:
        print("  пропущено (--no-github)")
    else:
        final_check(version)

    print("\nГотово: FloV:MP {} выпущен.".format(version))
    print("  Windows: .\\get.ps1 -GitHub {}".format(GITHUB))
    print("  Linux:   sudo bash /opt/flovmp/update.sh   (1.0.5 и раньше — см. README репозитория релизов)")


if __name__ == "__main__":
    main()
