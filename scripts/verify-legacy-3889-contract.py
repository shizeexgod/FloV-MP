#!/usr/bin/env python3
"""Проверка переносимого контракта Legacy b3889 без запуска Windows/GTA.

Скрипт не объявляет поддержку игры. Он проверяет, что профиль игры, native-
профиль, защитный статус и E2E-гейты согласованы между собой. Это безопасная
Mac/Linux-проверка до появления доступа к Windows-машине.
"""

import argparse
import json
from pathlib import Path
import sys


def fail(message):
    print("ОШИБКА: " + message, file=sys.stderr)
    raise SystemExit(1)


def load(path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        fail(f"не удалось прочитать {path}: {exc}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".", help="корень FloV-MP")
    parser.add_argument("--write-report", metavar="PATH", help="записать синтетический blocked-report")
    args = parser.parse_args()
    root = Path(args.root).resolve()
    client = load(root / "config/client-profiles/legacy-3889.json")
    native = load(root / "runtime/compat/legacy-3889/native-profile.json")

    for key in ("gameFileVersion", "gameSize", "gameSha256", "updateRpfSha256", "update2RpfSha256"):
        if client.get(key) != native.get(key):
            fail(f"профили расходятся по {key}")
    if client.get("supportStatus") != "needs-native-adapter" or native.get("supportStatus") != "needs-native-adapter":
        fail("профиль нельзя считать заблокированным до native-адаптера")
    adapter = native.get("nativeClient") or {}
    if adapter.get("status") != "missing" or adapter.get("requiredAdapter") != "flovmp-legacy-native-3889":
        fail("nativeClient не фиксирует отсутствие требуемого адаптера")
    gates = native.get("e2eGate") or {}
    required_gates = ("gameWindowAlive", "clientConnected", "resourceLoaded", "playerSpawned", "twoClientSync")
    if any(gates.get(name) is not False for name in required_gates):
        fail("неподтверждённый E2E-гейт отмечен как пройденный")

    report = {
        "schema": 1,
        "simulation": True,
        "platform": "macos-or-linux",
        "profile": native["id"],
        "nativeAdapter": adapter["requiredAdapter"],
        "supportStatus": "needs-native-adapter",
        "e2eGate": {name: False for name in required_gates},
        "note": "synthetic contract check; no GTA process, DLL or Windows native bootstrap was run",
    }
    if args.write_report:
        target = Path(args.write_report).resolve()
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("OK: контракт b3889 согласован; supportStatus=needs-native-adapter")


if __name__ == "__main__":
    main()
