#include "game.h"
#include "common.h"
#include "natives.h"
#include "net.h"
#include "settings.h"
#include "ui.h"
#include "voice.h"
#include "world.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <ctime>
#include <map>
#include <set>
#include <string>
#include <vector>

namespace flov::game
{
    namespace
    {
        constexpr Hash kFreemodeMale = 0x705E61F2;       // mp_m_freemode_01
        constexpr Hash kPlayerGroup = 0x6F0783F5;        // PLAYER
        constexpr Hash kFullAuto = 0xC6EE6B4C;           // FIRING_PATTERN_FULL_AUTO
        constexpr Hash kUnarmed = 0xA2719263;
        constexpr int kRemotePedHealth = 10000;
        constexpr ULONGLONG kStateIntervalMs = 50;

        enum Flag : int
        {
            FInVehicle = 1, FDead = 2, FAiming = 4, FShooting = 8, FDucking = 16,
            FJumping = 32, FRagdoll = 64, FNoClip = 128, FEngine = 256, FSiren = 512,
        };

        struct State
        {
            float x = 0, y = 0, z = 0, heading = 0, vx = 0, vy = 0, vz = 0;
            int flags = 0;
            Hash vehModel = 0;
            int vehOwner = 0, seat = -1;
            float rx = 0, ry = 0, rz = 0;
            int health = 200, armor = 0;
            Hash weapon = 0;
            float speed = 0;
            Hash pedModel = 0;
            ULONGLONG received = 0;
        };

        struct Remote
        {
            int id = 0;
            std::string name;
            State cur, prev;
            bool hasState = false;
            Ped ped = 0;
            Hash pedModel = 0;
            Vehicle veh = 0;
            Hash vehModel = 0;
            Blip blip = 0;
            Hash weapon = 0;
            ULONGLONG nextTask = 0;
            bool dead = false;
            bool inVehicleSeat = false;
            bool ragdoll = false;                 // сейчас падает (переносим с чужого экрана)
            int adminLevel = 0;
        };

        Net g_net;
        std::map<int, Remote> g_remotes;
        int g_myId = 0;
        std::string g_myName, g_serverName;
        int g_adminLevel = 0;
        std::set<std::string> g_allowed;
        std::map<int, int> g_roster;
        bool g_welcomed = false, g_readySent = false, g_spawnedOnce = false;
        ULONGLONG g_welcomeAt = 0, g_nextState = 0, g_nextPing = 0, g_nextClean = 0;
        int g_espMode = 0; // 0 — выкл, 1 — игроки, 2 — транспорт, 3 — всё
        bool g_noclip = false, g_god = false, g_frozen = false;
        int g_spectateTarget = 0;
        Entity g_spectateAttached = 0;
        bool g_localDead = false;
        int g_lastAttacker = 0;
        ULONGLONG g_lastAttackAt = 0;
        int g_lastHealth = 200, g_lastArmor = 0;
        Vehicle g_spawnedCar = 0, g_lastOwnVehicle = 0;
        bool g_carLocked = false;
        Hash g_remoteGroup = 0;
        std::string g_host;
        int g_port = 0;
        float g_voiceRadius = 25.f;
        bool g_micWarned = false;
        int g_altPort = 0; // запасной порт, если основной не ответил (ввели сразу порт шлюза)
        std::string g_typedAddress;
        std::string g_pendingName;
        std::string g_voiceToken;
        int g_voicePort = 0;

        // Загрузочный экран: от подключения до появления в мире.
        bool g_loadingActive = false;
        ULONGLONG g_loadStart = 0, g_spawnAt = 0;

        // Показатели для консоли и netgraph.
        ULONGLONG g_statsAt = 0;
        int g_statsFrames = 0;
        float g_statsFrameSum = 0.f;
        uint64_t g_lastIn = 0, g_lastOut = 0;

        /// Настройки владельца сервера (client.cfg), разобранные один раз при
        /// получении CFG, — чтобы не искать строки в словаре каждый кадр.
        struct Cfg
        {
            bool peds = false, traffic = false, parked = false, police = false, ambient = false, freezeTime = false;
            bool minimap = true, abilityBar = false, areaNames = false, vehicleNames = false, weaponWheel = false;
            bool pauseMenu = false, playerBlips = false, watermark = false;
            bool tags = true, tagId = true, tagHealth = true, tagArmor = true, tagVoice = true, tagAdmin = false, tagSpace = true;
            float tagDistance = 30.f;
            uint32_t tagColor = 0xFFFFFF;
            bool chat = true, voice = true, loading = true;
            int voiceKey = 'N', espKey = VK_F3, noclipKey = VK_F4, waypointKey = VK_F5;
            float vehPower = 1.f, vehTorque = 1.f;   // множители двигателя из настроек сервера
        } g_cfg;

        void NotifyEsp();
        void CopyToClipboard(const std::string& text);

        float Dist2(float ax, float ay, float az, float bx, float by, float bz)
        {
            const float dx = ax - bx, dy = ay - by, dz = az - bz;
            return dx * dx + dy * dy + dz * dz;
        }

        float AngleLerp(float a, float b, float t)
        {
            float d = std::fmod(b - a + 540.f, 360.f) - 180.f;
            return a + d * t;
        }

        bool LoadModel(Hash model, int timeoutMs = 3000)
        {
            if (!n::IS_MODEL_IN_CDIMAGE(model) || !n::IS_MODEL_VALID(model)) return false;
            n::REQUEST_MODEL(model);
            for (int waited = 0; !n::HAS_MODEL_LOADED(model); waited += 10)
            {
                if (waited > timeoutMs) return false;
                WAIT(10);
                n::REQUEST_MODEL(model);
            }
            return true;
        }

        void DeleteEntity(Entity& e)
        {
            if (e && n::DOES_ENTITY_EXIST(e))
            {
                n::SET_ENTITY_AS_MISSION_ENTITY(e, TRUE, TRUE);
                n::DELETE_ENTITY(&e);
            }
            e = 0;
        }

        void Chat(const std::string& text) { ui::AddChat(text); }

        void SendChatLine(const std::string& kind, const std::string& author, const std::string& text)
        {
            // Обычная реплика — как в прежнем чате: жирный автор и текст.
            if (kind == "player") ui::AddChat(text, author);
            else if (kind == "me") Chat("{c084fc}* " + author + " " + text);
            else if (kind == "do") Chat("{c084fc}* " + text + " (" + author + ")");
            else if (kind == "ooc") Chat("{a1a1aa}(( " + author + ": " + text + " ))");
            else if (kind == "shout") Chat("{fbbf24}" + author + " кричит: " + text);
            else if (kind == "whisper") Chat("{f9a8d4}" + author + " шепчет: " + text);
            else if (kind == "admin") ui::AddChat(text, "[A] " + author, 0x34D399);
            else if (!author.empty()) Chat("{ff3d8a}[" + author + "]{ffffff} " + text);
            else Chat("{ffffff}" + text);
        }

        // --- удалённые игроки ---------------------------------------------------

        Remote* FindByEntity(Entity e)
        {
            if (!e) return nullptr;
            for (auto& [id, r] : g_remotes)
                if (r.ped == e || r.veh == e) return &r;
            return nullptr;
        }

        void DestroyRemote(Remote& r)
        {
            if (r.blip) n::REMOVE_BLIP(&r.blip);
            DeleteEntity(r.ped);
            // Машину, в которой сидим мы сами, не удаляем из-под себя.
            if (r.veh && n::GET_VEHICLE_PED_IS_IN(n::PLAYER_PED_ID(), FALSE) == r.veh)
                n::SET_ENTITY_AS_NO_LONGER_NEEDED(&r.veh);
            else
                DeleteEntity(r.veh);
            r.veh = 0;
            r.ped = 0;
            r.blip = 0;
        }

        void AddRemoteBlip(Remote& r)
        {
            if (r.blip || !r.ped) return;
            r.blip = n::ADD_BLIP_FOR_ENTITY(r.ped);
            n::SET_BLIP_SPRITE(r.blip, 1);
            n::SET_BLIP_COLOUR(r.blip, 0);
            n::SET_BLIP_SCALE(r.blip, 0.75f);
            n::SET_BLIP_AS_SHORT_RANGE(r.blip, TRUE);
        }

        void ConfigureRemotePed(Remote& r)
        {
            const Ped ped = r.ped;
            n::SET_ENTITY_AS_MISSION_ENTITY(ped, TRUE, TRUE);
            n::SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(ped, TRUE);
            n::TASK_SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(ped, TRUE);
            n::SET_PED_FLEE_ATTRIBUTES(ped, 0, FALSE);
            n::SET_PED_COMBAT_ATTRIBUTES(ped, 46, TRUE);
            n::SET_PED_CAN_RAGDOLL(ped, FALSE);
            n::SET_PED_CAN_BE_DRAGGED_OUT(ped, FALSE);
            n::SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE(ped, 1);
            n::SET_PED_SUFFERS_CRITICAL_HITS(ped, FALSE);
            n::SET_PED_DIES_WHEN_INJURED(ped, FALSE);
            n::SET_PED_KEEP_TASK(ped, TRUE);
            n::SET_PED_CAN_SWITCH_WEAPON(ped, FALSE);
            n::SET_PED_INFINITE_AMMO_CLIP(ped, TRUE);
            n::SET_PED_CONFIG_FLAG(ped, 281, TRUE); // без «корчащихся» ранений
            n::SET_PED_CONFIG_FLAG(ped, 32, FALSE); // не вылетает через стекло
            n::SET_ENTITY_MAX_HEALTH(ped, kRemotePedHealth);
            n::SET_ENTITY_HEALTH(ped, kRemotePedHealth);
            if (g_remoteGroup) n::SET_PED_RELATIONSHIP_GROUP_HASH(ped, g_remoteGroup);
            if (r.pedModel == kFreemodeMale || r.pedModel == 0x9C9EFFD8)
                n::SET_PED_HEAD_BLEND_DATA(ped, 0, 0, 0, 0, 0, 0, 0.5f, 0.5f, 0.f, FALSE);
            n::SET_PED_DEFAULT_COMPONENT_VARIATION(ped);
            if (g_cfg.playerBlips) AddRemoteBlip(r);
            r.weapon = 0;
        }

        bool EnsureRemotePed(Remote& r)
        {
            const Hash model = r.cur.pedModel ? r.cur.pedModel : kFreemodeMale;
            if (r.ped && n::DOES_ENTITY_EXIST(r.ped) && r.pedModel == model) return true;
            if (r.ped) { if (r.blip) n::REMOVE_BLIP(&r.blip); DeleteEntity(r.ped); r.blip = 0; }
            if (!n::IS_MODEL_IN_CDIMAGE(model)) return false;
            n::REQUEST_MODEL(model);
            if (!n::HAS_MODEL_LOADED(model)) return false; // догрузится в следующих кадрах
            r.ped = n::CREATE_PED(4, model, r.cur.x, r.cur.y, r.cur.z - 1.f, r.cur.heading, FALSE, TRUE);
            n::SET_MODEL_AS_NO_LONGER_NEEDED(model);
            if (!r.ped) return false;
            r.pedModel = model;
            Log("игрок [" + std::to_string(r.id) + "] " + r.name + ": создан персонаж");
            r.dead = false;
            r.inVehicleSeat = false;
            ConfigureRemotePed(r);
            return true;
        }

        Vehicle VehicleOfOwner(int owner)
        {
            if (owner == g_myId)
            {
                const Ped me = n::PLAYER_PED_ID();
                const Vehicle v = n::GET_VEHICLE_PED_IS_IN(me, FALSE);
                return v ? v : g_lastOwnVehicle;
            }
            auto it = g_remotes.find(owner);
            return it != g_remotes.end() ? it->second.veh : 0;
        }

