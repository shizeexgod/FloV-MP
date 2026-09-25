#include <windows.h>
#include <d3d11.h>

#include <algorithm>
#include <atomic>
#include <condition_variable>
#include <deque>
#include <map>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>

#include "imgui.h"

#include "browser.h"
#include "browser_ipc.h"
#include "common.h"

namespace ipc = flov::browser_ipc;

namespace flov::browser
{
    namespace
    {
        struct Item
        {
            std::string url;
            bool visible = true;
            bool inputEnabled = true;
            int order = 0;
            int frameRate = 60;
            ULONGLONG triggerWindow = 0;
            int triggerCount = 0;
            bool triggerWarned = false;
            // Кадры
            std::string shmName;
            HANDLE map = nullptr;
            uint8_t* view = nullptr;
            size_t viewBytes = 0;
            int w = 0, h = 0;
            LONG64 lastSeq = -1;
            // Текстура (только поток Present)
            ID3D11Texture2D* tex = nullptr;
            ID3D11ShaderResourceView* srv = nullptr;
            int texW = 0, texH = 0;
            bool texFresh = false;
            // Последний полностью подтверждённый кадр нужен и для безопасной
            // загрузки в GPU, и для alpha hit-test без гонки с CEF writer.
            std::vector<uint8_t> stableFrame;
            std::vector<uint8_t> scratch;
        };

        std::mutex g_mutex;
        std::map<int, Item> g_items;          // порядок id — порядок наложения
        int g_nextId = 0;
        ULONGLONG g_triggerWindow = 0;
        int g_triggerCount = 0;
        std::wstring g_hostExe;
        std::wstring g_root;
        std::deque<Event> g_events;

        // Хост
        HANDLE g_job = nullptr;
        HANDLE g_process = nullptr;
        /// Одно подключение к хосту. Потоки чтения и записи держат его сами:
        /// после перезапуска хоста старые потоки не трогают новую очередь.
        struct Conn
        {
            HANDLE toHost = INVALID_HANDLE_VALUE;
            HANDLE fromHost = INVALID_HANDLE_VALUE;
            std::mutex m;
            std::condition_variable cv;
            std::deque<std::string> out;
            bool stop = false;
            ~Conn()
            {
                if (toHost != INVALID_HANDLE_VALUE) CloseHandle(toHost);
                if (fromHost != INVALID_HANDLE_VALUE) CloseHandle(fromHost);
            }
        };
        std::shared_ptr<Conn> g_conn;
        std::atomic<bool> g_hostUp{ false };
        bool g_hostReady = false;
        int g_restarts = 0;
        std::deque<std::string> g_backlog;    // до READY

        // Ввод
        std::atomic<bool> g_input{ false };
        int g_mouseTarget = 0, g_focus = 0;
        int g_lastX = -1, g_lastY = -1, g_lastButtons = 0;

        // Рисование
        int g_sentW = 0, g_sentH = 0;
        ID3D11DeviceContext* g_ctx = nullptr;
        ID3D11BlendState* g_premul = nullptr;

        std::string N(long long v) { return std::to_string(v); }
        void SendLocked(const std::vector<std::string>& fields);

        std::vector<int> OrderedIdsLocked()
        {
            std::vector<int> ids;
            ids.reserve(g_items.size());
            for (const auto& [id, item] : g_items) ids.push_back(id);
            std::stable_sort(ids.begin(), ids.end(), [](int a, int b) {
                const int ao = g_items.at(a).order, bo = g_items.at(b).order;
                return ao == bo ? a < b : ao < bo;
            });
            return ids;
        }

        void ReleaseInputLocked(int id)
        {
            if (g_mouseTarget == id)
            {
                SendLocked({ "LEAVE", N(id) });
                g_mouseTarget = 0;
            }
            if (g_focus == id)
            {
                SendLocked({ "FOCUS", N(id), "0" });
                g_focus = 0;
            }
            g_lastX = g_lastY = -1;
            g_lastButtons = 0;
        }

