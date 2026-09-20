# FloV:MP Legacy 3889 bridge

This is an emergency native bridge for a clean GTA Legacy `1.0.3889.0`
installation. It is deliberately separate from the old alt:V client runtime.

The bridge is an ASI-compatible DLL. When loaded into the GTA process it:

1. records that the 3889 process loaded the bridge;
2. connects to the FloV:MP bridge endpoint (`127.0.0.1:7798` by default);
3. sends a versioned process handshake and waits for an acknowledgement.

This first milestone proves the native process boundary and server connection;
it does not yet provide entity replication, input capture, or a game HUD. Those
must be implemented on top of the verified 3889 entry point.

Build:

```powershell
cmake -S native/legacy-3889/bridge -B native/legacy-3889/bridge/build -A x64
cmake --build native/legacy-3889/bridge/build --config Release
```

The output is `flovmp-legacy-3889-bridge.asi`. Loading it requires a compatible
ASI loader already installed for GTA Legacy 3889; this project does not ship or
replace third-party loader binaries.
