#include "http.h"
#include "common.h"

#include <windows.h>
#include <winhttp.h>
#include <bcrypt.h>

#include <algorithm>
#include <cstdio>
#include <vector>

#pragma comment(lib, "winhttp.lib")

namespace flov::http
{
    namespace
    {
        struct Handle
        {
            HINTERNET h = nullptr;
            ~Handle() { if (h) WinHttpCloseHandle(h); }
            operator HINTERNET() const { return h; }
        };

        std::string Hex(const std::vector<uint8_t>& bytes)
        {
            static const char* digits = "0123456789abcdef";
            std::string out;
            out.reserve(bytes.size() * 2);
            for (uint8_t b : bytes) { out += digits[b >> 4]; out += digits[b & 0xF]; }
            return out;
        }
    }

    std::string CacheName(const std::string& url)
    {
        BCRYPT_ALG_HANDLE alg = nullptr;
        std::vector<uint8_t> digest(32);
        if (BCryptOpenAlgorithmProvider(&alg, BCRYPT_SHA256_ALGORITHM, nullptr, 0) == 0)
        {
            BCryptHash(alg, nullptr, 0,
                       reinterpret_cast<PUCHAR>(const_cast<char*>(url.data())), (ULONG)url.size(),
                       digest.data(), (ULONG)digest.size());
            BCryptCloseAlgorithmProvider(alg, 0);
        }
        return Hex(digest);
    }

    bool Download(const std::string& url, const std::wstring& destination)
    {
        const std::wstring wide = FromUtf8(url);

        URL_COMPONENTS parts{};
        parts.dwStructSize = sizeof(parts);
        wchar_t host[256]{}, path[2048]{}, extra[2048]{};
        parts.lpszHostName = host;       parts.dwHostNameLength = (DWORD)std::size(host);
        parts.lpszUrlPath = path;        parts.dwUrlPathLength = (DWORD)std::size(path);
        // «?v=2&token=...» WinHTTP кладёт отдельно от пути. Без этого поля
        // параметры молча отбрасывались, и ссылки CDN/Discord отвечали 403/404.
        parts.lpszExtraInfo = extra;     parts.dwExtraInfoLength = (DWORD)std::size(extra);
        if (!WinHttpCrackUrl(wide.c_str(), 0, 0, &parts))
        {
            Log("загрузка: не удалось разобрать ссылку " + url);
            return false;
        }
        if (parts.nScheme != INTERNET_SCHEME_HTTP && parts.nScheme != INTERNET_SCHEME_HTTPS)
        {
            Log("загрузка: поддерживаются только http и https, а не " + url);
            return false;
        }

        Handle session{ WinHttpOpen(L"FloVMP",
                                    WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY,
                                    WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0) };
        if (!session) { Log("загрузка: не удалось начать сеанс WinHTTP"); return false; }
        WinHttpSetTimeouts(session, 10000, 10000, 20000, 60000);

        Handle connection{ WinHttpConnect(session, host, parts.nPort, 0) };
        if (!connection) { Log("загрузка: нет соединения с " + ToUtf8(host)); return false; }

        const DWORD flags = parts.nScheme == INTERNET_SCHEME_HTTPS ? WINHTTP_FLAG_SECURE : 0;
        // Якорь (#...) на сервер не отправляется — по стандарту он только для браузера.
        std::wstring target = std::wstring(path) + extra;
        if (const auto hash = target.find(L'#'); hash != std::wstring::npos) target.resize(hash);
        Handle request{ WinHttpOpenRequest(connection, L"GET", target.c_str(), nullptr, WINHTTP_NO_REFERER,
                                           WINHTTP_DEFAULT_ACCEPT_TYPES, flags) };
        if (!request) { Log("загрузка: не удалось создать запрос к " + url); return false; }

        if (!WinHttpSendRequest(request, WINHTTP_NO_ADDITIONAL_HEADERS, 0, WINHTTP_NO_REQUEST_DATA, 0, 0, 0) ||
            !WinHttpReceiveResponse(request, nullptr))
        {
            Log("загрузка: сервер не ответил на " + url + " (ошибка " + std::to_string(GetLastError()) + ")");
            return false;
        }

        DWORD status = 0, size = sizeof(status);
        WinHttpQueryHeaders(request, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                            WINHTTP_HEADER_NAME_BY_INDEX, &status, &size, WINHTTP_NO_HEADER_INDEX);
        if (status != 200)
        {
            Log("загрузка: " + url + " ответил кодом " + std::to_string(status));
            return false;
        }

        // Пишем во временный файл рядом с целевым: если связь оборвётся, в кэше
        // не останется наполовину скачанной картинки, которую клиент примет за
        // готовую.
        const std::wstring temp = destination + L".part";
        FILE* file = nullptr;
        if (_wfopen_s(&file, temp.c_str(), L"wb") != 0 || !file)
        {
            Log("загрузка: не удалось открыть файл для записи");
            return false;
        }

        std::vector<uint8_t> buffer(64 * 1024);
        size_t total = 0;
        bool ok = true;
        for (;;)
        {
            DWORD available = 0;
            if (!WinHttpQueryDataAvailable(request, &available)) { ok = false; break; }
            if (available == 0) break;
            DWORD read = 0;
            const DWORD want = (DWORD)std::min<size_t>(available, buffer.size());
            if (!WinHttpReadData(request, buffer.data(), want, &read) || read == 0) { ok = false; break; }
            total += read;
            if (total > kMaxBytes)
            {
                Log("загрузка: " + url + " больше допустимых 16 МБ, прервано");
                ok = false;
                break;
            }
            if (fwrite(buffer.data(), 1, read, file) != read) { ok = false; break; }
        }
        fclose(file);

        if (!ok || total == 0)
        {
            DeleteFileW(temp.c_str());
            if (ok) Log("загрузка: " + url + " вернул пустой ответ");
            return false;
        }

        DeleteFileW(destination.c_str());
        if (!MoveFileW(temp.c_str(), destination.c_str()))
        {
            DeleteFileW(temp.c_str());
            Log("загрузка: не удалось переименовать файл кэша");
            return false;
        }
        Log("загрузка: " + url + " — " + std::to_string(total / 1024) + " КБ");
        return true;
    }
}
