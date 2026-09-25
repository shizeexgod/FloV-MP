// Проверка браузеров (пункт 26b) без GTA V: настоящий flovmp-cef.exe,
// настоящий Chromium, страница из package://. Кадр читается обратно из
// текстуры DirectX — проверяется то, что увидит игрок, а не заглушка.
//
//   flovmp-browser-test.exe <путь к flovmp-cef.exe>   — код возврата 0, если всё прошло

#include <windows.h>
#include <d3d11.h>

#include <cstdio>
#include <fstream>
#include <functional>
#include <string>
#include <vector>

#include "browser.h"
#include "common.h"

namespace
{
    int g_failed = 0, g_passed = 0;
    void Check(bool ok, const std::string& what)
    {
        printf("  [%s] %s\n", ok ? "OK " : "ОШИБКА", what.c_str());
        (ok ? g_passed : g_failed)++;
    }

    ID3D11Device* g_dev = nullptr;
    ID3D11DeviceContext* g_ctx = nullptr;
    int g_w = 800, g_h = 600;
    std::vector<flov::browser::Event> g_seen;

    void Pump() { flov::browser::Render(g_dev, g_ctx, nullptr, (float)g_w, (float)g_h); for (auto& e : flov::browser::TakeEvents()) g_seen.push_back(e); }

    /// Крутить кадры, пока не выполнится условие или не выйдет время.
    bool Until(const std::function<bool()>& pred, int ms)
    {
        const ULONGLONG end = GetTickCount64() + ms;
        while (GetTickCount64() < end)
        {
            Pump();
            if (pred()) return true;
            Sleep(16);
        }
        return false;
    }

    const flov::browser::Event* Seen(flov::browser::Event::Kind k, const std::string& a = "")
    {
        for (auto& e : g_seen) if (e.kind == k && (a.empty() || e.a == a)) return &e;
        return nullptr;
    }

    size_t SeenCount(flov::browser::Event::Kind k, const std::string& a)
    {
        size_t n = 0;
        for (const auto& e : g_seen) if (e.kind == k && e.a == a) ++n;
        return n;
    }

    /// Пиксель BGRA из текстуры браузера.
    bool Pixel(int id, int x, int y, uint8_t out[4])
    {
        auto* tex = static_cast<ID3D11Texture2D*>(flov::browser::TextureOf(id));
        if (!tex) return false;
        D3D11_TEXTURE2D_DESC d{};
        tex->GetDesc(&d);
        d.Usage = D3D11_USAGE_STAGING;
        d.BindFlags = 0;
        d.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        ID3D11Texture2D* st = nullptr;
        if (FAILED(g_dev->CreateTexture2D(&d, nullptr, &st))) return false;
        g_ctx->CopyResource(st, tex);
        D3D11_MAPPED_SUBRESOURCE m{};
        bool ok = false;
        if (SUCCEEDED(g_ctx->Map(st, 0, D3D11_MAP_READ, 0, &m)))
        {
            memcpy(out, (uint8_t*)m.pData + (size_t)y * m.RowPitch + (size_t)x * 4, 4);
            g_ctx->Unmap(st, 0);
            ok = true;
        }
        st->Release();
        return ok;
    }

    int Size(int id)
    {
        auto* tex = static_cast<ID3D11Texture2D*>(flov::browser::TextureOf(id));
        if (!tex) return 0;
        D3D11_TEXTURE2D_DESC d{};
        tex->GetDesc(&d);
        return (int)d.Width;
    }
}

