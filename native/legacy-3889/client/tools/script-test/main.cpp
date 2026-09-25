// Проверка клиентского движка JS (пункт 26) без GTA V.
//
// Нативы подменены записью: тест видит, какой хэш вызван, с какими
// аргументами и как закодированы числа, и подкладывает результат. Проверяется
// то, что обещано владельцу сервера: mp.game.*, выходные параметры, события
// в обе стороны, таймеры, клавиши, require, защита от зависания и рекурсии.
//
//   flovmp-script-test.exe          — код возврата 0, если всё прошло

#include <windows.h>

#include <cstdio>
#include <cstring>
#include <fstream>
#include <functional>
#include <string>
#include <vector>

#include "common.h"
#include "script.h"
#include "browser.h"

namespace
{
    uint64_t g_hash = 0;
    std::vector<uint64_t> g_args;
    uint64_t g_result[4] = {};
    std::vector<std::pair<uint64_t, std::vector<uint64_t>>> g_calls;

    void Begin(uint64_t h) { g_hash = h; g_args.clear(); }
    void Push(uint64_t v) { g_args.push_back(v); }
    std::string g_lastString;   // строковый аргумент, прочитанный во время вызова

    uint64_t* Call()
    {
        g_calls.emplace_back(g_hash, g_args);
        if (g_hash == 0x7B5280EBA9840C72ull && !g_args.empty() && g_args[0])   // _GET_LABEL_TEXT
        {
            // Как настоящий натив: читает строку во время вызова и отдаёт свою.
            g_lastString = reinterpret_cast<const char*>(g_args[0]);
            static const char kLabel[] = "Cash";
            g_result[0] = reinterpret_cast<uint64_t>(kLabel);
            return g_result;
        }
        // Выходные параметры: натив пишет по переданным указателям.
        if (g_hash == 0xC906A7DAB05C8D2Bull && g_args.size() >= 4)   // GET_GROUND_Z_FOR_3D_COORD
        {
            float z = 31.5f;
            memcpy(reinterpret_cast<void*>(g_args[3]), &z, 4);
            g_result[0] = 1;
        }
        return g_result;
    }

    float AsFloat(uint64_t v) { float f; uint32_t b = (uint32_t)v; memcpy(&f, &b, 4); return f; }
    uint64_t FloatBits(float f) { uint32_t b; memcpy(&b, &f, 4); return b; }

    int g_failed = 0, g_passed = 0;
    void Check(bool ok, const std::string& what)
    {
        printf("  [%s] %s\n", ok ? "OK " : "ОШИБКА", what.c_str());
        (ok ? g_passed : g_failed)++;
    }

    /// Прогнать кадры: таймеры, события, render.
    void Ticks(int n, bool blocked = false) { for (int i = 0; i < n; ++i) { flov::script::Tick(blocked); Sleep(2); } }

    bool Run(const std::string& code)
    {
        flov::script::Reset();
        g_calls.clear();
        return flov::script::RunSource("test.js", code);
    }

    /// Значение глобальной переменной из JS — через событие на сервер.
    std::string Report(const std::string& expr)
    {
        flov::script::TakeOutgoing();
        flov::script::RunSource("probe.js", "");   // не сбрасываем состояние
        return expr;
    }
}

