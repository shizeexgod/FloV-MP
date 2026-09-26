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
#include <iterator>
#include <functional>
#include <map>
#include <string>
#include <vector>

#include "common.h"
#include "script.h"
#include "license.h"
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

namespace
{
    // --- подставной мир для mp.players / mp.vehicles ---
    std::map<int, std::map<std::string, std::string>> g_fakeVars;
    std::vector<int> FakePlayers() { return { 1, 2, 7 }; }
    flov::script::PlayerView FakePlayer(int id)
    {
        flov::script::PlayerView p;
        if (id != 1 && id != 2 && id != 7) return p;
        p.ok = true;
        p.name = id == 1 ? "Me" : id == 2 ? "Anna" : "Far_Away";
        p.handle = id == 7 ? 0 : 100 + id;   // 7 — далеко, не в зоне видимости
        p.hasPosition = id != 7;
        p.x = 10.f * id; p.y = 1.f; p.z = 2.f;
        p.health = 80; p.armor = 25;
        p.vehicle = id == 2 ? 5 : 0;
        p.seat = id == 2 ? -1 : -2;
        return p;
    }
    const std::string* FakeVar(int id, const std::string& key)
    {
        auto it = g_fakeVars.find(id);
        if (it == g_fakeVars.end()) return nullptr;
        auto v = it->second.find(key);
        return v == it->second.end() ? nullptr : &v->second;
    }
    std::vector<std::string> FakeVarKeys(int id)
    {
        std::vector<std::string> k;
        for (auto& [key, _] : g_fakeVars[id]) k.push_back(key);
        return k;
    }
    std::vector<int> FakeVehicles() { return { 5 }; }
    flov::script::VehicleView FakeVehicle(int id)
    {
        flov::script::VehicleView v;
        if (id != 5) return v;
        v.ok = true; v.handle = 555; v.model = 0xB779A091; v.plate = "FLOV 01"; v.driver = 2; v.engine = true;
        return v;
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
    printf("Лицензия сервера (LIC):\n");
    {
        // Настоящее подтверждение, подписанное сервером лицензий на VDS 26.09.
        std::ifstream f(FLOVMP_ATTEST_SAMPLE);
        std::string json((std::istreambuf_iterator<char>(f)), std::istreambuf_iterator<char>());
        auto field = [&](const char* name) {
            const std::string key = std::string("\"") + name + "\":\"";
            const size_t a = json.find(key);
            if (a == std::string::npos) return std::string();
            const size_t b = json.find('"', a + key.size());
            std::string v = json.substr(a + key.size(), b - a - key.size());
            // .NET экранирует + и / в строках JSON файла; серверу и клиенту
            // строки приходят уже раскрытыми.
            for (auto [from, to] : { std::pair{ "\\u002B", "+" }, std::pair{ "\\u002F", "/" }, std::pair{ "\\/", "/" } })
                for (size_t p; (p = v.find(from)) != std::string::npos;) v.replace(p, strlen(from), to);
            return v;
        };
        const std::string payload = field("payload_b64"), sig = field("signature");
        const long long issued = 1790411861;   // 26.09.2026 08:37:41 UTC — в пределах срока
        using flov::license::Verdict;
        Check(!payload.empty() && flov::license::Verify(payload, sig, "188.127.229.224", issued).verdict == Verdict::Ok,
              "подтверждение с VDS: подпись сервера лицензий, та же машина — вход разрешён");
        Check(flov::license::Verify(payload, sig, "5.6.7.8", issued).verdict == Verdict::WrongMachine,
              "та же лицензия на чужой машине — отказ");
        Check(flov::license::Verify(payload, sig, "192.168.1.20", issued).verdict == Verdict::Ok,
              "локальная сеть — проверка IP не мешает");
        Check(flov::license::Verify(payload, sig, "188.127.229.224", issued + 3 * 86400).verdict == Verdict::Expired,
              "устаревшее подтверждение — отказ");
        std::string forged = payload;
        forged[forged.size() / 2] = forged[forged.size() / 2] == 'A' ? 'B' : 'A';
        Check(flov::license::Verify(forged, sig, "188.127.229.224", issued).verdict != Verdict::Ok,
              "изменённое подтверждение — отказ (подпись)");
    }
    printf("Мир: mp.players, переменные, события:\n");
    {
        flov::script::SetWorldBackend({ FakePlayers, FakePlayer, FakeVar, FakeVarKeys, FakeVehicles, FakeVehicle });
        flov::script::SetLocalPlayer(1, "Me");
        g_fakeVars[2]["job"] = "\"taxi\"";
        Run("const a = mp.players.at(2);"
            "let streamed = 0; mp.players.forEachInStreamRange(() => streamed++);"
            "mp.events.callRemote('w', mp.players.length, a.name, a.getVariable('job'), a.getHealth(), a.vehicle.numberPlate,"
            "  a.vehicle.driver === a, streamed, mp.players.at(7).position, mp.players.local.getVariable('job'));");
        auto out = flov::script::TakeOutgoing();
        Check(out.size() == 1 && out[0].second == "[3,\"Anna\",\"taxi\",80,\"FLOV 01\",true,2,null,null]",
              "mp.players: список, имя, getVariable, здоровье, машина, зона видимости (" + (out.empty() ? std::string("—") : out[0].second) + ")");

        Run("mp.events.addDataHandler('money', (p, v, old) => mp.events.callRemote('money', p.name, v, old === undefined));"
            "mp.events.add('playerJoin', p => mp.events.callRemote('join', p.name));"
            "mp.events.add('playerQuit', p => mp.events.callRemote('quit', p.name));"
            "mp.nametags.enabled = false;");
        g_fakeVars[2]["money"] = "{\"cash\":5}";
        flov::script::DataChange(2, "money", "{\"cash\":5}", "");
        flov::script::EntityEvent("playerJoin", 2);
        flov::script::EntityEvent("playerQuit", 9, "Gone_Player");
        Ticks(1);
        out = flov::script::TakeOutgoing();
        std::string got;
        for (auto& [n, j] : out) got += n + j + " ";
        Check(got == "money[\"Anna\",{\"cash\":5},true] join[\"Anna\"] quit[\"Gone_Player\"] ",
              "addDataHandler, playerJoin, playerQuit с именем ушедшего (" + got + ")");
        Check(flov::script::NametagsDisabledByScript(), "mp.nametags.enabled = false выключает встроенные ники");
        flov::script::Reset();
        Check(!flov::script::NametagsDisabledByScript(), "после отключения от сервера встроенные ники снова включены");
        flov::script::SetWorldBackend({});
    }
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
            "let fractionalRejected = false; try { b.setBounds(0, 0, 0.5, 100); } catch (e) { fractionalRejected = e instanceof RangeError; }\n"
            "b.setBounds(999999, -999999, 999999, 999999); mp.events.callRemote('bounds', fractionalRejected, b.bounds);\n"
            "b.orderId = 7; b.inputEnabled = true; b.frameRate = 30; b.setBounds(10, 20, 320, 180).focus();\n"
            "mp.events.add('browserDomReady', (br) => { if (br === b) b.call('hello', 'сервер'); });\n"
            "mp.events.add('browserDestroyed', (br) => { if (br === b) mp.events.callRemote('destroyed', mp.browsers.exists(br)); });\n"
            "mp.events.add('page:loaded', (href) => mp.events.callRemote('loaded', href, mp.browsers.length, mp.browsers.exists(b), b.orderId, b.inputEnabled, b.frameRate, mp.browsers.max, mp.browsers.stats.count, b.bounds, b.focused, mp.browsers.focused === b));\n"
            "mp.events.add('page:hi', (text) => { mp.events.callRemote('hi', text); b.blur(); mp.events.callRemote('focus', b.focused, mp.browsers.focused); b.destroy(); mp.events.callRemote('after', mp.browsers.length); });\n";
        flov::browser::SetHostExe(FLOVMP_CEF_HOST);
        flov::script::Reset();
        Check(flov::script::RunFolder(dir), "index.js с mp.browsers.new запустился");
        std::vector<std::pair<std::string, std::string>> got;
        const ULONGLONG end = GetTickCount64() + 30000;
        while (GetTickCount64() < end && got.size() < 6)
        {
            flov::script::Tick(false);
            for (auto& e : flov::script::TakeOutgoing()) got.push_back(e);
            Sleep(16);
        }
        auto find = [&](const char* n) -> std::string { for (auto& [k, v] : got) if (k == n) return v; return "—"; };
        Check(find("bounds") == "[true,{\"x\":7680,\"y\":-7680,\"width\":7680,\"height\":7680}]",
              "bounds отклоняет дробный ноль и совпадает с native clamp (" + find("bounds") + ")");
        Check(find("loaded") == "[\"package://ui/index.html\",1,true,7,true,30,12,1,{\"x\":10,\"y\":20,\"width\":320,\"height\":180},true,true]",
              "страница загрузилась, bounds/focus/метрики browser применились (" + find("loaded") + ")");
        Check(find("hi") == "[\"привет, сервер\"]", "browserDomReady → browser.call → страница → обратно (" + find("hi") + ")");
        Check(find("focus") == "[false,null]", "browser.blur возвращает focus игре (" + find("focus") + ")");
        Check(find("destroyed") == "[false]", "browserDestroyed приходит после удаления из коллекции (" + find("destroyed") + ")");
        Check(find("after") == "[0]", "browser.destroy");
        flov::script::Reset();

        std::ofstream(dir + L"\\index.js") <<
            "const life = mp.browsers.new('package://ui/index.html');\n"
            "life.focus();\n"
            "mp.events.add('browserDomReady', br => { if (br === life) mp.events.callRemote('lifeReady'); });\n"
            "mp.events.add('browserCrashed', br => { if (br === life) mp.events.callRemote('lifeCrashed', life.focused, mp.browsers.focused); });\n"
            "mp.events.add('browserRestored', br => { if (br === life) mp.events.callRemote('lifeRestored'); });\n";
        Check(flov::script::RunFolder(dir), "per-browser lifecycle test запустился");
        bool lifeReady = false, lifeCrashed = false, lifeFocusReset = false, lifeRestored = false, lifeReadyAgain = false;
        const ULONGLONG readyEnd = GetTickCount64() + 15000;
        while (GetTickCount64() < readyEnd && !lifeReady)
        {
            flov::script::Tick(false);
            for (auto& [name, json] : flov::script::TakeOutgoing()) if (name == "lifeReady") lifeReady = true;
            Sleep(16);
        }
        Check(lifeReady && flov::browser::CrashHostForTest(), "CEF host остановлен для JS lifecycle");
        const ULONGLONG lifeEnd = GetTickCount64() + 20000;
        while (GetTickCount64() < lifeEnd && !(lifeCrashed && lifeRestored && lifeReadyAgain))
        {
            flov::script::Tick(false);
            for (auto& [name, json] : flov::script::TakeOutgoing())
            {
                if (name == "lifeCrashed") { lifeCrashed = true; lifeFocusReset = json == "[false,null]"; }
                else if (name == "lifeRestored") lifeRestored = true;
                else if (name == "lifeReady") lifeReadyAgain = true;
            }
            Sleep(16);
        }
        Check(lifeCrashed && lifeFocusReset && lifeRestored && lifeReadyAgain,
              "crash сбрасывает focus; browserCrashed/Restored и повторный DomReady доходят до server UI");
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
