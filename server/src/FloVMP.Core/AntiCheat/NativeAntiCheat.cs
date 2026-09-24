using FloVMP.Core.Native;

namespace FloVMP.Core.AntiCheat;

/// <summary>Категории подозрений: у каждой свой вес (anticheat.weight_* в client.cfg).</summary>
public enum SuspicionKind { Movement, Hit, Weapon, Ammo, TimeScale, Vehicle, Model, Custom }

/// <summary>
/// Вторая линия античита для игроков 3889 (пункт 7 roadmap): белые и чёрные
/// списки оружия и моделей, учёт выданного оружия и патронов, ускорение
/// времени — и всё это, как и прежние проверки движения, урона и машин, в
/// один <see cref="SuspicionLedger"/> с весами вместо мгновенных решений.
///
/// Платформа здесь — макет по умолчанию: каждый вес, порог и список владелец
/// меняет настройкой или из кода, геймод добавляет свои подозрения
/// (<see cref="Report"/> с <see cref="SuspicionKind.Custom"/>) и сам решает,
/// что делать на порогах (событие <see cref="ThresholdCrossed"/>).
/// Только главный поток.
/// </summary>
public sealed class NativeAntiCheat
{
    private static readonly uint Parachute = GameHash.Joaat("gadget_parachute");

    public SuspicionLedger Ledger { get; } = new();
    public WeaponLedger Weapons { get; } = new();
    public ClockDriftDetector Clock { get; } = new();

    public Dictionary<SuspicionKind, float> Weights { get; } = new()
    {
        [SuspicionKind.Movement] = 10, [SuspicionKind.Hit] = 5, [SuspicionKind.Weapon] = 25,
        [SuspicionKind.Ammo] = 10, [SuspicionKind.TimeScale] = 30, [SuspicionKind.Vehicle] = 10,
        [SuspicionKind.Model] = 25, [SuspicionKind.Custom] = 10,
    };

    /// <summary>Оружие, которого не должно быть ни у кого (anticheat.weapon_blacklist).</summary>
    public HashSet<uint> WeaponBlacklist { get; set; } = new();
    /// <summary>Только выданное сервером оружие (anticheat.issued_weapons_only).</summary>
    public bool IssuedWeaponsOnly { get; set; }
    /// <summary>Сверять попадания с выданными патронами (anticheat.ammo_accounting).</summary>
    public bool AmmoAccounting { get; set; } = true;
    /// <summary>Модели персонажа, которые можно носить (пусто — любые; anticheat.ped_whitelist).</summary>
    public HashSet<uint> PedWhitelist { get; set; } = new();
    /// <summary>Машины, которые нельзя взять в реестр из «трафика» (anticheat.vehicle_blacklist).</summary>
    public HashSet<uint> VehicleBlacklist { get; set; } = new();

    /// <summary>Подозрение записано: (игрок, категория, вес, счёт, подробности).</summary>
    public event Action<uint, string, float, float, string>? Suspected;
    /// <summary>Счёт пересёк порог: (игрок, порог, счёт).</summary>
    public event Action<uint, SuspicionLevel, float>? ThresholdCrossed;

    // Что уже отмечено у игрока: STATE приходит 20 раз в секунду, одно и то же
    // нарушение не должно превращаться в 20 подозрений.
    private readonly Dictionary<uint, (uint Weapon, uint Model)> _noted = new();
    private readonly Dictionary<(uint Player, SuspicionKind Kind), long> _cooldown = new();
    // Модели, выданные сервером (spawn.model, /skin, геймод): их носить можно всегда.
    private readonly Dictionary<uint, uint> _issuedModel = new();

    public const long RepeatCooldownMs = 5000;