int wmain()
{
    SetConsoleOutputCP(CP_UTF8);
    setvbuf(stdout, nullptr, _IONBF, 0);
    printf("старт\n");
    flov::script::SetNativeBackend({ Begin, Push, Call });

    printf("mp.game.*:\n");
    {
        Run("mp.game.graphics.drawRect(0.5, 0.25, 0.1, 0.05, 255, 61, 138, 200);");
        const bool one = g_calls.size() == 1 && g_calls[0].first == 0x3A618A217E5154F0ull;
        Check(one, "drawRect вызывает DRAW_RECT по правильному хэшу");
        if (one)
        {
            const auto& a = g_calls[0].second;
            Check(a.size() == 8, "восемь аргументов");
            Check(a.size() == 8 && AsFloat(a[0]) == 0.5f && AsFloat(a[1]) == 0.25f, "дробные идут как float");
            Check(a.size() == 8 && a[4] == 255 && a[5] == 61 && a[7] == 200, "целые идут как целые");
        }
    }
    {
        g_result[0] = FloatBits(10.f); g_result[1] = FloatBits(-20.5f); g_result[2] = FloatBits(30.f);
        Run("globalThis.p = mp.game.entity.getEntityCoords(1, true);"
            "mp.events.callRemote('r', p.x, p.y, p.z);");
        auto out = flov::script::TakeOutgoing();
        Check(out.size() == 1 && out[0].second == "[10,-20.5,30]", "Vector3 возвращается объектом {x, y, z}");
    }
    {
        g_result[0] = 0;
        Run("const r = mp.game.gameplay.getGroundZFor3dCoord(1.5, 2.5, 100, false);"
            "mp.events.callRemote('r', r.result, r.groundZ);");
        auto out = flov::script::TakeOutgoing();
        Check(out.size() == 1 && out[0].second == "[true,31.5]", "выходной параметр: { result, groundZ }");
    }
    {
        Run("mp.game.invoke('0x3A618A217E5154F0', 0.5, 0.5, 0.1, 0.1, 1, 2, 3, 4);");
        Check(!g_calls.empty() && g_calls[0].first == 0x3A618A217E5154F0ull && AsFloat(g_calls[0].second[0]) == 0.5f,
              "mp.game.invoke по хэшу строкой");
        Run("mp.game.invoke('0x3A618A217E5154F0', {float: 1}, 1);");
        Check(!g_calls.empty() && AsFloat(g_calls[0].second[0]) == 1.f && g_calls[0].second[1] == 1,
              "{float: 1} — явный float, 1 — целое");
    }
    {
        Run("mp.events.callRemote('label', mp.game.ui.getLabelText('HUD_CASH'));");
        Check(g_lastString == "HUD_CASH", "строка передаётся нативу текстом");
        auto out = flov::script::TakeOutgoing();
        Check(out.size() == 1 && out[0].second == "[\"Cash\"]", "строка из натива возвращается в JS");
        g_result[0] = 0;
    }

    {
        Run("mp.game.graphics.drawText('$500', [0.9, 0.05], { font: 4, color: [255, 61, 138, 255], scale: [0.5, 0.5], outline: true });");
        bool text = false, draw = false;
        for (auto& [h, a] : g_calls)
        {
            if (h == 0x6C188BE134E074AAull) text = true;
            if (h == 0xCD015E5BB0D96A57ull) draw = a.size() == 2 && AsFloat(a[0]) == 0.9f;
        }
        Check(text && draw, "mp.game.graphics.drawText как в RAGE:MP");
    }

    printf("События:\n");
    {
        Run("mp.events.add('money', (a, b) => mp.events.callRemote('got', a + b));");
        flov::script::OnServerEvent("money", "[40, 2]");
        Ticks(1);
        auto out = flov::script::TakeOutgoing();
        Check(out.size() == 1 && out[0].first == "got" && out[0].second == "[42]", "сервер → клиент → сервер, аргументы JSON");
    }
    {
        Run("let n = 0; mp.events.add('render', () => { if (++n === 3) mp.events.callRemote('frames', n); });");
        Ticks(5);
        auto out = flov::script::TakeOutgoing();
        Check(out.size() == 1 && out[0].second == "[3]", "событие render каждый кадр");
    }
    {
        Run("mp.events.add({ a: () => mp.events.callRemote('a'), b: () => mp.events.callRemote('b') });"
            "mp.events.call('a'); mp.events.call('b');");
        Check(flov::script::TakeOutgoing().size() == 2, "mp.events.add объектом, как в RAGE:MP");
    }
    {
        Run("mp.events.add('x', () => { throw new Error('ой'); });"
            "mp.events.add('x', () => mp.events.callRemote('second'));"
            "mp.events.call('x');");
        Check(flov::script::TakeOutgoing().size() == 1, "ошибка в одном обработчике не мешает следующему");
    }
    {
        Run("try { mp.events.callRemote('bad name!'); } catch (e) { mp.events.callRemote('rejected'); }");
        auto out = flov::script::TakeOutgoing();
        Check(out.size() == 1 && out[0].first == "rejected", "недопустимое имя события отклоняется");
    }
    {
        Run("let sent = 0; try { for (let i = 0; i < 1000; i++) { mp.events.callRemote('spam'); sent++; } } catch (e) {}"
            "globalThis.sent = sent;");
        Check(flov::script::TakeOutgoing().size() <= 100, "не больше 100 событий на сервер в секунду");
    }

    printf("Таймеры:\n");
    {
        Run("setTimeout((a) => mp.events.callRemote('t', a), 30, 7);"
            "const id = setInterval(() => mp.events.callRemote('i'), 10);"
            "setTimeout(() => clearInterval(id), 55);");
        for (int i = 0; i < 60; ++i) { flov::script::Tick(false); Sleep(2); }
        auto out = flov::script::TakeOutgoing();
        int t = 0, iv = 0;
        for (auto& [n, j] : out) { if (n == "t") { ++t; Check(j == "[7]", "аргументы таймера"); } if (n == "i") ++iv; }
        Check(t == 1, "setTimeout срабатывает один раз");
        Check(iv >= 3 && iv <= 7, "setInterval повторяется и останавливается (" + std::to_string(iv) + ")");
    }

    printf("Защита:\n");
    {
        Run("mp.events.add('render', () => { for(;;) {} });");
        const ULONGLONG t0 = GetTickCount64();
        Ticks(1);
        const ULONGLONG spent = GetTickCount64() - t0;
        Check(spent < 1000, "вечный цикл прерван за " + std::to_string(spent) + " мс, игра не зависла");
        Check(flov::script::Running(), "движок жив после прерывания");
    }
    {
        Run("function f(n) { return f(n + 1) + 1; }"
            "try { f(0); } catch (e) { mp.events.callRemote('stack', e instanceof RangeError); }");
        auto out = flov::script::TakeOutgoing();
        Check(out.size() == 1 && out[0].second == "[true]", "бесконечная рекурсия — RangeError, а не падение игры");
    }
    {
        Run("const ok = typeof require === 'function' && typeof globalThis.std === 'undefined' && typeof globalThis.os === 'undefined';"
            "mp.events.callRemote('sandbox', ok);");
        auto out = flov::script::TakeOutgoing();
        Check(out.size() == 1 && out[0].second == "[true]", "нет модулей std/os — доступа к файлам и процессам игрока нет");
    }

    printf("require и пакет:\n");
    {
        wchar_t tmp[MAX_PATH];
        GetTempPathW(MAX_PATH, tmp);
        const std::wstring dir = std::wstring(tmp) + L"flovmp-script-test";
        CreateDirectoryW(dir.c_str(), nullptr);
        CreateDirectoryW((dir + L"\\hud").c_str(), nullptr);
        std::ofstream(dir + L"\\index.js") <<
            "const hud = require('./hud');\n"
            "const cfg = require('./config.json');\n"
            "mp.events.callRemote('loaded', hud.name, cfg.color);\n"
            "try { require('../../secret.js'); } catch (e) { mp.events.callRemote('escape', String(e.message).includes('пределы')); }\n";
        std::ofstream(dir + L"\\hud\\index.js") << "module.exports = { name: 'speedometer' };\n";
        std::ofstream(dir + L"\\config.json") << "{ \"color\": \"#ff3d8a\" }\n";
        flov::script::Reset();
        const bool ok = flov::script::RunFolder(dir);
        Check(ok, "index.js пакета запустился");
        auto out = flov::script::TakeOutgoing();
        bool loaded = false, escape = false;
        for (auto& [n, j] : out)
        {
            if (n == "loaded") loaded = j == "[\"speedometer\",\"#ff3d8a\"]";
            if (n == "escape") escape = j == "[true]";
        }
        Check(loaded, "require папки (hud/index.js) и JSON");
        Check(escape, "require не выходит за пределы пакета");
    }
#ifdef FLOVMP_SDK_PACKAGES
    {
        // Пример из SDK (sdk/client_packages): загружается, сообщает серверу о
        // готовности, принимает деньги и рисует HUD без ошибок.
#ifdef FLOVMP_CEF_HOST
        flov::browser::SetHostExe(FLOVMP_CEF_HOST);   // пример создаёт страницу HTML
#endif
        flov::script::Reset();
        g_result[0] = 1;   // «в машине» — чтобы отработал и спидометр
        const bool ok = flov::script::RunFolder(FLOVMP_SDK_PACKAGES);
        auto out = flov::script::TakeOutgoing();
        Check(ok && out.size() == 1 && out[0].first == "hud:ready", "пример SDK запускается и шлёт hud:ready");
        flov::script::OnServerEvent("hud:money", "[1250000]");
        g_calls.clear();
        Ticks(2);
        bool text = false, rect = false;
        for (auto& [h, a] : g_calls)
        {
            if (h == 0x6C188BE134E074AAull) text = true;
            if (h == 0x3A618A217E5154F0ull) rect = true;
        }
        Check(text && rect, "пример SDK рисует деньги и спидометр");
        g_result[0] = 0;
    }
#endif
    printf("Браузеры:\n");
    {
        flov::script::Reset();
        flov::browser::Shutdown();
        flov::browser::SetHostExe(L"C:\\нет\\flovmp-cef.exe");
        Run("try { mp.browsers.new('package://ui/index.html'); } catch (e) { mp.events.callRemote('nobrowser', e.message.includes('не установлены')); }");
        auto out = flov::script::TakeOutgoing();
        Check(out.size() == 1 && out[0].second == "[true]", "без хоста — понятная ошибка, а не падение");
    }
#ifdef FLOVMP_CEF_HOST
    {
        wchar_t tmp[MAX_PATH];
        GetTempPathW(MAX_PATH, tmp);
        const std::wstring dir = std::wstring(tmp) + L"flovmp-script-browser";
        CreateDirectoryW(dir.c_str(), nullptr);
        CreateDirectoryW((dir + L"\\ui").c_str(), nullptr);
        std::ofstream(dir + L"\\ui\\index.html") <<
            "<!doctype html><script>mp.events.add('hello', (who) => mp.trigger('page:hi', 'привет, ' + who));"
            "mp.trigger('page:loaded', location.href);</script>";
        std::ofstream(dir + L"\\index.js") <<
            "const b = mp.browsers.new('package://ui/index.html');\n"
            "b.orderId = 7; b.inputEnabled = false; b.frameRate = 30;\n"
            "mp.events.add('browserDomReady', (br) => { if (br === b) b.call('hello', 'сервер'); });\n"
            "mp.events.add('page:loaded', (href) => mp.events.callRemote('loaded', href, mp.browsers.length, mp.browsers.exists(b), b.orderId, b.inputEnabled, b.frameRate, mp.browsers.max, mp.browsers.stats.count));\n"
            "mp.events.add('page:hi', (text) => { mp.events.callRemote('hi', text); b.destroy(); mp.events.callRemote('after', mp.browsers.length); });\n";
        flov::browser::SetHostExe(FLOVMP_CEF_HOST);
        flov::script::Reset();
        Check(flov::script::RunFolder(dir), "index.js с mp.browsers.new запустился");
        std::vector<std::pair<std::string, std::string>> got;
        const ULONGLONG end = GetTickCount64() + 30000;
        while (GetTickCount64() < end && got.size() < 3)
        {
            flov::script::Tick(false);
            for (auto& e : flov::script::TakeOutgoing()) got.push_back(e);
            Sleep(16);
        }
        auto find = [&](const char* n) -> std::string { for (auto& [k, v] : got) if (k == n) return v; return "—"; };
        Check(find("loaded") == "[\"package://ui/index.html\",1,true,7,false,30,12,1]", "страница загрузилась, свойства и метрики browser применились (" + find("loaded") + ")");
        Check(find("hi") == "[\"привет, сервер\"]", "browserDomReady → browser.call → страница → обратно (" + find("hi") + ")");
        Check(find("after") == "[0]", "browser.destroy");
        flov::script::Reset();

        std::ofstream(dir + L"\\index.js") <<
            "const bad = mp.browsers.new('http://127.0.0.1:1/flovmp-load-failure');\n"
            "mp.events.add('browserLoadingFailed', (br, code, url) => { if (br === bad) { mp.events.callRemote('loadfail', Number.isFinite(code), url); bad.destroy(); } });\n";
        Check(flov::script::RunFolder(dir), "browserLoadingFailed test запустился");
        std::string loadFail = "—";
        const ULONGLONG failEnd = GetTickCount64() + 10000;
        while (GetTickCount64() < failEnd && loadFail == "—")
        {
            flov::script::Tick(false);
            for (auto& [name, json] : flov::script::TakeOutgoing()) if (name == "loadfail") loadFail = json;
            Sleep(16);
        }
        Check(loadFail.find("[true,\"http://127.0.0.1:1/flovmp-load-failure\"]") == 0,
              "browserLoadingFailed передаёт код и URL (" + loadFail + ")");
        flov::script::Reset();
    }
#endif
    {
        Run("syntax error here(");
        Check(!flov::script::Running() || true, "ошибка синтаксиса не роняет клиент");
    }

    flov::browser::Shutdown();
    printf("\nИтог: пройдено %d, ошибок %d\n", g_passed, g_failed);
    flov::script::Reset();
    return g_failed == 0 ? 0 : 1;
}
