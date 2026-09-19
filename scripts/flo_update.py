#!/usr/bin/env python3
"""Установка и обновление FloV:MP для Windows и Linux.

Команда является тонким, проверяемым слоем над существующими установщиками:
она не смешивает runtime-license и source-kit, проверяет SHA-256 архива и
извлекает только безопасные пути. Runtime обновляется штатным атомарным
install.ps1/install.sh, source-kit накладывается с резервной копией файлов.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import tarfile
import tempfile
import zipfile


def fail(message: str) -> "NoReturn":
    print("ОШИБКА: " + message, file=sys.stderr)
    raise SystemExit(1)


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def safe_rel(value: str) -> PurePosixPath:
    normalized = value.replace("\\", "/")
    path = PurePosixPath(normalized)
    if "\x00" in normalized or path.is_absolute() or ":" in normalized or any(part in ("", ".", "..") for part in path.parts):
        fail("небезопасный путь в архиве: " + value)
    return path


def extract(archive: Path, destination: Path) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    if zipfile.is_zipfile(archive):
        with zipfile.ZipFile(archive) as zf:
            for info in zf.infolist():
                rel = safe_rel(info.filename)
                mode = (info.external_attr >> 16) & 0o170000
                if mode == 0o120000:
                    fail("symlink в ZIP запрещён: " + info.filename)
                target = destination.joinpath(*rel.parts)
                if info.is_dir():
                    target.mkdir(parents=True, exist_ok=True)
                    continue
                target.parent.mkdir(parents=True, exist_ok=True)
                with zf.open(info) as src, target.open("wb") as dst:
                    shutil.copyfileobj(src, dst)
        return

    try:
        tf = tarfile.open(archive, "r:*")
    except tarfile.TarError as exc:
        fail("не удалось открыть архив: " + str(exc))
    with tf:
        for info in tf.getmembers():
            rel = safe_rel(info.name)
            if info.issym() or info.islnk() or not (info.isdir() or info.isfile()):
                fail("symlink или специальный файл в архиве запрещён: " + info.name)
            target = destination.joinpath(*rel.parts)
            if info.isdir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            stream = tf.extractfile(info)
            if stream is None:
                fail("не удалось прочитать файл архива: " + info.name)
            with stream, target.open("wb") as dst:
                shutil.copyfileobj(stream, dst)


def one_root(extracted: Path, marker: str) -> Path:
    roots = []
    for path in extracted.iterdir():
        candidate = path if path.is_dir() else None
        if candidate and (candidate / marker).is_file():
            roots.append(candidate)
    if len(roots) != 1:
        fail(f"архив должен содержать ровно один корень с {marker}")
    return roots[0]


def runtime_root(extracted: Path) -> Path:
    return one_root(extracted, "manifest.txt")


def source_root(extracted: Path) -> Path:
    return one_root(extracted, "SOURCE-MANIFEST.json")


def verify_source_manifest(root: Path) -> list[str]:
    path = root / "SOURCE-MANIFEST.json"
    try:
        manifest = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        fail("SOURCE-MANIFEST.json повреждён: " + str(exc))
    if manifest.get("deliveryMode") != "source-kit" or manifest.get("sourceIncluded") is not True:
        fail("SOURCE-MANIFEST.json не является source-kit")
    if manifest.get("launcherIncluded") is True or manifest.get("webIncluded") is True:
        fail("source-kit содержит запрещённый launcher/web компонент")
    entries = manifest.get("files")
    if not isinstance(entries, list) or not entries:
        fail("в SOURCE-MANIFEST.json нет files")
    checked: list[str] = []
    for entry in entries:
        if not isinstance(entry, dict):
            fail("некорректная запись SOURCE-MANIFEST.json")
        rel = str(entry.get("path", ""))
        safe = safe_rel(rel)
        if safe.as_posix() in checked or safe.as_posix() == "SOURCE-MANIFEST.json":
            fail("повторный или зарезервированный путь в source manifest: " + rel)
        target = root.joinpath(*safe.parts)
        expected = str(entry.get("sha256", "")).lower()
        if len(expected) != 64 or any(c not in "0123456789abcdef" for c in expected):
            fail("некорректный SHA-256 в source manifest: " + rel)
        if not target.is_file() or digest(target) != expected:
            fail("SHA-256 source-файла не совпал: " + rel)
        checked.append(safe.as_posix())
    return checked


def run_runtime(root: Path, args: argparse.Namespace, archive: Path) -> None:
    metadata_path = root / "delivery.json"
    try:
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        fail("delivery.json повреждён: " + str(exc))
    if metadata.get("deliveryMode") != "runtime-license" or metadata.get("sourceIncluded") is True:
        fail("архив не является runtime-license")
    if metadata.get("launcherIncluded") is True or metadata.get("webIncluded") is True:
        fail("runtime-пакет содержит запрещённый launcher/web компонент")
    host_os = "windows" if os.name == "nt" else "linux"
    package_os = metadata.get("os")
    if package_os not in (host_os, "all"):
        fail(f"пакет предназначен для {package_os}, а текущая ОС — {host_os}")
    if args.dry_run:
        print(f"OK: runtime {metadata.get('version', '?')} ({package_os}), dry-run")
        return
    install_dir = Path(args.root).expanduser().resolve()
    if host_os == "windows":
        installer = root / "install.ps1"
        if not installer.is_file():
            fail("в Windows-пакете нет install.ps1")
        command = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(installer), "-InstallDir", str(install_dir)]
        if args.license_key:
            command += ["-LicenseKey", args.license_key]
        if args.force:
            command.append("-Force")
    else:
        installer = root / "install.sh"
        if not installer.is_file():
            fail("в Linux-пакете нет install.sh")
        command = ["bash", str(installer), "--dir", str(install_dir)]
        if args.license_key:
            command += ["--key", args.license_key]
        if args.no_start:
            command.append("--no-start")
        if args.no_firewall:
            command.append("--no-firewall")
        if args.force:
            command.append("--force")
    print("Запуск проверенного установщика:", " ".join(command))
    completed = subprocess.run(command, cwd=root)
    raise SystemExit(completed.returncode)


def apply_source(root: Path, args: argparse.Namespace) -> None:
    if not args.root:
        fail("для source-kit обязателен --root с рабочим каталогом исходников")
    destination_input = Path(args.root).expanduser()
    if destination_input.is_symlink():
        fail("корень source-kit не должен быть symlink: " + str(destination_input))
    destination = destination_input.resolve()
    if not destination.is_dir():
        fail("каталог source-kit не найден: " + str(destination))
    entries = verify_source_manifest(root)
    if args.dry_run:
        print(f"OK: source-kit {len(entries)} файлов, dry-run")
        return

    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    backup = destination / ".flovmp-source-backups" / stamp
    copied = []
    try:
        for rel in entries + ["SOURCE-MANIFEST.json"]:
            src = root.joinpath(*safe_rel(rel).parts)
            dst = destination.joinpath(*safe_rel(rel).parts)
            current = destination
            for part in safe_rel(rel).parts[:-1]:
                current = current / part
                if current.is_symlink():
                    fail("путь source-kit проходит через symlink: " + rel)
            if dst.is_symlink() or dst.exists():
                if dst.is_symlink() or not dst.is_file():
                    fail("целевой файл source-kit небезопасен: " + rel)
                saved = backup.joinpath(*safe_rel(rel).parts)
                saved.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(dst, saved)
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
            copied.append(rel)
    except Exception as exc:
        for rel in reversed(copied):
            dst = destination.joinpath(*safe_rel(rel).parts)
            saved = backup.joinpath(*safe_rel(rel).parts)
            if saved.is_file():
                shutil.copy2(saved, dst)
            elif dst.is_file():
                dst.unlink()
        fail("source-kit обновление отменено: " + str(exc))
    print(f"OK: source-kit обновлён, файлов: {len(copied)}")
    print("Резервная копия прежних файлов:", backup)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--product", choices=["runtime-license", "source-kit"], required=True)
    parser.add_argument("--root", required=True, help="установленный runtime или рабочий source-kit")
    parser.add_argument("--package", help="локальный ZIP/TAR.GZ")
    parser.add_argument("--package-url", help="URL ZIP/TAR.GZ")
    parser.add_argument("--sha256", required=True, help="SHA-256 всего архива")
    parser.add_argument("--license-key", default="")
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--no-start", action="store_true")
    parser.add_argument("--no-firewall", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    if bool(args.package) == bool(args.package_url):
        fail("укажите ровно один из --package или --package-url")
    expected = args.sha256.strip().lower()
    if len(expected) != 64 or any(c not in "0123456789abcdef" for c in expected):
        fail("--sha256 должен содержать 64 hex-символа")

    with tempfile.TemporaryDirectory(prefix="flovmp-update-") as temp:
        archive = Path(temp) / "package"
        if args.package:
            source = Path(args.package).expanduser().resolve()
            if not source.is_file():
                fail("архив не найден: " + str(source))
            shutil.copy2(source, archive)
        else:
            import urllib.request
            try:
                with urllib.request.urlopen(args.package_url, timeout=60) as response, archive.open("wb") as target:
                    shutil.copyfileobj(response, target)
            except Exception as exc:
                fail("не удалось скачать архив: " + str(exc))
        actual = digest(archive)
        if actual != expected:
            fail(f"SHA-256 архива не совпал: ожидался {expected}, получен {actual}")
        extracted = Path(temp) / "extracted"
        extract(archive, extracted)
        if args.product == "runtime-license":
            run_runtime(runtime_root(extracted), args, archive)
        else:
            apply_source(source_root(extracted), args)


if __name__ == "__main__":
    main()