        bool EnsureRemoteVehicle(Remote& r)
        {
            const auto& s = r.cur;
            if (r.veh && n::DOES_ENTITY_EXIST(r.veh) && r.vehModel == s.vehModel)
            {
                // Пересел в другую машину той же модели далеко от прежней — новая.
                const Vector3 p = n::GET_ENTITY_COORDS(r.veh, TRUE);
                if (Dist2(p.x, p.y, p.z, s.x, s.y, s.z) < 60.f * 60.f) return true;
            }
            if (r.veh) DeleteEntity(r.veh);
            if (!n::IS_MODEL_IN_CDIMAGE(s.vehModel) || !n::IS_MODEL_A_VEHICLE(s.vehModel))
            {
                static std::set<Hash> reported;
                if (reported.insert(s.vehModel).second) Log("неизвестная модель транспорта " + std::to_string(s.vehModel));
                return false;
            }
            n::REQUEST_MODEL(s.vehModel);
            if (!n::HAS_MODEL_LOADED(s.vehModel)) return false;
            r.veh = n::CREATE_VEHICLE(s.vehModel, s.x, s.y, s.z, s.heading, FALSE, TRUE);
            n::SET_MODEL_AS_NO_LONGER_NEEDED(s.vehModel);
            if (!r.veh) return false;
            r.vehModel = s.vehModel;
            Log("игрок [" + std::to_string(r.id) + "] " + r.name + ": создан транспорт " + std::to_string(s.vehModel));
            n::SET_ENTITY_AS_MISSION_ENTITY(r.veh, TRUE, TRUE);
            n::SET_VEHICLE_ON_GROUND_PROPERLY(r.veh);
            n::SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER(r.veh, TRUE);
            n::SET_VEHICLE_NEEDS_TO_BE_HOTWIRED(r.veh, FALSE);
            n::SET_VEHICLE_IS_STOLEN(r.veh, FALSE);
            // Неуязвимой машину не делаем: тогда пули по ней не доходили бы до
            // сидящих внутри («нерег» по игрокам в транспорте). Вместо этого
            // каждый кадр чиним прочность — чужая машина не горит и не взрывается
            // у нас на экране, а попадания по пассажирам считаются.
            n::SET_VEHICLE_NUMBER_PLATE_TEXT(r.veh, const_cast<char*>("FLOVMP"));
            r.inVehicleSeat = false;
            return true;
        }

        void UpdateRemote(Remote& r, ULONGLONG now, const Vector3& me)
        {
            if (!r.hasState) return;
            const State& s = r.cur;
            // Дальних игроков в мире не держим: персонаж и машина за 350 м не
            // видны, а каждый стоит кадру. Состояние остаётся — вернутся мгновенно.
            if (Dist2(s.x, s.y, s.z, me.x, me.y, me.z) > 350.f * 350.f && r.id != g_spectateTarget)
            {
                if (r.ped || r.veh) DestroyRemote(r);
                return;
            }
            if (!EnsureRemotePed(r)) return;
            const Ped ped = r.ped;

            // Администратор в NoClip невидим для остальных — как в alt:V-клиенте.
            const bool hidden = (s.flags & FNoClip) != 0;
            n::SET_ENTITY_VISIBLE(ped, !hidden, FALSE);
            n::SET_ENTITY_COLLISION(ped, !hidden, TRUE);

            // Смерть и возрождение.
            const bool dead = (s.flags & FDead) != 0;
            if (dead && !r.dead)
            {
                r.dead = true;
                n::SET_ENTITY_HEALTH(ped, 0);
                return;
            }
            if (!dead && r.dead)
            {
                // Воскресить ped нельзя — пересоздаём.
                if (r.blip) n::REMOVE_BLIP(&r.blip);
                DeleteEntity(r.ped);
                r.blip = 0;
                r.dead = false;
                return;
            }
            if (dead) return;

            // Оружие в руках.
            const Hash weapon = s.weapon ? s.weapon : kUnarmed;
            if (weapon != r.weapon)
            {
                if (weapon != kUnarmed) n::GIVE_WEAPON_TO_PED(ped, weapon, 9999, FALSE, TRUE);
                n::SET_CURRENT_PED_WEAPON(ped, weapon, TRUE);
                r.weapon = weapon;
            }

            const float dt = std::min(0.25f, (float)(now - s.received) / 1000.f);
            const bool inVehicle = (s.flags & FInVehicle) != 0 && s.vehModel != 0;
            if (inVehicle)
            {
                const bool driver = s.seat == -1 && s.vehOwner == r.id;
                Vehicle veh = 0;
                if (driver)
                {
                    if (!EnsureRemoteVehicle(r)) return;
                    veh = r.veh;
                    const float tx = s.x + s.vx * dt, ty = s.y + s.vy * dt, tz = s.z + s.vz * dt;
                    const Vector3 p = n::GET_ENTITY_COORDS(veh, TRUE);
                    const float err2 = Dist2(p.x, p.y, p.z, tx, ty, tz);
                    if (err2 > 12.f * 12.f)
                    {
                        n::SET_ENTITY_COORDS_NO_OFFSET(veh, tx, ty, tz, FALSE, FALSE, FALSE);
                        n::SET_ENTITY_VELOCITY(veh, s.vx, s.vy, s.vz);
                    }
                    else
                    {
                        // Коррекция скоростью, а не телепортом: физика машины сохраняется,
                        // движение без рывков. Высоту на земле ведут колёса и подвеска:
                        // вертикальную скорость трогаем только в воздухе или при заметном
                        // расхождении — иначе машина «висела» в паре сантиметров над дорогой.
                        const bool air = n::IS_ENTITY_IN_AIR(veh) != 0;
                        const float ez = tz - p.z;
                        const Vector3 cur = n::GET_ENTITY_VELOCITY(veh);
                        const float vz = air || std::fabs(ez) > 0.75f ? s.vz + ez * 4.f : cur.z;
                        n::SET_ENTITY_VELOCITY(veh, s.vx + (tx - p.x) * 4.f, s.vy + (ty - p.y) * 4.f, vz);
                    }
                    const Vector3 rot = n::GET_ENTITY_ROTATION(veh, 2);
                    auto diff = [](float a, float b) { return std::fabs(std::fmod(b - a + 540.f, 360.f) - 180.f); };
                    // Наклон (тангаж, крен) на земле тоже задаёт подвеска: правим его
                    // только при большом расхождении (перевернулся, прыжок).
                    const bool tilt = n::IS_ENTITY_IN_AIR(veh) || diff(rot.x, s.rx) > 12.f || diff(rot.y, s.ry) > 12.f;
                    if (tilt || diff(rot.z, s.rz) > 0.5f)
                        n::SET_ENTITY_ROTATION(veh, tilt ? AngleLerp(rot.x, s.rx, 0.5f) : rot.x, tilt ? AngleLerp(rot.y, s.ry, 0.5f) : rot.y,
                                               AngleLerp(rot.z, s.rz, 0.5f), 2, TRUE);
                    // Прочность чужой машины держим целой (см. EnsureRemoteVehicle).
                    n::SET_VEHICLE_ENGINE_HEALTH(veh, 1000.f);
                    n::SET_VEHICLE_BODY_HEALTH(veh, 1000.f);
                    n::SET_VEHICLE_PETROL_TANK_HEALTH(veh, 1000.f);
                    const bool engine = (s.flags & FEngine) != 0;
                    if ((bool)n::GET_IS_VEHICLE_ENGINE_RUNNING(veh) != engine) n::SET_VEHICLE_ENGINE_ON(veh, engine, TRUE, TRUE);
                    const bool siren = (s.flags & FSiren) != 0;
                    if ((bool)n::IS_VEHICLE_SIREN_ON(veh) != siren) n::SET_VEHICLE_SIREN(veh, siren);
                }
                else
                {
                    veh = VehicleOfOwner(s.vehOwner);
                    if (!veh || !n::DOES_ENTITY_EXIST(veh))
                    {
                        // Машины водителя у нас ещё нет (модель не догрузилась):
                        // пассажир едет вместе с ней, а не стоит столбом на дороге —
                        // его координаты в STATE и так координаты машины.
                        n::SET_ENTITY_COORDS_NO_OFFSET(ped, s.x, s.y, s.z, FALSE, FALSE, FALSE);
                        n::SET_ENTITY_HEADING(ped, s.heading);
                        r.inVehicleSeat = false;
                        return;
                    }
                }
                if (n::GET_VEHICLE_PED_IS_IN(ped, FALSE) != veh || !r.inVehicleSeat)
                {
                    Ped busy = n::GET_PED_IN_VEHICLE_SEAT(veh, s.seat);
                    // Место занял случайный прохожий (сел в машину сам) — убираем его,
                    // иначе игрок навсегда остаётся снаружи и «телепортируется» рядом.
                    if (busy && busy != ped && !FindByEntity(busy) && busy != n::PLAYER_PED_ID())
                        DeleteEntity(busy);
                    if (n::IS_VEHICLE_SEAT_FREE(veh, s.seat) || n::GET_PED_IN_VEHICLE_SEAT(veh, s.seat) == ped)
                    {
                        n::SET_PED_INTO_VEHICLE(ped, veh, s.seat);
                        r.inVehicleSeat = true;
                    }
                }
                return;
            }

            // Падение (сбила машина, упал с высоты): без этого игрок у соседей
            // «бежит стоя», пока у себя катится по земле.
            const bool ragdoll = (s.flags & FRagdoll) != 0;
            if (ragdoll != r.ragdoll)
            {
                r.ragdoll = ragdoll;
                n::SET_PED_CAN_RAGDOLL(ped, ragdoll ? TRUE : FALSE);
                if (ragdoll) n::SET_PED_TO_RAGDOLL(ped, 2000, 2000, 0, TRUE, TRUE, FALSE);
                else n::CLEAR_PED_TASKS(ped);
            }
            if (ragdoll)
            {
                // Пока падает, позицию не правим: иначе тело дёргается на месте.
                if (n::IS_PED_RAGDOLL(ped)) return;
                n::SET_PED_TO_RAGDOLL(ped, 2000, 2000, 0, TRUE, TRUE, FALSE);
                return;
            }
            // Приседание.
            const bool ducking = (s.flags & FDucking) != 0;
            if ((bool)n::IS_PED_DUCKING(ped) != ducking) n::SET_PED_DUCKING(ped, ducking ? TRUE : FALSE);

            // Пешком.
            if (r.inVehicleSeat || n::IS_PED_IN_ANY_VEHICLE(ped, FALSE))
            {
                n::CLEAR_PED_TASKS_IMMEDIATELY(ped);
                n::SET_ENTITY_COORDS_NO_OFFSET(ped, s.x, s.y, s.z, FALSE, FALSE, FALSE);
                r.inVehicleSeat = false;
            }
            const Vector3 p = n::GET_ENTITY_COORDS(ped, TRUE);
            const float tx = s.x + s.vx * dt, ty = s.y + s.vy * dt, tz = s.z + s.vz * dt;
            const float err2 = Dist2(p.x, p.y, p.z, tx, ty, tz);
            if (err2 > 8.f * 8.f)
            {
                n::SET_ENTITY_COORDS_NO_OFFSET(ped, tx, ty, tz, FALSE, FALSE, FALSE);
                n::SET_ENTITY_HEADING(ped, s.heading);
                r.nextTask = 0;
            }

            const bool aiming = (s.flags & FAiming) != 0, shooting = (s.flags & FShooting) != 0;
            if (now >= r.nextTask)
            {
                r.nextTask = now + 200;
                if (shooting && weapon != kUnarmed)
                {
                    n::TASK_SHOOT_AT_COORD(ped, s.rx, s.ry, s.rz, 250, kFullAuto);
                }
                else if (aiming && weapon != kUnarmed)
                {
                    n::TASK_AIM_GUN_AT_COORD(ped, s.rx, s.ry, s.rz, 400, FALSE, FALSE);
                }
                else if (s.speed >= 0.5f || err2 > 0.6f * 0.6f)
                {
                    const float moveSpeed = s.speed >= 2.5f ? 3.f : s.speed >= 1.5f ? 2.f : 1.f;
                    n::TASK_GO_STRAIGHT_TO_COORD(ped, tx + s.vx * 0.4f, ty + s.vy * 0.4f, tz, moveSpeed, -1, s.heading, 0.2f);
                }
                else
                {
                    n::CLEAR_PED_TASKS(ped);
                    n::SET_ENTITY_HEADING(ped, s.heading);
                }
                if ((s.flags & FJumping) && !(r.prev.flags & FJumping)) n::TASK_JUMP(ped, TRUE);
            }
            if (s.speed < 0.5f && !aiming && err2 > 0.05f && err2 < 1.5f)
            {
                // Стоит на месте — мягко дотягиваем позицию без анимации ходьбы.
                n::SET_ENTITY_COORDS_NO_OFFSET(ped, p.x + (tx - p.x) * 0.2f, p.y + (ty - p.y) * 0.2f, p.z + (tz - p.z) * 0.2f,
                                               FALSE, FALSE, FALSE);
            }
        }

        // --- локальный игрок ------------------------------------------------------

        int SeatOf(Ped ped, Vehicle veh)
        {
            const int max = n::GET_VEHICLE_MAX_NUMBER_OF_PASSENGERS(veh);
            for (int seat = -1; seat < max; ++seat)
                if (n::GET_PED_IN_VEHICLE_SEAT(veh, seat) == ped) return seat;
            return -1;
        }

