using System.Collections.Concurrent;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Auth;
using FloVMP.Core.Chat;

namespace FloVMP.Gamemode;

/// <summary>
/// Чат (каркас Фазы 3). Сервер принимает сырой текст от вошедшего игрока,
/// чистит/валидирует (<see cref="ChatSanitizer"/>), ограничивает частоту,
/// разбирает команды (<c>/...</c>) и рассылает результат.
///
/// Клиент → сервер: flovmp:chat:say {text}
/// Сервер → клиент: flovmp:chat:msg {kind, author, text}
///   kind: "player" | "system" | "me" | "cmd"
/// </summary>
public sealed class ChatSystem
{
    private const int MaxPerWindow = 4;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    private readonly Func<IPlayer, Account?> _accountOf;
    private readonly ConcurrentDictionary<uint, (int count, DateTime first)> _rate = new();
    // снимок ника — на выходе AuthSystem может уже вычистить свою запись
    private readonly ConcurrentDictionary<uint, string> _names = new();

    public ChatSystem(Func<IPlayer, Account?> accountOf)
    {
        _accountOf = accountOf;
    }

    public void Attach()
    {
        Alt.OnClient<string>("flovmp:chat:say", OnSay);
        Alt.OnPlayerDisconnect += OnDisconnect;
    }

    public void Detach()
    {
        Alt.OnPlayerDisconnect -= OnDisconnect;
    }

    /// <summary>Системное сообщение всем.</summary>
    public void Broadcast(string text)
    {
        foreach (var p in Alt.GetAllPlayers())
            if (p.Exists) p.Emit("flovmp:chat:msg", "system", "", text);
    }

    /// <summary>Системное сообщение одному игроку.</summary>
    public static void SendSystem(IPlayer player, string text)
    {
        if (player.Exists) player.Emit("flovmp:chat:msg", "system", "", text);
    }

    public void OnPlayerAuthed(IPlayer player, Account account) => Safe.Run("chat.OnPlayerAuthed", () =>
    {
        _names[player.Id] = account.Username;
        SendSystem(player, $"Добро пожаловать на FloV:MP, {account.Username}. /help — команды.");
        Broadcast($"{account.Username} зашёл на сервер.");
    });

    private void OnDisconnect(IPlayer player, string reason) => Safe.Run("chat.OnDisconnect", () =>
    {
        _rate.TryRemove(player.Id, out _);
        if (_names.TryRemove(player.Id, out var name))
            Broadcast($"{name} вышел с сервера.");
    });

    private void OnSay(IPlayer player, string raw) => Safe.Run("chat.OnSay", () =>
    {
        if (!player.Exists) return;

        var acc = _accountOf(player);
        if (acc is null)
        {
            SendSystem(player, "Сначала войдите в аккаунт.");
            return;
        }

        if (IsRateLimited(player.Id))
        {
            SendSystem(player, "Не так быстро.");
            return;
        }

        var text = ChatSanitizer.Clean(raw);
        if (text is null) return;

        if (ChatSanitizer.IsCommand(text))
        {
            HandleCommand(player, acc, text);
            return;
        }

        var msg = text.StartsWith("//", StringComparison.Ordinal) ? text[1..] : text;
        foreach (var p in Alt.GetAllPlayers())
            if (p.Exists && _accountOf(p) is not null)
                p.Emit("flovmp:chat:msg", "player", acc.Username, msg);
    });

    private void HandleCommand(IPlayer player, Account acc, string text)
    {
        var (cmd, args) = ChatSanitizer.ParseCommand(text);
        switch (cmd)
        {
            case "help":
                SendSystem(player, "Команды: /help, /me <действие>, /online, /pos");
                break;

            case "me":
                if (args.Length == 0) { SendSystem(player, "Использование: /me <действие>"); break; }
                var action = string.Join(' ', args);
                foreach (var p in Alt.GetAllPlayers())
                    if (p.Exists && _accountOf(p) is not null)
                        p.Emit("flovmp:chat:msg", "me", acc.Username, action);
                break;

            case "online":
                var n = Alt.GetAllPlayers().Count(p => p.Exists && _accountOf(p) is not null);
                SendSystem(player, $"Онлайн: {n}");
                break;

            case "pos":
                var pos = player.Position;
                SendSystem(player, $"Позиция: {pos.X:0.0} / {pos.Y:0.0} / {pos.Z:0.0}");
                break;

            default:
                SendSystem(player, $"Неизвестная команда: /{cmd}");
                break;
        }
    }

    private bool IsRateLimited(uint id)
    {
        var now = DateTime.UtcNow;
        var e = _rate.AddOrUpdate(id,
            _ => (1, now),
            (_, cur) => now - cur.first > Window ? (1, now) : (cur.count + 1, cur.first));
        return e.count > MaxPerWindow;
    }
}
