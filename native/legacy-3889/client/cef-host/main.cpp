// flovmp-cef.exe — хост браузеров клиента FloV:MP (пункт 26b).
//
// Отдельный процесс рядом с GTA5.exe: Chromium (CEF) рисует страницы без окна
// (off-screen), кадры уходят клиенту через разделяемую память, команды и
// события — через трубу. Протокол — src/browser_ipc.h.
//
// Этот же файл — подпроцессы Chromium (рендерер, GPU, сеть): CEF запускает
// его с --type=…, и CefExecuteProcess сразу уходит в свой цикл. В рендерере
// работает мост страницы: window.mp.trigger / mp.events, как в RAGE:MP.
//
//   flovmp-cef.exe --flovmp-pipe=\\.\pipe\… --flovmp-parent=<pid GTA5.exe>

#include <windows.h>

#include <atomic>
#include <filesystem>
#include <functional>
#include <map>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

#include "include/cef_app.h"
#include "include/cef_browser.h"
#include "include/cef_client.h"
#include "include/cef_parser.h"
#include "include/cef_request_context.h"
#include "include/cef_scheme.h"
#include "include/cef_task.h"
#include "include/cef_v8.h"
#include "include/wrapper/cef_stream_resource_handler.h"

#include "browser_ipc.h"

namespace ipc = flov::browser_ipc;
namespace fs = std::filesystem;

namespace
{
    // --- связь с клиентом ------------------------------------------------------------

    HANDLE g_pipe = INVALID_HANDLE_VALUE;   // события клиенту
    HANDLE g_in = INVALID_HANDLE_VALUE;     // команды от клиента
    std::mutex g_sendMutex;
    std::atomic<bool> g_quitting{ false };

    void Send(const std::vector<std::string>& fields)
    {
        if (g_pipe == INVALID_HANDLE_VALUE) return;
        const std::string line = ipc::Format(fields) + "\n";
        std::lock_guard lock(g_sendMutex);
        DWORD written = 0;
        WriteFile(g_pipe, line.data(), (DWORD)line.size(), &written, nullptr);
    }

    std::string N(long long v) { return std::to_string(v); }
    int I(const std::string& s) { return atoi(s.c_str()); }

    class Task : public CefTask
    {
    public:
        explicit Task(std::function<void()> f) : _f(std::move(f)) {}
        void Execute() override { _f(); }
    private:
        std::function<void()> _f;
        IMPLEMENT_REFCOUNTING(Task);
    };

    void PostUi(std::function<void()> f) { CefPostTask(TID_UI, new Task(std::move(f))); }
    void PostUiDelayed(std::function<void()> f, int64_t ms) { CefPostDelayedTask(TID_UI, new Task(std::move(f)), ms); }

    // --- общее состояние (только поток UI CEF) ---------------------------------------

    int g_width = 1920, g_height = 1080;
    std::mutex g_rootMutex;
    std::wstring g_root;   // папка client_packages у игрока — package://

    // --- кадр в разделяемой памяти ------------------------------------------------

    class Frame
    {
    public:
        ~Frame() { Close(); }

        /// true — создана новая память (клиенту нужно сообщить имя).
        bool Ensure(int id, int w, int h)
        {
            if (_view && w == _w && h == _h) return false;
            Close();
            ++_gen;
            _name = "Local\\flovmp-cef-" + N(GetCurrentProcessId()) + "-" + N(id) + "-" + N(_gen);
            const size_t bytes = ipc::FrameBytes(w, h);
            _map = CreateFileMappingA(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE,
                                      (DWORD)(bytes >> 32), (DWORD)(bytes & 0xFFFFFFFF), _name.c_str());
            if (!_map) return false;
            _view = static_cast<uint8_t*>(MapViewOfFile(_map, FILE_MAP_ALL_ACCESS, 0, 0, bytes));
            if (!_view) { Close(); return false; }
            auto* hd = Header();
            ZeroMemory(hd, sizeof *hd);
            hd->magic = ipc::kMagic;
            hd->width = (uint32_t)w;
            hd->height = (uint32_t)h;
            hd->stride = (uint32_t)w * 4;
            _w = w;
            _h = h;
            return true;
        }

        const std::string& Name() const { return _name; }
        bool Ready() const { return _view != nullptr; }

