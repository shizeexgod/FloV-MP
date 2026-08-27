using System.Collections.Concurrent;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Auth;

namespace FloVMP.Gamemode;

/// <summary>
/// Авторизация игрока (каркас Фазы 3, портирование паттерна az_auth
/// Florida V на C#/alt:V).
///
/// Поток:
///   connect  → игрок НЕ спавнится (клиент на чёрном экране), шлём
///              flovmp:auth:show, клиент показывает NUI логина/регистрации;
///   клиент   → flovmp:auth:login / flovmp:auth:register {user, pass};
///   сервер   → flovmp:auth:result {ok, message}; при ok — спавним игрока и
///              flovmp:auth:hide.
///
/// Чистая логика (хеши, стор, троттлинг) — в FloVMP.Core.Auth, тестируется
/// отдельно.
/// </summary>
public sealed class AuthSystem
{
    private readonly AuthService _auth;
    private readonly ConcurrentDictionary<uint, Account> _authed = new();
    private readonly Action<IPlayer, int> _spawnAuthed;

    public AuthSystem(string accountsPath, Action<IPlayer, int> spawnAuthed)
    {
        _auth = new AuthService(new JsonAccountStore(accountsPath));
        _spawnAuthed = spawnAuthed;
    }

    public void Attach()
    {
        Alt.OnPlayerConnect += OnConnect;
        Alt.OnPlayerDisconnect += OnDisconnect;
        Alt.OnClient<string, string>("flovmp:auth:login", OnLogin);
        Alt.OnClient<string, string>("flovmp:auth:register", OnRegister);
    }

    public void Detach()
    {
        Alt.OnPlayerConnect -= OnConnect;
        Alt.OnPlayerDisconnect -= OnDisconnect;
    }

    public bool IsAuthed(IPlayer player) => _authed.ContainsKey(player.Id);

    private void OnConnect(IPlayer player, string reason)
    {
        if (!player.Exists) return;
        Alt.Log($"[FloV:MP] auth: {player.Name} подключился, ожидание входа");
        player.Emit("flovmp:auth:show");
    }

    private void OnDisconnect(IPlayer player, string reason)
    {
        _authed.TryRemove(player.Id, out _);
    }

    private void OnLogin(IPlayer player, string username, string password)
    {
        if (!player.Exists || IsAuthed(player)) return;

        var res = _auth.Login(username ?? "", password ?? "", ThrottleKey(player));
        player.Emit("flovmp:auth:result", res.Ok, res.Message);

        if (res.Ok && res.Account is not null)
        {
            Finish(player, res.Account);
        }
        else
        {
            Alt.Log($"[FloV:MP] auth: вход отклонён для {player.Name}: {res.Outcome}");
        }
    }

    private void OnRegister(IPlayer player, string username, string password)
    {
        if (!player.Exists || IsAuthed(player)) return;

        var res = _auth.Register(username ?? "", password ?? "");
        if (!res.Ok)
        {
            player.Emit("flovmp:auth:result", false, res.Message);
            return;
        }

        // после успешной регистрации сразу логиним
        var login = _auth.Login(username!, password!, ThrottleKey(player));
        player.Emit("flovmp:auth:result", login.Ok, login.Ok ? "регистрация и вход выполнены" : login.Message);
        if (login.Ok && login.Account is not null)
            Finish(player, login.Account);
    }

    private void Finish(IPlayer player, Account account)
    {
        _authed[player.Id] = account;
        Alt.Log($"[FloV:MP] auth: {player.Name} вошёл как '{account.Username}' (id {account.Id})");
        player.Emit("flovmp:auth:hide");
        _spawnAuthed(player, account.Id);
    }

    private static string ThrottleKey(IPlayer player)
    {
        try { return string.IsNullOrEmpty(player.Ip) ? player.Name : player.Ip; }
        catch { return player.Name; }
    }
}
