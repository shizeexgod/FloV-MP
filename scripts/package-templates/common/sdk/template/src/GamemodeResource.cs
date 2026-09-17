using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;

namespace Gamemode;

/// <summary>
/// Точка входа вашего сервера. Работает рядом с платформой (ресурс
/// flovmp-starter): вход, администрирование, баны, голос и чат уже есть,
/// здесь — ваша игровая логика.
/// </summary>
public sealed class GamemodeResource : Resource
{
    public override void OnStart()
    {
        // Игрок загрузил клиент и появился в мире.
        Alt.OnServer<IPlayer>("flovmp:player:ready", OnPlayerReady);

        // Вызовы ваших чат-команд.
        Alt.OnServer<IPlayer, string, string>("flovmp:command", OnCommand);

        // Команды регистрируются в платформе. Если платформа стартует позже —
        // она сообщит об этом событием flovmp:platform:ready.
        Alt.OnServer("flovmp:platform:ready", ConfigurePlatform);
        ConfigurePlatform();

        // Пример события от вашего клиентского скрипта (client/index.js).
        Alt.OnClient<IPlayer, string>("gamemode:hello", (player, text) =>
            SendChat(player, $"Сервер получил: {text}"));

        Alt.Log("[Gamemode] ресурс запущен");
    }

    public override void OnStop()
    {
        Alt.Log("[Gamemode] ресурс остановлен");
    }

    private static void ConfigurePlatform()
    {
        // Своя точка появления (раскомментируйте и укажите координаты: /pos в игре):
        // Alt.Emit("flovmp:settings:spawn", -1037.7f, -2737.8f, 20.2f, 330f);

        // Возрождение после смерти своим кодом (событие flovmp:player:died):
        // Alt.Emit("flovmp:settings:respawn", false);

        RegisterCommands();
    }

    private static void RegisterCommands()
    {
        // имя, описание для /help, минимальный уровень администратора (0 — всем)
        Alt.Emit("flovmp:commands:register", "hello", "приветствие от сервера", 0);
        Alt.Emit("flovmp:commands:register", "sethp", "здоровье игроку: /sethp <id> <100-200>", 1);
    }

    private static void OnPlayerReady(IPlayer player)
    {
        SendChat(player, $"{{c4b5fd}}[Сервер]{{ffffff}} Привет, {player.Name}! Это ваш ресурс gamemode.");
    }

    private static void OnCommand(IPlayer player, string command, string args)
    {
        switch (command)
        {
            case "hello":
                SendChat(player, $"Привет, {player.Name}! Уровень администратора: {AdminLevel(player)}.");
                player.Emit("gamemode:notify", "Команда /hello выполнена");
                break;

            case "sethp":
                // Права уже проверены платформой (уровень 1+), но проверять
                // аргументы — ваша задача: их присылает игрок.
                var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !uint.TryParse(parts[0], out var id) ||
                    !ushort.TryParse(parts[1], out var hp) || hp is < 100 or > 200)
                {
                    SendChat(player, "Использование: /sethp <id> <100-200>");
                    return;
                }
                var target = Alt.GetPlayerById(id);
                if (target is null || !target.Exists)
                {
                    SendChat(player, "Игрок не найден.");
                    return;
                }
                target.Health = hp;
                SendChat(player, $"{target.Name}: здоровье {hp}.");
                break;
        }
    }

    /// <summary>Уровень администратора (0 — обычный игрок). Выставляет платформа.</summary>
    public static int AdminLevel(IPlayer player) =>
        player.GetLocalMetaData("adminLevel", out int level) ? level : 0;

    /// <summary>Сообщение в чат игроку. {RRGGBB} в тексте — цвет.</summary>
    public static void SendChat(IPlayer player, string text) =>
        player.Emit("flovmp:chat:msg", "system", "", text);
}
