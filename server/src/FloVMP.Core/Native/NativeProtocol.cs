using System.Globalization;
using System.Text;

namespace FloVMP.Core.Native;

/// <summary>
/// Протокол нативного клиента FloV:MP для GTA V Legacy 1.0.3889.0 (FLOV/2).
///
/// Одна строка UTF-8 = одно сообщение, поля разделены табуляцией. Внутри
/// полей экранируются обратная косая, табуляция и переводы строк — чат
/// игрока не может «вставить» лишнее поле или второе сообщение.
///
/// Рукопожатие:
///   клиент → HELLO  proto  gameVersion  clientVersion  name  publicKey(b64)  hwid(hex16)  mac(hex16)
///   сервер → CHALLENGE  nonce(b64)
///   клиент → AUTH  signature(b64)              (ECDSA P-256 над AuthMessage)
///   сервер → WELCOME  id  name  identity  serverName   |   REJECT  причина
/// Дальше клиент шлёт READY, STATE, CHAT, DIED, HIT, TPM, NOCLIP, PING,
/// а сервер — SPAWN, PADD, PSTATE, PDEL, MSG, ADMIN, TP и команды администрирования.
/// </summary>
public static class NativeProtocol
{
    public const string Version = "2";
    public const string GameVersion = "1.0.3889.0";
    public const int MaxLineBytes = 4096;
    public const string AuthDomain = "FLOVMP-AUTH-v2";

    /// <summary>Порт по умолчанию: игровой порт + 10 (7788 → 7798).</summary>
    public const int DefaultPort = 7798;

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var sb = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\t': sb.Append("\\t"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    public static string Unescape(string value)
    {
        if (value.IndexOf('\\') < 0) return value;
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch != '\\' || i + 1 >= value.Length) { sb.Append(ch); continue; }
            var next = value[++i];
            sb.Append(next switch { 't' => '\t', 'n' => '\n', 'r' => '\r', _ => next });
        }
        return sb.ToString();
    }

    /// <summary>Собрать строку сообщения (без завершающего \n).</summary>
    public static string Format(string type, params object?[] fields)
    {
        var sb = new StringBuilder(type, 64);
        foreach (var field in fields)
        {
            sb.Append('\t');
            sb.Append(field switch
            {
                null => "",
                string s => Escape(s),
                float f => f.ToString("0.###", CultureInfo.InvariantCulture),
                double d => d.ToString("0.###", CultureInfo.InvariantCulture),
                bool b => b ? "1" : "0",
                IFormattable fm => fm.ToString(null, CultureInfo.InvariantCulture),
                _ => Escape(field.ToString()),
            });
        }
        return sb.ToString();
    }

    /// <summary>Разобрать строку: [0] — тип, дальше поля без экранирования.</summary>
    public static string[] Parse(string line)
    {
        var parts = line.Split('\t');
        for (var i = 1; i < parts.Length; i++) parts[i] = Unescape(parts[i]);
        return parts;
    }

    public static bool TryFloat(string[] parts, int index, out float value)
    {
        value = 0;
        return index < parts.Length &&
               float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
               float.IsFinite(value);
    }

    public static float FloatOr(string[] parts, int index, float fallback) =>
        TryFloat(parts, index, out var v) ? v : fallback;

    public static int IntOr(string[] parts, int index, int fallback) =>
        index < parts.Length && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v : fallback;

    public static uint UIntOr(string[] parts, int index, uint fallback) =>
        index < parts.Length && uint.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v : fallback;

    /// <summary>Данные, которые подписывает клиент: домен + nonce + публичный ключ.</summary>
    public static byte[] AuthMessage(byte[] nonce, byte[] publicKey)
    {
        var domain = Encoding.ASCII.GetBytes(AuthDomain);
        var buffer = new byte[domain.Length + nonce.Length + publicKey.Length];
        domain.CopyTo(buffer, 0);
        nonce.CopyTo(buffer, domain.Length);
        publicKey.CopyTo(buffer, domain.Length + nonce.Length);
        return buffer;
    }
}

/// <summary>
/// Состояние игрока, которое клиент шлёт ~20 раз в секунду.
/// Поля STATE: x y z heading vx vy vz flags vehModel vehOwner seat rx ry rz health armor weapon speed pedModel.
/// Пешком rx ry rz — точка прицеливания, в транспорте — его поворот (градусы).
/// </summary>
public readonly record struct NativePlayerState(
    float X, float Y, float Z, float Heading,
    float Vx, float Vy, float Vz,
    int Flags,
    uint VehicleModel, int VehicleOwner, int Seat,
    float Rx, float Ry, float Rz,
    int Health, int Armor, uint Weapon, float Speed, uint PedModel = 0)
{
    public const int FlagInVehicle = 1;
    public const int FlagDead = 2;
    public const int FlagAiming = 4;
    public const int FlagShooting = 8;
    public const int FlagDucking = 16;
    public const int FlagJumping = 32;
    public const int FlagRagdoll = 64;
    public const int FlagNoClip = 128;
    public const int FlagEngineOn = 256;
    public const int FlagSiren = 512;

    public bool InVehicle => (Flags & FlagInVehicle) != 0;
    public bool Dead => (Flags & FlagDead) != 0;

    /// <summary>Разбор STATE с проверкой границ: мусор от клиента не попадает в мир.</summary>
    public static bool TryParse(string[] p, out NativePlayerState state)
    {
        state = default;
        if (p.Length < 19) return false;
        if (!NativeProtocol.TryFloat(p, 1, out var x) || !NativeProtocol.TryFloat(p, 2, out var y) ||
            !NativeProtocol.TryFloat(p, 3, out var z) || !NativeProtocol.TryFloat(p, 4, out var h)) return false;
        if (Math.Abs(x) > 25000f || Math.Abs(y) > 25000f || z < -1500f || z > 5000f) return false;
        static float Clamp(float v, float lim) => Math.Clamp(v, -lim, lim);
        state = new NativePlayerState(
            x, y, z, ((h % 360f) + 360f) % 360f,
            Clamp(NativeProtocol.FloatOr(p, 5, 0), 300), Clamp(NativeProtocol.FloatOr(p, 6, 0), 300),
            Clamp(NativeProtocol.FloatOr(p, 7, 0), 300),
            NativeProtocol.IntOr(p, 8, 0) & 0xFFFF,
            NativeProtocol.UIntOr(p, 9, 0), NativeProtocol.IntOr(p, 10, 0), Math.Clamp(NativeProtocol.IntOr(p, 11, -1), -1, 16),
            Clamp(NativeProtocol.FloatOr(p, 12, 0), 25000), Clamp(NativeProtocol.FloatOr(p, 13, 0), 25000),
            Clamp(NativeProtocol.FloatOr(p, 14, 0), 25000),
            Math.Clamp(NativeProtocol.IntOr(p, 15, 200), 0, 1000), Math.Clamp(NativeProtocol.IntOr(p, 16, 0), 0, 200),
            NativeProtocol.UIntOr(p, 17, 0), Math.Clamp(NativeProtocol.FloatOr(p, 18, 0), 0, 3),
            NativeProtocol.UIntOr(p, 19, 0));
        return true;
    }

    /// <summary>PSTATE для остальных игроков: тот же набор полей с ID владельца впереди.</summary>
    public string FormatFor(uint id) => NativeProtocol.Format("PSTATE", id, X, Y, Z, Heading, Vx, Vy, Vz, Flags,
        VehicleModel, VehicleOwner, Seat, Rx, Ry, Rz, Health, Armor, Weapon, Speed, PedModel);
}
