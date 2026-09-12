using System;
using System.Collections.Concurrent;
using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;

namespace FloVMP.Starter;

/// <summary>
/// Чистый ванильный стартер для клиентов FloV:MP с разграничением прав (RBAC):
/// - Обычные игроки: чистый спавн, чат, /pos, F8 консоль в режиме игрока (без админ-кнопок).
/// - Администраторы: F4 NoClip, /tpm, /car, /heal, /weather, /time, полная панель Дев-тулс в F8.
/// - 100% серверная валидация: любая попытка несанкционированного вызова админских событий
///   (teleportWaypoint, toggleNoClip, команды) строго блокируется на сервере.
/// </summary>
public class StarterResource : Resource
{
    private static readonly Position DefaultSpawnPosition = new(198.8f, -935.6f, 30.7f); // Легион Сквер
    private static readonly float DefaultSpawnHeading = 140f;
    private static readonly uint DefaultPlayerModel = Alt.Hash("mp_m_freemode_01");

    private static readonly string AdminPassword = Environment.GetEnvironmentVariable("FLOVMP_ADMIN_PASSWORD") ?? "flovmp2026";
    private readonly ConcurrentDictionary<uint, int> _adminLevels = new();

    private IVoiceChannel? _spatialVoiceChannel;

    public override void OnStart()
    {
        Alt.Log("[FloV:MP Starter] Чистый ванильный сервер успешно запущен!");
        Alt.Log("[FloV:MP Starter] Безопасность: Server-Side RBAC активна. Обычные игроки изолированы от админ-функций.");

        try
        {
            _spatialVoiceChannel = Alt.CreateVoiceChannel(true, 25.0f);
        }
        catch (Exception ex)
        {
            Alt.LogWarning($"[FloV:MP Starter] Войс-канал не активирован (проверьте voice-server): {ex.Message}");
        }

        Alt.OnPlayerConnect += OnPlayerConnect;
        Alt.OnPlayerDisconnect += OnPlayerDisconnect;
        Alt.OnClient<IPlayer, string>("chat:message", OnChatMessage);
        Alt.OnClient<IPlayer, string>("flovmp:chat:say", OnChatMessage);
        Alt.OnClient<IPlayer, float, float, float>("starter:teleportWaypoint", OnTeleportWaypoint);
        Alt.OnClient<IPlayer, bool>("starter:toggleNoClip", OnToggleNoClip);
        Alt.OnClient<IPlayer, bool>("flovmp:admin:noclip", OnToggleNoClip);
    }

    public override void OnStop()
    {
        Alt.Log("[FloV:MP Starter] Остановка ванильного стартера.");
    }

    public bool IsAdmin(IPlayer player, int minLevel = 1)
    {
        return _adminLevels.TryGetValue(player.Id, out var level) && level >= minLevel;
    }

    public void SendChatMessage(IPlayer player, string message, string kind = "system", string author = "")
    {
        if (player == null || !player.Exists) return;
        player.Emit("flovmp:chat:msg", kind, author, message);
    }

    public void BroadcastChatMessage(string message, string kind = "system", string author = "")
    {
        Alt.EmitAllClients("flovmp:chat:msg", kind, author, message);
    }

    private void OnPlayerConnect(IPlayer player, string reason)
    {
        Alt.Log($"[FloV:MP] Игрок {player.Name} (ID: {player.Id}) подключается...");

        // Чистый спавн игрока
        player.Model = DefaultPlayerModel;
        player.Spawn(DefaultSpawnPosition, 0);
        player.Rotation = new Rotation(0, 0, DefaultSpawnHeading);
        player.Health = 200;
        player.MaxHealth = 200;
        player.Armor = 100;

        // Локальный хост (127.0.0.1) автоматически получает максимальный уровень прав разработчика (8)
        var isAdminHost = player.Ip == "127.0.0.1" || player.Ip == "::1" || player.Ip == "localhost";
        var level = isAdminHost ? 8 : 0;
        _adminLevels[player.Id] = level;

        // Активация 3D войс-канала
        try
        {
            _spatialVoiceChannel?.AddPlayer(player);
        }
        catch
        {
        }

        SendChatMessage(player, "{ff3d8a}[FloV:MP]{ffffff} Добро пожаловать на сервер!");

        if (level > 0)
        {
            SendChatMessage(player, "{34d399}[Admin]{ffffff} Права администратора активированы (Уровень " + level + "). Доступны: /tpm, /pos, /car, /noclip, /heal, /armor, /god, /weather, /time, /fix, /tp, /goto, /gethere.");
        }
        else
        {
            SendChatMessage(player, "{a1a1aa}Доступна команда /pos для координат. Авторизация администратора: /adminauth <пароль>");
        }

        player.Emit("starter:initClient");
        player.Emit("flovmp:console:setAdmin", level);
    }

