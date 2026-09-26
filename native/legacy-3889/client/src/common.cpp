#include "common.h"

#include <shlobj.h>
#include <cstdio>
#include <ctime>
#include <atomic>
#include <mutex>
#include <share.h>
#include <thread>
#include <deque>
#include <condition_variable>
#include <sstream>
#include <iomanip>

#pragma comment(lib, "version.lib")

namespace flov
{
    std::wstring DataDir()
    {
        static std::wstring cached;
        if (!cached.empty()) return cached;
        PWSTR base = nullptr;
        std::wstring dir = L".";
        if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &base)) && base)
        {
            dir = std::wstring(base) + L"\\FloVMP";
            CoTaskMemFree(base);
        }
        CreateDirectoryW(dir.c_str(), nullptr);
        CreateDirectoryW((dir + L"\\logs").c_str(), nullptr);
        cached = dir;
        return cached;
    }

    namespace { std::atomic<LogHook> g_logHook{ nullptr }; }

    void SetLogHook(LogHook hook) { g_logHook = hook; }

    void Log(const std::string& text)
    {
        WriteLog(text);
        if (auto hook = g_logHook.load()) hook(text); // консоль F8 видит тот же журнал
    }

    namespace
    {
        // Журнал пишется своим потоком: раньше каждая строка открывала и
        // закрывала файл прямо в вызывающем потоке — игровом, отрисовки,
        // перехвата клавиш. С антивирусом это миллисекунды на строку и
        // заметные подтормаживания при пачке строк.
        std::mutex g_logMutex;
        std::condition_variable g_logCv;
        std::deque<std::string> g_logQueue;
        bool g_logStarted = false;
        FILE* g_logFile = nullptr;

        std::wstring LogPath()
        {
            // Утилиты и тесты (не GTA5.exe) — в свой файл: журнал игрока не
            // затирается проверками разработчика.
            wchar_t exe[MAX_PATH] = {};
            GetModuleFileNameW(nullptr, exe, MAX_PATH);
            std::wstring name = exe;
            name = name.substr(name.find_last_of(L"\\/") + 1);
            const bool game = _wcsicmp(name.c_str(), L"GTA5.exe") == 0;
            if (!game && name.size() > 4) name.resize(name.size() - 4);
            return DataDir() + L"\\logs\\" + (game ? std::wstring(L"client-3889") : L"dev-" + name) + L".log";
        }

        void WriteBatch(std::deque<std::string>& batch)
        {
            if (!g_logFile) return;
            for (const auto& line : batch) fwrite(line.data(), 1, line.size(), g_logFile);
            fflush(g_logFile);
        }

        void LogThread()
        {
            for (;;)
            {
                std::deque<std::string> batch;
                {
                    std::unique_lock lock(g_logMutex);
                    g_logCv.wait(lock, [] { return !g_logQueue.empty(); });
                    batch.swap(g_logQueue);
                }
                WriteBatch(batch);
            }
        }

        std::string Stamp(const std::string& text)
        {
            SYSTEMTIME t;
            GetLocalTime(&t);
            char head[32];
            snprintf(head, sizeof head, "%02d:%02d:%02d.%03d ", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds);
            return head + text + "\r\n";
        }
    }

    void WriteLog(const std::string& text)
    {
        std::lock_guard lock(g_logMutex);
        if (!g_logStarted)
        {
            g_logStarted = true;
            const auto path = LogPath();
            CreateDirectoryW((DataDir() + L"\\logs").c_str(), nullptr);
            // Журнал прошлого запуска — рядом, чтобы жалобу «вылетело» можно было разобрать.
            MoveFileExW(path.c_str(), (path + L".old").c_str(), MOVEFILE_REPLACE_EXISTING);
            g_logFile = _wfsopen(path.c_str(), L"ab", _SH_DENYWR);
            std::thread(LogThread).detach();
        }
        if (g_logQueue.size() < 20000) g_logQueue.push_back(Stamp(text));
        g_logCv.notify_one();
    }

    void FlushLogNow(const std::string& text)
    {
        // Падение игры: поток журнала может уже не успеть — пишем сами, сразу.
        std::deque<std::string> batch;
        {
            std::lock_guard lock(g_logMutex);
            batch.swap(g_logQueue);
        }
        batch.push_back(Stamp(text));
        WriteBatch(batch);
    }

    std::string ToUtf8(const std::wstring& text)
    {
        if (text.empty()) return {};
        const int n = WideCharToMultiByte(CP_UTF8, 0, text.data(), (int)text.size(), nullptr, 0, nullptr, nullptr);
        std::string out(n, '\0');
        WideCharToMultiByte(CP_UTF8, 0, text.data(), (int)text.size(), out.data(), n, nullptr, nullptr);
        return out;
    }

    std::wstring FromUtf8(const std::string& text)
    {
        if (text.empty()) return {};
        const int n = MultiByteToWideChar(CP_UTF8, 0, text.data(), (int)text.size(), nullptr, 0);
        std::wstring out(n, L'\0');
        MultiByteToWideChar(CP_UTF8, 0, text.data(), (int)text.size(), out.data(), n);
        return out;
    }

    std::string GameVersion()
    {
        wchar_t path[MAX_PATH]{};
        const DWORD len = GetModuleFileNameW(GetModuleHandleW(L"GTA5.exe"), path, MAX_PATH);
        if (len == 0 || len >= MAX_PATH) return {};
        DWORD ignored = 0;
        const DWORD size = GetFileVersionInfoSizeW(path, &ignored);
        if (!size) return {};
        std::vector<BYTE> data(size);
        if (!GetFileVersionInfoW(path, 0, size, data.data())) return {};
        VS_FIXEDFILEINFO* info = nullptr;
        UINT infoSize = 0;
        if (!VerQueryValueW(data.data(), L"\\", reinterpret_cast<void**>(&info), &infoSize) || !info) return {};
        std::ostringstream v;
        v << HIWORD(info->dwFileVersionMS) << '.' << LOWORD(info->dwFileVersionMS) << '.'
          << HIWORD(info->dwFileVersionLS) << '.' << LOWORD(info->dwFileVersionLS);
        return v.str();
    }

    std::string Escape(const std::string& value)
    {
        std::string out;
        out.reserve(value.size() + 4);
        for (char c : value)
        {
            switch (c)
            {
            case '\\': out += "\\\\"; break;
            case '\t': out += "\\t"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            default: out += c;
            }
        }
        return out;
    }

    std::vector<std::string> Parse(const std::string& line)
    {
        std::vector<std::string> parts;
        std::string cur;
        for (size_t i = 0; i < line.size(); ++i)
        {
            const char c = line[i];
            if (c == '\t') { parts.push_back(cur); cur.clear(); continue; }
            if (c == '\\' && i + 1 < line.size())
            {
                const char n = line[++i];
                cur += n == 't' ? '\t' : n == 'n' ? '\n' : n == 'r' ? '\r' : n;
                continue;
            }
            cur += c;
        }
        parts.push_back(cur);
        return parts;
    }

    std::string Format(const std::vector<std::string>& fields)
    {
        std::string out;
        for (size_t i = 0; i < fields.size(); ++i)
        {
            if (i) out += '\t';
            out += i == 0 ? fields[i] : Escape(fields[i]);
        }
        return out;
    }

    std::string F(float v)
    {
        if (!(v == v) || v > 1e7f || v < -1e7f) v = 0.f;
        char buf[32];
        snprintf(buf, sizeof buf, "%.3f", v);
        // Незначащие нули не нужны в трафике 20 раз в секунду.
        std::string s(buf);
        while (!s.empty() && s.back() == '0') s.pop_back();
        if (!s.empty() && s.back() == '.') s.pop_back();
        return s.empty() || s == "-" ? "0" : s;
    }

    float ToFloat(const std::string& s, float fallback)
    {
        char* end = nullptr;
        _locale_t c = _create_locale(LC_NUMERIC, "C");
        const float v = _strtof_l(s.c_str(), &end, c);
        _free_locale(c);
        return end && end != s.c_str() && v == v ? v : fallback;
    }

    int ToInt(const std::string& s, int fallback)
    {
        char* end = nullptr;
        const long v = strtol(s.c_str(), &end, 10);
        return end && end != s.c_str() ? (int)v : fallback;
    }

    uint32_t ToUInt(const std::string& s, uint32_t fallback)
    {
        char* end = nullptr;
        const unsigned long long v = strtoull(s.c_str(), &end, 10);
        return end && end != s.c_str() ? (uint32_t)v : fallback;
    }

    uint32_t Joaat(const std::string& text)
    {
        uint32_t h = 0;
        for (unsigned char c : text)
        {
            if (c >= 'A' && c <= 'Z') c = (unsigned char)(c - 'A' + 'a');
            h += c;
            h += h << 10;
            h ^= h >> 6;
        }
        h += h << 3;
        h ^= h >> 11;
        h += h << 15;
        return h;
    }

    std::string SanitizeName(const std::string& name)
    {
        std::wstring w = FromUtf8(name), out;
        for (wchar_t c : w)
        {
            if (c < 32 || c == 127 || c == L'{' || c == L'}' || c == L'[' || c == L']' || c == L'<' || c == L'>') continue;
            if (c == 0x200B || c == 0x200C || c == 0x200D || c == 0xFEFF || c == 0x202E || c == 0x202D) continue;
            out += c;
        }
        while (!out.empty() && out.front() == L' ') out.erase(out.begin());
        while (!out.empty() && out.back() == L' ') out.pop_back();
        if (out.size() > 32) out.resize(32);
        if (out.size() < 2) out = L"Player";
        return ToUtf8(out);
    }
}
