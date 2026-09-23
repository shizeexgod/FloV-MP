// Интерфейс клиента FloV:MP для GTA V Legacy 1.0.3889.0: чат, консоль F8,
// ники, загрузочный экран, уведомления. Рисуется поверх игры (Dear ImGui,
// DX11, колбэк Present из ScriptHookV). Внешний вид повторяет прежние
// HTML-экраны alt:V-клиента (chat/, console/, loading/), чтобы игроки и
// владельцы серверов не потеряли привычный интерфейс при переходе на 3889.
//
// Потоки: WndProc (клавиатура), игровой поток (данные, курсор), поток
// рендера (рисование). Всё общее — под g_mutex; журнал консоли — под своим
// g_logMutex, потому что Log() зовут отовсюду.

#include "ui.h"
#include "common.h"
#include "invoke.h"
#include "settings.h"

#include <d3d11.h>
#include <dxgi.h>
#include <algorithm>
#include <cmath>
#include <deque>
#include <mutex>

#include "imgui.h"
#include "backends/imgui_impl_dx11.h"

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "d3dcompiler.lib")

namespace flov::ui
{
    namespace
    {
        constexpr UINT kMsgApplyTitle = WM_APP + 0x46;

        // --- общее состояние ------------------------------------------------------
        std::mutex g_mutex;

        struct ChatLine { std::string time, author, text; uint32_t authorRgb; ULONGLONG tick; };
        std::deque<ChatLine> g_chat;
        constexpr size_t kChatHistory = 200;
        int g_chatScroll = 0;
        ULONGLONG g_chatWake = 0;

        std::vector<Label> g_labels;
        std::string g_watermark;
        int g_mic = 0;
        std::string g_notice;
        ULONGLONG g_noticeUntil = 0, g_noticeAt = 0;
        uint32_t g_accent = 0xFFFFFF;
        std::string g_brand = "FloV:MP";

        bool g_chatEnabled = false, g_chatOpen = false;
        std::wstring g_input;
        size_t g_caret = 0;
        std::vector<std::wstring> g_history;
        int g_historyPos = -1;
        std::vector<Command> g_commands;
        int g_acIndex = -1;               // выбранная подсказка (Tab)
        std::wstring g_acBase;            // что было набрано до Tab
        bool g_skipChar = false;
        ULONGLONG g_inputClosedAt = 0;
        int g_chatKey = 'T', g_consoleKey = VK_F8;

        bool g_connectOpen = false;
        std::wstring g_connectHost, g_connectName;
        int g_connectField = 0;
        bool g_connectRequested = false;

        std::vector<std::string> g_submitted;
        std::vector<int> g_hotkeys, g_watchKeys = { VK_F9 };

        // Меню сервера.
        bool g_menuOpen = false;
        bool g_escRequested = false;
        bool g_escMenuOff = false;
        bool g_gtaMenuOpen = false;
        std::string g_menuId, g_menuTitle;
        std::vector<MenuItem> g_menuItems;
        int g_menuSel = 0;
        std::vector<MenuEvent> g_menuEvents;

        // Консоль.
        bool g_consoleEnabled = true, g_consoleOpen = false;
        int g_consoleTab = 0, g_consoleFilter = 0, g_consoleField = 0, g_theme = 0;
        std::wstring g_consoleInput, g_search;
        std::vector<std::wstring> g_consoleHistory;
        int g_consoleHistoryPos = -1;
        int g_logScroll = 0;
        std::vector<std::string> g_consoleCommands;
        int g_adminLevel = 0;
        Stats g_stats;
        bool g_netgraph = false;
        std::deque<float> g_fpsHistory;
        ULONGLONG g_quitArmedUntil = 0;

        std::mutex g_logMutex;
        struct LogLine { std::string time, tag, text; };
        std::deque<LogLine> g_log;
        constexpr size_t kLogHistory = 600;

        // Мышь (консоль): позиция в долях экрана и щелчок.
        float g_mouseX = 0.5f, g_mouseY = 0.5f;
        bool g_mouseDown = false, g_clickPending = false;
        ImVec2 g_clickN, g_click; // щелчок: в долях экрана и в пикселях кадра
        int g_wheel = 0;

        // Загрузочный экран.
        bool g_loading = false;
        ULONGLONG g_loadingShownAt = 0, g_loadingHiddenAt = 0;
        std::string g_loadingTitle, g_loadingStep;
        float g_loadingPercent = -1.f, g_loadingShownPercent = 0.f;
        std::vector<std::string> g_tips;
        uint32_t g_loadingAccent = 0xFBBF24;

        // Окно игры.
        std::wstring g_title = L"FloV Multiplayer";
        HICON g_iconBig = nullptr, g_iconSmall = nullptr;

        // --- рендер ---------------------------------------------------------------
        HWND g_hwnd = nullptr;
        HHOOK g_keyboardHook = nullptr;
        WNDPROC g_originalWndProc = nullptr;
        ID3D11Device* g_device = nullptr;
        ID3D11DeviceContext* g_context = nullptr;
        bool g_imguiReady = false;
        LARGE_INTEGER g_lastFrame{}, g_freq{};
        float g_s = 1.f; // масштаб: 1.0 при высоте 1080

        // Шрифты загружаются в том размере, в каком рисуются — так текст чёткий.
        ImFont* g_text = nullptr;     // 14 — чат, консоль
        ImFont* g_textSm = nullptr;   // 12 — подписи, подсказки
        ImFont* g_bold = nullptr;     // 14 — автор, заголовки вкладок
        ImFont* g_name = nullptr;     // 15 — ники
        ImFont* g_title24 = nullptr;  // 24 — загрузочный экран
        ImFont* g_mono = nullptr;     // 12 — время, журнал, метрики
        ImFont* g_monoSm = nullptr;   // 11 — метки журнала, клавиши

        const char* const kThemeNames[] = { "Obsidian", "Slate", "Glass" };
        const char* const kFilterTags[] = { "", "CORE", "NET", "SYNC", "CHAT", "WARN", "ERR" };
        const char* const kFilterNames[] = { "Все", "Core", "Net", "Sync", "Чат", "Предупреждения", "Ошибки" };

        // Команды самой консоли (не уходят на сервер).
        const Command kLocalCommands[] = {
            { "help", "список команд консоли и сервера" },
            { "clear", "очистить журнал" },
            { "netgraph", "показатели сети и FPS на экране" },
            { "pos", "координаты в буфер обмена" },
            { "reconnect", "переподключиться к серверу" },
            { "theme", "сменить оформление консоли" },
            { "quit", "выйти из игры" },
        };

        std::string Now(bool seconds)
        {
            SYSTEMTIME t;
            GetLocalTime(&t);
            char buf[16];
            if (seconds) sprintf_s(buf, "%02d:%02d:%02d", t.wHour, t.wMinute, t.wSecond);
            else sprintf_s(buf, "%02d:%02d", t.wHour, t.wMinute);
            return buf;
        }

        // --- поля ввода -------------------------------------------------------------
        std::wstring* ActiveField()
        {
            if (g_connectOpen) return g_connectField == 0 ? &g_connectHost : &g_connectName;
            if (g_consoleOpen) return g_consoleField == 1 ? &g_search : &g_consoleInput;
            if (g_chatOpen) return &g_input;
            return nullptr;
        }

        void MarkClosed() { g_inputClosedAt = GetTickCount64(); }

        void CloseChat()
        {
            g_chatOpen = false;
            g_input.clear();
            g_caret = 0;
            g_historyPos = -1;
            g_chatScroll = 0;
            g_acIndex = -1;
            g_chatWake = GetTickCount64();
            MarkClosed();
        }

        void CloseConsole()
        {
            g_consoleOpen = false;
            g_consoleField = 0;
            g_caret = 0;
            g_quitArmedUntil = 0;
            MarkClosed();
        }

        void OpenConsole()
        {
            g_consoleOpen = true;
            g_consoleField = 0;
            g_caret = g_consoleInput.size();
            g_logScroll = 0;
        }

        void Paste(std::wstring& field, size_t limit)
        {
            if (!OpenClipboard(nullptr)) return;
            if (HANDLE h = GetClipboardData(CF_UNICODETEXT))
            {
                if (auto* text = static_cast<const wchar_t*>(GlobalLock(h)))
                {
                    for (const wchar_t* p = text; *p && field.size() < limit; ++p)
                        if (*p >= 32) { field.insert(g_caret, 1, *p); ++g_caret; }
                    GlobalUnlock(h);
                }
            }
            CloseClipboard();
        }

        void ConsoleLogLocked(const std::string& tag, const std::string& text)
        {
            std::lock_guard lock(g_logMutex);
            g_log.push_back({ Now(true), tag, text });
            while (g_log.size() > kLogHistory) g_log.pop_front();
        }

        /// Набрано в русской раскладке («/рудз» вместо «/help»): буквы той же
        /// клавиши латиницей. Нужно только для имени команды — текст сообщения
        /// не трогаем.
        std::wstring FromRussianLayout(const std::wstring& text)
        {
            static const wchar_t ru[] = L"йцукенгшщзхъфывапролджэячсмитьбюё";
            static const char en[] = "qwertyuiop[]asdfghjkl;'zxcvbnm,.`";
            std::wstring out = text;
            for (auto& ch : out)
            {
                const wchar_t lower = (ch >= L'А' && ch <= L'Я') ? ch + 32 : ch == L'Ё' ? L'ё' : ch;
                for (size_t i = 0; ru[i]; ++i)
                    if (ru[i] == lower) { ch = (wchar_t)en[i]; break; }
            }
            return out;
        }

        bool HasCyrillic(const std::wstring& t)
        {
            return std::any_of(t.begin(), t.end(), [](wchar_t c) { return c >= 0x400 && c <= 0x4FF; });
        }

        /// Подходящие команды для строки после «/» (или без неё — в консоли).
        std::vector<Command> Matches(const std::wstring& typedRaw, bool withLocal)
        {
            std::vector<Command> out;
            const std::wstring typed = HasCyrillic(typedRaw) ? FromRussianLayout(typedRaw) : typedRaw;
            const auto t = ToUtf8(typed);
            if (t.find(' ') != std::string::npos) return out;
            auto consider = [&](const Command& c) {
                if (c.name.rfind(t, 0) == 0 && out.size() < 8 &&
                    std::none_of(out.begin(), out.end(), [&](const Command& o) { return o.name == c.name; }))
                    out.push_back(c);
            };
            if (withLocal) for (const auto& c : kLocalCommands) consider(c);
            for (const auto& c : g_commands) consider(c);
            return out;
        }

        /// «/рудз привет» → «/help привет», если так набрана известная команда.
        void FixCommandLayout(std::wstring& line, bool slash)
        {
            if (slash && (line.empty() || line[0] != L'/')) return;
            const size_t start = slash ? 1 : 0;
            const size_t end = std::min(line.find(L' ', start), line.size());
            const std::wstring name = line.substr(start, end - start);
            if (name.empty() || !HasCyrillic(name)) return;
            const auto latin = ToUtf8(FromRussianLayout(name));
            auto known = [&](const Command& c) { return c.name == latin; };
            if (!std::any_of(g_commands.begin(), g_commands.end(), known) &&
                !(!slash && std::any_of(std::begin(kLocalCommands), std::end(kLocalCommands), known)))
                return;
            line.replace(start, name.size(), FromUtf8(latin));

            // У этих команд аргумент — латинское имя из игры (модель, оружие,
            // погода). На русской раскладке «/car adder» превращалось в
            // «/car фввук», и команда молча не срабатывала.
            static const char* kLatinArgs[] = { "car", "veh", "weapon", "gun", "givegun", "skin", "ped", "weather" };
            if (std::none_of(std::begin(kLatinArgs), std::end(kLatinArgs),
                             [&](const char* c) { return latin == c; }))
                return;
            const size_t argStart = start + FromUtf8(latin).size() + 1;
            if (argStart >= line.size()) return;
            const std::wstring args = line.substr(argStart);
            if (HasCyrillic(args)) line.replace(argStart, args.size(), FromRussianLayout(args));
        }

        void CycleAutocomplete(std::wstring& field, bool slash)
        {
            if (slash && (field.empty() || field[0] != L'/')) return;
            if (g_acIndex < 0) g_acBase = slash ? field.substr(1) : field;
            const auto list = Matches(g_acBase, !slash);
            if (list.empty()) return;
            g_acIndex = (g_acIndex + 1) % (int)list.size();
            field = (slash ? L"/" : L"") + FromUtf8(list[g_acIndex].name) + L" ";
            g_caret = field.size();
        }

        void HistoryStep(std::vector<std::wstring>& history, int& pos, std::wstring& field, bool up)
        {
            if (history.empty()) return;
            if (up) pos = pos < 0 ? (int)history.size() - 1 : std::max(0, pos - 1);
            else pos = pos < 0 ? -1 : pos + 1;
            if (pos >= (int)history.size()) pos = -1;
            field = pos < 0 ? L"" : history[pos];
            g_caret = field.size();
        }