        void SendLocalState()
        {
            const Ped ped = n::PLAYER_PED_ID();
            const Player pl = n::PLAYER_ID();
            State s;
            Vector3 pos = n::GET_ENTITY_COORDS(ped, TRUE);
            Vector3 vel = n::GET_ENTITY_VELOCITY(ped);
            s.heading = n::GET_ENTITY_HEADING(ped);
            s.health = n::IS_ENTITY_DEAD(ped) ? 0 : n::GET_ENTITY_HEALTH(ped);
            s.armor = n::GET_PED_ARMOUR(ped);
            s.weapon = n::GET_SELECTED_PED_WEAPON(ped);
            s.pedModel = n::GET_ENTITY_MODEL(ped);
            if (n::IS_ENTITY_DEAD(ped)) s.flags |= FDead;
            if (g_noclip) s.flags |= FNoClip;

            if (n::IS_PED_IN_ANY_VEHICLE(ped, FALSE))
            {
                const Vehicle veh = n::GET_VEHICLE_PED_IS_IN(ped, FALSE);
                s.flags |= FInVehicle;
                s.vehModel = n::GET_ENTITY_MODEL(veh);
                s.seat = SeatOf(ped, veh);
                Remote* owner = FindByEntity(veh);
                s.vehOwner = owner ? owner->id : g_myId;
                if (!owner) g_lastOwnVehicle = veh;
                pos = n::GET_ENTITY_COORDS(veh, TRUE);
                vel = n::GET_ENTITY_VELOCITY(veh);
                const Vector3 rot = n::GET_ENTITY_ROTATION(veh, 2);
                s.rx = rot.x; s.ry = rot.y; s.rz = rot.z;
                s.heading = n::GET_ENTITY_HEADING(veh);
                if (n::GET_IS_VEHICLE_ENGINE_RUNNING(veh)) s.flags |= FEngine;
                if (n::IS_VEHICLE_SIREN_ON(veh)) s.flags |= FSiren;
                s.speed = n::GET_ENTITY_SPEED(veh);
            }
            else
            {
                const float speed = n::GET_ENTITY_SPEED(ped);
                s.speed = speed < 0.4f ? 0.f : speed < 2.4f ? 1.f : speed < 5.5f ? 2.f : 3.f;
                if (n::IS_PED_DUCKING(ped)) s.flags |= FDucking;
                if (n::IS_PED_JUMPING(ped)) s.flags |= FJumping;
                if (n::IS_PED_RAGDOLL(ped)) s.flags |= FRagdoll;
                if (n::IS_PED_SHOOTING(ped)) s.flags |= FShooting;
                if (n::IS_PLAYER_FREE_AIMING(pl) || (s.flags & FShooting))
                {
                    s.flags |= FAiming;
                    const Vector3 cam = n::GET_GAMEPLAY_CAM_COORD();
                    const Vector3 rot = n::GET_GAMEPLAY_CAM_ROT(2);
                    const float pitch = rot.x * 3.14159265f / 180.f, yaw = rot.z * 3.14159265f / 180.f;
                    s.rx = cam.x - std::sin(yaw) * std::cos(pitch) * 60.f;
                    s.ry = cam.y + std::cos(yaw) * std::cos(pitch) * 60.f;
                    s.rz = cam.z + std::sin(pitch) * 60.f;
                }
            }
            s.x = pos.x; s.y = pos.y; s.z = pos.z;
            s.vx = vel.x; s.vy = vel.y; s.vz = vel.z;
            g_net.Send({ "STATE", F(s.x), F(s.y), F(s.z), F(s.heading), F(s.vx), F(s.vy), F(s.vz),
                         std::to_string(s.flags), std::to_string(s.vehModel), std::to_string(s.vehOwner),
                         std::to_string(s.seat), F(s.rx), F(s.ry), F(s.rz), std::to_string(s.health),
                         std::to_string(s.armor), std::to_string(s.weapon), F(s.speed), std::to_string(s.pedModel) });
        }

        /// Остановить сюжет: все скрипты одиночной игры (миссии, катсцены,
        /// звонки, смену персонажа, «больницу»). Сервер FloV:MP, как FiveM и
        /// alt:V, работает в мире без сюжетных скриптов — иначе игрок на входе
        /// оказывается посреди миссии своего сохранения. Возвращает число остановленных.
        int StopStoryScripts()
        {
            const int self = n::GET_ID_OF_THIS_THREAD();
            std::vector<int> ids;
            n::SCRIPT_THREAD_ITERATOR_RESET();
            for (int id = n::SCRIPT_THREAD_ITERATOR_GET_NEXT_THREAD_ID(); id != 0 && ids.size() < 512;
                 id = n::SCRIPT_THREAD_ITERATOR_GET_NEXT_THREAD_ID())
                ids.push_back(id);
            int stopped = 0;
            std::string names;
            for (int id : ids)
            {
                if (id == self || !n::IS_THREAD_ACTIVE(id)) continue;
                const char* raw = n::GET_NAME_OF_SCRIPT_WITH_THIS_ID(id);
                const std::string name = raw ? raw : "";
                // Потоки ScriptHookV (свои и чужих ASI) имени сюжетного скрипта не имеют — не трогаем.
                if (name.empty()) continue;
                // Служебный скрипт, который движок сразу запускает снова (награды, DLC):
                // после трёх попыток оставляем его в покое — сюжета в нём нет, а
                // остановка по кругу только тратит время кадра.
                static std::map<std::string, int> kills;
                if (kills[name] >= 3) continue;
                ++kills[name];
                n::TERMINATE_THREAD(id);
                ++stopped;
                if (names.size() < 600) names += name + " ";
            }
            if (stopped) Log("сюжет: остановлено скриптов " + std::to_string(stopped) + ": " + names);
            return stopped;
        }

        /// После остановки сюжета: убрать катсцену, камеры миссии, рамки, вернуть управление.
        void RestoreGameplayView()
        {
            if (n::IS_CUTSCENE_ACTIVE() || n::IS_CUTSCENE_PLAYING())
            {
                n::STOP_CUTSCENE_IMMEDIATELY();
                n::REMOVE_CUTSCENE();
            }
            n::RENDER_SCRIPT_CAMS(FALSE, FALSE, 0, TRUE, FALSE);
            n::DESTROY_ALL_CAMS(TRUE);
            n::SET_WIDESCREEN_BORDERS(FALSE, 0);
            n::SET_CINEMATIC_MODE_ACTIVE(FALSE);
            n::CLEAR_TIMECYCLE_MODIFIER();
            n::SET_MISSION_FLAG(FALSE);
            n::STOP_SCRIPTED_CONVERSATION(FALSE);
            n::STOP_AUDIO_SCENES();
            n::CLEAR_PRINTS();
            n::CLEAR_ALL_HELP_MESSAGES();
            n::DISPLAY_RADAR(TRUE);
            n::DISPLAY_HUD(TRUE);
            if (!n::IS_PLAYER_CONTROL_ON(n::PLAYER_ID())) n::SET_PLAYER_CONTROL(n::PLAYER_ID(), TRUE, 0);
            if (n::IS_SCREEN_FADED_OUT()) n::DO_SCREEN_FADE_IN(300);
        }

        /// Мир по настройкам сервера: прохожие, трафик, полиция, поезда и т.п.
        void ApplyWorldSettings()
        {
            n::SET_MAX_WANTED_LEVEL(g_cfg.police ? 5 : 0);
            n::SET_CREATE_RANDOM_COPS(g_cfg.police);
            for (int i = 1; i <= 15; ++i) n::ENABLE_DISPATCH_SERVICE(i, g_cfg.police);
            n::SET_RANDOM_TRAINS(g_cfg.ambient);
            n::SET_RANDOM_BOATS(g_cfg.ambient);
            n::SET_GARBAGE_TRUCKS(g_cfg.ambient);
            n::SET_PED_POPULATION_BUDGET(g_cfg.peds ? 3 : 0);
            n::SET_VEHICLE_POPULATION_BUDGET(g_cfg.traffic ? 3 : 0);
            n::SET_NUMBER_OF_PARKED_VEHICLES(g_cfg.parked ? -1 : 0);
            n::PAUSE_CLOCK(g_cfg.freezeTime);
        }

        /// Полоска способности под мини-картой. (Scaleform «minimap» здесь не
        /// трогать: свой экземпляр этого фильма гасит карту в мини-карте.)
        void HideAbilityBar()
        {
            n::SET_ABILITY_BAR_VISIBILITY(FALSE);
        }

        /// Однопользовательский мир под мультиплеер: без сюжета, полиции,
        /// прохожих и автоматического «возрождения в больнице».
        void PrepareWorldOnce()
        {
            StopStoryScripts();
            RestoreGameplayView();
            // Сюжетное сохранение открывает карту по мере прохождения — на сервере
            // вся карта должна быть видна сразу (меню паузы и метки для F5).
            n::SET_MINIMAP_HIDE_FOW(TRUE);
            n::PAUSE_DEATH_ARREST_RESTART(TRUE);
            n::IGNORE_NEXT_RESTART(TRUE);
            n::SET_FADE_OUT_AFTER_DEATH(FALSE);
            n::SET_FADE_OUT_AFTER_ARREST(FALSE);
            n::SET_FADE_IN_AFTER_DEATH_ARREST(FALSE);
            ApplyWorldSettings();
            if (!g_remoteGroup)
                n::ADD_RELATIONSHIP_GROUP(const_cast<char*>("FLOVMP_REMOTE"), &g_remoteGroup);
            n::SET_RELATIONSHIP_BETWEEN_GROUPS(3, g_remoteGroup, kPlayerGroup);
            n::SET_RELATIONSHIP_BETWEEN_GROUPS(3, kPlayerGroup, g_remoteGroup);
            n::SET_CAN_ATTACK_FRIENDLY(n::PLAYER_PED_ID(), TRUE, FALSE);
            const Vector3 p = n::GET_ENTITY_COORDS(n::PLAYER_PED_ID(), TRUE);
            if (!g_cfg.peds) n::CLEAR_AREA_OF_PEDS(p.x, p.y, p.z, 1500.f, TRUE);
            if (!g_cfg.traffic && !g_cfg.parked) n::CLEAR_AREA_OF_VEHICLES(p.x, p.y, p.z, 1500.f, FALSE, FALSE, FALSE, FALSE, FALSE);
        }

        void WorldEveryFrame()
        {
            if (!g_cfg.peds)
            {
                n::SET_PED_DENSITY_MULTIPLIER_THIS_FRAME(0.f);
                n::SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME(0.f, 0.f);
            }
            if (!g_cfg.traffic)
            {
                n::SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(0.f);
                n::SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(0.f);
            }
            if (!g_cfg.parked) n::SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(0.f);
            // Мощность машин владелец задаёт настройкой: множители держатся
            // только кадр, поэтому выставляем их каждый кадр своему транспорту.
            if (g_cfg.vehPower != 1.f || g_cfg.vehTorque != 1.f)
            {
                const Ped me = n::PLAYER_PED_ID();
                if (n::IS_PED_IN_ANY_VEHICLE(me, FALSE))
                {
                    const Vehicle veh = n::GET_VEHICLE_PED_IS_IN(me, FALSE);
                    if (veh && n::DOES_ENTITY_EXIST(veh))
                    {
                        n::SET_VEHICLE_ENGINE_POWER_MULTIPLIER(veh, (g_cfg.vehPower - 1.f) * 100.f);
                        n::SET_VEHICLE_ENGINE_TORQUE_MULTIPLIER(veh, g_cfg.vehTorque);
                    }
                }
            }

            const Player pl = n::PLAYER_ID();
            if (!g_cfg.police)
            {
                n::SET_MAX_WANTED_LEVEL(0);
                n::CLEAR_PLAYER_WANTED_LEVEL(pl);
                n::SET_POLICE_IGNORE_PLAYER(pl, TRUE);
                n::SET_DISPATCH_COPS_FOR_PLAYER(pl, FALSE);
                n::HIDE_HUD_COMPONENT_THIS_FRAME(1); // звёзды розыска
            }

            // Интерфейс GTA: только то, что владелец сервера оставил включённым.
            n::DISPLAY_RADAR(g_cfg.minimap && !ui::LoadingVisible());
            if (!g_cfg.abilityBar) HideAbilityBar();
            if (!g_cfg.areaNames) { n::HIDE_HUD_COMPONENT_THIS_FRAME(7); n::HIDE_HUD_COMPONENT_THIS_FRAME(9); }
            if (!g_cfg.vehicleNames) { n::HIDE_HUD_COMPONENT_THIS_FRAME(6); n::HIDE_HUD_COMPONENT_THIS_FRAME(8); }
            for (int c : { 3, 4, 13 }) n::HIDE_HUD_COMPONENT_THIS_FRAME(c); // деньги сюжета — не наши
            if (!g_cfg.weaponWheel)
            {
                n::DISABLE_CONTROL_ACTION(0, 37, TRUE); // Tab — колесо оружия
                n::HIDE_HUD_COMPONENT_THIS_FRAME(19);
                n::HIDE_HUD_COMPONENT_THIS_FRAME(20);
            }
            if (!g_cfg.pauseMenu)
            {
                n::DISABLE_CONTROL_ACTION(0, 199, TRUE);
                n::DISABLE_CONTROL_ACTION(0, 200, TRUE);
            }
            // Замечание про меню Esc: в одиночной GTA оно останавливает игру
            // вместе со скриптами, поэтому снять паузу изнутри нельзя — наш
            // код на этих кадрах не выполняется (проверено: состояние игрока
            // перестаёт уходить на сервер). Чтобы время шло у всех одинаково,
            // нужен свой экран на Esc вместо штатного — отдельная задача.
            n::DISABLE_CONTROL_ACTION(0, 19, TRUE);  // колесо смены персонажа
            n::DISABLE_CONTROL_ACTION(0, 166, TRUE); // F5..F8 — выбор персонажа в сюжете
            n::DISABLE_CONTROL_ACTION(0, 167, TRUE);
            n::DISABLE_CONTROL_ACTION(0, 168, TRUE);
            n::DISABLE_CONTROL_ACTION(0, 169, TRUE);
            if (g_frozen) n::DISABLE_ALL_CONTROL_ACTIONS(0);
        }