        /// Записать области из полного буфера кадра (w*h BGRA).
        void Write(const uint8_t* src, const std::vector<CefRect>& rects)
        {
            if (!_view || rects.empty()) return;
            auto* hd = Header();
            uint8_t* px = _view + sizeof(ipc::FrameHeader);
            const LONG64 writingSeq = InterlockedIncrement64(&hd->seq);   // нечётный — пишем
            // Объединение с тем, что клиент ещё не забрал.
            int x0, y0, x1, y1;
            if (ipc::LoadCounter(&hd->ack) == writingSeq - 1 || hd->dirtyW == 0) { x0 = INT_MAX; y0 = INT_MAX; x1 = 0; y1 = 0; }
            else { x0 = hd->dirtyX; y0 = hd->dirtyY; x1 = hd->dirtyX + hd->dirtyW; y1 = hd->dirtyY + hd->dirtyH; }
            for (const auto& r : rects)
            {
                const int rx = std::max(0, r.x), ry = std::max(0, r.y);
                const int rw = std::min(r.x + r.width, _w) - rx, rh = std::min(r.y + r.height, _h) - ry;
                if (rw <= 0 || rh <= 0) continue;
                for (int y = ry; y < ry + rh; ++y)
                    memcpy(px + ((size_t)y * _w + rx) * 4, src + ((size_t)y * _w + rx) * 4, (size_t)rw * 4);
                x0 = std::min(x0, rx); y0 = std::min(y0, ry);
                x1 = std::max(x1, rx + rw); y1 = std::max(y1, ry + rh);
            }
            if (x1 > x0 && y1 > y0) { hd->dirtyX = x0; hd->dirtyY = y0; hd->dirtyW = x1 - x0; hd->dirtyH = y1 - y0; }
            InterlockedIncrement64(&hd->seq);   // чётный — готово
        }

    private:
        ipc::FrameHeader* Header() { return reinterpret_cast<ipc::FrameHeader*>(_view); }
        void Close()
        {
            if (_view) UnmapViewOfFile(_view);
            if (_map) CloseHandle(_map);
            _view = nullptr;
            _map = nullptr;
        }

        HANDLE _map = nullptr;
        uint8_t* _view = nullptr;
        int _w = 0, _h = 0, _gen = 0;
        std::string _name;
    };

    // --- браузер -------------------------------------------------------------------------

    class Browser;
    std::map<int, CefRefPtr<Browser>> g_browsers;

