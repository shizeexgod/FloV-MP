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

    public AuthSystem(IAccountStore store, Action<IPlayer, Account> onAuthed)
    {
        _store = store;
        _auth = new AuthService(_store);
        _onAuthed = onAuthed;
    }

    public AuthSystem(string accountsPath, Action<IPlayer, Account> onAuthed)
        : this(new JsonAccountStore(accountsPath), onAuthed)
    {
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

        var playerId = player.Id;
        Task.Delay(2000).ContinueWith(_ =>
        {
            try
            {
                var p = Alt.GetAllPlayers().FirstOrDefault(x => x.Id == playerId);
                if (p != null && p.Exists && !IsAuthed(p))
                {
                    Alt.Log($"[FloV:MP] auth: страховочная отправка flovmp:auth:show для {p.Name}");
                    p.Emit("flovmp:auth:show");
                }
            }
            catch (Exception ex)
            {
                Alt.Log($"[FloV:MP] auth fallback warning: {ex.Message}");
            }
        });
    });

    private void OnClientReady(IPlayer player) => Safe.Run("auth.OnClientReady", () =>
    {
        if (!player.Exists || IsAuthed(player)) return;
        Alt.Log($"[FloV:MP] auth: клиент {player.Name} готов, отправляем flovmp:auth:show");
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

        // BUGFIX: если player.Emit или Finish бросят исключение (игрок отключился
        // между Claim и Finish), освобождаем блокировку аккаунта в _activeAccounts.
        // Без этого аккаунт зависал со статусом «в игре» до рестарта сервера.
        try
        {
            player.Emit("flovmp:auth:result", true, res.Message);
            Finish(player, res.Account);
        }
        catch
        {
            _activeAccounts.TryRemove(new KeyValuePair<int, uint>(res.Account.Id, player.Id));
            throw; // Safe.Run залогирует
        }
    });

    private void OnRegister(IPlayer player, string username, string password) => Safe.Run("auth.OnRegister", () =>
    {
        if (!player.Exists || IsAuthed(player)) return;

        // throttleKey (IP) — иначе клиент мог бы спамить регистрацию: забить
        // БД пустышками + нагрузить CPU PBKDF2 (120k итераций на попытку).
        var res = _auth.Register(username ?? "", password ?? "", ThrottleKey(player));
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

        try
        {
            FloVMP.Core.Logging.GameLog.Account("register",
                FloVMP.Core.Logging.LogActor.Player(login.Account.Id, login.Account.Username), Ip(player));
            player.Emit("flovmp:auth:result", true, "регистрация и вход выполнены");
            Finish(player, login.Account);
        }
        catch
        {
            _activeAccounts.TryRemove(new KeyValuePair<int, uint>(login.Account.Id, player.Id));
            throw;
        }
    });

    private bool TryClaimAccount(int accountId, uint playerId) =>
        _activeAccounts.TryAdd(accountId, playerId);

    private void Finish(IPlayer player, Account account)
    {
        _authed[player.Id] = account;
        Alt.Log($"[FloV:MP] auth: {player.Name} вошёл как '{account.Username}' (id {account.Id})");
        FloVMP.Core.Logging.GameLog.Account("login",
            FloVMP.Core.Logging.LogActor.Player(account.Id, account.Username), Ip(player));

        // Хэндофф в лаунчер: если игрок зашёл в игру без входа в лаунчере,
        // после этого лаунчер подхватит аккаунт из session.json.
        SessionHandoff.Write(account);

        try
        {
            player.SetSyncedMetaData("authed", true);
            player.SetSyncedMetaData("username", account.Username);
            player.SetSyncedMetaData("admin_level", account.AdminLevel);
        }
        catch (Exception ex)
        {
            Alt.Log($"[FloV:MP] auth metadata error: {ex.Message}");
        }

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