        void Remember(std::vector<std::wstring>& history, const std::wstring& line)
        {
            if (line.empty() || (!history.empty() && history.back() == line)) return;
            history.push_back(line);
            if (history.size() > 50) history.erase(history.begin());
        }

        void ClearLogs()
        {
            std::lock_guard lock(g_logMutex);
            g_log.clear();
        }

        /// Команда консоли. Свои (clear, theme, help) — здесь, остальное — игре.
        void RunConsoleCommand(const std::string& raw)
        {
            std::string line = raw;
            while (!line.empty() && line.front() == ' ') line.erase(line.begin());
            if (!line.empty() && line.front() == '/') line.erase(line.begin());
            while (!line.empty() && line.back() == ' ') line.pop_back();
            if (line.empty()) return;
            ConsoleLogLocked("CMD", "> " + line);
            const auto name = line.substr(0, line.find(' '));
            if (name == "clear" || name == "cls") { ClearLogs(); return; }
            if (name == "theme")
            {
                g_theme = (g_theme + 1) % 3;
                ConsoleLogLocked("DEV", std::string("Оформление консоли: ") + kThemeNames[g_theme]);
                return;
            }
            if (name == "help")
            {
                ConsoleLogLocked("DEV", "Команды консоли:");
                for (const auto& c : kLocalCommands) ConsoleLogLocked("DEV", "  " + c.name + " — " + c.desc);
                if (!g_commands.empty())
                {
                    ConsoleLogLocked("DEV", "Команды сервера (можно без «/»):");
                    for (const auto& c : g_commands) ConsoleLogLocked("DEV", "  " + c.name + (c.desc.empty() ? "" : " — " + c.desc));
                }
                return;
            }
            g_consoleCommands.push_back(line);
        }

        bool EditKey(std::wstring& field, WPARAM vk)
        {
            if (g_caret > field.size()) g_caret = field.size();
            switch (vk)
            {
            case VK_BACK: if (g_caret > 0) { field.erase(g_caret - 1, 1); --g_caret; } return true;
            case VK_DELETE: if (g_caret < field.size()) field.erase(g_caret, 1); return true;
            case VK_LEFT: if (g_caret > 0) --g_caret; return true;
            case VK_RIGHT: if (g_caret < field.size()) ++g_caret; return true;
            case VK_HOME: g_caret = 0; return true;
            case VK_END: g_caret = field.size(); return true;
            }
            return false;
        }

        /// true — клавиша наша, игре её не отдавать.
        bool HandleKey(WPARAM vk)
        {
            // F12 — оверлей Rockstar/Social Club поверх игры. На сервере он не
            // нужен и мешает, поэтому клавиша не доходит до игры совсем.
            // Делается перехватом сообщения окна: файлы игры не трогаем.
            if (vk == VK_F12) return true;
            const bool ctrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
            const bool alt = (GetKeyState(VK_MENU) & 0x8000) != 0;
            std::lock_guard lock(g_mutex);

            if (g_connectOpen)
            {
                auto& field = g_connectField == 0 ? g_connectHost : g_connectName;
                switch (vk)
                {
                case VK_ESCAPE: g_connectOpen = false; MarkClosed(); return true;
                case VK_RETURN: g_connectRequested = true; g_connectOpen = false; MarkClosed(); return true;
                case VK_TAB: g_connectField ^= 1; g_caret = (g_connectField == 0 ? g_connectHost : g_connectName).size(); return true;
                }
                EditKey(field, vk);
                return true;
            }

            if (g_consoleOpen)
            {
                if (vk == (WPARAM)g_consoleKey || vk == VK_ESCAPE)
                {
                    if (vk == VK_ESCAPE && g_consoleField == 1) { g_consoleField = 0; g_caret = g_consoleInput.size(); return true; }
                    CloseConsole();
                    return true;
                }
                auto& field = g_consoleField == 1 ? g_search : g_consoleInput;
                if (ctrl && vk == 'F') { g_consoleTab = 0; g_consoleField = 1; g_caret = g_search.size(); return true; }
                if (ctrl && vk == 'L') { ClearLogs(); return true; }
                switch (vk)
                {
                case VK_RETURN:
                    if (g_consoleField == 1) { g_consoleField = 0; g_caret = g_consoleInput.size(); return true; }
                    FixCommandLayout(g_consoleInput, false);
                    Remember(g_consoleHistory, g_consoleInput);
                    RunConsoleCommand(ToUtf8(g_consoleInput));
                    g_consoleInput.clear();
                    g_caret = 0;
                    g_consoleHistoryPos = -1;
                    g_acIndex = -1;
                    g_logScroll = 0;
                    g_consoleTab = 0;
                    return true;
                case VK_TAB:
                    if (g_consoleField == 0) CycleAutocomplete(g_consoleInput, false);
                    return true;
                case VK_UP: case VK_DOWN:
                    if (g_consoleField == 0) HistoryStep(g_consoleHistory, g_consoleHistoryPos, g_consoleInput, vk == VK_UP);
                    return true;
                case VK_PRIOR: g_logScroll += 8; return true;
                case VK_NEXT: g_logScroll = std::max(0, g_logScroll - 8); return true;
                }
                if (vk == VK_BACK || vk == VK_DELETE) g_acIndex = -1;
                EditKey(field, vk);
                return true; // при открытой консоли игре клавиши не нужны
            }

            if (g_chatOpen)
            {
                switch (vk)
                {
                case VK_ESCAPE: CloseChat(); return true;
                case VK_RETURN:
                    if (!g_input.empty())
                    {
                        FixCommandLayout(g_input, true);
                        g_submitted.push_back(ToUtf8(g_input));
                        Remember(g_history, g_input);
                    }
                    CloseChat();
                    return true;
                case VK_TAB: CycleAutocomplete(g_input, true); return true;
                case VK_PRIOR: g_chatScroll = std::min<int>(g_chatScroll + 5, (int)g_chat.size()); return true;
                case VK_NEXT: g_chatScroll = std::max(0, g_chatScroll - 5); return true;
                case VK_UP: case VK_DOWN: HistoryStep(g_history, g_historyPos, g_input, vk == VK_UP); return true;
                }
                if (vk == VK_BACK || vk == VK_DELETE) g_acIndex = -1;
                EditKey(g_input, vk);
                return true;
            }

            // Меню сервера забирает только клавиши навигации — ходить и открыть чат можно.
            if (g_menuOpen && !alt)
            {
                const int count = (int)g_menuItems.size();
                switch (vk)
                {
                case VK_UP: if (count) g_menuSel = (g_menuSel + count - 1) % count; return true;
                case VK_DOWN: if (count) g_menuSel = (g_menuSel + 1) % count; return true;
                case VK_RETURN:
                    if (count) g_menuEvents.push_back({ g_menuId, g_menuSel });
                    return true;
                case VK_ESCAPE:
                case VK_BACK:
                    g_menuEvents.push_back({ g_menuId, -1 });
                    g_menuOpen = false;
                    return true;
                }
            }

            // Esc при закрытых окнах — наше меню (его собирает игровой поток).
            // Штатное меню GTA сюда не попадает: оно останавливает мир и скрипты.
            if (vk == VK_ESCAPE && !g_escMenuOff && !g_gtaMenuOpen)
            {
                g_escRequested = true;
                return true;
            }

            // Ничего не открыто: горячие клавиши.
            if (vk == (WPARAM)g_consoleKey && g_consoleEnabled && !alt)
            {
                OpenConsole();
                g_skipChar = vk >= '0' && vk <= 'Z';
                return true;
            }
            if (g_chatEnabled && !alt && (vk == (WPARAM)g_chatKey || vk == VK_OEM_2))
            {
                g_chatOpen = true;
                g_skipChar = true; // WM_CHAR этой же клавиши придёт следом — в строку его не пускаем
                g_input = vk == VK_OEM_2 ? L"/" : L"";
                g_caret = g_input.size();
                g_acIndex = -1;
                return true;
            }
            if (std::find(g_watchKeys.begin(), g_watchKeys.end(), (int)vk) != g_watchKeys.end())
                g_hotkeys.push_back((int)vk);
            return false;
        }

        bool HandleChar(WPARAM ch)
        {
            std::lock_guard lock(g_mutex);
            auto* field = ActiveField();
            if (!field) return false;
            if (g_skipChar) { g_skipChar = false; return true; }
            const size_t limit = g_connectOpen ? 64 : g_consoleOpen ? 256 : (size_t)std::clamp(settings::Int("chat.max_length", 256), 16, 1024);
            if (ch == 0x16) { Paste(*field, limit); return true; } // Ctrl+V
            if (ch < 32 || ch == 127) return true;
            if (field->size() >= limit) return true;
            if (g_caret > field->size()) g_caret = field->size();
            field->insert(g_caret, 1, (wchar_t)ch);
            ++g_caret;
            g_acIndex = -1;
            return true;
        }

        /// Клавиши-модификаторы, которыми переключают раскладку.
        bool IsLayoutModifier(WPARAM vk)
        {
            return vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT ||
                   vk == VK_MENU || vk == VK_LMENU || vk == VK_RMENU ||
                   vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL;
        }

        /// Оверлей Rockstar/Social Club игра открывает сама, читая клавиатуру
        /// напрямую (RawInput/DirectInput), поэтому перехвата оконных сообщений
        /// мало — его клавиши просто не приходят в WndProc. Низкоуровневый хук
        /// забирает их раньше игры. Работает только когда окно игры активно,
        /// чтобы не мешать остальной системе.
        LRESULT CALLBACK LowLevelKeyboard(int code, WPARAM wp, LPARAM lp)
        {
            if (code == HC_ACTION && lp)
            {
                const auto* key = reinterpret_cast<const KBDLLHOOKSTRUCT*>(lp);
                const bool ourWindow = g_hwnd && GetForegroundWindow() == g_hwnd;
                bool swallow = false;
                if (ourWindow)
                {
                    if (key->vkCode == VK_F12) swallow = true;
                    // В меню GTA оверлей висит на CapsLock — там он тоже не нужен.
                    else if (key->vkCode == VK_CAPITAL)
                    {
                        std::lock_guard lock(g_mutex);
                        swallow = g_gtaMenuOpen;
                    }
                }
                if (swallow) return 1;
            }
            return CallNextHookEx(g_keyboardHook, code, wp, lp);
        }

        LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
        {
            switch (msg)
            {
            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                // Shift, Alt и Ctrl отдаём системе всегда: ими переключают
                // раскладку (Shift+Alt, Ctrl+Shift), а если их перехватывать,
                // в чате и консоли невозможно сменить язык.
                if (IsLayoutModifier(wp)) break;
                if (HandleKey(wp)) return 0;
                break;
            case WM_KEYUP:
            case WM_SYSKEYUP:
                if (wp == VK_F12) return 0;   // см. HandleKey: оверлей Rockstar
                // Не отдаём отпускание Esc GTA: иначе её frontend может открыть
                // штатную паузу даже после того, как наш обработчик съел keydown.
                if (wp == VK_ESCAPE && !g_escMenuOff && !g_gtaMenuOpen) return 0;
                if (IsLayoutModifier(wp)) break;
                if (InputActive()) return 0;
                break;
            case WM_INPUTLANGCHANGE:
            case WM_INPUTLANGCHANGEREQUEST:
                break;   // смена раскладки — дело системы, не мешаем
            case WM_CHAR:
                if (HandleChar(wp)) return 0;
                break;
            case kMsgApplyTitle:
            {
                std::wstring title;
                { std::lock_guard lock(g_mutex); title = g_title; }
                SetWindowTextW(hwnd, title.c_str());
                if (g_iconBig) SendMessageW(hwnd, WM_SETICON, ICON_BIG, (LPARAM)g_iconBig);
                if (g_iconSmall) SendMessageW(hwnd, WM_SETICON, ICON_SMALL, (LPARAM)g_iconSmall);
                return 0;
            }
            }
            return CallWindowProcW(g_originalWndProc, hwnd, msg, wp, lp);
        }