    private void OnPlayerDisconnect(IPlayer player, string reason)
    {
        Alt.Log($"[FloV:MP] Игрок {player.Name} (ID: {player.Id}) отключился ({reason}).");
        _adminLevels.TryRemove(player.Id, out _);
        _godModes.TryRemove(player.Id, out _);

        try
        {
            _spatialVoiceChannel?.RemovePlayer(player);
        }
        catch
        {
        }
    }

    private readonly ConcurrentDictionary<uint, bool> _godModes = new();

    private void OnChatMessage(IPlayer player, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        if (message.StartsWith("/"))
        {
            HandleCommand(player, message[1..]);
            return;
        }

        BroadcastChatMessage(message, "player", $"[{player.Id}] {player.Name}");
    }

    private void HandleCommand(IPlayer player, string commandLine)
    {
        var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var cmd = parts[0].ToLowerInvariant();
        switch (cmd)
        {
            case "help":
                SendChatMessage(player, "{38bdf8}─── СПИСОК КОМАНД СЕРВЕРА ───");
                SendChatMessage(player, "{e4e4e7}Чат и отыгровки: {a1a1aa}/me, /do, /b (OOC), /s (крик), /w <id> (шепот), /clear");
                SendChatMessage(player, "{e4e4e7}Общие: {a1a1aa}/pos (координаты), /adminauth <пароль>");
                if (IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{34d399}Администрация: {a1a1aa}/tpm, /tp <x y z>, /goto <id>, /gethere <id>, /car [модель], /fix, /noclip (F4), /heal, /armor, /god, /kill, /weather, /time, /speed, /dim, /skin, /kick, /a (админ-чат)");
                }
                if (IsAdmin(player, 8))
                {
                    SendChatMessage(player, "{fde047}Главный Администратор: {a1a1aa}/setadmin <id> <lvl 0-8>");
                }
                break;

            case "me":
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /me <действие персонажа>");
                    return;
                }
                var meAction = string.Join(' ', parts.Skip(1));
                BroadcastChatMessage(meAction, "me", player.Name);
                break;

            case "do":
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /do <описание ситуации/окружения>");
                    return;
                }
                var doAction = string.Join(' ', parts.Skip(1));
                BroadcastChatMessage(doAction, "do", player.Name);
                break;

