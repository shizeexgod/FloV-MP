#pragma once
#include <windows.h>
#include <cstdint>
#include <string>
#include <vector>

namespace flov::ui
{
    /// Подпись на экране (ник над игроком, ESP). Координаты — доли экрана 0..1.
    struct Label
    {
        float x = 0, y = 0;
        std::string text;
        uint32_t color = 0xFFFFFFFF; // ABGR (как ImU32)
        float health = -1;           // 0..1 — полоска здоровья, <0 — без неё
        float scale = 1.f;
    };

    enum Hotkey : int
    {
        KeyEsp = VK_F3,
        KeyNoClip = VK_F4,
        KeyWaypoint = VK_F5,
        KeyConnect = VK_F9,
    };

    void Init();
    void Shutdown();

    // --- из игрового потока -------------------------------------------------
    void AddChat(const std::string& textWithColors);
    void ClearChat();
    void SetLabels(std::vector<Label>&& labels);
    void SetHud(const std::string& topRight);
    void Notify(const std::string& text, int ms = 4000);
    /// Чат доступен только на сервере: в одиночной игре T остаётся за игрой.
    void SetChatEnabled(bool enabled);
    /// Подсказки команд (Tab дополняет).
    void SetCommands(const std::vector<std::string>& commands);
    void OpenConnectDialog(const std::string& host, const std::string& name);

    /// Открыт ли ввод (чат или окно подключения) — игре нужно отключить управление.
    bool InputActive();
    /// Мс с момента закрытия ввода: Esc/Enter не должны дойти до игры.
    uint32_t MsSinceInputClosed();

    std::vector<std::string> TakeSubmittedChat();
    std::vector<int> TakeHotkeys();
    bool TakeConnectRequest(std::string& host, std::string& name);
}
