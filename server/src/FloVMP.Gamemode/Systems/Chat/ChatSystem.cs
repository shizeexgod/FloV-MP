using System.Collections.Concurrent;
using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Admin;
using FloVMP.Core.Auth;
using FloVMP.Core.Chat;
using FloVMP.Core.Documents;
using FloVMP.Core.Factions;
using FloVMP.Core.Logging;

namespace FloVMP.Gamemode;

/// <summary>
/// Чат и обработчик 8-уровневой системы административных команд «Держава Онлайн».
/// </summary>
public sealed class ChatSystem
{
    private const int MaxPerWindow = 4;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    private readonly Func<IPlayer, Account?> _accountOf;
    private readonly Action<Account>? _saveAccount;
    private readonly Func<string, Account?>? _findAccountByName;
    private readonly FloVMP.Core.Economy.EconomyService? _economy;
    private readonly FactionService? _factions;
    private readonly DocumentService? _documents;
    private readonly FloVMP.Core.Housing.HousingService? _housing;
    private readonly InventorySystem? _inventory;
    private readonly Action<int>? _restartServer;
    private readonly Action<IPlayer, Position>? _notifyTeleport;
    private readonly Action<int, bool>? _setAdminExempt;
    // Платформенный режим: RP-геймплей (экономика/фракции/документы/транспорт)
    // не входит в платформу — эти команды недоступны, сервер-владелец добавляет
    // свои. Базовый чат/инфо/модерация остаются.
    private readonly bool _platformMode;

    private readonly ConcurrentDictionary<uint, (int count, DateTime first)> _rate = new();
    private readonly ConcurrentDictionary<uint, string> _names = new();
    private readonly ConcurrentDictionary<uint, (Position originalPos, int originalDim)> _spectatingAdmins = new();

    public ChatSystem(
        Func<IPlayer, Account?> accountOf,
        Action<Account>? saveAccount = null,
        Func<string, Account?>? findAccountByName = null,
        FloVMP.Core.Economy.EconomyService? economy = null,
        FactionService? factions = null,
        DocumentService? documents = null,
        FloVMP.Core.Housing.HousingService? housing = null,
        InventorySystem? inventory = null,
        Action<int>? restartServer = null,
        Action<IPlayer, Position>? notifyTeleport = null,
        Action<int, bool>? setAdminExempt = null,
        bool platformMode = false)
    {
        _accountOf = accountOf;
        _saveAccount = saveAccount;
        _findAccountByName = findAccountByName;
        _economy = economy;
        _factions = factions;
        _documents = documents;
        _housing = housing;
        _inventory = inventory;
        _restartServer = restartServer;
        _notifyTeleport = notifyTeleport;
        _setAdminExempt = setAdminExempt;
        _platformMode = platformMode;
    }

    // RP-команды игрока (экономика/фракции/документы/транспорт) — в платформенном
    // режиме отклоняются: это геймплей сервера-владельца, а не платформы.
    private static readonly HashSet<string> RpPlayerCommands = new(StringComparer.Ordinal)
    {
        "passport", "lic", "licenses", "pay", "bank", "balance", "deposit", "withdraw",
        "transfer", "factions", "f", "d", "invite", "uninvite", "giverank",
        "cuff", "uncuff", "arrest", "engine", "lock",
    };

    public void Attach()
    {
        Alt.OnClient<string>("flovmp:chat:say", OnSay);
        Alt.OnPlayerDisconnect += OnDisconnect;
    }

    public void Detach()
    {
        Alt.OnPlayerDisconnect -= OnDisconnect;
    }

    /// <summary>Системное сообщение всем игрокам онлайн.</summary>
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

    /// <summary>Сообщение администраторам (внутренний чат администрации).</summary>
    public void BroadcastAdmin(string text)
    {
        foreach (var p in Alt.GetAllPlayers())
        {
            if (!p.Exists) continue;
            var acc = _accountOf(p);
            if (acc != null && acc.AdminLevel > 0)
            {
                p.Emit("flovmp:chat:msg", "cmd", "[А-ЧАТ]", text);
            }
        }
    }

