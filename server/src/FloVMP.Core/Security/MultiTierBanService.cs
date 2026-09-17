using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FloVMP.Core.Security;

[Flags]
public enum BanFlags
{
    None = 0,
    Account = 1 << 0,       // Блокировка конкретного аккаунта
    Ip = 1 << 1,            // Блокировка текущего IP
    SocialClub = 1 << 2,    // Блокировка лицензии Rockstar Social Club
    Hwid = 1 << 3,          // Блокировка хэша материнской платы/BIOS/процессора
    Mac = 1 << 4,           // Блокировка MAC-адреса
    Subnet = 1 << 5         // Блокировка подсети IP /24
}

public enum BanTier
{
    StandardBan = 1,    // /ban — только аккаунт
    IpBan = 2,          // /banip — аккаунт + IP
    SocialClubBan = 3,  // /bansc — аккаунт + Social Club ID
    HardwareBan = 4,    // /hwidban /macban — аккаунт + HWID + MAC
    HardBan = 5         // /hardban — ультра-бан: Account + IP + SocialClub + HWID + MAC + Subnet
}

public sealed record BanRecord(
    string Id,
    int AccountId,
    string Username,
    string? Ip,
    string? SocialClubId,
    string? HwidHash,
    string? MacAddress,
    BanFlags Flags,
    string AdminUsername,
    string Reason,
    DateTime BannedAtUtc,
    DateTime? ExpiresAtUtc,
    bool IsActive = true);

public sealed record BanCheckResult(
    bool IsBlocked,
    string? Reason,
    BanFlags? MatchedFlag,
    string? AdminName,
    DateTime? ExpiresAtUtc,
    bool ShouldAlertAdmins);

/// <summary>
/// Сервис многоуровневых блокировок: аккаунт, IP, Social Club, HWID, MAC.
/// Поддерживает гранулярные уровни от простого /ban до тотального /hardban с учётом FloV:ID политики сервера.
/// </summary>
public class MultiTierBanService
{
    private readonly ConcurrentDictionary<string, BanRecord> _bans = new();

    // Хранилище блокировок. Необязательное: без него сервис работает как
    // раньше (только в памяти), и это допустимо для тестов. На живом сервере
    // отсутствие хранилища означало бы, что рестарт снимает все баны.
    private readonly IBanStore? _store;

    // Момент последней синхронизации — чтобы спрашивать у базы только новое.
    private DateTime? _lastRefreshUtc;

    public MultiTierBanService() { }

    public MultiTierBanService(IBanStore store)
    {
        _store = store;
        foreach (var record in store.LoadAll())
            if (!string.IsNullOrWhiteSpace(record.Id)) _bans[record.Id] = record;
    }

    /// <summary>Сколько блокировок держит сервис (включая снятые).</summary>
    public int Count => _bans.Count;

    /// <summary>
    /// Дозагрузить блокировки, появившиеся в общем хранилище. Нужна при
    /// нескольких инстансах: бан, выданный на соседнем сервере, иначе дошёл бы
    /// сюда только после перезапуска.
    /// </summary>
    public int RefreshFromStore()
    {
        if (_store is null) return 0;

        IReadOnlyList<BanRecord> batch;
        var startedAt = DateTime.UtcNow;

        if (_store is IIncrementalBanStore incremental)
        {
            // Берём только изменившееся: перечитывать всю таблицу банов каждые
            // полминуты на каждом инстансе — постоянная нагрузка на базу,
            // растущая вместе с числом банов.
            //
            // Окно назад на минуту намеренно: часы инстансов и базы расходятся,
            // и запрос «строго после прошлой синхронизации» может пропустить
            // бан, записанный в ту же секунду. Лучше перечитать десяток записей
            // повторно, чем не увидеть свежий бан.
            var since = _lastRefreshUtc?.AddMinutes(-1);
            batch = incremental.LoadChangedSince(since);
        }
        else
        {
            batch = _store.LoadAll();
        }

        var changed = 0;
        foreach (var record in batch)
        {
            if (string.IsNullOrWhiteSpace(record.Id)) continue;
            if (_bans.TryGetValue(record.Id, out var known) && known == record) continue;
            _bans[record.Id] = record;
            changed++;
        }

        _lastRefreshUtc = startedAt;
        return changed;
    }

    private void Persist(BanRecord record)
    {
        if (_store is null) return;
        try { _store.Upsert(record); }
        catch (Exception ex)
        {
            // Бан уже действует в памяти этого инстанса — падать из-за
            // недоступной БД в момент выдачи бана нельзя. Но и промолчать
            // нельзя: администратор должен знать, что бан не переживёт рестарт.
            CoreConsole.Warning(
                $"[FloV:MP] ВНИМАНИЕ: блокировка {record.Id} не сохранена ({ex.Message}). " +
                "Она действует до перезапуска сервера.");
        }
    }

    public static BanFlags TierToFlags(BanTier tier) => tier switch
    {
        BanTier.StandardBan => BanFlags.Account,
        BanTier.IpBan => BanFlags.Account | BanFlags.Ip,
        BanTier.SocialClubBan => BanFlags.Account | BanFlags.SocialClub,
        BanTier.HardwareBan => BanFlags.Account | BanFlags.Hwid | BanFlags.Mac,
        BanTier.HardBan => BanFlags.Account | BanFlags.Ip | BanFlags.SocialClub | BanFlags.Hwid | BanFlags.Mac | BanFlags.Subnet,
        _ => BanFlags.Account
    };

    public BanRecord CreateBan(
        int accountId,
        string username,
        string? ip,
        string? socialClubId,
        string? hwidHash,
        string? macAddress,
        BanTier tier,
        string adminUsername,
        string reason,
        int durationDays)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var flags = TierToFlags(tier);
        var expires = durationDays > 0 ? DateTime.UtcNow.AddDays(durationDays) : (DateTime?)null;

