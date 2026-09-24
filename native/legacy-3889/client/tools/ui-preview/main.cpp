// Предпросмотр игрового интерфейса FloV:MP без GTA V.
//
// Зачем: интерфейс клиента рисуется поверх кадра игры, и увидеть правку
// раньше можно было только запустив GTA V с установленным ASI. Здесь то же
// самое окно DirectX 11 создаётся на рабочем столе, и ui.cpp рисует в него
// настоящий, не переписанный заново интерфейс: ники, чат, консоль F8,
// загрузочный экран, меню Esc и уведомления.
//
// Это средство разработки. В поставку клиента оно не входит.
//
//   F1 — игровой HUD          F2 — загрузочный экран
//   F3 — меню Esc             F4 — уведомление
//   T  — чат                  F8 — консоль
//   Esc — выйти (когда ничего не открыто)

#include <windows.h>
#include <shellapi.h>
#include <d3d11.h>
#include <dxgi.h>

#include <cmath>
#include <cstdio>
#include <string>
#include <vector>

#include "common.h"
#include "ui.h"

#pragma comment(lib, "d3d11.lib")

namespace
{
    ID3D11Device* g_device = nullptr;
    ID3D11DeviceContext* g_context = nullptr;
    IDXGISwapChain* g_swapChain = nullptr;
    bool g_running = true;

    LRESULT CALLBACK PreviewWndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
    {
        if (msg == WM_DESTROY) { g_running = false; PostQuitMessage(0); return 0; }
        return DefWindowProcW(hwnd, msg, wp, lp);
    }