    public void OnPlayerAuthed(IPlayer player, Account account) => Safe.Run("chat.OnPlayerAuthed", () =>
    {
        _names[player.Id] = account.Username;
        SendSystem(player, $"Добро пожаловать на Держава Онлайн, {account.Username}. Введите /help для списка команд.");
        if (account.AdminLevel > 0)
        {
            SendSystem(player, $"[Администрация] Вы вошли с правами: {AdminTitles.GetTitle(account.AdminLevel)} ({account.AdminLevel} lvl). Введите /ahelp для команд.");
        }
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

        if (acc.IsMuted(DateTime.UtcNow))
        {
            SendSystem(player, $"[Чат] У вас действует блокировка текстового чата (мут) до {acc.MuteUntilUtc}.");
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
        var senderPos = player.Position;
        var senderDim = player.Dimension;
        foreach (var p in Alt.GetAllPlayers())
            if (p.Exists && _accountOf(p) is not null && p.Dimension == senderDim && p.Position.Distance(senderPos) <= 25.0f)
                p.Emit("flovmp:chat:msg", "player", acc.Username, msg);
    });

    private void HandleCommand(IPlayer player, Account acc, string text)
    {
        var (cmd, args) = ChatSanitizer.ParseCommand(text);

        // Платформенный режим: RP-геймплей не входит в платформу.
        if (_platformMode && RpPlayerCommands.Contains(cmd))
        {
            SendSystem(player, "Эта команда — часть геймплея сервера, а не платформы FloV:MP.");
            return;
        }

        // 1. Игровые команды для всех
        switch (cmd)
        {
            case "help":
                var helpMsg = "Игровые команды:\n/help, /passport, /lic, /pay, /bank, /factions, /f, /d, /invite, /uninvite, /giverank, /cuff, /uncuff, /arrest, /me, /b, /do, /try, /todo, /s, /w, /clear, /engine, /lock, /online, /pos";
                if (acc.AdminLevel > 0)
                    helpMsg += $"\n[Админ] Доступно {AdminCommandRegistry.GetAvailableCommands(acc.AdminLevel).Count} команд. Введите /ahelp";
                SendSystem(player, helpMsg);
                return;

            case "ahelp":
                if (acc.AdminLevel <= 0) { SendSystem(player, $"Неизвестная команда: /{cmd}"); return; }
                var cmds = AdminCommandRegistry.GetAvailableCommands(acc.AdminLevel);
                SendSystem(player, $"=== Команды администрации ({AdminTitles.GetTitle(acc.AdminLevel)}, {acc.AdminLevel} lvl) ===");
                foreach (var c in cmds)
                    SendSystem(player, $"{c.Usage} — {c.Description}");
                return;

            case "me":
                if (args.Length == 0) { SendSystem(player, "Использование: /me <действие>"); return; }
                var action = string.Join(' ', args);
                var mePos = player.Position;
                var meDim = player.Dimension;
                foreach (var p in Alt.GetAllPlayers())
                    if (p.Exists && _accountOf(p) is not null && p.Dimension == meDim && p.Position.Distance(mePos) <= 25.0f)
                        p.Emit("flovmp:chat:msg", "me", acc.Username, action);
                return;

            case "b":
                if (args.Length == 0) { SendSystem(player, "Использование: /b <OOC сообщение>"); return; }
                var oocMsg = string.Join(' ', args);
                var bPos = player.Position;
                var bDim = player.Dimension;
                foreach (var p in Alt.GetAllPlayers())
                    if (p.Exists && _accountOf(p) is not null && p.Dimension == bDim && p.Position.Distance(bPos) <= 25.0f)
                        p.Emit("flovmp:chat:msg", "ooc", acc.Username, $"(( {oocMsg} ))");
                return;

            case "s":
            case "shout":
                if (args.Length == 0) { SendSystem(player, "Использование: /s <сообщение>"); return; }
                var sMsg = string.Join(' ', args);
                var sPos = player.Position;
                var sDim = player.Dimension;
                foreach (var p in Alt.GetAllPlayers())
                    if (p.Exists && _accountOf(p) is not null && p.Dimension == sDim && p.Position.Distance(sPos) <= 55.0f)
                        p.Emit("flovmp:chat:msg", "shout", acc.Username, sMsg);
                return;

            case "w":
            case "whisper":
                if (args.Length < 2) { SendSystem(player, "Использование: /w <ID/ник> <сообщение>"); return; }
                var wTarget = FindPlayer(args[0]);
                if (wTarget == null || !wTarget.Exists) { SendSystem(player, "Игрок не найден."); return; }
                var wTargetAcc = _accountOf(wTarget);
                if (wTargetAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                if (player.Dimension != wTarget.Dimension || player.Position.Distance(wTarget.Position) > 3.5f)
                {
                    SendSystem(player, "Игрок слишком далеко, чтобы шептать ему на ухо (максимум 3.5м).");
                    return;
                }
                var wMsg = string.Join(' ', args[1..]);
                SendSystem(player, $"[Шёпот для {wTargetAcc.Username}] {wMsg}");
                SendSystem(wTarget, $"[{acc.Username} шепчет вам на ухо] {wMsg}");
                foreach (var p in Alt.GetAllPlayers())
                {
                    if (p.Exists && p != player && p != wTarget && p.Dimension == player.Dimension && p.Position.Distance(player.Position) <= 2.0f)
                        p.Emit("flovmp:chat:msg", "me", acc.Username, $"что-то прошептал на ухо {wTargetAcc.Username}");
                }
                return;

            case "clear":
                player.Emit("flovmp:chat:clear");
                return;

            case "online":
                var n = Alt.GetAllPlayers().Count(p => p.Exists && _accountOf(p) is not null);
                SendSystem(player, $"Игроков онлайн: {n}");
                return;

            case "pos":
                var pos = player.Position;
                SendSystem(player, $"Координаты: X: {pos.X:0.0}, Y: {pos.Y:0.0}, Z: {pos.Z:0.0}");
                return;

            case "pay":
                if (args.Length < 2 || !long.TryParse(args[1], out var payAmt) || payAmt <= 0)
                {
                    SendSystem(player, "Использование: /pay <ID/ник> <сумма>");
                    return;
                }
                var payTarget = FindPlayer(args[0]);
                if (payTarget == null || !payTarget.Exists) { SendSystem(player, "Игрок не найден."); return; }
                if (payTarget == player) { SendSystem(player, "Нельзя передать деньги самому себе."); return; }
                var payTargetAcc = _accountOf(payTarget);
                if (payTargetAcc == null) { SendSystem(player, "Аккаунт получателя не найден."); return; }
                if (player.Dimension != payTarget.Dimension || player.Position.Distance(payTarget.Position) > 5.0f)
                {
                    SendSystem(player, "Игрок находится слишком далеко от вас (максимум 5 метров).");
                    return;
                }
                if (_economy != null)
                {
                    if (_economy.TryPayCash(acc, payTargetAcc, payAmt, out var payErr))
                    {
                        _saveAccount?.Invoke(acc);
                        _saveAccount?.Invoke(payTargetAcc);
                        SendSystem(player, $"Вы передали {payAmt:N0} руб. игроку {payTargetAcc.Username}.");
                        SendSystem(payTarget, $"Игрок {acc.Username} передал вам {payAmt:N0} руб.");
                        foreach (var p in Alt.GetAllPlayers())
                        {
                            if (p.Exists && p.Dimension == player.Dimension && p.Position.Distance(player.Position) <= 15.0f)
                                p.Emit("flovmp:chat:msg", "me", acc.Username, $"достал кошелёк и передал купюры {payTargetAcc.Username}");
                        }
                    }
                    else
                    {
                        SendSystem(player, payErr);
                    }
                }
                return;

            case "bank":
            case "balance":
                SendSystem(player, $"=== Финансовый статус: {acc.Username} ===");
                SendSystem(player, $"Наличные: {acc.Cash:N0} руб.");
                SendSystem(player, $"Банковский счёт: {acc.Bank:N0} руб. (№ {acc.BankAccountNumber})");
                return;

            case "deposit":
                if (args.Length == 0 || !long.TryParse(args[0], out var depAmt) || depAmt <= 0)
                {
                    SendSystem(player, "Использование: /deposit <сумма>");
                    return;
                }
                if (_economy != null)
                {
                    if (_economy.TryDeposit(acc, depAmt, out var depErr))
                    {
                        _saveAccount?.Invoke(acc);
                        SendSystem(player, $"Вы внесли {depAmt:N0} руб. на банковский счёт. Баланс: {acc.Bank:N0} руб.");
                    }
                    else
                    {
                        SendSystem(player, depErr);
                    }
                }
                return;

            case "withdraw":
                if (args.Length == 0 || !long.TryParse(args[0], out var withAmt) || withAmt <= 0)
                {
                    SendSystem(player, "Использование: /withdraw <сумма>");
                    return;
                }
                if (_economy != null)
                {
                    if (_economy.TryWithdraw(acc, withAmt, out var withErr))
                    {
                        _saveAccount?.Invoke(acc);
                        SendSystem(player, $"Вы сняли {withAmt:N0} руб. с банковского счёта. Наличные: {acc.Cash:N0} руб.");
                    }
                    else
                    {
                        SendSystem(player, withErr);
                    }
                }
                return;

            case "transfer":
                if (args.Length < 2 || !long.TryParse(args[1], out var trAmt) || trAmt <= 0)
                {
                    SendSystem(player, "Использование: /transfer <ID/ник/номер_счёта> <сумма> [назначение]");
                    return;
                }
                var trTarget = FindPlayer(args[0]);
                Account? trTargetAcc = null;
                if (trTarget != null && trTarget.Exists)
                {
                    trTargetAcc = _accountOf(trTarget);
                }
                else
                {
                    trTargetAcc = _findAccountByName?.Invoke(args[0]);
                }

                if (trTargetAcc == null)
                {
                    SendSystem(player, "Получатель перевода не найден.");
                    return;
                }

                if (_economy != null)
                {
                    var desc = args.Length > 2 ? string.Join(' ', args.Skip(2)) : "Банковский перевод";
                    if (_economy.TryTransferBank(acc, trTargetAcc, trAmt, desc, out var trErr))
                    {
                        _saveAccount?.Invoke(acc);
                        _saveAccount?.Invoke(trTargetAcc);
                        SendSystem(player, $"Перевод {trAmt:N0} руб. в пользу {trTargetAcc.Username} успешно выполнен.");
                        if (trTarget != null && trTarget.Exists)
                        {
                            SendSystem(trTarget, $"На ваш банковский счёт поступил перевод: +{trAmt:N0} руб. от {acc.Username}. Назначение: {desc}");
                        }
                    }
                    else
                    {
                        SendSystem(player, trErr);
                    }
                }
                return;

            case "do":
                if (args.Length == 0) { SendSystem(player, "Использование: /do <описание>"); return; }
                var doAction = string.Join(' ', args);
                var doPos = player.Position;
                var doDim = player.Dimension;
                foreach (var p in Alt.GetAllPlayers())
                    if (p.Exists && _accountOf(p) is not null && p.Dimension == doDim && p.Position.Distance(doPos) <= 25.0f)
                        p.Emit("flovmp:chat:msg", "do", "", $"{doAction} (( {acc.Username} ))");
                return;

            case "try":
                if (args.Length == 0) { SendSystem(player, "Использование: /try <действие>"); return; }
                var tryAction = string.Join(' ', args);
                var isSuccess = Random.Shared.Next(0, 2) == 1;
                var outcomeTag = isSuccess ? "[Удачно]" : "[Неудачно]";
                var tryPos = player.Position;
                var tryDim = player.Dimension;
                foreach (var p in Alt.GetAllPlayers())
                    if (p.Exists && _accountOf(p) is not null && p.Dimension == tryDim && p.Position.Distance(tryPos) <= 25.0f)
                        p.Emit("flovmp:chat:msg", "try", acc.Username, $"{tryAction} | {outcomeTag}");
                return;

            case "todo":
                if (args.Length == 0) { SendSystem(player, "Использование: /todo <фраза*действие>"); return; }
                var rawTodo = string.Join(' ', args);
                var parts = rawTodo.Split('*', 2);
                var speech = parts[0].Trim();
                var actionPart = parts.Length > 1 ? parts[1].Trim() : "";
                var todoPos = player.Position;
                var todoDim = player.Dimension;
                foreach (var p in Alt.GetAllPlayers())
                    if (p.Exists && _accountOf(p) is not null && p.Dimension == todoDim && p.Position.Distance(todoPos) <= 25.0f)
                        p.Emit("flovmp:chat:msg", "todo", acc.Username, $"\"{speech}\", — сказал {acc.Username}, {actionPart}");
                return;

            case "engine":
                if (player.Vehicle != null)
                {
                    float fuel = 100.0f;
                    if (player.Vehicle.GetStreamSyncedMetaData("fuel", out float fVal))
                        fuel = fVal;

                    if (fuel <= 0.05f && !player.Vehicle.EngineOn)
                    {
                        SendSystem(player, "В баке нет топлива! Двигатель не заводится.");
                        return;
                    }

                    player.Vehicle.EngineOn = !player.Vehicle.EngineOn;
                    var engStatus = player.Vehicle.EngineOn ? "Двигатель заведён." : "Двигатель заглушен.";
                    SendSystem(player, engStatus);
                    BroadcastNearbyMe(player, player.Vehicle.EngineOn ? "повернул ключ зажигания и завёл двигатель" : "повернул ключ зажигания и заглушил двигатель");
                }
                else
                {
                    SendSystem(player, "Вы должны находиться в транспортном средстве.");
                }
                return;

            case "lock":
                IVehicle? lockVeh = player.Vehicle ?? FindNearestVehicle(player.Position, player.Dimension, 5.0f);
                if (lockVeh == null)
                {
                    SendSystem(player, "Рядом с вами нет транспортного средства (максимум 5 метров).");
                    return;
                }

                // Проверка прав на ключ: админ 4+, владелец, либо транспорт без назначенного владельца
                bool canLock = acc.AdminLevel >= 4;
                if (!canLock)
                {
                    if (lockVeh.GetMetaData("ownerAccountId", out int ownerId))
                    {
                        canLock = (ownerId == acc.Id);
                    }
                    else
                    {
                        lockVeh.SetMetaData("ownerAccountId", acc.Id);
                        canLock = true;
                    }
                }

                if (!canLock)
                {
                    SendSystem(player, "У вас нет ключей от этого транспортного средства.");
                    return;
                }

                lockVeh.LockState = lockVeh.LockState == AltV.Net.Enums.VehicleLockState.Locked
                    ? AltV.Net.Enums.VehicleLockState.Unlocked
                    : AltV.Net.Enums.VehicleLockState.Locked;

                bool isLockedNow = lockVeh.LockState == AltV.Net.Enums.VehicleLockState.Locked;
                SendSystem(player, isLockedNow ? "Двери заблокированы." : "Двери разблокированы.");
                BroadcastNearbyMe(player, isLockedNow ? "нажал кнопку брелока сигнализации и заблокировал двери" : "нажал кнопку брелока сигнализации и разблокировал двери");
                return;

            case "passport":
                if (_documents != null)
                {
                    var passTarget = args.Length > 0 ? FindPlayer(args[0]) : player;
                    if (passTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                    if (passTarget != player && (player.Dimension != passTarget.Dimension || player.Position.Distance(passTarget.Position) > 3.0f))
                    {
                        SendSystem(player, "Игрок находится слишком далеко (максимум 3 метра).");
                        return;
                    }
                    var targetAcc = _accountOf(passTarget);
                    if (targetAcc == null) { SendSystem(player, "Аккаунт не найден."); return; }

                    var pass = _documents.GetDocument(targetAcc.Id, DocumentType.Passport)
                               ?? FloVMP.Gamemode.Presets.DerzhavaDocuments.IssueRussianPassport(_documents, targetAcc.Id, targetAcc.Username, DateTime.UtcNow.AddYears(-25), "Мужской", "г. Москва, ул. Тверская, д. 1");

                    SendSystem(player, $"=== Паспорт гражданина РФ (№ {pass.DocumentNumber}) ===");
                    SendSystem(player, $"ФИО: {pass.FullName} | Пол: {pass.GetMeta("Gender")} | Рождение: {pass.GetMeta("BirthDate")}");
                    SendSystem(player, $"Прописка: {pass.GetMeta("Residence")} | Кем выдан: {pass.IssuedBy}");
                    if (passTarget != player)
                    {
                        SendSystem(passTarget, $"{acc.Username} показал вам свой паспорт.");
                    }
                }
                return;

            case "lic":
            case "licenses":
                if (_documents != null)
                {
                    var licTarget = args.Length > 0 ? FindPlayer(args[0]) : player;
                    if (licTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                    if (licTarget != player && (player.Dimension != licTarget.Dimension || player.Position.Distance(licTarget.Position) > 3.0f))
                    {
                        SendSystem(player, "Игрок находится слишком далеко (максимум 3 метра).");
                        return;
                    }
                    var targetAcc = _accountOf(licTarget);
                    if (targetAcc == null) { SendSystem(player, "Аккаунт не найден."); return; }

                    var drvLic = _documents.GetDocument(targetAcc.Id, DocumentType.DriverLicense)
                                 ?? FloVMP.Gamemode.Presets.DerzhavaDocuments.IssueRussianDriverLicense(_documents, targetAcc.Id, targetAcc.Username, new[] { "B" });
                    var wepLic = _documents.GetDocument(targetAcc.Id, DocumentType.WeaponLicense);
                    var medCard = _documents.GetDocument(targetAcc.Id, DocumentType.MedicalCard);

                    SendSystem(player, $"=== Пакет документов: {targetAcc.Username} ===");
                    SendSystem(player, $"Водительские права: Категории [{drvLic.GetMeta("Categories")}] | Действует: {(drvLic.IsValid ? "Да" : "Нет/Истёк")}");
                    SendSystem(player, $"Лицензия на оружие (РОХа): {(wepLic != null && wepLic.IsValid ? "Действует (" + wepLic.DocumentNumber + ")" : "Отсутствует")}");
                    SendSystem(player, $"Медицинская карта: {(medCard != null && medCard.IsValid ? medCard.GetMeta("OverallStatus") : "Не пройдена")}");
                    if (licTarget != player)
                    {
                        SendSystem(licTarget, $"{acc.Username} просмотрел ваш пакет документов.");
                    }
                }
                return;

            case "factions":
                if (_factions != null)
                {
                    SendSystem(player, "=== Государственные и общественные организации ===");
                    foreach (var f in _factions.GetAllFactions())
                    {
                        var count = _factions.GetFactionMembers(f.Id).Count;
                        SendSystem(player, $"[{f.Id}] {f.Tag} — {f.Name} (Сотрудников: {count}, Казна: {f.TreasuryBalance:N0} руб.)");
                    }
                }
                return;

            case "f":
            case "r":
                if (args.Length == 0) { SendSystem(player, "Использование: /f <сообщение в рацию организации>"); return; }
                if (_factions != null)
                {
                    var mem = _factions.GetMember(acc.Id);
                    if (mem == null) { SendSystem(player, "Вы не состоите в организации."); return; }
                    var fac = _factions.GetFaction(mem.FactionId);
                    var rank = fac?.GetRank(mem.RankLevel);
                    var fMsg = string.Join(' ', args);

                    foreach (var p in Alt.GetAllPlayers())
                    {
                        if (!p.Exists) continue;
                        var pAcc = _accountOf(p);
                        if (pAcc != null && _factions.GetMember(pAcc.Id)?.FactionId == mem.FactionId)
                        {
                            p.Emit("flovmp:chat:msg", "cmd", $"[Р] {rank?.Name ?? "Сотрудник"} {acc.Username}", fMsg);
                        }
                    }
                }
                return;

            case "d":
                if (args.Length == 0) { SendSystem(player, "Использование: /d <сообщение в рацию департамента>"); return; }
                if (_factions != null)
                {
                    var mem = _factions.GetMember(acc.Id);
                    if (mem == null) { SendSystem(player, "Вы не состоите в государственной организации."); return; }
                    var fac = _factions.GetFaction(mem.FactionId);
                    if (fac == null || !fac.IsGovernment) { SendSystem(player, "Доступ к рации департамента есть только у государственных структур."); return; }
                    if (!_factions.HasPermission(acc.Id, FactionPermissions.RadioDepartment))
                    {
                        SendSystem(player, "У вас нет допуска к общей волне департамента.");
                        return;
                    }
                    var rank = fac.GetRank(mem.RankLevel);
                    var dMsg = string.Join(' ', args);

                    foreach (var p in Alt.GetAllPlayers())
                    {
                        if (!p.Exists) continue;
                        var pAcc = _accountOf(p);
                        if (pAcc != null)
                        {
                            var targetMem = _factions.GetMember(pAcc.Id);
                            if (targetMem != null)
                            {
                                var targetFac = _factions.GetFaction(targetMem.FactionId);
                                if (targetFac != null && targetFac.IsGovernment)
                                {
                                    p.Emit("flovmp:chat:msg", "cmd", $"[Департамент] [{fac.Tag}] {rank?.Name ?? "Офицер"} {acc.Username}", dMsg);
                                }
                            }
                        }
                    }
                }
                return;

            case "invite":
                if (args.Length == 0) { SendSystem(player, "Использование: /invite <ID/ник>"); return; }
                if (_factions != null)
                {
                    var invTarget = FindPlayer(args[0]);
                    if (invTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                    var invAcc = _accountOf(invTarget);
                    if (invAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                    if (player.Dimension != invTarget.Dimension || player.Position.Distance(invTarget.Position) > 5.0f)
                    {
                        SendSystem(player, "Игрок находится слишком далеко от вас.");
                        return;
                    }

                    if (_factions.TryInvite(acc.Id, invAcc.Id, out var invErr))
                    {
                        var myMem = _factions.GetMember(acc.Id)!;
                        var fac = _factions.GetFaction(myMem.FactionId);
                        SendSystem(player, $"Вы приняли {invAcc.Username} в организацию {fac?.Name}.");
                        SendSystem(invTarget, $"{acc.Username} принял вас в организацию {fac?.Name} на должность Рядовой/Стажёр.");
                    }
                    else
                    {
                        SendSystem(player, invErr);
                    }
                }
                return;

            case "uninvite":
                if (args.Length == 0) { SendSystem(player, "Использование: /uninvite <ID/ник> [причина]"); return; }
                if (_factions != null)
                {
                    var uninvTarget = FindPlayer(args[0]);
                    if (uninvTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                    var uninvAcc = _accountOf(uninvTarget);
                    if (uninvAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                    var reason = args.Length > 1 ? string.Join(' ', args.Skip(1)) : "Собственное желание / нарушение устава";

                    if (_factions.TryKick(acc.Id, uninvAcc.Id, reason, out var uninvErr))
                    {
                        SendSystem(player, $"Вы уволили {uninvAcc.Username} из организации. Причина: {reason}");
                        SendSystem(uninvTarget, $"Вы были уволены из организации офицером {acc.Username}. Причина: {reason}");
                    }
                    else
                    {
                        SendSystem(player, uninvErr);
                    }
                }
                return;

            case "giverank":
                if (args.Length < 2 || !int.TryParse(args[1], out var newRank))
                {
                    SendSystem(player, "Использование: /giverank <ID/ник> <номер_ранга>");
                    return;
                }
                if (_factions != null)
                {
                    var rankTarget = FindPlayer(args[0]);
                    if (rankTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                    var rankAcc = _accountOf(rankTarget);
                    if (rankAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }

                    if (_factions.TrySetRank(acc.Id, rankAcc.Id, newRank, out var rankErr))
                    {
                        var targetMem = _factions.GetMember(rankAcc.Id)!;
                        var fac = _factions.GetFaction(targetMem.FactionId)!;
                        var rInfo = fac.GetRank(newRank);
                        SendSystem(player, $"Вы установили ранг '{rInfo?.Name ?? newRank.ToString()}' ({newRank}) для {rankAcc.Username}.");
                        SendSystem(rankTarget, $"Вам присвоен ранг '{rInfo?.Name ?? newRank.ToString()}' ({newRank}) сотрудником {acc.Username}.");
                    }
                    else
                    {
                        SendSystem(player, rankErr);
                    }
                }
                return;

            case "cuff":
                if (args.Length == 0) { SendSystem(player, "Использование: /cuff <ID/ник>"); return; }
                if (_factions != null)
                {
                    var cuffTarget = FindPlayer(args[0]);
                    if (cuffTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                    if (player.Dimension != cuffTarget.Dimension || player.Position.Distance(cuffTarget.Position) > 3.0f)
                    {
                        SendSystem(player, "Игрок находится слишком далеко от вас (максимум 3 метра).");
                        return;
                    }
                    var cuffAcc = _accountOf(cuffTarget);
                    if (cuffAcc == null) { SendSystem(player, "Аккаунт не найден."); return; }

                    if (_factions.TryCuff(acc.Id, cuffAcc.Id, out var cuffErr))
                    {
                        SendSystem(player, $"Вы надели наручники на {cuffAcc.Username}.");
                        SendSystem(cuffTarget, $"{acc.Username} надел на вас наручники.");
                        foreach (var p in Alt.GetAllPlayers())
                        {
                            if (p.Exists && p.Dimension == player.Dimension && p.Position.Distance(player.Position) <= 20.0f)
                                p.Emit("flovmp:chat:msg", "me", acc.Username, $"достал наручники и зафиксировал руки {cuffAcc.Username}");
                        }
                    }
                    else
                    {
                        SendSystem(player, cuffErr);
                    }
                }
                return;

            case "uncuff":
                if (args.Length == 0) { SendSystem(player, "Использование: /uncuff <ID/ник>"); return; }
                if (_factions != null)
                {
                    var uncuffTarget = FindPlayer(args[0]);
                    if (uncuffTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                    if (player.Dimension != uncuffTarget.Dimension || player.Position.Distance(uncuffTarget.Position) > 3.0f)
                    {
                        SendSystem(player, "Игрок находится слишком далеко от вас (максимум 3 метра).");
                        return;
                    }
                    var uncuffAcc = _accountOf(uncuffTarget);
                    if (uncuffAcc == null) { SendSystem(player, "Аккаунт не найден."); return; }

                    if (_factions.TryUncuff(acc.Id, uncuffAcc.Id, out var uncuffErr))
                    {
                        SendSystem(player, $"Вы сняли наручники с {uncuffAcc.Username}.");
                        SendSystem(uncuffTarget, $"{acc.Username} снял с вас наручники.");
                        foreach (var p in Alt.GetAllPlayers())
                        {
                            if (p.Exists && p.Dimension == player.Dimension && p.Position.Distance(player.Position) <= 20.0f)
                                p.Emit("flovmp:chat:msg", "me", acc.Username, $"достал ключ и расстегнул наручники на руках {uncuffAcc.Username}");
                        }
                    }
                    else
                    {
                        SendSystem(player, uncuffErr);
                    }
                }
                return;

            case "arrest":
                if (args.Length < 2 || !int.TryParse(args[1], out var arrestMinutes) || arrestMinutes <= 0)
                {
                    SendSystem(player, "Использование: /arrest <ID/ник> <минуты (1-120)> [статья/причина]");
                    return;
                }
                if (_factions != null)
                {
                    var arrTarget = FindPlayer(args[0]);
                    if (arrTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                    if (player.Dimension != arrTarget.Dimension || player.Position.Distance(arrTarget.Position) > 5.0f)
                    {
                        SendSystem(player, "Игрок находится слишком далеко от вас.");
                        return;
                    }
                    var arrAcc = _accountOf(arrTarget);
                    if (arrAcc == null) { SendSystem(player, "Аккаунт не найден."); return; }
                    var arrReason = args.Length > 2 ? string.Join(' ', args.Skip(2)) : "Нарушение УК РФ";
                    var arrestSec = arrestMinutes * 60;

                    if (_factions.TryArrest(acc.Id, arrAcc.Id, arrestSec, arrReason, out var arrErr))
                    {
                        // Перемещение в ИВС ГУ МВД
                        arrTarget.Dimension = 0;
                        arrTarget.Position = new Position(459.4f, -997.8f, 24.9f);
                        _notifyTeleport?.Invoke(arrTarget, arrTarget.Position);
                        arrTarget.RemoveAllWeapons(true);

                        SendSystem(player, $"Вы оформили {arrAcc.Username} в КПЗ на {arrestMinutes} мин. Причина: {arrReason}");
                        SendSystem(arrTarget, $"Вы помещены в камеру предварительного заключения на {arrestMinutes} мин. Причина: {arrReason}");
                        Broadcast($"[ГУ МВД] {arrAcc.Username} был заключён под стражу сотрудником {acc.Username}. Статья: {arrReason}");
                    }
                    else
                    {
                        SendSystem(player, arrErr);
                    }
                }
                return;

            case "buyhouse":
            case "buyproperty":
                if (_housing != null)
                {
                    var pPos = new FloVMP.Core.AntiCheat.Vector3D(player.Position.X, player.Position.Y, player.Position.Z);
                    var nearbyProp = _housing.GetNearbyProperty(pPos, radius: 4.0f);
                    if (nearbyProp == null)
                    {
                        SendSystem(player, "Вы должны находиться у входа в объект недвижимости.");
                        return;
                    }

                    if (_housing.TryBuy(acc, nearbyProp.Id, out var buyErr))
                    {
                        _saveAccount?.Invoke(acc);
                        SendSystem(player, $"Поздравляем с покупкой недвижимости! Адрес: {nearbyProp.Address}");
                        SendSystem(player, $"С вашего банковского счёта списано: {nearbyProp.Price:N0} руб. Баланс: {acc.Bank:N0} руб.");
                    }
                    else
                    {
                        SendSystem(player, buyErr);
                    }
                }
                return;

            case "sellhouse":
            case "sellproperty":
                if (_housing != null)
                {
                    var pPos = new FloVMP.Core.AntiCheat.Vector3D(player.Position.X, player.Position.Y, player.Position.Z);
                    var nearbyProp = _housing.GetNearbyProperty(pPos, radius: 4.0f);
                    if (nearbyProp == null)
                    {
                        SendSystem(player, "Вы должны находиться у своего объекта недвижимости.");
                        return;
                    }

                    if (_housing.TrySell(acc, nearbyProp.Id, out var refund, out var sellErr))
                    {
                        _saveAccount?.Invoke(acc);
                        SendSystem(player, $"Вы продали недвижимость по адресу: {nearbyProp.Address}");
                        SendSystem(player, $"Государственная выплата (75% + сейф): +{refund:N0} руб. Зачислено на банковский счёт.");
                    }
                    else
                    {
                        SendSystem(player, sellErr);
                    }
                }
                return;

            case "enter":
                if (_housing != null)
                {
                    var pPos = new FloVMP.Core.AntiCheat.Vector3D(player.Position.X, player.Position.Y, player.Position.Z);
                    var nearbyProp = _housing.GetNearbyEntrance(pPos, 3.0f);
                    if (nearbyProp == null)
                    {
                        SendSystem(player, "Рядом с вами нет входа в дом или квартиру.");
                        return;
                    }

                    if (nearbyProp.IsLocked && !nearbyProp.HasAccess(acc.Id))
                    {
                        SendSystem(player, "Дверь заперта на замок.");
                        return;
                    }

                    player.Dimension = nearbyProp.Dimension;
                    player.Position = new Position(nearbyProp.InteriorPosition.X, nearbyProp.InteriorPosition.Y, nearbyProp.InteriorPosition.Z);
                    _notifyTeleport?.Invoke(player, player.Position);
                    SendSystem(player, $"Вы вошли в помещение: {nearbyProp.Address}");
                }
                return;

            case "exit":
                if (_housing != null)
                {
                    var insideProp = _housing.GetPropertyByDimension(player.Dimension);
                    if (insideProp == null && player.Dimension != 0)
                    {
                        player.Dimension = 0;
                        player.Position = SpawnPoints.MoscowRedSquare;
                        _notifyTeleport?.Invoke(player, player.Position);
                        SendSystem(player, "Вы вышли на улицу.");
                        return;
                    }

                    if (insideProp != null)
                    {
                        player.Dimension = 0;
                        player.Position = new Position(insideProp.EntrancePosition.X, insideProp.EntrancePosition.Y, insideProp.EntrancePosition.Z);
                        _notifyTeleport?.Invoke(player, player.Position);
                        SendSystem(player, $"Вы вышли на улицу: {insideProp.Address}");
                    }
                    else
                    {
                        SendSystem(player, "Вы не находитесь внутри помещения.");
                    }
                }
                return;

            case "hlock":
                if (_housing != null)
                {
                    var pPos = new FloVMP.Core.AntiCheat.Vector3D(player.Position.X, player.Position.Y, player.Position.Z);
                    var prop = _housing.GetAllProperties().FirstOrDefault(p =>
                        p.EntrancePosition.DistanceTo(pPos) <= 3.0f || (p.Dimension == player.Dimension && p.InteriorPosition.DistanceTo(pPos) <= 3.0f));

                    if (prop == null)
                    {
                        SendSystem(player, "Вы должны находиться у двери дома или квартиры.");
                        return;
                    }

                    if (_housing.TryToggleLock(acc.Id, prop.Id, out bool isLocked, out var lockErr))
                    {
                        SendSystem(player, isLocked ? "Вы заперли входную дверь на замок." : "Вы открыли входную дверь.");
                    }
                    else
                    {
                        SendSystem(player, lockErr);
                    }
                }
                return;

            case "house":
                if (_housing != null)
                {
                    var owned = _housing.GetPropertiesByOwner(acc.Id);
                    if (owned.Count == 0)
                    {
                        SendSystem(player, "У вас нет в собственности недвижимости. Найдите свободный дом на карте и введите /buyhouse.");
                        return;
                    }

                    SendSystem(player, $"=== Ваша недвижимость ({owned.Count} объекта) ===");
                    foreach (var h in owned)
                    {
                        SendSystem(player, $"[{h.Id}] {h.Address} ({h.Type}) — Замок: {(h.IsLocked ? "Закрыт" : "Открыт")} | Сейф: {h.SafeCash:N0} руб. | Подселено: {h.Roommates.Count}");
                    }
                }
                return;

            case "hdeposit":
                if (args.Length < 2 || !int.TryParse(args[0], out var depHId) || !long.TryParse(args[1], out var depSafeAmt) || depSafeAmt <= 0)
                {
                    SendSystem(player, "Использование: /hdeposit <ID_недвижимости> <сумма>");
                    return;
                }
                if (_housing != null)
                {
                    if (_housing.TryDepositSafe(acc, depHId, depSafeAmt, out var sDepErr))
                    {
                        _saveAccount?.Invoke(acc);
                        var prop = _housing.GetProperty(depHId);
                        SendSystem(player, $"Вы положили {depSafeAmt:N0} руб. в сейф дома [{depHId}]. В сейфе: {prop?.SafeCash:N0} руб.");
                    }
                    else
                    {
                        SendSystem(player, sDepErr);
                    }
                }
                return;

            case "hwithdraw":
                if (args.Length < 2 || !int.TryParse(args[0], out var withHId) || !long.TryParse(args[1], out var withSafeAmt) || withSafeAmt <= 0)
                {
                    SendSystem(player, "Использование: /hwithdraw <ID_недвижимости> <сумма>");
                    return;
                }
                if (_housing != null)
                {
                    if (_housing.TryWithdrawSafe(acc, withHId, withSafeAmt, out var sWithErr))
                    {
                        _saveAccount?.Invoke(acc);
                        var prop = _housing.GetProperty(withHId);
                        SendSystem(player, $"Вы взяли {withSafeAmt:N0} руб. из сейфа дома [{withHId}]. В сейфе осталось: {prop?.SafeCash:N0} руб.");
                    }
                    else
                    {
                        SendSystem(player, sWithErr);
                    }
                }
                return;

            case "haddmate":
                if (args.Length < 2 || !int.TryParse(args[0], out var addHId))
                {
                    SendSystem(player, "Использование: /haddmate <ID_недвижимости> <ID/ник_жильца>");
                    return;
                }
                if (_housing != null)
                {
                    var targetMate = FindPlayer(args[1]);
                    if (targetMate == null) { SendSystem(player, "Игрок не найден."); return; }
                    var mateAcc = _accountOf(targetMate);
                    if (mateAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }

                    if (_housing.TryAddRoommate(acc.Id, addHId, mateAcc.Id, out var addMateErr))
                    {
                        SendSystem(player, $"Вы подселили {mateAcc.Username} в свой объект недвижимости [{addHId}].");
                        SendSystem(targetMate, $"Гражданин {acc.Username} подселил вас в дом/квартиру [{addHId}]. Теперь у вас есть ключ!");
                    }
                    else
                    {
                        SendSystem(player, addMateErr);
                    }
                }
                return;

            case "hdelmate":
                if (args.Length < 2 || !int.TryParse(args[0], out var delHId))
                {
                    SendSystem(player, "Использование: /hdelmate <ID_недвижимости> <ID/ник_жильца>");
                    return;
                }
                if (_housing != null)
                {
                    var targetDel = FindPlayer(args[1]);
                    Account? delAcc = targetDel != null ? _accountOf(targetDel) : _findAccountByName?.Invoke(args[1]);
                    if (delAcc == null) { SendSystem(player, "Аккаунт жильца не найден."); return; }

                    if (_housing.TryRemoveRoommate(acc.Id, delHId, delAcc.Id, out var delMateErr))
                    {
                        SendSystem(player, $"Вы выселили {delAcc.Username} из объекта недвижимости [{delHId}].");
                        if (targetDel != null && targetDel.Exists)
                        {
                            SendSystem(targetDel, $"Гражданин {acc.Username} аннулировал ваше подселение в объект [{delHId}].");
                        }
                    }
                    else
                    {
                        SendSystem(player, delMateErr);
                    }
                }
                return;
        }

        // 2. Проверка административных прав
        var adminDef = AdminCommandRegistry.Get(cmd);
        if (adminDef != null)
        {
            if (acc.AdminLevel < adminDef.MinLevel)
            {
                SendSystem(player, $"Недостаточно прав. Требуется ранг: {AdminTitles.GetTitle(adminDef.MinLevel)} ({adminDef.MinLevel}+ lvl).");
                return;
            }

            HandleAdminCommand(player, acc, cmd, args, adminDef);
            return;
        }

        SendSystem(player, $"Неизвестная команда: /{cmd}. Введите /help для списка.");
    }

    // Команды, которые нельзя применять к админу РАВНОГО или ВЫШЕ ранга —
    // иначе средний админ мог бы забанить/разжаловать/обокрасть руководство
    // или устроить админ-войну. Руководитель проекта (8) — исключение.
    private static readonly HashSet<string> RankSensitiveCmds = new(StringComparer.Ordinal)
    {
        "freeze", "kick", "mute", "jail", "ban", "banip", "slap", "warn", "bansc",
        "hwidban", "macban", "hardban", "takemoney", "sethp", "setarmor", "setskin",
        "setdim", "gethere", "tp", "tpm", "makeadmin", "clearadmin", "setadminlevel",
    };

    private void HandleAdminCommand(IPlayer player, Account acc, string cmd, string[] args, AdminCommandDef def)
    {
        // Защита иерархии: нельзя трогать равного/старшего админа.
        if (RankSensitiveCmds.Contains(cmd) && args.Length > 0 && acc.AdminLevel < 8)
        {
            var victim = FindPlayer(args[0]) is { } vp ? _accountOf(vp) : _findAccountByName?.Invoke(args[0]);
            if (victim != null && victim.Id != acc.Id && victim.AdminLevel >= acc.AdminLevel)
            {
                SendSystem(player, $"Нельзя применить /{cmd} к администрации равного или старшего ранга ({AdminTitles.GetTitle(victim.AdminLevel)}).");
                GameLog.Admin("blocked_hierarchy", LogActor.Admin(acc.Id, acc.Username), victim.Username, ("cmd", cmd));
                return;
            }
        }

        switch (cmd)
        {
            // ── Уровень 1: Хелпер ──────────────────────
            case "a":
                if (args.Length == 0) { SendSystem(player, "Использование: /a <текст>"); return; }
                var aMsg = string.Join(' ', args);
                var prefix = AdminTitles.GetPrefix(acc.AdminLevel);
                BroadcastAdmin($"{prefix} {acc.Username} ({player.Id}): {aMsg}");
                break;

            case "o":
                if (args.Length == 0) { SendSystem(player, "Использование: /o <сообщение>"); return; }
                var oMsg = string.Join(' ', args);
                var oPrefix = AdminTitles.GetPrefix(acc.AdminLevel);
                Broadcast($"[ОБЪЯВЛЕНИЕ] {oPrefix} {acc.Username} [{player.Id}]: {oMsg}");
                GameLog.Admin("global_announce", LogActor.Admin(acc.Id, acc.Username), oMsg);
                break;

            case "stats":
                var targetStats = args.Length > 0 ? FindPlayer(args[0]) : player;
                if (targetStats == null) { SendSystem(player, "Игрок не найден."); return; }
                var tAcc = _accountOf(targetStats);
                var tPos = targetStats.Position;
                SendSystem(player, $"--- Статистика {tAcc?.Username ?? targetStats.Name} (ID: {targetStats.Id}) ---");
                SendSystem(player, $"Наличные: {tAcc?.Cash ?? 0} руб. | Админ-ранг: {AdminTitles.GetTitle(tAcc?.AdminLevel ?? 0)} ({tAcc?.AdminLevel ?? 0} lvl)");
                SendSystem(player, $"Здоровье: {targetStats.Health} | Броня: {targetStats.Armor} | Пинг: {targetStats.Ping} мс");
                SendSystem(player, $"Позиция: X: {tPos.X:0.0}, Y: {tPos.Y:0.0}, Z: {tPos.Z:0.0}");
                break;

            case "freeze":
                if (args.Length == 0) { SendSystem(player, "Использование: /freeze <ID/ник>"); return; }
                var freezeTarget = FindPlayer(args[0]);
                if (freezeTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                freezeTarget.Frozen = true;
                SendSystem(player, $"Вы заморозили {freezeTarget.Name} (ID {freezeTarget.Id}).");
                SendSystem(freezeTarget, "Вы были заморожены администратором.");
                break;

            case "unfreeze":
                if (args.Length == 0) { SendSystem(player, "Использование: /unfreeze <ID/ник>"); return; }
                var unfreezeTarget = FindPlayer(args[0]);
                if (unfreezeTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                unfreezeTarget.Frozen = false;
                SendSystem(player, $"Вы разморозили {unfreezeTarget.Name} (ID {unfreezeTarget.Id}).");
                SendSystem(unfreezeTarget, "Вы были разморожены администратором.");
                break;

            case "ans":
                if (args.Length < 2) { SendSystem(player, "Использование: /ans <ID/ник> <ответ>"); return; }
                var ansTarget = FindPlayer(args[0]);
                if (ansTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var answer = string.Join(' ', args.Skip(1));
                SendSystem(ansTarget, $"[Ответ от {acc.Username}]: {answer}");
                SendSystem(player, $"[Ответ для {ansTarget.Name}]: {answer}");
                break;

            case "sp":
                if (args.Length == 0) { SendSystem(player, "Использование: /sp <ID/ник>"); return; }
                var spTarget = FindPlayer(args[0]);
                if (spTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                if (spTarget == player) { SendSystem(player, "Нельзя следить за самим собой."); return; }
                _spectatingAdmins[player.Id] = (player.Position, player.Dimension);
                player.Dimension = spTarget.Dimension;
                player.Position = spTarget.Position + new Position(0, 0, 2.0f);
                _notifyTeleport?.Invoke(player, player.Position);
                player.Visible = false;
                SendSystem(player, $"Вы вошли в режим слежки за {spTarget.Name} (ID {spTarget.Id}). Для выхода введите /spoff.");
                GameLog.Admin("spectate", LogActor.Admin(acc.Id, acc.Username), spTarget.Name);
                break;

            case "spoff":
                if (_spectatingAdmins.TryRemove(player.Id, out var orig))
                {
                    player.Position = orig.originalPos;
                    player.Dimension = orig.originalDim;
                    _notifyTeleport?.Invoke(player, player.Position);
                    player.Visible = true;
                    SendSystem(player, "Вы вышли из режима слежки и вернулись на исходную позицию.");
                }
                else
                {
                    player.Visible = true;
                    SendSystem(player, "Вы не находитесь в режиме слежки.");
                }
                break;

            // ── Уровень 2: Модератор ───────────────────
            case "goto":
                if (args.Length == 0) { SendSystem(player, "Использование: /goto <ID/ник>"); return; }
                var gotoTarget = FindPlayer(args[0]);
                if (gotoTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                player.Position = gotoTarget.Position + new Position(0, 1.0f, 0.5f);
                _notifyTeleport?.Invoke(player, player.Position);
                SendSystem(player, $"Вы телепортировались к {gotoTarget.Name}.");
                break;

            case "gethere":
                if (args.Length == 0) { SendSystem(player, "Использование: /gethere <ID/ник>"); return; }
                var gethereTarget = FindPlayer(args[0]);
                if (gethereTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                gethereTarget.Position = player.Position + new Position(0, 1.0f, 0.5f);
                _notifyTeleport?.Invoke(gethereTarget, gethereTarget.Position);
                SendSystem(player, $"Вы телепортировали к себе {gethereTarget.Name}.");
                SendSystem(gethereTarget, "Вы были телепортированы администратором.");
                break;

            case "kick":
                if (args.Length == 0) { SendSystem(player, "Использование: /kick <ID/ник> [причина]"); return; }
                var kickTarget = FindPlayer(args[0]);
                if (kickTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var kickReason = args.Length > 1 ? string.Join(' ', args.Skip(1)) : "Нарушение правил";
                Broadcast($"[Кик] {kickTarget.Name} был исключён администратором {acc.Username}. Причина: {kickReason}");
                GameLog.Punishment("kick", LogActor.Admin(acc.Id, acc.Username), kickTarget.Name, kickReason);
                kickTarget.Kick(kickReason);
                break;

            case "mute":
                if (args.Length < 2 || !int.TryParse(args[1], out var muteMins) || muteMins <= 0)
                {
                    SendSystem(player, "Использование: /mute <ID/ник> <минут> [причина]");
                    return;
                }
                var muteTarget = FindPlayer(args[0]);
                if (muteTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var muteAcc = _accountOf(muteTarget);
                if (muteAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                var muteReason = args.Length > 2 ? string.Join(' ', args.Skip(2)) : "Нарушение правил чата";
                var muteUntil = DateTime.UtcNow.AddMinutes(muteMins);
                muteAcc.MuteUntilUtc = muteUntil.ToString("O");
                _saveAccount?.Invoke(muteAcc);
                Broadcast($"[Мут] {muteTarget.Name} получил блокировку чата на {muteMins} мин. от администратора {acc.Username}. Причина: {muteReason}");
                GameLog.Punishment("mute", LogActor.Admin(acc.Id, acc.Username), muteTarget.Name, muteReason, muteMins * 60);
                break;

            case "unmute":
                if (args.Length == 0) { SendSystem(player, "Использование: /unmute <ID/ник>"); return; }
                var unmuteTarget = FindPlayer(args[0]);
                if (unmuteTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var unmuteAcc = _accountOf(unmuteTarget);
                if (unmuteAcc != null)
                {
                    unmuteAcc.MuteUntilUtc = "";
                    _saveAccount?.Invoke(unmuteAcc);
                }
                Broadcast($"[Размут] {unmuteTarget.Name} был размучен администратором {acc.Username}.");
                GameLog.Admin("unmute", LogActor.Admin(acc.Id, acc.Username), unmuteTarget.Name);
                break;

            case "jail":
                if (args.Length < 2 || !int.TryParse(args[1], out var jailMins) || jailMins <= 0)
                {
                    SendSystem(player, "Использование: /jail <ID/ник> <минут> [причина]");
                    return;
                }
                var jailTarget = FindPlayer(args[0]);
                if (jailTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var jailAcc = _accountOf(jailTarget);
                if (jailAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                var jailReason = args.Length > 2 ? string.Join(' ', args.Skip(2)) : "Нарушение правил сервера";
                var jailUntil = DateTime.UtcNow.AddMinutes(jailMins);
                jailAcc.JailUntilUtc = jailUntil.ToString("O");
                _saveAccount?.Invoke(jailAcc);

                jailTarget.RemoveAllWeapons(true);
                jailTarget.Dimension = FloVMP.Core.World.DimensionManager.AdminJailDimension;
                jailTarget.Position = new Position(1651.2f, 2570.3f, 45.5f);
                _notifyTeleport?.Invoke(jailTarget, jailTarget.Position);

                Broadcast($"[Деморган] {jailTarget.Name} отправлен в деморган на {jailMins} мин. администратором {acc.Username}. Причина: {jailReason}");
                GameLog.Punishment("jail", LogActor.Admin(acc.Id, acc.Username), jailTarget.Name, jailReason, jailMins * 60);
                break;

            case "unjail":
                if (args.Length == 0) { SendSystem(player, "Использование: /unjail <ID/ник>"); return; }
                var unjailTarget = FindPlayer(args[0]);
                if (unjailTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var unjailAcc = _accountOf(unjailTarget);
                if (unjailAcc != null)
                {
                    unjailAcc.JailUntilUtc = "";
                    _saveAccount?.Invoke(unjailAcc);
                }
                unjailTarget.Dimension = 0;
                unjailTarget.Position = SpawnPoints.MoscowRedSquare;
                _notifyTeleport?.Invoke(unjailTarget, unjailTarget.Position);
                Broadcast($"[Деморган] {unjailTarget.Name} освобождён из деморгана администратором {acc.Username}.");
                GameLog.Admin("unjail", LogActor.Admin(acc.Id, acc.Username), unjailTarget.Name);
                break;

            // ── Уровень 3: Старший Модератор ──────────
            case "ban":
                if (args.Length < 2 || !int.TryParse(args[1], out var banDays) || banDays <= 0)
                {
                    SendSystem(player, "Использование: /ban <ID/ник> <дней> [причина]");
                    return;
                }
                var banTarget = FindPlayer(args[0]);
                if (banTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var banAcc = _accountOf(banTarget);
                if (banAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                var banReason = args.Length > 2 ? string.Join(' ', args.Skip(2)) : "Нарушение правил";
                banAcc.IsBanned = true;
                banAcc.BanReason = banReason;
                banAcc.BanUntilUtc = DateTime.UtcNow.AddDays(banDays).ToString("O");
                _saveAccount?.Invoke(banAcc);
                Broadcast($"[Бан] {banTarget.Name} заблокирован на {banDays} дн. администратором {acc.Username}. Причина: {banReason}");
                GameLog.Punishment("ban", LogActor.Admin(acc.Id, acc.Username), banTarget.Name, banReason, (long)banDays * 86400);
                banTarget.Kick($"Ваш аккаунт заблокирован на {banDays} дн. Причина: {banReason}");
                break;

            case "banip":
                if (args.Length < 2 || !int.TryParse(args[1], out var banipDays) || banipDays <= 0)
                {
                    SendSystem(player, "Использование: /banip <ID/ник> <дней> [причина]");
                    return;
                }
                var banipTarget = FindPlayer(args[0]);
                if (banipTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var banipAcc = _accountOf(banipTarget);
                if (banipAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                var banipReason = args.Length > 2 ? string.Join(' ', args.Skip(2)) : "Блокировка IP и аккаунта";
                banipAcc.IsBanned = true;
                banipAcc.BanReason = $"[IP BAN] {banipReason}";
                banipAcc.BanUntilUtc = DateTime.UtcNow.AddDays(banipDays).ToString("O");
                _saveAccount?.Invoke(banipAcc);
                Broadcast($"[Бан IP] {banipTarget.Name} заблокирован по IP на {banipDays} дн. администратором {acc.Username}. Причина: {banipReason}");
                GameLog.Punishment("banip", LogActor.Admin(acc.Id, acc.Username), banipTarget.Name, banipReason, (long)banipDays * 86400);
                banipTarget.Kick($"Ваш аккаунт и IP заблокированы на {banipDays} дн. Причина: {banipReason}");
                break;

            case "checkban":
                if (args.Length == 0) { SendSystem(player, "Использование: /checkban <ID/ник>"); return; }
                var checkTarget = FindPlayer(args[0]);
                if (checkTarget == null) { SendSystem(player, "Игрок не найден онлайн."); return; }
                var checkAcc = _accountOf(checkTarget);
                SendSystem(player, $"=== Проверка блокировок: {checkTarget.Name} (ID: {checkTarget.Id}) ===");
                SendSystem(player, $"Аккаунт ID: {checkAcc?.Id ?? 0} | IP: {checkTarget.Ip} | SocialClub ID: {checkTarget.SocialClubId}");
                SendSystem(player, $"HWID Hash: {checkTarget.HardwareIdHash:X16} | HWID Ex: {checkTarget.HardwareIdExHash:X16}");
                SendSystem(player, $"Статус бана аккаунта: {(checkAcc?.IsBanned == true ? "ЗАБЛОКИРОВАН до " + checkAcc.BanUntilUtc : "Чист")}");
                break;

            case "unban":
                if (args.Length == 0) { SendSystem(player, "Использование: /unban <ник_игрока>"); return; }
                var unbanName = args[0];
                var unbanAcc = _findAccountByName?.Invoke(unbanName);
                if (unbanAcc == null) { SendSystem(player, $"Аккаунт '{unbanName}' не найден."); return; }
                unbanAcc.IsBanned = false;
                unbanAcc.BanReason = "";
                unbanAcc.BanUntilUtc = "";
                _saveAccount?.Invoke(unbanAcc);
                SendSystem(player, $"Аккаунт '{unbanName}' успешно разблокирован.");
                GameLog.Admin("unban", LogActor.Admin(acc.Id, acc.Username), unbanName);
                break;

            case "slap":
                if (args.Length == 0) { SendSystem(player, "Использование: /slap <ID/ник>"); return; }
                var slapTarget = FindPlayer(args[0]);
                if (slapTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                slapTarget.Position += new Position(0, 0, 2.5f);
                _notifyTeleport?.Invoke(slapTarget, slapTarget.Position);
                SendSystem(player, $"Вы подбросили {slapTarget.Name}.");
                break;

            case "warn":
                if (args.Length == 0) { SendSystem(player, "Использование: /warn <ID/ник> [причина]"); return; }
                var warnTarget = FindPlayer(args[0]);
                if (warnTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var warnAcc = _accountOf(warnTarget);
                if (warnAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                var warnReason = args.Length > 1 ? string.Join(' ', args.Skip(1)) : "Нарушение правил сервера";

                warnAcc.Warns++;
                if (warnAcc.Warns >= 3)
                {
                    warnAcc.Warns = 0;
                    warnAcc.IsBanned = true;
                    warnAcc.BanReason = $"[3/3 Варнов] {warnReason}";
                    warnAcc.BanUntilUtc = DateTime.UtcNow.AddDays(15).ToString("O");
                    _saveAccount?.Invoke(warnAcc);

                    Broadcast($"[Варн] {warnTarget.Name} получил предупреждение [3/3] от {acc.Username} и был заблокирован на 15 дн.! Причина: {warnReason}");
                    GameLog.Punishment("warn_ban", LogActor.Admin(acc.Id, acc.Username), warnTarget.Name, warnReason, 15 * 86400);
                    warnTarget.Kick($"Вы получили 3/3 варнов и заблокированы на 15 дн. Причина: {warnReason}");
                }
                else
                {
                    _saveAccount?.Invoke(warnAcc);
                    Broadcast($"[Варн] {warnTarget.Name} получил предупреждение [{warnAcc.Warns}/3] от администратора {acc.Username}. Причина: {warnReason}");
                    GameLog.Punishment("warn", LogActor.Admin(acc.Id, acc.Username), warnTarget.Name, warnReason);
                }
                break;

            case "unwarn":
                if (args.Length == 0) { SendSystem(player, "Использование: /unwarn <ID/ник>"); return; }
                var unwarnTarget = FindPlayer(args[0]);
                if (unwarnTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var unwarnAcc = _accountOf(unwarnTarget);
                if (unwarnAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                if (unwarnAcc.Warns > 0) unwarnAcc.Warns--;
                _saveAccount?.Invoke(unwarnAcc);
                SendSystem(player, $"Снято предупреждение с {unwarnTarget.Name}. Текущий счёт: {unwarnAcc.Warns}/3.");
                SendSystem(unwarnTarget, $"Администратор {acc.Username} снял с вас предупреждение (осталось {unwarnAcc.Warns}/3).");
                GameLog.Admin("unwarn", LogActor.Admin(acc.Id, acc.Username), unwarnTarget.Name, ("warns", unwarnAcc.Warns));
                break;

            // ── Уровень 4: Администратор ──────────────
            case "bansc":
                if (args.Length < 2 || !int.TryParse(args[1], out var banscDays) || banscDays <= 0)
                {
                    SendSystem(player, "Использование: /bansc <ID/ник> <дней> [причина]");
                    return;
                }
                var banscTarget = FindPlayer(args[0]);
                if (banscTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var banscAcc = _accountOf(banscTarget);
                if (banscAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                var banscReason = args.Length > 2 ? string.Join(' ', args.Skip(2)) : "Блокировка Social Club";
                banscAcc.IsBanned = true;
                banscAcc.BanReason = $"[SC BAN] {banscReason}";
                banscAcc.BanUntilUtc = DateTime.UtcNow.AddDays(banscDays).ToString("O");
                _saveAccount?.Invoke(banscAcc);
                Broadcast($"[Social Club Бан] {banscTarget.Name} заблокирован по лицензии SC на {banscDays} дн. администратором {acc.Username}. Причина: {banscReason}");
                GameLog.Punishment("bansc", LogActor.Admin(acc.Id, acc.Username), banscTarget.Name, banscReason, (long)banscDays * 86400);
                banscTarget.Kick($"Ваш Rockstar Social Club заблокирован на {banscDays} дн. Причина: {banscReason}");
                break;

            case "veh":
                if (args.Length == 0) { SendSystem(player, "Использование: /veh <модель> [цвет1] [цвет2]"); return; }
                var model = args[0];
                try
                {
                    var spawnPos = player.Position + new Position(1.5f, 1.5f, 0.5f);
                    var veh = Alt.CreateVehicle(model, spawnPos, player.Rotation);
                    if (veh != null)
                    {
                        veh.Dimension = player.Dimension;
                        byte c1 = args.Length > 1 && byte.TryParse(args[1], out var parsedC1) ? parsedC1 : (byte)0;
                        byte c2 = args.Length > 2 && byte.TryParse(args[2], out var parsedC2) ? parsedC2 : (byte)0;
                        veh.PrimaryColor = c1;
                        veh.SecondaryColor = c2;
                        veh.SetMetaData("ownerAccountId", acc.Id);
                        veh.SetStreamSyncedMetaData("fuel", 100.0f);
                        SendSystem(player, $"Транспорт '{model}' успешно создан (ID: {veh.Id}).");
                    }
                    else
                    {
                        SendSystem(player, $"Ошибка: модель '{model}' не найдена.");
                    }
                }
                catch (Exception ex)
                {
                    SendSystem(player, $"Не удалось заспавнить транспорт: {ex.Message}");
                }
                break;

            case "dv":
                IVehicle? dvVeh = player.Vehicle ?? FindNearestVehicle(player.Position, player.Dimension, 5.0f);
                if (dvVeh != null)
                {
                    dvVeh.Destroy();
                    SendSystem(player, "Транспорт удалён.");
                }
                else
                {
                    SendSystem(player, "Вы должны находиться в транспорте или рядом с ним (до 5 метров).");
                }
                break;

            case "sethp":
                if (args.Length < 2 || !ushort.TryParse(args[1], out var hp))
                {
                    SendSystem(player, "Использование: /sethp <ID/ник> <кол-во 0-200>");
                    return;
                }
                var hpTarget = FindPlayer(args[0]);
                if (hpTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                hpTarget.Health = hp;
                SendSystem(player, $"Установлено {hp} HP для {hpTarget.Name}.");
                break;

            case "setarmor":
                if (args.Length < 2 || !ushort.TryParse(args[1], out var armor))
                {
                    SendSystem(player, "Использование: /setarmor <ID/ник> <кол-во 0-100>");
                    return;
                }
                var armorTarget = FindPlayer(args[0]);
                if (armorTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                armorTarget.Armor = armor;
                SendSystem(player, $"Установлено {armor} брони для {armorTarget.Name}.");
                break;

            case "repair":
                IVehicle? repVeh = player.Vehicle ?? FindNearestVehicle(player.Position, player.Dimension, 5.0f);
                if (repVeh != null)
                {
                    repVeh.EngineHealth = 1000;
                    repVeh.BodyHealth = 1000;
                    SendSystem(player, "Транспорт отремонтирован.");
                    BroadcastNearbyMe(player, "достал инструменты и восстановил состояние автомобиля");
                }
                else
                {
                    SendSystem(player, "Вы должны находиться в транспорте или рядом с ним (до 5 метров).");
                }
                break;

            case "fuel":
                IVehicle? fuelVeh = player.Vehicle ?? FindNearestVehicle(player.Position, player.Dimension, 5.0f);
                if (fuelVeh != null)
                {
                    fuelVeh.SetStreamSyncedMetaData("fuel", 100.0f);
                    SendSystem(player, "Транспортное средство заправлено на 100%.");
                    BroadcastNearbyMe(player, "заправил бак автомобиля до полного объёма");
                }
                else
                {
                    SendSystem(player, "Вы должны находиться в транспорте или рядом с ним (до 5 метров).");
                }
                break;

            // ── Уровень 5: Старший Администратор ──────
            case "hwidban":
            case "macban":
                if (args.Length < 2 || !int.TryParse(args[1], out var hwidDays) || hwidDays <= 0)
                {
                    SendSystem(player, "Использование: /hwidban <ID/ник> <дней> [причина]");
                    return;
                }
                var hwidTarget = FindPlayer(args[0]);
                if (hwidTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var hwidAcc = _accountOf(hwidTarget);
                if (hwidAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                var hwidReason = args.Length > 2 ? string.Join(' ', args.Skip(2)) : "Аппаратная блокировка читера";
                hwidAcc.IsBanned = true;
                hwidAcc.BanReason = $"[HWID BAN] {hwidReason}";
                hwidAcc.BanUntilUtc = DateTime.UtcNow.AddDays(hwidDays).ToString("O");
                _saveAccount?.Invoke(hwidAcc);
                Broadcast($"[HWID БАН] {hwidTarget.Name} заблокирован по железу (FloV:ID) на {hwidDays} дн. администратором {acc.Username}. Причина: {hwidReason}");
                GameLog.Punishment("hwidban", LogActor.Admin(acc.Id, acc.Username), hwidTarget.Name, hwidReason, (long)hwidDays * 86400);
                hwidTarget.Kick($"Ваш ПК заблокирован по железу на {hwidDays} дн. Причина: {hwidReason}");
                break;

            case "tp":
                if (args.Length < 3 || !float.TryParse(args[0], out var x) || !float.TryParse(args[1], out var y) || !float.TryParse(args[2], out var z))
                {
                    SendSystem(player, "Использование: /tp <X> <Y> <Z>");
                    return;
                }
                player.Position = new Position(x, y, z);
                _notifyTeleport?.Invoke(player, player.Position);
                SendSystem(player, $"Телепортирован в: X: {x:0.0}, Y: {y:0.0}, Z: {z:0.0}");
                break;

            case "tpm":
                var targetPreset = args.Length > 0 ? args[0].ToLowerInvariant() : "redsquare";
                Position targetPos = targetPreset switch
                {
                    "city" or "сити" => SpawnPoints.MoscowCity,
                    "police" or "мвд" or "полиция" => SpawnPoints.MoscowPolice,
                    "hospital" or "больница" or "склиф" => SpawnPoints.MoscowHospital,
                    _ => SpawnPoints.MoscowRedSquare
                };
                player.Position = targetPos;
                _notifyTeleport?.Invoke(player, player.Position);
                SendSystem(player, $"Телепортирован в локацию: {targetPreset.ToUpperInvariant()} (Москва)");
                break;

            case "setweather":
                if (args.Length == 0 || !uint.TryParse(args[0], out var wId))
                {
                    SendSystem(player, "Использование: /setweather <0-14>");
                    return;
                }
                Alt.EmitAllClients("flovmp:env:weather", wId);
                SendSystem(player, $"Погода сервера установлена на ID {wId}.");
                break;

            case "settime":
                if (args.Length == 0 || !int.TryParse(args[0], out var h))
                {
                    SendSystem(player, "Использование: /settime <часы 0-23> [минуты]");
                    return;
                }
                var m = args.Length > 1 && int.TryParse(args[1], out var parsedM) ? parsedM : 0;
                Alt.EmitAllClients("flovmp:env:time", h, m);
                SendSystem(player, $"Время сервера установлено на {h:D2}:{m:D2}.");
                break;

            case "setskin":
                if (args.Length < 2) { SendSystem(player, "Использование: /setskin <ID/ник> <модель_скина>"); return; }
                var skinTarget = FindPlayer(args[0]);
                if (skinTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var skinModel = args[1];
                try
                {
                    skinTarget.Model = Alt.Hash(skinModel);
                    SendSystem(player, $"Скин игрока {skinTarget.Name} изменён на '{skinModel}'.");
                    SendSystem(skinTarget, $"Администратор {acc.Username} установил вам модель персонажа '{skinModel}'.");
                    GameLog.Admin("setskin", LogActor.Admin(acc.Id, acc.Username), skinTarget.Name, ("model", skinModel));
                }
                catch (Exception ex)
                {
                    SendSystem(player, $"Ошибка смены скина: {ex.Message}");
                }
                break;

            // ── Уровень 6: Куратор / Зам. ГА ──────────
            case "hardban":
                if (args.Length < 1)
                {
                    SendSystem(player, "Использование: /hardban <ID/ник> [причина]");
                    return;
                }
                var hardTarget = FindPlayer(args[0]);
                if (hardTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var hardAcc = _accountOf(hardTarget);
                if (hardAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                var hardReason = args.Length > 1 ? string.Join(' ', args.Skip(1)) : "Тотальная перманентная блокировка вредителя";
                hardAcc.IsBanned = true;
                hardAcc.BanReason = $"[HARDBAN: Account+IP+SC+HWID+MAC] {hardReason}";
                hardAcc.BanUntilUtc = DateTime.UtcNow.AddYears(10).ToString("O");
                _saveAccount?.Invoke(hardAcc);
                Broadcast($"[HARDBAN] Вредитель {hardTarget.Name} получил ТОТАЛЬНУЮ блокировку (Account+IP+SC+HWID). Причина: {hardReason}");
                GameLog.Punishment("hardban", LogActor.Admin(acc.Id, acc.Username), hardTarget.Name, hardReason, 315360000);
                hardTarget.Kick($"ТОТАЛЬНЫЙ БАН (HardBan: HWID+SC+IP+Acc): {hardReason}");
                break;

            case "givemoney":
                if (args.Length < 2 || !long.TryParse(args[1], out var gAmt) || gAmt <= 0)
                {
                    SendSystem(player, "Использование: /givemoney <ID/ник> <сумма>");
                    return;
                }
                var gTarget = FindPlayer(args[0]);
                if (gTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var gAcc = _accountOf(gTarget);
                if (gAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                if (_economy != null)
                {
                    _economy.TryGiveCash(gAcc, gAmt, $"Выдано админом {acc.Username}", out _);
                    _saveAccount?.Invoke(gAcc);
                    SendSystem(player, $"Выдано {gAmt:N0} руб. игроку {gTarget.Name}.");
                    SendSystem(gTarget, $"Администратор {acc.Username} выдал вам {gAmt:N0} руб.");
                    GameLog.Admin("givemoney", LogActor.Admin(acc.Id, acc.Username), gTarget.Name, ("amount", gAmt));
                }
                break;

            case "takemoney":
                if (args.Length < 2 || !long.TryParse(args[1], out var tAmt) || tAmt <= 0)
                {
                    SendSystem(player, "Использование: /takemoney <ID/ник> <сумма>");
                    return;
                }
                var takeTarget = FindPlayer(args[0]);
                if (takeTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var takeAcc = _accountOf(takeTarget);
                if (takeAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                if (_economy != null)
                {
                    _economy.TryTakeCash(takeAcc, tAmt, $"Изъято админом {acc.Username}", out _);
                    _saveAccount?.Invoke(takeAcc);
                    SendSystem(player, $"Изъято {tAmt:N0} руб. у игрока {takeTarget.Name}.");
                    SendSystem(takeTarget, $"Администратор {acc.Username} изъял у вас {tAmt:N0} руб.");
                    GameLog.Admin("takemoney", LogActor.Admin(acc.Id, acc.Username), takeTarget.Name, ("amount", tAmt));
                }
                break;

            case "setdim":
                if (args.Length < 2 || !int.TryParse(args[1], out var newDim))
                {
                    SendSystem(player, "Использование: /setdim <ID/ник> <dimension>");
                    return;
                }
                var dimTarget = FindPlayer(args[0]);
                if (dimTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                dimTarget.Dimension = newDim;
                SendSystem(player, $"Виртуальный мир игрока {dimTarget.Name} изменён на {newDim}.");
                SendSystem(dimTarget, $"Администратор {acc.Username} переместил вас в виртуальный мир #{newDim}.");
                GameLog.Admin("setdim", LogActor.Admin(acc.Id, acc.Username), dimTarget.Name, ("dim", newDim));
                break;

            case "giveitem":
                if (args.Length < 3 || !int.TryParse(args[2], out var itemQty) || itemQty <= 0)
                {
                    SendSystem(player, "Использование: /giveitem <ID/ник> <item_id> <кол-во>");
                    return;
                }
                var itemTarget = FindPlayer(args[0]);
                if (itemTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var itemId = args[1].ToLowerInvariant();
                if (_inventory != null)
                {
                    if (_inventory.TryGiveItem(itemTarget, itemId, itemQty))
                    {
                        SendSystem(player, $"Вы выдали {itemQty} шт. '{itemId}' игроку {itemTarget.Name}.");
                        SendSystem(itemTarget, $"Администратор {acc.Username} выдал вам в инвентарь: {itemQty}x {itemId}.");
                        GameLog.Admin("giveitem", LogActor.Admin(acc.Id, acc.Username), itemTarget.Name, ("item", itemId), ("qty", itemQty));
                    }
                    else
                    {
                        SendSystem(player, $"Не удалось выдать предмет. Проверьте ID ('{itemId}') или свободное место в инвентаре.");
                    }
                }
                else
                {
                    SendSystem(player, "Система инвентаря временно недоступна.");
                }
                break;

            // ── Уровень 7: Главный Администратор ───────
            case "makeadmin":
                if (args.Length < 2 || !int.TryParse(args[1], out var newLvl) || newLvl is < 0 or > 6)
                {
                    SendSystem(player, "Использование: /makeadmin <ID/ник> <уровень 0-6>");
                    return;
                }
                var promoteTarget = FindPlayer(args[0]);
                if (promoteTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var promoteAcc = _accountOf(promoteTarget);
                if (promoteAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                promoteAcc.AdminLevel = newLvl;
                _saveAccount?.Invoke(promoteAcc);
                _setAdminExempt?.Invoke(promoteAcc.Id, newLvl > 0);
                SendSystem(promoteTarget, $"[Администрация] Ваш статус изменён на: {AdminTitles.GetTitle(newLvl)} ({newLvl} lvl) администратором {acc.Username}.");
                SendSystem(player, $"Вы назначили {promoteTarget.Name} на должность: {AdminTitles.GetTitle(newLvl)} ({newLvl} lvl).");
                GameLog.Admin("promote", LogActor.Admin(acc.Id, acc.Username), promoteTarget.Name, ("newLevel", newLvl));
                break;

            case "clearadmin":
                if (args.Length == 0) { SendSystem(player, "Использование: /clearadmin <ник>"); return; }
                var clearName = args[0];
                var clearAcc = _findAccountByName?.Invoke(clearName);
                if (clearAcc == null)
                {
                    var onlineTarget = FindPlayer(clearName);
                    clearAcc = onlineTarget != null ? _accountOf(onlineTarget) : null;
                }
                if (clearAcc == null)
                {
                    SendSystem(player, $"Аккаунт '{clearName}' не найден.");
                    return;
                }
                clearAcc.AdminLevel = 0;
                _saveAccount?.Invoke(clearAcc);
                _setAdminExempt?.Invoke(clearAcc.Id, false);
                BroadcastAdmin($"[А-ЧАТ] Главный Администратор {acc.Username} снял права администратора с {clearAcc.Username}.");
                SendSystem(player, $"Администраторские права успешно сняты с {clearAcc.Username}.");
                GameLog.Admin("clearadmin", LogActor.Admin(acc.Id, acc.Username), clearAcc.Username);
                break;

            // ── Уровень 8: Руководитель проекта ───────
            case "setadminlevel":
                if (args.Length < 2 || !int.TryParse(args[1], out var fullLvl) || fullLvl is < 0 or > 8)
                {
                    SendSystem(player, "Использование: /setadminlevel <ID/ник> <уровень 0-8>");
                    return;
                }
                var fullTarget = FindPlayer(args[0]);
                if (fullTarget == null) { SendSystem(player, "Игрок не найден."); return; }
                var fullAcc = _accountOf(fullTarget);
                if (fullAcc == null) { SendSystem(player, "Аккаунт игрока не найден."); return; }
                fullAcc.AdminLevel = fullLvl;
                _saveAccount?.Invoke(fullAcc);
                _setAdminExempt?.Invoke(fullAcc.Id, fullLvl > 0);
                SendSystem(fullTarget, $"[Руководство] Ваш статус изменён на: {AdminTitles.GetTitle(fullLvl)} ({fullLvl} lvl).");
                SendSystem(player, $"Успешно установлен ранг {AdminTitles.GetTitle(fullLvl)} ({fullLvl} lvl) для {fullTarget.Name}.");
                GameLog.Admin("setadmin", LogActor.Admin(acc.Id, acc.Username), fullTarget.Name, ("level", fullLvl));
                break;

            case "srvrestart":
                int restartSec = args.Length > 0 && int.TryParse(args[0], out var parsedSec) ? Math.Max(3, parsedSec) : 10;
                Broadcast($"[ВНИМАНИЕ] Перезапуск сервера через {restartSec} сек. по команде администратора {acc.Username}. Сохранение...");
                GameLog.Admin("srvrestart", LogActor.Admin(acc.Id, acc.Username), "SERVER", ("seconds", restartSec));
                _restartServer?.Invoke(restartSec);
                break;
        }
    }

    private IPlayer? FindPlayer(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        if (uint.TryParse(query, out var id))
        {
            var byId = Alt.GetAllPlayers().FirstOrDefault(p => p.Exists && p.Id == id);
            if (byId != null) return byId;
        }
        return Alt.GetAllPlayers().FirstOrDefault(p =>
            p.Exists && _accountOf(p)?.Username.Equals(query, StringComparison.OrdinalIgnoreCase) == true);
    }

    private bool IsRateLimited(uint id)
    {
        var now = DateTime.UtcNow;
        var e = _rate.AddOrUpdate(id,
            _ => (1, now),
            (_, cur) => now - cur.first > Window ? (1, now) : (cur.count + 1, cur.first));
        return e.count > MaxPerWindow;
    }

    private static IVehicle? FindNearestVehicle(Position pos, int dimension, float maxDistance = 5.0f)
    {
        IVehicle? best = null;
        float bestDist = maxDistance;
        foreach (var v in Alt.GetAllVehicles())
        {
            if (!v.Exists || v.Dimension != dimension) continue;
            var dist = v.Position.Distance(pos);
            if (dist <= bestDist)
            {
                bestDist = dist;
                best = v;
            }
        }
        return best;
    }

    private void BroadcastNearbyMe(IPlayer player, string action)
    {
        var pos = player.Position;
        var dim = player.Dimension;
        var acc = _accountOf(player);
        var username = acc?.Username ?? player.Name;
        foreach (var p in Alt.GetAllPlayers())
        {
            if (p.Exists && p.Dimension == dim && p.Position.Distance(pos) <= 20.0f)
            {
                p.Emit("flovmp:chat:msg", "me", username, action);
            }
        }
    }
}

