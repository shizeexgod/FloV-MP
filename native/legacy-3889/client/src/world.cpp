#include "world.h"
#include "common.h"
#include "natives.h"

#include <algorithm>
#include <cmath>
#include <map>
#include <string>
#include <vector>

// Мир от сервера. Сервер присылает всё сразу (карта может быть в десятки
// тысяч объектов), а в игре создаётся только то, что рядом: объекты — в
// радиусе kObjRadius, NPC — kNpcRadius. Проход по списку — раз в 500 мс, за
// кадр создаётся не больше kSpawnPerFrame штук, чтобы вход в плотный район не
// давал рывка кадра.
namespace flov::world
{
    namespace
    {
        constexpr float kObjRadius = 250.f, kObjKeep = 280.f;   // запас против мигания на границе
        constexpr float kNpcRadius = 120.f, kNpcKeep = 140.f;
        constexpr int kSpawnPerFrame = 8;
        constexpr ULONGLONG kScanMs = 500;
        constexpr ULONGLONG kModelTimeoutMs = 5000;

        struct Obj
        {
            Hash model = 0;
            float x = 0, y = 0, z = 0, rx = 0, ry = 0, rz = 0;
            int flags = 0;               // 1 — заморожен, 2 — без коллизии
            Object handle = 0;
            bool failed = false;         // модели нет в игре — не пытаться снова
        };
        struct Npc
        {
            Hash model = 0;
            float x = 0, y = 0, z = 0, heading = 0;
            std::string scenario;
            Ped handle = 0;
            bool failed = false;
        };
        struct BlipItem { Blip handle = 0; };
        struct Marker { int type = 1; float x = 0, y = 0, z = 0, scale = 1, dist = 60; int r = 255, g = 61, b = 138, a = 180; };
        struct Label3D { float x = 0, y = 0, z = 0, dist = 20; std::string text; };

        std::map<std::string, Obj> g_objs;
        std::map<std::string, Npc> g_npcs;
        std::map<std::string, BlipItem> g_blips;
        std::map<std::string, Marker> g_markers;
        std::map<std::string, Label3D> g_labels;
        std::vector<std::string> g_toSpawnObj, g_toSpawnNpc;
        std::map<Hash, ULONGLONG> g_modelWait;   // модель запрошена с этого момента
        ULONGLONG g_nextScan = 0;

        std::string at(const std::vector<std::string>& m, size_t i) { return i < m.size() ? m[i] : std::string(); }

        float Dist2(float ax, float ay, float az, float bx, float by, float bz)
        {
            const float dx = ax - bx, dy = ay - by, dz = az - bz;
            return dx * dx + dy * dy + dz * dz;
        }

        void Delete(Entity& e)
        {
            if (e && n::DOES_ENTITY_EXIST(e))
            {
                n::SET_ENTITY_AS_MISSION_ENTITY(e, TRUE, TRUE);
                n::DELETE_ENTITY(&e);
            }
            e = 0;
        }

        void RemoveBlip(BlipItem& b) { if (b.handle) n::REMOVE_BLIP(&b.handle); b.handle = 0; }

        /// Модель готова? Запрашивает её и ждёт до kModelTimeoutMs; false + failed — модели нет.
        bool ModelReady(Hash model, bool& failed)
        {
            if (!n::IS_MODEL_IN_CDIMAGE(model) || !n::IS_MODEL_VALID(model)) { failed = true; return false; }
            if (n::HAS_MODEL_LOADED(model)) { g_modelWait.erase(model); return true; }
            const ULONGLONG now = GetTickCount64();
            auto it = g_modelWait.find(model);
            if (it == g_modelWait.end()) g_modelWait[model] = now;
            else if (now - it->second > kModelTimeoutMs) { g_modelWait.erase(it); failed = true; return false; }
            n::REQUEST_MODEL(model);
            return false;
        }

        void SpawnObj(Obj& o)
        {
            if (o.handle || o.failed) return;
            if (!ModelReady(o.model, o.failed))
            {
                if (o.failed) Log("мир: модели объекта 0x" + [&] { char b[12]; sprintf_s(b, "%08X", o.model); return std::string(b); }() + " нет в игре");
                return;
            }
            o.handle = n::CREATE_OBJECT_NO_OFFSET(o.model, o.x, o.y, o.z, FALSE, TRUE, FALSE);
            if (!o.handle) return;
            n::SET_ENTITY_ROTATION(o.handle, o.rx, o.ry, o.rz, 2, TRUE);
            if (o.flags & 1) n::FREEZE_ENTITY_POSITION(o.handle, TRUE);
            if (o.flags & 2) n::SET_ENTITY_COLLISION(o.handle, FALSE, FALSE);
            n::SET_ENTITY_INVINCIBLE(o.handle, TRUE);
        }

