#!/usr/bin/env python3
"""Publish the locally signed release on REDL by downloading its GitHub assets on the VDS.

This avoids the REDL /write size limit. REDL_TOKEN is read only from the process
environment. Every remote asset is checked against the local release SHA-256
before the license authority switches the current release.
"""

import hashlib
import os
from pathlib import Path
import shlex
import sys

import deploy_vds


ROOT = Path(__file__).resolve().parents[2]
RELEASE = ROOT / "dist" / "release"
VERSION = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
if not VERSION or any(c not in "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz._-" for c in VERSION):
    sys.exit("invalid VERSION")
STAGE = "/var/tmp/flovmp-release-" + VERSION
BASE = "https://github.com/shizeexgod/FloV-MP-releases/releases/download/v" + VERSION + "/"
NAMES = (
    "flovmp-server-" + VERSION + "-linux.tar.gz",
    "flovmp-server-" + VERSION + "-windows.zip",
    "release-linux.txt", "release-linux.txt.sig",
    "release-windows.txt", "release-windows.txt.sig",
    "get.sh", "get.ps1", "flovmp-setup.zip",
)


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def checked_run(command, timeout=600):
    result = deploy_vds.run(command, t=timeout, quiet=True)
    if result.get("exitCode") != 0:
        print((result.get("output") or result.get("stderr") or "")[-2000:])
        sys.exit("VDS command failed")
    return (result.get("output") or "").strip()


def main():
    if not deploy_vds.TOKEN:
        sys.exit("REDL_TOKEN is required in the process environment")
    digests = {}
    for name in NAMES:
        path = RELEASE / name
        if not path.is_file():
            sys.exit("release file missing: " + name)
        digests[name] = sha256(path)

    for system in ("linux", "windows"):
        manifest = dict(line.split("=", 1) for line in (RELEASE / ("release-" + system + ".txt")).read_text(
            encoding="utf-8").splitlines() if "=" in line)
        archive = "flovmp-server-" + VERSION + "-" + system + (".tar.gz" if system == "linux" else ".zip")
        if manifest.get("version") != VERSION or manifest.get("file") != archive or manifest.get("sha256") != digests[archive]:
            sys.exit("signed manifest does not match local " + system + " archive")

    # flovmp-license runs as an unprivileged service account and must be able
    # to traverse this directory to validate the signed manifests and archives.
    checked_run("install -d -m 0755 " + shlex.quote(STAGE) + " && chmod 0755 " + shlex.quote(STAGE))
    for name in NAMES:
        target = STAGE + "/" + name
        expected = digests[name]
        current = checked_run("sha256sum " + shlex.quote(target) + " 2>/dev/null || true")
        if current.split(" ", 1)[0] == expected:
            print("already verified:", name, flush=True)
            continue
        part = target + ".part"
        url = BASE + name
        command = (
            "curl --fail --location --silent --show-error --retry 4 --connect-timeout 30 "
            "--max-time 540 --output " + shlex.quote(part) + " " + shlex.quote(url) +
            " && echo " + shlex.quote(expected + "  " + part) + " | sha256sum -c - --status" +
            " && mv -f " + shlex.quote(part) + " " + shlex.quote(target)
        )
        print("downloading and verifying:", name, flush=True)
        checked_run(command)

    print("publishing signed release", flush=True)
    checked_run("flovmp-license publish " + shlex.quote(STAGE), timeout=120)
    for name in ("get.sh", "get.ps1", "flovmp-setup.zip"):
        checked_run("install -m 0644 " + shlex.quote(STAGE + "/" + name) +
                    " " + shlex.quote("/var/www/cdn/" + name))
    for name in ("release-linux.txt", "release-windows.txt"):
        remote = "/var/lib/flovmp-license/releases/current/" + name
        actual = checked_run("sha256sum " + shlex.quote(remote)).split(" ", 1)[0]
        if actual != digests[name]:
            sys.exit("published release mismatch: " + name)
    print("VDS release published and verified:", VERSION, flush=True)


if __name__ == "__main__":
    main()