    /// <summary>
    /// Записать подозрение. weight — только для <see cref="SuspicionKind.Custom"/>
    /// (геймод), у остальных вес из <see cref="Weights"/>.
    /// </summary>
    public float Report(uint playerId, SuspicionKind kind, string details, long nowMs, float? weight = null)
    {
        var w = kind == SuspicionKind.Custom && weight is { } custom ? custom : Weights.GetValueOrDefault(kind, 10f);
        var type = kind.ToString().ToLowerInvariant();
        var (score, crossed) = Ledger.Add(playerId, type, w, details, nowMs);
        Suspected?.Invoke(playerId, type, w, score, details);
        if (crossed != SuspicionLevel.None) ThresholdCrossed?.Invoke(playerId, crossed, score);
        return score;
    }

    /// <summary>То же, но не чаще раза в <see cref="RepeatCooldownMs"/> на категорию.</summary>
    public void ReportLimited(uint playerId, SuspicionKind kind, string details, long nowMs)
    {
        if (_cooldown.TryGetValue((playerId, kind), out var at) && nowMs - at < RepeatCooldownMs) return;
        _cooldown[(playerId, kind)] = nowMs;
        Report(playerId, kind, details, nowMs);
    }

    public void NoteIssuedModel(uint playerId, uint model) => _issuedModel[playerId] = model;

    /// <summary>
    /// Оружие и модель из STATE. Возвращает true, если в руках запрещённое
    /// оружие (в строгом режиме платформа его заберёт). Одно и то же
    /// нарушение отмечается один раз, пока не сменится.
    /// </summary>
    public bool CheckState(uint playerId, in NativePlayerState st, long nowMs)
    {
        var weapon = st.Weapon;
        var model = st.PedModel;
        _noted.TryGetValue(playerId, out var noted);
        var forbiddenWeapon = false;

        if (weapon != 0 && weapon != WeaponLedger.Unarmed && weapon != Parachute)
        {
            string? why = null;
            if (WeaponBlacklist.Contains(weapon)) why = $"запрещённое оружие 0x{weapon:X8}";
            else if (IssuedWeaponsOnly && !Weapons.WasIssued(playerId, weapon)) why = $"оружие 0x{weapon:X8}, которого сервер не выдавал";
            if (why is not null)
            {
                forbiddenWeapon = true;
                if (noted.Weapon != weapon) Report(playerId, SuspicionKind.Weapon, why, nowMs);
            }
        }

        if (model != 0 && PedWhitelist.Count > 0 && !PedWhitelist.Contains(model) &&
            !(_issuedModel.TryGetValue(playerId, out var issued) && issued == model) && noted.Model != model)
            Report(playerId, SuspicionKind.Model, $"модель персонажа 0x{model:X8} не из разрешённых", nowMs);

        _noted[playerId] = (weapon, model);
        return forbiddenWeapon;
    }

    /// <summary>Засчитанное попадание: не больше, чем выдано патронов.</summary>
    public void OnHit(uint playerId, uint weapon, long nowMs)
    {
        if (!AmmoAccounting || Weapons.ConsumeHit(playerId, weapon)) return;
        ReportLimited(playerId, SuspicionKind.Ammo, $"попаданий из 0x{weapon:X8} больше, чем выдано патронов", nowMs);
    }

    /// <summary>PING клиента с его часами: разгон времени (speedhack).</summary>
    public void OnPing(uint playerId, long clientMs, long serverMs)
    {
        if (Clock.Sample(playerId, clientMs, serverMs) is { } ratio)
            Report(playerId, SuspicionKind.TimeScale, $"часы клиента идут в {ratio:0.00} раза быстрее сервера", serverMs);
    }

    public bool VehicleAllowed(uint model) => !VehicleBlacklist.Contains(model);

    public void RemovePlayer(uint playerId)
    {
        Ledger.Remove(playerId);
        Weapons.Remove(playerId);
        Clock.Remove(playerId);
        _noted.Remove(playerId);
        _issuedModel.Remove(playerId);
        foreach (var key in _cooldown.Keys.Where(k => k.Player == playerId).ToList()) _cooldown.Remove(key);
    }
}
