#include <windows.h>
#include <winver.h>
#include <winsock2.h>
#include <ws2tcpip.h>

#include <chrono>
#include <atomic>
#include <cstdint>
#include <fstream>
#include <mutex>
#include <queue>
#include <sstream>
#include <string>
#include <thread>
#include <unordered_map>

#pragma comment(lib, "ws2_32.lib")

namespace {

constexpr wchar_t kGameModule[] = L"GTA5.exe";
constexpr char kProtocol[] = "FLOVMP-BRIDGE/1";
constexpr char kExpectedGameVersion[] = "1.0.3889.0";

using ScriptRegisterFn = void (*)(HMODULE, void (*)());
using ScriptWaitFn = void (*)(unsigned long);
using NativeInitFn = void (*)(uint64_t);
using NativePush64Fn = void (*)(uint64_t);
using NativeCallFn = uint64_t* (*)();

struct Vec3 { float x, y, z; };
struct RemoteState { int id; Vec3 position; float heading; };

HMODULE g_bridgeModule = nullptr;
HMODULE g_scriptHookModule = nullptr;
ScriptWaitFn g_scriptWait = nullptr;
NativeInitFn g_nativeInit = nullptr;
NativePush64Fn g_nativePush64 = nullptr;
NativeCallFn g_nativeCall = nullptr;
SOCKET g_socket = INVALID_SOCKET;
std::atomic<bool> g_sessionAlive = false;
std::atomic<float> g_localX = 0.0f;
std::atomic<float> g_localY = 0.0f;
std::atomic<float> g_localZ = 0.0f;
std::atomic<float> g_localHeading = 0.0f;
std::mutex g_inboundMutex;
std::queue<RemoteState> g_inbound;

std::string ModuleDirectory() {
    char path[MAX_PATH]{};
    const auto length = GetModuleFileNameA(nullptr, path, MAX_PATH);
    if (length == 0) return ".";
    std::string value(path, length);
    const auto slash = value.find_last_of("\\/");
    return slash == std::string::npos ? "." : value.substr(0, slash);
}

void Log(const std::string& text) {
    std::ofstream out(ModuleDirectory() + "\\flovmp-bridge.log", std::ios::app);
    if (!out) return;
    out << text << "\n";
}

std::string GameVersion() {
    char path[MAX_PATH]{};
    const auto length = GetModuleFileNameA(GetModuleHandleW(kGameModule), path, MAX_PATH);
    if (length == 0 || length >= MAX_PATH) return {};

    DWORD ignored = 0;
    const auto size = GetFileVersionInfoSizeA(path, &ignored);
    if (size == 0) return {};
    std::string data(size, '\0');
    if (!GetFileVersionInfoA(path, 0, size, data.data())) return {};

    VS_FIXEDFILEINFO* info = nullptr;
    UINT infoSize = 0;
    if (!VerQueryValueA(data.data(), "\\", reinterpret_cast<void**>(&info), &infoSize) ||
        info == nullptr || infoSize < sizeof(VS_FIXEDFILEINFO)) return {};

    std::ostringstream version;
    version << HIWORD(info->dwFileVersionMS) << '.'
            << LOWORD(info->dwFileVersionMS) << '.'
            << HIWORD(info->dwFileVersionLS) << '.'
            << LOWORD(info->dwFileVersionLS);
    return version.str();
}

std::string Env(const char* name, const char* fallback) {
    char value[256]{};
    const auto length = GetEnvironmentVariableA(name, value, sizeof(value));
    return length == 0 || length >= sizeof(value) ? fallback : std::string(value, length);
}

uint64_t Bits(float value) {
    uint32_t bits = 0;
    static_assert(sizeof(bits) == sizeof(value));
    memcpy(&bits, &value, sizeof(bits));
    return bits;
}

uint64_t Native0(uint64_t hash) {
    g_nativeInit(hash);
    return *g_nativeCall();
}

uint64_t Native1(uint64_t hash, uint64_t arg) {
    g_nativeInit(hash);
    g_nativePush64(arg);
    return *g_nativeCall();
}

uint64_t Native5(uint64_t hash, uint64_t a, uint64_t b, uint64_t c,
                uint64_t d, uint64_t e) {
    g_nativeInit(hash);
    g_nativePush64(a); g_nativePush64(b); g_nativePush64(c);
    g_nativePush64(d); g_nativePush64(e);
    return *g_nativeCall();
}

// ScriptHookV keeps native invocation on the GTA script thread. The network
// thread only updates this queue; it never touches game memory or natives.
void ScriptMain();

void QueueInbound(const std::string& line) {
    std::istringstream input(line);
    std::string protocol, type;
    input >> protocol >> type;
    if (protocol != kProtocol || type != "state") return;

    RemoteState state{};
    std::string key;
    while (input >> key) {
        const auto separator = key.find('=');
        if (separator == std::string::npos) continue;
        const auto name = key.substr(0, separator);
        const auto value = key.substr(separator + 1);
        try {
            if (name == "id") state.id = std::stoi(value);
            else if (name == "x") state.position.x = std::stof(value);
            else if (name == "y") state.position.y = std::stof(value);
            else if (name == "z") state.position.z = std::stof(value);
            else if (name == "heading") state.heading = std::stof(value);
        } catch (...) { return; }
    }
    if (state.id <= 0) return;
    std::lock_guard lock(g_inboundMutex);
    g_inbound.push(state);
}

void ReceiveLoop() {
    std::string pending;
    char buffer[1024]{};
    while (g_sessionAlive) {
        const auto received = recv(g_socket, buffer, sizeof(buffer), 0);
        if (received <= 0) break;
        pending.append(buffer, received);
        for (;;) {
            const auto end = pending.find('\n');
            if (end == std::string::npos) break;
            QueueInbound(pending.substr(0, end));
            pending.erase(0, end + 1);
        }
    }
    g_sessionAlive = false;
}

bool SendHello() {
    const auto host = Env("FLOVMP_BRIDGE_HOST", "127.0.0.1");
    const auto port = Env("FLOVMP_BRIDGE_PORT", "7798");

    addrinfo hints{};
    hints.ai_family = AF_INET;
    hints.ai_socktype = SOCK_STREAM;
    hints.ai_protocol = IPPROTO_TCP;
    addrinfo* addresses = nullptr;
    const auto resolveResult = getaddrinfo(host.c_str(), port.c_str(), &hints, &addresses);
    if (resolveResult != 0) {
        Log("resolve-failed host=" + host + " port=" + port + " code=" + std::to_string(resolveResult));
        return false;
    }

    SOCKET socketHandle = INVALID_SOCKET;
    for (auto* address = addresses; address != nullptr; address = address->ai_next) {
        socketHandle = socket(address->ai_family, address->ai_socktype, address->ai_protocol);
        if (socketHandle == INVALID_SOCKET) continue;

        DWORD timeout = 1500;
        setsockopt(socketHandle, SOL_SOCKET, SO_SNDTIMEO,
                   reinterpret_cast<const char*>(&timeout), sizeof(timeout));
        setsockopt(socketHandle, SOL_SOCKET, SO_RCVTIMEO,
                   reinterpret_cast<const char*>(&timeout), sizeof(timeout));
        if (connect(socketHandle, address->ai_addr, static_cast<int>(address->ai_addrlen)) == 0)
            break;
        Log("connect-failed host=" + host + " port=" + port + " code=" + std::to_string(WSAGetLastError()));
        closesocket(socketHandle);
        socketHandle = INVALID_SOCKET;
    }
    freeaddrinfo(addresses);
    if (socketHandle == INVALID_SOCKET) return false;

    std::ostringstream hello;
    const auto version = GameVersion();
    if (version != kExpectedGameVersion) {
        Log("unsupported-game-version=" + (version.empty() ? "unknown" : version));
        closesocket(socketHandle);
        return false;
    }

    hello << kProtocol << " hello build=3889 version=" << version
          << " pid=" << GetCurrentProcessId() << "\n";
    const auto payload = hello.str();
    const auto sent = send(socketHandle, payload.data(), static_cast<int>(payload.size()), 0);
    char response[128]{};
    const auto received = recv(socketHandle, response, sizeof(response) - 1, 0);
    closesocket(socketHandle);
    if (sent != static_cast<int>(payload.size())) {
        Log("send-failed code=" + std::to_string(WSAGetLastError()));
        return false;
    }
    if (received <= 0) {
        Log("response-failed code=" + std::to_string(WSAGetLastError()));
        return false;
    }
    response[received] = '\0';
    const auto accepted = std::string(response).find("FLOVMP-BRIDGE/1 WELCOME") != std::string::npos;
    Log(std::string("server-response=") + (accepted ? "ok" : "reject"));
    if (!accepted) return false;

    g_socket = socketHandle;
    g_sessionAlive = true;
    std::thread(ReceiveLoop).detach();
    while (g_sessionAlive) {
        std::ostringstream state;
        state << kProtocol << " state x=" << g_localX.load()
              << " y=" << g_localY.load() << " z=" << g_localZ.load()
              << " heading=" << g_localHeading.load() << "\n";
        const auto stateText = state.str();
        if (send(g_socket, stateText.data(), static_cast<int>(stateText.size()), 0) !=
            static_cast<int>(stateText.size())) break;
        Sleep(50);
    }
    closesocket(g_socket);
    g_socket = INVALID_SOCKET;
    return true;
}

void ApplyRemoteStates() {
    // The first native milestone only proves that a second client can be
    // represented by a streamed ped. Entity lifetime is kept conservative;
    // a later protocol message will handle explicit despawn/reconnect.
    static std::unordered_map<int, int> peds;
    std::queue<RemoteState> states;
    {
        std::lock_guard lock(g_inboundMutex);
        std::swap(states, g_inbound);
    }
    while (!states.empty()) {
        const auto state = states.front();
        states.pop();
        auto it = peds.find(state.id);
        if (it == peds.end()) {
            constexpr uint64_t kRequestModel = 0x963D27A58DF860AC;
            constexpr uint64_t kHasModelLoaded = 0x98A4EB5D89A0C952;
            constexpr uint64_t kCreatePed = 0xD49F9B0955C367DE;
            const uint64_t model = 0x9C9EFFD8; // mp_m_freemode_01
            Native1(kRequestModel, model);
            for (int attempt = 0; attempt < 20 && !Native1(kHasModelLoaded, model); ++attempt)
                g_scriptWait(10);
            g_nativeInit(kCreatePed);
            g_nativePush64(4);
            g_nativePush64(model);
            g_nativePush64(Bits(state.position.x));
            g_nativePush64(Bits(state.position.y));
            g_nativePush64(Bits(state.position.z));
            g_nativePush64(Bits(state.heading));
            g_nativePush64(1);
            g_nativePush64(1);
            const auto ped = static_cast<int>(g_nativeCall()[0]);
            if (ped != 0) peds.emplace(state.id, ped);
            it = peds.find(state.id);
        }
        if (it == peds.end()) continue;
        constexpr uint64_t kSetEntityCoords = 0x06843DA7060A026B;
        g_nativeInit(kSetEntityCoords);
        g_nativePush64(static_cast<uint64_t>(it->second));
        g_nativePush64(Bits(state.position.x));
        g_nativePush64(Bits(state.position.y));
        g_nativePush64(Bits(state.position.z));
        g_nativePush64(0); g_nativePush64(0); g_nativePush64(0); g_nativePush64(0);
        g_nativeCall();
    }
}

void ScriptMain() {
    constexpr uint64_t kPlayerId = 0x4F8644AF03D0E0D6;
    constexpr uint64_t kGetPlayerPed = 0x43A66C31C68491C0;
    constexpr uint64_t kGetEntityCoords = 0x3FEF770D40960D5A;
    for (;;) {
        if (!g_sessionAlive) {
            g_scriptWait(100);
            continue;
        }
        const auto player = static_cast<int>(Native0(kPlayerId));
        const auto ped = static_cast<int>(Native1(kGetPlayerPed, player));
        g_nativeInit(kGetEntityCoords);
        g_nativePush64(static_cast<uint64_t>(ped));
        g_nativePush64(0);
        const auto coordinates = reinterpret_cast<const float*>(g_nativeCall());
        if (coordinates != nullptr) {
            g_localX = coordinates[0];
            g_localY = coordinates[1];
            g_localZ = coordinates[2];
        }
        ApplyRemoteStates();
        g_scriptWait(50);
    }
}

void RegisterScript() {
    for (int attempt = 0; attempt < 120 &&
         (g_scriptHookModule = GetModuleHandleW(L"ScriptHookV.dll")) == nullptr; ++attempt)
        Sleep(250);
    if (g_scriptHookModule == nullptr) {
        Log("scripthook-not-loaded; transport remains available");
        return;
    }
    g_scriptWait = reinterpret_cast<ScriptWaitFn>(GetProcAddress(g_scriptHookModule, "?scriptWait@@YAXK@Z"));
    g_nativeInit = reinterpret_cast<NativeInitFn>(GetProcAddress(g_scriptHookModule, "?nativeInit@@YAX_K@Z"));
    g_nativePush64 = reinterpret_cast<NativePush64Fn>(GetProcAddress(g_scriptHookModule, "?nativePush64@@YAX_K@Z"));
    g_nativeCall = reinterpret_cast<NativeCallFn>(GetProcAddress(g_scriptHookModule, "?nativeCall@@YAPEA_KXZ"));
    auto registerScript = reinterpret_cast<ScriptRegisterFn>(GetProcAddress(g_scriptHookModule, "?scriptRegister@@YAXPEAUHINSTANCE__@@P6AXXZ@Z"));
    if (!g_scriptWait || !g_nativeInit || !g_nativePush64 || !g_nativeCall || !registerScript) {
        Log("scripthook-exports-missing");
        return;
    }
    registerScript(g_bridgeModule, ScriptMain);
    Log("scripthook-native-script-registered");
}

void Run() {
    while (GetModuleHandleW(kGameModule) == nullptr) Sleep(250);

    WSADATA data{};
    if (WSAStartup(MAKEWORD(2, 2), &data) != 0) {
        Log("startup-failed");
        return;
    }

    Log("loaded build=3889");
    for (int attempt = 0; attempt < 20; ++attempt) {
        if (SendHello()) {
            Log("server-handshake=ok");
            WSACleanup();
            return;
        }
        Sleep(1000);
    }
    Log("server-handshake=failed");
    WSACleanup();
}

} // namespace

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(module);
        g_bridgeModule = module;
        HANDLE thread = CreateThread(nullptr, 0,
            [](LPVOID) -> DWORD {
                std::thread(RegisterScript).detach();
                Run();
                return 0;
            }, nullptr, 0, nullptr);
        if (thread) CloseHandle(thread);
    }
    return TRUE;
}