        /// Пока открыт чат, консоль или окно F9 — игре не нужны ни клавиши, ни Esc.
        void InputEveryFrame()
        {
            if (ui::InputActive() || ui::MsSinceInputClosed() < 300 || ui::LoadingVisible())
            {
                n::DISABLE_ALL_CONTROL_ACTIONS(0);
                for (int g : { 0, 2 }) { n::DISABLE_CONTROL_ACTION(g, 199, TRUE); n::DISABLE_CONTROL_ACTION(g, 200, TRUE); }
            }
            if (ui::ConsoleOpen())
            {
                n::SET_MOUSE_CURSOR_ACTIVE_THIS_FRAME();
                int wheel = 0;
                if (n::IS_DISABLED_CONTROL_JUST_PRESSED(0, 241)) wheel += 1;
                if (n::IS_DISABLED_CONTROL_JUST_PRESSED(0, 242)) wheel -= 1;
                ui::SetCursor(n::GET_DISABLED_CONTROL_NORMAL(0, 239), n::GET_DISABLED_CONTROL_NORMAL(0, 240),
                              n::IS_DISABLED_CONTROL_PRESSED(0, 237) != 0, wheel);
            }
            if (ui::LoadingVisible()) n::HIDE_HUD_AND_RADAR_THIS_FRAME();
        }

        /// false — модели нет в игре (игрок уже получил сообщение).
        bool ApplyModel(Hash model, const std::string& name = "")
        {
            if (!LoadModel(model))
            {
                Chat("{ef4444}Модель персонажа" + (name.empty() ? "" : " «" + name + "»") + " не найдена в игре.");
                return false;
            }
            const int health = n::GET_ENTITY_HEALTH(n::PLAYER_PED_ID());
            n::SET_PLAYER_MODEL(n::PLAYER_ID(), model);
            const Ped ped = n::PLAYER_PED_ID();
            if (model == kFreemodeMale || model == 0x9C9EFFD8)
                n::SET_PED_HEAD_BLEND_DATA(ped, 0, 0, 0, 0, 0, 0, 0.5f, 0.5f, 0.f, FALSE);
            n::SET_PED_DEFAULT_COMPONENT_VARIATION(ped);
            n::SET_MODEL_AS_NO_LONGER_NEEDED(model);
            if (health > 100) n::SET_ENTITY_HEALTH(ped, health);
            return true;
        }

        void Teleport(float x, float y, float z)
        {
            const Ped ped = n::PLAYER_PED_ID();
            const Vehicle veh = n::GET_VEHICLE_PED_IS_IN(ped, FALSE);
            const Entity e = veh && SeatOf(ped, veh) == -1 ? veh : ped;
            n::REQUEST_COLLISION_AT_COORD(x, y, z);
            n::SET_ENTITY_COORDS(e, x, y, z, FALSE, FALSE, FALSE, FALSE);
        }

        void Resurrect(float x, float y, float z, float heading)
        {
            nv::invoke<void>(0xEA23C49EAA83ACFBull, x, y, z, heading, FALSE, FALSE);
            const Ped ped = n::PLAYER_PED_ID();
            n::CLEAR_PED_TASKS_IMMEDIATELY(ped);
            n::SET_ENTITY_COORDS_NO_OFFSET(ped, x, y, z, FALSE, FALSE, FALSE);
            n::SET_ENTITY_HEADING(ped, heading);
            n::SET_ENTITY_HEALTH(ped, 200);
            n::ANIMPOSTFX_STOP_ALL();
            n::RESET_LOCALPLAYER_STATE();
            n::DO_SCREEN_FADE_IN(500);
            g_localDead = false;
            g_lastHealth = 200;
        }

        void SpawnCar(Hash model, const std::string& name)
        {
            if (!n::IS_MODEL_IN_CDIMAGE(model) || !n::IS_MODEL_A_VEHICLE(model))
            {
                Chat("{ef4444}Транспорт «" + name + "» не найден в игре.");
                return;
            }
            if (!LoadModel(model)) { Chat("{ef4444}Модель не загрузилась, попробуйте ещё раз."); return; }
            const Ped ped = n::PLAYER_PED_ID();
            const Vector3 p = n::GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS(ped, 0.f, 3.f, 0.5f);
            const float heading = n::GET_ENTITY_HEADING(ped);
            if (g_spawnedCar) DeleteEntity(g_spawnedCar);
            Vehicle veh = n::CREATE_VEHICLE(model, p.x, p.y, p.z, heading, FALSE, TRUE);
            n::SET_MODEL_AS_NO_LONGER_NEEDED(model);
            if (!veh) { Chat("{ef4444}Не удалось создать транспорт."); return; }
            n::SET_ENTITY_AS_MISSION_ENTITY(veh, TRUE, TRUE);
            n::SET_VEHICLE_ON_GROUND_PROPERLY(veh);
            n::SET_VEHICLE_NUMBER_PLATE_TEXT(veh, const_cast<char*>("FLOVMP"));
            n::SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER(veh, TRUE);
            n::SET_VEHICLE_NEEDS_TO_BE_HOTWIRED(veh, FALSE);
            n::SET_VEHICLE_ENGINE_ON(veh, TRUE, TRUE, FALSE);
            n::SET_PED_INTO_VEHICLE(ped, veh, -1);
            g_spawnedCar = veh;
            g_lastOwnVehicle = veh;
            g_carLocked = false;
            Chat("{34d399}Создан транспорт: " + name + ". Прежний ваш транспорт убран.");
        }

        bool FindGround(float x, float y, float& z)
        {
            const Ped ped = n::PLAYER_PED_ID();
            for (float h = 950.f; h >= -50.f; h -= 50.f)
            {
                n::SET_ENTITY_COORDS_NO_OFFSET(ped, x, y, h, FALSE, FALSE, FALSE);
                n::REQUEST_COLLISION_AT_COORD(x, y, h);
                WAIT(30);
                float ground = 0;
                if (n::GET_GROUND_Z_FOR_3D_COORD(x, y, h, &ground, FALSE)) { z = ground; return true; }
            }
            return false;
        }

        void TeleportToWaypoint()
        {
            const Blip blip = n::GET_FIRST_BLIP_INFO_ID(8);
            if (!blip || !n::DOES_BLIP_EXIST(blip))
            {
                ui::Notify("Поставьте метку на карте (Esc → Карта), затем F5.");
                return;
            }
            const Vector3 target = n::GET_BLIP_INFO_ID_COORD(blip);
            const Ped ped = n::PLAYER_PED_ID();
            const Vector3 back = n::GET_ENTITY_COORDS(ped, TRUE);
            n::FREEZE_ENTITY_POSITION(ped, TRUE);
            float z = 0;
            const bool found = FindGround(target.x, target.y, z);
            n::FREEZE_ENTITY_POSITION(ped, FALSE);
            if (!found)
            {
                n::SET_ENTITY_COORDS_NO_OFFSET(ped, back.x, back.y, back.z, FALSE, FALSE, FALSE);
                ui::Notify("Не удалось найти землю у метки.");
                return;
            }
            // Сервер проверяет право (tpm) и сам присылает TP — локальный поиск земли только помогает.
            g_net.Send({ "TPM", F(target.x), F(target.y), F(z) });
        }

        void NoClipTick()
        {
            const Ped ped = n::PLAYER_PED_ID();
            const Vehicle veh = n::GET_VEHICLE_PED_IS_IN(ped, FALSE);
            const Entity e = veh ? veh : ped;
            const Vector3 p = n::GET_ENTITY_COORDS(e, TRUE);
            const Vector3 rot = n::GET_GAMEPLAY_CAM_ROT(2);
            const float pitch = rot.x * 3.14159265f / 180.f, yaw = rot.z * 3.14159265f / 180.f;
            const float fx = -std::sin(yaw) * std::cos(pitch), fy = std::cos(yaw) * std::cos(pitch), fz = std::sin(pitch);
            const float rx = std::cos(yaw), ry = std::sin(yaw);
            for (int c : { 30, 31, 32, 33, 34, 35, 21, 22, 36, 44, 38 }) n::DISABLE_CONTROL_ACTION(0, c, TRUE);
            float speed = 0.6f;
            if (n::IS_DISABLED_CONTROL_PRESSED(0, 21)) speed = 3.0f;   // Shift — быстрее
            if (n::IS_DISABLED_CONTROL_PRESSED(0, 36)) speed = 0.15f;  // Ctrl — медленнее
            float mx = 0, my = 0, mz = 0;
            if (!ui::InputActive())
            {
                if (n::IS_DISABLED_CONTROL_PRESSED(0, 32)) { mx += fx; my += fy; mz += fz; } // W
                if (n::IS_DISABLED_CONTROL_PRESSED(0, 33)) { mx -= fx; my -= fy; mz -= fz; } // S
                if (n::IS_DISABLED_CONTROL_PRESSED(0, 34)) { mx -= rx; my -= ry; }           // A
                if (n::IS_DISABLED_CONTROL_PRESSED(0, 35)) { mx += rx; my += ry; }           // D
                if (n::IS_DISABLED_CONTROL_PRESSED(0, 22)) mz += 1.f;                        // Пробел — вверх
                if (n::IS_DISABLED_CONTROL_PRESSED(0, 44)) mz -= 1.f;                        // Q — вниз
            }
            n::SET_ENTITY_VELOCITY(e, 0, 0, 0);
            n::SET_ENTITY_ROTATION(e, 0, 0, rot.z, 2, TRUE);
            n::SET_ENTITY_COORDS_NO_OFFSET(e, p.x + mx * speed, p.y + my * speed, p.z + mz * speed, FALSE, FALSE, FALSE);
        }

        void SetNoClip(bool on, bool notifyServer)
        {
            const Ped ped = n::PLAYER_PED_ID();
            const Vehicle veh = n::GET_VEHICLE_PED_IS_IN(ped, FALSE);
            const Entity e = veh ? veh : ped;
            g_noclip = on;
            if (!on)
            {
                // Выключили в воздухе — ставим на землю, а не роняем с высоты.
                const Vector3 p = n::GET_ENTITY_COORDS(e, TRUE);
                float ground = 0;
                if (n::GET_GROUND_Z_FOR_3D_COORD(p.x, p.y, p.z + 2.f, &ground, FALSE) && p.z - ground > 1.5f)
                    n::SET_ENTITY_COORDS_NO_OFFSET(e, p.x, p.y, ground + (veh ? 0.5f : 1.0f), FALSE, FALSE, FALSE);
            }
            n::FREEZE_ENTITY_POSITION(e, on);
            n::SET_ENTITY_COLLISION(e, !on, !on);
            n::SET_ENTITY_INVINCIBLE(ped, on || g_god);
            if (on) n::SET_ENTITY_ALPHA(ped, 150, FALSE); else n::RESET_ENTITY_ALPHA(ped);
            if (notifyServer) g_net.Send({ "NOCLIP", on ? "1" : "0" });
            ui::Notify(on ? "NoClip включён: WASD, Пробел/Q — выше/ниже, Shift — быстро, Ctrl — медленно"
                          : "NoClip выключен", 2500);
        }

        bool Allowed(const char* command) { return g_allowed.count(command) > 0; }

        /// Заголовок окна игры: window.title из client.cfg, {server} — имя сервера.
        std::string WindowTitle()
        {
            if (!g_welcomed) return "FloV Multiplayer";
            std::string t = settings::Get("window.title");
            if (t.empty()) t = "FloV Multiplayer";
            const auto at = t.find("{server}");
            if (at != std::string::npos) t.replace(at, 8, g_serverName.empty() ? std::string("сервер") : g_serverName);
            return t.empty() ? "FloV Multiplayer" : t;
        }