        void SpawnNpc(Npc& p)
        {
            if (p.handle || p.failed) return;
            if (!ModelReady(p.model, p.failed)) return;
            p.handle = n::CREATE_PED(4, p.model, p.x, p.y, p.z, p.heading, FALSE, TRUE);
            if (!p.handle) return;
            n::SET_PED_DEFAULT_COMPONENT_VARIATION(p.handle);
            n::FREEZE_ENTITY_POSITION(p.handle, TRUE);
            n::SET_ENTITY_INVINCIBLE(p.handle, TRUE);
            n::SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(p.handle, TRUE);
            n::SET_PED_CAN_RAGDOLL(p.handle, FALSE);
            n::SET_PED_CAN_BE_TARGETTED(p.handle, FALSE);
            if (!p.scenario.empty())
                n::TASK_START_SCENARIO_IN_PLACE(p.handle, const_cast<char*>(p.scenario.c_str()), 0, TRUE);
        }

        void MakeBlip(BlipItem& b, float x, float y, float z, int sprite, int color, float scale, bool shortRange, const std::string& name)
        {
            RemoveBlip(b);
            b.handle = n::ADD_BLIP_FOR_COORD(x, y, z);
            if (!b.handle) return;
            n::SET_BLIP_SPRITE(b.handle, sprite);
            n::SET_BLIP_COLOUR(b.handle, color);
            n::SET_BLIP_SCALE(b.handle, scale);
            n::SET_BLIP_AS_SHORT_RANGE(b.handle, shortRange ? TRUE : FALSE);
            if (!name.empty())
            {
                n::BEGIN_TEXT_COMMAND_SET_BLIP_NAME(const_cast<char*>("STRING"));
                n::ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME(const_cast<char*>(name.c_str()));
                n::END_TEXT_COMMAND_SET_BLIP_NAME(b.handle);
            }
        }

        /// Текст без {rrggbb}; первый цвет — цвет надписи.
        std::string StripColors(const std::string& text, uint32_t& rgb)
        {
            std::string out;
            bool first = true;
            for (size_t i = 0; i < text.size(); ++i)
            {
                if (text[i] == '{' && i + 7 < text.size() && text[i + 7] == '}')
                {
                    bool hex = true;
                    for (size_t k = i + 1; k < i + 7; ++k) hex = hex && isxdigit((unsigned char)text[k]);
                    if (hex)
                    {
                        if (first) rgb = (uint32_t)strtoul(text.substr(i + 1, 6).c_str(), nullptr, 16);
                        first = false;
                        i += 7;
                        continue;
                    }
                }
                out += text[i];
            }
            return out;
        }
    }

    bool Handle(const std::vector<std::string>& m)
    {
        const std::string& t = m[0];
        if (t == "WOBJ")
        {
            auto& o = g_objs[at(m, 1)];
            Delete(o.handle);
            o = Obj{ ToUInt(at(m, 2)), ToFloat(at(m, 3)), ToFloat(at(m, 4)), ToFloat(at(m, 5)),
                     ToFloat(at(m, 6)), ToFloat(at(m, 7)), ToFloat(at(m, 8)), ToInt(at(m, 9)) };
            g_nextScan = 0;
        }
        else if (t == "WNPC")
        {
            auto& p = g_npcs[at(m, 1)];
            Delete(p.handle);
            p = Npc{ ToUInt(at(m, 2)), ToFloat(at(m, 3)), ToFloat(at(m, 4)), ToFloat(at(m, 5)), ToFloat(at(m, 6)), at(m, 7) };
            g_nextScan = 0;
        }
        else if (t == "WBLIP")
            MakeBlip(g_blips[at(m, 1)], ToFloat(at(m, 2)), ToFloat(at(m, 3)), ToFloat(at(m, 4)), ToInt(at(m, 5), 1),
                     ToInt(at(m, 6)), ToFloat(at(m, 7), 0.8f), at(m, 8) != "0", at(m, 9));
        else if (t == "WMARKER")
            g_markers[at(m, 1)] = Marker{ ToInt(at(m, 2), 1), ToFloat(at(m, 3)), ToFloat(at(m, 4)), ToFloat(at(m, 5)),
                                          ToFloat(at(m, 6), 1.f), ToFloat(at(m, 11), 60.f), ToInt(at(m, 7), 255),
                                          ToInt(at(m, 8), 61), ToInt(at(m, 9), 138), ToInt(at(m, 10), 180) };
        else if (t == "WLABEL")
            g_labels[at(m, 1)] = Label3D{ ToFloat(at(m, 2)), ToFloat(at(m, 3)), ToFloat(at(m, 4)), ToFloat(at(m, 5), 20.f), at(m, 6) };
        else if (t == "WDEL")
        {
            const std::string kind = at(m, 1), id = at(m, 2);
            if (kind == "OBJ") { auto it = g_objs.find(id); if (it != g_objs.end()) { Delete(it->second.handle); g_objs.erase(it); } }
            else if (kind == "NPC") { auto it = g_npcs.find(id); if (it != g_npcs.end()) { Delete(it->second.handle); g_npcs.erase(it); } }
            else if (kind == "BLIP") { auto it = g_blips.find(id); if (it != g_blips.end()) { RemoveBlip(it->second); g_blips.erase(it); } }
            else if (kind == "MARKER") g_markers.erase(id);
            else if (kind == "LABEL") g_labels.erase(id);
        }
        else if (t == "WCLEAR") Clear();
        else return false;
        return true;
    }

