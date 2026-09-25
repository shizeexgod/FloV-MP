#pragma once
// Браузеры клиентского кода сервера (пункт 26b): mp.browsers.new(url), как в
// RAGE:MP. Chromium работает в отдельном процессе flovmp-cef.exe (см.
// browser_ipc.h), здесь — запуск хоста, команды, кадры в текстуры DirectX и
// ввод. Потоки: команды и события — игровой поток, рисование — поток Present,
// клавиатура — поток окна; общее состояние под своим мьютексом.

#include <cstdint>
#include <string>
#include <vector>

namespace flov::browser
{
    /// Путь к flovmp-cef.exe. По умолчанию — папка FloVMP\cef рядом с FloVMP.asi.
    void SetHostExe(const std::wstring& path);
    bool Available();

    /// Новый браузер; 0 — хоста нет. Видим сразу, как в RAGE:MP.
    int Create(const std::string& url);
    void Destroy(int id);
    void DestroyAll();
    /// Закрыть всё и хост (выгрузка клиента).
    void Shutdown();
    void SetUrl(int id, const std::string& url);
    void Execute(int id, const std::string& code);
    void Call(int id, const std::string& name, const std::string& argsJson);
    void Show(int id, bool visible);
    /// Разрешить странице получать мышь/клавиатуру. Отключённая страница
    /// остаётся видимой, но ввод проходит к слою ниже либо обратно в игру.
    void SetInputEnabled(int id, bool enabled);
    /// Порядок наложения: больше — выше. При равенстве выше созданный позже.
    void SetOrder(int id, int order);
    /// Частота перерисовки CEF; 60 для HUD, ниже для статичных меню.
    void SetFrameRate(int id, int frameRate);
    void Reload(int id, bool ignoreCache);

    struct Stats
    {
        int count = 0;
        int visible = 0;
        int maxBrowsers = 0;
        int screenWidth = 0, screenHeight = 0;
        int renderWidth = 0, renderHeight = 0;
        uint64_t pixels = 0;
        uint64_t estimatedBytes = 0;
        uint64_t uploadedFrames = 0;
        uint64_t droppedFrames = 0;
        uint64_t uploadMicros = 0;
    };
    /// Снимок нагрузки CEF. estimatedBytes — приблизительная цена клиентской
    /// части pipeline, а не точный working set всех Chromium subprocesses.
    Stats GetStats();
    int MaxCount();

    /// Папка скачанных client_packages — для адресов package://.
    void SetPackageRoot(const std::wstring& dir);

    struct Event
    {
        enum class Kind { DomReady, LoadFailed, Trigger, Console, HostLost, HostRestored } kind;
        int id = 0;
        std::string a, b;   // DomReady: url; LoadFailed: код, url; Trigger: имя, JSON; Console: уровень, текст
    };
    std::vector<Event> TakeEvents();

    bool AnyVisible();

    /// Ввод, пока скрипт показал курсор. Координаты — доли экрана 0..1,
    /// кнопки битами: 1 левая, 2 правая, 4 средняя.
    void SetInput(bool enabled);
    void Mouse(float nx, float ny, int buttons, int wheel);
    /// Из оконной процедуры: true — клавиша ушла в браузер, игре её не отдавать.
    bool Key(unsigned msg, uintptr_t wp, intptr_t lp);

    /// Из Present: обновить текстуры и положить браузеры под интерфейс платформы.
    void Render(void* device, void* context, void* drawList, float width, float height);

    /// Для проверки без GTA: текстура браузера (ID3D11Texture2D*), nullptr — кадра ещё нет.
    void* TextureOf(int id);

#ifdef FLOVMP_BROWSER_TEST
    /// Только для fault-injection интеграционного теста: имитирует падение CEF.
    bool CrashHostForTest();
#endif
}
