using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using FloVMP.Sdk;

namespace Gamemode;

/// <summary>
/// Точка входа вашего сервера. Работает рядом с платформой (ресурс
/// flovmp-starter): вход, администрирование, баны, голос и чат уже есть,
/// здесь — ваша игровая логика.
///
/// Игроки на GTA V Legacy 1.0.3889.0 (клиент FloV:MP) приходят к вам не
/// объектом IPlayer, а номером: у них нет сущности движка alt:V. Поэтому
/// рядом с каждым «обычным» событием есть парное flovmp:native:* с ID игрока,
/// а чат и интерфейс им отправляются событиями по этому же номеру.
/// </summary>
public sealed class GamemodeResource : Resource
{
    public override void OnStart()
    {
        // Асинхронные обработчики (база, сеть — без остановки тика сервера):
        // FloVAsync.OnServerAsync(...) — см. README, раздел «Асинхронный код».
        FloVAsync.Attach();

        // Игрок появился в мире: клиент alt:V и клиент 3889.
        Alt.OnServer<IPlayer>("flovmp:player:ready", p => OnPlayerReady((int)p.Id, p.Name));
        Alt.OnServer<int, string>("flovmp:native:ready", OnPlayerReady);

        // Вызовы ваших чат-команд.
        Alt.OnServer<IPlayer, string, string>("flovmp:command", (p, cmd, args) => OnCommand((int)p.Id, p.Name, cmd, args));
        Alt.OnServer<int, string, string, string>("flovmp:native:command", OnCommand);

        // Ответы игрока 3889 на меню и клавиши (см. команду /menu ниже).
        Alt.OnServer<int, string, int>("flovmp:native:menuSelect", OnMenuSelect);
        Alt.OnServer<int, string>("flovmp:native:key", (id, key) =>
            SendChat(id, $"Вы нажали {key}. Клавиши регистрируются событием flovmp:keys:bind."));

        // Каждое попадание проходит через вас до того, как сервер снимет здоровье.
        // Отвечать надо сразу, внутри обработчика (не после await): решение
        // ждётся до следующего тика, позже выстрел уже засчитан.
        // Не ответили — урон применится такой, как считала платформа.
        Alt.OnServer<int, int, int, string, int, float>("flovmp:damage", OnDamage);

        // События из клиентского кода игрока (server/client_packages, mp.events.callRemote).
        // Аргументы приходят JSON-массивом: их прислал игрок — проверяйте.
        Alt.OnServer<int, string, string>("flovmp:client:event", OnClientEvent);

        // Команды регистрируются в платформе. Если платформа стартует позже —
        // она сообщит об этом событием flovmp:platform:ready.
        Alt.OnServer("flovmp:platform:ready", ConfigurePlatform);
        ConfigurePlatform();

        // Пример события от вашего клиентского скрипта (client/index.js, только клиенты alt:V).
        Alt.OnClient<IPlayer, string>("gamemode:hello", (player, text) =>
            SendChat((int)player.Id, $"Сервер получил: {text}"));

        Alt.Log("[Gamemode] ресурс запущен");
    }

    // Продолжения асинхронных обработчиков выполняются здесь, в главном потоке.
    public override void OnTick() => FloVAsync.Pump();

    public override void OnStop()
    {
        // Отменить незаконченные запросы: их finally выполнится до выгрузки ресурса.
        FloVAsync.Stop();
        Alt.Log("[Gamemode] ресурс остановлен");
    }

    private static void ConfigurePlatform()
    {
        // Своя точка появления (раскомментируйте и укажите координаты: /pos в игре):
        // Alt.Emit("flovmp:settings:spawn", -1037.7f, -2737.8f, 20.2f, 330f);

        // Возрождение после смерти своим кодом (событие flovmp:player:died):
        // Alt.Emit("flovmp:settings:respawn", false);

        // Клавиша, о нажатии которой клиент 3889 сообщит серверу (A..Z, 0..9, F1..F12).
        Alt.Emit("flovmp:keys:bind", "E");

        RegisterCommands();
    }

    private static void RegisterCommands()
    {
        // имя, описание для /help, минимальный уровень администратора (0 — всем).
        // Уровень своих команд выбираете вы; уровни команд самой платформы
        // лежат в server/config/admin-commands.cfg (по умолчанию все — 8,
        // то есть только у создателя сервера).
        Alt.Emit("flovmp:commands:register", "hello", "приветствие от сервера", 0);
        Alt.Emit("flovmp:commands:register", "menu", "пример меню (игроки 3889)", 0);
        Alt.Emit("flovmp:commands:register", "money", "пример HUD из client_packages: /money <сумма>", 0);
        Alt.Emit("flovmp:commands:register", "sethp", "здоровье игроку: /sethp <id> <100-200>", 1);
    }

    private static void OnPlayerReady(int id, string name)
    {
        SendChat(id, $"{{c4b5fd}}[Сервер]{{ffffff}} Привет, {name}! Это ваш ресурс gamemode. Команды: /hello, /menu");
    }

