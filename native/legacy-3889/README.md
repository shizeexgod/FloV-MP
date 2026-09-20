# FloV:MP native adapter — Legacy b3889

This directory is the native boundary for Epic GTA V Legacy `1.0.3889.0`.

The first checked-in implementation is a strict fingerprint preflight. It
validates the three version-sensitive game files and deliberately reports
`preflight-only-native-binding-pending` until the client runtime is actually
bound to the b3889 executable. A DLL that only validates hashes must not be
advertised as a working multiplayer adapter.

## Build

```powershell
cmake -S native/legacy-3889 -B native/legacy-3889/build -A x64
cmake --build native/legacy-3889/build --config Release
```

The output is `flovmp-legacy-native-3889.dll`. The exported ABI is consumed by
the launcher/diagnostic harness; `FlovMpLegacy3889_IsRuntimeBound()` remains
false until the actual GTA client binding is implemented and tested.

## Required next native work

The missing implementation is the version-dependent client binding: load the
FloV:MP client runtime into the b3889 GTA process, resolve b3889 signatures,
and establish the client connection without replacing the Epic executable or
RPF files. Only a Windows process-level E2E test can change
`FlovMpLegacy3889_IsRuntimeBound()` to return `1`.
