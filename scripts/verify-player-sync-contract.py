#!/usr/bin/env python3
"""Проверяет переносимый контракт player streaming без запуска GTA.

Это статическая проверка конфигурации и обязательных серверных/client hooks.
Она не заменяет двухклиентский Windows E2E-прогон.
"""

from pathlib import Path
import re
import sys


def fail(message):
    print("ОШИБКА: " + message, file=sys.stderr)
    raise SystemExit(1)


def require(text, needle, label):
    if needle not in text:
        fail(f"не найден обязательный hook: {label}")


def main():
    root = Path(__file__).resolve().parent.parent
    toml = (root / "config/server.toml").read_text(encoding="utf-8-sig")
    starter = (root / "server/src/FloVMP.Starter/StarterResource.cs").read_text(encoding="utf-8-sig")
    client = (root / "client/resources/flovmp-client/client/index.js").read_text(encoding="utf-8-sig")

    def integer(key):
        match = re.search(rf"(?m)^\s*{re.escape(key)}\s*=\s*(\d+)\s*(?:#.*)?$", toml)
        if not match:
            fail(f"в server.toml нет числового ключа {key}")
        return int(match.group(1))

    streaming = integer("streamingDistance")
    peds = re.search(r"(?ms)^\[maxStreaming\](.*?)(?:^\[|\Z)", toml)
    if not peds or not re.search(r"(?m)^\s*peds\s*=\s*(\d+)", peds.group(1)):
        fail("в server.toml нет maxStreaming.peds")
    peds_limit = int(re.search(r"(?m)^\s*peds\s*=\s*(\d+)", peds.group(1)).group(1))
    if streaming <= 0 or peds_limit <= 0:
        fail("streamingDistance и maxStreaming.peds должны быть положительными")

    require(starter, "player.Spawn(_spawnPosition, 0)", "server player spawn")
    require(starter, "Alt.OnPlayerConnect += OnPlayerConnect", "player connect handler")
    require(starter, "Alt.OnPlayerDisconnect += OnPlayerDisconnect", "player disconnect handler")
    require(client, "alt.Player.streamedIn", "client streamed player collection")

    print(f"OK: player-sync contract; streamingDistance={streaming}; maxStreaming.peds={peds_limit}")
    print("NOTE: static contract only; two-client Windows E2E remains required")


if __name__ == "__main__":
    main()