    class Browser : public CefClient, public CefRenderHandler, public CefLoadHandler,
                    public CefLifeSpanHandler, public CefDisplayHandler, public CefRequestHandler
    {
    public:
        explicit Browser(int id) : _id(id) {}

        CefRefPtr<CefRenderHandler> GetRenderHandler() override { return this; }
        CefRefPtr<CefLoadHandler> GetLoadHandler() override { return this; }
        CefRefPtr<CefLifeSpanHandler> GetLifeSpanHandler() override { return this; }
        CefRefPtr<CefDisplayHandler> GetDisplayHandler() override { return this; }
        CefRefPtr<CefRequestHandler> GetRequestHandler() override { return this; }

        CefRefPtr<CefBrowser> Get() const { return _browser; }
        int Id() const { return _id; }

        void Close()
        {
            _closing = true;
            if (_browser) _browser->GetHost()->CloseBrowser(true);
        }

        void SetVisible(bool visible)
        {
            _visible = visible;
            if (_browser) _browser->GetHost()->WasHidden(!visible);
        }

        void SetFrameRate(int rate)
        {
            _frameRate = std::clamp(rate, 1, 60);
            if (_browser) _browser->GetHost()->SetWindowlessFrameRate(_frameRate);
        }

        void Navigate(const std::string& url)
        {
            if (_browser) _browser->GetMainFrame()->LoadURL(url);
            else _pendingUrl = url;
        }

        // --- рисование ---
        void GetViewRect(CefRefPtr<CefBrowser>, CefRect& rect) override { rect = CefRect(0, 0, g_width, g_height); }

        bool GetScreenInfo(CefRefPtr<CefBrowser>, CefScreenInfo& info) override
        {
            info.device_scale_factor = 1.f;
            info.rect = info.available_rect = CefRect(0, 0, g_width, g_height);
            return true;
        }

        void OnPopupShow(CefRefPtr<CefBrowser> browser, bool show) override
        {
            _popupShown = show;
            if (!show) { _popup.clear(); browser->GetHost()->Invalidate(PET_VIEW); }
        }

        void OnPopupSize(CefRefPtr<CefBrowser>, const CefRect& rect) override { _popupRect = rect; }

        void OnPaint(CefRefPtr<CefBrowser>, PaintElementType type, const RectList& dirty,
                     const void* buffer, int w, int h) override
        {
            if (w <= 0 || h <= 0 || w > ipc::kMaxSide || h > ipc::kMaxSide) return;
            std::vector<CefRect> rects;
            if (type == PET_VIEW)
            {
                if (_viewW != w || _viewH != h)
                {
                    _view.assign((size_t)w * h * 4, 0);
                    _viewW = w;
                    _viewH = h;
                    rects.emplace_back(0, 0, w, h);
                }
                const auto* src = static_cast<const uint8_t*>(buffer);
                for (const auto& r : dirty)
                {
                    for (int y = std::max(0, r.y); y < std::min(h, r.y + r.height); ++y)
                    {
                        const int x = std::max(0, r.x), n = std::min(w, r.x + r.width) - x;
                        if (n > 0) memcpy(&_view[((size_t)y * w + x) * 4], src + ((size_t)y * w + x) * 4, (size_t)n * 4);
                    }
                    rects.push_back(r);
                }
                if (_popupShown && !_popup.empty()) rects.push_back(_popupRect);
            }
            else
            {
                if (_view.empty()) return;
                _popup.assign(static_cast<const uint8_t*>(buffer), static_cast<const uint8_t*>(buffer) + (size_t)w * h * 4);
                _popupW = w;
                _popupH = h;
                rects.push_back(_popupRect);
            }
            Publish(rects);
        }

        bool OnCursorChange(CefRefPtr<CefBrowser>, CefCursorHandle, cef_cursor_type_t type, const CefCursorInfo&) override
        {
            Send({ "CURSOR", N(_id), N((int)type) });
            return true;
        }

        // --- жизнь ---
        void OnAfterCreated(CefRefPtr<CefBrowser> browser) override
        {
            _browser = browser;
            auto host = browser->GetHost();
            host->SetWindowlessFrameRate(_frameRate);
            host->WasHidden(!_visible);
            if (_closing) { host->CloseBrowser(true); return; }
            if (!_pendingUrl.empty())
            {
                browser->GetMainFrame()->LoadURL(_pendingUrl);
                _pendingUrl.clear();
            }
        }

        void OnBeforeClose(CefRefPtr<CefBrowser>) override
        {
            _browser = nullptr;
            g_browsers.erase(_id);
        }

        bool OnBeforePopup(CefRefPtr<CefBrowser>, CefRefPtr<CefFrame>, int, const CefString&, const CefString&,
                           CefLifeSpanHandler::WindowOpenDisposition, bool, const CefPopupFeatures&, CefWindowInfo&, CefRefPtr<CefClient>&,
                           CefBrowserSettings&, CefRefPtr<CefDictionaryValue>&, bool*) override
        {
            return true;   // новых окон нет: интерфейс сервера живёт в своих браузерах
        }

        // --- загрузка ---
        void OnLoadEnd(CefRefPtr<CefBrowser>, CefRefPtr<CefFrame> frame, int) override
        {
            if (frame->IsMain()) Send({ "DOM", N(_id), frame->GetURL().ToString() });
        }

        void OnLoadError(CefRefPtr<CefBrowser>, CefRefPtr<CefFrame> frame, ErrorCode code, const CefString&,
                         const CefString& url) override
        {
            if (frame->IsMain() && code != ERR_ABORTED) Send({ "FAIL", N(_id), N((int)code), url.ToString() });
        }

        bool OnConsoleMessage(CefRefPtr<CefBrowser>, cef_log_severity_t level, const CefString& message,
                              const CefString& source, int line) override
        {
            const int lv = level >= LOGSEVERITY_ERROR ? 2 : level >= LOGSEVERITY_WARNING ? 1 : 0;
            std::string text = message.ToString();
            if (lv > 0 && !source.empty()) text += "  (" + source.ToString() + ":" + N(line) + ")";
            Send({ "LOG", N(_id), N(lv), text.substr(0, 2000) });
            return true;
        }

        // --- куда можно ходить ---
        bool OnBeforeBrowse(CefRefPtr<CefBrowser>, CefRefPtr<CefFrame>, CefRefPtr<CefRequest> request, bool, bool) override
        {
            // Файлы игрока и служебные страницы Chromium странице сервера не нужны.
            const std::string url = request->GetURL().ToString();
            for (const char* p : { "http://", "https://", "package://", "data:", "about:blank" })
                if (url.rfind(p, 0) == 0) return false;
            Send({ "LOG", N(_id), "1", "переход запрещён: " + url.substr(0, 200) });
            return true;
        }

        // --- мост страницы ---
        bool OnProcessMessageReceived(CefRefPtr<CefBrowser>, CefRefPtr<CefFrame>, CefProcessId,
                                      CefRefPtr<CefProcessMessage> message) override
        {
            if (message->GetName() != "flov-trigger") return false;
            auto args = message->GetArgumentList();
            const std::string name = args->GetString(0).ToString();
            const std::string json = args->GetString(1).ToString();
            if (name.empty() || name.size() > 128 || json.size() > 64 * 1024) return true;
            Send({ "TRIG", N(_id), name, json });
            return true;
        }

        // --- ввод ---
        void Mouse(int x, int y, int buttons, int wheel)
        {
            if (!_browser) return;
            auto host = _browser->GetHost();
            CefMouseEvent e;
            e.x = x;
            e.y = y;
            e.modifiers = Mods(buttons);
            host->SendMouseMoveEvent(e, false);
            static const struct { int bit; cef_mouse_button_type_t type; } kButtons[] = {
                { 1, MBT_LEFT }, { 2, MBT_RIGHT }, { 4, MBT_MIDDLE } };
            for (const auto& b : kButtons)
            {
                const bool now = (buttons & b.bit) != 0, was = (_buttons & b.bit) != 0;
                if (now == was) continue;
                int clicks = 1;
                if (now)
                {
                    const ULONGLONG t = GetTickCount64();
                    if (b.bit == _lastClickButton && t - _lastClickAt < GetDoubleClickTime() &&
                        abs(x - _lastClickX) < 5 && abs(y - _lastClickY) < 5)
                        clicks = std::min(_clickCount + 1, 3);
                    _clickCount = clicks;
                    _lastClickAt = t;
                    _lastClickButton = b.bit;
                    _lastClickX = x;
                    _lastClickY = y;
                }
                else clicks = _clickCount;
                host->SendMouseClickEvent(e, b.type, !now, clicks);
            }
            _buttons = buttons;
            if (wheel) host->SendMouseWheelEvent(e, 0, wheel * 120);
        }

        void Leave()
        {
            if (!_browser) return;
            CefMouseEvent e;
            _browser->GetHost()->SendMouseMoveEvent(e, true);
            _buttons = 0;
        }

        void Key(UINT msg, WPARAM wp, LPARAM lp, int mods)
        {
            if (!_browser) return;
            CefKeyEvent e;
            e.windows_key_code = (int)wp;
            e.native_key_code = (int)lp;
            e.modifiers = (uint32_t)mods;
            e.is_system_key = msg == WM_SYSKEYDOWN || msg == WM_SYSKEYUP || msg == WM_SYSCHAR;
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) e.type = KEYEVENT_RAWKEYDOWN;
            else if (msg == WM_KEYUP || msg == WM_SYSKEYUP) e.type = KEYEVENT_KEYUP;
            else if (msg == WM_CHAR || msg == WM_SYSCHAR) e.type = KEYEVENT_CHAR;
            else return;
            _browser->GetHost()->SendKeyEvent(e);
        }

