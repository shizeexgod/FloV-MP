using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Native;

namespace FloVMP.Starter;

/// <summary>
/// Нативные клиенты GTA V Legacy 1.0.3889.0 (ASI на ScriptHookV).
///
/// Клиент alt:V 16.4.39 работает только с GTA b3521, а у игроков сейчас
/// Legacy b3889 — поэтому у платформы второй, собственный клиент. Он
/// подключается к TCP-шлюзу (по умолчанию порт игры + 10, 7798) и внутри
/// платформы выглядит обычным IPlayer (<see cref="NativePlayerProxy"/>):
/// те же проверки входа, баны, права, лицензия, чат и команды.
///
/// Потоки: сеть — пул потоков NativeServer; всё, что трогает игровую
/// логику, выполняется здесь, в OnTick главного потока.
/// </summary>
public partial class StarterResource
{
    private NativeServer? _native;
    private NativeVoice? _nativeVoice;
    private readonly Dictionary<uint, IPlayer> _nativePlayers = new();
    private readonly ConcurrentDictionary<uint, byte> _altPlayerIds = new();
    private readonly HashSet<uint> _nativeReady = new();
    private readonly HashSet<string> _reportedUnsupported = new();

    /// <summary>Игрока перевели в другое измерение — у него другой набор объектов и меток.</summary>
    internal void OnNativeDimensionChanged(NativeSession session, int dimension)
    {
        if (_nativeReady.Contains(session.Id)) SendWorldSnapshot(session, dimension);
    }

    // Кого из игроков уже видит нативный клиент и какую версию состояния ему отправили.
    private readonly Dictionary<uint, Dictionary<uint, (long Version, long Tick)>> _nativeVisible = new();
    private readonly FloVMP.Core.Spatial.SpatialHashGrid<uint> _nativeGrid =
        new(FloVMP.Core.Spatial.SpatialHashGrid<uint>.RecommendedCellSize(NativeStreamRadius));
    private readonly List<uint> _nativeCandidates = new();
    private long _nextNativeSyncMs;
    private long _nextAltSnapshotMs;
    private long _altSnapshotVersion;

    /// <summary>Радиус, в котором клиент видит других игроков, м.</summary>
    private const float NativeStreamRadius = 400f;
    private const long NativeSyncIntervalMs = 50;

    // Попадания по другим игрокам — клиент сообщает, сервер проверяет и
    // передаёт урон жертве. Лимит против «убить всех одной строкой».
    private readonly Dictionary<uint, (int Count, int Damage, long WindowStart)> _hitRate = new();
    // Урон одной жертве от одного стрелка: ограничивает «мгновенную смерть»,
    // но не мешает быстрому оружию — у игрока 200 здоровья.
    private readonly Dictionary<(uint Attacker, uint Victim), (int Damage, long WindowStart)> _hitPairRate = new();
    // Миниган делает ~50 выстрелов в секунду, пистолет-пулемёт ~12: прежний
    // лимит 12 отбрасывал честные попадания («нерег» автоматическим оружием).
    private const int MaxHitsPerSecond = 40;
    private const int MaxDamagePerSecond = 1000;      // суммарно по всем жертвам
    private const int MaxDamagePerVictimPerSecond = 300;
    private const float MaxHitDistance = 300f;
    private const float MaxMeleeDistance = 6f;
    private const float MaxRamDistance = 20f;
    private const uint WeaponUnarmed = 0xA2719263;
    private readonly Dictionary<uint, long> _hitWarnedAt = new();

    /// <summary>Прошлое и текущее оружие игрока: HIT выстрела, сделанного
    /// сразу после смены оружия, приходит раньше нового STATE.</summary>
    private readonly Dictionary<uint, (uint Prev, uint Cur, long ChangedAt)> _weaponHistory = new();
    private const int WeaponSwitchGraceMs = 2000;

    /// <summary>Когда подтверждённое сервером попадание дошло до жертвы: убийцу
    /// из сообщения DIED засчитываем только тому, кто и правда стрелял.</summary>
    private readonly Dictionary<(uint Attacker, uint Victim), long> _lastHitAt = new();
    private const int KillCreditWindowMs = 15_000;

    private static readonly int PerfNative = FloVMP.Core.Diagnostics.TickProfiler.Register("native-b3889");

    internal static bool IsNative(IPlayer? player) => player is NativePlayerProxy;

