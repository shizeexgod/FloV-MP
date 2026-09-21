#pragma once
// Настройки владельца сервера (server/config/client.cfg), присланные в CFG.
// Значения по умолчанию совпадают с серверными — на случай старого сервера.

#include <cstdint>
#include <cstdlib>
#include <map>
#include <mutex>
#include <string>
#include <vector>

namespace flov::settings
{
    inline std::mutex g_mutex;
    inline std::map<std::string, std::string> g_values = {
        { "world.peds", "off" }, { "world.traffic", "off" }, { "world.parked_vehicles", "off" },
        { "world.police", "off" }, { "world.ambient_events", "off" }, { "world.freeze_time", "off" },
        { "hud.minimap", "on" }, { "hud.ability_bar", "off" }, { "hud.area_names", "off" },
        { "hud.vehicle_names", "off" }, { "hud.weapon_wheel", "off" }, { "hud.pause_menu", "off" },
        { "hud.player_blips", "off" }, { "hud.watermark", "off" }, { "hud.accent", "#ffffff" },
        { "nametags.enabled", "on" }, { "nametags.distance", "30" }, { "nametags.show_id", "on" },
        { "nametags.health", "on" }, { "nametags.armor", "on" }, { "nametags.voice_icon", "on" },
        { "nametags.admin_badge", "off" }, { "nametags.color", "#ffffff" }, { "nametags.underscore_to_space", "on" },
        { "chat.enabled", "on" }, { "chat.lines", "10" }, { "chat.fade_seconds", "15" }, { "chat.timestamps", "on" },
        { "chat.width", "540" }, { "console.enabled", "on" },
        { "voice.enabled", "on" }, { "voice.radius", "25" }, { "voice.key", "N" },
        { "keys.esp", "F3" }, { "keys.noclip", "F4" }, { "keys.waypoint", "F5" },
        { "keys.chat", "T" }, { "keys.console", "F8" }, { "console.theme", "obsidian" }, { "chat.max_length", "256" },
        { "window.title", "FloV Multiplayer — {server}" }, { "branding.name", "FloV:MP" },
        { "loading.enabled", "on" }, { "loading.title", "" }, { "loading.accent", "#fbbf24" },
        { "loading.tips", "{a1a1aa}Голос:{71717a} удерживайте N, чтобы говорить. Вас слышат те, кто рядом. | "
                          "{a1a1aa}Чат:{71717a} клавиша T. Команды начинаются с «/», список — /help. | "
                          "{a1a1aa}Консоль:{71717a} клавиша F8 — там видно, что происходит, если что-то пошло не так. | "
                          "{a1a1aa}Первый вход{71717a} дольше обычного: игра подгружает город." },
    };

    inline void Set(const std::string& k, const std::string& v)
    {
        std::lock_guard lock(g_mutex);
        g_values[k] = v;
    }

    inline std::string Get(const std::string& k)
    {
        std::lock_guard lock(g_mutex);
        auto it = g_values.find(k);
        return it == g_values.end() ? std::string() : it->second;
    }

    inline bool Bool(const std::string& k) { const auto v = Get(k); return v == "on" || v == "true" || v == "1"; }
    inline float Float(const std::string& k, float def = 0.f) { const auto v = Get(k); return v.empty() ? def : std::strtof(v.c_str(), nullptr); }
    inline int Int(const std::string& k, int def = 0) { const auto v = Get(k); return v.empty() ? def : std::atoi(v.c_str()); }

    /// "#rrggbb" → 0xRRGGBB.
    inline uint32_t Rgb(const std::string& k, uint32_t def = 0xFFFFFF)
    {
        const auto v = Get(k);
        if (v.size() != 7 || v[0] != '#') return def;
        return (uint32_t)std::strtoul(v.c_str() + 1, nullptr, 16);
    }

    /// Виртуальный код клавиши: A..Z, 0..9, F1..F12.
    inline int Key(const std::string& k, int def)
    {
        const auto v = Get(k);
        if (v.size() == 1 && ((v[0] >= 'A' && v[0] <= 'Z') || (v[0] >= '0' && v[0] <= '9'))) return v[0];
        if (v.size() >= 2 && v[0] == 'F') { const int n = std::atoi(v.c_str() + 1); if (n >= 1 && n <= 12) return 0x70 + n - 1; }
        return def;
    }
}
