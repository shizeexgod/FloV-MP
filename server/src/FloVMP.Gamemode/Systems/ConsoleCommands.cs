using AltV.Net;
using AltV.Net.Elements.Entities;

namespace FloVMP.Gamemode;

/// <summary>
/// Команды серверной консоли (для владельца во время теста/эксплуатации).
///   saveall            — принудительно сохранить инвентари
///   online             — список игроков
///   say &lt;текст&gt;         — системное сообщение в чат всем
///   kick &lt;ID|ник&gt; [причина] — выкинуть игрока
///   setadmin &lt;ID|аккаунт&gt; &lt;0-8&gt; — уровень администратора (игрок в сети или нет)
/// </summary>
public sealed class ConsoleCommands
{
    private readonly Action _saveAll;
    private readonly Action<string> _broadcast;
    private readonly Func<IPlayer, string?> _nameOf;
    private readonly Func<string, int, string>? _setAdmin;

    /// <param name="setAdmin">(ID игрока или имя аккаунта, уровень) → сообщение для консоли.</param>
    public ConsoleCommands(Action saveAll, Action<string> broadcast, Func<IPlayer, string?> nameOf,
                           Func<string, int, string>? setAdmin = null)
    {
        _saveAll = saveAll;
        _broadcast = broadcast;
        _nameOf = nameOf;
        _setAdmin = setAdmin;
    }

    public void Attach() => Alt.OnConsoleCommand += OnCommand;
    public void Detach() => Alt.OnConsoleCommand -= OnCommand;

    private void OnCommand(string name, string[] args) => Safe.Run("console." + name, () =>
    {
        switch (name.ToLowerInvariant())
        {
            case "saveall":
                _saveAll();
                Alt.Log("[FloV:MP] console: инвентари сохранены");
                break;

            case "online":
                var players = Alt.GetAllPlayers();
                Alt.Log($"[FloV:MP] console: онлайн {players.Count}");
                foreach (var p in players)
                    Alt.Log($"  [{p.Id}] {p.Name} — аккаунт: {_nameOf(p) ?? "(не вошёл)"}");
                break;

            case "say":
                if (args.Length == 0) { Alt.Log("использование: say <текст>"); break; }
                var text = string.Join(' ', args);
                _broadcast(text);
                Alt.Log($"[FloV:MP] console: say -> {text}");
                break;

            case "kick":
                if (args.Length == 0) { Alt.Log("использование: kick <ник> [причина]"); break; }
                var target = uint.TryParse(args[0], out var kickId) ? Alt.GetPlayerById(kickId) : null;
                target ??= Alt.GetAllPlayers()
                    .FirstOrDefault(p => string.Equals(p.Name, args[0], StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(_nameOf(p), args[0], StringComparison.OrdinalIgnoreCase));
                if (target is null) { Alt.Log($"игрок не найден: {args[0]}"); break; }
                var reason = args.Length > 1 ? string.Join(' ', args[1..]) : "kicked by admin";
                target.Kick(reason);
                Alt.Log($"[FloV:MP] console: kick {target.Name} — {reason}");
                break;

            // Первый администратор RP-режима: без этой команды права выдавались
            // только правкой базы вручную.
            case "setadmin":
                if (_setAdmin is null) { Alt.Log("setadmin недоступен"); break; }
                if (args.Length < 2 || !int.TryParse(args[1], out var level) || level is < 0 or > 8)
                {
                    Alt.Log("использование: setadmin <ID игрока|имя аккаунта> <уровень 0-8>");
                    break;
                }
                Alt.Log("[FloV:MP] console: " + _setAdmin(args[0], level));
                break;
        }
    });
}