    private:
        uint32_t Mods(int buttons) const
        {
            uint32_t m = 0;
            if (buttons & 1) m |= EVENTFLAG_LEFT_MOUSE_BUTTON;
            if (buttons & 2) m |= EVENTFLAG_RIGHT_MOUSE_BUTTON;
            if (buttons & 4) m |= EVENTFLAG_MIDDLE_MOUSE_BUTTON;
            if (GetKeyState(VK_SHIFT) & 0x8000) m |= EVENTFLAG_SHIFT_DOWN;
            if (GetKeyState(VK_CONTROL) & 0x8000) m |= EVENTFLAG_CONTROL_DOWN;
            if (GetKeyState(VK_MENU) & 0x8000) m |= EVENTFLAG_ALT_DOWN;
            return m;
        }

        /// Кадр вида + выпадающий список поверх → разделяемая память.
        void Publish(std::vector<CefRect> rects)
        {
            if (_view.empty()) return;
            if (_frame.Ensure(_id, _viewW, _viewH))
            {
                Send({ "FRAME", N(_id), _frame.Name(), N(_viewW), N(_viewH) });
                rects.assign(1, CefRect(0, 0, _viewW, _viewH));
            }
            if (!_frame.Ready()) return;
            const uint8_t* src = _view.data();
            std::vector<uint8_t> composed;
            if (_popupShown && !_popup.empty())
            {
                composed = _view;
                const auto& r = _popupRect;
                for (int y = 0; y < std::min(_popupH, _viewH - r.y); ++y)
                {
                    if (r.y + y < 0) continue;
                    const int x0 = std::max(0, r.x), n = std::min(_popupW, _viewW - r.x) - (x0 - r.x);
                    if (n > 0)
                        memcpy(&composed[((size_t)(r.y + y) * _viewW + x0) * 4],
                               &_popup[((size_t)y * _popupW + (x0 - r.x)) * 4], (size_t)n * 4);
                }
                src = composed.data();
            }
            _frame.Write(src, rects);
        }

        int _id;
        CefRefPtr<CefBrowser> _browser;
        std::string _pendingUrl;
        bool _visible = true, _closing = false;
        int _frameRate = 60;
        Frame _frame;
        std::vector<uint8_t> _view, _popup;
        int _viewW = 0, _viewH = 0, _popupW = 0, _popupH = 0;
        CefRect _popupRect;
        bool _popupShown = false;
        int _buttons = 0, _clickCount = 0, _lastClickButton = 0, _lastClickX = 0, _lastClickY = 0;
        ULONGLONG _lastClickAt = 0;

