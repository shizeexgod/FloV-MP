#!/usr/bin/env python3
"""Проверка переносимого контракта GTA V Enhanced 1.0.1158.13.

Mac/Linux не запускают GTA и Windows native bootstrap. Этот скрипт проверяет
только согласованность provisional-профиля и гарантирует, что неизвестные
отпечатки и неподтверждённые E2E-гейты не выданы за поддержку.
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
    client = load(root / "config/client-profiles/enhanced-1158.json")
    native = load(root / "runtime/compat/enhanced-1158/native-profile.json")

    if client.get("id") != "enhanced-1158" or native.get("id") != "enhanced-1158":
        fail("профили имеют разные id")
    for key in ("gameExecutable", "gameFileVersion", "gameSha256", "updateRpfSha256", "update2RpfSha256"):
        if client.get(key) != native.get(key):
            fail(f"профили расходятся по {key}")
    if client.get("gameExecutable") != "GTA5_Enhanced.exe":
        fail("профиль не привязан к GTA5_Enhanced.exe")
    if client.get("gameFileVersion") != "1.0.1158.13":
        fail("ожидался известный Enhanced build 1.0.1158.13")
    if (client.get("fingerprintStatus") != "pending-windows-capture" or
            native.get("fingerprintStatus") != "pending-windows-capture"):
        fail("fingerprintStatus должен оставаться pending-windows-capture")
    if client.get("supportStatus") != "needs-enhanced-runtime" or native.get("supportStatus") != "needs-enhanced-runtime":
        fail("Enhanced нельзя считать поддержанным до native-адаптера")
    adapter = native.get("nativeClient") or {}
    if adapter.get("status") != "missing" or adapter.get("requiredAdapter") != "flovmp-enhanced-native-1158":
        fail("nativeClient не фиксирует отсутствие Enhanced-адаптера")
    gates = native.get("e2eGate") or {}
    required_gates = ("gameWindowAlive", "clientConnected", "resourceLoaded", "playerSpawned", "twoClientSync")
    if any(gates.get(name) is not False for name in required_gates):
        fail("неподтверждённый Enhanced E2E-гейт отмечен как пройденный")

    report = {
        "schema": 1,
        "simulation": True,
        "platform": "macos-or-linux",
        "profile": native["id"],
        "gameFileVersion": native["gameFileVersion"],
        "nativeAdapter": adapter["requiredAdapter"],
        "supportStatus": "needs-enhanced-runtime",
        "fingerprintStatus": "pending-windows-capture",
        "e2eGate": {name: False for name in required_gates},
        "note": "synthetic contract check; no GTA process, DLL or Windows native bootstrap was run",
    }
    if args.write_report:
        target = Path(args.write_report).resolve()
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("OK: контракт Enhanced 1.0.1158.13 согласован; supportStatus=needs-enhanced-runtime")


if __name__ == "__main__":
    main()