    private static void OnCommand(int id, string name, string command, string args)
    {
        switch (command)
        {
            case "hello":
                SendChat(id, $"Привет, {name}! Вы вошли как игрок с ID {id}.");
                // Уведомление на экране игрока 3889 (мс — сколько держать).
                Alt.Emit("flovmp:ui:notify", id, "Команда /hello выполнена", 4000);
                break;

            case "menu":
                // Пункты меню — JSON: строки или {"label","desc"}.
                Alt.Emit("flovmp:ui:menu", id, "demo", "Пример меню",
                    "[{\"label\":\"Выдать брони\",\"desc\":\"Пример действия сервера\"}," +
                    " {\"label\":\"Сказать в чат\"}, {\"label\":\"Закрыть\"}]");
                break;

            case "money":
                // Событие в клиентский код игрока: mp.events.add('hud:money', ...)
                // в client_packages. Аргументы — JSON-массив.
                if (!long.TryParse(args.Trim(), out var money)) money = 5000;
                Alt.Emit("flovmp:client:call", id, "hud:money", $"[{money}]");
                break;

            case "sethp":
                // Права уже проверены платформой (уровень 1+), но проверять
                // аргументы — ваша задача: их присылает игрок.
                var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !uint.TryParse(parts[0], out var target) ||
                    !ushort.TryParse(parts[1], out var hp) || hp is < 100 or > 200)
                {
                    SendChat(id, "Использование: /sethp <id> <100-200>");
                    return;
                }
                var victim = Alt.GetPlayerById(target);
                if (victim is null || !victim.Exists)
                {
                    // Игрок 3889 виден платформе, но не alt:V: меняйте его состояние
                    // своими командами платформы или событиями flovmp:*.
                    SendChat(id, "Игрок не найден среди клиентов alt:V.");
                    return;
                }
                victim.Health = hp;
                SendChat(id, $"{victim.Name}: здоровье {hp}.");
                break;
        }
    }

    private static void OnMenuSelect(int id, string menu, int index)
    {
        if (menu != "demo") return;
        switch (index)
        {
            case 0:
                Alt.Emit("flovmp:ui:notify", id, "Броня выдана (пример)", 3000);
                break;
            case 1:
                SendChat(id, "Вы выбрали второй пункт меню.");
                break;
            default:
                Alt.Emit("flovmp:ui:closeMenu", id);
                break;
        }
    }

    private static void OnClientEvent(int id, string name, string json)
    {
        switch (name)
        {
            case "hud:ready":
                // Пример из sdk/client_packages: HUD загрузился у игрока.
                Alt.Emit("flovmp:client:call", id, "hud:money", "[5000]");
                break;

            case "hud:report":
                // Панель F2 из примера (страница HTML): текст прислал игрок — длину и
                // содержимое проверяем здесь, а не верим клиенту.
                var text = System.Text.Json.JsonSerializer.Deserialize<string[]>(json)?.FirstOrDefault() ?? "";
                if (text.Length is 0 or > 200) return;
                SendChat(id, "{34d399}[Сервер]{ffffff} Обращение принято: " + text);
                break;
        }
    }

    /// <summary>Уровень администратора (0 — обычный игрок). Выставляет платформа.</summary>
    public static int AdminLevel(IPlayer player) =>
        player.GetLocalMetaData("adminLevel", out int level) ? level : 0;

    /// <summary>Сообщение в чат игроку по его ID. {RRGGBB} в тексте — цвет.</summary>
    public static void SendChat(int playerId, string text) => Alt.Emit("flovmp:chat:to", playerId, text);

    /// <summary>Сообщение в чат всем игрокам.</summary>
    public static void SendChatAll(string text) => Alt.Emit("flovmp:chat:all", text);
    /// <summary>
    /// Попадание до того, как сервер снимет здоровье.
    ///
    /// На этом событии держатся броня фракций, режимы без оружия,
    /// дуэли и безопасные зоны. Ответ отправляется сразу же, с тем же
    /// номером вопроса: позже он уже ни на что не влияет.
    /// </summary>
    /// <param name="request">Номер вопроса — вернуть его в ответе.</param>
    /// <param name="attackerId">Кто стрелял.</param>
    /// <param name="victimId">По кому попали.</param>
    /// <param name="weapon">Хэш оружия строкой; 0 или WEAPON_UNARMED — рукопашная.</param>
    /// <param name="damage">Сколько собирается снять платформа.</param>
    /// <param name="distance">Расстояние между ними в метрах.</param>
    private void OnDamage(int request, int attackerId, int victimId, string weapon, int damage, float distance)
    {
        // Пример первый: рукопашная не работает вовсе.
        // if (weapon == "0" || weapon == "2725352035")
        // {
        //     Alt.Emit("flovmp:damage:set", request, false, 0);
        //     return;
        // }

        // Пример второй: вдвое меньше урона на дистанции больше ста метров.
        // if (distance > 100f)
        // {
        //     Alt.Emit("flovmp:damage:set", request, true, damage / 2);
        //     return;
        // }

        // Ничего не отвечаем — платформа снимет столько, сколько собиралась.
    }

}
