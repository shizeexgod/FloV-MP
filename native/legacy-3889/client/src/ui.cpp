#include "ui.h"
#include "common.h"
#include "invoke.h"

#include <d3d11.h>
#include <dxgi.h>
#include <algorithm>
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
        // --- общее состояние (WndProc, игровой поток, поток рендера) ----------
        std::mutex g_mutex;

        struct ChatEntry { std::string text; ULONGLONG time; };
        std::deque<ChatEntry> g_chat;
        constexpr size_t kChatHistory = 200;
        int g_chatScroll = 0;

        std::vector<Label> g_labels;
        std::string g_hud;
        std::string g_notice;
        ULONGLONG g_noticeUntil = 0;

        bool g_chatEnabled = false;
        bool g_chatOpen = false;
        std::wstring g_input;
        size_t g_caret = 0;
        std::vector<std::wstring> g_history;
        int g_historyPos = -1;
        std::vector<std::string> g_commands;
        bool g_skipChar = false;
        ULONGLONG g_inputClosedAt = 0;

        bool g_connectOpen = false;
        std::wstring g_connectHost, g_connectName;
        int g_connectField = 0;
        bool g_connectRequested = false;

        std::vector<std::string> g_submitted;
        std::vector<int> g_hotkeys;

        // --- рендер -------------------------------------------------------------
        HWND g_hwnd = nullptr;
        WNDPROC g_originalWndProc = nullptr;
        ID3D11Device* g_device = nullptr;
        ID3D11DeviceContext* g_context = nullptr;
        bool g_imguiReady = false;
        LARGE_INTEGER g_lastFrame{}, g_freq{};
        ImFont* g_font = nullptr;
        float g_fontPx = 18.f;

        std::wstring* ActiveField()
        {
            if (g_connectOpen) return g_connectField == 0 ? &g_connectHost : &g_connectName;
            if (g_chatOpen) return &g_input;
            return nullptr;
        }

        void CloseInput()
        {
            g_chatOpen = false;
            g_connectOpen = false;
            g_input.clear();
            g_caret = 0;
            g_historyPos = -1;
            g_chatScroll = 0;
            g_inputClosedAt = GetTickCount64();
        }

        void Paste(std::wstring& field)
        {
            if (!OpenClipboard(nullptr)) return;
            if (HANDLE h = GetClipboardData(CF_UNICODETEXT))
            {
                if (auto* text = static_cast<const wchar_t*>(GlobalLock(h)))
                {
                    for (const wchar_t* p = text; *p && field.size() < 200; ++p)
                        if (*p >= 32) { field.insert(g_caret, 1, *p); ++g_caret; }
                    GlobalUnlock(h);
                }
            }
            CloseClipboard();
        }

        void Autocomplete()
        {
            if (g_input.empty() || g_input[0] != L'/') return;
            const auto typed = ToUtf8(g_input.substr(1));
            for (const auto& c : g_commands)
            {
                if (c.rfind(typed, 0) == 0 && c.size() > typed.size())
                {
                    g_input = L"/" + FromUtf8(c) + L" ";
                    g_caret = g_input.size();
                    return;
                }
            }
        }

        /// true — сообщение обработано и игре его отдавать не нужно.
        bool HandleKey(WPARAM vk)
        {
            const bool ctrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
            std::lock_guard lock(g_mutex);
            auto* field = ActiveField();
            if (!field)
            {
                if (vk == 'T' && g_chatEnabled && !(GetKeyState(VK_MENU) & 0x8000))
                {
                    g_chatOpen = true;
                    g_skipChar = true; // WM_CHAR 't' придёт следом — в строку его не пускаем
                    g_caret = 0;
                    return true;
                }
                if (vk == VK_F3 || vk == VK_F4 || vk == VK_F5 || vk == VK_F9)
                    g_hotkeys.push_back((int)vk);
                return false;
            }

            if (g_caret > field->size()) g_caret = field->size();
            switch (vk)
            {
            case VK_ESCAPE:
                CloseInput();
                return true;
            case VK_RETURN:
                if (g_connectOpen)
                {
                    g_connectRequested = true;
                    CloseInput();
                    return true;
                }
                if (!g_input.empty())
                {
                    g_submitted.push_back(ToUtf8(g_input));
                    g_history.push_back(g_input);
                    if (g_history.size() > 50) g_history.erase(g_history.begin());
                }
                CloseInput();
                return true;
            case VK_TAB:
                if (g_connectOpen) { g_connectField ^= 1; g_caret = ActiveField()->size(); }
                else Autocomplete();
                return true;
            case VK_BACK:
                if (g_caret > 0) { field->erase(g_caret - 1, 1); --g_caret; }
                return true;
            case VK_DELETE:
                if (g_caret < field->size()) field->erase(g_caret, 1);
                return true;
            case VK_LEFT: if (g_caret > 0) --g_caret; return true;
            case VK_RIGHT: if (g_caret < field->size()) ++g_caret; return true;
            case VK_HOME: g_caret = 0; return true;
            case VK_END: g_caret = field->size(); return true;
            case VK_PRIOR: if (g_chatOpen) g_chatScroll = std::min<int>(g_chatScroll + 5, (int)g_chat.size()); return true;
            case VK_NEXT: if (g_chatOpen) g_chatScroll = std::max(0, g_chatScroll - 5); return true;
            case VK_UP:
            case VK_DOWN:
                if (g_chatOpen && !g_history.empty())
                {
                    if (vk == VK_UP) g_historyPos = g_historyPos < 0 ? (int)g_history.size() - 1 : std::max(0, g_historyPos - 1);
                    else g_historyPos = g_historyPos < 0 ? -1 : g_historyPos + 1;
                    if (g_historyPos >= (int)g_history.size()) g_historyPos = -1;
                    g_input = g_historyPos < 0 ? L"" : g_history[g_historyPos];
                    g_caret = g_input.size();
                }
                return true;
            case 'V':
                // Вставку делает WM_CHAR 0x16 (Ctrl+V) — здесь только не пускаем клавишу в игру.
                break;
            }
            (void)ctrl;
            return true; // остальные клавиши при открытом вводе игре не нужны
        }

        bool HandleChar(WPARAM ch)
        {
            std::lock_guard lock(g_mutex);
            auto* field = ActiveField();
            if (!field) return false;
            if (g_skipChar) { g_skipChar = false; return true; }
            if (ch == 0x16) { Paste(*field); return true; } // Ctrl+V как символ
            if (ch < 32 || ch == 127) return true;
            const size_t limit = g_connectOpen ? 64 : 200;
            if (field->size() >= limit) return true;
            if (g_caret > field->size()) g_caret = field->size();
            field->insert(g_caret, 1, (wchar_t)ch);
            ++g_caret;
            return true;
        }

        LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
        {
            switch (msg)
            {
            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                if (HandleKey(wp)) return 0;
                break;
            case WM_KEYUP:
            case WM_SYSKEYUP:
                if (InputActive()) return 0;
                break;
            case WM_CHAR:
                if (HandleChar(wp)) return 0;
                break;
            }
            return CallWindowProcW(g_originalWndProc, hwnd, msg, wp, lp);
        }

        // --- рисование ------------------------------------------------------------
        ImU32 Rgb(uint32_t rgb, int alpha = 255)
        {
            return IM_COL32((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, alpha);
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

        void ShadowText(ImDrawList* dl, ImVec2 pos, ImU32 color, const char* begin, const char* end = nullptr, float size = 0)
        {
            const float px = size > 0 ? size : g_fontPx;
            const ImU32 shadow = IM_COL32(0, 0, 0, (color >> IM_COL32_A_SHIFT) & 0xFF);
            dl->AddText(g_font, px, ImVec2(pos.x + 1, pos.y + 1), shadow, begin, end);
            dl->AddText(g_font, px, pos, color, begin, end);
        }

        /// Строка чата с цветами и переносом по ширине. Возвращает высоту.
        float DrawColoredWrapped(ImDrawList* dl, ImVec2 origin, float width, const std::string& text, int alpha, bool measureOnly)
        {
            float x = origin.x, y = origin.y;
            const float line = g_fontPx * 1.2f;
            for (const auto& seg : SplitColors(text, 0xFFFFFF))
            {
                const char* p = seg.text.c_str();
                const char* end = p + seg.text.size();
                while (p < end)
                {
                    const char* wrap = g_font->CalcWordWrapPositionA(g_fontPx / g_font->FontSize, p, end, origin.x + width - x);
                    if (wrap == p)
                    {
                        if (x > origin.x) { x = origin.x; y += line; continue; }
                        wrap = p + 1;
                    }
                    if (!measureOnly) ShadowText(dl, ImVec2(x, y), Rgb(seg.rgb, alpha), p, wrap);
                    x += g_font->CalcTextSizeA(g_fontPx, FLT_MAX, 0, p, wrap).x;
                    p = wrap;
                    if (p < end) { x = origin.x; y += line; while (p < end && *p == ' ') ++p; }
                }
            }
            return y - origin.y + line;
        }

        void DrawInputBox(ImDrawList* dl, ImVec2 pos, float width, const std::wstring& value, bool focused, const char* placeholder)
        {
            const float h = g_fontPx * 1.6f;
            dl->AddRectFilled(pos, ImVec2(pos.x + width, pos.y + h), IM_COL32(12, 12, 16, 220), 4.f);
            dl->AddRect(pos, ImVec2(pos.x + width, pos.y + h), focused ? IM_COL32(255, 61, 138, 255) : IM_COL32(63, 63, 70, 255), 4.f, 0, 1.5f);
            const auto utf8 = ToUtf8(value);
            const ImVec2 textPos(pos.x + 8, pos.y + (h - g_fontPx) / 2);
            if (utf8.empty() && placeholder)
                dl->AddText(g_font, g_fontPx, textPos, IM_COL32(113, 113, 122, 255), placeholder);
            else
                dl->AddText(g_font, g_fontPx, textPos, IM_COL32(244, 244, 245, 255), utf8.c_str());
            if (focused && (GetTickCount64() / 500) % 2 == 0)
            {
                const auto before = ToUtf8(value.substr(0, std::min(g_caret, value.size())));
                const float cx = textPos.x + g_font->CalcTextSizeA(g_fontPx, FLT_MAX, 0, before.c_str()).x;
                dl->AddLine(ImVec2(cx, textPos.y), ImVec2(cx, textPos.y + g_fontPx), IM_COL32(255, 255, 255, 255), 1.5f);
            }
        }

        void DrawFrame(float w, float h)
        {
            auto* dl = ImGui::GetBackgroundDrawList();
            const float s = h / 1080.f;
            const auto now = GetTickCount64();
            std::lock_guard lock(g_mutex);

            // Ники и ESP (под чатом).
            for (const auto& l : g_labels)
            {
                const float px = g_fontPx * l.scale;
                const ImVec2 size = g_font->CalcTextSizeA(px, FLT_MAX, 0, l.text.c_str());
                const ImVec2 pos(l.x * w - size.x / 2, l.y * h - size.y);
                ShadowText(dl, pos, l.color, l.text.c_str(), nullptr, px);
                if (l.health >= 0)
                {
                    const float bw = 60 * s, bh = 5 * s;
                    const ImVec2 b0(l.x * w - bw / 2, pos.y + size.y + 2 * s);
                    dl->AddRectFilled(b0, ImVec2(b0.x + bw, b0.y + bh), IM_COL32(0, 0, 0, 160));
                    dl->AddRectFilled(b0, ImVec2(b0.x + bw * std::clamp(l.health, 0.f, 1.f), b0.y + bh), IM_COL32(52, 211, 153, 230));
                }
            }

            // Чат: слева сверху, как привычно игрокам alt:V и SA-MP.
            if (g_chatEnabled || !g_chat.empty())
            {
                const float x = 24 * s, top = 60 * s, width = 620 * s;
                const int visible = g_chatOpen ? 14 : 9;
                const bool fade = !g_chatOpen;
                std::vector<const ChatEntry*> lines;
                const int end = (int)g_chat.size() - (g_chatOpen ? g_chatScroll : 0);
                for (int i = std::max(0, end - visible); i < end; ++i) lines.push_back(&g_chat[i]);
                float y = top;
                if (g_chatOpen)
                {
                    float total = 0;
                    for (auto* e : lines) total += DrawColoredWrapped(dl, ImVec2(x, 0), width, e->text, 0, true);
                    dl->AddRectFilled(ImVec2(x - 10 * s, top - 8 * s), ImVec2(x + width + 10 * s, top + total + 8 * s), IM_COL32(0, 0, 0, 110), 6.f);
                }
                for (auto* e : lines)
                {
                    int alpha = 255;
                    if (fade)
                    {
                        const auto age = now - e->time;
                        if (age > 15000) continue;
                        if (age > 12000) alpha = (int)(255 * (15000 - age) / 3000.0);
                    }
                    y += DrawColoredWrapped(dl, ImVec2(x, y), width, e->text, alpha, false);
                }
                if (g_chatOpen)
                    DrawInputBox(dl, ImVec2(x - 10 * s, std::max(y, top) + 14 * s), width + 20 * s, g_input, true,
                                 "Сообщение или /команда  (Enter — отправить, Esc — закрыть, Tab — дополнить)");
            }

            // Строка состояния справа сверху.
            if (!g_hud.empty())
            {
                const ImVec2 size = g_font->CalcTextSizeA(g_fontPx * 0.85f, FLT_MAX, 0, g_hud.c_str());
                ShadowText(dl, ImVec2(w - size.x - 20 * s, 12 * s), IM_COL32(228, 228, 231, 220), g_hud.c_str(), nullptr, g_fontPx * 0.85f);
            }

            // Уведомление по центру снизу.
            if (!g_notice.empty() && now < g_noticeUntil)
            {
                const float px = g_fontPx * 1.1f;
                const ImVec2 size = g_font->CalcTextSizeA(px, 900 * s, 900 * s, g_notice.c_str());
                const ImVec2 pos((w - size.x) / 2, h * 0.78f);
                dl->AddRectFilled(ImVec2(pos.x - 16 * s, pos.y - 10 * s), ImVec2(pos.x + size.x + 16 * s, pos.y + size.y + 10 * s), IM_COL32(9, 9, 11, 215), 6.f);
                dl->AddRectFilled(ImVec2(pos.x - 16 * s, pos.y - 10 * s), ImVec2(pos.x - 12 * s, pos.y + size.y + 10 * s), IM_COL32(255, 61, 138, 255));
                dl->AddText(g_font, px, pos, IM_COL32(244, 244, 245, 255), g_notice.c_str(), nullptr, 900 * s);
            }

            // Окно подключения (F9).
            if (g_connectOpen)
            {
                const float pw = 520 * s, ph = 250 * s;
                const ImVec2 p0((w - pw) / 2, (h - ph) / 2);
                dl->AddRectFilled(ImVec2(0, 0), ImVec2(w, h), IM_COL32(0, 0, 0, 120));
                dl->AddRectFilled(p0, ImVec2(p0.x + pw, p0.y + ph), IM_COL32(9, 9, 11, 245), 8.f);
                dl->AddRect(p0, ImVec2(p0.x + pw, p0.y + ph), IM_COL32(39, 39, 42, 255), 8.f);
                ShadowText(dl, ImVec2(p0.x + 24 * s, p0.y + 20 * s), IM_COL32(255, 61, 138, 255), "FloV:MP", nullptr, g_fontPx * 1.3f);
                ShadowText(dl, ImVec2(p0.x + 24 * s + g_font->CalcTextSizeA(g_fontPx * 1.3f, FLT_MAX, 0, "FloV:MP ").x, p0.y + 20 * s),
                           IM_COL32(244, 244, 245, 255), "подключение к серверу", nullptr, g_fontPx * 1.3f);
                dl->AddText(g_font, g_fontPx * 0.85f, ImVec2(p0.x + 24 * s, p0.y + 64 * s), IM_COL32(161, 161, 170, 255), "Адрес сервера (IP:порт)");
                DrawInputBox(dl, ImVec2(p0.x + 24 * s, p0.y + 84 * s), pw - 48 * s, g_connectHost, g_connectField == 0, "127.0.0.1:7788");
                dl->AddText(g_font, g_fontPx * 0.85f, ImVec2(p0.x + 24 * s, p0.y + 128 * s), IM_COL32(161, 161, 170, 255), "Ник");
                DrawInputBox(dl, ImVec2(p0.x + 24 * s, p0.y + 148 * s), pw - 48 * s, g_connectName, g_connectField == 1, "Ваш ник");
                dl->AddText(g_font, g_fontPx * 0.8f, ImVec2(p0.x + 24 * s, p0.y + 204 * s), IM_COL32(113, 113, 122, 255),
                            "Enter — подключиться   Tab — следующее поле   Esc — закрыть");
            }
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

            // Шрифт с кириллицей из Windows: у игры своих шрифтов для оверлея нет.
            g_fontPx = std::max(15.f, 19.f * (float)desc.BufferDesc.Height / 1080.f);
            static const ImWchar ranges[] = { 0x0020, 0x00FF, 0x0400, 0x052F, 0x2000, 0x206F, 0x20A0, 0x20CF, 0x2100, 0x218F, 0 };
            ImFontConfig cfg;
            cfg.OversampleH = 2;
            for (const char* path : { "C:\\Windows\\Fonts\\segoeui.ttf", "C:\\Windows\\Fonts\\arial.ttf", "C:\\Windows\\Fonts\\tahoma.ttf" })
            {
                if (GetFileAttributesA(path) == INVALID_FILE_ATTRIBUTES) continue;
                g_font = io.Fonts->AddFontFromFileTTF(path, g_fontPx * 1.3f, &cfg, ranges);
                if (g_font) break;
            }
            if (!g_font) g_font = io.Fonts->AddFontDefault();

            ImGui_ImplDX11_Init(g_device, g_context);
            QueryPerformanceFrequency(&g_freq);
            QueryPerformanceCounter(&g_lastFrame);

            g_originalWndProc = reinterpret_cast<WNDPROC>(SetWindowLongPtrW(g_hwnd, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(WndProc)));
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

    void Init()
    {
        if (shv::presentCallbackRegister) shv::presentCallbackRegister(OnPresent);
        else Log("ui: ScriptHookV без presentCallbackRegister — оверлей недоступен");
    }

    void Shutdown()
    {
        if (shv::presentCallbackUnregister) shv::presentCallbackUnregister(OnPresent);
        if (g_hwnd && g_originalWndProc)
            SetWindowLongPtrW(g_hwnd, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(g_originalWndProc));
    }

    void AddChat(const std::string& text)
    {
        std::lock_guard lock(g_mutex);
        g_chat.push_back({ text, GetTickCount64() });
        while (g_chat.size() > kChatHistory) g_chat.pop_front();
    }

    void ClearChat()
    {
        std::lock_guard lock(g_mutex);
        g_chat.clear();
        g_chatScroll = 0;
    }

    void SetLabels(std::vector<Label>&& labels)
    {
        std::lock_guard lock(g_mutex);
        g_labels = std::move(labels);
    }

    void SetHud(const std::string& text)
    {
        std::lock_guard lock(g_mutex);
        g_hud = text;
    }

    void Notify(const std::string& text, int ms)
    {
        std::lock_guard lock(g_mutex);
        g_notice = text;
        g_noticeUntil = GetTickCount64() + ms;
    }

    void SetChatEnabled(bool enabled)
    {
        std::lock_guard lock(g_mutex);
        g_chatEnabled = enabled;
        if (!enabled && g_chatOpen) CloseInput();
    }

    void SetCommands(const std::vector<std::string>& commands)
    {
        std::lock_guard lock(g_mutex);
        g_commands = commands;
        std::sort(g_commands.begin(), g_commands.end());
    }

    void OpenConnectDialog(const std::string& host, const std::string& name)
    {
        std::lock_guard lock(g_mutex);
        g_chatOpen = false;
        g_connectOpen = true;
        g_connectHost = FromUtf8(host);
        g_connectName = FromUtf8(name);
        g_connectField = host.empty() ? 0 : 1;
        g_caret = (g_connectField == 0 ? g_connectHost : g_connectName).size();
    }

    bool InputActive()
    {
        std::lock_guard lock(g_mutex);
        return g_chatOpen || g_connectOpen;
    }

    uint32_t MsSinceInputClosed()
    {
        std::lock_guard lock(g_mutex);
        return (uint32_t)std::min<ULONGLONG>(GetTickCount64() - g_inputClosedAt, 0xFFFFFFFF);
    }

    std::vector<std::string> TakeSubmittedChat()
    {
        std::lock_guard lock(g_mutex);
        return std::exchange(g_submitted, {});
    }

    std::vector<int> TakeHotkeys()
    {
        std::lock_guard lock(g_mutex);
        return std::exchange(g_hotkeys, {});
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
}
