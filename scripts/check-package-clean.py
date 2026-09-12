#!/usr/bin/env python3
"""
scripts/check-package-clean.py
Строгий сканер бинарных файлов (.dll) и текстовых файлов на утечки приватного брендинга
(«Держава» / «derzhava_rp») в сборках движка и платформы FloV:MP.

.NET компилирует строковые литералы в UTF-16LE, поэтому обычный grep/strings кириллицу
в бинарных DLL не видит. Этот скрипт сканирует сырые байты во всех кодировках:
- UTF-8
- UTF-16LE (стандарт C#/.NET)
- UTF-16BE
- Windows-1251 (CP1251)
"""

import os
import sys
import argparse

FORBIDDEN_TERMS = [
    "Держава",
    "держава",
    "ДЕРЖАВА",
    "derzhava",
    "Derzhava",
    "DERZHAVA",
    "FLV-ENTERPRISE-2026-DERZHAVA"
]

EXTENSIONS_TO_CHECK = {
    ".dll", ".so", ".exe", ".bin",
    ".cs", ".js", ".ts", ".tsx", ".jsx",
    ".json", ".toml", ".yaml", ".yml",
    ".sh", ".cmd", ".ps1", ".env", ".sql", ".html"
}

IGNORED_DIRS = {
    ".git", "node_modules", ".next", "bin", "obj", ".vs", "scratch",
    "dist", "native-dist", "out", "release", "build", "runtime", "flovmp-data",
    "archive", ".native-stage"
}

def generate_byte_signatures(term):
    sigs = []
    try:
        sigs.append(("utf-8", term.encode("utf-8")))
    except UnicodeEncodeError:
        pass
    try:
        sigs.append(("utf-16le", term.encode("utf-16le")))
    except UnicodeEncodeError:
        pass
    try:
        sigs.append(("utf-16be", term.encode("utf-16be")))
    except UnicodeEncodeError:
        pass
    try:
        sigs.append(("cp1251", term.encode("cp1251")))
    except UnicodeEncodeError:
        pass
    return sigs

def scan_file(filepath):
    leaks = []
    try:
        with open(filepath, "rb") as f:
            data = f.read()
    except Exception as e:
        return [f"ERROR reading file: {e}"]

    for term in FORBIDDEN_TERMS:
        signatures = generate_byte_signatures(term)
        for enc, sig in signatures:
            idx = 0
            while True:
                pos = data.find(sig, idx)
                if pos == -1:
                    break
                leaks.append(f"Found '{term}' ({enc}) at byte offset 0x{pos:X} ({pos})")
                idx = pos + len(sig)
    return leaks

def main():
    parser = argparse.ArgumentParser(description="Scan FloV:MP distribution packages for brand leaks.")
    parser.add_argument("target", nargs="?", default=".", help="File or directory to scan (default: current dir)")
    parser.add_argument("--fail-on-leak", action="store_true", default=True, help="Exit with code 1 if leaks found")
    args = parser.parse_args()

    target = os.path.abspath(args.target)
    files_to_scan = []

    if os.path.isfile(target):
        files_to_scan.append(target)
    elif os.path.isdir(target):
        for root, dirs, files in os.walk(target):
            dirs[:] = [d for d in dirs if d not in IGNORED_DIRS]
            for file in files:
                ext = os.path.splitext(file)[1].lower()
                if ext in EXTENSIONS_TO_CHECK:
                    files_to_scan.append(os.path.join(root, file))
    else:
        print(f"[!] Target not found: {target}")
        sys.exit(2)

    print(f"=== FloV:MP Brand Leak Scanner ===")
    print(f"Target: {target}")
    print(f"Files to scan: {len(files_to_scan)}")
    print(f"Signatures checked: UTF-8, UTF-16LE, UTF-16BE, CP1251")
    print("-" * 50)

    total_leaks = 0
    clean_files = 0

    for fpath in sorted(files_to_scan):
        # Don't scan this script itself
        if os.path.abspath(fpath) == os.path.abspath(__file__):
            continue

        leaks = scan_file(fpath)
        if leaks:
            rel = os.path.relpath(fpath, target)
            print(f"[FAIL] {rel}:")
            for leak in leaks:
                print(f"   -> {leak}")
            total_leaks += len(leaks)
        else:
            clean_files += 1

    print("-" * 50)
    if total_leaks > 0:
        print(f"[X] FAILED: Found {total_leaks} leak(s) across scanned files.")
        if args.fail_on_leak:
            sys.exit(1)
    else:
        print(f"[OK] PASSED: All {clean_files} files are 100% clean of brand markers.")
        sys.exit(0)

if __name__ == "__main__":
    main()