    void Tick(float x, float y, float z)
    {
        const ULONGLONG now = GetTickCount64();
        if (now >= g_nextScan)
        {
            g_nextScan = now + kScanMs;
            g_toSpawnObj.clear();
            g_toSpawnNpc.clear();
            for (auto& [id, o] : g_objs)
            {
                const float d2 = Dist2(o.x, o.y, o.z, x, y, z);
                if (o.handle && d2 > kObjKeep * kObjKeep) Delete(o.handle);
                else if (!o.handle && !o.failed && d2 < kObjRadius * kObjRadius) g_toSpawnObj.push_back(id);
            }
            for (auto& [id, p] : g_npcs)
            {
                const float d2 = Dist2(p.x, p.y, p.z, x, y, z);
                if (p.handle && d2 > kNpcKeep * kNpcKeep) Delete(p.handle);
                else if (!p.handle && !p.failed && d2 < kNpcRadius * kNpcRadius) g_toSpawnNpc.push_back(id);
            }
            // Сначала ближние: в плотном районе игрок сразу видит то, что вокруг него.
            auto nearFirst = [&](auto& list, auto& items) {
                std::sort(list.begin(), list.end(), [&](const std::string& a, const std::string& b) {
                    const auto& ia = items[a]; const auto& ib = items[b];
                    return Dist2(ia.x, ia.y, ia.z, x, y, z) > Dist2(ib.x, ib.y, ib.z, x, y, z);
                });
            };
            nearFirst(g_toSpawnObj, g_objs);
            nearFirst(g_toSpawnNpc, g_npcs);
        }
        for (int i = 0; i < kSpawnPerFrame && !g_toSpawnObj.empty(); ++i)
        {
            auto it = g_objs.find(g_toSpawnObj.back());
            if (it != g_objs.end())
            {
                SpawnObj(it->second);
                if (!it->second.handle && !it->second.failed) break; // модель ещё грузится — в следующий кадр
            }
            g_toSpawnObj.pop_back();
        }
        if (!g_toSpawnNpc.empty())
        {
            auto it = g_npcs.find(g_toSpawnNpc.back());
            if (it != g_npcs.end()) SpawnNpc(it->second);
            if (it == g_npcs.end() || it->second.handle || it->second.failed) g_toSpawnNpc.pop_back();
        }

        for (const auto& [id, mk] : g_markers)
        {
            if (Dist2(mk.x, mk.y, mk.z, x, y, z) > mk.dist * mk.dist) continue;
            n::DRAW_MARKER(mk.type, mk.x, mk.y, mk.z, 0, 0, 0, 0, 0, 0, mk.scale, mk.scale, mk.scale * 0.6f,
                           mk.r, mk.g, mk.b, mk.a, FALSE, FALSE, 2, FALSE, nullptr, nullptr, FALSE);
        }
    }

    void AddLabels(std::vector<ui::Label>& out, float cx, float cy, float cz)
    {
        for (const auto& [id, l] : g_labels)
        {
            const float d2 = Dist2(l.x, l.y, l.z, cx, cy, cz);
            if (d2 > l.dist * l.dist) continue;
            float sx = 0, sy = 0;
            if (!n::GET_SCREEN_COORD_FROM_WORLD_COORD(l.x, l.y, l.z, &sx, &sy)) continue;
            ui::Label lab;
            lab.x = sx; lab.y = sy;
            lab.rgb = 0xFFFFFF;
            lab.name = StripColors(l.text, lab.rgb);
            const float k = std::clamp(std::sqrt(d2) / std::max(1.f, l.dist), 0.f, 1.f);
            lab.scale = 1.f - 0.25f * k;
            lab.alpha = 1.f - 0.5f * k;
            out.push_back(std::move(lab));
        }
    }

    void Clear()
    {
        for (auto& [id, o] : g_objs) Delete(o.handle);
        for (auto& [id, p] : g_npcs) Delete(p.handle);
        for (auto& [id, b] : g_blips) RemoveBlip(b);
        g_objs.clear(); g_npcs.clear(); g_blips.clear(); g_markers.clear(); g_labels.clear();
        g_toSpawnObj.clear(); g_toSpawnNpc.clear(); g_modelWait.clear();
    }

    std::string Summary()
    {
        int objs = 0, npcs = 0;
        for (auto& [id, o] : g_objs) objs += o.handle ? 1 : 0;
        for (auto& [id, p] : g_npcs) npcs += p.handle ? 1 : 0;
        return "объекты " + std::to_string(objs) + "/" + std::to_string(g_objs.size()) +
               ", NPC " + std::to_string(npcs) + "/" + std::to_string(g_npcs.size()) +
               ", метки " + std::to_string(g_blips.size()) + ", маркеры " + std::to_string(g_markers.size()) +
               ", надписи " + std::to_string(g_labels.size());
    }
}
