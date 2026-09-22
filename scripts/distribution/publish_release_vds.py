#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Залить подписанный релиз FloV:MP на VDS и опубликовать его.

  $env:REDL_TOKEN = "redl_pat_..."
  python scripts/distribution/publish_release_vds.py dist/release-1.0.0

Пакеты перед публикацией повторно проверяются командой flovmp-license publish:
имя, SHA-256 и наличие обоих подписанных манифестов. Временная копия на VDS
удаляется только после успешной публикации.
"""
import argparse
import os
import re
import sys

import deploy_vds

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("folder", nargs="?", default=os.path.join(REPO, "dist", "release-1.0.0"))
    args = ap.parse_args()
    folder = os.path.abspath(args.folder)
    if not os.path.isdir(folder):
        sys.exit("нет папки релиза: " + folder)

    manifests = [os.path.join(folder, "release-{}.txt".format(os_name)) for os_name in ("linux", "windows")]
    for manifest in manifests:
        if not os.path.isfile(manifest) or not os.path.isfile(manifest + ".sig"):
            sys.exit("нет манифеста или подписи: " + manifest)
    version_text = open(manifests[0], encoding="utf-8").read()
    match = re.search(r"^version=([^\r\n]+)$", version_text, re.M)
    if not match or not re.fullmatch(r"[0-9A-Za-z._-]+", match.group(1)):
        sys.exit("неверная версия в " + manifests[0])
    version = match.group(1)
    remote = "/var/tmp/flovmp-release-" + version

    deploy_vds.BUNDLE = folder
    deploy_vds.step("Заливка подписанного релиза {}".format(version))
    files = sorted(f for f in os.listdir(folder) if os.path.isfile(os.path.join(folder, f)))
    for name in files:
        deploy_vds.upload(os.path.join(folder, name), remote + "/" + name)

    deploy_vds.step("Публикация релиза")
    result = deploy_vds.run("flovmp-license publish '{}'".format(remote), t=120)
    if result.get("exitCode") != 0:
        sys.exit("публикация релиза завершилась с ошибкой")
    deploy_vds.run("rm -rf '{}'".format(remote), quiet=True)
    print("\nРелиз {} опубликован на VDS.".format(version))


if __name__ == "__main__":
    main()