        std::string Utf8(const std::wstring& w)
        {
            if (w.empty()) return {};
            const int n = WideCharToMultiByte(CP_UTF8, 0, w.data(), (int)w.size(), nullptr, 0, nullptr, nullptr);
            std::string s(n, '\0');
            WideCharToMultiByte(CP_UTF8, 0, w.data(), (int)w.size(), s.data(), n, nullptr, nullptr);
            return s;
        }

        void Push(Event::Kind kind, int id, std::string a = {}, std::string b = {})
        {
            if (g_events.size() >= 512) g_events.pop_front();
            g_events.push_back({ kind, id, std::move(a), std::move(b) });
        }

        /// В хост (под g_mutex). До READY команды копятся.
        void SendLocked(const std::vector<std::string>& fields)
        {
            std::string line = ipc::Format(fields);
            if (!g_hostUp) return;
            if (!g_hostReady) { if (g_backlog.size() < 4096) g_backlog.push_back(std::move(line)); return; }
            auto conn = g_conn;
            if (!conn) return;
            std::lock_guard lock(conn->m);
            if (conn->out.size() > 8192) return;   // хост не читает — не копим бесконечно
            conn->out.push_back(std::move(line));
            conn->cv.notify_one();
        }

        void CloseFrame(Item& it)
        {
            if (it.view) UnmapViewOfFile(it.view);
            if (it.map) CloseHandle(it.map);
            it.view = nullptr;
            it.map = nullptr;
            it.viewBytes = 0;
            it.lastSeq = -1;
            it.texFresh = true;
            it.stableFrame.clear();
            it.scratch.clear();
        }

        std::wstring DefaultHostExe()
        {
            HMODULE self = nullptr;
            GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                               reinterpret_cast<LPCWSTR>(&DefaultHostExe), &self);
            wchar_t path[MAX_PATH] = {};
            GetModuleFileNameW(self, path, MAX_PATH);
            std::wstring dir = path;
            dir = dir.substr(0, dir.find_last_of(L"\\/"));
            return dir + L"\\FloVMP\\cef\\flovmp-cef.exe";
        }

        void OnHostLine(const std::shared_ptr<Conn>& conn, const std::vector<std::string>& p);

        void ReaderLoop(std::shared_ptr<Conn> conn)
        {
            HANDLE in = conn->fromHost;
            std::string buf;
            char chunk[16384];
            for (;;)
            {
                DWORD n = 0;
                if (!ReadFile(in, chunk, sizeof chunk, &n, nullptr) || n == 0) break;
                buf.append(chunk, n);
                size_t nl;
                while ((nl = buf.find('\n')) != std::string::npos)
                {
                    auto parts = ipc::Parse(buf.substr(0, nl));
                    buf.erase(0, nl + 1);
                    if (!parts.empty()) OnHostLine(conn, parts);
                }
                if (buf.size() > 8 * 1024 * 1024) break;
            }
            {
                std::lock_guard l(conn->m);
                conn->stop = true;
            }
            conn->cv.notify_all();
            std::lock_guard lock(g_mutex);
            if (g_hostUp && g_conn == conn)
            {
                g_conn.reset();
                if (g_process) { CloseHandle(g_process); g_process = nullptr; }
                g_hostUp = false;
                g_hostReady = false;
                Push(Event::Kind::HostLost, 0);
                for (auto& [id, it] : g_items) { it.shmName.clear(); CloseFrame(it); }
            }
        }

        void WriterLoop(std::shared_ptr<Conn> conn)
        {
            HANDLE out = conn->toHost;
            for (;;)
            {
                std::string line;
                {
                    std::unique_lock lock(conn->m);
                    conn->cv.wait(lock, [&] { return conn->stop || !conn->out.empty(); });
                    if (conn->stop) return;
                    line = std::move(conn->out.front());
                    conn->out.pop_front();
                }
                line += '\n';
                size_t offset = 0;
                while (offset < line.size())
                {
                    DWORD written = 0;
                    const DWORD left = (DWORD)std::min<size_t>(line.size() - offset, MAXDWORD);
                    if (!WriteFile(out, line.data() + offset, left, &written, nullptr) || written == 0) return;
                    offset += written;
                }
            }
        }

