using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using AltV.Net.Enums;
using FloVMP.Core.Native;

namespace FloVMP.Starter;

/// <summary>
/// Игрок нативного клиента GTA V Legacy b3889 в виде обычного <see cref="IPlayer"/>.
///
/// Вся платформа — вход, баны, права, лицензия, чат и админ-команды — написана
/// против IPlayer. Отдельная копия всего этого для b3889 разъехалась бы с
/// оригиналом при первой же правке, поэтому нативный игрок притворяется
/// IPlayer: чтение (позиция, здоровье, ник) берётся из его последнего STATE,
/// а запись и Emit превращаются в сообщения протокола FLOV/2.
///
/// Такой объект НЕЛЬЗЯ передавать в движок alt:V (голосовой канал, Alt.Emit
/// с игроком в аргументах): у него нет нативного указателя. Эти места в
/// StarterResource проверяют <see cref="StarterResource.IsNative"/>.
/// </summary>
public class NativePlayerProxy : DispatchProxy
{
    internal NativeSession Session = null!;
    internal StarterResource Owner = null!;

    private int _dimension;
    private uint _model = 0x705E61F2; // mp_m_freemode_01

    /// <summary>Модель, выданная командой платформы вместе с именем (см. StarterResource.Native).</summary>
    internal void NoteModel(uint model) => _model = model;
    private ushort _maxHealth = 200;
    private ushort? _healthOverride;
    private ushort? _armorOverride;
    private long _overrideStateVersion;
    internal bool DeadReported;

    /// <summary>
    /// Здоровье и броня, которые считает сервер. Клиент сам применяет урон у
    /// себя, поэтому его число — только заявка: если оно больше серверного,
    /// значит урон «не заметили», и верим серверу. Меньше — верим клиенту
    /// (упал с высоты, сбила машина) и опускаем серверное следом. Расти эти
    /// значения могут только по команде сервера: лечение, спавн, возрождение.
    /// </summary>
    internal ushort ServerHealth = 200;
    internal ushort ServerArmor;
    internal readonly ConcurrentDictionary<string, object?> LocalMeta = new();

    internal void Init(NativeSession session, StarterResource owner)
    {
        Session = session;
        Owner = owner;
    }

    public NativePlayerState State => Session.State;
    public int DimensionValue => _dimension;

    private ushort CurrentHealth()
    {
        if (DeadReported) return 0;
        if (_healthOverride is { } h && Session.StateVersion <= _overrideStateVersion + 2) return h;
        _healthOverride = null;
        if (!Session.HasState) return ServerHealth;
        var reported = (ushort)Math.Clamp(State.Health, 0, 1000);
        if (reported < ServerHealth) ServerHealth = reported;   // клиент потерял больше — верим ему
        return ServerHealth;                                     // больше серверного — не верим
    }

    private ushort CurrentArmor()
    {
        if (_armorOverride is { } a && Session.StateVersion <= _overrideStateVersion + 2) return a;
        _armorOverride = null;
        if (!Session.HasState) return ServerArmor;
        var reported = (ushort)Math.Clamp(State.Armor, 0, 200);
        if (reported < ServerArmor) ServerArmor = reported;
        return ServerArmor;
    }