        /// Наблюдение администратора (/sp): мы невидимы и привязаны за спиной цели.
        void StopSpectate()
        {
            const Ped me = n::PLAYER_PED_ID();
            n::DETACH_ENTITY(me, TRUE, TRUE);
            n::FREEZE_ENTITY_POSITION(me, FALSE);
            n::SET_ENTITY_COLLISION(me, TRUE, TRUE);
            n::SET_ENTITY_VISIBLE(me, TRUE, FALSE);
            n::RESET_ENTITY_ALPHA(me);
            n::SET_ENTITY_INVINCIBLE(me, g_god);
            g_spectateTarget = 0;
            g_spectateAttached = 0;
        }

        void StartSpectate(int id)
        {
            auto it = g_remotes.find(id);
            if (it == g_remotes.end() || !it->second.hasState) { ui::Notify("Игрок не найден рядом.", 2500); return; }
            const Ped me = n::PLAYER_PED_ID();
            g_spectateTarget = id;
            const auto& st = it->second.cur;
            n::SET_ENTITY_COORDS_NO_OFFSET(me, st.x, st.y, st.z + 2.f, FALSE, FALSE, FALSE);
            n::FREEZE_ENTITY_POSITION(me, TRUE);
            n::SET_ENTITY_COLLISION(me, FALSE, FALSE);
            n::SET_ENTITY_VISIBLE(me, FALSE, FALSE);
            n::SET_ENTITY_ALPHA(me, 0, FALSE);
            n::SET_ENTITY_INVINCIBLE(me, TRUE);
        }

        void SpectateTick()
        {
            auto it = g_remotes.find(g_spectateTarget);
            if (it == g_remotes.end()) { StopSpectate(); return; }
            const Ped me = n::PLAYER_PED_ID();
            const Remote& r = it->second;
            const Entity target = r.veh && n::DOES_ENTITY_EXIST(r.veh) && (r.cur.flags & FInVehicle) ? r.veh : r.ped;
            if (!target || !n::DOES_ENTITY_EXIST(target)) return; // догружается
            if (g_spectateAttached == target) return;
            g_spectateAttached = target; // цель пересела в машину или пересоздана — привязываемся заново
            n::ATTACH_ENTITY_TO_ENTITY(me, target, 0, 0.f, -1.8f, 1.2f, 0.f, 0.f, 0.f, FALSE, FALSE, FALSE, FALSE, 2, TRUE);
        }

        // Клавиши, о которых просил сервер (flovmp:keys:bind): код клавиши → имя.
        std::vector<std::pair<int, std::string>> g_serverKeys;

        int KeyFromName(const std::string& v)
        {
            if (v.size() == 1 && ((v[0] >= 'A' && v[0] <= 'Z') || (v[0] >= '0' && v[0] <= '9'))) return v[0];
            if (v.size() >= 2 && v[0] == 'F') { const int k = std::atoi(v.c_str() + 1); if (k >= 1 && k <= 12) return 0x70 + k - 1; }
            return 0;
        }

        void ApplyHotkeys()
        {
            std::vector<int> keys = { g_cfg.espKey, g_cfg.noclipKey, g_cfg.waypointKey };
            for (const auto& [vk, name] : g_serverKeys) keys.push_back(vk);
            ui::SetHotkeys(std::move(keys));
        }

        void ResetSession()
        {
            world::Clear();
            ui::CloseMenu();
            g_serverKeys.clear();
            for (auto& [id, r] : g_remotes) DestroyRemote(r);
            g_remotes.clear();
            g_roster.clear();
            g_allowed.clear();
            if (g_noclip) SetNoClip(false, false);
            if (g_spectateTarget) StopSpectate();
            g_welcomed = g_readySent = g_spawnedOnce = false;
            g_adminLevel = 0;
            g_espMode = 0;
            g_frozen = false;
            g_serverName.clear();
            g_voiceToken.clear();
            voice::Stop();
            ui::SetChatEnabled(false);
            ui::SetAdminLevel(0);
            ui::SetLabels({});
            ui::SetWatermark("");
            ui::SetMicIndicator(0);
            ui::HideLoading();
            g_loadingActive = false;
            g_spawnAt = 0;
            ui::SetWindowTitle(WindowTitle());
        }

        void StartConnect(std::string host, int port, const std::string& name, int altPort = 0,
                          const std::string& typed = "")
        {
            ResetSession();
            g_host = host;
            g_port = port;
            g_altPort = altPort;
            g_typedAddress = typed.empty() ? host + ":" + std::to_string(port) : typed;
            g_pendingName = SanitizeName(name.empty() ? std::string(n::GET_PLAYER_NAME(n::PLAYER_ID())) : name);
            g_myName = g_pendingName;
            if (g_cfg.loading)
            {
                ui::ShowLoading(g_typedAddress);
                ui::LoadingStep("Устанавливаем соединение…", 10);
                g_loadingActive = true;
                g_loadStart = GetTickCount64();
            }
            else ui::Notify("FloV:MP: подключение к " + host + ":" + std::to_string(port) + "…", 6000);
            g_net.Connect(host, port, g_pendingName);

            // Запомнить сервер для окна F9.
            FILE* f = nullptr;
            if (_wfopen_s(&f, (DataDir() + L"\\last-server.txt").c_str(), L"wb") == 0 && f)
            {
                fprintf(f, "%s\n%s\n", g_typedAddress.c_str(), g_pendingName.c_str());
                fclose(f);
            }
        }

        /// Адрес, который вводит игрок, — адрес игры ("host" или "host:7788"):
        /// шлюз клиентов b3889 слушает порт игры + 10. altPort — введённый порт
        /// как есть, на случай если игроку дали сразу порт шлюза.
        /// gameAddress = false — адрес уже указывает на шлюз (connect.txt от коннектора).
        bool ParseAddress(const std::string& text, std::string& host, int& port, int* altPort = nullptr,
                          bool gameAddress = true)
        {
            if (altPort) *altPort = 0;
            std::string t = text;
            t.erase(std::remove_if(t.begin(), t.end(), [](char c) { return c == ' '; }), t.end());
            if (t.rfind("flovmp://", 0) == 0) t = t.substr(9);
            if (t.empty()) return false;
            port = kDefaultNativePort;
            const auto colon = t.rfind(':');
            if (colon != std::string::npos && t.find(':') == colon)
            {
                port = ToInt(t.substr(colon + 1), 0);
                t = t.substr(0, colon);
            }
            if (port <= 0 || port > 65535 || t.empty()) return false;
            if (gameAddress && colon != std::string::npos && t.find(':') == std::string::npos)
            {
                if (altPort) *altPort = port;
                port += 10;
                if (port > 65535) return false;
            }
            host = t;
            return true;
        }

        std::vector<std::string> SplitTips(const std::string& text)
        {
            std::vector<std::string> out;
            std::string cur;
            for (char c : text + "|")
            {
                if (c != '|') { cur += c; continue; }
                while (!cur.empty() && cur.front() == ' ') cur.erase(cur.begin());
                while (!cur.empty() && cur.back() == ' ') cur.pop_back();
                if (!cur.empty()) out.push_back(cur);
                cur.clear();
            }
            return out;
        }

        /// Применить настройки владельца (CFG при входе и после reloadsettings).
        void ApplySettings()
        {
            using namespace settings;
            auto& c = g_cfg;
            c.peds = Bool("world.peds"); c.traffic = Bool("world.traffic"); c.parked = Bool("world.parked_vehicles");
            c.police = Bool("world.police"); c.ambient = Bool("world.ambient_events"); c.freezeTime = Bool("world.freeze_time");
            c.minimap = Bool("hud.minimap"); c.abilityBar = Bool("hud.ability_bar"); c.areaNames = Bool("hud.area_names");
            c.vehicleNames = Bool("hud.vehicle_names"); c.weaponWheel = Bool("hud.weapon_wheel"); c.pauseMenu = Bool("hud.pause_menu");
            c.playerBlips = Bool("hud.player_blips"); c.watermark = Bool("hud.watermark");
            c.tags = Bool("nametags.enabled"); c.tagDistance = std::clamp(Float("nametags.distance", 30.f), 3.f, 500.f);
            c.tagId = Bool("nametags.show_id"); c.tagHealth = Bool("nametags.health"); c.tagArmor = Bool("nametags.armor");
            c.tagVoice = Bool("nametags.voice_icon"); c.tagAdmin = Bool("nametags.admin_badge");
            c.tagColor = Rgb("nametags.color"); c.tagSpace = Bool("nametags.underscore_to_space");
            c.chat = Bool("chat.enabled"); c.voice = Bool("voice.enabled"); c.loading = Bool("loading.enabled");
            c.vehPower = std::clamp(Float("vehicles.power", 1.f), 0.1f, 10.f);
            c.vehTorque = std::clamp(Float("vehicles.torque", 1.f), 0.1f, 10.f);
            c.voiceKey = Key("voice.key", 'N'); c.espKey = Key("keys.esp", VK_F3);
            c.noclipKey = Key("keys.noclip", VK_F4); c.waypointKey = Key("keys.waypoint", VK_F5);

            ui::SetInputKeys(Key("keys.chat", 'T'), Key("keys.console", VK_F8));
            ApplyHotkeys();
            ui::SetConsoleEnabled(Bool("console.enabled"));
            ui::SetConsoleTheme(Get("console.theme"));
            ui::SetAccent(Rgb("hud.accent"));
            ui::SetBrand(Get("branding.name"));
            ui::SetLoadingStyle(SplitTips(Get("loading.tips")), Rgb("loading.accent", 0xFBBF24));
            if (!g_welcomed) return;

            ui::SetChatEnabled(c.chat);
            ui::SetWindowTitle(WindowTitle());
            ApplyWorldSettings();
            for (auto& [id, r] : g_remotes)
            {
                if (c.playerBlips) AddRemoteBlip(r);
                else if (r.blip) { n::REMOVE_BLIP(&r.blip); r.blip = 0; }
            }
            g_voiceRadius = std::clamp(Float("voice.radius", g_voiceRadius), 3.f, 500.f);
            if (!c.voice && voice::Running()) voice::Stop();
            else if (c.voice && !voice::Running() && !g_voiceToken.empty())
                voice::Start(g_host, g_voicePort, g_voiceToken, g_voiceRadius);
        }