        // --- рисование: общее ------------------------------------------------------
        ImU32 Rgb(uint32_t rgb, float alpha = 1.f)
        {
            return IM_COL32((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, (int)(255 * std::clamp(alpha, 0.f, 1.f)));
        }

        ImU32 Rgba(int r, int g, int b, float a) { return IM_COL32(r, g, b, (int)(255 * std::clamp(a, 0.f, 1.f))); }

        ImU32 WithAlpha(ImU32 c, float mul)
        {
            const int a = (int)(((c >> IM_COL32_A_SHIFT) & 0xFF) * std::clamp(mul, 0.f, 1.f));
            return (c & ~IM_COL32_A_MASK) | ((ImU32)a << IM_COL32_A_SHIFT);
        }

        float Px(ImFont* f) { return f->FontSize; }

        ImVec2 Measure(ImFont* f, const std::string& s, float size = 0)
        {
            return f->CalcTextSizeA(size > 0 ? size : f->FontSize, FLT_MAX, 0, s.c_str(), s.c_str() + s.size());
        }

        void Text(ImDrawList* dl, ImFont* f, ImVec2 p, ImU32 c, const std::string& s, float size = 0)
        {
            dl->AddText(f, size > 0 ? size : f->FontSize, p, c, s.c_str(), s.c_str() + s.size());
        }

        void ShadowText(ImDrawList* dl, ImFont* f, ImVec2 p, ImU32 c, const std::string& s, float size = 0)
        {
            const float a = ((c >> IM_COL32_A_SHIFT) & 0xFF) / 255.f;
            Text(dl, f, ImVec2(p.x + 1, p.y + 1), Rgba(0, 0, 0, a * 0.85f), s, size);
            Text(dl, f, p, c, s, size);
        }

        /// Текст с чёрной обводкой — как ники в alt:V и RAGE:MP.
        void OutlineText(ImDrawList* dl, ImFont* f, ImVec2 p, ImU32 c, const std::string& s, float size)
        {
            const float a = ((c >> IM_COL32_A_SHIFT) & 0xFF) / 255.f;
            const ImU32 o = Rgba(0, 0, 0, a * 0.9f);
            for (int dx = -1; dx <= 1; ++dx)
                for (int dy = -1; dy <= 1; ++dy)
                    if (dx || dy) Text(dl, f, ImVec2(p.x + dx, p.y + dy), o, s, size);
            Text(dl, f, p, c, s, size);
        }

        struct Segment { std::string text; uint32_t rgb; };

        std::vector<Segment> SplitColors(const std::string& text, uint32_t base)
        {
            std::vector<Segment> out;
            uint32_t color = base;
            std::string cur;
            for (size_t i = 0; i < text.size(); ++i)
            {
                if (text[i] == '{' && i + 7 < text.size() && text[i + 7] == '}')
                {
                    bool hex = true;
                    for (size_t k = i + 1; k < i + 7; ++k) hex = hex && isxdigit((unsigned char)text[k]);
                    if (hex)
                    {
                        if (!cur.empty()) out.push_back({ cur, color });
                        cur.clear();
                        color = (uint32_t)strtoul(text.substr(i + 1, 6).c_str(), nullptr, 16);
                        i += 7;
                        continue;
                    }
                }
                cur += text[i];
            }
            if (!cur.empty()) out.push_back({ cur, color });
            return out;
        }

        std::string StripColors(const std::string& text)
        {
            std::string out;
            for (const auto& s : SplitColors(text, 0)) out += s.text;
            return out;
        }

        /// Цветной текст с переносом по словам. startX — отступ первой строки
        /// (после времени и автора). Возвращает высоту; measure — только посчитать.
        float DrawWrapped(ImDrawList* dl, ImFont* f, ImVec2 origin, float startX, float width, float lineH,
                          const std::string& text, uint32_t base, float alpha, bool shadow, bool measure)
        {
            float x = origin.x + startX, y = origin.y;
            const float px = f->FontSize;
            for (const auto& seg : SplitColors(text, base))
            {
                const char* p = seg.text.c_str();
                const char* end = p + seg.text.size();
                while (p < end)
                {
                    const char* wrap = f->CalcWordWrapPositionA(1.f, p, end, origin.x + width - x);
                    if (wrap == p)
                    {
                        if (x > origin.x) { x = origin.x; y += lineH; continue; }
                        wrap = p + 1;
                        while (wrap < end && (*wrap & 0xC0) == 0x80) ++wrap; // не резать символ UTF-8
                    }
                    if (!measure)
                    {
                        const std::string part(p, wrap);
                        if (shadow) ShadowText(dl, f, ImVec2(x, y), Rgb(seg.rgb, alpha), part);
                        else Text(dl, f, ImVec2(x, y), Rgb(seg.rgb, alpha), part);
                    }
                    x += f->CalcTextSizeA(px, FLT_MAX, 0, p, wrap).x;
                    p = wrap;
                    if (p < end) { x = origin.x; y += lineH; while (p < end && *p == ' ') ++p; }
                }
            }
            return y - origin.y + lineH;
        }

        /// Обрезать строку по ширине с «…».
        std::string Fit(ImFont* f, const std::string& s, float width)
        {
            if (Measure(f, s).x <= width) return s;
            std::string out = s;
            while (!out.empty() && Measure(f, out + "…").x > width)
            {
                out.pop_back();
                while (!out.empty() && (out.back() & 0xC0) == 0x80) out.pop_back();
                if (!out.empty() && (out.back() & 0xC0) == 0xC0) out.pop_back();
            }
            return out + "…";
        }

        /// Клавиша-«кнопка» как <kbd> в прежнем HTML.
        float Kbd(ImDrawList* dl, ImVec2 p, const std::string& key, float alpha = 1.f)
        {
            const float s = g_s;
            const ImVec2 sz = Measure(g_monoSm, key);
            const ImVec2 b(p.x + sz.x + 10 * s, p.y + sz.y + 4 * s);
            dl->AddRectFilled(p, b, Rgba(255, 255, 255, 0.06f * alpha), 3 * s);
            dl->AddRect(p, b, Rgba(255, 255, 255, 0.12f * alpha), 3 * s);
            Text(dl, g_monoSm, ImVec2(p.x + 5 * s, p.y + 2 * s), Rgba(212, 212, 216, alpha), key);
            return b.x - p.x;
        }

        // Мышь консоли.
        ImVec2 MousePos(float w, float h) { return ImVec2(g_mouseX * w, g_mouseY * h); }
        bool Hover(ImVec2 a, ImVec2 b, ImVec2 m) { return m.x >= a.x && m.x < b.x && m.y >= a.y && m.y < b.y; }
        bool Clicked(ImVec2 a, ImVec2 b)
        {
            if (g_clickPending && Hover(a, b, g_click)) { g_clickPending = false; return true; }
            return false;
        }

        /// Поле ввода: курсор, выделение отсутствует (как в игровом чате).
        void DrawField(ImDrawList* dl, ImFont* f, ImVec2 textPos, float width, const std::wstring& value, bool focused,
                       const char* placeholder, ImU32 color)
        {
            const auto utf8 = ToUtf8(value);
            const auto before = ToUtf8(value.substr(0, std::min(g_caret, value.size())));
            float shift = 0;
            const float caretX = Measure(f, before).x;
            if (caretX > width - 8 * g_s) shift = caretX - (width - 8 * g_s); // длинная строка — видно конец
            dl->PushClipRect(ImVec2(textPos.x - 1, textPos.y - 4 * g_s), ImVec2(textPos.x + width, textPos.y + Px(f) + 4 * g_s), true);
            if (utf8.empty() && placeholder)
                Text(dl, f, textPos, Rgba(82, 82, 91, 1.f), placeholder);
            else
                Text(dl, f, ImVec2(textPos.x - shift, textPos.y), color, utf8);
            if (focused && (GetTickCount64() / 530) % 2 == 0)
            {
                const float cx = textPos.x + caretX - shift;
                dl->AddLine(ImVec2(cx, textPos.y), ImVec2(cx, textPos.y + Px(f)), Rgba(244, 244, 245, 1.f), std::max(1.f, 1.2f * g_s));
            }
            dl->PopClipRect();
        }

        // --- ники -------------------------------------------------------------------
        void DrawMicIcon(ImDrawList* dl, ImVec2 c, float h, ImU32 col)
        {
            // Капсула микрофона, дужка и ножка — узнаваемо и без шрифта со значками.
            const float w = h * 0.42f;
            dl->AddRectFilled(ImVec2(c.x - w / 2, c.y - h / 2), ImVec2(c.x + w / 2, c.y + h * 0.12f), col, w / 2);
            dl->PathArcTo(ImVec2(c.x, c.y - h * 0.05f), h * 0.36f, 0.15f, 3.14159f - 0.15f, 10);
            dl->PathStroke(col, 0, std::max(1.f, h * 0.1f));
            dl->AddLine(ImVec2(c.x, c.y + h * 0.31f), ImVec2(c.x, c.y + h * 0.5f), col, std::max(1.f, h * 0.1f));
        }

        void DrawLabels(ImDrawList* dl, float w, float h)
        {
            const float s = g_s;
            for (const auto& l : g_labels)
            {
                const float k = std::clamp(l.scale, 0.5f, 1.5f);
                const float a = std::clamp(l.alpha, 0.f, 1.f);
                const float px = Px(g_name) * k;
                const float cx = l.x * w;
                float y = l.y * h;

                const std::string idText = l.id >= 0 ? " (" + std::to_string(l.id) + ")" : "";
                const float nameW = Measure(g_name, l.name, px).x;
                const float idW = idText.empty() ? 0 : Measure(g_name, idText, px).x;
                const bool badge = l.adminLevel > 0;
                const float badgeW = badge ? Measure(g_monoSm, "ADMIN", Px(g_monoSm) * k).x + 8 * s * k : 0;
                const float micW = l.speaking ? px * 0.95f : 0;
                const float gap = 5 * s * k;
                const float total = (badge ? badgeW + gap : 0) + nameW + idW + (l.speaking ? gap + micW : 0);

                // Полоски под ником: броня (если есть) над здоровьем.
                const float barW = std::clamp(l.barWidth, 24.f, 240.f) * s * k;
                const float barH = std::max(2.f, std::clamp(l.barHeight, 2.f, 16.f) * s * k);
                const float barGap = 2 * s * k;
                const bool hp = l.health >= 0, ar = l.armor > 0;
                const float barsH = (hp ? barH : 0) + (ar ? barH + (hp ? barGap : 0) : 0);
                const float nameTop = y - barsH - (barsH > 0 ? 4 * s * k : 0) - px;

                float x = cx - total / 2;
                if (badge)
                {
                    const float bh = Px(g_monoSm) * k + 4 * s * k;
                    const float by = nameTop + (px - bh) / 2;
                    dl->AddRectFilled(ImVec2(x, by), ImVec2(x + badgeW, by + bh), Rgba(220, 38, 38, 0.9f * a), 3 * s * k);
                    Text(dl, g_monoSm, ImVec2(x + 4 * s * k, by + 2 * s * k), Rgba(255, 255, 255, a), "ADMIN", Px(g_monoSm) * k);
                    x += badgeW + gap;
                }
                OutlineText(dl, g_name, ImVec2(x, nameTop), Rgb(l.rgb, a), l.name, px);
                x += nameW;
                if (!idText.empty()) { OutlineText(dl, g_name, ImVec2(x, nameTop), Rgba(212, 212, 216, a), idText, px); x += idW; }
                if (l.speaking) DrawMicIcon(dl, ImVec2(x + gap + micW / 2, nameTop + px / 2), px * 0.8f, Rgba(74, 222, 128, a));

                if (!l.extra.empty())
                {
                    const float ex = Px(g_monoSm) * k;
                    const ImVec2 sz = Measure(g_monoSm, l.extra, ex);
                    OutlineText(dl, g_monoSm, ImVec2(cx - sz.x / 2, nameTop - ex - 3 * s * k), Rgb(0xE4E4E7, a), l.extra, ex);
                }

                float by = nameTop + px + 4 * s * k;
                auto bar = [&](float v, ImU32 fill) {
                    const ImVec2 b0(cx - barW / 2, by), b1(cx + barW / 2, by + barH);
                    dl->AddRectFilled(ImVec2(b0.x - 1, b0.y - 1), ImVec2(b1.x + 1, b1.y + 1), Rgba(0, 0, 0, 0.55f * a), 2 * s);
                    dl->AddRectFilled(b0, ImVec2(b0.x + barW * std::clamp(v, 0.f, 1.f), b1.y), fill, 1.5f * s);
                    by += barH + barGap;
                };
                if (ar) bar(l.armor, Rgb(l.armorColor, a));
                if (hp) bar(l.health, Rgb(l.health < 0.3f ? l.healthLowColor : l.healthColor, a));
            }
        }

        // --- чат ------------------------------------------------------------------------
        void DrawChat(ImDrawList* dl, float w, float h)
        {
            if (!g_chatEnabled && g_chat.empty()) return;
            const float s = g_s;
            const auto now = GetTickCount64();
            const float x = 24 * s, top = 24 * s;
            const float width = std::clamp((float)settings::Int("chat.width", 540), 300.f, 1200.f) * s;
            const int visible = std::clamp(settings::Int("chat.lines", 10), 3, 30) + (g_chatOpen ? 4 : 0);
            const int fadeSec = settings::Int("chat.fade_seconds", 15);
            const bool stamps = settings::Bool("chat.timestamps");
            const float lineH = Px(g_text) * 1.38f;

            // Лента гаснет целиком через fade_seconds после последнего сообщения
            // (как прежний чат), а не построчно — так легче читать переписку.
            float alpha = 1.f;
            if (!g_chatOpen && fadeSec > 0)
            {
                const float age = (float)(now - g_chatWake) / 1000.f;
                alpha = age <= fadeSec ? 1.f : std::max(0.f, 1.f - (age - fadeSec) / 0.6f);
            }

            const int end = (int)g_chat.size() - (g_chatOpen ? g_chatScroll : 0);
            const int begin = std::max(0, end - visible);
            // Строки с переносом: считаем высоты, чтобы лента росла вниз от верха.
            float y = top;
            if (alpha > 0.01f)
            {
                if (g_chatOpen)
                {
                    float total = 0;
                    for (int i = begin; i < end; ++i)
                    {
                        const auto& c = g_chat[i];
                        const float lead = (stamps ? Measure(g_mono, "[" + c.time + "] ").x : 0) +
                                           (c.author.empty() ? 0 : Measure(g_bold, c.author + ": ").x);
                        total += DrawWrapped(dl, g_text, ImVec2(x, 0), lead, width, lineH, c.text, 0xF4F4F5, 1, false, true) + 3 * s;
                    }
                    dl->AddRectFilled(ImVec2(x - 10 * s, top - 8 * s), ImVec2(x + width + 10 * s, top + std::max(total, lineH) + 8 * s),
                                      Rgba(0, 0, 0, 0.28f), 8 * s);
                }
                for (int i = begin; i < end; ++i)
                {
                    const auto& c = g_chat[i];
                    float lead = 0;
                    if (stamps)
                    {
                        const std::string t = "[" + c.time + "] ";
                        ShadowText(dl, g_mono, ImVec2(x, y + (Px(g_text) - Px(g_mono)) * 0.6f), Rgba(113, 113, 122, alpha), t);
                        lead += Measure(g_mono, t).x;
                    }
                    if (!c.author.empty())
                    {
                        const std::string a = c.author + ": ";
                        ShadowText(dl, g_bold, ImVec2(x + lead, y), Rgb(c.authorRgb, alpha), a);
                        lead += Measure(g_bold, a).x;
                    }
                    y += DrawWrapped(dl, g_text, ImVec2(x, y), lead, width, lineH, c.text, 0xF4F4F5, alpha, true, false) + 3 * s;
                }
            }
            if (!g_chatOpen) return;

            // Поле ввода: как в прежнем HTML — тёмная плашка, шеврон, подсказки клавиш.
            const float boxY = std::max(y, top + lineH) + 12 * s;
            const float boxH = Px(g_text) + 18 * s;
            const ImVec2 b0(x - 10 * s, boxY), b1(x + width + 10 * s, boxY + boxH);
            dl->AddRectFilled(b0, b1, Rgba(14, 14, 18, 0.94f), 8 * s);
            dl->AddRect(b0, b1, Rgba(255, 255, 255, 0.28f), 8 * s, 0, std::max(1.f, s));
            const float cy = boxY + boxH / 2;
            // Шеврон ›
            dl->AddLine(ImVec2(b0.x + 12 * s, cy - 4 * s), ImVec2(b0.x + 16 * s, cy), Rgb(g_accent), 2 * s);
            dl->AddLine(ImVec2(b0.x + 16 * s, cy), ImVec2(b0.x + 12 * s, cy + 4 * s), Rgb(g_accent), 2 * s);
            DrawField(dl, g_text, ImVec2(b0.x + 28 * s, cy - Px(g_text) / 2), width - 30 * s, g_input, true,
                      "Введите сообщение или команду (/)…", Rgba(244, 244, 245, 1.f));

            // Подвал: клавиши и счётчик.
            float fx = b0.x + 2 * s;
            const float fy = b1.y + 6 * s;
            auto hint = [&](const std::string& key, const std::string& what) {
                fx += Kbd(dl, ImVec2(fx, fy), key) + 5 * s;
                ShadowText(dl, g_textSm, ImVec2(fx, fy + 1 * s), Rgba(113, 113, 122, 1.f), what);
                fx += Measure(g_textSm, what).x + 14 * s;
            };
            hint("Enter", "Отправить");
            hint("Esc", "Закрыть");
            hint("↑↓", "История");
            hint("Tab", "Дополнить");
            const int limit = std::clamp(settings::Int("chat.max_length", 256), 16, 1024);
            const std::string counter = std::to_string(g_input.size()) + " / " + std::to_string(limit);
            ShadowText(dl, g_mono, ImVec2(b1.x - Measure(g_mono, counter).x - 2 * s, fy + 1 * s),
                       g_input.size() >= (size_t)limit ? Rgba(248, 113, 113, 1.f) : Rgba(82, 82, 91, 1.f), counter);

            // Подсказки команд под полем.
            if (!g_input.empty() && g_input[0] == L'/')
            {
                const auto list = Matches(g_acIndex >= 0 ? g_acBase : g_input.substr(1), false);
                if (!list.empty())
                {
                    const float rowH = Px(g_text) + 12 * s;
                    const ImVec2 p0(b0.x, fy + Px(g_monoSm) + 14 * s);
                    const ImVec2 p1(b1.x, p0.y + rowH * list.size() + 8 * s);
                    dl->AddRectFilled(p0, p1, Rgba(17, 17, 21, 0.97f), 10 * s);
                    dl->AddRect(p0, p1, Rgba(255, 255, 255, 0.08f), 10 * s);
                    for (size_t i = 0; i < list.size(); ++i)
                    {
                        const float ry = p0.y + 4 * s + rowH * i;
                        if ((int)i == g_acIndex)
                            dl->AddRectFilled(ImVec2(p0.x + 4 * s, ry), ImVec2(p1.x - 4 * s, ry + rowH), Rgba(255, 255, 255, 0.07f), 6 * s);
                        const std::string cmd = "/" + list[i].name;
                        Text(dl, g_mono, ImVec2(p0.x + 12 * s, ry + (rowH - Px(g_mono)) / 2), Rgba(244, 244, 245, 1.f), cmd);
                        const float dx = p0.x + 12 * s + std::max(Measure(g_mono, cmd).x + 14 * s, 130 * s);
                        Text(dl, g_textSm, ImVec2(dx, ry + (rowH - Px(g_textSm)) / 2), Rgba(113, 113, 122, 1.f),
                             Fit(g_textSm, list[i].desc, p1.x - dx - 12 * s));
                    }
                }
            }
        }

        // --- консоль F8 ----------------------------------------------------------------
        struct Theme { ImU32 bg, header, filter, logs, input, border, line; };

        Theme ThemeColors()
        {
            switch (g_theme)
            {
            case 1: return { Rgba(13, 17, 23, 1), Rgba(22, 27, 34, 1), Rgba(15, 20, 28, 1), Rgba(8, 11, 15, 1), Rgba(19, 24, 34, 1),
                             Rgba(33, 38, 45, 1), Rgba(30, 36, 48, 1) };
            case 2: return { Rgba(8, 8, 12, 0.93f), Rgba(18, 18, 26, 0.75f), Rgba(12, 12, 18, 0.7f), Rgba(4, 4, 7, 0.5f), Rgba(14, 14, 20, 0.85f),
                             Rgba(255, 255, 255, 0.12f), Rgba(255, 255, 255, 0.07f) };
            default: return { Rgba(9, 9, 11, 0.98f), Rgba(17, 17, 21, 1), Rgba(13, 13, 17, 1), Rgba(6, 6, 8, 1), Rgba(15, 15, 19, 1),
                              Rgba(255, 255, 255, 0.08f), Rgba(255, 255, 255, 0.06f) };
            }
        }

        ImU32 TagColor(const std::string& tag)
        {
            if (tag == "WARN") return Rgba(254, 240, 138, 1);
            if (tag == "ERR") return Rgba(252, 165, 165, 1);
            if (tag == "CHAT") return Rgba(56, 189, 248, 1);
            if (tag == "NET") return Rgba(148, 163, 184, 1);
            if (tag == "SYNC") return Rgba(203, 213, 225, 1);
            if (tag == "VOICE") return Rgba(134, 239, 172, 1);
            if (tag == "CMD") return Rgba(255, 255, 255, 1);
            if (tag == "DEV") return Rgba(228, 228, 231, 1);
            return Rgba(212, 212, 216, 1);
        }

        /// Кнопка консоли: прямоугольная (не «таблетка»), подсветка при наведении.
        bool Button(ImDrawList* dl, ImVec2 p, const std::string& label, ImVec2 m, bool active = false,
                    ImU32 color = 0, float minW = 0, ImFont* font = nullptr)
        {
            const float s = g_s;
            font = font ? font : g_monoSm;
            const ImVec2 sz = Measure(font, label);
            const ImVec2 b(p.x + std::max(sz.x + 14 * s, minW), p.y + 22 * s);
            const bool hover = Hover(p, b, m);
            dl->AddRectFilled(p, b, active ? Rgba(255, 255, 255, 0.10f) : hover ? Rgba(255, 255, 255, 0.07f) : Rgba(255, 255, 255, 0.03f), 5 * s);
            dl->AddRect(p, b, active ? Rgba(255, 255, 255, 0.22f) : Rgba(255, 255, 255, 0.08f), 5 * s);
            Text(dl, font, ImVec2(p.x + (b.x - p.x - sz.x) / 2, p.y + (22 * s - Px(font)) / 2),
                 color ? color : active ? Rgba(255, 255, 255, 1) : Rgba(161, 161, 170, 1), label);
            return Clicked(p, b);
        }

        float ButtonWidth(const std::string& label, float minW = 0, ImFont* font = nullptr)
        {
            return std::max(Measure(font ? font : g_monoSm, label).x + 14 * g_s, minW);
        }

        void DrawCursor(ImDrawList* dl, ImVec2 m)
        {
            const float s = std::max(1.f, g_s);
            const ImVec2 pts[] = { m, ImVec2(m.x, m.y + 17 * s), ImVec2(m.x + 4.5f * s, m.y + 13 * s),
                                   ImVec2(m.x + 7.5f * s, m.y + 19.5f * s), ImVec2(m.x + 10 * s, m.y + 18.5f * s),
                                   ImVec2(m.x + 7 * s, m.y + 12 * s), ImVec2(m.x + 12.5f * s, m.y + 12 * s) };
            dl->AddConcavePolyFilled(pts, 7, IM_COL32(255, 255, 255, 255));
            dl->AddPolyline(pts, 7, IM_COL32(0, 0, 0, 255), ImDrawFlags_Closed, 1.2f * s);
        }

        void DrawConsole(ImDrawList* dl, float w, float h)
        {
            const float s = g_s;
            const Theme th = ThemeColors();
            const ImVec2 m = MousePos(w, h);
            const float W = std::min(780 * s, w - 32 * s), H = std::min(440 * s, h - 32 * s);
            const ImVec2 p0(w - W - 16 * s, 16 * s), p1(w - 16 * s, 16 * s + H);

            // Тень и окно.
            for (int i = 1; i <= 4; ++i)
                dl->AddRectFilled(ImVec2(p0.x - i * 4 * s, p0.y - i * 2 * s), ImVec2(p1.x + i * 4 * s, p1.y + i * 7 * s), Rgba(0, 0, 0, 0.12f), 12 * s);
            dl->AddRectFilled(p0, p1, th.bg, 8 * s);

            // --- заголовок 44 ---
            const float hh = 44 * s;
            dl->AddRectFilled(p0, ImVec2(p1.x, p0.y + hh), th.header, 8 * s, ImDrawFlags_RoundCornersTop);
            dl->AddLine(ImVec2(p0.x, p0.y + hh), ImVec2(p1.x, p0.y + hh), th.line);
            float x = p0.x + 14 * s;
            const float by = p0.y + (hh - 22 * s) / 2;
            {
                // >_ FloV:MP
                const float cy = p0.y + hh / 2;
                dl->AddLine(ImVec2(x, cy - 4 * s), ImVec2(x + 4 * s, cy), Rgba(244, 244, 245, 1), 1.6f * s);
                dl->AddLine(ImVec2(x + 4 * s, cy), ImVec2(x, cy + 4 * s), Rgba(244, 244, 245, 1), 1.6f * s);
                dl->AddLine(ImVec2(x + 6 * s, cy + 5 * s), ImVec2(x + 12 * s, cy + 5 * s), Rgba(244, 244, 245, 1), 1.6f * s);
                x += 18 * s;
                const std::string brand = Fit(g_bold, g_brand, 160 * s);
                Text(dl, g_bold, ImVec2(x, cy - Px(g_bold) / 2), Rgba(244, 244, 245, 1), brand);
                x += Measure(g_bold, brand).x + 10 * s;
                const std::string role = g_adminLevel >= 8 ? "ОСНОВАТЕЛЬ" : g_adminLevel > 0 ? "АДМИН " + std::to_string(g_adminLevel) : "ИГРОК";
                const ImVec2 rs = Measure(g_monoSm, role);
                const ImU32 rc = g_adminLevel >= 8 ? Rgba(255, 199, 64, 1) : g_adminLevel > 0 ? Rgba(248, 113, 113, 1) : Rgba(161, 161, 170, 1);
                dl->AddRectFilled(ImVec2(x, cy - rs.y / 2 - 3 * s), ImVec2(x + rs.x + 12 * s, cy + rs.y / 2 + 3 * s), Rgba(255, 255, 255, 0.05f), 4 * s);
                dl->AddRect(ImVec2(x, cy - rs.y / 2 - 3 * s), ImVec2(x + rs.x + 12 * s, cy + rs.y / 2 + 3 * s), Rgba(255, 255, 255, 0.08f), 4 * s);
                Text(dl, g_monoSm, ImVec2(x + 6 * s, cy - rs.y / 2), rc, role);
                x += rs.x + 22 * s;
            }
            const char* tabs[] = { "Логи", "Сеть и FPS", "Сущности", "Инструменты" };
            for (int i = 0; i < 4; ++i)
            {
                if (Button(dl, ImVec2(x, by), tabs[i], m, g_consoleTab == i, 0, 0, g_textSm)) { g_consoleTab = i; g_consoleField = 0; }
                x += ButtonWidth(tabs[i], 0, g_textSm) + 4 * s;
            }

            // Справа: метрики, очистить, выход, закрыть.
            float rx = p1.x - 12 * s;
            auto rightButton = [&](const std::string& label, ImU32 color) {
                rx -= ButtonWidth(label);
                const bool c = Button(dl, ImVec2(rx, by), label, m, false, color);
                rx -= 5 * s;
                return c;
            };
            if (rightButton("✕", Rgba(228, 228, 231, 1))) CloseConsole();
            const bool armed = GetTickCount64() < g_quitArmedUntil;
            if (rightButton(armed ? "Точно выйти?" : "Выход", Rgba(248, 113, 113, 1)))
            {
                if (armed) g_consoleCommands.push_back("quit");
                else g_quitArmedUntil = GetTickCount64() + 3000; // выход — только повторным нажатием
            }
            if (g_consoleTab == 0 && rightButton("Очистить", 0)) ClearLogs();
            auto pill = [&](const std::string& text) {
                const ImVec2 sz = Measure(g_monoSm, text);
                rx -= sz.x + 14 * s;
                dl->AddRectFilled(ImVec2(rx, by + 2 * s), ImVec2(rx + sz.x + 14 * s, by + 20 * s), Rgba(255, 255, 255, 0.04f), 5 * s);
                dl->AddRect(ImVec2(rx, by + 2 * s), ImVec2(rx + sz.x + 14 * s, by + 20 * s), Rgba(255, 255, 255, 0.07f), 5 * s);
                Text(dl, g_monoSm, ImVec2(rx + 7 * s, by + (22 * s - Px(g_monoSm)) / 2), Rgba(228, 228, 231, 1), text);
                rx -= 5 * s;
            };
            pill(g_stats.connected ? std::to_string(g_stats.ping) + " мс" : "—");
            pill(g_stats.fps > 0 ? std::to_string((int)std::lround(g_stats.fps)) + " FPS" : "— FPS");

            float top = p0.y + hh;
            const float inputH = 44 * s;
            const ImVec2 areaMax(p1.x, p1.y - inputH);

            if (g_consoleTab == 0)
            {
                // --- фильтры и поиск 34 ---
                const float fh = 34 * s;
                dl->AddRectFilled(ImVec2(p0.x, top), ImVec2(p1.x, top + fh), th.filter);
                dl->AddLine(ImVec2(p0.x, top + fh), ImVec2(p1.x, top + fh), th.line);
                float fx = p0.x + 10 * s;
                const float fy = top + (fh - 22 * s) / 2;
                for (int i = 0; i < 7; ++i)
                {
                    if (Button(dl, ImVec2(fx, fy), kFilterNames[i], m, g_consoleFilter == i)) { g_consoleFilter = i; g_logScroll = 0; }
                    fx += ButtonWidth(kFilterNames[i]) + 4 * s;
                }
                const float sw = std::max(120 * s, p1.x - 10 * s - (fx + 10 * s));
                const ImVec2 s0(p1.x - 10 * s - sw, fy), s1(p1.x - 10 * s, fy + 22 * s);
                dl->AddRectFilled(s0, s1, Rgba(255, 255, 255, 0.03f), 5 * s);
                dl->AddRect(s0, s1, g_consoleField == 1 ? Rgba(255, 255, 255, 0.28f) : Rgba(255, 255, 255, 0.08f), 5 * s);
                if (Clicked(s0, s1)) { g_consoleField = 1; g_caret = g_search.size(); }
                DrawField(dl, g_monoSm, ImVec2(s0.x + 7 * s, fy + (22 * s - Px(g_monoSm)) / 2), sw - 14 * s, g_search,
                          g_consoleField == 1, "Поиск по логам… (Ctrl+F)", Rgba(228, 228, 231, 1));
                top += fh;

                // --- журнал ---
                dl->AddRectFilled(ImVec2(p0.x, top), areaMax, th.logs);
                std::vector<LogLine> rows;
                {
                    std::lock_guard lock(g_logMutex);
                    const std::string tag = kFilterTags[g_consoleFilter];
                    const std::string q = ToUtf8(g_search);
                    for (const auto& l : g_log)
                    {
                        if (!tag.empty() && l.tag != tag) continue;
                        if (!q.empty() && l.text.find(q) == std::string::npos && l.tag.find(q) == std::string::npos) continue;
                        rows.push_back(l);
                    }
                }
                const float timeW = 66 * s, tagW = 56 * s;
                const float textX = p0.x + 12 * s + timeW + tagW;
                const float textW = p1.x - 14 * s - textX;
                const float lineH = Px(g_mono) + 5 * s;
                if (Hover(ImVec2(p0.x, top), areaMax, m) && g_wheel != 0) g_logScroll = std::max(0, g_logScroll + g_wheel * 3);
                g_logScroll = std::min(g_logScroll, std::max(0, (int)rows.size() - 1));
                dl->PushClipRect(ImVec2(p0.x, top), areaMax, true);
                float y = areaMax.y - 6 * s;
                if (rows.empty())
                {
                    const std::string empty = g_search.empty() ? "Журнал пуст" : "Ничего не найдено";
                    Text(dl, g_textSm, ImVec2(p0.x + (W - Measure(g_textSm, empty).x) / 2, top + 30 * s), Rgba(82, 82, 91, 1), empty);
                }
                for (int i = (int)rows.size() - 1 - g_logScroll; i >= 0 && y > top; --i)
                {
                    const auto& r = rows[i];
                    const float hgt = DrawWrapped(dl, g_mono, ImVec2(textX, 0), 0, textW, lineH, r.text, 0xE4E4E7, 1, false, true);
                    y -= hgt;
                    const bool warn = r.tag == "WARN", err = r.tag == "ERR";
                    if (warn || err)
                        dl->AddRectFilled(ImVec2(p0.x + 6 * s, y - 1 * s), ImVec2(p1.x - 6 * s, y + hgt - 2 * s),
                                          err ? Rgba(252, 165, 165, 0.05f) : Rgba(254, 240, 138, 0.04f), 3 * s);
                    if (warn || err)
                        dl->AddRectFilled(ImVec2(p0.x + 6 * s, y - 1 * s), ImVec2(p0.x + 8 * s, y + hgt - 2 * s), TagColor(r.tag));
                    Text(dl, g_mono, ImVec2(p0.x + 12 * s, y), Rgba(82, 82, 91, 1), r.time);
                    const std::string tag = "[" + r.tag + "]";
                    Text(dl, g_monoSm, ImVec2(p0.x + 12 * s + timeW, y + (Px(g_mono) - Px(g_monoSm)) / 2), TagColor(r.tag), tag);
                    DrawWrapped(dl, g_mono, ImVec2(textX, y), 0, textW, lineH, r.text, err ? 0xFCA5A5 : warn ? 0xFEF08A : 0xE4E4E7, 1, false, false);
                }
                dl->PopClipRect();
                if (g_logScroll > 0)
                {
                    const std::string more = "↓ новые записи ниже (PgDn)";
                    const ImVec2 sz = Measure(g_monoSm, more);
                    const ImVec2 q0(p1.x - sz.x - 26 * s, areaMax.y - sz.y - 14 * s);
                    dl->AddRectFilled(q0, ImVec2(p1.x - 10 * s, areaMax.y - 6 * s), Rgba(24, 24, 27, 0.95f), 4 * s);
                    Text(dl, g_monoSm, ImVec2(q0.x + 8 * s, q0.y + 4 * s), Rgba(212, 212, 216, 1), more);
                    if (Clicked(q0, ImVec2(p1.x - 10 * s, areaMax.y - 6 * s))) g_logScroll = 0;
                }
            }
            else
            {
                dl->AddRectFilled(ImVec2(p0.x, top), areaMax, th.logs);
                float y = top + 14 * s;
                const float lx = p0.x + 18 * s, vx = p0.x + 210 * s;
                auto row = [&](const std::string& k, const std::string& v, ImU32 vc = 0) {
                    Text(dl, g_textSm, ImVec2(lx, y), Rgba(113, 113, 122, 1), k);
                    Text(dl, g_mono, ImVec2(vx, y), vc ? vc : Rgba(228, 228, 231, 1), v);
                    y += Px(g_mono) + 9 * s;
                };
                auto kb = [](uint64_t v) {
                    char b[32];
                    if (v >= 1024 * 1024) sprintf_s(b, "%.1f МБ/с", v / 1048576.0);
                    else sprintf_s(b, "%.1f КБ/с", v / 1024.0);
                    return std::string(b);
                };
                if (g_consoleTab == 1)
                {
                    char fps[64];
                    sprintf_s(fps, "%d  (кадр %.1f мс)", (int)std::lround(g_stats.fps), g_stats.frameMs);
                    row("FPS", fps, g_stats.fps < 30 ? Rgba(248, 113, 113, 1) : g_stats.fps < 50 ? Rgba(254, 240, 138, 1) : 0);
                    row("Пинг", g_stats.connected ? std::to_string(g_stats.ping) + " мс" : "—",
                        g_stats.ping > 150 ? Rgba(248, 113, 113, 1) : 0);
                    row("Состояние", g_stats.state.empty() ? "—" : g_stats.state);
                    row("Сервер", g_stats.server.empty() ? "—" : Fit(g_mono, g_stats.server, p1.x - vx - 18 * s));
                    row("Адрес", g_stats.endpoint.empty() ? "—" : g_stats.endpoint);
                    row("Игроков рядом / онлайн", std::to_string(g_stats.streamed) + " / " + std::to_string(g_stats.online));
                    row("Входящий трафик", kb(g_stats.bytesIn));
                    row("Исходящий трафик", kb(g_stats.bytesOut));

                    // График FPS за последние ~2 минуты.
                    const ImVec2 g0(lx, y + 6 * s), g1(p1.x - 18 * s, areaMax.y - 44 * s);
                    if (g1.y - g0.y > 30 * s && g_fpsHistory.size() > 1)
                    {
                        dl->AddRect(g0, g1, Rgba(255, 255, 255, 0.07f), 4 * s);
                        float maxFps = 60;
                        for (float f : g_fpsHistory) maxFps = std::max(maxFps, f);
                        std::vector<ImVec2> pts;
                        const float step = (g1.x - g0.x) / std::max<size_t>(1, 119);
                        const size_t off = 120 - std::min<size_t>(120, g_fpsHistory.size());
                        for (size_t i = 0; i < g_fpsHistory.size(); ++i)
                            pts.push_back(ImVec2(g0.x + (off + i) * step, g1.y - 4 * s - (g1.y - g0.y - 8 * s) * (g_fpsHistory[i] / maxFps)));
                        dl->AddPolyline(pts.data(), (int)pts.size(), Rgb(g_accent == 0xFFFFFF ? 0x4ADE80 : g_accent), 0, 1.5f * s);
                        Text(dl, g_monoSm, ImVec2(g0.x + 6 * s, g0.y + 4 * s), Rgba(82, 82, 91, 1), "FPS, 2 мин · макс " + std::to_string((int)maxFps));
                    }
                    if (Button(dl, ImVec2(lx, areaMax.y - 34 * s), g_netgraph ? "Netgraph на экране: вкл" : "Netgraph на экране: выкл", m, g_netgraph))
                        g_netgraph = !g_netgraph;
                }
                else if (g_consoleTab == 2)
                {
                    row("Игроки в зоне видимости", std::to_string(g_stats.entities.size()));
                    y += 4 * s;
                    dl->PushClipRect(ImVec2(p0.x, y), areaMax, true);
                    if (g_stats.entities.empty())
                        Text(dl, g_textSm, ImVec2(lx, y), Rgba(82, 82, 91, 1), "Рядом никого нет");
                    for (const auto& e : g_stats.entities)
                    {
                        if (y > areaMax.y) break;
                        Text(dl, g_mono, ImVec2(lx, y), Rgba(212, 212, 216, 1), Fit(g_mono, e, W - 36 * s));
                        y += Px(g_mono) + 6 * s;
                    }
                    dl->PopClipRect();
                }
                else
                {
                    Text(dl, g_textSm, ImVec2(lx, y), Rgba(113, 113, 122, 1), "Оформление консоли");
                    y += Px(g_textSm) + 8 * s;
                    float bx = lx;
                    for (int i = 0; i < 3; ++i)
                    {
                        if (Button(dl, ImVec2(bx, y), kThemeNames[i], m, g_theme == i, 0, 90 * s)) g_theme = i;
                        bx += ButtonWidth(kThemeNames[i], 90 * s) + 6 * s;
                    }
                    y += 36 * s;
                    Text(dl, g_textSm, ImVec2(lx, y), Rgba(113, 113, 122, 1), "Действия");
                    y += Px(g_textSm) + 8 * s;
                    struct Act { const char* label; const char* cmd; bool admin; };
                    const Act acts[] = {
                        { "Координаты в буфер (pos)", "pos", false },
                        { "Переподключиться", "reconnect", false },
                        { "Телепорт на метку (tpm)", "tpm", true },
                        { "NoClip", "noclip", true },
                        { "ESP: игроки → машины → всё → выкл", "esp", true },
                    };
                    bx = lx;
                    for (const auto& a : acts)
                    {
                        if (a.admin && g_adminLevel <= 0) continue;
                        const float bw = ButtonWidth(a.label, 0, g_textSm);
                        if (bx + bw > p1.x - 18 * s) { bx = lx; y += 30 * s; }
                        if (Button(dl, ImVec2(bx, y), a.label, m, false, 0, 0, g_textSm))
                        {
                            ConsoleLogLocked("CMD", std::string("> ") + a.cmd);
                            g_consoleCommands.push_back(a.cmd);
                        }
                        bx += bw + 6 * s;
                    }
                    if (g_adminLevel <= 0)
                    {
                        y += 36 * s;
                        Text(dl, g_textSm, ImVec2(lx, y), Rgba(82, 82, 91, 1), "Инструменты администратора появятся, когда вам выдадут права.");
                    }
                }
            }

            // --- строка ввода 44 ---
            const ImVec2 i0(p0.x, p1.y - inputH);
            dl->AddRectFilled(i0, p1, th.input, 8 * s, ImDrawFlags_RoundCornersBottom);
            dl->AddLine(i0, ImVec2(p1.x, i0.y), th.line);
            if (Clicked(i0, p1)) { g_consoleField = 0; g_caret = g_consoleInput.size(); }
            const float iy = i0.y + (inputH - Px(g_mono)) / 2;
            Text(dl, g_mono, ImVec2(p0.x + 14 * s, iy), Rgb(g_accent == 0xFFFFFF ? 0xA1A1AA : g_accent), ">");
            float hintsW = 0;
            {
                float hx = p1.x - 12 * s;
                for (const char* k : { "Esc", "Tab", "Enter" })
                {
                    hx -= Measure(g_monoSm, k).x + 10 * s;
                    Kbd(dl, ImVec2(hx, i0.y + (inputH - Px(g_monoSm) - 4 * s) / 2), k, 0.8f);
                    hx -= 5 * s;
                }
                hintsW = p1.x - hx;
            }
            DrawField(dl, g_mono, ImVec2(p0.x + 30 * s, iy), W - 44 * s - hintsW, g_consoleInput, g_consoleField == 0,
                      "Команда… (help — список, Tab — дополнить)", Rgba(244, 244, 245, 1));

            // Подсказки команд над строкой ввода.
            if (g_consoleField == 0 && !g_consoleInput.empty())
            {
                const auto list = Matches(g_acIndex >= 0 ? g_acBase : g_consoleInput, true);
                if (!list.empty())
                {
                    const float rowH = Px(g_mono) + 10 * s;
                    const ImVec2 a1(p1.x - 10 * s, i0.y - 4 * s), a0(p0.x + 10 * s, a1.y - rowH * list.size() - 8 * s);
                    dl->AddRectFilled(a0, a1, Rgba(17, 17, 21, 0.98f), 8 * s);
                    dl->AddRect(a0, a1, Rgba(255, 255, 255, 0.08f), 8 * s);
                    for (size_t i = 0; i < list.size(); ++i)
                    {
                        const float ry = a0.y + 4 * s + rowH * i;
                        if ((int)i == g_acIndex) dl->AddRectFilled(ImVec2(a0.x + 4 * s, ry), ImVec2(a1.x - 4 * s, ry + rowH), Rgba(255, 255, 255, 0.07f), 5 * s);
                        Text(dl, g_mono, ImVec2(a0.x + 10 * s, ry + 5 * s), Rgba(244, 244, 245, 1), list[i].name);
                        const float dx = a0.x + 10 * s + std::max(Measure(g_mono, list[i].name).x + 14 * s, 120 * s);
                        Text(dl, g_textSm, ImVec2(dx, ry + (rowH - Px(g_textSm)) / 2), Rgba(113, 113, 122, 1), Fit(g_textSm, list[i].desc, a1.x - dx - 10 * s));
                    }
                }
            }

            dl->AddRect(p0, p1, th.border, 8 * s);
            g_clickPending = false; // щелчок мимо кнопок — просто теряется
            g_wheel = 0;
        }

        // --- загрузочный экран ------------------------------------------------------------
        void DrawLoading(ImDrawList* dl, float w, float h)
        {
            const auto now = GetTickCount64();
            float fade = 1.f;
            if (!g_loading)
            {
                if (!g_loadingHiddenAt || now - g_loadingHiddenAt > 400) return;
                fade = 1.f - (now - g_loadingHiddenAt) / 400.f;
            }
            else fade = std::min(1.f, (now - g_loadingShownAt) / 150.f);
            const float s = g_s;
            const uint32_t acc = g_loadingAccent;

            dl->AddRectFilled(ImVec2(0, 0), ImVec2(w, h), Rgba(9, 9, 11, fade));
            // Мягкое свечение акцента в центре (как radial-gradient прежнего экрана).
            for (int i = 0; i < 6; ++i)
                dl->AddCircleFilled(ImVec2(w / 2, h * 0.4f), (760 - i * 110) * s, Rgb(acc, 0.012f * fade), 96);

            const float cw = std::min(460 * s, w - 48 * s);
            const float x0 = (w - cw) / 2;
            float y = h * 0.5f - 110 * s;

            // Бренд: квадратик + FLOV:MP вразрядку.
            {
                std::string brand = g_brand;
                for (auto& c : brand) c = (char)toupper((unsigned char)c); // латиница — прописными, как раньше
                const float track = 2.1f * s;
                float bw = 0;
                for (char c : brand) bw += Measure(g_bold, std::string(1, c), 13 * s).x + track;
                const float bx = (w - (bw + 19 * s)) / 2;
                dl->AddRectFilled(ImVec2(bx, y + 4 * s), ImVec2(bx + 9 * s, y + 13 * s), Rgb(acc, fade), 2 * s);
                float cx = bx + 19 * s;
                for (char c : brand)
                {
                    const std::string ch(1, c);
                    Text(dl, g_bold, ImVec2(cx, y), Rgb(acc, fade), ch, 13 * s);
                    cx += Measure(g_bold, ch, 13 * s).x + track;
                }
            }
            y += 44 * s;

            const std::string title = Fit(g_title24, g_loadingTitle.empty() ? "Подключение…" : g_loadingTitle, cw);
            Text(dl, g_title24, ImVec2((w - Measure(g_title24, title).x) / 2, y), Rgba(244, 244, 245, fade), title);
            y += Px(g_title24) + 10 * s;
            const std::string step = Fit(g_text, g_loadingStep, cw);
            Text(dl, g_text, ImVec2((w - Measure(g_text, step).x) / 2, y), Rgba(161, 161, 170, fade), step);
            y += Px(g_text) + 24 * s;

            // Полоса прогресса.
            const ImVec2 b0(x0, y), b1(x0 + cw, y + 4 * s);
            dl->AddRectFilled(ImVec2(b0.x - 1, b0.y - 1), ImVec2(b1.x + 1, b1.y + 1), Rgba(39, 39, 42, fade), 2 * s);
            dl->AddRectFilled(b0, b1, Rgba(23, 23, 27, fade), 2 * s);
            if (g_loadingPercent >= 0)
            {
                g_loadingShownPercent += (g_loadingPercent - g_loadingShownPercent) * 0.12f;
                dl->AddRectFilled(b0, ImVec2(b0.x + cw * std::clamp(g_loadingShownPercent / 100.f, 0.f, 1.f), b1.y), Rgb(acc, fade), 2 * s);
                const std::string pct = std::to_string((int)std::lround(g_loadingPercent)) + "%";
                Text(dl, g_mono, ImVec2((w - Measure(g_mono, pct).x) / 2, b1.y + 10 * s), Rgba(113, 113, 122, fade), pct);
            }
            else
            {
                // Неизвестный прогресс: бегущий отрезок, честнее выдуманных процентов.
                const float t = (float)((now - g_loadingShownAt) % 1400) / 1400.f;
                const float seg = cw * 0.35f;
                const float sx = b0.x - seg + (cw + seg) * t;
                dl->PushClipRect(b0, b1, true);
                dl->AddRectFilled(ImVec2(sx, b0.y), ImVec2(sx + seg, b1.y), Rgb(acc, fade), 2 * s);
                dl->PopClipRect();
            }
            y += 4 * s + 26 * s + 34 * s;

            // Подсказка, меняется каждые 7 секунд.
            if (!g_tips.empty())
            {
                dl->AddLine(ImVec2(x0, y - 18 * s), ImVec2(x0 + cw, y - 18 * s), Rgba(28, 28, 32, fade));
                const auto& tip = g_tips[((now - g_loadingShownAt) / 7000 + g_loadingShownAt / 7) % g_tips.size()];
                const float lineH = Px(g_textSm) * 1.5f;
                const float tw = std::min(cw, Measure(g_textSm, StripColors(tip)).x + 2);
                DrawWrapped(dl, g_textSm, ImVec2((w - tw) / 2, y), 0, tw, lineH, tip, 0x71717A, fade, false, false);
            }
        }

        // --- прочее: водяной знак, netgraph, микрофон, уведомления, окно F9 ----------------
        void DrawHud(ImDrawList* dl, float w, float h)
        {
            const float s = g_s;
            const auto now = GetTickCount64();
            float ty = 12 * s;
            if (!g_watermark.empty())
            {
                const ImVec2 sz = Measure(g_textSm, g_watermark);
                ShadowText(dl, g_textSm, ImVec2(w - sz.x - 18 * s, ty), Rgba(228, 228, 231, 0.8f), g_watermark);
                ty += sz.y + 6 * s;
            }
            if (g_netgraph)
            {
                char a[96], b[96], c[96];
                sprintf_s(a, "FPS %d  ·  кадр %.1f мс", (int)std::lround(g_stats.fps), g_stats.frameMs);
                sprintf_s(b, "пинг %d мс  ·  рядом %d  ·  онлайн %d", g_stats.ping, g_stats.streamed, g_stats.online);
                sprintf_s(c, "↓ %.1f КБ/с  ↑ %.1f КБ/с", g_stats.bytesIn / 1024.0, g_stats.bytesOut / 1024.0);
                float bw = 0;
                for (const char* l : { a, b, c }) bw = std::max(bw, Measure(g_monoSm, l).x);
                const ImVec2 q0(w - bw - 30 * s, ty), q1(w - 12 * s, ty + (Px(g_monoSm) + 4 * s) * 3 + 12 * s);
                dl->AddRectFilled(q0, q1, Rgba(9, 9, 11, 0.72f), 6 * s);
                float ly = q0.y + 6 * s;
                for (const char* l : { a, b, c }) { Text(dl, g_monoSm, ImVec2(q0.x + 9 * s, ly), Rgba(228, 228, 231, 1), l); ly += Px(g_monoSm) + 4 * s; }
            }

            if (g_mic != 0)
            {
                const bool ok = g_mic == 1;
                const std::string t = ok ? "Говорите" : "Нет микрофона";
                const ImVec2 sz = Measure(g_textSm, t);
                const float bw = sz.x + 34 * s, bh = sz.y + 12 * s;
                const ImVec2 q0((w - bw) / 2, h - 64 * s - bh), q1(q0.x + bw, q0.y + bh);
                dl->AddRectFilled(q0, q1, Rgba(9, 9, 11, 0.8f), 6 * s);
                dl->AddRect(q0, q1, ok ? Rgba(74, 222, 128, 0.45f) : Rgba(248, 113, 113, 0.45f), 6 * s);
                DrawMicIcon(dl, ImVec2(q0.x + 14 * s, q0.y + bh / 2), 13 * s, ok ? Rgba(74, 222, 128, 1) : Rgba(248, 113, 113, 1));
                Text(dl, g_textSm, ImVec2(q0.x + 26 * s, q0.y + 6 * s), Rgba(244, 244, 245, 1), t);
            }

            // Уведомление выезжает снизу и так же уходит вниз: заметно, но не
            // закрывает центр экрана. Ход — по кривой ease-out, чтобы движение
            // не выглядело рывком и не «дёргалось» при низком FPS.
            if (!g_notice.empty() && now < g_noticeUntil)
            {
                constexpr float kSlideMs = 260.f;
                const float left = (float)(g_noticeUntil - now), age = (float)(now - g_noticeAt);
                const float in = std::clamp(age / kSlideMs, 0.f, 1.f);
                const float out = std::clamp(left / kSlideMs, 0.f, 1.f);
                auto easeOut = [](float t) { const float u = 1.f - t; return 1.f - u * u * u; };
                const float a = std::min(easeOut(in), easeOut(out));
                const float maxW = 900 * s;
                const ImVec2 sz = g_text->CalcTextSizeA(Px(g_text), FLT_MAX, maxW, g_notice.c_str());
                const float padX = 16 * s, padY = 10 * s;
                const float boxH = sz.y + padY * 2;
                // Конечное место — над полосой голоса у нижнего края.
                const float restY = h - 120 * s - boxH;
                const float hidden = h + 10 * s;                       // старт и финиш за краем экрана
                const float y = hidden + (restY - hidden) * std::min(easeOut(in), easeOut(out));
                const ImVec2 pos((w - sz.x) / 2, y + padY);
                dl->AddRectFilled(ImVec2(pos.x - padX, y), ImVec2(pos.x + sz.x + padX, y + boxH), Rgba(9, 9, 11, 0.86f * a), 6 * s);
                dl->AddRectFilled(ImVec2(pos.x - padX, y), ImVec2(pos.x - padX + 3 * s, y + boxH),
                                  Rgb(g_accent == 0xFFFFFF ? 0xFF3D8A : g_accent, a));
                dl->AddText(g_text, Px(g_text), pos, Rgba(244, 244, 245, a), g_notice.c_str(), nullptr, maxW);
            }

            if (g_connectOpen)
            {
                const float pw = 520 * s, ph = 250 * s;
                const ImVec2 p0((w - pw) / 2, (h - ph) / 2);
                dl->AddRectFilled(ImVec2(0, 0), ImVec2(w, h), Rgba(0, 0, 0, 0.5f));
                dl->AddRectFilled(p0, ImVec2(p0.x + pw, p0.y + ph), Rgba(9, 9, 11, 0.97f), 8 * s);
                dl->AddRect(p0, ImVec2(p0.x + pw, p0.y + ph), Rgba(255, 255, 255, 0.08f), 8 * s);
                Text(dl, g_title24, ImVec2(p0.x + 24 * s, p0.y + 20 * s), Rgba(244, 244, 245, 1), "Подключение к серверу");
                auto field = [&](float fy, const char* label, const std::wstring& v, bool focus, const char* ph2) {
                    Text(dl, g_textSm, ImVec2(p0.x + 24 * s, p0.y + fy), Rgba(161, 161, 170, 1), label);
                    const ImVec2 f0(p0.x + 24 * s, p0.y + fy + 20 * s), f1(p0.x + pw - 24 * s, p0.y + fy + 20 * s + Px(g_text) + 16 * s);
                    dl->AddRectFilled(f0, f1, Rgba(14, 14, 18, 1), 6 * s);
                    dl->AddRect(f0, f1, focus ? Rgba(255, 255, 255, 0.28f) : Rgba(255, 255, 255, 0.08f), 6 * s);
                    DrawField(dl, g_text, ImVec2(f0.x + 12 * s, f0.y + 8 * s), f1.x - f0.x - 24 * s, v, focus, ph2, Rgba(244, 244, 245, 1));
                };
                field(66 * s, "Адрес сервера (IP:порт)", g_connectHost, g_connectField == 0, "127.0.0.1:7788");
                field(134 * s, "Ник", g_connectName, g_connectField == 1, "Nick_Name");
                float hx = p0.x + 24 * s;
                const float hy = p0.y + ph - 32 * s;
                static const std::pair<const char*, const char*> keys[] = { { "Enter", "подключиться" }, { "Tab", "следующее поле" }, { "Esc", "закрыть" } };
                for (const auto& [k, t] : keys)
                {
                    hx += Kbd(dl, ImVec2(hx, hy), k) + 5 * s;
                    Text(dl, g_textSm, ImVec2(hx, hy + 1 * s), Rgba(113, 113, 122, 1), t);
                    hx += Measure(g_textSm, t).x + 14 * s;
                }
            }
        }

        void DrawMenu(ImDrawList* dl, float w, float h)
        {
            if (!g_menuOpen) return;
            const float s = g_s;
            const float mw = 380 * s, rowH = Px(g_text) + 16 * s;
            const int visible = 10, count = (int)g_menuItems.size();
            const int first = std::clamp(g_menuSel - visible / 2, 0, std::max(0, count - visible));
            const int shown = std::min(visible, count);
            const ImVec2 p0(36 * s, h * 0.22f);
            const float headH = Px(g_title24) + 22 * s;
            const uint32_t accent = g_accent == 0xFFFFFF ? 0xFF3D8A : g_accent;
            dl->AddRectFilled(p0, ImVec2(p0.x + mw, p0.y + headH), Rgb(accent, 0.95f), 6 * s, ImDrawFlags_RoundCornersTop);
            Text(dl, g_title24, ImVec2(p0.x + 16 * s, p0.y + 11 * s), Rgba(255, 255, 255, 1), Fit(g_title24, g_menuTitle, mw - 90 * s));
            char pos[32];
            sprintf_s(pos, "%d / %d", count ? g_menuSel + 1 : 0, count);
            const ImVec2 psz = Measure(g_textSm, pos);
            Text(dl, g_textSm, ImVec2(p0.x + mw - psz.x - 14 * s, p0.y + (headH - psz.y) / 2), Rgba(255, 255, 255, 0.85f), pos);
            float y = p0.y + headH;
            dl->AddRectFilled(ImVec2(p0.x, y), ImVec2(p0.x + mw, y + rowH * shown), Rgba(9, 9, 11, 0.9f));
            for (int i = first; i < first + shown; ++i)
            {
                const bool sel = i == g_menuSel;
                if (sel) dl->AddRectFilled(ImVec2(p0.x, y), ImVec2(p0.x + mw, y + rowH), Rgba(244, 244, 245, 0.95f));
                Text(dl, g_text, ImVec2(p0.x + 16 * s, y + 8 * s), sel ? Rgba(9, 9, 11, 1) : Rgba(228, 228, 231, 1),
                     Fit(g_text, g_menuItems[i].label, mw - 32 * s));
                y += rowH;
            }
            const std::string& desc = count ? g_menuItems[g_menuSel].desc : std::string();
            float by = y;
            if (!desc.empty())
            {
                const ImVec2 dsz = g_textSm->CalcTextSizeA(Px(g_textSm), FLT_MAX, mw - 32 * s, desc.c_str());
                dl->AddRectFilled(ImVec2(p0.x, y), ImVec2(p0.x + mw, y + dsz.y + 16 * s), Rgba(18, 18, 22, 0.92f));
                dl->AddText(g_textSm, Px(g_textSm), ImVec2(p0.x + 16 * s, y + 8 * s), Rgba(161, 161, 170, 1), desc.c_str(), nullptr, mw - 32 * s);
                by = y + dsz.y + 16 * s;
            }
            dl->AddRectFilled(ImVec2(p0.x, by), ImVec2(p0.x + mw, by + 30 * s), Rgba(9, 9, 11, 0.9f), 6 * s, ImDrawFlags_RoundCornersBottom);
            float hx = p0.x + 12 * s;
            static const std::pair<const char*, const char*> keys[] = { { "↑↓", "выбор" }, { "Enter", "выбрать" }, { "Esc", "закрыть" } };
            for (const auto& [k, t] : keys)
            {
                hx += Kbd(dl, ImVec2(hx, by + 6 * s), k) + 5 * s;
                Text(dl, g_textSm, ImVec2(hx, by + 7 * s), Rgba(113, 113, 122, 1), t);
                hx += Measure(g_textSm, t).x + 12 * s;
            }
            (void)w;
        }

        void DrawFrame(float w, float h)
        {
            auto* dl = ImGui::GetBackgroundDrawList();
            g_s = std::max(0.6f, h / 1080.f);
            std::lock_guard lock(g_mutex);
            g_click = ImVec2(g_clickN.x * w, g_clickN.y * h);
            if (g_loading || g_loadingHiddenAt)
            {
                DrawLoading(dl, w, h);
                if (g_loading) { g_clickPending = false; g_wheel = 0; return; }
            }
            DrawLabels(dl, w, h);
            DrawChat(dl, w, h);
            DrawMenu(dl, w, h);
            DrawHud(dl, w, h);
            if (g_consoleOpen)
            {
                DrawConsole(dl, w, h);
                DrawCursor(dl, MousePos(w, h));
            }
            else { g_clickPending = false; g_wheel = 0; }
        }

        // --- DX11 и шрифты --------------------------------------------------------------
        ImFont* LoadFont(const std::vector<const char*>& paths, float px, bool withSymbols)
        {
            auto& io = ImGui::GetIO();
            static const ImWchar ranges[] = { 0x0020, 0x00FF, 0x0400, 0x052F, 0x2000, 0x206F, 0x20A0, 0x20CF, 0x2100, 0x218F, 0 };
            ImFontConfig cfg;
            cfg.OversampleH = 3;
            cfg.OversampleV = 1;
            cfg.PixelSnapH = true;
            ImFont* font = nullptr;
            for (const char* path : paths)
            {
                if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES) continue;
                font = io.Fonts->AddFontFromFileTTF(path, px, &cfg, ranges);
                if (font) break;
            }
            if (!font)
            {
                ImFontConfig def;
                def.SizePixels = px;
                return io.Fonts->AddFontDefault(&def);
            }
            if (withSymbols)
            {
                // Стрелки, рамки и значки (↑ ↓ ✕ • ►) — из Segoe UI Symbol, иначе вместо них «?».
                static const ImWchar symbols[] = { 0x2190, 0x21FF, 0x2500, 0x25FF, 0x2600, 0x27BF, 0 };
                ImFontConfig merge;
                merge.MergeMode = true;
                merge.OversampleH = 3;
                merge.PixelSnapH = true;
                for (const char* path : { "C:\\Windows\\Fonts\\seguisym.ttf", "C:\\Windows\\Fonts\\segoeui.ttf" })
                    if (GetFileAttributesA(path) != INVALID_FILE_ATTRIBUTES && io.Fonts->AddFontFromFileTTF(path, px, &merge, symbols))
                        break;
            }
            return font;
        }