        /// Запустить хост (под g_mutex). Хост в job-объекте: закрылась игра —
        /// Windows сама закроет его вместе с процессами Chromium.
        bool StartHostLocked()
        {
            if (g_hostUp) return true;
            if (g_hostExe.empty()) g_hostExe = DefaultHostExe();
            if (GetFileAttributesW(g_hostExe.c_str()) == INVALID_FILE_ATTRIBUTES)
            {
                Log("браузеры: нет " + Utf8(g_hostExe) + " — mp.browsers недоступен");
                return false;
            }
            if (g_restarts >= 3) return false;
            ++g_restarts;

            const std::wstring base = L"\\\\.\\pipe\\flovmp-cef-" + std::to_wstring(GetCurrentProcessId()) + L"-" +
                                      std::to_wstring(GetTickCount64());
            // Две трубы, по одной на направление: чтение и запись по одной
            // синхронной трубе из разных потоков Windows выполняет по очереди.
            HANDLE toHost = CreateNamedPipeW((base + L"-c2h").c_str(), PIPE_ACCESS_OUTBOUND | FILE_FLAG_FIRST_PIPE_INSTANCE,
                                             PIPE_TYPE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS, 1, 1 << 20, 0, 0, nullptr);
            HANDLE fromHost = CreateNamedPipeW((base + L"-h2c").c_str(), PIPE_ACCESS_INBOUND | FILE_FLAG_FIRST_PIPE_INSTANCE,
                                               PIPE_TYPE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS, 1, 0, 1 << 20, 0, nullptr);
            if (toHost == INVALID_HANDLE_VALUE || fromHost == INVALID_HANDLE_VALUE)
            {
                if (toHost != INVALID_HANDLE_VALUE) CloseHandle(toHost);
                if (fromHost != INVALID_HANDLE_VALUE) CloseHandle(fromHost);
                Log("браузеры: не создана труба к хосту");
                return false;
            }

            if (!g_job)
            {
                g_job = CreateJobObjectW(nullptr, nullptr);
                JOBOBJECT_EXTENDED_LIMIT_INFORMATION info{};
                info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
                if (g_job) SetInformationJobObject(g_job, JobObjectExtendedLimitInformation, &info, sizeof info);
            }
            std::wstring cmd = L"\"" + g_hostExe + L"\" --flovmp-pipe=" + base + L" --flovmp-parent=" +
                               std::to_wstring(GetCurrentProcessId());
            const std::wstring dir = g_hostExe.substr(0, g_hostExe.find_last_of(L"\\/"));
            STARTUPINFOW si{ sizeof si };
            PROCESS_INFORMATION pi{};
            if (!CreateProcessW(g_hostExe.c_str(), cmd.data(), nullptr, nullptr, FALSE,
                                CREATE_SUSPENDED | CREATE_NO_WINDOW, nullptr, dir.c_str(), &si, &pi))
            {
                CloseHandle(toHost);
                CloseHandle(fromHost);
                Log("браузеры: хост не запустился, код " + N(GetLastError()));
                return false;
            }
            if (g_job) AssignProcessToJobObject(g_job, pi.hProcess);
            ResumeThread(pi.hThread);
            CloseHandle(pi.hThread);
            if (g_process) CloseHandle(g_process);
            g_process = pi.hProcess;
            auto conn = std::make_shared<Conn>();
            conn->toHost = toHost;
            conn->fromHost = fromHost;
            g_conn = conn;
            g_hostUp = true;
            g_hostReady = false;
            g_backlog.clear();
            // Подключение хоста ждём в своих потоках, не в игровом.
            std::thread([conn] {
                if (!ConnectNamedPipe(conn->fromHost, nullptr) && GetLastError() != ERROR_PIPE_CONNECTED) return;
                ReaderLoop(conn);
            }).detach();
            std::thread([conn] {
                if (!ConnectNamedPipe(conn->toHost, nullptr) && GetLastError() != ERROR_PIPE_CONNECTED) return;
                WriterLoop(conn);
            }).detach();
            Log("браузеры: хост запущен (" + N(pi.dwProcessId) + ")");
            return true;
        }