        void HandleMessage(const std::vector<std::string>& m)
        {
            const std::string& type = m[0];
            auto at = [&m](size_t i) -> std::string { return i < m.size() ? m[i] : std::string(); };
            if (world::Handle(m)) return;

            if (type == "PSTATE" && m.size() >= 20)
            {
                const int id = ToInt(m[1]);
                if (id == g_myId || id <= 0) return;
                auto& r = g_remotes[id];
                r.id = id;
                r.prev = r.cur;
                State& s = r.cur;
                s.x = ToFloat(m[2]); s.y = ToFloat(m[3]); s.z = ToFloat(m[4]); s.heading = ToFloat(m[5]);
                s.vx = ToFloat(m[6]); s.vy = ToFloat(m[7]); s.vz = ToFloat(m[8]); s.flags = ToInt(m[9]);
                s.vehModel = ToUInt(m[10]); s.vehOwner = ToInt(m[11]); s.seat = ToInt(m[12], -1);
                s.rx = ToFloat(m[13]); s.ry = ToFloat(m[14]); s.rz = ToFloat(m[15]);
                s.health = ToInt(m[16]); s.armor = ToInt(m[17]); s.weapon = ToUInt(m[18]); s.speed = ToFloat(m[19]);
                s.pedModel = ToUInt(at(20));
                s.received = GetTickCount64();
                if (!r.hasState) r.prev = r.cur;
                r.hasState = true;
            }
            else if (type == "PADD")
            {
                const int id = ToInt(at(1));
                if (id <= 0 || id == g_myId) return;
                auto& r = g_remotes[id];
                r.id = id;
                r.name = at(2);
            }
            else if (type == "PDEL")
            {
                auto it = g_remotes.find(ToInt(at(1)));
                if (it != g_remotes.end()) { DestroyRemote(it->second); g_remotes.erase(it); }
                voice::Forget((uint32_t)ToInt(at(1)));
            }
            else if (type == "MSG") SendChatLine(at(1), at(2), at(3));
            else if (type == "WELCOME")
            {
                g_myId = ToInt(at(1));
                g_myName = at(2);
                g_serverName = at(4);
                g_welcomed = true;
                g_welcomeAt = GetTickCount64();
                PrepareWorldOnce();
                ui::SetChatEnabled(g_cfg.chat);
                ui::SetWindowTitle(WindowTitle());
                // Голос: токен и порт из WELCOME (старый сервер их не шлёт — голоса нет).
                g_voiceRadius = std::max(3.f, ToFloat(at(7), 25.f));
                g_micWarned = false;
                g_voiceToken = at(5);
                g_voicePort = ToInt(at(6), g_port);
                if (!g_voiceToken.empty() && g_cfg.voice) voice::Start(g_host, g_voicePort, g_voiceToken, g_voiceRadius);
                if (g_loadingActive)
                {
                    const auto title = settings::Get("loading.title");
                    ui::ShowLoading(title.empty() ? (g_serverName.empty() ? std::string("Сервер FloV:MP") : g_serverName) : title);
                    ui::LoadingStep("Готовим мир…", 55);
                }
                else ui::Notify("Добро пожаловать на " + (g_serverName.empty() ? std::string("сервер") : g_serverName) +
                                "! T — чат, /help — команды.", 5000);
                Log("welcome id=" + at(1) + " identity=" + at(3));
            }
            else if (type == "SPAWN")
            {
                const float x = ToFloat(at(1)), y = ToFloat(at(2)), z = ToFloat(at(3)), h = ToFloat(at(4));
                const Hash model = ToUInt(at(5), kFreemodeMale);
                if (n::GET_ENTITY_MODEL(n::PLAYER_PED_ID()) != model) ApplyModel(model);
                Resurrect(x, y, z, h);
                g_spawnedOnce = true;
                if (g_loadingActive && !g_spawnAt)
                {
                    g_spawnAt = GetTickCount64();
                    ui::LoadingStep("Загружаем окрестности…", 80);
                }
            }
            else if (type == "TP") Teleport(ToFloat(at(1)), ToFloat(at(2)), ToFloat(at(3)));
            else if (type == "HEADING") n::SET_ENTITY_HEADING(n::PLAYER_PED_ID(), ToFloat(at(1)));
            else if (type == "HEALTH")
            {
                const int h = ToInt(at(1), 200);
                const Ped ped = n::PLAYER_PED_ID();
                if (h > 0 && n::IS_ENTITY_DEAD(ped))
                {
                    const Vector3 p = n::GET_ENTITY_COORDS(ped, TRUE);
                    Resurrect(p.x, p.y, p.z, n::GET_ENTITY_HEADING(ped));
                }
                n::SET_ENTITY_HEALTH(ped, h);
                g_lastHealth = h;
            }
            else if (type == "ARMOR") { n::SET_PED_ARMOUR(n::PLAYER_PED_ID(), ToInt(at(1))); g_lastArmor = ToInt(at(1)); }
            else if (type == "MODEL")
            {
                if (ApplyModel(ToUInt(at(1), kFreemodeMale), at(2)) && at(2).size())
                    Chat("{34d399}Модель персонажа: " + at(2) + ".");
            }
            else if (type == "WEATHER")
            {
                const std::string w = at(1);
                n::CLEAR_OVERRIDE_WEATHER();
                n::SET_WEATHER_TYPE_NOW_PERSIST(const_cast<char*>(w.c_str()));
                n::SET_OVERRIDE_WEATHER(const_cast<char*>(w.c_str()));
            }
            else if (type == "TIME")
            {
                const int h = ToInt(at(1)), mnt = ToInt(at(2));
                n::SET_CLOCK_TIME(h, mnt, 0);
                n::NETWORK_OVERRIDE_CLOCK_TIME(h, mnt, 0);
            }
            else if (type == "FREEZE")
            {
                g_frozen = at(1) == "1";
                n::FREEZE_ENTITY_POSITION(n::PLAYER_PED_ID(), g_frozen);
            }
            else if (type == "GOD")
            {
                g_god = at(1) == "1";
                n::SET_PLAYER_INVINCIBLE(n::PLAYER_ID(), g_god);
                n::SET_ENTITY_INVINCIBLE(n::PLAYER_PED_ID(), g_god);
            }
            else if (type == "SPEED") n::SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER(n::PLAYER_ID(), std::clamp(ToFloat(at(1), 1.f), 1.f, 1.49f));
            else if (type == "WEAPON")
            {
                // Имя оружия приходит от игрока — проверяем, что такое в игре есть,
                // иначе команда молча ничего не делала, а сервер писал «выдано».
                const Hash weapon = ToUInt(at(1));
                if (!n::IS_WEAPON_VALID(weapon)) Chat("{ef4444}Оружие «" + at(4) + "» не найдено в игре.");
                else
                {
                    n::GIVE_WEAPON_TO_PED(n::PLAYER_PED_ID(), weapon, std::clamp(ToInt(at(2), 250), 1, 9999), FALSE, at(3) != "0");
                    Chat("{34d399}Выдано оружие: " + at(4) + " (патронов: " + std::to_string(std::clamp(ToInt(at(2), 250), 1, 9999)) + ").");
                }
            }
            else if (type == "DISARM") n::REMOVE_ALL_PED_WEAPONS(n::PLAYER_PED_ID(), TRUE);
            else if (type == "REVIVE")
            {
                const Ped ped = n::PLAYER_PED_ID();
                const Vector3 p = n::GET_ENTITY_COORDS(ped, TRUE);
                Resurrect(p.x, p.y, p.z, n::GET_ENTITY_HEADING(ped));
            }
            else if (type == "NOCLIP")
            {
                // Сервер просит переключить (команда /noclip) — проверка права уже на сервере.
                const bool on = at(1).empty() ? !g_noclip : at(1) == "1";
                if (on != g_noclip) SetNoClip(on, true);
            }
            else if (type == "ESP")
            {
                g_espMode = at(1).empty() ? (g_espMode ? 0 : 1) : std::clamp(ToInt(at(1)), 0, 3);
                NotifyEsp();
            }
            else if (type == "REQTPM") TeleportToWaypoint();
            else if (type == "CLEARCHAT") ui::ClearChat();
            else if (type == "ADMIN")
            {
                g_adminLevel = ToInt(at(1));
                ui::SetAdminLevel(g_adminLevel);
                if (g_adminLevel <= 0 && g_noclip) SetNoClip(false, false);
                if (g_adminLevel <= 0) g_espMode = 0;
                if (g_adminLevel <= 0 && g_spectateTarget) StopSpectate();
            }
            else if (type == "CMDS")
            {
                // CMDS имена,через,запятую [имя описание имя описание ...] —
                // описания для подсказок чата и консоли (старый сервер их не шлёт).
                g_allowed.clear();
                std::vector<ui::Command> list;
                std::map<std::string, std::string> descs;
                for (size_t i = 2; i + 1 < m.size(); i += 2) descs[m[i]] = m[i + 1];
                std::string cur;
                for (char c : at(1) + ",")
                {
                    if (c == ',') { if (!cur.empty()) { g_allowed.insert(cur); list.push_back({ cur, descs[cur] }); } cur.clear(); }
                    else cur += c;
                }
                ui::SetCommands(std::move(list));
                // Права сняли — выключить то, что было включено по праву.
                if (!Allowed("noclip") && !Allowed("fly") && g_noclip) SetNoClip(false, false);
                if (!Allowed("esp")) g_espMode = 0;
            }
            else if (type == "ROSTER")
            {
                g_roster.clear();
                std::string cur;
                for (char c : at(1) + ",")
                {
                    if (c != ',') { cur += c; continue; }
                    const auto colon = cur.find(':');
                    if (colon != std::string::npos) g_roster[ToInt(cur.substr(0, colon))] = ToInt(cur.substr(colon + 1));
                    cur.clear();
                }
            }
            else if (type == "CAR") SpawnCar(ToUInt(at(1)), at(2));
            else if (type == "FIXCAR")
            {
                const Vehicle veh = n::GET_VEHICLE_PED_IS_IN(n::PLAYER_PED_ID(), FALSE);
                if (veh) { n::SET_VEHICLE_FIXED(veh); n::SET_VEHICLE_DIRT_LEVEL(veh, 0.f); n::SET_VEHICLE_ENGINE_HEALTH(veh, 1000.f); n::SET_VEHICLE_BODY_HEALTH(veh, 1000.f); }
            }
            else if (type == "DELCAR")
            {
                Vehicle veh = n::GET_VEHICLE_PED_IS_IN(n::PLAYER_PED_ID(), FALSE);
                if (!veh) veh = g_spawnedCar;
                if (veh && FindByEntity(veh)) { Chat("{fde047}[Транспорт] Это транспорт другого игрока."); return; }
                if (veh && n::DOES_ENTITY_EXIST(veh))
                {
                    if (veh == g_spawnedCar) g_spawnedCar = 0;
                    if (veh == g_lastOwnVehicle) g_lastOwnVehicle = 0;
                    DeleteEntity(veh);
                    Chat("{34d399}[Транспорт] Транспортное средство удалено.");
                }
                else Chat("{fde047}[Транспорт] Сядьте в транспорт, чтобы удалить его.");
            }
            else if (type == "ENGINE")
            {
                const Ped ped = n::PLAYER_PED_ID();
                const Vehicle veh = n::GET_VEHICLE_PED_IS_IN(ped, FALSE);
                if (!veh || SeatOf(ped, veh) != -1) { Chat("{fde047}[Транспорт] Управлять зажиганием может только водитель."); return; }
                const bool on = !n::GET_IS_VEHICLE_ENGINE_RUNNING(veh);
                n::SET_VEHICLE_ENGINE_ON(veh, on, TRUE, TRUE);
                Chat(on ? "{34d399}[Транспорт] Двигатель заведён." : "{34d399}[Транспорт] Двигатель заглушен.");
            }
            else if (type == "LOCK")
            {
                const Vehicle veh = g_spawnedCar && n::DOES_ENTITY_EXIST(g_spawnedCar) ? g_spawnedCar : g_lastOwnVehicle;
                if (!veh || !n::DOES_ENTITY_EXIST(veh)) { Chat("{fde047}[Транспорт] Поблизости нет вашего транспорта."); return; }
                g_carLocked = !g_carLocked;
                n::SET_VEHICLE_DOORS_LOCKED(veh, g_carLocked ? 2 : 1);
                Chat(g_carLocked ? "{f87171}[Транспорт] Двери заблокированы." : "{34d399}[Транспорт] Двери разблокированы.");
            }
            else if (type == "DAMAGE")
            {
                const int amount = std::clamp(ToInt(at(1)), 0, 200);
                const Ped ped = n::PLAYER_PED_ID();
                if (g_god || n::IS_ENTITY_DEAD(ped)) return;
                int armor = n::GET_PED_ARMOUR(ped), health = n::GET_ENTITY_HEALTH(ped), left = amount;
                const int absorbed = std::min(armor, left);
                armor -= absorbed; left -= absorbed;
                health = std::max(0, health - left);
                n::SET_PED_ARMOUR(ped, armor);
                n::SET_ENTITY_HEALTH(ped, health < 100 ? 0 : health);
                g_lastHealth = health < 100 ? 0 : health;
                g_lastArmor = armor;
                g_lastAttacker = ToInt(at(2));
                g_lastAttackAt = GetTickCount64();
            }
            else if (type == "PONG")
            {
                const ULONGLONG sent = _strtoui64(at(1).c_str(), nullptr, 10);
                if (sent) g_net.SetPing((int)std::min<ULONGLONG>(GetTickCount64() - sent, 9999));
            }
            else if (type == "KICK" || type == "REJECT")
            {
                Chat("{ef4444}[FloV:MP] " + at(1));
            }
            else if (type == "NET_FAILED" || type == "NET_CLOSED")
            {
                const bool kicked = type == "NET_CLOSED" && at(2) == "1";
                const bool refused = type == "NET_FAILED" && at(1).find("не отвечает") != std::string::npos;
                const bool notFlov = type == "NET_FAILED" && (at(1).find("не сервер FloV:MP") != std::string::npos ||
                                                                 at(1).find("закрыл соединение при входе") != std::string::npos);
                // Порт игры + 10 не ответил — возможно, игроку дали сразу порт шлюза.
                if ((refused || notFlov) && g_altPort > 0 && !g_welcomed)
                {
                    g_port = g_altPort;
                    g_altPort = 0;
                    Log("пробую порт шлюза " + std::to_string(g_port));
                    g_net.Connect(g_host, g_port, g_pendingName);
                    return;
                }
                ResetSession();
                Chat(std::string(kicked ? "{ef4444}" : "{fde047}") + "[FloV:MP] " + at(1));
                ui::Notify(at(1) + "  (F9 — подключиться снова)", 9000);
            }
            else if (type == "CFG")
            {
                for (size_t i = 1; i + 1 < m.size(); i += 2) settings::Set(m[i], m[i + 1]);
                ApplySettings();
                Log("настройки сервера получены (" + std::to_string((m.size() - 1) / 2) + ")");
            }
            else if (type == "COPY")
            {
                CopyToClipboard(at(1).substr(0, 256));
                ui::Notify("Скопировано в буфер обмена: " + at(1), 2500);
            }
            else if (type == "SPECTATE")
            {
                const int id = ToInt(at(1));
                if (at(2) == "1" && id > 0) StartSpectate(id);
                else if (g_spectateTarget) StopSpectate();
            }
            else if (type == "EVENT") Log("событие сервера " + at(1));
            else if (type == "NOTIFY") ui::Notify(at(1).substr(0, 300), std::clamp(ToInt(at(2), 4000), 1000, 20000));
            else if (type == "MENU")
            {
                std::vector<ui::MenuItem> items;
                for (size_t i = 3; i < m.size() && items.size() < 200; i += 2)
                    items.push_back({ m[i].substr(0, 80), at(i + 1).substr(0, 300) });
                ui::OpenMenu(at(1), at(2).substr(0, 60), std::move(items));
            }
            else if (type == "MENUCLOSE") ui::CloseMenu();
            else if (type == "KEYS")
            {
                g_serverKeys.clear();
                size_t start = 0;
                const std::string list = at(1);
                while (start <= list.size() && g_serverKeys.size() < 32)
                {
                    const size_t end = std::min(list.find(',', start), list.size());
                    const std::string name = list.substr(start, end - start);
                    if (const int vk = KeyFromName(name)) g_serverKeys.push_back({ vk, name });
                    start = end + 1;
                }
                ApplyHotkeys();
            }
        }