int wmain(int argc, wchar_t** argv)
{
    SetConsoleOutputCP(CP_UTF8);
    setvbuf(stdout, nullptr, _IONBF, 0);
    if (argc < 2) { printf("нужен путь к flovmp-cef.exe\n"); return 2; }
    flov::browser::SetHostExe(argv[1]);

    D3D_FEATURE_LEVEL fl;
    if (FAILED(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &g_dev, &fl, &g_ctx)))
    {
        printf("нет DirectX 11\n");
        return 2;
    }

    // Пакет сервера во временной папке.
    wchar_t tmp[MAX_PATH];
    GetTempPathW(MAX_PATH, tmp);
    const std::wstring root = std::wstring(tmp) + L"flovmp-browser-test";
    CreateDirectoryW(root.c_str(), nullptr);
    CreateDirectoryW((root + L"\\ui").c_str(), nullptr);
    std::ofstream(root + L"\\ui\\index.html") << R"(<!doctype html>
<html><head><meta charset="utf-8"><title>FloV</title><link rel="stylesheet" href="style.css"></head>
<body>
<div id="panel"><button id="btn" style="position:absolute;left:100px;top:100px;width:200px;height:80px">Жми</button>
<input id="name" style="position:absolute;left:100px;top:250px;width:300px;height:40px"></div>
<iframe src="package://ui/frame.html" style="display:none"></iframe>
<script src="app.js"></script>
</body></html>)";
    std::ofstream(root + L"\\ui\\style.css") <<
        "html,body{margin:0;background:transparent}\n"
        "#panel{position:absolute;left:0;top:0;width:50%;height:100%;background:rgb(255,61,138)}\n";
    std::ofstream(root + L"\\ui\\app.js") << R"(
mp.events.add('ping', (n) => mp.trigger('pong', n + 1));
window.addEventListener('message', (e) => mp.trigger('iframeBridge', e.data));
document.getElementById('btn').addEventListener('click', () => mp.trigger('clicked', 'btn'));
document.getElementById('name').addEventListener('input', (e) => mp.trigger('typed', e.target.value));
fetch('package://../secret.txt').then(r => mp.trigger('escape', r.ok ? 'open' : 'closed'), () => mp.trigger('escape', 'closed'));
fetch('package://ui/style.css').then(r => r.text()).then(t => mp.trigger('fetched', t.includes('255,61,138')));
localStorage.setItem('serverSecret', 'first-server');
mp.trigger('loaded', document.title, typeof window.__flovTrigger);
)";
    std::ofstream(root + L"\\ui\\frame.html") << R"(<!doctype html><meta charset="utf-8"><script>
if (typeof window.mp === 'undefined') parent.postMessage('absent', '*');
else mp.trigger('iframeBridge', 'present');
</script>)";
    std::ofstream(root + L"\\ui\\top.html") << R"(<!doctype html><meta charset="utf-8">
<style>html,body{margin:0;background:transparent}button{position:absolute;left:100px;top:100px;width:200px;height:80px}</style>
<button id="top">Верхний</button><script>
top.addEventListener('click', () => mp.trigger('topClicked'));
document.addEventListener('mousemove', () => mp.trigger('topMoved'));
</script>)";
    std::ofstream(root + L"\\ui\\switch.html") << R"(<!doctype html><meta charset="utf-8"><script>
