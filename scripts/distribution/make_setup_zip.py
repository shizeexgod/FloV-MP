#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Собрать архив установки для клиента на Windows: flovmp-setup.zip.

  python scripts/distribution/make_setup_zip.py
  python scripts/distribution/make_setup_zip.py --github shizeexgod/FloV-MP-releases

Внутри — УСТАНОВИТЬ.cmd (двойной щелчок), flovmp-setup.ps1 (спрашивает ключ и
папку, запоминает их) и get.ps1 с вшитым открытым ключом релизов. Сам архив
ничего не содержит от сервера: платформу он скачивает с VDS по ключу лицензии
и проверяет подпись, поэтому архив не устаревает между версиями.

Результат: dist/cdn/flovmp-setup.zip — его кладут на VDS в /var/www/cdn.
"""
import argparse
import io
import os
import re
import sys
import zipfile

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(REPO, "scripts", "package-templates", "setup-zip")
LOADER = os.path.join(REPO, "scripts", "distribution", "get.ps1")
OUT = os.path.join(REPO, "dist", "cdn", "flovmp-setup.zip")

# Windows-инструменты: .cmd и .txt читаются блокнотом и cmd.exe, им нужен CRLF.
CRLF = (".cmd", ".txt")


def read(path):
    with io.open(path, encoding="utf-8-sig", newline="") as fh:
        return fh.read().replace("\r\n", "\n")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    # Источник по умолчанию для архива: GitHub-релизы вместо сервера раздачи.
    # У части клиентов большие файлы с VDS обрываются, с GitHub — нет.
    ap.add_argument("--github", default="", help="owner/repo релизов на GitHub")
    ap.add_argument("--out", default=OUT)
    args = ap.parse_args()
    if args.github and not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", args.github):
        sys.exit("--github ждёт owner/repo")
    loader = read(LOADER)
    key = re.search(r"\$ReleasePubKeyXml = '([^']*)'", loader)
    if not key or len(key.group(1)) < 100:
        sys.exit("в get.ps1 не вшит ключ релизов: python scripts/distribution/sign_release.py --init-key")

    files = {"get.ps1": loader}
    for name in sorted(os.listdir(SRC)):
        files[name] = read(os.path.join(SRC, name))
    for required in ("УСТАНОВИТЬ.cmd", "flovmp-setup.ps1", "ПРОЧТИ-МЕНЯ.txt"):
        if required not in files:
            sys.exit("нет файла шаблона: " + required)
    if args.github:
        # flovmp-setup.ps1 читает настройки.txt при каждом запуске и дописывает
        # в него ключ и папку; здесь — только откуда брать релиз.
        files["настройки.txt"] = ("# Настройки установки FloV:MP. Файл читается при каждом запуске.\n"
                                  "github = {}\n".format(args.github))

    out = os.path.abspath(args.out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for name, text in files.items():
            if name.endswith(CRLF):
                text = text.replace("\n", "\r\n")
            # BOM: cmd.exe и PowerShell 5.1 без него читают кириллицу как ANSI.
            data = text.encode("utf-8-sig")
            info = zipfile.ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 0
            info.flag_bits |= 0x800  # имена файлов в UTF-8 — кириллица в проводнике
            z.writestr(info, data)
    print("готово: {} ({} КБ, файлов {}{})".format(out, os.path.getsize(out) // 1024 + 1, len(files),
                                                    ", источник GitHub " + args.github if args.github else ""))
    print("на VDS: положить в /var/www/cdn/flovmp-setup.zip (делает setup-vds.sh)")


if __name__ == "__main__":
    main()
