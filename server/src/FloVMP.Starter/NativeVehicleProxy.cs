using System;
using System.Reflection;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using AltV.Net.Enums;

namespace FloVMP.Starter;

/// <summary>
/// Машина серверного реестра в виде <see cref="IVehicle"/> — то, что теперь
/// возвращает <c>IPlayer.Vehicle</c> у игрока 3889 (раньше всегда null).
///
/// Как и <see cref="NativePlayerProxy"/>: чтение берётся из реестра, запись
/// превращается в команды реестра (их получит водитель через VSET, остальные —
/// обычной рассылкой). Нативного указателя у объекта нет — передавать его в
/// движок alt:V нельзя; всё, чего реестр не знает, пропускается с
/// предупреждением в журнале.
/// </summary>
public class NativeVehicleProxy : DispatchProxy
{
    private uint _id;
    private StarterResource _owner = null!;

    internal void Init(uint id, StarterResource owner)
    {
        _id = id;
        _owner = owner;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method is null) return null;
        args ??= Array.Empty<object?>();
        var v = _owner.RegisteredVehicleById(_id);
        switch (method.Name)
        {
            case "get_Id": return _id;
            case "get_Exists": return v is not null;
            case "get_Type": return BaseObjectType.Vehicle;
            case "get_NativePointer":
            case "get_VehicleNativePointer": return IntPtr.Zero;
            case "GetHashCode": return (int)_id;
            case "Equals": return ReferenceEquals(this, args[0]);
            case "ToString": return $"NativeVehicle[{_id}]";
        }
        // Машину уже убрали: ресурс держит устаревшую ссылку. Значения по
        // умолчанию, как у удалённой сущности alt:V, без исключения.
        if (v is null) return Default(method);

        switch (method.Name)
        {
            case "get_Model": return v.Model;
            case "get_Position": return new Position(v.X, v.Y, v.Z);
            // Rotation alt:V — в радианах, реестр хранит градусы игры.
            case "get_Rotation": return new Rotation(v.Rx * MathF.PI / 180f, v.Ry * MathF.PI / 180f, v.Rz * MathF.PI / 180f);
            case "get_Velocity": return new Position(v.Vx, v.Vy, v.Vz);
            case "get_Dimension": return v.Dimension;
            case "get_NumberplateText": return v.Plate;
            case "set_NumberplateText": _owner.VehicleApiSetPlate(_id, (string?)args[0] ?? ""); return null;
            case "get_EngineOn": return v.EngineOn;
            case "set_EngineOn": _owner.VehicleApiSetEngine(_id, (bool)args[0]!); return null;
            case "get_SirenActive": return v.SirenOn;
            case "get_LockState": return v.Locked ? VehicleLockState.Locked : VehicleLockState.Unlocked;
            case "set_LockState":
                // Реестр знает «закрыта / открыта»; все варианты блокировки alt:V — «закрыта».
                var state = (VehicleLockState)args[0]!;
                _owner.VehicleApiSetLocked(_id, state is not (VehicleLockState.None or VehicleLockState.Unlocked));
                return null;
            case "get_BodyHealth": return (uint)Math.Max(0f, v.BodyHealth);
            case "set_BodyHealth": _owner.VehicleApiSetHealth(_id, (uint)args[0]!, v.EngineHealth); return null;
            case "get_EngineHealth": return (int)v.EngineHealth;
            case "set_EngineHealth": _owner.VehicleApiSetHealth(_id, v.BodyHealth, (int)args[0]!); return null;
            case "get_Driver": return _owner.PlayerForProxy(v.Driver);
            case "Repair": _owner.VehicleApiRepair(_id); return null;
            case "Destroy": _owner.VehicleApiRemove(_id); return null;
        }

        _owner.ReportUnsupportedNativeMember("Vehicle." + method.Name);
        return Default(method);
    }

    private static object? Default(MethodInfo method)
    {
        var type = method.ReturnType;
        return type == typeof(void) || !type.IsValueType ? null : Activator.CreateInstance(type);
    }
}
