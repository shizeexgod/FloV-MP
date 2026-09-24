using System.Globalization;

namespace FloVMP.Core.Native;

/// <summary>
/// Сообщения реестра транспорта FLOV/2 (клиент 1.0.6+). Спецификация и
/// согласованный порядок полей — docs/vehicle-registry-protocol.md.
/// </summary>
public static class NativeVehicleProtocol
{
    /// <summary>С этой версии клиента место в машине берётся из реестра, а не из STATE.</summary>
    public static readonly Version RegistryClientVersion = new(1, 0, 6);

    public const float MinEngineHealth = -4000f;
    public const float MaxHealth = 1000f;

    /// <summary>
    /// Понимает ли клиент реестр. Сравниваем только major.minor.patch:
    /// «1.0.6-beta» — это уже 1.0.6, иначе бета не получила бы то, ради чего
    /// выпущена. «dev» — сборка разработчика из свежего кода. Пустая или
    /// нераспознанная версия — старый клиент: безопаснее старый путь, чем
    /// машины, которые клиент не умеет показать.
    /// </summary>
    public static bool SupportsRegistry(string? clientVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion)) return false;
        var v = clientVersion.Trim();
        if (v.Equals("dev", StringComparison.OrdinalIgnoreCase)) return true;
        var cut = v.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0) v = v[..cut];
        var parts = v.Split('.');
        if (parts.Length != 3) return false;
        var nums = new int[3];
        for (var i = 0; i < 3; i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out nums[i])) return false;
        return new Version(nums[0], nums[1], nums[2]) >= RegistryClientVersion;
    }

    public static string FormatAdd(RegisteredVehicleView v) => NativeProtocol.Format("VADD",
        v.Id, v.Model, v.X, v.Y, v.Z, v.Rx, v.Ry, v.Rz, v.Dimension, AddFlags(v), v.Plate);

    public static string FormatState(RegisteredVehicleView v) => NativeProtocol.Format("VSTATE",
        v.Id, v.X, v.Y, v.Z, v.Rx, v.Ry, v.Rz, v.Vx, v.Vy, v.Vz,
        v.EngineOn, v.SirenOn, v.Locked, v.BodyHealth, v.EngineHealth);

    public static string FormatSet(RegisteredVehicleView v) => NativeProtocol.Format("VSET",
        v.Id, v.EngineOn, v.SirenOn, v.Locked, v.BodyHealth, v.EngineHealth);

    public static string FormatOwner(uint vehicleId, uint playerId, int seat) =>
        NativeProtocol.Format("VOWN", vehicleId, playerId, seat);

    public static int AddFlags(RegisteredVehicleView v) =>
        (v.EngineOn ? 1 : 0) | (v.SirenOn ? 2 : 0) | (v.Locked ? 4 : 0);
}

/// <summary>То, что видят клиенты: одна точка форматирования для VADD/VSTATE/VSET.</summary>
public readonly record struct RegisteredVehicleView(
    uint Id, uint Model, float X, float Y, float Z, float Rx, float Ry, float Rz,
    float Vx, float Vy, float Vz, int Dimension, bool EngineOn, bool SirenOn, bool Locked,
    float BodyHealth, float EngineHealth, string Plate);

/// <summary>
/// VSYNC от водителя: id x y z rx ry rz vx vy vz engineOn sirenOn bodyHp engHp.
/// Разбирается в сетевом потоке и хранится «последнее побеждает», как STATE:
/// 20 строк в секунду от каждого водителя не должны идти через очередь событий.
/// </summary>
public readonly record struct NativeVehicleSync(
    uint VehicleId, float X, float Y, float Z, float Rx, float Ry, float Rz,
    float Vx, float Vy, float Vz, bool EngineOn, bool SirenOn, float BodyHealth, float EngineHealth)
{
    public static bool TryParse(string[] p, out NativeVehicleSync sync)
    {
        sync = default;
        if (p.Length < 15) return false;
        var id = NativeProtocol.UIntOr(p, 1, 0);
        if (id == 0) return false;
        if (!NativeProtocol.TryFloat(p, 2, out var x) || !NativeProtocol.TryFloat(p, 3, out var y) ||
            !NativeProtocol.TryFloat(p, 4, out var z)) return false;
        // Те же границы карты, что у STATE: мусор не должен стать позицией машины.
        if (Math.Abs(x) > 25000f || Math.Abs(y) > 25000f || z < -1500f || z > 5000f) return false;
        // Здоровье без числа отбрасываем целиком, а не подставляем «полное»:
        // иначе строка без этих полей тихо чинила бы машину.
        if (!NativeProtocol.TryFloat(p, 13, out var body) || !NativeProtocol.TryFloat(p, 14, out var engine)) return false;
        // Углы — как их отдаёт игра (−180…180), без приведения к 0…360: тот же
        // вид, что поворот машины в STATE, и клиенту не нужно второе правило.
        static float Angle(float a) => Math.Clamp(a, -360f, 360f);
        static float Clamp(float v, float lim) => Math.Clamp(v, -lim, lim);
        sync = new NativeVehicleSync(id, x, y, z,
            Angle(NativeProtocol.FloatOr(p, 5, 0)), Angle(NativeProtocol.FloatOr(p, 6, 0)), Angle(NativeProtocol.FloatOr(p, 7, 0)),
            Clamp(NativeProtocol.FloatOr(p, 8, 0), 300), Clamp(NativeProtocol.FloatOr(p, 9, 0), 300),
            Clamp(NativeProtocol.FloatOr(p, 10, 0), 300),
            p[11] == "1", p[12] == "1",
            Math.Clamp(body, 0f, NativeVehicleProtocol.MaxHealth),
            Math.Clamp(engine, NativeVehicleProtocol.MinEngineHealth, NativeVehicleProtocol.MaxHealth));
        return true;
    }
}