    /// Снимок кадра в BMP без захвата экрана: окно может быть скрыто, чужие
    /// окна в кадр не попадают, фокус у пользователя не отбирается.
    bool SaveFrame(const std::wstring& path)
    {
        ID3D11Texture2D* back = nullptr;
        if (FAILED(g_swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&back))) || !back) return false;
        D3D11_TEXTURE2D_DESC desc{};
        back->GetDesc(&desc);
        desc.Usage = D3D11_USAGE_STAGING;
        desc.BindFlags = 0;
        desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        desc.MiscFlags = 0;
        ID3D11Texture2D* staging = nullptr;
        if (FAILED(g_device->CreateTexture2D(&desc, nullptr, &staging)) || !staging) { back->Release(); return false; }
        g_context->CopyResource(staging, back);

        D3D11_MAPPED_SUBRESOURCE mapped{};
        bool ok = false;
        if (SUCCEEDED(g_context->Map(staging, 0, D3D11_MAP_READ, 0, &mapped)))
        {
            const uint32_t width = desc.Width, height = desc.Height;
            const uint32_t rowBytes = width * 4;
            BITMAPFILEHEADER file{};
            BITMAPINFOHEADER info{};
            file.bfType = 0x4D42;
            file.bfOffBits = sizeof(file) + sizeof(info);
            file.bfSize = file.bfOffBits + rowBytes * height;
            info.biSize = sizeof(info);
            info.biWidth = (LONG)width;
            info.biHeight = -(LONG)height;   // сверху вниз
            info.biPlanes = 1;
            info.biBitCount = 32;
            info.biCompression = BI_RGB;

            FILE* out = nullptr;
            if (_wfopen_s(&out, path.c_str(), L"wb") == 0 && out)
            {
                fwrite(&file, sizeof(file), 1, out);
                fwrite(&info, sizeof(info), 1, out);
                std::vector<uint8_t> row(rowBytes);
                for (uint32_t y = 0; y < height; ++y)
                {
                    const uint8_t* src = static_cast<const uint8_t*>(mapped.pData) + (size_t)y * mapped.RowPitch;
                    for (uint32_t x = 0; x < width; ++x)
                    {
                        // Буфер RGBA, BMP ждёт BGRA.
                        row[x * 4 + 0] = src[x * 4 + 2];
                        row[x * 4 + 1] = src[x * 4 + 1];
                        row[x * 4 + 2] = src[x * 4 + 0];
                        row[x * 4 + 3] = 255;
                    }
                    fwrite(row.data(), 1, rowBytes, out);
                }
                fclose(out);
                ok = true;
            }
            g_context->Unmap(staging, 0);
        }
        staging->Release();
        back->Release();
        return ok;
    }

    /// Ники над игроками: координаты — доли экрана, как их считает game.cpp.
    std::vector<flov::ui::Label> DemoLabels(float time)
    {
        std::vector<flov::ui::Label> labels;

        flov::ui::Label near_;
        near_.x = 0.38f + 0.02f * std::sin(time * 0.6f);
        near_.y = 0.42f;
        near_.name = "Дмитрий Соколов";
        near_.id = 7;
        near_.health = 0.82f;
        near_.armor = 0.45f;
        near_.speaking = true;
        near_.extra = "12 м";
        labels.push_back(near_);

        flov::ui::Label admin;
        admin.x = 0.62f;
        admin.y = 0.47f;
        admin.name = "Анна Верещагина";
        admin.id = 12;
        admin.health = 0.34f;   // ниже порога: полоска должна стать красной
        admin.armor = 0.f;
        admin.adminLevel = 3;
        admin.extra = "31 м";
        admin.scale = 0.88f;
        admin.alpha = 0.85f;
        labels.push_back(admin);

        flov::ui::Label far_;
        far_.x = 0.72f;
        far_.y = 0.5f;
        far_.name = "Игрок с очень длинным ником";
        far_.id = 148;
        far_.health = 1.f;
        far_.armor = 1.f;
        far_.extra = "74 м";
        far_.scale = 0.7f;
        far_.alpha = 0.55f;
        labels.push_back(far_);

        return labels;
    }

    void FillChat()
    {
        flov::ui::AddChat("Сервер запущен, версия " + std::string(flov::kClientVersion), "", 0xFF3D8A);
        flov::ui::AddChat("подключился к серверу", "Дмитрий Соколов", 0x9AE6B4);
        flov::ui::AddChat("Всем привет, где тут автосалон?", "Дмитрий Соколов", 0xFFFFFF);
        flov::ui::AddChat("{ff3d8a}[Администрация]{ffffff} техработы через 10 минут", "", 0xFFFFFF);
        flov::ui::AddChat("Очень длинное сообщение, которое обязано переноситься по словам, а не уезжать за край экрана и не обрезаться посередине слова.", "Анна Верещагина", 0x60A5FA);
    }

    void PushStats(float fps, float time)
    {
        flov::ui::Stats stats;
        stats.fps = fps;
        stats.frameMs = fps > 0.f ? 1000.f / fps : 0.f;
        stats.ping = 28 + (int)(8.f * std::sin(time));
        stats.streamed = 3;
        stats.online = 17;
        stats.server = "Тестовый сервер";
        stats.endpoint = "188.127.229.224:7798";
        stats.connected = true;
        stats.bytesIn = 14 * 1024;
        stats.bytesOut = 6 * 1024;
        stats.state = "в игре";
        stats.entities = {
            "7   Дмитрий Соколов     HP 164  AR 45   12 м",
            "12  Анна Верещагина     HP 68   AR 0    31 м",
            "148 Игрок с очень длинным ником  HP 200 AR 100  74 м",
        };
        flov::ui::SetStats(stats);
    }

    void ShowMenu()
    {
        flov::ui::OpenMenu("preview", "FloV:MP", {
            { "Продолжить игру", "Закрыть меню и вернуться в мир" },
            { "Настройки игры", "Штатное меню GTA: графика, звук, управление" },
            { "Карта", "Полноэкранная карта сервера" },
            { "Список игроков", "Кто сейчас на сервере" },
            { "Отключиться", "Выйти с сервера" },
        });
    }
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR commandLine, int)
{
    // --shot <файл.bmp> --scene hud|loading|menu|console — снять кадр и выйти.
    // Так снимок делает сама программа: чужие окна в кадр не попадают и фокус
    // у пользователя не отбирается.
    std::wstring shotPath, scene = L"hud";
    {
        int count = 0;
        LPWSTR* argv = CommandLineToArgvW(commandLine, &count);
        for (int i = 0; argv && i < count; ++i)
        {
            const std::wstring arg = argv[i];
            if (arg == L"--shot" && i + 1 < count) shotPath = argv[++i];
            else if (arg == L"--scene" && i + 1 < count) scene = argv[++i];
        }
        if (argv) LocalFree(argv);
    }
    const bool headless = !shotPath.empty();

    WNDCLASSEXW wc{};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = PreviewWndProc;
    wc.hInstance = instance;
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.lpszClassName = L"FloVMPUiPreview";
    RegisterClassExW(&wc);

    HWND hwnd = CreateWindowExW(0, wc.lpszClassName, L"FloV:MP — предпросмотр интерфейса",
                                WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 1600, 900,
                                nullptr, nullptr, instance, nullptr);
    if (!hwnd) return 1;

    DXGI_SWAP_CHAIN_DESC sd{};
    sd.BufferCount = 2;
    sd.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    sd.BufferDesc.RefreshRate.Numerator = 60;
    sd.BufferDesc.RefreshRate.Denominator = 1;
    sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    sd.OutputWindow = hwnd;
    sd.SampleDesc.Count = 1;
    sd.Windowed = TRUE;
    sd.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;

    D3D_FEATURE_LEVEL level{};
    const D3D_FEATURE_LEVEL wanted[] = { D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_0 };
    if (FAILED(D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, wanted, 2,
                                             D3D11_SDK_VERSION, &sd, &g_swapChain, &g_device, &level, &g_context)))
    {
        MessageBoxW(hwnd, L"Не удалось создать устройство DirectX 11.", L"FloV:MP", MB_ICONERROR);
        return 1;
    }

    if (!headless) { ShowWindow(hwnd, SW_SHOW); UpdateWindow(hwnd); }

    // Перехват клавиш Rockstar и переименование окна — только внутри игры.
    flov::ui::SetPreviewMode(true);
    flov::ui::Init();
    flov::ui::SetBrand("FloV:MP");
    flov::ui::SetAccent(0xFF3D8A);
    flov::ui::SetWatermark("FloV:MP · предпросмотр интерфейса");
    flov::ui::SetChatEnabled(true);
    flov::ui::SetConsoleEnabled(true);
    flov::ui::SetAdminLevel(5);
    flov::ui::SetNetgraph(true);
    flov::ui::SetCommands({
        { "/help", "Список команд" },
        { "/car", "Заспавнить машину" },
        { "/dv", "Убрать машину" },
        { "/me", "Действие от третьего лица" },
    });
    flov::ui::SetLoadingStyle({}, 0xFF3D8A);
    FillChat();
    flov::ui::Notify("Средство предпросмотра: F1 HUD, F2 загрузка, F3 меню, F4 уведомление", 6000);

    LARGE_INTEGER freq{}, start{}, prev{};
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&start);
    prev = start;
    const float clear[4] = { 0.07f, 0.08f, 0.10f, 1.f };

    if (headless)
    {
        if (scene == L"loading") { flov::ui::ShowLoading(""); flov::ui::LoadingStep("Loading the surroundings", 62.f); }
        else if (scene == L"menu") ShowMenu();
        else if (scene == L"connect") flov::ui::OpenConnectDialog("188.127.229.224:7798", "Дмитрий_Соколов");
    }

    int frame = 0;
    MSG msg{};
    while (g_running)
    {
        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            if (msg.message == WM_QUIT) g_running = false;
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        if (!g_running) break;

        LARGE_INTEGER now{};
        QueryPerformanceCounter(&now);
        const float time = (float)(now.QuadPart - start.QuadPart) / (float)freq.QuadPart;
        const float dt = (float)(now.QuadPart - prev.QuadPart) / (float)freq.QuadPart;
        prev = now;

        // Клавиши сцен читаем сами: ui.cpp отдаёт наверх только свои горячие
        // клавиши, а сцены существуют лишь в предпросмотре.
        static bool prevF1 = false, prevF2 = false, prevF3 = false, prevF4 = false;
        const bool f1 = (GetAsyncKeyState(VK_F1) & 0x8000) != 0;
        const bool f2 = (GetAsyncKeyState(VK_F2) & 0x8000) != 0;
        const bool f3 = (GetAsyncKeyState(VK_F3) & 0x8000) != 0;
        const bool f4 = (GetAsyncKeyState(VK_F4) & 0x8000) != 0;
        if (f1 && !prevF1) { flov::ui::HideLoading(); flov::ui::CloseMenu(); }
        if (f2 && !prevF2)
        {
            flov::ui::ShowLoading("");
            flov::ui::LoadingStep("Loading the surroundings", 62.f);
        }
        if (f3 && !prevF3) ShowMenu();
        if (f4 && !prevF4) flov::ui::Notify("Вы получили 5 000 ₽ за смену", 4000);
        prevF1 = f1; prevF2 = f2; prevF3 = f3; prevF4 = f4;

        if (flov::ui::LoadingVisible() && !headless)
        {
            const float percent = std::fmod(time * 12.f, 100.f);
            flov::ui::LoadingStep(percent < 50.f ? "Receiving server settings" : "Preparing the world", percent);
        }

        // Меню и чат читаем, иначе очереди растут без конца.
        for (const auto& event : flov::ui::TakeMenuEvents())
            flov::ui::ConsoleLog("UI", "меню " + event.id + ": пункт " + std::to_string(event.index));
        for (const auto& line : flov::ui::TakeSubmittedChat())
            flov::ui::AddChat(line, "Вы", 0xFF3D8A);
        for (const auto& command : flov::ui::TakeConsoleCommands())
            flov::ui::ConsoleLog("CMD", command);
        if (flov::ui::TakeEscRequest()) ShowMenu();
        flov::ui::TakeHotkeys();

        // Курсор консоли: в игре его отдаёт игровой поток из управления GTA.
        POINT cursor{};
        GetCursorPos(&cursor);
        ScreenToClient(hwnd, &cursor);
        RECT client{};
        GetClientRect(hwnd, &client);
        const float width = (float)std::max<LONG>(1, client.right - client.left);
        const float height = (float)std::max<LONG>(1, client.bottom - client.top);
        flov::ui::SetCursor((float)cursor.x / width, (float)cursor.y / height,
                            (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0, 0);

        flov::ui::SetLabels(DemoLabels(time));
        PushStats(dt > 0.f ? 1.f / dt : 0.f, time);
        flov::ui::SetMicIndicator(std::fmod(time, 6.f) < 2.f ? 1 : 0);

        ID3D11Texture2D* back = nullptr;
        if (SUCCEEDED(g_swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&back))) && back)
        {
            ID3D11RenderTargetView* rtv = nullptr;
            if (SUCCEEDED(g_device->CreateRenderTargetView(back, nullptr, &rtv)))
            {
                g_context->ClearRenderTargetView(rtv, clear);
                g_context->OMSetRenderTargets(1, &rtv, nullptr);
                rtv->Release();
            }
            back->Release();
        }

        flov::ui::Present(g_swapChain);
        g_swapChain->Present(headless ? 0 : 1, 0);

        // Ждём и кадры, и время: панели появляются с плавным проявлением, и
        // снимок, сделанный слишком рано, показывает их полупрозрачными.
        ++frame;
        if (headless && frame >= 45 && time > 1.2f)
        {
            SaveFrame(shotPath);
            g_running = false;
        }
    }

    flov::ui::Shutdown();
    if (g_swapChain) g_swapChain->Release();
    if (g_context) g_context->Release();
    if (g_device) g_device->Release();
    return 0;
}
