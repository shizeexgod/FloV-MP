using AltV.Net;
using AltV.Net.Elements.Entities;

namespace FloVMP.Gamemode;

/// <summary>
/// Команды серверной консоли (для владельца во время теста/эксплуатации).
///   saveall            — принудительно сохранить инвентари
///   online             — список игроков
///   say &lt;текст&gt;         — системное сообщение в чат всем
///   kick &lt;ник&gt; [причина] — выкинуть игрока
/// </summary>
public sealed class ConsoleCommands
{
    private readonly Action _saveAll;
    private readonly Action<string> _broadcast;
    private readonly Func<IPlayer, string?> _nameOf;

    public ConsoleCommands(Action saveAll, Action<string> broadcast, Func<IPlayer, string?> nameOf)
    {
        _saveAll = saveAll;
        _broadcast = broadcast;
        _nameOf = nameOf;
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
                var target = Alt.GetAllPlayers()
                    .FirstOrDefault(p => string.Equals(p.Name, args[0], StringComparison.OrdinalIgnoreCase));
                if (target is null) { Alt.Log($"игрок не найден: {args[0]}"); break; }
                var reason = args.Length > 1 ? string.Join(' ', args[1..]) : "kicked by admin";
                target.Kick(reason);
                Alt.Log($"[FloV:MP] console: kick {target.Name} — {reason}");
                break;
        }
    });
}