        /// Попадания по чужим игрокам: урон считаем мы, применяет сервер.
        void DetectHits(Ped me)
        {
            for (auto& [id, r] : g_remotes)
            {
                if (!r.ped || r.dead || !n::DOES_ENTITY_EXIST(r.ped)) continue;
                const int h = n::GET_ENTITY_HEALTH(r.ped);
                if (h >= kRemotePedHealth) continue;
                const Vehicle myVeh = n::GET_VEHICLE_PED_IS_IN(me, FALSE);
                if (n::HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(r.ped, me, TRUE) ||
                    (myVeh && n::HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(r.ped, myVeh, TRUE)))
                {
                    const int damage = std::min(kRemotePedHealth - h, 200);
                    g_net.Send({ "HIT", std::to_string(id), std::to_string(n::GET_SELECTED_PED_WEAPON(me)), std::to_string(damage) });
                }
                n::SET_ENTITY_HEALTH(r.ped, kRemotePedHealth);
                n::CLEAR_ENTITY_LAST_DAMAGE_ENTITY(r.ped);
            }
        }

        /// Урон нам от чужих ped'ов — только через сервер (DAMAGE). Локальные
        /// попадания их копий откатываем, иначе урон считался бы дважды.
        void UndoRemoteDamage(Ped me)
        {
            if (n::IS_ENTITY_DEAD(me)) return;
            bool byRemote = false;
            for (auto& [id, r] : g_remotes)
            {
                if ((r.ped && n::HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(me, r.ped, TRUE)) ||
                    (r.veh && n::HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(me, r.veh, TRUE)))
                    byRemote = true;
            }
            if (byRemote)
            {
                n::SET_ENTITY_HEALTH(me, g_lastHealth);
                n::SET_PED_ARMOUR(me, g_lastArmor);
                n::CLEAR_ENTITY_LAST_DAMAGE_ENTITY(me);
            }
            g_lastHealth = n::GET_ENTITY_HEALTH(me);
            g_lastArmor = n::GET_PED_ARMOUR(me);
        }

        void CheckDeath(Ped me)
        {
            const bool dead = n::IS_ENTITY_DEAD(me) != 0;
            if (dead && !g_localDead)
            {
                g_localDead = true;
                int killer = 0;
                if (Remote* r = FindByEntity(n::GET_PED_SOURCE_OF_DEATH(me))) killer = r->id;
                if (!killer && GetTickCount64() - g_lastAttackAt < 5000) killer = g_lastAttacker;
                g_net.Send({ "DIED", std::to_string(killer), std::to_string(n::GET_PED_CAUSE_OF_DEATH(me)) });
                if (g_noclip) SetNoClip(false, true);
            }
        }

        void NotifyEsp()
        {
            static const char* names[] = { "выключен", "игроки", "транспорт", "игроки и транспорт" };
            ui::Notify(std::string("ESP: ") + names[std::clamp(g_espMode, 0, 3)], 1500);
        }

        void CycleEsp()
        {
            g_espMode = (g_espMode + 1) % 4;
            NotifyEsp();
        }

        std::string DisplayName(const std::string& name)
        {
            if (!g_cfg.tagSpace) return name;
            std::string out = name;
            std::replace(out.begin(), out.end(), '_', ' ');
            return out;
        }

        int RosterLevel(int id)
        {
            auto it = g_roster.find(id);
            return it != g_roster.end() ? it->second : 0;
        }

        /// Ники над игроками («Nick Name (ID)», полоски, микрофон) и ESP администратора.
        void BuildLabels()
        {
            std::vector<ui::Label> labels;
            const bool espPlayers = g_espMode == 1 || g_espMode == 3;
            const bool espVehicles = g_espMode == 2 || g_espMode == 3;
            const Vector3 cam = n::GET_GAMEPLAY_CAM_COORD();
            world::AddLabels(labels, cam.x, cam.y, cam.z);
            if (!g_cfg.tags && !g_espMode) { ui::SetLabels(std::move(labels)); return; }
            const float tagDist = g_cfg.tagDistance;
            for (auto& [id, r] : g_remotes)
            {
                if (!r.hasState) continue;
                const bool hidden = (r.cur.flags & FNoClip) != 0;
                float x = r.cur.x, y = r.cur.y, z = r.cur.z;
                const bool spawned = r.ped && n::DOES_ENTITY_EXIST(r.ped);
                if (spawned)
                {
                    const Vector3 p = n::GET_ENTITY_COORDS(r.ped, TRUE);
                    x = p.x; y = p.y; z = p.z;
                }
                const float d = std::sqrt(Dist2(x, y, z, cam.x, cam.y, cam.z));
                const int level = RosterLevel(id);
                // Основатель (-1) скрыт от младших администраторов, как в alt:V-клиенте.
                const bool espShow = espPlayers && d < 250.f && level >= 0 && !(g_adminLevel < 8 && level >= 8);
                const bool nametag = g_cfg.tags && !hidden && d < tagDist && spawned &&
                                     n::HAS_ENTITY_CLEAR_LOS_TO_ENTITY(n::PLAYER_PED_ID(), r.ped, 17);
                if (!nametag && !espShow) continue;
                float sx = 0, sy = 0;
                if (!n::GET_SCREEN_COORD_FROM_WORLD_COORD(x, y, z + 1.05f, &sx, &sy)) continue;
                ui::Label l;
                l.x = sx; l.y = sy;
                l.name = DisplayName(r.name);
                l.id = g_cfg.tagId || espShow ? id : -1;
                l.rgb = g_cfg.tagColor;
                const bool dead = (r.cur.flags & FDead) != 0;
                if (g_cfg.tagHealth || espShow) l.health = dead ? 0.f : std::clamp((r.cur.health - 100) / 100.f, 0.f, 1.f);
                if (g_cfg.tagArmor || espShow) l.armor = std::clamp(r.cur.armor / 100.f, 0.f, 1.f);
                l.speaking = g_cfg.tagVoice && voice::IsSpeaking((uint32_t)id);
                l.adminLevel = g_cfg.tagAdmin ? level : 0;
                // Ближе 40% дистанции — полный размер, дальше плавно меньше и прозрачнее.
                const float k = std::clamp((d - tagDist * 0.4f) / std::max(1.f, tagDist * 0.6f), 0.f, 1.f);
                l.scale = 1.f - 0.2f * k;
                l.alpha = 1.f - 0.45f * k;
                if (espShow)
                {
                    l.scale = std::max(l.scale, 0.8f);
                    l.alpha = std::max(l.alpha, 0.85f);
                    if (level >= 8) l.rgb = 0xFFC740;
                    else if (level > 0) l.rgb = 0xF87171;
                    l.extra = std::to_string(std::max(0, r.cur.health - 100)) + " HP · " + std::to_string(r.cur.armor) + " AR · " +
                              std::to_string((int)d) + " м" + (hidden ? " · NoClip" : "") + (dead ? " · мёртв" : "");
                }
                labels.push_back(std::move(l));
            }
            if (espVehicles)
            {
                for (auto& [id, r] : g_remotes)
                {
                    if (!r.veh || !n::DOES_ENTITY_EXIST(r.veh)) continue;
                    const Vector3 p = n::GET_ENTITY_COORDS(r.veh, TRUE);
                    const float d = std::sqrt(Dist2(p.x, p.y, p.z, cam.x, cam.y, cam.z));
                    if (d > 180.f) continue;
                    float sx = 0, sy = 0;
                    if (!n::GET_SCREEN_COORD_FROM_WORLD_COORD(p.x, p.y, p.z + 0.6f, &sx, &sy)) continue;
                    ui::Label l;
                    l.x = sx; l.y = sy;
                    l.name = "Транспорт игрока " + DisplayName(r.name);
                    l.rgb = 0x7DD3FC;
                    const char* plate = n::GET_VEHICLE_NUMBER_PLATE_TEXT(r.veh);
                    l.extra = std::string(plate ? plate : "") + " · " + std::to_string((int)n::GET_VEHICLE_ENGINE_HEALTH(r.veh)) + " HP · " +
                              std::to_string((int)(n::GET_ENTITY_SPEED(r.veh) * 3.6f)) + " км/ч · " + std::to_string((int)d) + " м";
                    l.scale = 0.85f;
                    labels.push_back(std::move(l));
                }
            }
            ui::SetLabels(std::move(labels));
        }

        /// Разговор кнопкой из настроек (N), громкость и панорама собеседников.
        void VoiceTick(Ped me)
        {
            if (!voice::Running()) { ui::SetMicIndicator(0); return; }
            DWORD pid = 0;
            GetWindowThreadProcessId(GetForegroundWindow(), &pid);
            const bool focused = pid == GetCurrentProcessId();
            const bool talking = focused && !ui::InputActive() && (GetAsyncKeyState(g_cfg.voiceKey) & 0x8000) != 0;
            voice::SetTalking(talking);
            ui::SetMicIndicator(talking ? (voice::MicrophoneOk() ? 1 : 2) : 0);
            if (talking && !voice::MicrophoneOk() && !g_micWarned)
            {
                g_micWarned = true;
                ui::Notify("Микрофон: " + voice::LastError(), 6000);
            }

            const Vector3 cam = n::GET_GAMEPLAY_CAM_COORD();
            const Vector3 rot = n::GET_GAMEPLAY_CAM_ROT(2);
            const float yaw = rot.z * 3.14159265f / 180.f;
            const float fx = -std::sin(yaw), fy = std::cos(yaw); // взгляд камеры
            const Vector3 mine = n::GET_ENTITY_COORDS(me, TRUE);
            for (auto& [id, r] : g_remotes)
            {
                float x = r.cur.x, y = r.cur.y, z = r.cur.z;
                if (r.ped && n::DOES_ENTITY_EXIST(r.ped))
                {
                    const Vector3 p = n::GET_ENTITY_COORDS(r.ped, TRUE);
                    x = p.x; y = p.y; z = p.z;
                }
                const float d = std::sqrt(Dist2(x, y, z, mine.x, mine.y, mine.z));
                float gain = std::clamp(1.f - d / g_voiceRadius, 0.f, 1.f);
                gain = gain * gain * 1.2f; // ближе — заметно громче
                // Панорама: собеседник справа от камеры — в правом канале.
                const float dx = x - cam.x, dy = y - cam.y;
                const float len = std::sqrt(dx * dx + dy * dy);
                float pan = 0.f;
                if (len > 0.5f) pan = std::clamp((dx * fy - dy * fx) / len, -1.f, 1.f) * 0.8f;
                voice::SetGain((uint32_t)id, gain, pan);
            }
        }

        void CopyToClipboard(const std::string& text)
        {
            const auto w = FromUtf8(text);
            if (!OpenClipboard(nullptr)) return;
            EmptyClipboard();
            if (HGLOBAL h = GlobalAlloc(GMEM_MOVEABLE, (w.size() + 1) * sizeof(wchar_t)))
            {
                memcpy(GlobalLock(h), w.c_str(), (w.size() + 1) * sizeof(wchar_t));
                GlobalUnlock(h);
                if (!SetClipboardData(CF_UNICODETEXT, h)) GlobalFree(h);
            }
            CloseClipboard();
        }

        void OpenConnectWindow()
        {
            std::string host, name;
            FILE* f = nullptr;
            if (_wfopen_s(&f, (DataDir() + L"\\last-server.txt").c_str(), L"rb") == 0 && f)
            {
                char a[256]{}, b[128]{};
                if (fgets(a, sizeof a, f)) host = a;
                if (fgets(b, sizeof b, f)) name = b;
                fclose(f);
            }
            auto trim = [](std::string& s) { while (!s.empty() && (s.back() == '\n' || s.back() == '\r')) s.pop_back(); };
            trim(host); trim(name);
            if (name.empty()) name = n::GET_PLAYER_NAME(n::PLAYER_ID());
            ui::OpenConnectDialog(host, name);
        }

