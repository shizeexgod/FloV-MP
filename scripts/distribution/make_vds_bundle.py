#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Комплект для VDS: сервер лицензий, загрузчики и (при первой установке) ключ подписи.

  python scripts/distribution/make_vds_bundle.py              # dist/vds
  python scripts/distribution/make_vds_bundle.py --with-key   # + authority.pem (первая установка)

Дальше: залить папку на VDS и выполнить  sudo bash setup-vds.sh <папка>.
Ключ подписи (%USERPROFILE%\.flovmp\license-authority.pem) setup-vds.sh кладёт в
/etc/flovmp-license и стирает из комплекта. Резервную копию ключа храните отдельно:
без него новые лицензии не примет ни один сервер FloV:MP.
"""
import argparse
import os
import shutil
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(REPO, "dist", "vds"))
    ap.add_argument("--with-key", action="store_true")
    args = ap.parse_args()
    if os.path.exists(args.out):
        shutil.rmtree(args.out)
    app = os.path.join(args.out, "flovmp-license")
    subprocess.run(["dotnet", "publish", os.path.join(REPO, "server", "src", "FloVMP.LicenseAuthority"),
                    "-c", "Release", "-o", app, "--nologo", "-v", "q"], check=True)
    for f in os.listdir(app):
        if f.endswith(".pdb"):
            os.remove(os.path.join(app, f))
    for f in ("setup-vds.sh", "get.sh", "get.ps1"):
        shutil.copy2(os.path.join(HERE, f), os.path.join(args.out, f))
    if args.with_key:
        key = os.path.join(os.path.expanduser("~"), ".flovmp", "license-authority.pem")
        if not os.path.isfile(key):
            sys.exit("нет " + key)
        shutil.copy2(key, os.path.join(args.out, "authority.pem"))
    size = sum(os.path.getsize(os.path.join(d, f)) for d, _, fs in os.walk(args.out) for f in fs)
    print("Готово: {} ({:.1f} МБ){}".format(args.out, size / 1048576, ", с ключом подписи" if args.with_key else ""))


if __name__ == "__main__":
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    main()