        IMPLEMENT_REFCOUNTING(Browser);
    };

    CefRefPtr<Browser> Find(const std::string& id)
    {
        auto it = g_browsers.find(I(id));
        return it == g_browsers.end() ? nullptr : it->second;
    }

    // --- package:// — файлы client_packages сервера --------------------------------------

    std::string ExtensionOf(const std::wstring& path)
    {
        const auto ext = fs::path(path).extension().wstring();
        std::string out;
        for (wchar_t c : ext) if (c != L'.') out += (char)towlower(c);
        return out;
    }

    /// Файл пакета. Текст — всегда UTF-8: у многих страниц RAGE:MP нет
    /// <meta charset>, и без этого кириллица превращалась в «Ð¿Ñ€».
    class PackageFile : public CefStreamResourceHandler
    {
    public:
        PackageFile(const std::string& mime, const CefResponse::HeaderMap& headers, CefRefPtr<CefStreamReader> stream, bool text)
            : CefStreamResourceHandler(200, "OK", mime, headers, stream), _text(text) {}

        void GetResponseHeaders(CefRefPtr<CefResponse> response, int64_t& length, CefString& redirect) override
        {
            CefStreamResourceHandler::GetResponseHeaders(response, length, redirect);
            if (_text) response->SetCharset("utf-8");
        }

    private:
        bool _text;
    };

    class PackageScheme : public CefSchemeHandlerFactory
    {
    public:
        CefRefPtr<CefResourceHandler> Create(CefRefPtr<CefBrowser>, CefRefPtr<CefFrame>, const CefString&,
                                             CefRefPtr<CefRequest> request) override
        {
            CefURLParts parts;
            if (!CefParseURL(request->GetURL(), parts)) return NotFound();
            // package://ui/index.html — «ui» здесь имя хоста, то есть первая папка.
            std::string rel = CefString(&parts.host).ToString() + CefString(&parts.path).ToString();
            rel = CefURIDecode(rel, true, static_cast<cef_uri_unescape_rule_t>(UU_SPACES | UU_PATH_SEPARATORS |
                               UU_URL_SPECIAL_CHARS_EXCEPT_PATH_SEPARATORS)).ToString();
            std::wstring root;
            { std::lock_guard lock(g_rootMutex); root = g_root; }
            if (root.empty()) return NotFound();
            // Только внутри папки пакета: без .., без абсолютных путей и потоков NTFS.
            std::wstring safe;
            size_t start = 0;
            const std::wstring w = CefString(rel).ToWString();
            while (start <= w.size())
            {
                size_t slash = w.find_first_of(L"/\\", start);
                if (slash == std::wstring::npos) slash = w.size();
                const std::wstring seg = w.substr(start, slash - start);
                start = slash + 1;
                if (seg.empty() || seg == L".") continue;
                if (seg == L".." || seg.find(L':') != std::wstring::npos) return NotFound();
                safe += L"\\" + seg;
            }
            if (safe.empty()) safe = L"\\index.html";
            const std::wstring full = root + safe;
            std::error_code ec;
            if (!fs::is_regular_file(full, ec)) return NotFound();
            const std::string ext = ExtensionOf(full);
            std::string mime = CefGetMimeType(ext).ToString();
            if (ext == "js" || ext == "mjs") mime = "text/javascript";
            if (mime.empty()) mime = "application/octet-stream";
            auto stream = CefStreamReader::CreateForFile(full);
            if (!stream) return NotFound();
            CefResponse::HeaderMap headers;
            headers.insert({ "Cache-Control", "no-cache" });
            const bool text = mime.rfind("text/", 0) == 0 || ext == "json" || ext == "svg";
            return new PackageFile(mime, headers, stream, text);
        }

    private:
        static CefRefPtr<CefResourceHandler> NotFound()
        {
            static const char kText[] = "Not Found";
            auto stream = CefStreamReader::CreateForData(const_cast<char*>(kText), sizeof kText - 1);
            CefResponse::HeaderMap headers;
            return new CefStreamResourceHandler(404, "Not Found", "text/plain", headers, stream);
        }

        IMPLEMENT_REFCOUNTING(PackageScheme);
    };

    CefRefPtr<CefRequestContext> g_requestContext;

    CefRefPtr<CefRequestContext> NewRequestContext()
    {
        // Пустой cache_path = incognito: cookies/localStorage одного сервера
        // доступны его браузерам, но не остаются на диске. При смене ROOT
        // создаётся новый контекст, поэтому соседний сервер не наследует их.
        CefRequestContextSettings settings;
        CefString(&settings.accept_language_list) = "ru-RU,ru,en-US,en";
        settings.persist_session_cookies = false;
        auto context = CefRequestContext::CreateContext(settings, nullptr);
        if (context) context->RegisterSchemeHandlerFactory("package", "", new PackageScheme());
        return context;
    }

    // --- мост страницы в рендерере ------------------------------------------------------

    // window.mp в странице — как в RAGE:MP: mp.trigger(имя, …) — событие в
    // клиентский код (mp.events.add в client_packages), mp.events.add в самой
    // странице — приём browser.call(имя, …).
    const char* kPageBridge = R"JS(