    /// <summary>Урон, посчитанный сервером: сначала броня, потом здоровье.</summary>
    internal (ushort Health, ushort Armor) ApplyServerDamage(int damage)
    {
        var left = Math.Max(0, damage);
        var absorbed = Math.Min((int)ServerArmor, left);
        ServerArmor = (ushort)(ServerArmor - absorbed);
        left -= absorbed;
        ServerHealth = (ushort)Math.Max(0, ServerHealth - left);
        return (ServerHealth, ServerArmor);
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method is null) return null;
        args ??= Array.Empty<object?>();
        var s = Session;
        switch (method.Name)
        {
            case "get_Id": return s.Id;
            case "get_Name": return s.Name;
            case "get_Exists":
            case "get_IsConnected": return s.Joined && !s.Closing;
            case "get_Type": return BaseObjectType.Player;
            case "get_NativePointer": return IntPtr.Zero;
            case "get_SocialClubId": return s.Identity;
            case "get_Ip": return s.Ip;
            case "get_HardwareIdHash": return s.HardwareId;
            case "get_HardwareIdExHash": return s.MacHash;
            case "get_Position":
                {
                    var st = State;
                    return new Position(st.X, st.Y, st.Z);
                }
            case "set_Position":
                {
                    var p = (Position)args[0]!;
                    s.OverridePosition(p.X, p.Y, p.Z);
                    s.Send("TP", p.X, p.Y, p.Z);
                    return null;
                }
            case "get_Rotation":
                return new Rotation(0, 0, State.Heading * (float)Math.PI / 180f);
            case "set_Rotation":
                {
                    var r = (Rotation)args[0]!;
                    // Rotation alt:V — в радианах; клиенту GTA нужен курс в градусах.
                    var heading = ((r.Yaw * 180f / (float)Math.PI) % 360f + 360f) % 360f;
                    s.OverrideHeading(heading);
                    s.Send("HEADING", heading);
                    return null;
                }
            case "get_Dimension": return _dimension;
            case "set_Dimension":
                _dimension = (int)args[0]!;
                s.Dimension = _dimension;
                s.Send("DIM", _dimension);
                Owner.OnNativeDimensionChanged(s, _dimension);
                return null;
            case "get_Health": return CurrentHealth();
            case "set_Health":
                {
                    var h = (ushort)args[0]!;
                    _healthOverride = h;
                    _overrideStateVersion = s.StateVersion;
                    ServerHealth = h;   // лечение и возрождение — единственный путь вверх
                    if (h > 0) DeadReported = false;
                    s.Send("HEALTH", (int)h);
                    return null;
                }
            case "get_MaxHealth": return _maxHealth;
            case "set_MaxHealth": _maxHealth = (ushort)args[0]!; return null;
            case "get_Armor": return CurrentArmor();
            case "set_Armor":
                {
                    var a = (ushort)args[0]!;
                    _armorOverride = a;
                    _overrideStateVersion = s.StateVersion;
                    ServerArmor = a;
                    s.Send("ARMOR", (int)a);
                    return null;
                }
            case "get_IsDead": return DeadReported || (s.HasState && State.Dead);
            case "get_IsInVehicle": return s.HasState && State.InVehicle;
            case "get_Vehicle": return null;
            case "get_Seat": return (byte)0;
            case "get_Model": return s.HasState && State.PedModel != 0 ? State.PedModel : _model;
            case "set_Model":
                _model = (uint)args[0]!;
                s.Send("MODEL", _model);
                return null;
            case "get_CurrentWeapon": return State.Weapon;
            case "Spawn":
                {
                    // Spawn(Position, uint) | Spawn(uint model, Position, uint) | Spawn(PedModel, Position, uint)
                    var posIndex = args.Length == 2 ? 0 : 1;
                    if (args.Length == 3) _model = Convert.ToUInt32(args[0], CultureInfo.InvariantCulture);
                    var p = (Position)args[posIndex]!;
                    DeadReported = false;
                    _healthOverride = 200;
                    ServerHealth = 200;
                    ServerArmor = 0;
                    _overrideStateVersion = s.StateVersion;
                    s.OverridePosition(p.X, p.Y, p.Z);
                    s.Send("SPAWN", p.X, p.Y, p.Z, State.Heading, _model);
                    return null;
                }
            case "Kick":
                s.Close(args.Length > 0 ? args[0] as string ?? "Вы отключены от сервера." : "Вы отключены от сервера.");
                return null;
            case "GiveWeapon":
                s.Send("WEAPON", Convert.ToUInt32(args[0], CultureInfo.InvariantCulture), (int)args[1]!, (bool)args[2]!);
                return null;
            case "RemoveAllWeapons":
                s.Send("DISARM");
                return null;
            case "SetLocalMetaData":
                LocalMeta[(string)args[0]!] = args[1];
                return null;
            case "Emit":
                OnEmit((string)args[0]!, args.Length > 1 ? args[1] as object?[] ?? Array.Empty<object?>() : Array.Empty<object?>());
                return null;
            case "GetHashCode": return (int)s.Id;
            case "Equals": return ReferenceEquals(this, args[0]);
            case "ToString": return $"NativePlayer[{s.Id}] {s.Name}";
        }

