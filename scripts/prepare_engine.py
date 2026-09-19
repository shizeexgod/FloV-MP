#!/usr/bin/env python3
"""Импорт разрешённого внешнего alt:V runtime в локальную папку engine/.

Скрипт не скачивает и не распространяет сторонние бинарники. Владелец должен
сам передать каталог или архив runtime, права на который он имеет. В engine/
копируются только файлы, необходимые сборщику FloV:MP, после чего создаётся
ENGINE-MANIFEST.json с SHA-256.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import sys
import tarfile
import tempfile
import zipfile


ENGINE_FILES = {
    "windows": [
        "server/{br}/x64_win32/altv-server.exe",
        "server/{br}/x64_win32/altv-crash-handler.exe",
        "coreclr-module/{br}/x64_win32/AltV.Net.Host.dll",
        "coreclr-module/{br}/x64_win32/AltV.Net.Host.runtimeconfig.json",
        "coreclr-module/{br}/x64_win32/modules/csharp-module.dll",
        "js-module/{br}/x64_win32/modules/js-module/js-module.dll",
        "js-module/{br}/x64_win32/modules/js-module/libnode.dll",
        "voice-server/{br}/x64_win32/altv-voice-server.exe",
    ],
    "linux": [
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


def fail(message: str) -> None:
    print("ОШИБКА: " + message, file=sys.stderr)
    raise SystemExit(1)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def safe_member(name: str) -> PurePosixPath:
    normalized = name.replace("\\", "/")
    path = PurePosixPath(normalized)
    if path.is_absolute() or any(part in ("", ".", "..") for part in path.parts):
        fail("небезопасный путь в архиве: " + name)
    return path


def extract_archive(source: Path, destination: Path) -> None:
    if source.suffix.lower() == ".zip":
        with zipfile.ZipFile(source) as archive:
            for member in archive.infolist():
                rel = safe_member(member.filename)
                target = destination.joinpath(*rel.parts)
                if member.is_dir():
                    target.mkdir(parents=True, exist_ok=True)
                    continue
                target.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(member) as src, target.open("wb") as dst:
                    shutil.copyfileobj(src, dst)
        return
    try:
        archive = tarfile.open(source, "r:*")
    except tarfile.TarError as exc:
        fail(f"не удалось открыть архив runtime: {exc}")
    with archive:
        for member in archive.getmembers():
            rel = safe_member(member.name)
            if member.issym() or member.islnk():
                fail("symlink/hardlink в архиве runtime запрещён: " + member.name)
            target = destination.joinpath(*rel.parts)
            if member.isdir():
                target.mkdir(parents=True, exist_ok=True)
            elif member.isfile():
                target.parent.mkdir(parents=True, exist_ok=True)
                stream = archive.extractfile(member)
                if stream is None:
                    fail("не удалось прочитать файл архива: " + member.name)
                with stream, target.open("wb") as dst:
                    shutil.copyfileobj(stream, dst)
            else:
                fail("неподдерживаемый тип файла в архиве: " + member.name)


def has_layout(root: Path, branch: str, platform: str) -> bool:
    return all((root / item.format(br=branch)).is_file() for item in ENGINE_FILES[platform])


def locate_root(extracted: Path, branch: str, platforms: list[str]) -> Path:
    if all(has_layout(extracted, branch, platform) for platform in platforms):
        return extracted
    candidates = [p for p in extracted.iterdir() if p.is_dir()] if extracted.is_dir() else []
    for candidate in candidates:
        if all(has_layout(candidate, branch, platform) for platform in platforms):
            return candidate
    fail("архив не содержит ожидаемую структуру alt:V для ветки " + branch)


def copy_files(source: Path, staging: Path, branch: str, platforms: list[str]) -> list[str]:
    relative: set[str] = set()
    for platform in platforms:
        for item in ENGINE_FILES[platform]:
            rel = item.format(br=branch)
            src = source / rel
            dst = staging / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
            relative.add(rel)
    data = source / "data" / branch / "data"
    bins = sorted(p for p in data.glob("*.bin") if p.is_file())
    if not bins:
        fail(f"в runtime нет data/{branch}/data/*.bin")
    for src in bins:
        rel = Path("data") / branch / "data" / src.name
        dst = staging / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
        relative.add(rel.as_posix())
    return sorted(relative)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, help="каталог или .zip/.tar.gz с разрешённым runtime")
    parser.add_argument("--dest", default="engine", help="локальная папка engine/ (по умолчанию engine)")
    parser.add_argument("--branch", default="release")
    parser.add_argument("--platform", choices=["windows", "linux", "all"], default="all")
    parser.add_argument("--replace", action="store_true", help="заменить существующий импортированный engine")
    args = parser.parse_args()

    source = Path(args.source).expanduser().resolve()
    destination = Path(args.dest).expanduser().resolve()
    if not source.exists():
        fail("не найден внешний runtime: " + str(source))
    platforms = ["windows", "linux"] if args.platform == "all" else [args.platform]
    if destination.exists() and any(destination.iterdir()) and not args.replace:
        fail(f"{destination} не пуста; используйте --replace только после проверки резервной копии")

    with tempfile.TemporaryDirectory(prefix="flovmp-engine-") as temp:
        workspace = Path(temp) / "input"
        workspace.mkdir()
        if source.is_dir():
            root = source
        else:
            extract_archive(source, workspace)
            root = locate_root(workspace, args.branch, platforms)
        if not all(has_layout(root, args.branch, platform) for platform in platforms):
            fail("неполная структура runtime")
        staging = Path(temp) / "engine"
        files = copy_files(root, staging, args.branch, platforms)
        manifest = {
            "schema": 1,
            "product": "external alt:V runtime",
            "branch": args.branch,
            "platforms": platforms,
            "sourceIncluded": True,
            "redistribution": "owner-supplied; verify third-party rights before delivery",
            "files": [
                {"path": rel, "sha256": sha256(staging / rel), "size": (staging / rel).stat().st_size}
                for rel in files
            ],
        }
        (staging / "ENGINE-MANIFEST.json").write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
        )
        (staging / "ENGINE-THIRD-PARTY-NOTICE.txt").write_text(
            "Этот каталог импортирован из внешнего alt:V runtime.\n"
            "Права на передачу и условия стороннего компонента проверяет владелец поставки.\n",
            encoding="utf-8",
        )
        if destination.exists():
            shutil.rmtree(destination)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(staging, destination)

    print(f"OK: runtime импортирован в {destination}")
    print(f"Ветка: {args.branch}; платформы: {', '.join(platforms)}; файлов: {len(files)}")
    print("Перед продажей проверьте лицензии alt:V и используйте scripts/pack_server.py только с разрешённым runtime.")


if __name__ == "__main__":
    main()
