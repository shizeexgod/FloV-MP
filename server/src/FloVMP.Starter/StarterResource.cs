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
        Alt.OnClient<IPlayer, float, float, float>("starter:teleportWaypoint", OnTeleportWaypoint);
        Alt.OnClient<IPlayer, bool>("starter:toggleNoClip", OnToggleNoClip);
    }

    public override void OnStop()
    {
        Alt.Log("[FloV:MP Starter] Остановка ванильного стартера.");
    }

    public bool IsAdmin(IPlayer player, int minLevel = 1)
    {
        return _adminLevels.TryGetValue(player.Id, out var level) && level >= minLevel;
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

        player.Emit("chat:addMessage", "{ff3d8a}[FloV:MP]{ffffff} Добро пожаловать на сервер!");

        if (level > 0)
        {
            player.Emit("chat:addMessage", "{34d399}[Admin]{ffffff} Права администратора активированы (Уровень " + level + "). Доступны: /tpm, /pos, /car, /noclip, /heal, /weather, /time.");
        }
        else
        {
            player.Emit("chat:addMessage", "{a1a1aa}Доступна команда /pos для координат. Авторизация администратора: /adminauth <пароль>");
        }

        player.Emit("starter:initClient");
        player.Emit("flovmp:console:setAdmin", level);
    }

    private void OnPlayerDisconnect(IPlayer player, string reason)
    {
        Alt.Log($"[FloV:MP] Игрок {player.Name} (ID: {player.Id}) отключился ({reason}).");
        _adminLevels.TryRemove(player.Id, out _);

        try
        {
            _spatialVoiceChannel?.RemovePlayer(player);
        }
        catch
        {
        }
    }

    private void OnChatMessage(IPlayer player, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        if (message.StartsWith("/"))
        {
            HandleCommand(player, message[1..]);
            return;
        }

        var formatted = $"[{player.Id}] {player.Name}: {message}";
        Alt.EmitAllClients("chat:addMessage", formatted);
    }

    private void HandleCommand(IPlayer player, string commandLine)
    {
        var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var cmd = parts[0].ToLowerInvariant();
        switch (cmd)
        {
            case "help":
                if (IsAdmin(player, 1))
                {
                    player.Emit("chat:addMessage", "{38bdf8}Админ-команды: /pos, /tpm, /car [модель], /noclip, /heal, /weather [тип], /time [час] [мин], /setadmin <id> <lvl>");
                }
                else
                {
                    player.Emit("chat:addMessage", "{38bdf8}Команды: /pos (координаты для багрепорта), /adminauth <пароль>");
                }
                break;

            case "pos":
            case "coords":
                player.Emit("chat:addMessage", $"{{38bdf8}}Координаты: X: {player.Position.X:F2}, Y: {player.Position.Y:F2}, Z: {player.Position.Z:F2}, Yaw: {player.Rotation.Yaw:F2}");
                player.Emit("starter:copyCoords", player.Position.X, player.Position.Y, player.Position.Z, player.Rotation.Yaw);
                break;

            case "adminauth":
                if (parts.Length < 2)
                {
                    player.Emit("chat:addMessage", "{fde047}Использование: /adminauth <пароль>");
                    return;
                }
                if (parts[1] == AdminPassword)
                {
                    _adminLevels[player.Id] = 8;
                    player.Emit("flovmp:console:setAdmin", 8);
                    player.Emit("chat:addMessage", "{34d399}[FloV:MP Security] Авторизация успешна! Вам присвоен уровень Главного Администратора (8). Панель Дев-тулс в F8 разблокирована.");
                    Alt.Log($"[Security] Игрок {player.Name} (ID: {player.Id}) успешно авторизовался как администратор.");
                }
                else
                {
                    player.Emit("chat:addMessage", "{ef4444}[FloV:MP Security] Неверный пароль администратора!");
                    Alt.LogWarning($"[Security Alert] Неудачная попытка авторизации /adminauth от {player.Name} (ID: {player.Id})");
                }
                break;

            case "setadmin":
                if (!IsAdmin(player, 8))
                {
                    player.Emit("chat:addMessage", "{ef4444}[FloV:MP Security] Доступ запрещен (требуется Уровень 8).");
                    return;
                }
                if (parts.Length < 3 || !uint.TryParse(parts[1], out var targetId) || !int.TryParse(parts[2], out var targetLvl))
                {
                    player.Emit("chat:addMessage", "{fde047}Использование: /setadmin <ID> <Уровень 0-8>");
                    return;
                }
                var target = Alt.GetPlayerById(targetId);
                if (target == null)
                {
                    player.Emit("chat:addMessage", "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                _adminLevels[target.Id] = targetLvl;
                target.Emit("flovmp:console:setAdmin", targetLvl);
                target.Emit("chat:addMessage", $"{{34d399}}[Admin] Администратор {player.Name} установил вам уровень доступа {targetLvl}.");
                player.Emit("chat:addMessage", $"{{34d399}}Установлен уровень {targetLvl} для {target.Name}.");
                break;

            case "tpm":
                if (!IsAdmin(player, 1))
                {
                    player.Emit("chat:addMessage", "{ef4444}[FloV:MP Security] У вас нет прав для телепортации.");
                    return;
                }
                player.Emit("starter:requestWaypointTp");
                break;

            case "car":
            case "veh":
                if (!IsAdmin(player, 1))
                {
                    player.Emit("chat:addMessage", "{ef4444}[FloV:MP Security] У вас нет прав для спавна транспорта.");
                    return;
                }
                var modelName = parts.Length > 1 ? parts[1] : "adder";
                try
                {
                    var spawnPos = new Position(player.Position.X + 2f, player.Position.Y + 2f, player.Position.Z);
                    var veh = Alt.CreateVehicle(Alt.Hash(modelName), spawnPos, player.Rotation);
                    player.Emit("chat:addMessage", $"{{34d399}}Создан транспорт: {modelName}");
                }
                catch (Exception ex)
                {
                    player.Emit("chat:addMessage", $"{{ef4444}}Ошибка спавна транспорта: {ex.Message}");
                }
                break;

            case "noclip":
            case "fly":
                if (!IsAdmin(player, 1))
                {
                    player.Emit("chat:addMessage", "{ef4444}[FloV:MP Security] У вас нет прав для включения NoClip.");
                    return;
                }
                player.Emit("starter:toggleNoClip");
                break;

            case "heal":
                if (!IsAdmin(player, 1))
                {
                    player.Emit("chat:addMessage", "{ef4444}[FloV:MP Security] У вас нет прав для лечения.");
                    return;
                }
                player.Health = 200;
                player.Armor = 100;
                player.Emit("chat:addMessage", "{34d399}Здоровье и броня восстановлены до 100%.");
                break;

            case "weather":
                if (!IsAdmin(player, 1))
                {
                    player.Emit("chat:addMessage", "{ef4444}[FloV:MP Security] У вас нет прав для смены погоды.");
                    return;
                }
                if (parts.Length > 1)
                {
                    var weatherType = parts[1].ToUpperInvariant();
                    Alt.EmitAllClients("starter:setWeather", weatherType);
                    player.Emit("chat:addMessage", $"{{38bdf8}}Погода изменена на: {weatherType}");
                }
                break;

            case "time":
                if (!IsAdmin(player, 1))
                {
                    player.Emit("chat:addMessage", "{ef4444}[FloV:MP Security] У вас нет прав для смены времени.");
                    return;
                }
                if (parts.Length > 1 && int.TryParse(parts[1], out var hour))
                {
                    var minute = parts.Length > 2 && int.TryParse(parts[2], out var m) ? m : 0;
                    Alt.EmitAllClients("starter:setTime", hour, minute);
                    player.Emit("chat:addMessage", $"{{38bdf8}}Время установлено на {hour:D2}:{minute:D2}");
                }
                break;

            default:
                player.Emit("chat:addMessage", $"{{a1a1aa}}Неизвестная команда: /{cmd}. Введите /help для списка доступных команд.");
                break;
        }
    }

    private void OnTeleportWaypoint(IPlayer player, float x, float y, float z)
    {
        if (!IsAdmin(player, 1))
        {
            Alt.LogWarning($"[Security Violation] Неавторизованный запрос teleportWaypoint от {player.Name} (ID: {player.Id})");
            player.Emit("chat:addMessage", "{ef4444}[FloV:MP Security] Телепортация отклонена сервером (недостаточно прав).");
            return;
        }

        player.Position = new Position(x, y, z + 1.0f);
        player.Emit("chat:addMessage", $"{{34d399}}Телепортация по метке: {x:F1}, {y:F1}, {z:F1}");
    }

    private void OnToggleNoClip(IPlayer player, bool enabled)
    {
        if (!IsAdmin(player, 1))
        {
            Alt.LogWarning($"[Security Violation] Неавторизованная попытка toggleNoClip от {player.Name} (ID: {player.Id})");
            player.Emit("chat:addMessage", "{ef4444}[FloV:MP Security] Полет NoClip отклонен сервером (недостаточно прав).");
            return;
        }

        player.Emit("chat:addMessage", enabled ? "{34d399}Админ-полет (NoClip) ВКЛЮЧЕН" : "{fde047}Админ-полет (NoClip) ВЫКЛЮЧЕН");
    }
}