        void OnHostLine(const std::shared_ptr<Conn>& conn, const std::vector<std::string>& p)
        {
            auto at = [&](size_t i) -> std::string { return i < p.size() ? p[i] : std::string(); };
            const int id = atoi(at(1).c_str());
            std::lock_guard lock(g_mutex);
            if (conn != g_conn) return;   // строка от прежнего хоста
            const std::string& t = p[0];
            if (t == "READY")
            {
                g_hostReady = true;
                std::lock_guard out(conn->m);
                for (auto& l : g_backlog) conn->out.push_back(std::move(l));
                g_backlog.clear();
                conn->cv.notify_one();
                return;
            }
            auto it = g_items.find(id);
            if (it == g_items.end()) return;
            if (t == "FRAME")
            {
                CloseFrame(it->second);
                it->second.shmName = at(2);
                it->second.w = atoi(at(3).c_str());
                it->second.h = atoi(at(4).c_str());
            }
            else if (t == "DOM") Push(Event::Kind::DomReady, id, at(2));
            else if (t == "FAIL") Push(Event::Kind::LoadFailed, id, at(2), at(3));
            else if (t == "TRIG")
            {
                // Ошибочный requestAnimationFrame/цикл в странице не должен
                // забить игровой тик тысячами CEF → JS callbacks.
                const ULONGLONG now = GetTickCount64();
                if (!it->second.triggerWindow || now - it->second.triggerWindow >= 1000)
                {
                    it->second.triggerWindow = now;
                    it->second.triggerCount = 0;
                    it->second.triggerWarned = false;
                }
                if (!g_triggerWindow || now - g_triggerWindow >= 1000)
                {
                    g_triggerWindow = now;
                    g_triggerCount = 0;
                }
                if (it->second.triggerCount >= 240 || g_triggerCount >= 1000)
                {
                    if (!it->second.triggerWarned)
                    {
                        it->second.triggerWarned = true;
                        Push(Event::Kind::Console, id, "1", "mp.trigger: лимит 240 событий/с на browser; лишние отброшены");
                    }
                    return;
                }
                ++it->second.triggerCount;
                ++g_triggerCount;
                Push(Event::Kind::Trigger, id, at(2), at(3));
            }
            else if (t == "LOG") Push(Event::Kind::Console, id, at(2), at(3));
        }

        /// Открыть память кадров (под g_mutex).
        bool OpenFrame(Item& it)
        {
            if (it.view) return true;
            if (it.shmName.empty() || it.w <= 0 || it.h <= 0) return false;
            it.map = OpenFileMappingA(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, it.shmName.c_str());
            if (!it.map) return false;
            it.viewBytes = ipc::FrameBytes(it.w, it.h);
            it.view = static_cast<uint8_t*>(MapViewOfFile(it.map, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, it.viewBytes));
            if (!it.view) { CloseFrame(it); return false; }
            const auto* hd = reinterpret_cast<const ipc::FrameHeader*>(it.view);
            if (hd->magic != ipc::kMagic || (int)hd->width != it.w || (int)hd->height != it.h) { CloseFrame(it); return false; }
            return true;
        }

        /// Прозрачна ли точка страницы (под g_mutex) — через прозрачное место
        /// клик уходит браузеру ниже.
        bool OpaqueAt(Item& it, int x, int y)
        {
            if (x < 0 || y < 0 || x >= it.w || y >= it.h ||
                it.stableFrame.size() != (size_t)it.w * it.h * 4) return false;
            return it.stableFrame[((size_t)y * it.w + x) * 4 + 3] > 8;
        }

