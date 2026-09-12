using System;
using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;

namespace FloVMP.Starter;

/// <summary>
/// Чистый ванильный стартер для клиентов FloV:MP.
/// Поставляется покупателям лицензии как чистый холст:
/// - Спавн игрока на карте без NPC и трафика
/// - Рабочий текстовый чат с админ-командами (/tpm, /pos, /car, /noclip, /heal, /weather, /time)
/// - Голосовой 3D-чат
/// - Админ-полет NoClip (F4)
/// - F8 консоль разработчика
/// - Никаких лишних RP-компонентов и тяжелых баз данных
/// </summary>
public class StarterResource : Resource
{
    private static readonly Position DefaultSpawnPosition = new(198.8f, -935.6f, 30.7f); // Легион Сквер
    private static readonly float DefaultSpawnHeading = 140f;
    private static readonly uint DefaultPlayerModel = Alt.Hash("mp_m_freemode_01");

    private IVoiceChannel? _spatialVoiceChannel;

    public override void OnStart()
    {
        Alt.Log("[FloV:MP Starter] Чистый ванильный сервер успешно запущен!");
        Alt.Log("[FloV:MP Starter] Движок: FloV:MP Core | Доступен чат, F4 NoClip, F8 консоль, войс.");

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

        // Активация 3D войс-канала
        try
        {
            _spatialVoiceChannel?.AddPlayer(player);
        }
        catch
        {
        }

        player.Emit("chat:addMessage", "{ff3d8a}[FloV:MP]{ffffff} Добро пожаловать на сервер!");
        player.Emit("chat:addMessage", "{a1a1aa}Доступные команды: /tpm, /pos, /car [модель], /noclip, /heal, /weather, /time");
        player.Emit("starter:initClient");
    }

    private void OnPlayerDisconnect(IPlayer player, string reason)
    {
        Alt.Log($"[FloV:MP] Игрок {player.Name} (ID: {player.Id}) отключился ({reason}).");
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
            case "pos":
            case "coords":
                player.Emit("chat:addMessage", $"{{38bdf8}}Координаты: X: {player.Position.X:F2}, Y: {player.Position.Y:F2}, Z: {player.Position.Z:F2}, Yaw: {player.Rotation.Yaw:F2}");
                player.Emit("starter:copyCoords", player.Position.X, player.Position.Y, player.Position.Z, player.Rotation.Yaw);
                break;

            case "tpm":
                player.Emit("starter:requestWaypointTp");
                break;

            case "car":
            case "veh":
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
                player.Emit("starter:toggleNoClip");
                break;

            case "heal":
                player.Health = 200;
                player.Armor = 100;
                player.Emit("chat:addMessage", "{34d399}Здоровье и броня восстановлены до 100%.");
                break;

            case "weather":
                if (parts.Length > 1)
                {
                    var weatherType = parts[1].ToUpperInvariant();
                    Alt.EmitAllClients("starter:setWeather", weatherType);
                    player.Emit("chat:addMessage", $"{{38bdf8}}Погода изменена на: {weatherType}");
                }
                break;

            case "time":
                if (parts.Length > 1 && int.TryParse(parts[1], out var hour))
                {
                    var minute = parts.Length > 2 && int.TryParse(parts[2], out var m) ? m : 0;
                    Alt.EmitAllClients("starter:setTime", hour, minute);
                    player.Emit("chat:addMessage", $"{{38bdf8}}Время установлено на {hour:D2}:{minute:D2}");
                }
                break;

            default:
                player.Emit("chat:addMessage", $"{{a1a1aa}}Неизвестная команда: /{cmd}. Доступны: /pos, /tpm, /car, /noclip, /heal, /weather, /time");
                break;
        }
    }

    private void OnTeleportWaypoint(IPlayer player, float x, float y, float z)
    {
        player.Position = new Position(x, y, z + 1.0f);
        player.Emit("chat:addMessage", $"{{34d399}}Телепортация по метке: {x:F1}, {y:F1}, {z:F1}");
    }

    private void OnToggleNoClip(IPlayer player, bool enabled)
    {
        player.Emit("chat:addMessage", enabled ? "{34d399}Админ-полет (NoClip) ВКЛЮЧЕН" : "{fde047}Админ-полет (NoClip) ВЫКЛЮЧЕН");
    }
}