        Owner.ReportUnsupportedNativeMember(method.Name);
        var type = method.ReturnType;
        return type == typeof(void) || !type.IsValueType ? null : Activator.CreateInstance(type);
    }

    private static string Str(object?[] a, int i) => i < a.Length ? Convert.ToString(a[i], CultureInfo.InvariantCulture) ?? "" : "";

    /// <summary>
    /// События клиента alt:V → команды нативного клиента. Имена событий те же,
    /// что шлёт платформа, поэтому код StarterResource не знает, какой у игрока клиент.
    /// </summary>
    private void OnEmit(string name, object?[] a)
    {
        var s = Session;
        switch (name)
        {
            case "flovmp:chat:msg": s.Send("MSG", Str(a, 0), Str(a, 1), Str(a, 2)); break;
            case "flovmp:console:setAdmin": s.Send("ADMIN", Str(a, 0)); break;
            case "flovmp:chat:commands":
                {
                    // CMDS имена,через,запятую имя описание имя описание ...: имена —
                    // доступность F3/F4/F5, пары — подсказки чата и консоли.
                    // Старый клиент читает только первое поле.
                    var names = new System.Collections.Generic.List<string>();
                    var fields = new System.Collections.Generic.List<object?>();
                    try
                    {
                        using var doc = JsonDocument.Parse(Str(a, 0));
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            if (!item.TryGetProperty("cmd", out var cmd)) continue;
                            var cmdName = cmd.GetString() ?? "";
                            if (cmdName.Length == 0 || cmdName.Contains(',')) continue;
                            names.Add(cmdName);
                            fields.Add(cmdName);
                            fields.Add(item.TryGetProperty("desc", out var desc) ? desc.GetString() ?? "" : "");
                        }
                    }
                    catch (JsonException) { }
                    fields.Insert(0, string.Join(",", names));
                    s.Send("CMDS", fields.ToArray());
                    break;
                }
            case "flovmp:admin:roster":
                {
                    var pairs = new System.Collections.Generic.List<string>();
                    try
                    {
                        using var doc = JsonDocument.Parse(Str(a, 0));
                        foreach (var p in doc.RootElement.EnumerateObject())
                            pairs.Add(p.Name + ":" + p.Value.GetInt32().ToString(CultureInfo.InvariantCulture));
                    }
                    catch (JsonException) { }
                    s.Send("ROSTER", string.Join(",", pairs));
                    break;
                }
            // Интерфейс без клиентских скриптов: уведомление и меню (см. StarterResource.World).
            case "flovmp:ui:notify":
                s.Send("NOTIFY", Str(a, 0), a.Length > 1 ? Str(a, 1) : "4000");
                break;
            case "flovmp:ui:menu":
                {
                    var menuId = Str(a, 0);
                    if (menuId.Length is 0 or > 64) break;
                    var fields = new System.Collections.Generic.List<object?> { menuId, Str(a, 1) };
                    try
                    {
                        using var doc = JsonDocument.Parse(Str(a, 2));
                        foreach (var item in doc.RootElement.EnumerateArray().Take(200))
                        {
                            if (item.ValueKind == JsonValueKind.String) { fields.Add(item.GetString()); fields.Add(""); continue; }
                            fields.Add(item.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "");
                            fields.Add(item.TryGetProperty("desc", out var d) ? d.GetString() ?? "" : "");
                        }
                    }
                    catch (JsonException) { AltV.Net.Alt.LogWarning($"[FloV:MP] flovmp:ui:menu «{menuId}»: пункты — JSON-массив строк или {{label, desc}}"); break; }
                    Owner.NoteMenuOpened(s.Id, menuId);
                    s.Send("MENU", fields.ToArray());
                    break;
                }
            case "flovmp:ui:closeMenu":
                Owner.NoteMenuOpened(s.Id, null);
                s.Send("MENUCLOSE");
                break;
            case "starter:setWeather": s.Send("WEATHER", Str(a, 0)); break;
            case "starter:setTime": s.Send("TIME", Str(a, 0), Str(a, 1)); break;
            case "starter:setFrozen": s.Send("FREEZE", Str(a, 0) == "True" ? "1" : "0"); break;
            case "starter:setGodMode": s.Send("GOD", Str(a, 0) == "True" ? "1" : "0"); break;
            case "starter:setSpeed": s.Send("SPEED", Str(a, 0)); break;
            case "starter:revive": s.Send("REVIVE"); break;
            case "starter:toggleNoClip": s.Send("NOCLIP"); break;
            case "flovmp:admin:toggleEsp": s.Send("ESP", Str(a, 0)); break;
            case "starter:requestWaypointTp": s.Send("REQTPM"); break;
            case "flovmp:chat:clear": s.Send("CLEARCHAT"); break;
            // Привычные события чата из ресурсов alt:V — тот же чат у клиента 3889.
            case "chat:addMessage": s.Send("MSG", "system", "", Str(a, 0)); break;
            case "chat:message": s.Send("MSG", "player", Str(a, 0), Str(a, 1)); break;
            case "flovmp:admin:spectate": s.Send("SPECTATE", Str(a, 0), Str(a, 1) == "True" ? "1" : "0"); break;
            case "starter:copyCoords":
                s.Send("COPY", string.Join(", ", Enumerable.Range(0, 4).Select(i =>
                    Convert.ToDouble(i < a.Length ? a[i] : 0, CultureInfo.InvariantCulture).ToString("F2", CultureInfo.InvariantCulture))));
                break;
            // Для alt:V-клиента: у нативного своё приветствие.
            case "starter:initClient":
            case "flovmp:client:welcome":
                break;
            default:
                // Событие своего ресурса (gamemode). Клиентских скриптов у
                // нативного клиента нет — передаём как есть, клиент покажет его
                // в журнале, а SDK может подписаться на EVENT.
                s.Send("EVENT", name, JsonSerializer.Serialize(a));
                break;
        }
    }
}