        void PremultipliedBlend(const ImDrawList*, const ImDrawCmd*)
        {
            // CEF отдаёт цвет, уже умноженный на альфу: обычное смешивание
            // ImGui затемнило бы края и полупрозрачные места.
            if (!g_ctx || !g_premul) return;
            const float factor[4] = { 0, 0, 0, 0 };
            g_ctx->OMSetBlendState(g_premul, factor, 0xFFFFFFFF);
        }
    }

    void SetHostExe(const std::wstring& path) { std::lock_guard lock(g_mutex); g_hostExe = path; }

    bool Available()
    {
        std::lock_guard lock(g_mutex);
        if (g_hostExe.empty()) g_hostExe = DefaultHostExe();
        return GetFileAttributesW(g_hostExe.c_str()) != INVALID_FILE_ATTRIBUTES;
    }

    int Create(const std::string& url)
    {
        std::lock_guard lock(g_mutex);
        if (g_items.size() >= 32) { Log("браузеры: больше 32 одновременно не создаётся"); return 0; }
        if (!StartHostLocked()) return 0;
        const int id = ++g_nextId;
        g_items[id].url = url;
        g_items[id].order = id;
        if (!g_root.empty()) SendLocked({ "ROOT", Utf8(g_root) });
        if (g_sentW) SendLocked({ "SIZE", N(g_sentW), N(g_sentH) });
        SendLocked({ "NEW", N(id), url });
        return id;
    }

    void Destroy(int id)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_items.find(id);
        if (it == g_items.end()) return;
        SendLocked({ "DEL", N(id) });
        CloseFrame(it->second);
        // Текстуру освободит поток Present: пометить нечем, поэтому
        // освобождаем здесь — Present берёт тот же мьютекс.
        if (it->second.srv) it->second.srv->Release();
        if (it->second.tex) it->second.tex->Release();
        g_items.erase(it);
        if (g_mouseTarget == id) g_mouseTarget = 0;
        if (g_focus == id) g_focus = 0;
    }

    void DestroyAll()
    {
        std::vector<int> ids;
        {
            std::lock_guard lock(g_mutex);
            for (auto& [id, it] : g_items) ids.push_back(id);
            g_events.clear();
        }
        for (int id : ids) Destroy(id);
    }

    void Shutdown()
    {
        DestroyAll();
        std::lock_guard lock(g_mutex);
        if (auto conn = g_conn)
        {
            std::lock_guard l(conn->m);
            conn->stop = true;
            conn->cv.notify_all();
        }
        g_conn.reset();
        if (g_process) { TerminateProcess(g_process, 0); CloseHandle(g_process); g_process = nullptr; }
        g_hostUp = false;
        g_hostReady = false;
        g_backlog.clear();
        g_restarts = 0;
    }

    void SetUrl(int id, const std::string& url)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_items.find(id);
        if (it == g_items.end()) return;
        it->second.url = url;
        SendLocked({ "URL", N(id), url });
    }

    void Execute(int id, const std::string& code)
    {
        std::lock_guard lock(g_mutex);
        if (g_items.count(id)) SendLocked({ "EXEC", N(id), code });
    }

    void Call(int id, const std::string& name, const std::string& argsJson)
    {
        std::lock_guard lock(g_mutex);
        if (g_items.count(id)) SendLocked({ "CALL", N(id), name, argsJson });
    }

    void Show(int id, bool visible)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_items.find(id);
        if (it == g_items.end() || it->second.visible == visible) return;
        it->second.visible = visible;
        if (!visible) ReleaseInputLocked(id);
        SendLocked({ "SHOW", N(id), visible ? "1" : "0" });
    }

    void SetInputEnabled(int id, bool enabled)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_items.find(id);
        if (it == g_items.end() || it->second.inputEnabled == enabled) return;
        it->second.inputEnabled = enabled;
        if (!enabled) ReleaseInputLocked(id);
        else g_lastX = g_lastY = -1;
    }

    void SetOrder(int id, int order)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_items.find(id);
        if (it == g_items.end()) return;
        it->second.order = std::clamp(order, -1000000, 1000000);
        g_lastX = g_lastY = -1;
    }

    void SetFrameRate(int id, int frameRate)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_items.find(id);
        if (it == g_items.end()) return;
        const int rate = std::clamp(frameRate, 1, 60);
        if (it->second.frameRate == rate) return;
        it->second.frameRate = rate;
        SendLocked({ "RATE", N(id), N(rate) });
    }

    void Reload(int id, bool ignoreCache)
    {
        std::lock_guard lock(g_mutex);
        if (g_items.count(id)) SendLocked({ "RELOAD", N(id), ignoreCache ? "1" : "0" });
    }

    void SetPackageRoot(const std::wstring& dir)
    {
        std::lock_guard lock(g_mutex);
        g_root = dir;
        if (g_hostUp) SendLocked({ "ROOT", Utf8(dir) });
    }

    std::vector<Event> TakeEvents()
    {
        std::lock_guard lock(g_mutex);
        std::vector<Event> out(g_events.begin(), g_events.end());
        g_events.clear();
        // Хост упал: поднять заново и вернуть страницы на место.
        if (!g_hostUp && !g_items.empty() && g_restarts < 3 && StartHostLocked())
        {
            if (!g_root.empty()) SendLocked({ "ROOT", Utf8(g_root) });
            if (g_sentW) SendLocked({ "SIZE", N(g_sentW), N(g_sentH) });
            for (auto& [id, it] : g_items)
            {
                SendLocked({ "NEW", N(id), it.url });
                if (!it.visible) SendLocked({ "SHOW", N(id), "0" });
                if (it.frameRate != 60) SendLocked({ "RATE", N(id), N(it.frameRate) });
            }
        }
        return out;
    }

    bool AnyVisible()
    {
        std::lock_guard lock(g_mutex);
        for (auto& [id, it] : g_items) if (it.visible) return true;
        return false;
    }

    void SetInput(bool enabled)
    {
        const bool was = g_input.exchange(enabled);
        if (was && !enabled)
        {
            std::lock_guard lock(g_mutex);
            if (g_mouseTarget) SendLocked({ "LEAVE", N(g_mouseTarget) });
            if (g_focus) SendLocked({ "FOCUS", N(g_focus), "0" });
            g_mouseTarget = 0;
            g_lastButtons = 0;
            g_lastX = g_lastY = -1;
        }
    }

    void Mouse(float nx, float ny, int buttons, int wheel)
    {
        if (!g_input) return;
        std::lock_guard lock(g_mutex);
        if (!g_sentW || g_items.empty()) return;
        const int x = std::clamp((int)(nx * g_sentW), 0, g_sentW - 1);
        const int y = std::clamp((int)(ny * g_sentH), 0, g_sentH - 1);
        if (x == g_lastX && y == g_lastY && buttons == g_lastButtons && !wheel) return;

        // Цель: пока кнопка зажата — тот же браузер (перетаскивание); иначе —
        // верхний видимый, у которого под курсором не прозрачно.
        int target = g_mouseTarget;
        if (!(g_lastButtons && target && g_items.count(target)))
        {
            target = 0;
            const auto ordered = OrderedIdsLocked();
            for (auto it = ordered.rbegin(); it != ordered.rend(); ++it)
            {
                auto& item = g_items.at(*it);
                if (item.visible && item.inputEnabled && OpaqueAt(item, x, y)) { target = *it; break; }
            }
            // До первого кадра alpha ещё неизвестна. Разрешаем ранний ввод
            // только такому браузеру; стабильный полностью прозрачный кадр
            // больше не перехватывает мышь у игры.
            if (!target)
                for (auto it = ordered.rbegin(); it != ordered.rend(); ++it)
                {
                    auto& item = g_items.at(*it);
                    if (item.visible && item.inputEnabled && item.lastSeq < 0) { target = *it; break; }
                }
        }
        if (target != g_mouseTarget && g_mouseTarget && g_items.count(g_mouseTarget))
            SendLocked({ "LEAVE", N(g_mouseTarget) });
        g_mouseTarget = target;
        if (target)
        {
            // Клик — фокус клавиатуры туда же.
            if ((buttons & ~g_lastButtons) && g_focus != target)
            {
                if (g_focus && g_items.count(g_focus)) SendLocked({ "FOCUS", N(g_focus), "0" });
                g_focus = target;
                SendLocked({ "FOCUS", N(target), "1" });
            }
            SendLocked({ "MOUSE", N(target), N(x), N(y), N(buttons), N(wheel) });
        }
        g_lastX = x;
        g_lastY = y;
        g_lastButtons = buttons;
    }

    bool Key(unsigned msg, uintptr_t wp, intptr_t lp)
    {
        if (!g_input) return false;
        if (msg != WM_KEYDOWN && msg != WM_KEYUP && msg != WM_CHAR && msg != WM_SYSKEYDOWN && msg != WM_SYSKEYUP &&
            msg != WM_SYSCHAR)
            return false;
        std::lock_guard lock(g_mutex);
        int target = g_focus && g_items.count(g_focus) && g_items[g_focus].visible && g_items[g_focus].inputEnabled ? g_focus : 0;
        if (!target)
        {
            const auto ordered = OrderedIdsLocked();
            for (auto it = ordered.rbegin(); it != ordered.rend(); ++it)
            {
                const auto& item = g_items.at(*it);
                if (item.visible && item.inputEnabled) { target = *it; break; }
            }
        }
        if (!target) return false;
        if (g_focus != target) { g_focus = target; SendLocked({ "FOCUS", N(target), "1" }); }
        int mods = 0;
        if (GetKeyState(VK_SHIFT) & 0x8000) mods |= 1 << 1;     // EVENTFLAG_SHIFT_DOWN
        if (GetKeyState(VK_CONTROL) & 0x8000) mods |= 1 << 2;   // EVENTFLAG_CONTROL_DOWN
        if (GetKeyState(VK_MENU) & 0x8000) mods |= 1 << 3;      // EVENTFLAG_ALT_DOWN
        SendLocked({ "KEY", N(target), N(msg), N((long long)wp), N((long long)lp), N(mods) });
        // Shift, Ctrl и Alt всё равно отдаём системе: ими меняют раскладку.
        if ((msg == WM_KEYDOWN || msg == WM_KEYUP || msg == WM_SYSKEYDOWN || msg == WM_SYSKEYUP) &&
            (wp == VK_SHIFT || wp == VK_CONTROL || wp == VK_MENU || wp == VK_LSHIFT || wp == VK_RSHIFT ||
             wp == VK_LCONTROL || wp == VK_RCONTROL || wp == VK_LMENU || wp == VK_RMENU))
            return false;
        return true;
    }

    void Render(void* devicePtr, void* contextPtr, void* drawList, float width, float height)
    {
        auto* device = static_cast<ID3D11Device*>(devicePtr);
        auto* ctx = static_cast<ID3D11DeviceContext*>(contextPtr);
        auto* dl = static_cast<ImDrawList*>(drawList);
        std::lock_guard lock(g_mutex);
        const int w = (int)width, h = (int)height;
        if (w > 0 && h > 0 && (w != g_sentW || h != g_sentH))
        {
            g_sentW = w;
            g_sentH = h;
            SendLocked({ "SIZE", N(w), N(h) });
        }
        if (g_items.empty() || !device || !ctx) return;
        g_ctx = ctx;
        if (!g_premul)
        {
            D3D11_BLEND_DESC bd{};
            auto& rt = bd.RenderTarget[0];
            rt.BlendEnable = TRUE;
            rt.SrcBlend = D3D11_BLEND_ONE;
            rt.DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
            rt.BlendOp = D3D11_BLEND_OP_ADD;
            rt.SrcBlendAlpha = D3D11_BLEND_ONE;
            rt.DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
            rt.BlendOpAlpha = D3D11_BLEND_OP_ADD;
            rt.RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
            device->CreateBlendState(&bd, &g_premul);
        }

        bool any = false;
        for (int id : OrderedIdsLocked())
        {
            auto& it = g_items.at(id);
            if (!it.visible) continue;
            if (!OpenFrame(it)) continue;
            auto* hd = reinterpret_cast<ipc::FrameHeader*>(it.view);
            if (!it.tex || it.texW != it.w || it.texH != it.h)
            {
                if (it.srv) it.srv->Release();
                if (it.tex) it.tex->Release();
                it.srv = nullptr;
                it.tex = nullptr;
                D3D11_TEXTURE2D_DESC td{};
                td.Width = (UINT)it.w;
                td.Height = (UINT)it.h;
                td.MipLevels = 1;
                td.ArraySize = 1;
                td.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
                td.SampleDesc.Count = 1;
                td.Usage = D3D11_USAGE_DEFAULT;
                td.BindFlags = D3D11_BIND_SHADER_RESOURCE;
                if (FAILED(device->CreateTexture2D(&td, nullptr, &it.tex))) continue;
                if (FAILED(device->CreateShaderResourceView(it.tex, nullptr, &it.srv))) { it.tex->Release(); it.tex = nullptr; continue; }
                it.texW = it.w;
                it.texH = it.h;
                it.texFresh = true;
                it.lastSeq = -1;
                it.stableFrame.clear();
                it.scratch.clear();
            }
            const LONG64 seq = ipc::LoadCounter(&hd->seq);
            if (!(seq & 1) && seq != it.lastSeq && seq > 0)
            {
                // Новая текстура — целиком, дальше — только изменившееся.
                int x = 0, y = 0, rw = it.w, rh = it.h;
                if (!it.texFresh && hd->dirtyW > 0 && hd->dirtyH > 0)
                {
                    x = std::clamp(hd->dirtyX, 0, it.w);
                    y = std::clamp(hd->dirtyY, 0, it.h);
                    rw = std::min(hd->dirtyW, it.w - x);
                    rh = std::min(hd->dirtyH, it.h - y);
                }
                if (rw > 0 && rh > 0)
                {
                    const size_t rowBytes = (size_t)rw * 4;
                    it.scratch.resize(rowBytes * rh);
                    const uint8_t* shared = it.view + sizeof(ipc::FrameHeader);
                    for (int row = 0; row < rh; ++row)
                        memcpy(it.scratch.data() + (size_t)row * rowBytes,
                               shared + ((size_t)(y + row) * it.w + x) * 4, rowBytes);

                    // Пока копировали, CEF мог начать следующий кадр. Такой
                    // снимок нельзя ни показывать, ни использовать для hit-test.
                    if (ipc::LoadCounter(&hd->seq) == seq)
                    {
                        const size_t fullBytes = (size_t)it.w * it.h * 4;
                        if (it.stableFrame.size() != fullBytes) it.stableFrame.assign(fullBytes, 0);
                        for (int row = 0; row < rh; ++row)
                            memcpy(it.stableFrame.data() + ((size_t)(y + row) * it.w + x) * 4,
                                   it.scratch.data() + (size_t)row * rowBytes, rowBytes);

                        const D3D11_BOX box{ (UINT)x, (UINT)y, 0, (UINT)(x + rw), (UINT)(y + rh), 1 };
                        ctx->UpdateSubresource(it.tex, 0, &box, it.scratch.data(), (UINT)rowBytes, 0);
                        it.lastSeq = seq;
                        it.texFresh = false;
                        ipc::StoreCounter(&hd->ack, seq);
                    }
                }
            }
            if (!it.visible || it.lastSeq < 0 || !dl) continue;
            if (!any) { dl->AddCallback(PremultipliedBlend, nullptr); any = true; }
            dl->AddImage((ImTextureID)it.srv, ImVec2(0, 0), ImVec2(width, height));
        }
        if (any) dl->AddCallback(ImDrawCallback_ResetRenderState, nullptr);
    }

    void* TextureOf(int id)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_items.find(id);
        return it == g_items.end() || it->second.lastSeq < 0 ? nullptr : it->second.tex;
    }
}