        void LoadIcons()
        {
            HMODULE self = nullptr;
            GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                               reinterpret_cast<LPCWSTR>(&LoadIcons), &self);
            if (!self) return;
            g_iconBig = (HICON)LoadImageW(self, MAKEINTRESOURCEW(101), IMAGE_ICON, GetSystemMetrics(SM_CXICON), GetSystemMetrics(SM_CYICON), 0);
            g_iconSmall = (HICON)LoadImageW(self, MAKEINTRESOURCEW(101), IMAGE_ICON, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), 0);
        }

        void EnsureImGui(IDXGISwapChain* swapChain)
        {
            if (g_imguiReady) return;
            if (FAILED(swapChain->GetDevice(__uuidof(ID3D11Device), reinterpret_cast<void**>(&g_device)))) return;
            g_device->GetImmediateContext(&g_context);
            DXGI_SWAP_CHAIN_DESC desc{};
            swapChain->GetDesc(&desc);
            g_hwnd = desc.OutputWindow;

            ImGui::CreateContext();
            auto& io = ImGui::GetIO();
            io.IniFilename = nullptr;
            io.LogFilename = nullptr;
            io.ConfigFlags |= ImGuiConfigFlags_NoMouseCursorChange;

            const float s = std::max(0.6f, (float)desc.BufferDesc.Height / 1080.f);
            const std::vector<const char*> regular = { "C:\\Windows\\Fonts\\segoeui.ttf", "C:\\Windows\\Fonts\\arial.ttf", "C:\\Windows\\Fonts\\tahoma.ttf" };
            const std::vector<const char*> semibold = { "C:\\Windows\\Fonts\\seguisb.ttf", "C:\\Windows\\Fonts\\segoeuib.ttf", "C:\\Windows\\Fonts\\arialbd.ttf" };
            const std::vector<const char*> bold = { "C:\\Windows\\Fonts\\segoeuib.ttf", "C:\\Windows\\Fonts\\arialbd.ttf", "C:\\Windows\\Fonts\\tahomabd.ttf" };
            const std::vector<const char*> mono = { "C:\\Windows\\Fonts\\consola.ttf", "C:\\Windows\\Fonts\\cour.ttf" };
            g_text = LoadFont(regular, std::round(14.5f * s), true);
            g_textSm = LoadFont(regular, std::round(12.5f * s), true);
            g_bold = LoadFont(semibold, std::round(14.5f * s), true);
            g_name = LoadFont(bold, std::round(16.f * s), false);
            g_title24 = LoadFont(semibold, std::round(24.f * s), true);
            g_mono = LoadFont(mono, std::round(12.5f * s), true);
            g_monoSm = LoadFont(mono, std::round(11.f * s), true);

            ImGui_ImplDX11_Init(g_device, g_context);
            QueryPerformanceFrequency(&g_freq);
            QueryPerformanceCounter(&g_lastFrame);

            LoadIcons();
            g_originalWndProc = reinterpret_cast<WNDPROC>(SetWindowLongPtrW(g_hwnd, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(WndProc)));
            if (!g_keyboardHook)
            {
                // Модуль хука — наш ASI, а не GTA5.exe: с чужим handle Windows
                // хук ставить отказывается, и оверлей Rockstar снова вылезал бы.
                HMODULE self = nullptr;
                GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                   reinterpret_cast<LPCWSTR>(&LowLevelKeyboard), &self);
                g_keyboardHook = SetWindowsHookExW(WH_KEYBOARD_LL, LowLevelKeyboard, self, 0);
                if (g_keyboardHook) Log("ui: перехват клавиш Rockstar включён");
                else Log("ui: не удалось поставить перехват клавиш, ошибка " + std::to_string(GetLastError()));
            }
            PostMessageW(g_hwnd, kMsgApplyTitle, 0, 0);
            g_imguiReady = true;
            Log("ui: оверлей готов (" + std::to_string(desc.BufferDesc.Width) + "x" + std::to_string(desc.BufferDesc.Height) + ")");
        }

        void OnPresent(void* raw)
        {
            auto* swapChain = static_cast<IDXGISwapChain*>(raw);
            __try
            {
                EnsureImGui(swapChain);
            }
            __except (EXCEPTION_EXECUTE_HANDLER) { return; }
            if (!g_imguiReady) return;

            ID3D11Texture2D* back = nullptr;
            if (FAILED(swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&back))) || !back) return;
            D3D11_TEXTURE2D_DESC bd{};
            back->GetDesc(&bd);
            ID3D11RenderTargetView* rtv = nullptr;
            // Свой view на каждый кадр: закэшированный держал бы буфер и ломал
            // смену разрешения (ResizeBuffers).
            if (FAILED(g_device->CreateRenderTargetView(back, nullptr, &rtv))) { back->Release(); return; }

            LARGE_INTEGER now;
            QueryPerformanceCounter(&now);
            auto& io = ImGui::GetIO();
            io.DeltaTime = std::max(1e-4f, (float)(now.QuadPart - g_lastFrame.QuadPart) / (float)g_freq.QuadPart);
            g_lastFrame = now;
            io.DisplaySize = ImVec2((float)bd.Width, (float)bd.Height);

            ImGui_ImplDX11_NewFrame();
            ImGui::NewFrame();
            DrawFrame((float)bd.Width, (float)bd.Height);
            ImGui::Render();

            ID3D11RenderTargetView* prevRtv = nullptr;
            ID3D11DepthStencilView* prevDsv = nullptr;
            g_context->OMGetRenderTargets(1, &prevRtv, &prevDsv);
            g_context->OMSetRenderTargets(1, &rtv, nullptr);
            ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());
            g_context->OMSetRenderTargets(1, &prevRtv, prevDsv);
            if (prevRtv) prevRtv->Release();
            if (prevDsv) prevDsv->Release();
            rtv->Release();
            back->Release();
        }
    }

    namespace
    {
        /// Журнал клиента → консоль F8 с меткой по смыслу строки.
        void LogToConsole(const std::string& text)
        {
            auto has = [&](const char* w) { return text.find(w) != std::string::npos; };
            const char* tag = "CORE";
            if (has("ошибк") || has("не удалось") || has("не найден") || has("недоступ") || has("сбой")) tag = "ERR";
            else if (has("устарел") || has("неизвестн") || has("предупрежд") || has("игнорирую")) tag = "WARN";
            else if (text.rfind("голос", 0) == 0) tag = "VOICE";
            else if (text.rfind("игрок [", 0) == 0) tag = "SYNC";
            else if (has("соединен") || has("подключ") || has("сервер") || has("welcome") || has("порт")) tag = "NET";
            ConsoleLogLocked(tag, text);
        }
    }

    void Init()
    {
        SetLogHook(&LogToConsole);
        if (shv::presentCallbackRegister) shv::presentCallbackRegister(OnPresent);
        else Log("ui: ScriptHookV без presentCallbackRegister — оверлей недоступен");
    }

    void Shutdown()
    {
        if (shv::presentCallbackUnregister) shv::presentCallbackUnregister(OnPresent);
        if (g_keyboardHook) { UnhookWindowsHookEx(g_keyboardHook); g_keyboardHook = nullptr; }
        if (g_hwnd && g_originalWndProc)
            SetWindowLongPtrW(g_hwnd, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(g_originalWndProc));
    }

    // --- чат ------------------------------------------------------------------------------
    void AddChat(const std::string& text, const std::string& author, uint32_t authorRgb)
    {
        {
            std::lock_guard lock(g_mutex);
            g_chat.push_back({ Now(false), author, text, authorRgb, GetTickCount64() });
            while (g_chat.size() > kChatHistory) g_chat.pop_front();
            g_chatWake = GetTickCount64();
            if (g_chatScroll > 0) ++g_chatScroll; // читающий историю не теряет место
        }
        ConsoleLog("CHAT", (author.empty() ? "" : author + ": ") + StripColors(text));
    }

    void ClearChat()
    {
        std::lock_guard lock(g_mutex);
        g_chat.clear();
        g_chatScroll = 0;
    }

    void SetChatEnabled(bool enabled)
    {
        std::lock_guard lock(g_mutex);
        g_chatEnabled = enabled;
        if (!enabled && g_chatOpen) CloseChat();
    }

    void SetCommands(std::vector<Command> commands)
    {
        std::sort(commands.begin(), commands.end(), [](const Command& a, const Command& b) { return a.name < b.name; });
        std::lock_guard lock(g_mutex);
        g_commands = std::move(commands);
        g_acIndex = -1;
    }

    std::vector<std::string> TakeSubmittedChat()
    {
        std::lock_guard lock(g_mutex);
        return std::exchange(g_submitted, {});
    }

    // --- консоль ------------------------------------------------------------------------------
    void ConsoleLog(const std::string& tag, const std::string& text) { ConsoleLogLocked(tag, text); }

    void SetConsoleEnabled(bool enabled)
    {
        std::lock_guard lock(g_mutex);
        g_consoleEnabled = enabled;
        if (!enabled && g_consoleOpen) CloseConsole();
    }

    void SetAdminLevel(int level)
    {
        std::lock_guard lock(g_mutex);
        g_adminLevel = level;
    }

    bool ConsoleOpen()
    {
        std::lock_guard lock(g_mutex);
        return g_consoleOpen;
    }

    std::vector<std::string> TakeConsoleCommands()
    {
        std::lock_guard lock(g_mutex);
        return std::exchange(g_consoleCommands, {});
    }

    void SetStats(const Stats& stats)
    {
        std::lock_guard lock(g_mutex);
        g_stats = stats;
        g_fpsHistory.push_back(stats.fps);
        while (g_fpsHistory.size() > 120) g_fpsHistory.pop_front();
    }

    void SetNetgraph(bool on)
    {
        std::lock_guard lock(g_mutex);
        g_netgraph = on;
    }

    bool Netgraph()
    {
        std::lock_guard lock(g_mutex);
        return g_netgraph;
    }

    void SetConsoleTheme(const std::string& name)
    {
        std::lock_guard lock(g_mutex);
        g_theme = name == "slate" ? 1 : name == "glass" ? 2 : 0;
    }

    void SetCursor(float nx, float ny, bool left, int wheel)
    {
        std::lock_guard lock(g_mutex);
        g_mouseX = std::clamp(nx, 0.f, 1.f);
        g_mouseY = std::clamp(ny, 0.f, 1.f);
        if (left && !g_mouseDown)
        {
            g_clickPending = true;
            g_clickN = ImVec2(g_mouseX, g_mouseY); // в пиксели — в DrawFrame, где известен размер кадра
        }
        g_mouseDown = left;
        g_wheel += wheel;
    }

    // --- мир и HUD ---------------------------------------------------------------------------
    void SetLabels(std::vector<Label>&& labels)
    {
        std::lock_guard lock(g_mutex);
        g_labels = std::move(labels);
    }

    void SetWatermark(const std::string& text)
    {
        std::lock_guard lock(g_mutex);
        g_watermark = text;
    }

    void SetMicIndicator(int state)
    {
        std::lock_guard lock(g_mutex);
        g_mic = state;
    }

    void Notify(const std::string& text, int ms)
    {
        {
            std::lock_guard lock(g_mutex);
            g_notice = text;
            g_noticeAt = GetTickCount64();
            g_noticeUntil = g_noticeAt + ms;
        }
        ConsoleLog("CORE", text);
    }

    void OpenMenu(const std::string& id, const std::string& title, std::vector<MenuItem> items)
    {
        std::lock_guard lock(g_mutex);
        g_menuOpen = true;
        g_menuId = id;
        g_menuTitle = title.empty() ? "Меню" : title;
        g_menuItems = std::move(items);
        g_menuSel = 0;
    }

    void SetGtaMenuOpen(bool open)
    {
        std::lock_guard lock(g_mutex);
        g_gtaMenuOpen = open;
    }

    void SetEscMenu(bool ours)
    {
        std::lock_guard lock(g_mutex);
        g_escMenuOff = !ours;
    }

    bool TakeEscRequest()
    {
        std::lock_guard lock(g_mutex);
        return std::exchange(g_escRequested, false);
    }

    void CloseMenu()
    {
        std::lock_guard lock(g_mutex);
        g_menuOpen = false;
    }

    std::vector<MenuEvent> TakeMenuEvents()
    {
        std::lock_guard lock(g_mutex);
        return std::exchange(g_menuEvents, {});
    }

    void SetAccent(uint32_t rgb)
    {
        std::lock_guard lock(g_mutex);
        g_accent = rgb;
    }

    void SetBrand(const std::string& name)
    {
        std::lock_guard lock(g_mutex);
        g_brand = name.empty() ? "FloV:MP" : name.substr(0, 40);
    }

    // --- загрузочный экран ------------------------------------------------------------------------
    void ShowLoading(const std::string& title)
    {
        std::lock_guard lock(g_mutex);
        if (!g_loading)
        {
            g_loadingShownAt = GetTickCount64();
            g_loadingShownPercent = 0;
        }
        g_loading = true;
        g_loadingHiddenAt = 0;
        g_loadingTitle = title;
        if (g_chatOpen) CloseChat();
        if (g_consoleOpen) CloseConsole();
    }

    void LoadingStep(const std::string& text, float percent)
    {
        {
            std::lock_guard lock(g_mutex);
            g_loadingStep = text;
            g_loadingPercent = percent;
        }
        ConsoleLog("CORE", "загрузка: " + text);
    }

    void HideLoading()
    {
        std::lock_guard lock(g_mutex);
        if (!g_loading) return;
        g_loading = false;
        g_loadingHiddenAt = GetTickCount64();
    }

    bool LoadingVisible()
    {
        std::lock_guard lock(g_mutex);
        return g_loading;
    }

    void SetLoadingStyle(std::vector<std::string> tips, uint32_t accent)
    {
        std::lock_guard lock(g_mutex);
        g_tips = std::move(tips);
        g_loadingAccent = accent;
    }

    // --- окно игры ---------------------------------------------------------------------------------
    void SetWindowTitle(const std::string& title)
    {
        HWND hwnd = nullptr;
        {
            std::lock_guard lock(g_mutex);
            const auto t = FromUtf8(title.empty() ? std::string("FloV Multiplayer") : title);
            if (t == g_title) return;
            g_title = t;
            hwnd = g_hwnd;
        }
        // Заголовок меняет поток окна (наш WndProc): SetWindowText из игрового
        // потока ждал бы поток окна и мог подвесить кадр.
        if (hwnd) PostMessageW(hwnd, kMsgApplyTitle, 0, 0);
    }

    // --- ввод -------------------------------------------------------------------------------------------
    void SetHotkeys(std::vector<int> vks)
    {
        std::lock_guard lock(g_mutex);
        if (std::find(vks.begin(), vks.end(), (int)KeyConnect) == vks.end()) vks.push_back(KeyConnect);
        g_watchKeys = std::move(vks);
    }

    std::vector<int> TakeHotkeys()
    {
        std::lock_guard lock(g_mutex);
        return std::exchange(g_hotkeys, {});
    }

    void OpenConnectDialog(const std::string& host, const std::string& name)
    {
        std::lock_guard lock(g_mutex);
        if (g_chatOpen) CloseChat();
        if (g_consoleOpen) CloseConsole();
        g_connectOpen = true;
        g_connectHost = FromUtf8(host);
        g_connectName = FromUtf8(name);
        g_connectField = host.empty() ? 0 : 1;
        g_caret = (g_connectField == 0 ? g_connectHost : g_connectName).size();
    }

    bool TakeConnectRequest(std::string& host, std::string& name)
    {
        std::lock_guard lock(g_mutex);
        if (!g_connectRequested) return false;
        g_connectRequested = false;
        host = ToUtf8(g_connectHost);
        name = ToUtf8(g_connectName);
        return true;
    }

    bool InputActive()
    {
        std::lock_guard lock(g_mutex);
        return g_chatOpen || g_connectOpen || g_consoleOpen;
    }

    uint32_t MsSinceInputClosed()
    {
        std::lock_guard lock(g_mutex);
        return (uint32_t)std::min<ULONGLONG>(GetTickCount64() - g_inputClosedAt, 0xFFFFFFFF);
    }

    void SetInputKeys(int chatVk, int consoleVk)
    {
        std::lock_guard lock(g_mutex);
        g_chatKey = chatVk;
        g_consoleKey = consoleVk;
    }
}
