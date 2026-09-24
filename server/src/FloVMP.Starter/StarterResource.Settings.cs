using System;
using System.IO;
using System.Linq;
using AltV.Net;
using AltV.Net.Data;
using FloVMP.Core.Native;
using FloVMP.Core.Settings;

namespace FloVMP.Starter;

/// <summary>
/// Настройки владельца (server/config/client.cfg): что применяет сервер и что
/// рассылается клиентам. См. <see cref="ServerSettings"/>.
/// </summary>
public partial class StarterResource
{
    private ServerSettings _settings = new();
    // Свой ресурс задал точку появления событием flovmp:settings:spawn — она
    // важнее списка из client.cfg.
    private bool _spawnOverridden;

    // Значения, заданные геймодом из кода (flovmp:settings:set). Код ресурса —
    // решение владельца, поэтому оно важнее файла и переживает reloadsettings.
    private readonly Dictionary<string, string> _settingOverrides = new(StringComparer.OrdinalIgnoreCase);

    private void LoadSettings(bool broadcast)
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "config");
        _settings = ServerSettings.LoadOrCreate(dir, Alt.LogWarning, out var summary);
        Alt.Log("[FloV:MP] " + summary);
        foreach (var (key, value) in _settingOverrides) _settings.TrySet(key, value, out _);
        if (_settingOverrides.Count > 0)
            Alt.Log($"[FloV:MP] Настройки из кода ресурса поверх client.cfg: {string.Join(", ", _settingOverrides.Keys)}");
        ApplyAntiCheatSettings();
        ApplyMetricsSettings();
        ApplyIplSettings();

        // Погода и время по умолчанию — только если владелец их задал; команда
        // /weather и /time администратора по-прежнему меняют их на ходу.
        var weather = _settings.Get("world.weather").Trim().ToUpperInvariant();
        if (weather.Length > 0)
        {
            if (ValidWeatherTypes.Contains(weather)) _worldWeather = weather;
            else Alt.LogWarning($"[FloV:MP] client.cfg: неизвестная погода «{weather}» — доступно: {string.Join(", ", ValidWeatherTypes)}");
        }
        var time = _settings.Get("world.time").Trim();
        if (time.Length > 0)
        {
            var hm = time.Split(':');
            if (hm.Length == 2 && int.TryParse(hm[0], out var h) && int.TryParse(hm[1], out var m) && h is >= 0 and <= 23 && m is >= 0 and <= 59)
                _worldTime = (h, m);
            else Alt.LogWarning($"[FloV:MP] client.cfg: время «{time}» — нужно ЧЧ:ММ");
        }

        if (_nativeVoice is not null) _nativeVoice.Radius = _settings.Float("voice.radius");
        var branding = _settings.CustomizedBrandingKeys().ToList();
        if (branding.Count > 0 && !FloVMP.Core.Licensing.Edition.BrandingAllowed(_license.Info))
            Alt.LogWarning($"[FloV:MP] client.cfg: {string.Join(", ", branding)} — свой бренд доступен только с Source Kit; игрокам уходит оформление FloV:MP");

        if (!broadcast) return;
        foreach (var p in _nativePlayers.Values)
            SendClientSettings(((NativePlayerProxy)(object)p).Session);
        if (_worldWeather is not null) EmitAllClients("starter:setWeather", _worldWeather);
        if (_worldTime is { } wt) EmitAllClients("starter:setTime", wt.Hour, wt.Minute);
        Alt.Log($"[Console] Настройки перечитаны и отправлены игрокам ({_nativePlayers.Count}).");
    }

    /// <summary>
    /// flovmp:settings:set (ключ, значение) — любая настройка client.cfg из
    /// кода геймода. Всё, что платформа делает «от себя», владелец сервера
    /// может поменять, не трогая платформу: в файле или своим кодом.
    /// </summary>
    private void OnSettingSet(string key, string value)
    {
        if (!_settings.TrySet(key, value, out var error))
        {
            Alt.LogWarning($"[FloV:MP] flovmp:settings:set {key} = «{value}»: {error}");
            return;
        }
        _settingOverrides[key] = value;
        Alt.Log($"[FloV:MP] Ресурс изменил настройку: {key} = {_settings.Get(key)}");
        ApplyAntiCheatSettings();
        ApplyMetricsSettings();
        ApplyIplSettings();
        if (ServerSettings.IsClientKey(key))
            foreach (var p in _nativePlayers.Values)
                SendClientSettings(((NativePlayerProxy)(object)p).Session);
    }

    /// <summary>Клиентские настройки одним сообщением: CFG ключ значение ключ значение ...</summary>
    private void SendClientSettings(NativeSession session)
    {
        var branding = FloVMP.Core.Licensing.Edition.BrandingAllowed(_license.Info);
        var fields = _settings.ClientValues(branding).SelectMany(kv => new object?[] { kv.Key, kv.Value }).ToArray();
        session.Send("CFG", fields);
    }

    private (Position Position, float Heading) NextSpawn()
    {
        if (_spawnOverridden) return (_spawnPosition, _spawnHeading);
        var points = _settings.SpawnPoints();
        if (points.Count == 0) return (_spawnPosition, _spawnHeading);
        var p = points[Random.Shared.Next(points.Count)];
        return (new Position(p.X, p.Y, p.Z), p.Heading);
    }

    /// <summary>Как игрок подписан в чате: «Nick Name (12)».</summary>
    private string ChatTag(AltV.Net.Elements.Entities.IPlayer player)
    {
        var name = _settings.Bool("nametags.underscore_to_space") ? player.Name.Replace('_', ' ') : player.Name;
        return $"{name} ({player.Id})";
    }

    private uint SpawnModel()
    {
        var model = _settings.Get("spawn.model").Trim();
        return model.Length == 0 ? DefaultPlayerModel : Alt.Hash(model.ToLowerInvariant());
    }
}
