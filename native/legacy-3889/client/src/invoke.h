#pragma once
// Вызов нативных функций GTA V через ScriptHookV.
//
// ScriptHookV.dll не линкуется статически: ASI должен загружаться и без него
// (тогда он просто пишет в журнал, что сетевой режим недоступен), а SDK
// ScriptHookV распространять нельзя. Экспорты берутся через GetProcAddress.

#include <windows.h>
#include <cstdint>
#include <cstring>
#include <type_traits>

using Void = DWORD;
using Any = DWORD;
using Hash = DWORD;
using Entity = int;
using Player = int;
using Ped = int;
using Vehicle = int;
using Object = int;
using Blip = int;
using Cam = int;
using ScrHandle = int;

#pragma pack(push, 1)
// Раскладка вектора в стеке аргументов движка: каждое поле занимает 8 байт.
struct Vector3
{
    float x; DWORD _px;
    float y; DWORD _py;
    float z; DWORD _pz;
};
#pragma pack(pop)

namespace shv
{
    using NativeInitFn = void (*)(uint64_t);
    using NativePushFn = void (*)(uint64_t);
    using NativeCallFn = uint64_t* (*)();
    using ScriptWaitFn = void (*)(DWORD);
    using ScriptRegisterFn = void (*)(HMODULE, void (*)());
    using ScriptUnregisterFn = void (*)(HMODULE);
    using PresentCallback = void (*)(void*);
    using PresentRegisterFn = void (*)(PresentCallback);
    using KeyboardHandler = void (*)(DWORD, WORD, BYTE, BOOL, BOOL, BOOL, BOOL);
    using KeyboardRegisterFn = void (*)(KeyboardHandler);
    using WorldGetAllFn = int (*)(int*, int);

    inline NativeInitFn nativeInit = nullptr;
    inline NativePushFn nativePush64 = nullptr;
    inline NativeCallFn nativeCall = nullptr;
    inline ScriptWaitFn scriptWait = nullptr;
    inline ScriptRegisterFn scriptRegister = nullptr;
    inline ScriptUnregisterFn scriptUnregister = nullptr;
    inline PresentRegisterFn presentCallbackRegister = nullptr;
    inline PresentRegisterFn presentCallbackUnregister = nullptr;
    inline KeyboardRegisterFn keyboardHandlerRegister = nullptr;
    inline KeyboardRegisterFn keyboardHandlerUnregister = nullptr;
    inline WorldGetAllFn worldGetAllVehicles = nullptr;
    inline WorldGetAllFn worldGetAllPeds = nullptr;

    /// Найти ScriptHookV.dll в процессе и получить экспорты. false — нет или не та версия.
    inline bool Bind(HMODULE module)
    {
        if (!module) return false;
        auto get = [module](const char* name) { return GetProcAddress(module, name); };
        nativeInit = reinterpret_cast<NativeInitFn>(get("?nativeInit@@YAX_K@Z"));
        nativePush64 = reinterpret_cast<NativePushFn>(get("?nativePush64@@YAX_K@Z"));
        nativeCall = reinterpret_cast<NativeCallFn>(get("?nativeCall@@YAPEA_KXZ"));
        scriptWait = reinterpret_cast<ScriptWaitFn>(get("?scriptWait@@YAXK@Z"));
        scriptRegister = reinterpret_cast<ScriptRegisterFn>(get("?scriptRegister@@YAXPEAUHINSTANCE__@@P6AXXZ@Z"));
        scriptUnregister = reinterpret_cast<ScriptUnregisterFn>(get("?scriptUnregister@@YAXPEAUHINSTANCE__@@@Z"));
        presentCallbackRegister = reinterpret_cast<PresentRegisterFn>(get("?presentCallbackRegister@@YAXP6AXPEAX@Z@Z"));
        presentCallbackUnregister = reinterpret_cast<PresentRegisterFn>(get("?presentCallbackUnregister@@YAXP6AXPEAX@Z@Z"));
        keyboardHandlerRegister = reinterpret_cast<KeyboardRegisterFn>(get("?keyboardHandlerRegister@@YAXP6AXKGEHHHH@Z@Z"));
        keyboardHandlerUnregister = reinterpret_cast<KeyboardRegisterFn>(get("?keyboardHandlerUnregister@@YAXP6AXKGEHHHH@Z@Z"));
        worldGetAllVehicles = reinterpret_cast<WorldGetAllFn>(get("?worldGetAllVehicles@@YAHPEAHH@Z"));
        worldGetAllPeds = reinterpret_cast<WorldGetAllFn>(get("?worldGetAllPeds@@YAHPEAHH@Z"));
        return nativeInit && nativePush64 && nativeCall && scriptWait && scriptRegister;
    }
}

namespace nv
{
    template <typename T>
    inline void push(T value)
    {
        static_assert(sizeof(T) <= sizeof(uint64_t), "аргумент натива больше 64 бит");
        uint64_t raw = 0;
        std::memcpy(&raw, &value, sizeof(T));
        shv::nativePush64(raw);
    }

    template <typename R, typename... Args>
    inline R invoke(uint64_t hash, Args... args)
    {
        shv::nativeInit(hash);
        (push(args), ...);
        auto* result = shv::nativeCall();
        if constexpr (std::is_void_v<R>) { (void)result; }
        else { return *reinterpret_cast<R*>(result); }
    }
}

inline void WAIT(DWORD ms) { shv::scriptWait(ms); }
