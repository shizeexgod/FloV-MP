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

    // Кого из игроков уже видит нативный клиент и какую версию состояния ему отправили.
    private readonly Dictionary<uint, Dictionary<uint, long>> _nativeVisible = new();
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
    private readonly Dictionary<uint, (int Count, long WindowStart)> _hitRate = new();
    private const int MaxHitsPerSecond = 12;
    private const float MaxHitDistance = 300f;

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
        if (refusal is not null)
        {
            if (refusal != "бан") proxy.Kick(refusal);
            Alt.Log($"[FloV:MP b3889] вход {session.Name} ({session.Ip}) отклонён: {refusal}");
            _nativePlayers.Remove(session.Id);
            return;
        }

        // Токен голоса — в WELCOME: UDP-пакеты без него сервер не принимает.
        var voiceToken = _nativeVoice?.Register(session) ?? "";
        session.VoiceMuted = _voiceMutes?.IsMuted(session.Identity.ToString(), DateTime.UtcNow) ?? false;
        session.Send("WELCOME", session.Id, session.Name, session.Identity.ToString(), _native!.ServerName,
            voiceToken, _nativeVoice?.Port ?? 0, _nativeVoice?.Radius ?? 0f);
        Alt.Log($"[FloV:MP b3889] Клиент GTA Legacy {NativeProtocol.GameVersion}: {session.Name} ({session.Ip}), " +
                $"ID игрока {session.Identity} (для setadmin sc:{session.Identity}), клиент {session.ClientVersion}.");

        // Тот же вход, что у клиента alt:V: ник, бан, лицензия, лимит, спавн, права.
        OnPlayerConnect(proxy, "native-b3889");
    }

    private void OnNativeLeft(NativeSession session, string reason)
    {
        if (!_nativePlayers.TryGetValue(session.Id, out var proxy) ||
            !ReferenceEquals(((NativePlayerProxy)(object)proxy).Session, session)) return;
        _nativePlayers.Remove(session.Id);
        _nativeVoice?.Unregister(session);
        _nativeReady.Remove(session.Id);
        _nativeVisible.Remove(session.Id);
        _hitRate.Remove(session.Id);
        foreach (var seen in _nativeVisible.Values) seen.Remove(session.Id);
        foreach (var other in _nativePlayers.Values)
            ((NativePlayerProxy)(object)other).Session.Send("PDEL", session.Id);
        OnPlayerDisconnect(proxy, reason);
    }

    private void OnNativeMessage(NativeSession session, string[] p)
    {
        if (!_nativePlayers.TryGetValue(session.Id, out var player)) return;
        var np = (NativePlayerProxy)(object)player;
        if (!ReferenceEquals(np.Session, session)) return; // хвост сообщений ушедшей сессии
        switch (p[0])
        {
            case "READY":
                if (_nativeReady.Add(session.Id)) OnClientReady(player);
                break;
            case "CHAT":
                if (p.Length > 1) OnChatMessage(player, p[1]);
                break;
            case "DIED":
                if (np.DeadReported) break;
                np.DeadReported = true;
                var killerId = NativeProtocol.UIntOr(p, 1, 0);
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

        var now = _clock.ElapsedMilliseconds;
        var rate = _hitRate.TryGetValue(session.Id, out var r) && now - r.WindowStart < 1000 ? (r.Count + 1, r.WindowStart) : (1, now);
        _hitRate[session.Id] = rate;
        if (rate.Item1 > MaxHitsPerSecond) return;

        var a = attacker.State;
        var v = victim.State;
        var dx = a.X - v.X; var dy = a.Y - v.Y; var dz = a.Z - v.Z;
        if (dx * dx + dy * dy + dz * dz > MaxHitDistance * MaxHitDistance)
        {
            Alt.LogWarning($"[FloV:MP Античит] попадание с {MathF.Sqrt(dx * dx + dy * dy + dz * dz):F0} м: " +
                           $"[{session.Id}] {session.Name} → [{victimId}] {victim.Session.Name} — отклонено.");
            return;
        }
        victim.Session.Send("DAMAGE", damage, session.Id, NativeProtocol.UIntOr(p, 2, 0));
    }

    /// <summary>
    /// Рассылка состояний: каждому нативному клиенту — игроки в радиусе
    /// <see cref="NativeStreamRadius"/> в его измерении. Неизменившееся
    /// состояние повторно не отправляется.
    /// </summary>
    private void SyncNativeWorld(long nowMs)
    {
        if (_nativePlayers.Count == 0) return;

        _nativeGrid.Clear();
        var states = new Dictionary<uint, (NativePlayerState State, long Version, string Name, int Dimension)>();
        foreach (var (id, player) in _nativePlayers)
        {
            var np = (NativePlayerProxy)(object)player;
            if (!np.Session.HasState || !_nativeReady.Contains(id)) continue;
            var st = np.State;
            states[id] = (st, np.Session.StateVersion, np.Session.Name, np.DimensionValue);
            _nativeGrid.InsertOrUpdate(id, new FloVMP.Core.AntiCheat.Vector3D(st.X, st.Y, st.Z), np.DimensionValue);
        }

        // Игроки клиента alt:V (если такие есть) — снимок раз в 100 мс.
        var altSnapshot = nowMs >= _nextAltSnapshotMs;
        if (altSnapshot) { _nextAltSnapshotMs = nowMs + 100; _altSnapshotVersion++; }
        foreach (var alt in Alt.GetAllPlayers())
        {
            if (!alt.Exists || !_clientReady.ContainsKey(alt.Id)) continue;
            var pos = alt.Position;
            var heading = alt.Rotation.Yaw * 180f / MathF.PI;
            var st = new NativePlayerState(pos.X, pos.Y, pos.Z, (heading % 360f + 360f) % 360f, 0, 0, 0,
                alt.IsDead ? NativePlayerState.FlagDead : 0, 0, 0, -1, 0, 0, 0, alt.Health, alt.Armor, alt.CurrentWeapon, 1f, alt.Model);
            states[alt.Id] = (st, _altSnapshotVersion, alt.Name, alt.Dimension);
            _nativeGrid.InsertOrUpdate(alt.Id, new FloVMP.Core.AntiCheat.Vector3D(pos.X, pos.Y, pos.Z), alt.Dimension);
        }

        foreach (var (id, player) in _nativePlayers)
        {
            if (!_nativeReady.Contains(id)) continue;
            var np = (NativePlayerProxy)(object)player;
            if (!_nativeVisible.TryGetValue(id, out var visible))
                _nativeVisible[id] = visible = new Dictionary<uint, long>();

            _nativeCandidates.Clear();
            if (states.TryGetValue(id, out var own))
                _nativeGrid.FindInRadius(new FloVMP.Core.AntiCheat.Vector3D(own.State.X, own.State.Y, own.State.Z),
                    NativeStreamRadius, np.DimensionValue, _nativeCandidates, use3D: false);

            var inRange = new HashSet<uint>(_nativeCandidates);
            inRange.Remove(id);

            foreach (var gone in visible.Keys.Where(k => !inRange.Contains(k)).ToList())
            {
                visible.Remove(gone);
                np.Session.Send("PDEL", gone);
            }

            foreach (var other in inRange)
            {
                if (!states.TryGetValue(other, out var info)) continue;
                if (!visible.TryGetValue(other, out var sentVersion))
                {
                    np.Session.Send("PADD", other, info.Name);
                    sentVersion = -1;
                }
                if (sentVersion == info.Version) continue;
                visible[other] = info.Version;
                np.Session.Send(info.State.FormatFor(other));
            }
        }
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

    /// <summary>Отключить всех нативных клиентов (лицензия, остановка).</summary>
    private void KickAllNative(string reason)
    {
        foreach (var p in _nativePlayers.Values.ToList()) p.Kick(reason);
    }
}