        /// Команды консоли F8: свои — здесь, остальные уходят на сервер как «/команда».
        void HandleConsoleCommand(const std::string& line)
        {
            std::string name = line.substr(0, line.find(' '));
            std::transform(name.begin(), name.end(), name.begin(), [](char c) { return (char)tolower((unsigned char)c); });
            if (name == "netgraph")
            {
                ui::SetNetgraph(!ui::Netgraph());
                ui::ConsoleLog("DEV", ui::Netgraph() ? "Netgraph включён" : "Netgraph выключен");
            }
            else if (name == "pos" || name == "coords")
            {
                const Ped ped = n::PLAYER_PED_ID();
                const Vector3 p = n::GET_ENTITY_COORDS(ped, TRUE);
                char buf[128];
                sprintf_s(buf, "%.2f, %.2f, %.2f, %.2f", p.x, p.y, p.z, n::GET_ENTITY_HEADING(ped));
                CopyToClipboard(buf);
                ui::ConsoleLog("DEV", std::string("Координаты скопированы в буфер: ") + buf);
            }
            else if (name == "reconnect")
            {
                if (g_host.empty()) { ui::ConsoleLog("WARN", "Сервер ещё не выбран — F9, чтобы подключиться."); return; }
                const std::string host = g_host, nm = g_myName;
                const int port = g_port;
                g_net.Disconnect("переподключение");
                StartConnect(host, port, nm);
            }
            else if (name == "connect") OpenConnectWindow();
            else if (name == "quit" || name == "exit" || name == "q")
            {
                g_net.Disconnect("выход из игры");
                Log("выход из игры по команде консоли");
                Sleep(150);
                TerminateProcess(GetCurrentProcess(), 0);
            }
            else if (name == "tpm" && Allowed("tpm")) TeleportToWaypoint();
            else if ((name == "noclip" || name == "fly") && (Allowed("noclip") || Allowed("fly"))) SetNoClip(!g_noclip, true);
            else if (name == "esp" && Allowed("esp"))
            {
                const auto sp = line.find(' ');
                if (sp != std::string::npos) { g_espMode = std::clamp(ToInt(line.substr(sp + 1)), 0, 3); NotifyEsp(); }
                else CycleEsp();
            }
            else if (!g_welcomed) ui::ConsoleLog("WARN", "Нет подключения к серверу — команда «" + name + "» не отправлена.");
            else g_net.Send({ "CHAT", ("/" + line).substr(0, 256) });
        }

        void HandleHotkeys()
        {
            for (int key : ui::TakeHotkeys())
            {
                if (key == ui::KeyConnect) { OpenConnectWindow(); continue; }
                if (!g_welcomed) continue;
                if (key == g_cfg.espKey && Allowed("esp")) CycleEsp();
                else if (key == g_cfg.noclipKey && (Allowed("noclip") || Allowed("fly"))) SetNoClip(!g_noclip, true);
                else if (key == g_cfg.waypointKey && Allowed("tpm")) TeleportToWaypoint();
                for (const auto& [vk, name] : g_serverKeys)
                    if (vk == key) g_net.Send({ "KEY", name });
            }

            for (const auto& ev : ui::TakeMenuEvents())
            {
                if (!g_welcomed) continue;
                if (ev.index < 0) g_net.Send({ "MENUCLOSED", ev.id });
                else g_net.Send({ "MENUSEL", ev.id, std::to_string(ev.index) });
            }

            for (const auto& line : ui::TakeConsoleCommands()) HandleConsoleCommand(line);

            std::string host, name;
            if (ui::TakeConnectRequest(host, name))
            {
                int port = 0;
                std::string h;
                int alt = 0;
                if (ParseAddress(host, h, port, &alt)) StartConnect(h, port, name, alt, host);
                else ui::Notify("Неверный адрес сервера. Пример: 127.0.0.1:7788", 4000);
            }

            const size_t maxLen = (size_t)std::clamp(settings::Int("chat.max_length", 256), 16, 1024);
            for (const auto& line : ui::TakeSubmittedChat())
            {
                if (!g_welcomed) continue;
                g_net.Send({ "CHAT", line.substr(0, maxLen) });
            }
        }

        /// Запрос на подключение от лаунчера/коннектора: переменная окружения
        /// FLOVMP_CONNECT или файл connect.txt (действует 10 минут, одноразовый).
        bool ReadConnectRequest(std::string& host, int& port, std::string& name)
        {
            char env[256]{};
            if (GetEnvironmentVariableA("FLOVMP_CONNECT", env, sizeof env) > 0 && ParseAddress(env, host, port, nullptr, true))
            {
                char nm[128]{};
                if (GetEnvironmentVariableA("FLOVMP_NAME", nm, sizeof nm) > 0) name = nm;
                return true;
            }
            const auto path = DataDir() + L"\\connect.txt";
            FILE* f = nullptr;
            if (_wfopen_s(&f, path.c_str(), L"rb") != 0 || !f) return false;
            std::string address;
            long long created = 0;
            char line[512];
            while (fgets(line, sizeof line, f))
            {
                std::string s(line);
                while (!s.empty() && (s.back() == '\n' || s.back() == '\r')) s.pop_back();
                const auto eq = s.find('=');
                if (eq == std::string::npos) continue;
                const auto key = s.substr(0, eq), value = s.substr(eq + 1);
                if (key == "address") address = value;
                else if (key == "name") name = value;
                else if (key == "created") created = _strtoi64(value.c_str(), nullptr, 10);
            }
            fclose(f);
            DeleteFileW(path.c_str());
            const long long now = (long long)time(nullptr);
            if (created <= 0 || now - created > 600)
            {
                Log("connect.txt устарел — игнорирую");
                return false;
            }
            return ParseAddress(address, host, port, nullptr, false); // коннектор пишет адрес шлюза
        }

        /// Загрузочный экран: закрыть, когда персонаж стоит в загруженном мире.
        void LoadingTick(ULONGLONG now)
        {
            if (!g_loadingActive) return;
            const bool timeout = now - g_loadStart > 30000;
            bool done = false;
            if (g_spawnAt)
            {
                const bool ground = n::HAS_COLLISION_LOADED_AROUND_ENTITY(n::PLAYER_PED_ID()) != 0;
                done = (ground && now - g_spawnAt > 700) || now - g_spawnAt > 6000;
            }
            else if (g_welcomed && g_readySent && now - g_welcomeAt > 5000) done = true; // сервер не прислал спавн
            if (!done && !timeout) return;
            if (timeout && !done) Log("загрузка не завершилась за 30 с — показываю игру как есть");
            ui::LoadingStep("Готово", 100);
            ui::HideLoading();
            g_loadingActive = false;
            ui::SetChatEnabled(g_welcomed && g_cfg.chat);
            if (g_welcomed)
                ui::Notify("Добро пожаловать на " + (g_serverName.empty() ? std::string("сервер") : g_serverName) + "! T — чат, /help — команды.", 5000);
        }

        /// Показатели для консоли F8 и netgraph (раз в полсекунды).
        void StatsTick(ULONGLONG now)
        {
            ++g_statsFrames;
            g_statsFrameSum += n::GET_FRAME_TIME();
            if (now - g_statsAt < 500) return;
            const float elapsed = g_statsAt ? (now - g_statsAt) / 1000.f : 0.5f;
            g_statsAt = now;
            ui::Stats st;
            st.fps = g_statsFrames / std::max(0.001f, elapsed);
            st.frameMs = g_statsFrames ? g_statsFrameSum / g_statsFrames * 1000.f : 0.f;
            g_statsFrames = 0;
            g_statsFrameSum = 0;
            const uint64_t in = g_bytesIn.load(), out = g_bytesOut.load();
            st.bytesIn = (uint64_t)((in - g_lastIn) / elapsed);
            st.bytesOut = (uint64_t)((out - g_lastOut) / elapsed);
            g_lastIn = in; g_lastOut = out;
            st.ping = g_net.PingMs();
            st.connected = g_welcomed;
            st.server = g_serverName;
            st.endpoint = g_net.Endpoint();
            st.online = g_welcomed ? (int)g_remotes.size() + 1 : 0;
            st.state = g_welcomed ? "в игре" : g_net.State() == NetState::Connecting ? "подключение…" : "нет подключения";
            const bool console = ui::ConsoleOpen();
            const Vector3 me = n::GET_ENTITY_COORDS(n::PLAYER_PED_ID(), TRUE);
            for (auto& [id, r] : g_remotes)
            {
                if (!r.ped) continue;
                ++st.streamed;
                if (!console) continue;
                const int dist = (int)std::sqrt(Dist2(r.cur.x, r.cur.y, r.cur.z, me.x, me.y, me.z));
                st.entities.push_back("[" + std::to_string(id) + "] " + r.name + " — " + std::to_string(dist) + " м · " +
                                      ((r.cur.flags & FInVehicle) ? "в транспорте" : "пешком") + " · " +
                                      std::to_string(std::max(0, r.cur.health - 100)) + " HP");
            }
            ui::SetStats(st);
            if (g_cfg.watermark && g_welcomed)
                ui::SetWatermark("FloV:MP  ·  " + (g_serverName.empty() ? g_net.Endpoint() : g_serverName) +
                                 "  ·  ID " + std::to_string(g_myId) + "  ·  " + std::to_string(g_net.PingMs()) + " мс");
            else ui::SetWatermark("");
        }

        void Tick()
        {
            const ULONGLONG now = GetTickCount64();
            HandleHotkeys();

            for (const auto& m : g_net.Poll())
                if (!m.empty()) HandleMessage(m);

            InputEveryFrame();
            StatsTick(now);
            if (!g_welcomed)
            {
                if (g_loadingActive && now - g_loadStart > 30000) LoadingTick(now);
                return;
            }
            const Ped me = n::PLAYER_PED_ID();
            WorldEveryFrame();

            // READY после первого спавна (или через 3 с, если сервер спавн не прислал).
            if (!g_readySent && (g_spawnedOnce || now - g_welcomeAt > 3000))
            {
                g_readySent = true;
                g_net.Send({ "READY" });
            }
            LoadingTick(now);

            if (g_noclip) NoClipTick();
            if (g_spectateTarget) SpectateTick();
            VoiceTick(me);
            CheckDeath(me);
            UndoRemoteDamage(me);
            DetectHits(me);
            const Vector3 myPos = n::GET_ENTITY_COORDS(me, TRUE);
            for (auto& [id, r] : g_remotes) UpdateRemote(r, now, myPos);
            world::Tick(myPos.x, myPos.y, myPos.z);

            if (now >= g_nextState && g_readySent)
            {
                g_nextState = now + kStateIntervalMs;
                SendLocalState();
            }
            if (now >= g_nextPing)
            {
                g_nextPing = now + 2000;
                g_net.Send({ "PING", std::to_string(now) });
            }
            if (n::IS_CUTSCENE_ACTIVE()) RestoreGameplayView();
            if (now >= g_nextClean)
            {
                // Сюжет мог запустить новый скрипт (например, после смерти) — снова остановить.
                if (StopStoryScripts() > 0) RestoreGameplayView();
                g_nextClean = now + 3000;
                if (!g_cfg.police)
                {
                    const Vector3 p = n::GET_ENTITY_COORDS(me, TRUE);
                    n::CLEAR_AREA_OF_COPS(p.x, p.y, p.z, 500.f, 0);
                }
            }
            BuildLabels();
        }
    }

    void ScriptMain()
    {
        Log("скрипт запущен, GTA5.exe " + GameVersion());
        ApplySettings(); // значения по умолчанию до первого CFG от сервера
        ui::SetWindowTitle("FloV Multiplayer");
        while (n::GET_IS_LOADING_SCREEN_ACTIVE() || !n::DOES_ENTITY_EXIST(n::PLAYER_PED_ID())) WAIT(250);

        if (GameVersion() != kGameVersion)
        {
            ui::Notify("FloV:MP: нужна GTA V Legacy " + std::string(kGameVersion) + ", у вас " + GameVersion() + ". Мультиплеер отключён.", 15000);
            Log("неподдерживаемая версия игры — сетевой режим выключен");
            for (;;) WAIT(1000);
        }

        std::string host, name;
        int port = 0;
        if (ReadConnectRequest(host, port, name)) StartConnect(host, port, name);
        else ui::Notify("FloV:MP загружен. F9 — подключиться к серверу.", 6000);

        for (;;)
        {
            Tick();
            WAIT(0);
        }
    }

    void Shutdown()
    {
        g_net.Disconnect("выгрузка клиента");
    }
}
