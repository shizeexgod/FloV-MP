#include "crash.h"
#include "common.h"

#include <windows.h>
#include <dbghelp.h>

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <ctime>

namespace flov::crash
{
    namespace
    {
        LPTOP_LEVEL_EXCEPTION_FILTER g_previous = nullptr;
        std::atomic<bool> g_inside{ false };

        // Сколько дампов держать у игрока: каждый — несколько мегабайт.
        constexpr int kKeepDumps = 5;

        std::wstring Dir()
        {
            const std::wstring dir = DataDir() + L"\\crashes";
            CreateDirectoryW(dir.c_str(), nullptr);
            return dir;
        }

        std::wstring PendingPath() { return Dir() + L"\\pending.txt"; }

        /// Оставить только последние kKeepDumps дампов (имена сортируются по времени).
        void Rotate()
        {
            std::vector<std::wstring> names;
            WIN32_FIND_DATAW fd{};
            const HANDLE h = FindFirstFileW((Dir() + L"\\crash-*.dmp").c_str(), &fd);
            if (h == INVALID_HANDLE_VALUE) return;
            do names.push_back(fd.cFileName); while (FindNextFileW(h, &fd));
            FindClose(h);
            if ((int)names.size() <= kKeepDumps) return;
            std::sort(names.begin(), names.end());
            for (size_t i = 0; i + kKeepDumps < names.size(); i++)
            {
                const std::wstring base = Dir() + L"\\" + names[i];
                DeleteFileW(base.c_str());
                DeleteFileW((base.substr(0, base.size() - 4) + L".txt").c_str());
            }
        }

