# FloV:MP Legacy 3889 bridge

This is an emergency native bridge for a clean GTA Legacy `1.0.3889.0`
installation. It is deliberately separate from the old alt:V client runtime.

The bridge is an ASI-compatible DLL. When loaded into the GTA process it:

1. waits for `GTA5.exe` and verifies its PE file version is exactly
   `1.0.3889.0`;
2. connects to the FloV:MP bridge endpoint (`127.0.0.1:7798` by default);
3. sends a versioned process handshake and keeps the TCP session alive with
   heartbeats after the server accepts it.

This milestone proves the version-gated native process boundary and a
long-lived server connection. It does not yet provide entity replication,
game-thread native calls, input capture, or a game HUD by itself. When an
official ScriptHookV runtime for `1.0.3889.0` is already present in the game
directory, the bridge registers a script-thread adapter through its documented
exports: it reads the local player's coordinates and creates/moves streamed
remote peds. Without that optional runtime the transport remains available,
but no game native calls are attempted. The server's existing alt:V protocol
cannot consume this session automatically.

The server rejects a different game version with an explicit
`unsupported-game-version` reason. This is intentional: loading the old
alt:V 16.4.39 client into a b3889 game would recreate the mixed-runtime failure
that caused `FAILED_TO_VERIFY_GAME_LICENSE`.

Build:

```powershell
cmake -S native/legacy-3889/bridge -B native/legacy-3889/bridge/build -A x64
cmake --build native/legacy-3889/bridge/build --config Release
```

The output is `flovmp-legacy-3889-bridge.asi`. Loading it requires a compatible
ASI loader already installed for GTA Legacy 3889; this project does not ship or
replace third-party loader binaries.
