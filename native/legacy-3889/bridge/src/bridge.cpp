#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>

#include <chrono>
#include <fstream>
#include <sstream>
#include <string>
#include <thread>

#pragma comment(lib, "ws2_32.lib")

namespace {

constexpr wchar_t kGameModule[] = L"GTA5.exe";
constexpr char kProtocol[] = "FLOVMP-BRIDGE/1";

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

std::string Env(const char* name, const char* fallback) {
    char value[256]{};
    const auto length = GetEnvironmentVariableA(name, value, sizeof(value));
    return length == 0 || length >= sizeof(value) ? fallback : std::string(value, length);
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
    hello << kProtocol << " build=3889 pid=" << GetCurrentProcessId() << "\n";
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
    const auto accepted = std::string(response).find("FLOVMP-BRIDGE/1 OK") != std::string::npos;
    Log(std::string("server-response=") + (accepted ? "ok" : "reject"));
    return accepted;
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
        HANDLE thread = CreateThread(nullptr, 0,
            [](LPVOID) -> DWORD { Run(); return 0; }, nullptr, 0, nullptr);
        if (thread) CloseHandle(thread);
    }
    return TRUE;
}