        /// Модуль, в котором лежит адрес, и смещение от его начала.
        void Locate(const void* address, std::wstring& module, uintptr_t& offset)
        {
            module = L"?";
            offset = (uintptr_t)address;
            HMODULE owner = nullptr;
            if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                    (LPCWSTR)address, &owner) || !owner)
                return;
            wchar_t path[MAX_PATH]{};
            if (GetModuleFileNameW(owner, path, MAX_PATH))
            {
                const wchar_t* slash = wcsrchr(path, L'\\');
                module = slash ? slash + 1 : path;
            }
            offset = (uintptr_t)address - (uintptr_t)owner;
        }

        /// Имя модуля для строки CRASH: только то, что пропустит сервер.
        std::string SafeModule(const std::wstring& module)
        {
            std::string out;
            for (wchar_t c : module)
                if ((c >= L'a' && c <= L'z') || (c >= L'A' && c <= L'Z') || (c >= L'0' && c <= L'9') ||
                    c == L'.' || c == L'_' || c == L'-')
                    out += (char)c;
            if (out.empty()) out = "unknown";
            return out.substr(0, 64);
        }

        void WriteDump(const std::wstring& path, EXCEPTION_POINTERS* ep)
        {
            // dbghelp грузим только сейчас: в обычной игре он не нужен.
            const HMODULE dbghelp = LoadLibraryW(L"dbghelp.dll");
            if (!dbghelp) return;
            using WriteFn = BOOL(WINAPI*)(HANDLE, DWORD, HANDLE, MINIDUMP_TYPE, PMINIDUMP_EXCEPTION_INFORMATION,
                                          PMINIDUMP_USER_STREAM_INFORMATION, PMINIDUMP_CALLBACK_INFORMATION);
            const auto write = (WriteFn)GetProcAddress(dbghelp, "MiniDumpWriteDump");
            const HANDLE file = CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
            if (write && file != INVALID_HANDLE_VALUE)
            {
                MINIDUMP_EXCEPTION_INFORMATION info{};
                info.ThreadId = GetCurrentThreadId();
                info.ExceptionPointers = ep;
                info.ClientPointers = FALSE;
                // Небольшой дамп: стеки потоков и список модулей — этого хватает,
                // чтобы увидеть место; полная память процесса — это гигабайты.
                write(GetCurrentProcess(), GetCurrentProcessId(), file,
                      (MINIDUMP_TYPE)(MiniDumpNormal | MiniDumpWithThreadInfo | MiniDumpWithUnloadedModules),
                      &info, nullptr, nullptr);
            }
            if (file != INVALID_HANDLE_VALUE) CloseHandle(file);
        }

        LONG WINAPI Filter(EXCEPTION_POINTERS* ep)
        {
            // Упали внутри своего же обработчика — второй раз не пишем.
            if (!g_inside.exchange(true) && ep && ep->ExceptionRecord)
            {
                const DWORD code = ep->ExceptionRecord->ExceptionCode;
                std::wstring module;
                uintptr_t offset = 0;
                Locate(ep->ExceptionRecord->ExceptionAddress, module, offset);

                SYSTEMTIME t;
                GetLocalTime(&t);
                wchar_t stamp[32];
                swprintf(stamp, 32, L"%04d%02d%02d-%02d%02d%02d", t.wYear, t.wMonth, t.wDay, t.wHour, t.wMinute, t.wSecond);
                const std::wstring base = Dir() + L"\\crash-" + stamp;
                WriteDump(base + L".dmp", ep);

                char codeText[16], offsetText[24];
                snprintf(codeText, sizeof codeText, "0x%08lX", (unsigned long)code);
                snprintf(offsetText, sizeof offsetText, "0x%llX", (unsigned long long)offset);
                const std::string safeModule = SafeModule(module);
                const std::string game = GameVersion();

                // Сводка для игрока и поддержки — рядом с дампом.
                FILE* f = nullptr;
                if (_wfopen_s(&f, (base + L".txt").c_str(), L"wb") == 0 && f)
                {
                    fprintf(f, "FloV:MP client %s, GTA5 %s\r\ncode=%s\r\nmodule=%s\r\noffset=%s\r\ndump=%s\r\n",
                            kClientVersion, game.c_str(), codeText, ToUtf8(module).c_str(), offsetText,
                            ToUtf8(base + L".dmp").c_str());
                    fclose(f);
                }
                // Для сервера: строка уйдёт при следующем входе. Время — в секундах
                // Unix, чтобы при отправке посчитать, как давно это было.
                if (_wfopen_s(&f, PendingPath().c_str(), L"wb") == 0 && f)
                {
                    fprintf(f, "%s\t%s\t%s\t%s\t%lld\t%s\n", codeText, safeModule.c_str(), offsetText, kClientVersion,
                            (long long)time(nullptr), game.c_str());
                    fclose(f);
                }
                FlushLogNow("ПАДЕНИЕ ИГРЫ: " + std::string(codeText) + " в " + safeModule + "+" + offsetText +
                         " — дамп: " + ToUtf8(base + L".dmp"));
            }
            return g_previous ? g_previous(ep) : EXCEPTION_CONTINUE_SEARCH;
        }
    }

    void Install()
    {
        const LPTOP_LEVEL_EXCEPTION_FILTER prev = SetUnhandledExceptionFilter(Filter);
        // Повторный вызов вернёт наш же обработчик — цепочку на себя не замыкаем.
        if (prev != Filter) g_previous = prev;
        static bool rotated = false;
        if (!rotated) { rotated = true; Rotate(); }
    }

    bool TakePending(std::vector<std::string>& fields)
    {
        FILE* f = nullptr;
        if (_wfopen_s(&f, PendingPath().c_str(), L"rb") != 0 || !f) return false;
        char buf[512]{};
        const size_t n = fread(buf, 1, sizeof buf - 1, f);
        fclose(f);
        std::string line(buf, n);
        while (!line.empty() && (line.back() == '\n' || line.back() == '\r')) line.pop_back();

        fields.clear();
        size_t start = 0;
        for (;;)
        {
            const size_t tab = line.find('\t', start);
            fields.push_back(line.substr(start, tab == std::string::npos ? std::string::npos : tab - start));
            if (tab == std::string::npos) break;
            start = tab + 1;
        }
        if (fields.size() < 6) { MarkSent(); return false; }   // испорчен — не слать вечно
        // Пятое поле в файле — момент падения; серверу — сколько секунд назад.
        const long long at = _atoi64(fields[4].c_str());
        const long long now = (long long)time(nullptr);
        fields[4] = std::to_string(at > 0 && now >= at ? now - at : 0);
        return true;
    }

    void MarkSent() { DeleteFileW(PendingPath().c_str()); }
}
