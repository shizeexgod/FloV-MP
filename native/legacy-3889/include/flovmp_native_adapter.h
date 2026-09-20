#pragma once

#include <cstdint>

#ifdef _WIN32
#define FLOVMP_ADAPTER_API extern "C" __declspec(dllexport)
#else
#define FLOVMP_ADAPTER_API extern "C"
#endif

// This ABI is deliberately small. The launcher uses it as a hard preflight
// boundary before any GTA process is started. A future gameplay binding can be
// added without changing the fingerprint contract.
struct FlovMpLegacy3889AdapterInfo {
    std::uint32_t abiVersion;
    const wchar_t* id;
    const wchar_t* profile;
    const wchar_t* gameVersion;
    const wchar_t* bindingStatus;
};

FLOVMP_ADAPTER_API const FlovMpLegacy3889AdapterInfo*
FlovMpLegacy3889_GetInfo();

// Returns 1 only when GTA5.exe, update.rpf and update2.rpf are the exact Epic
// Legacy b3889 files. It does not claim that the client runtime is bound.
FLOVMP_ADAPTER_API int
FlovMpLegacy3889_ValidateGame(const wchar_t* gameDirectory);

// Returns 1 only after the native GTA binding has been implemented and tested.
// The first implementation intentionally returns 0: a fingerprint check is
// not a substitute for a working client runtime.
FLOVMP_ADAPTER_API int
FlovMpLegacy3889_IsRuntimeBound();
