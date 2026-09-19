#!/usr/bin/env python3
"""Проверка распакованного FloV:MP source-kit по SOURCE-MANIFEST.json."""

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import sys


def fail(message):
    print("ОШИБКА: " + message, file=sys.stderr)
    raise SystemExit(1)


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def safe_relative(value):
    if not isinstance(value, str) or not value:
        fail("пустой путь в SOURCE-MANIFEST.json")
    normalized = value.replace("\\", "/")
    path = PurePosixPath(normalized)
    if path.is_absolute() or ":" in normalized or normalized.startswith("-"):
        fail("опасный путь в SOURCE-MANIFEST.json: " + value)
    parts = normalized.split("/")
    if any(part in ("", ".", "..") for part in parts):
        fail("опасный путь в SOURCE-MANIFEST.json: " + value)
    return "/".join(parts)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", nargs="?", default=".", help="распакованный source-kit")
    args = parser.parse_args()
    root = Path(args.root).expanduser().resolve()
    manifest_path = root / "SOURCE-MANIFEST.json"
    if not manifest_path.is_file():
        fail("не найден SOURCE-MANIFEST.json: " + str(manifest_path))
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        fail("не удалось прочитать SOURCE-MANIFEST.json: " + str(exc))
    entries = manifest.get("files")
    if not isinstance(entries, list) or not entries:
        fail("SOURCE-MANIFEST.json не содержит files")

    seen = set()
    checked = 0
    for entry in entries:
        if not isinstance(entry, dict):
            fail("некорректная запись в SOURCE-MANIFEST.json")
        rel = safe_relative(entry.get("path"))
        key = rel.casefold()
        if key in seen:
            fail("повторный путь в SOURCE-MANIFEST.json: " + rel)
        seen.add(key)
        digest = entry.get("sha256")
        size = entry.get("size")
        if not isinstance(digest, str) or len(digest) != 64 or any(c not in "0123456789abcdefABCDEF" for c in digest):
            fail("некорректный SHA-256: " + rel)
        if isinstance(size, bool) or not isinstance(size, int) or size < 0:
            fail("некорректный размер: " + rel)
        path = (root / rel).resolve()
        try:
            path.relative_to(root)
        except ValueError:
            fail("путь выходит за пределы source-kit: " + rel)
        if not path.is_file():
            fail("файл отсутствует: " + rel)
        actual_size = path.stat().st_size
        if actual_size != size:
            fail("размер не совпал: {} (ожидался {}, получен {})".format(rel, size, actual_size))
        actual_hash = sha256_file(path)
        if actual_hash.lower() != digest.lower():
            fail("SHA-256 не совпал: " + rel)
        checked += 1

    print("OK: проверено файлов: {}".format(checked))


if __name__ == "__main__":
    main()