        var record = new BanRecord(
            id,
            accountId,
            username,
            ip,
            socialClubId,
            hwidHash,
            macAddress,
            flags,
            adminUsername,
            reason,
            DateTime.UtcNow,
            expires,
            true);

        _bans[id] = record;
        Persist(record);
        return record;
    }

    public BanRecord CreateCustomBan(
        int accountId,
        string username,
        string? ip,
        string? socialClubId,
        string? hwidHash,
        string? macAddress,
        BanFlags flags,
        string adminUsername,
        string reason,
        DateTime? expiresAtUtc)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var record = new BanRecord(
            id,
            accountId,
            username,
            ip,
            socialClubId,
            hwidHash,
            macAddress,
            flags,
            adminUsername,
            reason,
            DateTime.UtcNow,
            expiresAtUtc,
            true);

        _bans[id] = record;
        Persist(record);
        return record;
    }

    /// <summary>
    /// Комплексная проверка входящего подключения игрока с учётом политики сервера
    /// </summary>
    public BanCheckResult CheckConnection(
        int accountId,
        string? ip,
        string? socialClubId,
        string? hwidHash,
        string? macAddress,
        HwidPolicyMode policy)
    {
        var now = DateTime.UtcNow;
        var activeBans = _bans.Values
            .Where(b => b.IsActive && (!b.ExpiresAtUtc.HasValue || b.ExpiresAtUtc.Value > now))
            .ToList();

        foreach (var ban in activeBans)
        {
            // 1. Проверка прямого бана аккаунта (действует ВСЕГДА, при любой политике)
            if (ban.Flags.HasFlag(BanFlags.Account) && ban.AccountId == accountId)
            {
                return new BanCheckResult(true, ban.Reason, BanFlags.Account, ban.AdminUsername, ban.ExpiresAtUtc, false);
            }

            // Если политика отключена — проверка аппаратных банов пропускается
            if (policy == HwidPolicyMode.Disabled) continue;

            bool hwidMatch = ban.Flags.HasFlag(BanFlags.Hwid) && !string.IsNullOrEmpty(hwidHash) && string.Equals(ban.HwidHash, hwidHash, StringComparison.OrdinalIgnoreCase);
            bool macMatch = ban.Flags.HasFlag(BanFlags.Mac) && !string.IsNullOrEmpty(macAddress) && string.Equals(ban.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase);
            bool scMatch = ban.Flags.HasFlag(BanFlags.SocialClub) && !string.IsNullOrEmpty(socialClubId) && string.Equals(ban.SocialClubId, socialClubId, StringComparison.OrdinalIgnoreCase);
            bool ipMatch = ban.Flags.HasFlag(BanFlags.Ip) && !string.IsNullOrEmpty(ip) && string.Equals(ban.Ip, ip, StringComparison.OrdinalIgnoreCase);
            // BUGFIX: флаг Subnet ставился в /hardban, но НЕ проверялся — забаненный
            // заходил с того же /24 с другим IP. Теперь матчим подсеть /24.
            bool subnetMatch = ban.Flags.HasFlag(BanFlags.Subnet) && SameSubnet24(ban.Ip, ip);

            if (hwidMatch || macMatch || scMatch || ipMatch || subnetMatch)
            {
                var matchedFlag = hwidMatch ? BanFlags.Hwid
                    : macMatch ? BanFlags.Mac
                    : scMatch ? BanFlags.SocialClub
                    : ipMatch ? BanFlags.Ip
                    : BanFlags.Subnet;

                if (policy == HwidPolicyMode.Strict)
                {
                    // В Strict-режиме: немедленный отказ во входе
                    return new BanCheckResult(true, $"Обход блокировки [{matchedFlag}]: {ban.Reason}", matchedFlag, ban.AdminUsername, ban.ExpiresAtUtc, false);
                }
                else if (policy == HwidPolicyMode.Lenient)
                {
                    // В Lenient-режиме (дефицит онлайна/амнистия): вход разрешён, но администрация уведомляется
                    return new BanCheckResult(false, null, matchedFlag, ban.AdminUsername, ban.ExpiresAtUtc, true);
                }
            }
        }

        return new BanCheckResult(false, null, null, null, null, false);
    }

    /// <summary>
    /// Снятие блокировки (по ID бана, нику, HWID, Social Club или IP)
    /// </summary>
    public int Unban(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return 0;
        int unbanned = 0;

        foreach (var (key, ban) in _bans)
        {
            if (string.Equals(ban.Id, query, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ban.Username, query, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ban.HwidHash, query, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ban.SocialClubId, query, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ban.Ip, query, StringComparison.OrdinalIgnoreCase))
            {
                var updated = ban with { IsActive = false };
                if (_bans.TryUpdate(key, updated, ban))
                {
                    Persist(updated);
                    unbanned++;
                }
            }
        }

        return unbanned;
    }

    public IReadOnlyList<BanRecord> GetAllBans() => _bans.Values.ToList();

    /// <summary>
    /// true, если два IPv4-адреса в одной подсети /24 (совпадают первые 3 октета).
    /// Пустой/не-IPv4 вход → false (не блокируем по мусору).
    /// </summary>
    public static bool SameSubnet24(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        var pa = a.Trim().Split('.');
        var pb = b.Trim().Split('.');
        if (pa.Length != 4 || pb.Length != 4) return false;
        for (int i = 0; i < 3; i++)
        {
            if (!byte.TryParse(pa[i], out var oa) || !byte.TryParse(pb[i], out var ob)) return false;
            if (oa != ob) return false;
        }
        return true;
    }
}
