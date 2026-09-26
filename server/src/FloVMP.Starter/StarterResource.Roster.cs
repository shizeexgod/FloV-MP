using System.Text.Json;
using System.Text.RegularExpressions;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Native;

namespace FloVMP.Starter;

/// <summary>
/// Список игроков сервера и синхронизируемые переменные для клиентов 3889 —
/// то, на чём в RAGE:MP строятся свои табло, ники, HUD денег и работы.
///
/// PADD/PDEL — это зона видимости (кого рисовать рядом). Список игроков
/// другое: PJOIN/PQUIT приходят всем при входе и выходе, новому игроку — весь
/// список сразу. Клиентский код видит его как mp.players.
///
/// Переменные — как player.setVariable / setOwnVariable в RAGE:MP. Геймод:
///   Alt.Emit("flovmp:player:setVariable", id, "job", "\"taxi\"")     — видят все;
///   Alt.Emit("flovmp:player:setOwnVariable", id, "money", "5000")    — видит только он;
///   значение — JSON; пусто или null — удалить.
/// Клиент: player.getVariable("job"), mp.events.addDataHandler("job", …).
/// Протокол: SVAR id ключ JSON (пустой JSON — удалено).
/// </summary>
public partial class StarterResource
{
    private const int MaxVariablesPerPlayer = 128;
    private const int MaxVariableJson = 3800;
    private static readonly Regex VariableKey = new("^[A-Za-z0-9_:.\\-]{1,64}$", RegexOptions.Compiled);

    private readonly Dictionary<uint, Dictionary<string, string>> _sharedVars = new();
    private readonly Dictionary<uint, Dictionary<string, string>> _ownVars = new();

    private void RegisterRosterApi()
    {
        Alt.OnServer<int, string, string>("flovmp:player:setVariable", (id, key, json) => SetPlayerVariable(id, key, json, shared: true));
        Alt.OnServer<int, string, string>("flovmp:player:setOwnVariable", (id, key, json) => SetPlayerVariable(id, key, json, shared: false));
    }

    /// <summary>После WELCOME: новому — весь список и переменные, остальным — что он вошёл.</summary>
    private void SendRosterOnJoin(NativeSession session)
    {
        foreach (var (id, player) in _nativePlayers)
        {
            if (id == session.Id) continue;
            session.Send("PJOIN", id, player.Name);
            if (_sharedVars.TryGetValue(id, out var vars))
                foreach (var (k, v) in vars) session.Send("SVAR", id, k, v);
        }
        // Свои переменные, выставленные геймодом до конца входа (событие
        // подключения приходит раньше WELCOME у медленного клиента).
        foreach (var source in new[] { _sharedVars, _ownVars })
            if (source.TryGetValue(session.Id, out var mine))
                foreach (var (k, v) in mine) session.Send("SVAR", session.Id, k, v);
        foreach (var (id, player) in _nativePlayers)
            if (id != session.Id) ((NativePlayerProxy)(object)player).Session.Send("PJOIN", session.Id, session.Name);
    }

    private void ForgetRoster(uint id)
    {
        _sharedVars.Remove(id);
        _ownVars.Remove(id);
        foreach (var (other, player) in _nativePlayers)
            if (other != id) ((NativePlayerProxy)(object)player).Session.Send("PQUIT", id);
    }

    private void SetPlayerVariable(int id, string key, string? json, bool shared)
    {
        var api = shared ? "flovmp:player:setVariable" : "flovmp:player:setOwnVariable";
        if (key is null || !VariableKey.IsMatch(key))
        {
            Alt.LogWarning($"[FloV:MP] {api}: недопустимое имя «{key}» (буквы, цифры, _:.- , до 64)");
            return;
        }
        var target = NativeById(id);
        if (target is null) return;
        var remove = string.IsNullOrWhiteSpace(json) || json.Trim() == "null";
        if (!remove)
        {
            if (json!.Length > MaxVariableJson)
            {
                Alt.LogWarning($"[FloV:MP] {api} «{key}»: значение длиннее {MaxVariableJson} символов");
                return;
            }
            try { using var _ = JsonDocument.Parse(json); }
            catch (JsonException)
            {
                Alt.LogWarning($"[FloV:MP] {api} «{key}»: значение не JSON — строки в кавычках: \"\\\"taxi\\\"\"");
                return;
            }
        }

        var store = shared ? _sharedVars : _ownVars;
        var pid = (uint)id;
        if (!store.TryGetValue(pid, out var vars)) store[pid] = vars = new Dictionary<string, string>(StringComparer.Ordinal);
        if (remove)
        {
            if (!vars.Remove(key)) return;
        }
        else
        {
            if (vars.TryGetValue(key, out var old) && old == json) return;   // не рассылать то же самое
            if (!vars.ContainsKey(key) && vars.Count >= MaxVariablesPerPlayer)
            {
                Alt.LogWarning($"[FloV:MP] {api}: у игрока {id} уже {MaxVariablesPerPlayer} переменных — «{key}» не сохранена");
                return;
            }
            vars[key] = json!;
        }

        var value = remove ? "" : json!;
        if (!shared)
        {
            ((NativePlayerProxy)(object)target).Session.Send("SVAR", pid, key, value);
            return;
        }
        foreach (var (_, player) in _nativePlayers)
            ((NativePlayerProxy)(object)player).Session.Send("SVAR", pid, key, value);
    }
}
