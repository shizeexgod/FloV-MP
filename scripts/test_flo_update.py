#!/usr/bin/env python3
"""Минимальный переносимый self-test для scripts/flo_update.py."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tarfile
import tempfile
import zipfile


ROOT = Path(__file__).resolve().parents[1]
UPDATER = ROOT / "scripts" / "flo_update.py"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(*args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(UPDATER), *args],
        cwd=ROOT,
        text=True,
        capture_output=True,
    )


def make_runtime_zip(path: Path) -> None:
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(
            "runtime-test/delivery.json",
            json.dumps(
                {
                    "deliveryMode": "runtime-license",
                    "sourceIncluded": False,
                    "launcherIncluded": False,
                    "webIncluded": False,
                    "os": "all",
                    "version": "test",
                }
            ),
        )
        archive.writestr("runtime-test/manifest.txt", "0" * 64 + "  server/test.bin\n")


def make_source_tar(path: Path) -> None:
    with tempfile.TemporaryDirectory(prefix="flovmp-source-fixture-") as temp:
        root = Path(temp) / "source-test"
        root.mkdir()
        payload = root / "server" / "Example.cs"
        payload.parent.mkdir()
        payload.write_text("public sealed class Example {}\n", encoding="utf-8")
        manifest = {
            "deliveryMode": "source-kit",
            "sourceIncluded": True,
            "launcherIncluded": False,
            "webIncluded": False,
            "files": [{"path": "server/Example.cs", "sha256": sha256(payload)}],
        }
        (root / "SOURCE-MANIFEST.json").write_text(json.dumps(manifest), encoding="utf-8")
        with tarfile.open(path, "w:gz") as archive:
            archive.add(root, arcname=root.name)


def make_traversal_zip(path: Path) -> None:
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("../escape.txt", "must be rejected")


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="flovmp-update-test-") as temp:
        work = Path(temp)
        runtime = work / "runtime.zip"
        source = work / "source.tar.gz"
        traversal = work / "traversal.zip"
        make_runtime_zip(runtime)
        make_source_tar(source)
        make_traversal_zip(traversal)

        result = run(
            "--product",
            "runtime-license",
            "--root",
            str(work / "runtime-install"),
            "--package",
            str(runtime),
            "--sha256",
            sha256(runtime),
            "--dry-run",
        )
        assert result.returncode == 0, result.stderr

        result = run(
            "--product",
            "source-kit",
            "--root",
            str(work),
            "--package",
            str(source),
            "--sha256",
            sha256(source),
            "--dry-run",
        )
        assert result.returncode == 0, result.stderr

        result = run(
            "--product",
            "runtime-license",
            "--root",
            str(work / "runtime-install"),
            "--package",
            str(source),
            "--sha256",
            sha256(source),
            "--dry-run",
        )
        assert result.returncode != 0 and result.stderr

        result = run(
            "--product",
            "source-kit",
            "--root",
            str(work),
            "--package",
            str(runtime),
            "--sha256",
            sha256(runtime),
            "--dry-run",
        )
        assert result.returncode != 0 and result.stderr

        result = run(
            "--product",
            "runtime-license",
            "--root",
            str(work / "runtime-install"),
            "--package",
            str(traversal),
            "--sha256",
            sha256(traversal),
            "--dry-run",
        )
        assert result.returncode != 0 and "небезопасный путь" in result.stderr

    print("flo_update self-test: OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