            case "b":
            case "ooc":
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /b <OOC сообщение>");
                    return;
                }
                var oocText = string.Join(' ', parts.Skip(1));
                BroadcastChatMessage(oocText, "ooc", $"[{player.Id}] {player.Name}");
                break;

            case "s":
            case "shout":
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /s <крик>");
                    return;
                }
                var shoutText = string.Join(' ', parts.Skip(1));
                BroadcastChatMessage(shoutText, "shout", $"[{player.Id}] {player.Name}");
                break;

            case "w":
            case "whisper":
                if (parts.Length < 3 || !uint.TryParse(parts[1], out var wId))
                {
                    SendChatMessage(player, "{fde047}Использование: /w <ID игрока> <сообщение>");
                    return;
                }
                var wTarget = Alt.GetPlayerById(wId);
                if (wTarget == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                var wMsg = string.Join(' ', parts.Skip(2));
                SendChatMessage(wTarget, wMsg, "whisper", $"[{player.Id}] {player.Name}");
                SendChatMessage(player, $"[для [{wTarget.Id}] {wTarget.Name}]: {wMsg}", "whisper", "Вы");
                break;

            case "a":
            case "admin":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для использования админ-чата.");
                    return;
                }
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /a <сообщение для администрации>");
                    return;
                }
                var aMsg = string.Join(' ', parts.Skip(1));
                var adminLvl = _adminLevels.TryGetValue(player.Id, out var al) ? al : 1;
                foreach (var p in Alt.GetAllPlayers())
                {
                    if (IsAdmin(p, 1))
                    {
                        SendChatMessage(p, aMsg, "admin", $"[{player.Id}] {player.Name} (Ур.{adminLvl})");
                    }
                }
                break;

            case "pos":
            case "coords":
                SendChatMessage(player, $"{{38bdf8}}Координаты: X: {player.Position.X:F2}, Y: {player.Position.Y:F2}, Z: {player.Position.Z:F2}, Yaw: {player.Rotation.Yaw:F2}");
                player.Emit("starter:copyCoords", player.Position.X, player.Position.Y, player.Position.Z, player.Rotation.Yaw);
                break;

            case "adminauth":
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /adminauth <пароль>");
                    return;
                }
                if (parts[1] == AdminPassword)
                {
                    _adminLevels[player.Id] = 8;
                    player.Emit("flovmp:console:setAdmin", 8);
                    SendChatMessage(player, "{34d399}[FloV:MP Security] Авторизация успешна! Вам присвоен уровень Главного Администратора (8). Админ-команды в чате и F8 разблокированы.");
                    Alt.Log($"[Security] Игрок {player.Name} (ID: {player.Id}) успешно авторизовался как администратор.");
                }
                else
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] Неверный пароль администратора!");
                    Alt.LogWarning($"[Security Alert] Неудачная попытка авторизации /adminauth от {player.Name} (ID: {player.Id})");
                }
                break;

            case "setadmin":
                if (!IsAdmin(player, 8))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] Доступ запрещен (требуется Уровень 8).");
                    return;
                }
                if (parts.Length < 3 || !uint.TryParse(parts[1], out var targetId) || !int.TryParse(parts[2], out var targetLvl))
                {
                    SendChatMessage(player, "{fde047}Использование: /setadmin <ID> <Уровень 0-8>");
                    return;
                }
                var target = Alt.GetPlayerById(targetId);
                if (target == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                _adminLevels[target.Id] = targetLvl;
                target.Emit("flovmp:console:setAdmin", targetLvl);
                SendChatMessage(target, $"{{34d399}}[Admin] Администратор {player.Name} установил вам уровень доступа {targetLvl}.");
                SendChatMessage(player, $"{{34d399}}Установлен уровень {targetLvl} для {target.Name}.");
                break;

            case "tpm":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для телепортации.");
                    return;
                }
                player.Emit("starter:requestWaypointTp");
                break;

            case "tp":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для телепортации.");
                    return;
                }
                if (parts.Length < 4 || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tpx)
                    || !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tpy)
                    || !float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tpz))
                {
                    SendChatMessage(player, "{fde047}Использование: /tp <X> <Y> <Z>");
                    return;
                }
                player.Position = new Position(tpx, tpy, tpz + 0.5f);
                SendChatMessage(player, $"{{34d399}}Телепортирован на координаты: {tpx:F1}, {tpy:F1}, {tpz:F1}");
                break;

            case "goto":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (parts.Length < 2 || !uint.TryParse(parts[1], out var gotoId))
                {
                    SendChatMessage(player, "{fde047}Использование: /goto <ID игрока>");
                    return;
                }
                var gotoTarget = Alt.GetPlayerById(gotoId);
                if (gotoTarget == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                player.Position = new Position(gotoTarget.Position.X, gotoTarget.Position.Y + 1.0f, gotoTarget.Position.Z);
                SendChatMessage(player, $"{{34d399}}Вы телепортировались к {gotoTarget.Name} (ID: {gotoTarget.Id})");
                break;

            case "gethere":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (parts.Length < 2 || !uint.TryParse(parts[1], out var gethereId))
                {
                    SendChatMessage(player, "{fde047}Использование: /gethere <ID игрока>");
                    return;
                }
                var gethereTarget = Alt.GetPlayerById(gethereId);
                if (gethereTarget == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                gethereTarget.Position = new Position(player.Position.X + 1.0f, player.Position.Y, player.Position.Z);
                SendChatMessage(player, $"{{34d399}}Игрок {gethereTarget.Name} телепортирован к вам.");
                SendChatMessage(gethereTarget, $"{{34d399}}Администратор {player.Name} телепортировал вас к себе.");
                break;

            case "freeze":
            case "unfreeze":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (parts.Length < 2 || !uint.TryParse(parts[1], out var frzId))
                {
                    SendChatMessage(player, $"{{fde047}}Использование: /{cmd} <ID игрока>");
                    return;
                }
                var frzTarget = Alt.GetPlayerById(frzId);
                if (frzTarget == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок не найден.");
                    return;
                }
                var isFreeze = cmd == "freeze";
                frzTarget.Emit("starter:setFrozen", isFreeze);
                SendChatMessage(player, isFreeze ? $"{{34d399}}Игрок {frzTarget.Name} заморожен." : $"{{34d399}}Игрок {frzTarget.Name} разморожен.");
                SendChatMessage(frzTarget, isFreeze ? "{ef4444}Вы были заморожены администратором." : "{34d399}Вы были разморожены администратором.");
                break;

            case "car":
            case "veh":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для спавна транспорта.");
                    return;
                }
                var modelName = parts.Length > 1 ? parts[1] : "adder";
                try
                {
                    var spawnPos = new Position(player.Position.X + 2f, player.Position.Y + 2f, player.Position.Z);
                    var veh = Alt.CreateVehicle(Alt.Hash(modelName), spawnPos, player.Rotation);
                    SendChatMessage(player, $"{{34d399}}Создан транспорт: {modelName}");
                }
                catch (Exception ex)
                {
                    SendChatMessage(player, $"{{ef4444}}Ошибка спавна транспорта: {ex.Message}");
                }
                break;

            case "fix":
            case "repair":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (player.Vehicle != null)
                {
                    player.Vehicle.Repair();
                    SendChatMessage(player, "{34d399}Транспорт отремонтирован.");
                }
                else
                {
                    SendChatMessage(player, "{fde047}Вы должны находиться в транспорте для починки.");
                }
                break;

            case "noclip":
            case "fly":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для включения NoClip.");
                    return;
                }
                player.Emit("starter:toggleNoClip");
                break;

            case "heal":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для лечения.");
                    return;
                }
                player.Health = 200;
                player.Armor = 100;
                SendChatMessage(player, "{34d399}Здоровье и броня восстановлены до 100%.");
                break;

            case "armor":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                var armorVal = parts.Length > 1 && ushort.TryParse(parts[1], out var arm) ? arm : (ushort)100;
                player.Armor = armorVal;
                SendChatMessage(player, $"{{34d399}}Броня установлена на {armorVal}.");
                break;

            case "god":
            case "godmode":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                var currentGod = _godModes.GetOrAdd(player.Id, false);
                var newGod = !currentGod;
                _godModes[player.Id] = newGod;
                player.Emit("starter:setGodMode", newGod);
                SendChatMessage(player, newGod ? "{34d399}Режим бога (GodMode) ВКЛЮЧЕН." : "{fde047}Режим бога (GodMode) ВЫКЛЮЧЕН.");
                break;

            case "kill":
            case "suicide":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                player.Health = 0;
                SendChatMessage(player, "{ef4444}Вы погибли.");
                break;

            case "speed":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                var speedMult = parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sm) ? sm : 1.0f;
                player.Emit("starter:setSpeed", speedMult);
                SendChatMessage(player, $"{{38bdf8}}Множитель скорости бега: {speedMult:F2}");
                break;

            case "weather":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для смены погоды.");
                    return;
                }
                if (parts.Length > 1)
                {
                    var weatherType = parts[1].ToUpperInvariant();
                    Alt.EmitAllClients("starter:setWeather", weatherType);
                    BroadcastChatMessage($"{{38bdf8}}[Погода] Администратор установил погоду: {weatherType}");
                }
                else
                {
                    SendChatMessage(player, "{fde047}Использование: /weather <CLEAR|EXTRASUNNY|CLOUDS|RAIN|THUNDER|SNOW|XMAS>");
                }
                break;

            case "time":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для смены времени.");
                    return;
                }
                if (parts.Length > 1 && int.TryParse(parts[1], out var hour))
                {
                    var minute = parts.Length > 2 && int.TryParse(parts[2], out var m) ? m : 0;
                    Alt.EmitAllClients("starter:setTime", hour, minute);
                    BroadcastChatMessage($"{{38bdf8}}[Время] Администратор установил время: {hour:D2}:{minute:D2}");
                }
                else
                {
                    SendChatMessage(player, "{fde047}Использование: /time <час 0-23> [минута 0-59]");
                }
                break;

            case "dim":
            case "dimension":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (parts.Length > 1 && int.TryParse(parts[1], out var dim))
                {
                    player.Dimension = dim;
                    SendChatMessage(player, $"{{38bdf8}}Измерение изменено на: {dim}");
                }
                else
                {
                    SendChatMessage(player, "{fde047}Использование: /dim <номер измерения>");
                }
                break;

            case "skin":
            case "ped":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (parts.Length > 1)
                {
                    var skinModel = parts[1];
                    try
                    {
                        player.Model = Alt.Hash(skinModel);
                        SendChatMessage(player, $"{{34d399}}Скин изменен на: {skinModel}");
                    }
                    catch (Exception ex)
                    {
                        SendChatMessage(player, $"{{ef4444}}Ошибка смены скина: {ex.Message}");
                    }
                }
                else
                {
                    SendChatMessage(player, "{fde047}Использование: /skin <модель, напр. mp_m_freemode_01>");
                }
                break;

            case "kick":
                if (!IsAdmin(player, 2))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] Доступ запрещен (требуется Уровень 2+).");
                    return;
                }
                if (parts.Length < 2 || !uint.TryParse(parts[1], out var kickId))
                {
                    SendChatMessage(player, "{fde047}Использование: /kick <ID> [причина]");
                    return;
                }
                var kickTarget = Alt.GetPlayerById(kickId);
                if (kickTarget == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                var reason = parts.Length > 2 ? string.Join(' ', parts[2..]) : "Исключен администратором";
                BroadcastChatMessage($"{{ef4444}}[Kick] {kickTarget.Name} был исключен администратором {player.Name}. Причина: {reason}");
                kickTarget.Kick(reason);
                break;

            case "clear":
            case "cls":
                player.Emit("flovmp:chat:clear");
                SendChatMessage(player, "{a1a1aa}Чат очищен.");
                break;

            default:
                SendChatMessage(player, $"{{a1a1aa}}Неизвестная команда: /{cmd}. Введите /help для списка доступных команд.");
                break;
        }
    }

    private void OnTeleportWaypoint(IPlayer player, float x, float y, float z)
    {
        if (!IsAdmin(player, 1))
        {
            Alt.LogWarning($"[Security Violation] Неавторизованный запрос teleportWaypoint от {player.Name} (ID: {player.Id})");
            SendChatMessage(player, "{ef4444}[FloV:MP Security] Телепортация отклонена сервером (недостаточно прав).");
            return;
        }

        player.Position = new Position(x, y, z + 1.0f);
        SendChatMessage(player, $"{{34d399}}Телепортация по метке: {x:F1}, {y:F1}, {z:F1}");
    }

    private void OnToggleNoClip(IPlayer player, bool enabled)
    {
        if (!IsAdmin(player, 1))
        {
            Alt.LogWarning($"[Security Violation] Неавторизованная попытка toggleNoClip от {player.Name} (ID: {player.Id})");
            SendChatMessage(player, "{ef4444}[FloV:MP Security] Полет NoClip отклонен сервером (недостаточно прав).");
            return;
        }

        SendChatMessage(player, enabled ? "{34d399}Админ-полет (NoClip) ВКЛЮЧЕН" : "{fde047}Админ-полет (NoClip) ВЫКЛЮЧЕН");
    }
}
