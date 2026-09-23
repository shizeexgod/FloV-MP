#pragma once
#include <windows.h>
#include <cstdint>
#include <string>
#include <utility>
#include <vector>

namespace flov::ui
{
    /// Ник над игроком. Координаты — доли экрана 0..1 (точка над головой).
    struct Label
    {
        float x = 0, y = 0;
        std::string name;          // уже с пробелом вместо «_», если так настроено
        int id = -1;               // <0 — не показывать ID
        float health = -1;         // 0..1, <0 — без полоски
        float armor = -1;          // 0..1, <=0 — без полоски
        bool speaking = false;
        int adminLevel = 0;        // >0 и включено в настройках — метка ADMIN
        std::string extra;         // ESP: дистанция и т.п.
        uint32_t rgb = 0xFFFFFF;
        uint32_t healthColor = 0x4ADE80;
        uint32_t healthLowColor = 0xF87171;
        uint32_t armorColor = 0x60A5FA;
        float barWidth = 74.f;      // пиксели до масштабирования интерфейса
        float barHeight = 5.f;
        float scale = 1.f;         // уменьшение вдали
        float alpha = 1.f;         // затухание вдали
    };

    struct Command { std::string name, desc; };

    struct Stats
    {
        float fps = 0, frameMs = 0;
        int ping = 0, streamed = 0, online = 0;
        std::string server, endpoint;
        bool connected = false;
        uint64_t bytesIn = 0, bytesOut = 0;   // байт в секунду
        std::string state;                     // «в игре», «подключение…»
        std::vector<std::string> entities;     // вкладка «Сущности»: строка на игрока
    };

    void Init();
    void Shutdown();

    // --- чат ---------------------------------------------------------------
    /// Строка чата: author (может быть пустым) и текст с цветами {rrggbb}.
    void AddChat(const std::string& textWithColors, const std::string& author = "", uint32_t authorRgb = 0xFFFFFF);
    void ClearChat();
    void SetChatEnabled(bool enabled);
    void SetCommands(std::vector<Command> commands);
    std::vector<std::string> TakeSubmittedChat();

    // --- консоль F8 -----------------------------------------------------------
    void ConsoleLog(const std::string& tag, const std::string& text);
    void SetConsoleEnabled(bool enabled);
    void SetAdminLevel(int level);
    bool ConsoleOpen();
    /// Команды, набранные в консоли (без «/»), и кнопки вкладки «Инструменты».
    std::vector<std::string> TakeConsoleCommands();
    void SetStats(const Stats& stats);
    void SetNetgraph(bool on);
    /// Тема консоли: obsidian, slate, glass (настройка console.theme).
    void SetConsoleTheme(const std::string& name);
    bool Netgraph();

    /// Курсор мыши для консоли (игровой поток читает управление GTA).
    void SetCursor(float nx, float ny, bool left, int wheel);

    // --- мир и HUD --------------------------------------------------------------
    void SetLabels(std::vector<Label>&& labels);
    void SetWatermark(const std::string& text);
    void SetMicIndicator(int state); // 0 — нет, 1 — говорю, 2 — нет микрофона
    void Notify(const std::string& text, int ms = 4000);
    void SetAccent(uint32_t rgb);
    /// Название платформы на загрузочном экране и в консоли (branding.name).
    void SetBrand(const std::string& name);

    // --- меню сервера ---------------------------------------------------------------
    struct MenuItem { std::string label, desc; };
    /// Меню слева: стрелки — выбор, Enter — выбрать, Esc/Backspace — закрыть.
    void OpenMenu(const std::string& id, const std::string& title, std::vector<MenuItem> items);
    /// Игрок нажал Esc, когда ничего не открыто: игровой поток соберёт и
    /// покажет наше меню (штатное меню GTA останавливает мир и скрипты).
    bool TakeEscRequest();
    /// Владелец сервера может вернуть штатное меню GTA (hud.pause_menu = on);
    /// тогда Esc уходит игре как раньше, вместе с её паузой.
    void SetEscMenu(bool ours);
    /// Открыто ли сейчас штатное меню GTA (его можно открыть из нашего меню
    /// пунктом «Настройки игры»). Пока оно открыто, Esc отдаём игре, иначе
    /// закрыть её меню нечем.
    void SetGtaMenuOpen(bool open);
    void CloseMenu();
    /// Выбор (index >= 0) или закрытие меню игроком (index = -1).
    struct MenuEvent { std::string id; int index; };
    std::vector<MenuEvent> TakeMenuEvents();

    // --- загрузочный экран -----------------------------------------------------------
    /// Экран от подключения до появления в мире: вместо «зависшей» игры видно шаг.
    void ShowLoading(const std::string& title);
    /// percent < 0 — неизвестный прогресс (бегущая полоса).
    void LoadingStep(const std::string& text, float percent = -1.f);
    void HideLoading();
    bool LoadingVisible();
    /// Подсказки внизу загрузочного экрана ({rrggbb} — цвет) и цвет акцента.
    void SetLoadingStyle(std::vector<std::string> tips, uint32_t accent);

    // --- окно игры ------------------------------------------------------------------
    /// Заголовок окна (панель задач, Alt+Tab, диспетчер задач) и значок FloV:MP.
    void SetWindowTitle(const std::string& title);

    // --- ввод ---------------------------------------------------------------------
    /// Клавиши, о нажатии которых сообщать игре (вне чата и консоли).
    void SetHotkeys(std::vector<int> vks);
    std::vector<int> TakeHotkeys();
    void OpenConnectDialog(const std::string& host, const std::string& name);
    bool TakeConnectRequest(std::string& host, std::string& name);

    /// Открыт ввод (чат, консоль, окно подключения) — игре выключить управление.
    bool InputActive();
    uint32_t MsSinceInputClosed();
    /// Клавиши чата и консоли (настройки сервера keys.chat / keys.console).
    void SetInputKeys(int chatVk, int consoleVk);

    enum Hotkey : int { KeyConnect = VK_F9 };
}