(function () {
    if (window.mp && window.mp.__flov) return;
    const send = window.__flovTrigger;
    try { delete window.__flovTrigger; } catch (e) {}
    const listeners = new Map();
    function dispatch(name, args) {
        const list = listeners.get(name);
        if (!list) return;
        for (const fn of list.slice()) {
            try { fn.apply(null, args); } catch (e) { console.error(e); }
        }
    }
    const mp = {
        __flov: true,
        trigger(name, ...args) { send(String(name), JSON.stringify(args)); },
        events: {
            add(name, fn) {
                if (name && typeof name === 'object') { for (const k of Object.keys(name)) mp.events.add(k, name[k]); return; }
                let list = listeners.get(name);
                if (!list) { list = []; listeners.set(name, list); }
                list.push(fn);
            },
            remove(name, fn) {
                if (fn === undefined) { listeners.delete(name); return; }
                const list = listeners.get(name);
                if (list) { const i = list.indexOf(fn); if (i >= 0) list.splice(i, 1); }
            },
            call(name, ...args) { dispatch(name, args); },
        },
    };
    Object.defineProperty(window, 'mp', { value: mp, writable: false, configurable: false });
    Object.defineProperty(window, '__flovRecv', {
        value: (name, json) => { let a = []; try { a = JSON.parse(json); } catch (e) {} dispatch(name, Array.isArray(a) ? a : [a]); },
        writable: false, configurable: false,
    });
})();
)JS";

    class TriggerHandler : public CefV8Handler
    {
    public:
        bool Execute(const CefString&, CefRefPtr<CefV8Value>, const CefV8ValueList& args,
                     CefRefPtr<CefV8Value>&, CefString& exception) override
        {
            if (args.size() < 2 || !args[0]->IsString() || !args[1]->IsString())
            {
                exception = "mp.trigger: нужно имя события";
                return true;
            }
            auto msg = CefProcessMessage::Create("flov-trigger");
            msg->GetArgumentList()->SetString(0, args[0]->GetStringValue());
            msg->GetArgumentList()->SetString(1, args[1]->GetStringValue());
            if (auto ctx = CefV8Context::GetCurrentContext())
                if (auto frame = ctx->GetFrame()) frame->SendProcessMessage(PID_BROWSER, msg);
            return true;
        }
        IMPLEMENT_REFCOUNTING(TriggerHandler);
    };

    // --- приложение -------------------------------------------------------------------

    class App : public CefApp, public CefBrowserProcessHandler, public CefRenderProcessHandler
    {
    public:
        CefRefPtr<CefBrowserProcessHandler> GetBrowserProcessHandler() override { return this; }
        CefRefPtr<CefRenderProcessHandler> GetRenderProcessHandler() override { return this; }

        void OnRegisterCustomSchemes(CefRawPtr<CefSchemeRegistrar> registrar) override
        {
            registrar->AddCustomScheme("package", CEF_SCHEME_OPTION_STANDARD | CEF_SCHEME_OPTION_SECURE |
                                                  CEF_SCHEME_OPTION_CORS_ENABLED | CEF_SCHEME_OPTION_FETCH_ENABLED);
        }

        void OnBeforeCommandLineProcessing(const CefString& processType, CefRefPtr<CefCommandLine> cmd) override
        {
            if (!processType.empty()) return;
            // Видеокарта занята игрой: страницы рисует процессор, кадр уходит в
            // игру готовым. Для интерфейсов (HTML/CSS/Vue/React) этого хватает.
            cmd->AppendSwitch("disable-gpu");
            cmd->AppendSwitch("disable-gpu-compositing");
            cmd->AppendSwitchWithValue("autoplay-policy", "no-user-gesture-required");
            cmd->AppendSwitch("disable-extensions");
            cmd->AppendSwitch("disable-pdf-extension");
            cmd->AppendSwitch("mute-audio-on-hide");
        }

        void OnContextInitialized() override
        {
            CefRegisterSchemeHandlerFactory("package", "", new PackageScheme());
            g_requestContext = NewRequestContext();
            Send({ "READY" });
        }

        void OnContextCreated(CefRefPtr<CefBrowser>, CefRefPtr<CefFrame> frame, CefRefPtr<CefV8Context> context) override
        {
            // Игровой bridge принадлежит только верхней странице. В противном
            // случае любой подключённый iframe (в том числе с чужого origin)
            // получает mp.trigger и может выдавать себя за доверенный UI
            // серверного пакета.
            if (!frame || !frame->IsMain()) return;
            context->GetGlobal()->SetValue("__flovTrigger", CefV8Value::CreateFunction("__flovTrigger", new TriggerHandler()),
                                           V8_PROPERTY_ATTRIBUTE_NONE);
            CefRefPtr<CefV8Value> ret;
            CefRefPtr<CefV8Exception> ex;
            context->Eval(kPageBridge, "flovmp://bridge.js", 0, ret, ex);
        }

        IMPLEMENT_REFCOUNTING(App);
    };

    // --- команды клиента (поток UI) ---------------------------------------------------

    void QuitAll()
    {
        if (g_quitting.exchange(true)) return;
        for (auto& [id, b] : g_browsers)
            b->Close();
        PostUiDelayed([] { CefQuitMessageLoop(); }, 400);
    }

    void Handle(const std::vector<std::string>& p)
    {
        const std::string& t = p[0];
        auto at = [&](size_t i) -> const std::string& { static const std::string e; return i < p.size() ? p[i] : e; };
        if (t == "NEW")
        {
            const int id = I(at(1));
            if (id <= 0 || g_browsers.count(id)) return;
            CefRefPtr<Browser> b = new Browser(id);
            g_browsers[id] = b;
            CefWindowInfo wi;
            wi.SetAsWindowless(nullptr);
            CefBrowserSettings bs;
            bs.windowless_frame_rate = 60;
            bs.background_color = CefColorSetARGB(0, 0, 0, 0);   // прозрачный: под страницей — игра
            if (!CefBrowserHost::CreateBrowser(wi, b.get(), at(2), bs, nullptr, g_requestContext))
            {
                g_browsers.erase(id);
                Send({ "FAIL", N(id), "-2", at(2) });
            }
        }
        else if (t == "SIZE")
        {
            const int w = I(at(1)), h = I(at(2));
            if (w < 64 || h < 64 || w > ipc::kMaxSide || h > ipc::kMaxSide) return;
            g_width = w;
            g_height = h;
            for (auto& [id, b] : g_browsers) if (b->Get()) b->Get()->GetHost()->WasResized();
        }
        else if (t == "ROOT")
        {
            std::wstring next = CefString(at(1)).ToWString();
            while (!next.empty() && (next.back() == L'\\' || next.back() == L'/')) next.pop_back();
            bool changed = false;
            {
                std::lock_guard lock(g_rootMutex);
                changed = next != g_root;
                g_root = std::move(next);
            }
            if (changed) g_requestContext = NewRequestContext();
        }
        else if (t == "QUIT") QuitAll();
        else
        {
            auto b = Find(at(1));
            if (!b) return;
            auto br = b->Get();
            if (t == "DEL") { b->Close(); return; }
            if (t == "URL") { b->Navigate(at(2)); return; }
            if (t == "SHOW") { b->SetVisible(at(2) == "1"); return; }
            if (t == "RATE") { b->SetFrameRate(I(at(2))); return; }
            if (!br) return;   // ввод/JS требуют уже созданный Chromium browser
            if (t == "EXEC") br->GetMainFrame()->ExecuteJavaScript(at(2), "", 0);
            else if (t == "CALL")
                br->GetMainFrame()->ExecuteJavaScript("window.__flovRecv&&window.__flovRecv(" + ipc::JsString(at(2)) + "," +
                                                      ipc::JsString(at(3)) + ")", "", 0);
            else if (t == "RELOAD") { if (at(2) == "1") br->ReloadIgnoreCache(); else br->Reload(); }
            else if (t == "MOUSE") b->Mouse(I(at(2)), I(at(3)), I(at(4)), I(at(5)));
            else if (t == "LEAVE") b->Leave();
            else if (t == "KEY") b->Key((UINT)I(at(2)), (WPARAM)_atoi64(at(3).c_str()), (LPARAM)_atoi64(at(4).c_str()), I(at(5)));
            else if (t == "FOCUS") br->GetHost()->SetFocus(at(2) == "1");
        }
    }

    void ReadPipe()
    {
        std::string buf;
        char chunk[16384];
        for (;;)
        {
            DWORD n = 0;
            if (!ReadFile(g_in, chunk, sizeof chunk, &n, nullptr) || n == 0) break;
            buf.append(chunk, n);
            size_t nl;
            while ((nl = buf.find('\n')) != std::string::npos)
            {
                auto parts = ipc::Parse(buf.substr(0, nl));
                buf.erase(0, nl + 1);
                if (!parts.empty()) PostUi([parts] { Handle(parts); });
            }
            if (buf.size() > 4 * 1024 * 1024) break;
        }
        // Клиент закрыл трубу (игра вышла или упала) — уходим.
        PostUi([] { QuitAll(); });
    }

    std::wstring Arg(const std::wstring& cmd, const std::wstring& name)
    {
        const std::wstring key = L"--" + name + L"=";
        const size_t at = cmd.find(key);
        if (at == std::wstring::npos) return L"";
        size_t end = cmd.find(L' ', at);
        std::wstring v = cmd.substr(at + key.size(), end == std::wstring::npos ? std::wstring::npos : end - at - key.size());
        if (!v.empty() && v.front() == L'"') v.erase(0, 1);
        if (!v.empty() && v.back() == L'"') v.pop_back();
        return v;
    }

    std::wstring LocalAppData()
    {
        wchar_t buf[MAX_PATH] = {};
        const DWORD n = GetEnvironmentVariableW(L"LOCALAPPDATA", buf, MAX_PATH);
        return n ? std::wstring(buf, n) : L".";
    }

    /// Журнал запуска хоста: почему он не поднялся, видно без отладчика.
    int Fail(int code, const std::string& why)
    {
        const DWORD err = GetLastError();
        const std::wstring path = LocalAppData() + L"\\FloVMP\\logs\\cef-host.log";
        HANDLE f = CreateFileW(path.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_ALWAYS, 0, nullptr);
        if (f != INVALID_HANDLE_VALUE)
        {
            SYSTEMTIME t;
            GetLocalTime(&t);
            char head[64];
            snprintf(head, sizeof head, "%02d.%02d %02d:%02d:%02d ", t.wDay, t.wMonth, t.wHour, t.wMinute, t.wSecond);
            const std::string line = head + why + " (код " + std::to_string(code) + ", ошибка Windows " + std::to_string(err) + ")\r\n";
            DWORD w = 0;
            WriteFile(f, line.data(), (DWORD)line.size(), &w, nullptr);
            CloseHandle(f);
        }
        return code;
    }
}

