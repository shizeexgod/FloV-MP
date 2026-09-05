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
    private readonly IAccountStore _store;
    private readonly AuthService _auth;
    private readonly ConcurrentDictionary<uint, Account> _authed = new();
    // accountId → playerId: не пускаем один аккаунт с двух клиентов
    private readonly ConcurrentDictionary<int, uint> _activeAccounts = new();
    private readonly Action<IPlayer, Account> _onAuthed;

    public AuthSystem(string accountsPath, Action<IPlayer, Account> onAuthed)
    {
        _store = new JsonAccountStore(accountsPath);
        _auth = new AuthService(_store);
        _onAuthed = onAuthed;
    }

    /// <summary>Аккаунт вошедшего игрока, либо null.</summary>
    public Account? AccountOf(IPlayer player) =>
        _authed.TryGetValue(player.Id, out var a) ? a : null;

    /// <summary>Сохранить изменения аккаунта (мут, бан, уровень админа, баланс).</summary>
    public void SaveAccount(Account acc) => _store.Update(acc);

    public Account? FindByName(string username) => _store.FindByUsername(username);

    public void Attach()
    {
        Alt.OnPlayerConnect += OnConnect;
        Alt.OnPlayerDisconnect += OnDisconnect;
        Alt.OnClient("flovmp:client:ready", OnClientReady);
        Alt.OnClient<string, string>("flovmp:auth:login", OnLogin);
        Alt.OnClient<string, string>("flovmp:auth:register", OnRegister);
    }

    public void Detach()
    {
        Alt.OnPlayerConnect -= OnConnect;
        Alt.OnPlayerDisconnect -= OnDisconnect;
    }

    public bool IsAuthed(IPlayer player) => _authed.ContainsKey(player.Id);

    private void OnConnect(IPlayer player, string reason) => Safe.Run("auth.OnConnect", () =>
    {
        if (!player.Exists) return;
        // NUI логина покажем, когда клиентский ресурс сообщит, что готов
        // (flovmp:client:ready). Иначе auth:show может уйти раньше, чем
        // index.js навесит обработчики — и игрок застрянет на чёрном экране.
        Alt.Log($"[FloV:MP] auth: {player.Name} подключился, ждём готовности клиента");
    });

    private void OnClientReady(IPlayer player) => Safe.Run("auth.OnClientReady", () =>
    {
        if (!player.Exists || IsAuthed(player)) return;
        player.Emit("flovmp:auth:show");
    });

    private void OnDisconnect(IPlayer player, string reason) => Safe.Run("auth.OnDisconnect", () =>
    {
        if (_authed.TryRemove(player.Id, out var acc))
            _activeAccounts.TryRemove(new KeyValuePair<int, uint>(acc.Id, player.Id));
    });

    private void OnLogin(IPlayer player, string username, string password) => Safe.Run("auth.OnLogin", () =>
    {
        if (!player.Exists || IsAuthed(player)) return;

        var res = _auth.Login(username ?? "", password ?? "", ThrottleKey(player));
        if (!res.Ok || res.Account is null)
        {
            player.Emit("flovmp:auth:result", false, res.Message);
            Alt.Log($"[FloV:MP] auth: вход отклонён для {player.Name}: {res.Outcome}");
            return;
        }

        if (!TryClaimAccount(res.Account.Id, player.Id))
        {
            player.Emit("flovmp:auth:result", false, "аккаунт уже в игре");
            return;
        }

        player.Emit("flovmp:auth:result", true, res.Message);
        Finish(player, res.Account);
    });

    private void OnRegister(IPlayer player, string username, string password) => Safe.Run("auth.OnRegister", () =>
    {
        if (!player.Exists || IsAuthed(player)) return;

        var res = _auth.Register(username ?? "", password ?? "");
        if (!res.Ok)
        {
            player.Emit("flovmp:auth:result", false, res.Message);
            return;
        }

        var login = _auth.Login(username!, password!, ThrottleKey(player));
        if (!login.Ok || login.Account is null || !TryClaimAccount(login.Account.Id, player.Id))
        {
            player.Emit("flovmp:auth:result", false, login.Ok ? "аккаунт уже в игре" : login.Message);
            return;
        }

        FloVMP.Core.Logging.GameLog.Account("register",
            FloVMP.Core.Logging.LogActor.Player(login.Account.Id, login.Account.Username), Ip(player));
        player.Emit("flovmp:auth:result", true, "регистрация и вход выполнены");
        Finish(player, login.Account);
    });

    private bool TryClaimAccount(int accountId, uint playerId) =>
        _activeAccounts.TryAdd(accountId, playerId);

    private void Finish(IPlayer player, Account account)
    {
        _authed[player.Id] = account;
        Alt.Log($"[FloV:MP] auth: {player.Name} вошёл как '{account.Username}' (id {account.Id})");
        FloVMP.Core.Logging.GameLog.Account("login",
            FloVMP.Core.Logging.LogActor.Player(account.Id, account.Username), Ip(player));
        player.Emit("flovmp:auth:hide");
        _onAuthed(player, account);
    }

    private static string Ip(IPlayer player)
    {
        try { return player.Ip ?? ""; } catch { return ""; }
    }

    private static string ThrottleKey(IPlayer player)
    {
        try { return string.IsNullOrEmpty(player.Ip) ? player.Name : player.Ip; }
        catch { return player.Name; }
    }
}
