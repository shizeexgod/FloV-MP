using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using FloVMP.Core.Auth;

namespace FloVMP.Core.Bridge;

/// <summary>
/// Атрибут команды, совместимый с RageMP / RedAge v3 ([Command("veh")]).
/// Позволяет подключать существующие контроллеры и команды RedAge к серверу FloV:MP без переписывания сигнатур.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class CommandAttribute : Attribute
{
    public string CommandName { get; }
    public string Description { get; set; } = string.Empty;
    public string Usage { get; set; } = string.Empty;
    public int MinAdminLevel { get; set; }

    public CommandAttribute(string commandName)
    {
        CommandName = commandName?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}

/// <summary>
/// Атрибут удалённого клиентского события, совместимый с RageMP / RedAge v3 ([RemoteEvent("eventName")]).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RemoteEventAttribute : Attribute
{
    public string EventName { get; }

    public RemoteEventAttribute(string eventName)
    {
        EventName = eventName ?? string.Empty;
    }
}

/// <summary>
/// Контекст вызова команды мода RedAge / RageMP.
/// </summary>
public sealed record RageCallContext(
    uint PlayerId,
    string PlayerName,
    int AdminLevel,
    float PosX,
    float PosY,
    float PosZ,
    int Dimension);

/// <summary>
/// Диспетчер команд и удалённых вызовов для адаптации готовых RP-модов (RedAge, NeptuneEvo, State99).
/// </summary>
public sealed class RageCommandDispatcher
{
    private readonly ConcurrentDictionary<string, RegisteredCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RegisteredRemoteEvent> _remoteEvents = new(StringComparer.OrdinalIgnoreCase);

    private sealed record RegisteredCommand(
        string Name,
        int MinAdminLevel,
        MethodInfo Method,
        object? TargetInstance,
        ParameterInfo[] Parameters);

    private sealed record RegisteredRemoteEvent(
        string Name,
        MethodInfo Method,
        object? TargetInstance,
        ParameterInfo[] Parameters);

    /// <summary>
    /// Автоматически сканирует экземпляр класса мода и регистрирует все [Command] и [RemoteEvent].
    /// </summary>
    public int RegisterHandlers(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var type = instance.GetType();
        int count = 0;

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            var cmdAttr = method.GetCustomAttribute<CommandAttribute>();
            if (cmdAttr != null && !string.IsNullOrEmpty(cmdAttr.CommandName))
            {
                var target = method.IsStatic ? null : instance;
                _commands[cmdAttr.CommandName] = new RegisteredCommand(
                    cmdAttr.CommandName,
                    cmdAttr.MinAdminLevel,
                    method,
                    target,
                    method.GetParameters());
                count++;
            }

            var remAttr = method.GetCustomAttribute<RemoteEventAttribute>();
            if (remAttr != null && !string.IsNullOrEmpty(remAttr.EventName))
            {
                var target = method.IsStatic ? null : instance;
                _remoteEvents[remAttr.EventName] = new RegisteredRemoteEvent(
                    remAttr.EventName,
                    method,
                    target,
                    method.GetParameters());
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Выполнить команду мода. Возвращает true если команда была найдена и обработана.
    /// </summary>
    public bool ExecuteCommand(RageCallContext context, string commandLine, out string feedback)
    {
        feedback = string.Empty;
        if (string.IsNullOrWhiteSpace(commandLine)) return false;

        var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var cmdName = parts[0].TrimStart('/').ToLowerInvariant();
        if (!_commands.TryGetValue(cmdName, out var reg)) return false;

        if (context.AdminLevel < reg.MinAdminLevel)
        {
            feedback = $"[FloV:MP Security] Доступ запрещен: требуется уровень администратора {reg.MinAdminLevel}.";
            return true;
        }

        try
        {
            var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
            var invokedArgs = BindParameters(reg.Parameters, context, args);
            var result = reg.Method.Invoke(reg.TargetInstance, invokedArgs);
            if (result is string s) feedback = s;
            return true;
        }
        catch (Exception ex)
        {
            feedback = $"[FloV:MP Bridge Error] Ошибка выполнения /{cmdName}: {ex.InnerException?.Message ?? ex.Message}";
            return true;
        }
    }

    /// <summary>
    /// Вызвать зарегистрированное [RemoteEvent].
    /// </summary>
    public bool TriggerRemoteEvent(RageCallContext context, string eventName, object[] args, out object? result)
    {
        result = null;
        if (string.IsNullOrEmpty(eventName) || !_remoteEvents.TryGetValue(eventName, out var reg))
            return false;

        try
        {
            var invokedArgs = BindRemoteParameters(reg.Parameters, context, args);
            result = reg.Method.Invoke(reg.TargetInstance, invokedArgs);
            return true;
        }
        catch (Exception ex)
        {
            result = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
    }

    public bool HasCommand(string name) => _commands.ContainsKey(name);
    public bool HasRemoteEvent(string name) => _remoteEvents.ContainsKey(name);
    public IReadOnlyCollection<string> RegisteredCommandNames => _commands.Keys.ToArray();

    private static object?[] BindParameters(ParameterInfo[] parameters, RageCallContext ctx, string[] args)
    {
        var result = new object?[parameters.Length];
        int rawArgIdx = 0;

        for (int i = 0; i < parameters.Length; i++)
        {
            var pType = parameters[i].ParameterType;

            if (pType == typeof(RageCallContext))
            {
                result[i] = ctx;
            }
            else if (pType == typeof(uint) && i == 0)
            {
                result[i] = ctx.PlayerId;
            }
            else if (rawArgIdx < args.Length)
            {
                var val = args[rawArgIdx++];
                result[i] = ConvertArgument(val, pType);
            }
            else
            {
                result[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : GetDefaultValue(pType);
            }
        }

        return result;
    }

    private static object?[] BindRemoteParameters(ParameterInfo[] parameters, RageCallContext ctx, object[] args)
    {
        var result = new object?[parameters.Length];
        int rawIdx = 0;

        for (int i = 0; i < parameters.Length; i++)
        {
            var pType = parameters[i].ParameterType;
            if (pType == typeof(RageCallContext))
            {
                result[i] = ctx;
            }
            else if (rawIdx < args.Length)
            {
                result[i] = args[rawIdx++];
            }
            else
            {
                result[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : GetDefaultValue(pType);
            }
        }

        return result;
    }

    private static object? ConvertArgument(string val, Type targetType)
    {
        if (targetType == typeof(string)) return val;
        if (targetType == typeof(int) && int.TryParse(val, out var iVal)) return iVal;
        if (targetType == typeof(uint) && uint.TryParse(val, out var uVal)) return uVal;
        if (targetType == typeof(float) && float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fVal)) return fVal;
        if (targetType == typeof(double) && double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dVal)) return dVal;
        if (targetType == typeof(bool) && bool.TryParse(val, out var bVal)) return bVal;
        return val;
    }

    private static object? GetDefaultValue(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
}

/// <summary>
/// Адаптер структуры персонажей RedAge / NeptuneEvo для прозрачного переноса в FloV:MP.
/// </summary>
public sealed class RedAgeCharacterAdapter
{
    public int Uuid { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Gender { get; set; } // 0: Мужской, 1: Женский
    public int Health { get; set; } = 200;
    public int Armor { get; set; } = 100;
    public int Level { get; set; } = 1;
    public long Money { get; set; }
    public long Bank { get; set; }
    public int FractionId { get; set; }
    public int FractionRank { get; set; }
    public int AdminLevel { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }

    /// <summary>
    /// Конвертирует персонажа RedAge в аккаунт FloV:MP Core.
    /// </summary>
    public Account ToFlovmpAccount()
    {
        var fullName = $"{FirstName}_{LastName}".Trim('_');
        if (string.IsNullOrEmpty(fullName)) fullName = $"player_{Uuid}";

        return new Account
        {
            Id = Uuid,
            Username = fullName,
            AdminLevel = Math.Clamp(AdminLevel, 0, 8),
            IsBanned = false,
            CreatedUtc = DateTime.UtcNow.ToString("O"),
            LastLoginUtc = DateTime.UtcNow.ToString("O")
        };
    }

    /// <summary>
    /// Парсит позицию персонажа из формата RedAge (JSON: {"x":..,"y":..,"z":..} или X,Y,Z).
    /// </summary>
    public static (float x, float y, float z) ParsePosition(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (198.8f, -935.6f, 30.7f);

        raw = raw.Trim();
        if (raw.StartsWith('{') && raw.EndsWith('}'))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                float x = root.TryGetProperty("x", out var px) ? px.GetSingle() : 198.8f;
                float y = root.TryGetProperty("y", out var py) ? py.GetSingle() : -935.6f;
                float z = root.TryGetProperty("z", out var pz) ? pz.GetSingle() : 30.7f;
                return (x, y, z);
            }
            catch { }
        }

        var parts = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 &&
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fx) &&
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fy) &&
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fz))
        {
            return (fx, fy, fz);
        }

        return (198.8f, -935.6f, 30.7f);
    }
}
