using System.Collections.Concurrent;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Auth;

namespace FloVMP.Gamemode;

/// <summary>
/// Авторизация игрока в RP-режиме.
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
    private readonly string _serverName;
    private readonly ConcurrentDictionary<uint, Account> _authed = new();
    // accountId → playerId: не пускаем один аккаунт с двух клиентов
    private readonly ConcurrentDictionary<int, uint> _activeAccounts = new();

    // ПРОИЗВОДИТЕЛЬНОСТЬ: вход/регистрация выполняли чтение БД и PBKDF2
    // (120 000 итераций, ~13 мс на слабом CPU и больше) ПРЯМО на главном потоке
    // alt:V. Каждый вход съедал почти целый тик, а массовый реконнект после
    // рестарта складывался в секунды заморозки сервера.
    // Теперь тяжёлая часть считается в фоне, а все операции alt:V (Emit, спавн,
    // метаданные) выполняются строго на главном потоке в Pump() из OnTick.
    private readonly ConcurrentQueue<PendingAuth> _completed = new();
    private readonly ConcurrentDictionary<uint, byte> _inFlight = new();

    /// <summary>Кто уже сообщил о готовности — защита от повторов от клиента.</summary>
    private readonly ConcurrentDictionary<uint, byte> _clientReady = new();

    private sealed record PendingAuth(IPlayer Player, bool IsRegister, AuthResult Result, string Username, string Password);
    private readonly Action<IPlayer, Account> _onAuthed;

    // Многоуровневые блокировки (IP / Social Club / HWID / MAC). Необязательны:
    // без них работает только бан аккаунта, как было раньше.
    private readonly FloVMP.Core.Security.MultiTierBanService? _bans;

    // Лимит игроков по лицензии (license.flv); null — без ограничения.
    private readonly Func<int>? _playerLimit;

    public AuthSystem(IAccountStore store, Action<IPlayer, Account> onAuthed, string serverName = "RolePlay Server",
                      FloVMP.Core.Security.MultiTierBanService? bans = null, Func<int>? playerLimit = null)
    {
        _playerLimit = playerLimit;
        _store = store;
        _auth = new AuthService(_store);
        _onAuthed = onAuthed;
        _serverName = serverName;
        _bans = bans;
    }

    public AuthSystem(string accountsPath, Action<IPlayer, Account> onAuthed, string serverName = "RolePlay Server")
        : this(new JsonAccountStore(accountsPath), onAuthed, serverName)
    {
    }

    /// <summary>Аккаунт вошедшего игрока, либо null.</summary>
    public Account? AccountOf(IPlayer player) =>
        _authed.TryGetValue(player.Id, out var a) ? a : null;

    /// <summary>Сохранить изменения аккаунта (мут, бан, уровень админа, баланс).</summary>
    public void SaveAccount(Account acc) => _store.Update(acc);

    public Account? FindByName(string username) => _store.FindByUsername(username);
    public Account? FindByBankAccount(string bankAccountNumber) => _store.FindByBankAccount(bankAccountNumber);
    public Account? FindByIdentifier(string identifier) =>
        string.IsNullOrWhiteSpace(identifier) ? null : (_store.FindByUsername(identifier.Trim()) ?? _store.FindByBankAccount(identifier.Trim()));

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

        // Блокировка по идентификаторам проверяется ДО показа экрана входа:
        // забаненный по железу не должен вообще видеть форму логина, а тем
        // более получать возможность зарегистрировать новый аккаунт — ровно
        // этим раньше и обходился «бан по HWID».
        if (_bans is not null && RejectIfBanned(player)) return;

        if (_playerLimit is not null)
        {
            var limit = _playerLimit();
            if (Alt.GetAllPlayers().Count > limit)
            {
                Alt.Log($"[FloV:MP] [License] вход {player.Name} отклонён: предел {limit} игроков");
                player.Kick($"Сервер заполнен ({limit} игроков).");
                return;
            }
        }

        // NUI логина покажем, когда клиентский ресурс сообщит, что готов
        // (flovmp:client:ready). Иначе auth:show может уйти раньше, чем
        // index.js навесит обработчики — и игрок застрянет на чёрном экране.
        Alt.Log($"[FloV:MP] auth: {player.Name} подключился, ждём готовности клиента");

        // Раньше здесь был Task.Delay(...).ContinueWith(...): продолжение
        // выполнялось на произвольном потоке пула и перебирало игроков плюс
        // трогало сущность alt:V в нативной памяти. Теперь то же самое, но в
        // тике главного потока, где игрок не может исчезнуть посреди проверки.
        var playerId = player.Id;
        Systems.MainThreadScheduler.RunAfter(2000, "auth.show.fallback", () =>
        {
            var p = Alt.GetPlayerById(playerId);
            if (p is null || !p.Exists || IsAuthed(p)) return;
            Alt.Log($"[FloV:MP] auth: страховочная отправка flovmp:auth:show для {p.Name}");
            p.Emit("flovmp:auth:show", _serverName);
        });
    });

    /// <summary>
    /// Проверка входящего подключения по IP / Social Club / HWID / MAC.
    /// Возвращает true, если игрок отклонён.
    ///
    /// Проверка идёт по памяти сервиса, без обращения к БД: она выполняется на
    /// главном потоке для каждого входа, и поход в базу здесь означал бы
    /// задержку тика при каждом подключении.
    /// </summary>
    private bool RejectIfBanned(IPlayer player)
    {
        if (_bans is null) return false;

        try
        {
            var result = _bans.CheckConnection(
                accountId: 0, // аккаунт ещё неизвестен — вход не выполнен
                ip: player.Ip,
                socialClubId: player.SocialClubId.ToString(),
                hwidHash: player.HardwareIdHash.ToString("X16"),
                macAddress: player.HardwareIdExHash.ToString("X16"),
                policy: FloVMP.Core.Security.HwidPolicyMode.Strict);

            if (!result.IsBlocked) return false;

            var until = result.ExpiresAtUtc is null
                ? "навсегда"
                : $"до {result.ExpiresAtUtc:dd.MM.yyyy HH:mm} UTC";
            Alt.Log($"[FloV:MP] [Ban] отклонён вход {player.Name} ({player.Ip}): {result.MatchedFlag} — {result.Reason}");
            FloVMP.Core.Logging.GameLog.System("ban_connection_rejected",
                ("player", player.Name), ("flag", result.MatchedFlag?.ToString() ?? "?"),
                ("reason", result.Reason ?? ""));
            player.Kick($"Доступ заблокирован ({until}). Причина: {result.Reason}");
            return true;
        }
        catch (Exception ex)
        {
            // Сломанная проверка не должна запирать вход всем подряд: в
            // сомнительной ситуации пускаем и пишем в лог, а не глушим сервер.
            Alt.Log($"[FloV:MP] [Ban] ошибка проверки блокировки: {ex.Message}");
            return false;
        }
    }

    private void OnClientReady(IPlayer player) => Safe.Run("auth.OnClientReady", () =>
    {
        if (!player.Exists || IsAuthed(player)) return;

        // Событие приходит ОТ КЛИЕНТА и может слаться модифицированным клиентом
        // сколько угодно раз: каждый вызов — запись в лог и ответное событие,
        // то есть усилитель для DoS. Готовность осмысленна один раз за
        // подключение.
        if (!_clientReady.TryAdd(player.Id, 0)) return;

        Alt.Log($"[FloV:MP] auth: клиент {player.Name} готов, отправляем flovmp:auth:show");
        player.Emit("flovmp:auth:show", _serverName);
    });

    private void OnDisconnect(IPlayer player, string reason) => Safe.Run("auth.OnDisconnect", () =>
    {
                // Игрок мог выйти, пока его вход считался в фоне — снимаем блокировку,
        // иначе повторный заход тем же player.Id был бы проигнорирован.
        _inFlight.TryRemove(player.Id, out _);
        _clientReady.TryRemove(player.Id, out _);
if (_authed.TryRemove(player.Id, out var acc))
        {
            if (_activeAccounts.TryGetValue(acc.Id, out var activePid) && activePid == player.Id)
            {
                _activeAccounts.TryRemove(acc.Id, out _);
            }
        }
        else
        {
            // Страховочная очистка, если игрок отключился во время процесса входа
            foreach (var kvp in _activeAccounts)
            {
                if (kvp.Value == player.Id)
                {
                    _activeAccounts.TryRemove(kvp.Key, out _);
                }
            }
        }
    });

    private void OnLogin(IPlayer player, string username, string password) => Safe.Run("auth.OnLogin", () =>
    {
        if (!player.Exists || IsAuthed(player)) return;
        // Один запрос на игрока за раз: иначе спам кнопкой «Войти» плодит
        // параллельные PBKDF2-вычисления и выжигает CPU.
        if (!_inFlight.TryAdd(player.Id, 0)) return;

        var u = username ?? "";
        var pw = password ?? "";
        var key = ThrottleKey(player);
        Task.Run(() =>
        {
            AuthResult r;
            // Вход в игру — только логин и пароль, без второго фактора: 2FA
            // относится к лаунчеру/кабинету, а в игровой форме кода нет, и
            // игрок с включённой 2FA иначе не мог бы войти вовсе.
            try { r = _auth.Login(u, pw, key, totpCode: null, enforceTwoFa: false); }
            catch (Exception ex)
            {
                Alt.Log($"[FloV:MP] auth: сбой входа: {ex.Message}");
                r = new AuthResult(AuthOutcome.WrongPassword, "внутренняя ошибка входа");
            }
            _completed.Enqueue(new PendingAuth(player, false, r, u, pw));
        });
    });

    /// <summary>
    /// Применение результатов входа/регистрации. Вызывается ТОЛЬКО с главного
    /// потока (из OnTick) — здесь трогаются сущности alt:V.
    /// </summary>
    public void Pump()
    {
        while (_completed.TryDequeue(out var item))
        {
            var pl = item.Player;
            try { _inFlight.TryRemove(pl.Id, out _); } catch { }
            Safe.Run("auth.Pump", () =>
            {
                if (!pl.Exists || IsAuthed(pl)) return;
                if (item.IsRegister) ApplyRegister(pl, item);
                else ApplyLogin(pl, item);
            });
        }
    }

    private void ApplyLogin(IPlayer player, PendingAuth item) => Safe.Run("auth.ApplyLogin", () =>
    {
        var res = item.Result;
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
        if (!_inFlight.TryAdd(player.Id, 0)) return;

        // Регистрация дороже входа: PBKDF2 считается дважды (хеш при создании
        // + проверка при входе сразу после). Обе — в фоне.
        // throttleKey (IP) — иначе клиент мог бы спамить регистрацию: забить
        // БД пустышками + нагрузить CPU PBKDF2 (120k итераций на попытку).
        var u = username ?? "";
        var pw = password ?? "";
        var key = ThrottleKey(player);
        Task.Run(() =>
        {
            AuthResult r;
            try
            {
                var reg = _auth.Register(u, pw, key);
                // При неуспехе регистрации отдаём её результат как есть.
                r = reg.Ok ? _auth.Login(u, pw, key, totpCode: null, enforceTwoFa: false) : reg;
                if (reg.Ok && !r.Ok)
                {
                    // Зарегистрировали, но войти не смогли — сообщаем причину входа.
                    r = new AuthResult(r.Outcome, r.Message, r.Account);
                }
            }
            catch (Exception ex)
            {
                Alt.Log($"[FloV:MP] auth: сбой регистрации: {ex.Message}");
                r = new AuthResult(AuthOutcome.BadUsername, "внутренняя ошибка регистрации");
            }
            _completed.Enqueue(new PendingAuth(player, true, r, u, pw));
        });
    });

    private void ApplyRegister(IPlayer player, PendingAuth item) => Safe.Run("auth.ApplyRegister", () =>
    {
        var login = item.Result;
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
        // после этого лаунчер подхватит аккаунт из session.json. Только для
        // игрока с этой же машины: файл пишется на машине СЕРВЕРА, и раньше
        // лаунчер на ПК с сервером подхватывал аккаунт любого зашедшего игрока.
        if (IsLocalPlayer(player))
            SessionHandoff.Write(account);

        try
        {
#pragma warning disable CS0612, CS0618
            player.SetSyncedMetaData("authed", true);
            player.SetSyncedMetaData("username", account.Username);
            player.SetSyncedMetaData("admin_level", account.AdminLevel);
#pragma warning restore CS0612, CS0618
            player.SetStreamSyncedMetaData("adminLevel", account.AdminLevel);
            player.Emit("flovmp:console:setAdmin", account.AdminLevel);
        }
        catch (Exception ex)
        {
            Alt.Log($"[FloV:MP] auth metadata error: {ex.Message}");
        }

        player.Emit("flovmp:auth:hide");
        _onAuthed(player, account);
    }

    private static bool IsLocalPlayer(IPlayer player)
    {
        try
        {
            var ip = (player.Ip ?? "").Trim();
            if (ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase)) ip = ip[7..];
            return System.Net.IPAddress.TryParse(ip, out var addr) && System.Net.IPAddress.IsLoopback(addr);
        }
        catch { return false; }
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