    /// <summary>Все игроки: клиенты alt:V и нативные b3889.</summary>
    private List<IPlayer> AllPlayers()
    {
        var alt = Alt.GetAllPlayers();
        var list = new List<IPlayer>(alt.Count + _nativePlayers.Count);
        list.AddRange(alt);
        list.AddRange(_nativePlayers.Values);
        return list;
    }

    private IPlayer? PlayerById(uint id) =>
        _nativePlayers.TryGetValue(id, out var native) ? native : Alt.GetPlayerById(id);

    /// <summary>Alt.EmitAllClients + нативные клиенты.</summary>
    private void EmitAllClients(string eventName, params object[] args)
    {
        Alt.EmitAllClients(eventName, args);
        foreach (var p in _nativePlayers.Values) p.Emit(eventName, args);
    }

    private static bool InAnyVehicle(IPlayer player) =>
        player is NativePlayerProxy np ? np.Session.HasState && np.State.InVehicle : player.Vehicle is not null;

    internal void ReportUnsupportedNativeMember(string member)
    {
        lock (_reportedUnsupported)
            if (!_reportedUnsupported.Add(member)) return;
        Alt.LogWarning($"[FloV:MP b3889] IPlayer.{member} у нативного клиента не поддерживается — вызов пропущен.");
    }