mp.trigger('switched', location.href);
</script>)";
    std::ofstream(std::wstring(tmp) + L"secret.txt") << "secret";
    flov::browser::SetPackageRoot(root);

    printf("Хост и страница:\n");
    Pump();   // SIZE до создания
    const int id = flov::browser::Create("package://ui/index.html");
    Check(id > 0, "браузер создан, хост запущен");
    Check(Until([] { return Seen(flov::browser::Event::Kind::DomReady) != nullptr; }, 20000), "страница из package:// загрузилась (browserDomReady)");
    Check(Until([] { return Seen(flov::browser::Event::Kind::Trigger, "loaded") != nullptr; }, 5000), "mp.trigger из страницы дошёл");
    if (auto* e = Seen(flov::browser::Event::Kind::Trigger, "loaded"))
        Check(e->b == "[\"FloV\",\"undefined\"]", "аргументы mp.trigger JSON; служебная функция из страницы убрана (" + e->b + ")");
    Check(Until([] { return Seen(flov::browser::Event::Kind::Trigger, "iframeBridge") != nullptr; }, 5000) &&
          Seen(flov::browser::Event::Kind::Trigger, "iframeBridge")->b == "[\"absent\"]",
          "iframe не получает игровой window.mp bridge");
    Check(Until([] { return Seen(flov::browser::Event::Kind::Trigger, "fetched") != nullptr; }, 5000) &&
          Seen(flov::browser::Event::Kind::Trigger, "fetched")->b == "[true]", "fetch файла пакета из страницы");
    Check(Until([] { return Seen(flov::browser::Event::Kind::Trigger, "escape") != nullptr; }, 5000) &&
          Seen(flov::browser::Event::Kind::Trigger, "escape")->b == "[\"closed\"]", "package:// не выходит за папку пакета");

    printf("Кадр:\n");
    uint8_t px[4] = {};
    const bool color = Until([&] { return Pixel(id, 50, 50, px) && px[2] == 255 && px[1] == 61 && px[0] == 138; }, 5000);
    Check(color, "цвет страницы в текстуре (BGRA " + std::to_string(px[0]) + "," + std::to_string(px[1]) + "," +
                 std::to_string(px[2]) + "," + std::to_string(px[3]) + ")");
    Check(Pixel(id, 700, 300, px) && px[3] == 0, "прозрачная часть страницы прозрачна — под ней игра");

    printf("События и ввод:\n");
    flov::browser::Call(id, "ping", "[41]");
    Check(Until([] { return Seen(flov::browser::Event::Kind::Trigger, "pong") != nullptr; }, 5000) &&
          Seen(flov::browser::Event::Kind::Trigger, "pong")->b == "[42]", "browser.call → mp.events в странице → mp.trigger");
    flov::browser::SetInput(true);
    const float bx = 200.f / g_w, by = 140.f / g_h;
    flov::browser::Mouse(bx, by, 0, 0);
    flov::browser::Mouse(bx, by, 1, 0);
    flov::browser::Mouse(bx, by, 0, 0);
    Check(Until([] { return Seen(flov::browser::Event::Kind::Trigger, "clicked") != nullptr; }, 5000), "клик мышью по кнопке");
    const float ix = 250.f / g_w, iy = 270.f / g_h;
    flov::browser::Mouse(ix, iy, 1, 0);
    flov::browser::Mouse(ix, iy, 0, 0);
    Sleep(200);
    for (wchar_t c : std::wstring(L"Привет")) flov::browser::Key(WM_CHAR, c, 0);
    Check(Until([] { for (auto& e : g_seen) if (e.a == "typed" && e.b == "[\"Привет\"]") return true; return false; }, 5000),
          "ввод текста с клавиатуры, кириллица");

    printf("Слои и пропуск ввода:\n");
    const int top = flov::browser::Create("package://ui/top.html");
    Check(Until([&] { return Size(top) == g_w; }, 10000), "второй прозрачный слой получил кадр");
    const size_t lowerBefore = SeenCount(flov::browser::Event::Kind::Trigger, "clicked");
    flov::browser::SetOrder(top, 100);
    flov::browser::SetInputEnabled(top, false);
    flov::browser::Mouse(bx, by, 1, 0); flov::browser::Mouse(bx, by, 0, 0);
    Check(Until([&] { return SeenCount(flov::browser::Event::Kind::Trigger, "clicked") > lowerBefore; }, 5000),
          "inputEnabled=false пропускает клик слою ниже");

    flov::browser::SetInputEnabled(top, true);
    const size_t topBefore = SeenCount(flov::browser::Event::Kind::Trigger, "topClicked");
    flov::browser::Mouse(bx, by, 1, 0); flov::browser::Mouse(bx, by, 0, 0);
    Check(Until([&] { return SeenCount(flov::browser::Event::Kind::Trigger, "topClicked") > topBefore; }, 5000),
          "больший orderId получает клик первым");

    flov::browser::SetOrder(top, -100);
    const size_t lowerAfterOrder = SeenCount(flov::browser::Event::Kind::Trigger, "clicked");
    flov::browser::Mouse(bx, by, 1, 0); flov::browser::Mouse(bx, by, 0, 0);
    Check(Until([&] { return SeenCount(flov::browser::Event::Kind::Trigger, "clicked") > lowerAfterOrder; }, 5000),
          "смена orderId меняет hit-test слоёв");

    flov::browser::SetOrder(top, 100);
    const size_t movedBefore = SeenCount(flov::browser::Event::Kind::Trigger, "topMoved");
    flov::browser::Mouse(700.f / g_w, 300.f / g_h, 0, 0);
    for (int i = 0; i < 20; ++i) { Pump(); Sleep(16); }
    Check(SeenCount(flov::browser::Event::Kind::Trigger, "topMoved") == movedBefore,
          "полностью прозрачная точка не перехватывает мышь");
    flov::browser::Destroy(top);

    const int switching = flov::browser::Create("package://ui/index.html");
    flov::browser::SetUrl(switching, "package://ui/switch.html");
    Check(Until([] { return Seen(flov::browser::Event::Kind::Trigger, "switched") != nullptr; }, 10000) &&
          Seen(flov::browser::Event::Kind::Trigger, "switched")->b == "[\"package://ui/switch.html\"]",
          "смена URL сразу после new не теряется до OnAfterCreated");
    flov::browser::Show(switching, false);
    flov::browser::SetFrameRate(switching, 15);
    flov::browser::Destroy(switching);

    printf("Размер и жизнь:\n");
    g_w = 1024; g_h = 768;
    Check(Until([&] { return Size(id) == 1024; }, 5000), "смена разрешения игры — кадр нового размера");
    flov::browser::Execute(id, "mp.trigger('exec', 1 + 1)");
    Check(Until([] { auto* e = Seen(flov::browser::Event::Kind::Trigger, "exec"); return e && e->b == "[2]"; }, 5000), "browser.execute");
    flov::browser::SetUrl(id, "file:///C:/Windows/win.ini");
    Check(Until([] { for (auto& e : g_seen) if (e.kind == flov::browser::Event::Kind::Console && e.b.find("запрещён") != std::string::npos) return true; return false; }, 5000),
          "переход на file:// запрещён");
    const int second = flov::browser::Create("package://ui/missing.html");
    Check(Until([&] { for (auto& e : g_seen) if (e.id == second && (e.kind == flov::browser::Event::Kind::DomReady || e.kind == flov::browser::Event::Kind::LoadFailed)) return true; return false; }, 10000),
          "второй браузер работает рядом с первым");
    flov::browser::DestroyAll();
    Check(!flov::browser::AnyVisible(), "все браузеры закрыты");

    printf("Изоляция серверов:\n");
    const std::wstring root2 = std::wstring(tmp) + L"flovmp-browser-test-second";
    CreateDirectoryW(root2.c_str(), nullptr);
    CreateDirectoryW((root2 + L"\\ui").c_str(), nullptr);
    std::ofstream(root2 + L"\\ui\\index.html") << R"(<!doctype html><meta charset="utf-8"><script>
mp.trigger('storage', localStorage.getItem('serverSecret'));
</script>)";
    flov::browser::SetPackageRoot(root2);
    const int isolated = flov::browser::Create("package://ui/index.html");
    Check(isolated > 0 && Until([] { return Seen(flov::browser::Event::Kind::Trigger, "storage") != nullptr; }, 10000) &&
          Seen(flov::browser::Event::Kind::Trigger, "storage")->b == "[null]",
          "новый server package не наследует localStorage предыдущего сервера");
    flov::browser::DestroyAll();

    printf("\nИтог: пройдено %d, ошибок %d\n", g_passed, g_failed);
    return g_failed == 0 ? 0 : 1;
}
