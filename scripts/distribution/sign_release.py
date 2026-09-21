#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Подготовить и подписать релиз для раздачи (flovmp_dist.py publish).

  python scripts/distribution/sign_release.py                 # из dist/server (после pack_server.py)
  python scripts/distribution/sign_release.py --out dist/release
  python scripts/distribution/sign_release.py --init-key      # создать ключ релизов (один раз)

Ключ релизов: RSA-3072, закрытая часть — %USERPROFILE%\\.flovmp\\release-signing.pem
(только на ПК владельца, НЕ на VDS и НЕ в репозитории). Открытая часть вшита в
scripts/distribution/get.sh и get.ps1 — загрузчики проверяют ею release-<os>.txt, а в нём
SHA-256 пакета. Поэтому пакет нельзя подменить ни на VDS, ни по дороге (HTTP).

Нужен openssl (есть в Git for Windows и в любом Linux).
"""

import argparse
import base64
import hashlib
import os
import re
import shutil
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KEY_DIR = os.path.join(os.path.expanduser("~"), ".flovmp")
KEY = os.path.join(KEY_DIR, "release-signing.pem")
PUB = os.path.join(REPO, "scripts", "distribution", "release-signing.pub.pem")


def openssl(*args, data=None):
    r = subprocess.run(["openssl", *args], input=data, capture_output=True)
    if r.returncode != 0:
        sys.exit("openssl {}: {}".format(" ".join(args[:2]), r.stderr.decode(errors="replace")))
    return r.stdout


def init_key():
    if os.path.exists(KEY):
        sys.exit("ключ уже есть: {} — новый сломает проверку у уже выданных загрузчиков".format(KEY))
    os.makedirs(KEY_DIR, exist_ok=True)
    openssl("genpkey", "-algorithm", "RSA", "-pkeyopt", "rsa_keygen_bits:3072", "-out", KEY)
    export_public()
    print("ключ релизов создан: {}\nоткрытый ключ: {}\nСделайте резервную копию закрытого ключа в надёжном месте.".format(KEY, PUB))


def export_public():
    pem = openssl("pkey", "-in", KEY, "-pubout")
    with open(PUB, "wb") as fh:
        fh.write(pem)
    # Для PowerShell 5.1 (RSACryptoServiceProvider.FromXmlString): модуль и экспонента.
    mod_hex = openssl("rsa", "-pubin", "-in", PUB, "-modulus", "-noout").decode().strip().split("=", 1)[1]
    text = openssl("rsa", "-pubin", "-in", PUB, "-text", "-noout").decode()
    exp = int(re.search(r"Exponent: (\d+)", text).group(1))
    mod_b64 = base64.b64encode(bytes.fromhex(mod_hex)).decode()
    exp_b64 = base64.b64encode(exp.to_bytes((exp.bit_length() + 7) // 8, "big")).decode()
    xml = "<RSAKeyValue><Modulus>{}</Modulus><Exponent>{}</Exponent></RSAKeyValue>".format(mod_b64, exp_b64)
    embed("get.sh", "RELEASE_PUBKEY_PEM", pem.decode().strip())
    embed("get.ps1", "RELEASE_PUBKEY_XML", xml)


def embed(script, marker, value):
    """Вписать открытый ключ между строками-маркерами BEGIN/END в загрузчике."""
    path = os.path.join(REPO, "scripts", "distribution", script)
    enc = "utf-8-sig" if script.endswith(".ps1") else "utf-8"  # PowerShell 5.1 без BOM читает кириллицу как ANSI
    text = open(path, encoding=enc).read()
    pattern = re.compile(r"(# BEGIN {m}\n).*?(\n# END {m})".format(m=marker), re.S)
    if not pattern.search(text):
        sys.exit("в {} нет маркеров {}".format(script, marker))
    if script.endswith(".sh"):
        body = "RELEASE_PUBKEY_PEM='{}'".format(value)
    else:
        body = "$ReleasePubKeyXml = '{}'".format(value)
    text = pattern.sub(lambda m: m.group(1) + body + m.group(2), text)
    with open(path, "w", encoding=enc, newline="\n") as fh:
        fh.write(text)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=os.path.join(REPO, "dist", "server"))
    ap.add_argument("--out", default=os.path.join(REPO, "dist", "release"))
    ap.add_argument("--init-key", action="store_true")
    args = ap.parse_args()
    if args.init_key:
        return init_key()
    if not os.path.exists(KEY):
        sys.exit("нет ключа релизов ({}). Создайте: python scripts/distribution/sign_release.py --init-key".format(KEY))

    version = open(os.path.join(REPO, "VERSION"), encoding="utf-8").read().strip()
    if os.path.exists(args.out):
        shutil.rmtree(args.out)
    os.makedirs(args.out)
    files = {"linux": "flovmp-server-{}-linux.tar.gz".format(version),
             "windows": "flovmp-server-{}-windows.zip".format(version)}
    for os_name, name in files.items():
        pkg = os.path.join(args.src, name)
        if not os.path.isfile(pkg):
            print("пропуск {}: нет {}".format(os_name, pkg))
            continue
        shutil.copy2(pkg, os.path.join(args.out, name))
        txt = os.path.join(args.out, "release-{}.txt".format(os_name))
        body = "format=1\nproduct=flovmp-server\nversion={}\nos={}\nfile={}\nsha256={}\nsize={}\n".format(
            version, os_name, name, sha256(pkg), os.path.getsize(pkg))
        with open(txt, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(body)
        sig = openssl("dgst", "-sha256", "-sign", KEY, txt)
        with open(txt + ".sig", "w", encoding="ascii", newline="\n") as fh:
            fh.write(base64.b64encode(sig).decode() + "\n")
        print("{}: {} подписан".format(os_name, name))
    for loader in ("get.sh", "get.ps1"):
        shutil.copy2(os.path.join(REPO, "scripts", "distribution", loader), os.path.join(args.out, loader))
    print("\nГотово: {}\nЗалить на VDS и опубликовать: flovmp-dist publish <папка>".format(args.out))


if __name__ == "__main__":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    main()