    private void StartNativeGateway()
    {
        var mode = (Environment.GetEnvironmentVariable("FLOVMP_NATIVE") ?? "on").Trim().ToLowerInvariant();
        if (mode is "off" or "0" or "false")
        {
            Alt.Log("[FloV:MP b3889] Шлюз нативных клиентов выключен (FLOVMP_NATIVE=off).");
            return;
        }

        // Порт шлюза — порт игры + 10 (7788 → 7798): так его находят клиент и
        // коннектор по одному адресу сервера. FLOVMP_NATIVE_PORT — явная замена.
        var port = int.TryParse(ReadServerTomlValue("port"), out var gamePort) && gamePort is > 0 and < 65526
            ? gamePort + 10
            : NativeProtocol.DefaultPort;
        if (int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_NATIVE_PORT"), out var p) && p is > 0 and < 65536)
            port = p;

        try
        {
            _native = new NativeServer(IPAddress.Any, port, msg => Alt.Log("[FloV:MP b3889] " + msg),
                id => _altPlayerIds.ContainsKey(id));
            _native.ServerName = Environment.GetEnvironmentVariable("FLOVMP_SERVER_NAME") ??
                                 FloVMP.Core.Chat.ChatSanitizer.CleanPlayerText(ReadServerTomlValue("name")) ?? "FloV:MP";
            _native.Start();
            StartNativeVoice(port);
            Alt.Log($"[FloV:MP b3889] Шлюз клиентов GTA V Legacy {NativeProtocol.GameVersion} слушает TCP {port}. " +
                    "Для игроков из интернета откройте этот порт.");
        }
        catch (Exception ex)
        {
            _native = null;
            Alt.LogError($"[FloV:MP b3889] Шлюз НЕ запущен (TCP {port}): {ex.Message}. " +
                         "Порт занят другим процессом? Задайте FLOVMP_NATIVE_PORT.");
        }
    }

    /// <summary>Значение верхнего уровня из server.toml (рабочая папка сервера): name, port.</summary>
    private static string? ReadServerTomlValue(string key)
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "server.toml");
            if (!System.IO.File.Exists(path)) return null;
            foreach (var raw in System.IO.File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.StartsWith('[')) break; // только верхний уровень
                var m = System.Text.RegularExpressions.Regex.Match(line,
                    "^" + key + "\\s*=\\s*(?:[\"'](?<v>.+?)[\"']|(?<v>[0-9]+))");
                if (m.Success) return m.Groups["v"].Value;
            }
        }
        catch (Exception) { }
        return null;
    }

    /// <summary>Голос клиентов b3889 — UDP на том же порту, что и шлюз.</summary>
    private void StartNativeVoice(int port)
    {
        var mode = (Environment.GetEnvironmentVariable("FLOVMP_NATIVE_VOICE") ?? "on").Trim().ToLowerInvariant();
        if (mode is "off" or "0" or "false")
        {
            Alt.Log("[FloV:MP b3889] Голосовой чат клиентов b3889 выключен (FLOVMP_NATIVE_VOICE=off).");
            return;
        }
        try
        {
            _nativeVoice = new NativeVoice(IPAddress.Any, port, () => _native?.Sessions ?? Array.Empty<NativeSession>(),
                msg => Alt.LogWarning("[FloV:MP b3889] " + msg));
            if (float.TryParse(Environment.GetEnvironmentVariable("FLOVMP_VOICE_RADIUS"),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var radius)
                && radius is >= 3f and <= 500f)
                _nativeVoice.Radius = radius;
            _nativeVoice.Start();
            Alt.Log($"[FloV:MP b3889] Голосовой чат: UDP {port}, радиус {_nativeVoice.Radius:0} м. Откройте этот UDP-порт вместе с TCP.");
        }
        catch (Exception ex)
        {
            _nativeVoice = null;
            Alt.LogError($"[FloV:MP b3889] Голосовой чат НЕ запущен (UDP {port}): {ex.Message}.");
        }
    }

    private void ToggleNativeVoiceMute(IPlayer admin, IPlayer target, int? minutes)
    {
        var session = ((NativePlayerProxy)(object)target).Session;
        var sc = target.SocialClubId.ToString();
        if (session.VoiceMuted)
        {
            session.VoiceMuted = false;
            _voiceMutes?.Unmute(sc);
            SendChatMessage(admin, $"{{34d399}}Голос игрока {target.Name} восстановлен.");
            SendChatMessage(target, "{34d399}[FloV:MP] Ваш голос снова слышен.");
            Alt.Log($"[FloV:MP] [Voice] {admin.Name} снял голосовой мут с {target.Name}");
            return;
        }
        session.VoiceMuted = true;
        _voiceMutes?.Mute(sc, minutes is null ? null : DateTime.UtcNow.AddMinutes(minutes.Value));
        var howLong = minutes is null ? "до снятия" : $"на {minutes} мин.";
        SendChatMessage(admin, $"{{fde047}}Голос игрока {target.Name} заглушён ({howLong}).");
        SendChatMessage(target, $"{{f59e0b}}[FloV:MP] Ваш голос заглушён администрацией ({howLong}).");
        Alt.Log($"[FloV:MP] [Voice] {admin.Name} заглушил голос {target.Name} ({howLong})");
    }

    private void StopNativeGateway()
    {
        _nativeVoice?.Dispose();
        _nativeVoice = null;
        _native?.Dispose();
        _native = null;
    }

    private void TickNative(long nowMs)
    {
        if (_native is null) return;
        using var _perf = FloVMP.Core.Diagnostics.TickProfiler.Measure(PerfNative);

        // Не больше 2000 событий за тик: поток сообщений не должен вешать сервер.
        for (var i = 0; i < 2000 && _native.Events.TryDequeue(out var ev); i++)
        {
            try
            {
                switch (ev)
                {
                    case NativeJoined j: OnNativeJoined(j.Session); break;
                    case NativeMessage m: OnNativeMessage(m.Session, m.Parts); break;
                    case NativeLeft l: OnNativeLeft(l.Session, l.Reason); break;
                }
            }
            catch (Exception ex)
            {
                Alt.LogError($"[FloV:MP b3889] Ошибка обработки {ev.GetType().Name} от [{ev.Session.Id}] {ev.Session.Name}: {ex}");
            }
        }

        // Снимок мира уходит порциями: у карты бывают десятки тысяч объектов.
        PumpWorldSnapshots();

        if (nowMs >= _nextNativeSyncMs)
        {
            _nextNativeSyncMs = nowMs + NativeSyncIntervalMs;
            SyncNativeWorld(nowMs);
        }
    }

    private void OnNativeJoined(NativeSession session)
    {
        var proxy = DispatchProxy.Create<IPlayer, NativePlayerProxy>();
        ((NativePlayerProxy)(object)proxy).Init(session, this);
        _nativePlayers[session.Id] = proxy;

        // Бан, лицензия и слоты — до WELCOME: иначе клиент успевает войти в
        // режим сервера (остановить сюжет игры) и только потом узнаёт об отказе.
        string? refusal = null;
        if (RejectIfBanned(proxy)) refusal = "бан";
        else if (!_license.IsLicensed) refusal = "[Лицензия] " + _license.Message;
        else if (AllPlayers().Count > _license.PlayerLimit) refusal = $"Сервер заполнен ({_license.PlayerLimit} игроков).";
        else refusal = NameConflict(session);
        if (refusal is not null)
        {
            if (refusal != "бан") proxy.Kick(refusal);
            Alt.Log($"[FloV:MP b3889] вход {session.Name} ({session.Ip}) отклонён: {refusal}");
            _nativePlayers.Remove(session.Id);
            return;
        }

        // Токен голоса — в WELCOME: UDP-пакеты без него сервер не принимает.
        var voiceOn = _nativeVoice is not null && _settings.Bool("voice.enabled");
        var voiceToken = voiceOn ? _nativeVoice!.Register(session) : "";
        session.VoiceMuted = _voiceMutes?.IsMuted(session.Identity.ToString(), DateTime.UtcNow) ?? false;
        session.Send("WELCOME", session.Id, session.Name, session.Identity.ToString(), _native!.ServerName,
            voiceToken, voiceOn ? _nativeVoice!.Port : 0, _settings.Float("voice.radius"));
        // Настройки владельца (client.cfg) — до спавна: HUD, мир, ники, клавиши.
        SendClientSettings(session);
        Alt.Log($"[FloV:MP b3889] Клиент GTA Legacy {NativeProtocol.GameVersion}: {session.Name} ({session.Ip}), " +
                $"ID игрока {session.Identity} (для setadmin sc:{session.Identity}), клиент {session.ClientVersion}.");

        // Тот же вход, что у клиента alt:V: ник, бан, лицензия, лимит, спавн, права.
        OnPlayerConnect(proxy, "native-b3889");
    }

    private void OnNativeLeft(NativeSession session, string reason)
    {
        if (!_nativePlayers.TryGetValue(session.Id, out var proxy) ||
            !ReferenceEquals(((NativePlayerProxy)(object)proxy).Session, session)) return;
        // Ресурсу нужно узнать об уходе игрока 3889: сохранить его данные,
        // снять таймеры, закрыть сделки. Без этого события геймод про выход
        // просто не знал.
        if (_nativeReady.Contains(session.Id))
            Alt.Emit("flovmp:native:left", (int)session.Id, session.Name, reason ?? "");
        _nativePlayers.Remove(session.Id);
        _nativeVoice?.Unregister(session);
        _nativeReady.Remove(session.Id);
        _nativeVisible.Remove(session.Id);
        _hitRate.Remove(session.Id);
        _hitWarnedAt.Remove(session.Id);
        _weaponHistory.Remove(session.Id);
        _nativeVehicleOwners.Remove(session.Id);
        foreach (var key in _hitPairRate.Keys.Where(k => k.Attacker == session.Id || k.Victim == session.Id).ToList())
            _hitPairRate.Remove(key);
        foreach (var key in _lastHitAt.Keys.Where(k => k.Attacker == session.Id || k.Victim == session.Id).ToList())
            _lastHitAt.Remove(key);
        ForgetNativeUi(session.Id);
        foreach (var seen in _nativeVisible.Values) seen.Remove(session.Id);
        foreach (var other in _nativePlayers.Values)
            ((NativePlayerProxy)(object)other).Session.Send("PDEL", session.Id);
        OnPlayerDisconnect(proxy, reason);
    }

    /// <summary>
    /// Два игрока с одним ником — это и путаница в админских командах по нику,
    /// и подмена: можно зайти под ником администратора. Свой же обрыв связи
    /// таким отказом не наказываем: если ник занят тем же клиентом (тот же
    /// ключ), старую сессию закрываем и пускаем новую.
    /// </summary>
    private string? NameConflict(NativeSession session)
    {
        foreach (var (id, other) in _nativePlayers)
        {
            if (id == session.Id) continue;
            var np = (NativePlayerProxy)(object)other;
            if (!string.Equals(np.Session.Name, session.Name, StringComparison.OrdinalIgnoreCase)) continue;
            if (np.Session.Identity == session.Identity)
            {
                np.Session.Close("вы зашли на сервер заново");
                continue;
            }
            return "Ник уже занят игроком на сервере — выберите другой.";
        }
        foreach (var alt in Alt.GetAllPlayers())
            if (alt.Exists && string.Equals(alt.Name, session.Name, StringComparison.OrdinalIgnoreCase))
                return "Ник уже занят игроком на сервере — выберите другой.";
        return null;
    }

    private void OnNativeMessage(NativeSession session, string[] p)
    {
        if (!_nativePlayers.TryGetValue(session.Id, out var player)) return;
        var np = (NativePlayerProxy)(object)player;
        if (!ReferenceEquals(np.Session, session)) return; // хвост сообщений ушедшей сессии
        switch (p[0])
        {
            case "READY":
                if (_nativeReady.Add(session.Id))
                {
                    OnClientReady(player);
                    SendWorldSnapshot(session, np.DimensionValue);
                }
                break;
            case "CHAT":
                if (p.Length > 1) OnChatMessage(player, p[1]);
                break;
            case "DIED":
                if (np.DeadReported) break;
                np.DeadReported = true;
                var killerId = NativeProtocol.UIntOr(p, 1, 0);
                // Убийцу называет клиент жертвы — верим только если этот игрок
                // действительно попал по ней недавно (иначе можно «дарить» убийства).
                if (killerId != 0 && (!_lastHitAt.TryGetValue((killerId, session.Id), out var hitAt) ||
                                      _clock.ElapsedMilliseconds - hitAt > KillCreditWindowMs))
                    killerId = 0;
                var killer = killerId != 0 && killerId != session.Id ? PlayerById(killerId) : null;
                if (killer is not null)
                    Alt.Log($"[FloV:MP] {session.Name} убит игроком [{killer.Id}] {killer.Name}.");
                OnPlayerDead(player, null!, NativeProtocol.UIntOr(p, 2, 0));
                break;
            case "HIT":
                OnNativeHit(session, np, p);
                break;
            case "TPM":
                if (NativeProtocol.TryFloat(p, 1, out var x) && NativeProtocol.TryFloat(p, 2, out var y) &&
                    NativeProtocol.TryFloat(p, 3, out var z))
                    OnTeleportWaypoint(player, x, y, z);
                break;
            case "NOCLIP":
                OnToggleNoClip(player, p.Length > 1 && p[1] == "1");
                break;
            case "MENUSEL":
            case "MENUCLOSED":
            case "KEY":
                HandleNativeUi(player, session, p);
                break;
            case "LOG":
                // Диагностика клиента в журнал сервера — ограничена по длине.
                if (p.Length > 1)
                    Alt.Log($"[FloV:MP b3889] клиент [{session.Id}] {session.Name}: {FloVMP.Core.Chat.PlayerNamePolicy.ForLog(p[1][..Math.Min(p[1].Length, 200)])}");
                break;
        }
    }

    /// <summary>
    /// Попадание по другому игроку. У клиента на экране чужой игрок — обычный
    /// ped, урон по нему локальный; настоящее здоровье жертвы меняет только сервер.
    /// </summary>
    private void OnNativeHit(NativeSession session, NativePlayerProxy attacker, string[] p)
    {
        var victimId = NativeProtocol.UIntOr(p, 1, 0);
        var damage = Math.Clamp(NativeProtocol.IntOr(p, 3, 0), 0, 200);
        if (victimId == 0 || victimId == session.Id || damage == 0) return;
        if (!_nativePlayers.TryGetValue(victimId, out var victimPlayer)) return;
        var victim = (NativePlayerProxy)(object)victimPlayer;
        if (victim.DimensionValue != attacker.DimensionValue || victim.DeadReported) return;
        if (_godModes.TryGetValue(victimId, out var god) && god) return;

        var a = attacker.State;
        var v = victim.State;
        if (!attacker.Session.HasState || a.Dead) return; // мёртвый не стреляет
        var weapon = NativeProtocol.UIntOr(p, 2, 0);
        var dx = a.X - v.X; var dy = a.Y - v.Y; var dz = a.Z - v.Z;
        var dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        // Правдоподобие: удар рукой — вплотную и слабый; наезд — рядом; оружие
        // в сообщении должно совпадать с тем, что у стрелка в руках по STATE.
        var now = _clock.ElapsedMilliseconds;
        string? why = null;
        if (dist > MaxHitDistance) why = $"попадание с {dist:F0} м";
        else if (a.InVehicle) { if (dist > MaxRamDistance && weapon == WeaponUnarmed) why = $"наезд с {dist:F0} м"; }
        else if (weapon == WeaponUnarmed || weapon == 0)
        {
            if (dist > MaxMeleeDistance) why = $"удар рукой с {dist:F0} м";
            else damage = Math.Min(damage, 60);
        }
        else if (a.Weapon != 0 && a.Weapon != WeaponUnarmed && a.Weapon != weapon &&
                 !(_weaponHistory.TryGetValue(session.Id, out var wh) && weapon == wh.Prev &&
                   now - wh.ChangedAt < WeaponSwitchGraceMs))
            why = "оружие не совпадает с тем, что в руках";

        var rate = _hitRate.TryGetValue(session.Id, out var r) && now - r.WindowStart < 1000
            ? (Count: r.Count + 1, Damage: r.Damage + damage, r.WindowStart)
            : (Count: 1, Damage: damage, WindowStart: now);
        _hitRate[session.Id] = rate;
        var pair = _hitPairRate.TryGetValue((session.Id, victimId), out var pr) && now - pr.WindowStart < 1000
            ? (Damage: pr.Damage + damage, pr.WindowStart)
            : (Damage: damage, WindowStart: now);
        _hitPairRate[(session.Id, victimId)] = pair;
        if (why is null && (rate.Count > MaxHitsPerSecond || rate.Damage > MaxDamagePerSecond)) why = "слишком частый урон";
        else if (why is null && pair.Damage > MaxDamagePerVictimPerSecond) why = "слишком много урона одной жертве";
        if (why is not null)
        {
            // В журнал — не чаще раза в 10 с на игрока: поток поддельных HIT не должен забивать лог.
            if (!_hitWarnedAt.TryGetValue(session.Id, out var warned) || now - warned > 10_000)
            {
                _hitWarnedAt[session.Id] = now;
                Alt.LogWarning($"[FloV:MP Античит] {why}: [{session.Id}] {session.Name} → [{victimId}] {victim.Session.Name} — отклонено.");
            }
            return;
        }
        _lastHitAt[(session.Id, victimId)] = now;
        victim.Session.Send("DAMAGE", damage, session.Id, NativeProtocol.UIntOr(p, 2, 0));
    }

    /// <summary>
    /// Рассылка состояний: каждому нативному клиенту — ближайшие игроки в
    /// радиусе sync.stream_radius его измерения, не больше sync.max_streamed.
    ///
    /// Под большой онлайн: частота по дальности (до 60 м — каждые 50 мс, до
    /// 150 м — каждые 100 мс, дальше — каждые 200 мс), неизменившееся не
    /// шлётся, строка PSTATE собирается один раз на игрока за тик, а не на
    /// каждого получателя.
    /// </summary>
    private void SyncNativeWorld(long nowMs)
    {
        if (_nativePlayers.Count == 0) return;
        _syncTick++;
        var radius = _settings.Float("sync.stream_radius");
        var maxStreamed = _settings.Int("sync.max_streamed");

        _nativeGrid.Clear();
        _syncStates.Clear();
        _syncLines.Clear();

        // Сначала собираем только серверно подтверждённых водителей. Это
        // позволяет проверить пассажиров независимо от порядка словаря
        // подключённых клиентов в текущем тике.
        //
        // Записи НЕ очищаются каждый тик: один пропущенный STATE водителя
        // (сеть моргнула, игрок садится за руль) иначе выкидывал бы всех
        // пассажиров из машины на экранах остальных. Запись живёт ещё
        // несколько тиков — столько же, сколько допускает проверка ниже.
        foreach (var (driverId, driver) in _nativePlayers)
        {
            var driverProxy = (NativePlayerProxy)(object)driver;
            if (!_nativeReady.Contains(driverId) || !driverProxy.Session.HasState) continue;
            var driverState = driverProxy.State;
            if (driverState.InVehicle && driverState.Seat == -1 && driverState.VehicleModel != 0)
                _nativeVehicleOwners[driverId] = new NativeVehicleOwner(
                    driverState.VehicleModel, driverState.X, driverState.Y, driverState.Z,
                    driverProxy.DimensionValue, nowMs);
        }
        foreach (var stale in _nativeVehicleOwners
                     .Where(o => nowMs - o.Value.SeenAt > NativeVehicleOwnerTtlMs)
                     .Select(o => o.Key).ToList())
            _nativeVehicleOwners.Remove(stale);

        foreach (var (id, player) in _nativePlayers)
        {
            var np = (NativePlayerProxy)(object)player;
            if (!np.Session.HasState || !_nativeReady.Contains(id)) continue;
            var st = np.State;
            // Флаг NoClip прячет игрока у остальных — верим ему только от тех,
            // кому NoClip разрешён: иначе это невидимость для любого читера.
            if ((st.Flags & NativePlayerState.FlagNoClip) != 0 && !MayUse(player, "noclip"))
                st = st with { Flags = st.Flags & ~NativePlayerState.FlagNoClip };
            st = AuthorizeVehicleState((uint)id, np.DimensionValue, st, nowMs);
            if (_weaponHistory.TryGetValue(id, out var wh))
            {
                if (wh.Cur != st.Weapon) _weaponHistory[id] = (wh.Cur, st.Weapon, nowMs);
            }
            else _weaponHistory[id] = (st.Weapon, st.Weapon, nowMs);
            _syncStates[id] = (st, np.Session.StateVersion, np.Session.Name, np.DimensionValue);
            _nativeGrid.InsertOrUpdate(id, new FloVMP.Core.AntiCheat.Vector3D(st.X, st.Y, st.Z), np.DimensionValue);
        }

        // Игроки клиента alt:V (если такие есть) — снимок раз в 100 мс.
        if (nowMs >= _nextAltSnapshotMs) { _nextAltSnapshotMs = nowMs + 100; _altSnapshotVersion++; }
        foreach (var alt in Alt.GetAllPlayers())
        {
            if (!alt.Exists || !_clientReady.ContainsKey(alt.Id)) continue;
            var pos = alt.Position;
            var heading = alt.Rotation.Yaw * 180f / MathF.PI;
            var st = new NativePlayerState(pos.X, pos.Y, pos.Z, (heading % 360f + 360f) % 360f, 0, 0, 0,
                alt.IsDead ? NativePlayerState.FlagDead : 0, 0, 0, -1, 0, 0, 0, alt.Health, alt.Armor, alt.CurrentWeapon, 1f, alt.Model);
            _syncStates[alt.Id] = (st, _altSnapshotVersion, alt.Name, alt.Dimension);
            _nativeGrid.InsertOrUpdate(alt.Id, new FloVMP.Core.AntiCheat.Vector3D(pos.X, pos.Y, pos.Z), alt.Dimension);
        }

        foreach (var (id, player) in _nativePlayers)
        {
            if (!_nativeReady.Contains(id)) continue;
            var np = (NativePlayerProxy)(object)player;
            if (!_nativeVisible.TryGetValue(id, out var visible))
                _nativeVisible[id] = visible = new Dictionary<uint, (long Version, long Tick)>();

            _syncNear.Clear();
            if (_syncStates.TryGetValue(id, out var own))
            {
                _nativeCandidates.Clear();
                _nativeGrid.FindInRadius(new FloVMP.Core.AntiCheat.Vector3D(own.State.X, own.State.Y, own.State.Z),
                    radius, np.DimensionValue, _nativeCandidates, use3D: false);
                foreach (var other in _nativeCandidates)
                {
                    if (other == id || !_syncStates.TryGetValue(other, out var o)) continue;
                    var dx = o.State.X - own.State.X; var dy = o.State.Y - own.State.Y;
                    _syncNear.Add((other, dx * dx + dy * dy));
                }
                if (_syncNear.Count > maxStreamed)
                {
                    _syncNear.Sort((a, b) => a.D2.CompareTo(b.D2));
                    _syncNear.RemoveRange(maxStreamed, _syncNear.Count - maxStreamed);
                }
            }

            _syncInRange.Clear();
            foreach (var (other, _) in _syncNear) _syncInRange.Add(other);
            _syncGone.Clear();
            foreach (var k in visible.Keys) if (!_syncInRange.Contains(k)) _syncGone.Add(k);
            foreach (var gone in _syncGone)
            {
                visible.Remove(gone);
                np.Session.Send("PDEL", gone);
            }

            foreach (var (other, d2) in _syncNear)
            {
                var info = _syncStates[other];
                if (!visible.TryGetValue(other, out var sent))
                {
                    np.Session.Send("PADD", other, info.Name);
                    sent = (-1, 0);
                }
                if (sent.Version == info.Version) continue;
                var every = d2 < 60 * 60 ? 1 : d2 < 150 * 150 ? 2 : 4;
                if (sent.Version >= 0 && _syncTick - sent.Tick < every) continue;
                visible[other] = (info.Version, _syncTick);
                if (!_syncLines.TryGetValue(other, out var line))
                    _syncLines[other] = line = info.State.FormatFor(other);
                np.Session.Send(line);
            }
        }
    }

    private long _syncTick;
    private readonly record struct NativeVehicleOwner(uint Model, float X, float Y, float Z, int Dimension, long SeenAt);
    private readonly Dictionary<uint, NativeVehicleOwner> _nativeVehicleOwners = new();

    /// <summary>Сколько живёт запись о водителе после его последнего состояния:
    /// короткий пропуск не должен высаживать пассажиров.</summary>
    private const long NativeVehicleOwnerTtlMs = NativeSyncIntervalMs * 4;
    private readonly Dictionary<uint, (NativePlayerState State, long Version, string Name, int Dimension)> _syncStates = new();
    private readonly Dictionary<uint, string> _syncLines = new();
    private readonly List<(uint Id, float D2)> _syncNear = new();
    private readonly HashSet<uint> _syncInRange = new();
    private readonly List<uint> _syncGone = new();

    /// <summary>
    /// Транспорт — серверная сущность даже у native-клиента: водитель владеет
    /// им, а пассажир может ссылаться только на актуального водителя в том же
    /// измерении. Поддельная ссылка превращается в пешее состояние, чтобы не
    /// создать на других клиентах чужую/дублированную машину.
    /// </summary>
    private NativePlayerState AuthorizeVehicleState(uint playerId, int dimension, NativePlayerState state, long nowMs)
    {
        if (!state.InVehicle || state.VehicleModel == 0) return state;
        if (state.Seat == -1)
            return state with { VehicleOwner = (int)playerId };

        var ownerId = state.VehicleOwner > 0 ? (uint)state.VehicleOwner : 0;
        if (ownerId != 0 && _nativeVehicleOwners.TryGetValue(ownerId, out var owner) &&
            owner.Model == state.VehicleModel && owner.Dimension == dimension &&
            nowMs - owner.SeenAt <= NativeSyncIntervalMs * 4 &&
            DistanceSquared(state.X, state.Y, state.Z, owner.X, owner.Y, owner.Z) <= 25f * 25f)
            return state;

        return state with
        {
            Flags = state.Flags & ~NativePlayerState.FlagInVehicle,
            VehicleModel = 0,
            VehicleOwner = 0,
            Seat = -1,
            Rx = 0,
            Ry = 0,
            Rz = 0,
        };
    }

    private static float DistanceSquared(float ax, float ay, float az, float bx, float by, float bz)
    {
        var dx = ax - bx;
        var dy = ay - by;
        var dz = az - bz;
        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>
    /// Транспорт у клиента b3889 создаётся в его игре и синхронизируется
    /// через STATE (модель, место, поворот), а не сущностью alt:V. Поэтому
    /// команды транспорта уходят клиенту, остальные — общим обработчиком.
    /// Уровень доступа уже проверен общим входом HandleCommand.
    /// </summary>
    private bool HandleNativeVehicleCommand(IPlayer player, string cmd, string[] parts)
    {
        var np = (NativePlayerProxy)(object)player;
        var inVehicle = np.Session.HasState && np.State.InVehicle;
        switch (cmd)
        {
            case "car":
            case "veh":
                var modelName = parts.Length > 1 ? parts[1] : "adder";
                modelName = new string(modelName.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
                if (modelName.Length == 0 || modelName.Length > 32)
                {
                    SendChatMessage(player, "{fde047}Использование: /car <модель, например adder>");
                    return true;
                }
                np.Session.Send("CAR", Alt.Hash(modelName.ToLowerInvariant()), modelName);
                return true;
            case "fix":
            case "repair":
                if (!inVehicle) { SendChatMessage(player, "{fde047}Сядьте в транспорт, чтобы починить его."); return true; }
                np.Session.Send("FIXCAR");
                SendChatMessage(player, "{34d399}Транспорт отремонтирован.");
                return true;
            case "dv":
            case "delveh":
            case "destroyveh":
                np.Session.Send("DELCAR");
                return true;
            case "engine":
                if (!inVehicle) { SendChatMessage(player, "{fde047}[Транспорт] Вы должны находиться в транспортном средстве."); return true; }
                np.Session.Send("ENGINE");
                return true;
            case "lock":
                np.Session.Send("LOCK");
                return true;
        }
        return false;
    }

    /// <summary>
    /// Выдать оружие клиенту 3889 вместе с именем: клиент проверяет, что такое
    /// оружие в игре есть, и сам пишет игроку результат. Без этого сервер
    /// отвечал «выдано» на несуществующее имя, а в руках ничего не появлялось.
    /// </summary>
    private bool GiveWeaponWithName(IPlayer player, uint hash, string name, int ammo)
    {
        if (player is not NativePlayerProxy np) return false;
        np.Session.Send("WEAPON", hash, ammo, true, name);
        return true;
    }

    /// <summary>Сменить модель игроку 3889 (клиент сам сообщит, есть ли такая модель).</summary>
    private bool SetModelWithName(IPlayer player, uint hash, string name)
    {
        if (player is not NativePlayerProxy np) return false;
        np.NoteModel(hash);   // иначе player.Model на сервере остался бы прежним
        np.Session.Send("MODEL", hash, name);
        return true;
    }

    /// <summary>Отключить всех нативных клиентов (лицензия, остановка).</summary>
    private void KickAllNative(string reason)
    {
        foreach (var p in _nativePlayers.Values.ToList()) p.Kick(reason);
    }
}