int APIENTRY wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR, int)
{
    CefMainArgs args(instance);
    CefRefPtr<App> app = new App();
    // Подпроцессы Chromium (--type=…) уходят в свой цикл здесь.
    const int code = CefExecuteProcess(args, app.get(), nullptr);
    if (code >= 0) return code;

    const std::wstring cmd = GetCommandLineW();
    const std::wstring pipeName = Arg(cmd, L"flovmp-pipe");
    const DWORD parentPid = (DWORD)_wtoi(Arg(cmd, L"flovmp-parent").c_str());
    const std::wstring data = LocalAppData() + L"\\FloVMP";
    CreateDirectoryW(data.c_str(), nullptr);
    CreateDirectoryW((data + L"\\logs").c_str(), nullptr);
    if (pipeName.rfind(L"\\\\.\\pipe\\flovmp-cef-", 0) != 0 || !parentPid)
        return Fail(2, "нет параметров --flovmp-pipe/--flovmp-parent");

    // Две трубы, по одной на направление (см. browser.cpp).
    g_in = CreateFileW((pipeName + L"-c2h").c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, 0, nullptr);
    if (g_in == INVALID_HANDLE_VALUE) return Fail(3, "не открыта труба команд");
    g_pipe = CreateFileW((pipeName + L"-h2c").c_str(), GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
    if (g_pipe == INVALID_HANDLE_VALUE) return Fail(3, "не открыта труба событий");
    HANDLE parent = OpenProcess(SYNCHRONIZE, FALSE, parentPid);
    if (!parent) return Fail(4, "процесс игры не найден");

    CefSettings settings;
    settings.no_sandbox = true;
    settings.windowless_rendering_enabled = true;
    settings.multi_threaded_message_loop = false;
    settings.log_severity = LOGSEVERITY_WARNING;
    CefString(&settings.log_file) = data + L"\\logs\\cef.log";
    CefString(&settings.root_cache_path) = data + L"\\cef-cache";
    CefString(&settings.accept_language_list) = "ru-RU,ru,en-US,en";
    settings.persist_session_cookies = false;

    if (!CefInitialize(args, settings, app.get(), nullptr)) return Fail(5, "Chromium не запустился — см. cef.log");

    std::thread([parent] {
        WaitForSingleObject(parent, INFINITE);   // игра закрылась — хост за ней
        PostUi([] { QuitAll(); });
        Sleep(3000);
        ExitProcess(0);                          // если CEF не успел закрыться сам
    }).detach();
    std::thread(ReadPipe).detach();

    CefRunMessageLoop();
    if (g_browsers.empty()) CefShutdown();
    ExitProcess(0);
}
