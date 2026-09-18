using System.Text.Json;

namespace FloVMP.Core.Security;

/// <summary>
/// Голосовые муты, которые переживают переподключение и перезапуск сервера.
///
/// Зачем: мут голосового канала alt:V живёт только пока игрок в сети. То есть
/// заглушённый нарушитель выходил и заходил снова — и опять кричал в
/// микрофон. Для модерации это то же самое, что не иметь мута вовсе.
///
/// Ключ — SocialClubId: ник задаёт клиент и подделывается, аккаунтов в
/// платформе нет. Файл маленький и переписывается целиком: мутов на сервере
/// единицы, а не тысячи.
/// </summary>
public sealed class VoiceMuteStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, DateTime?> _muted = new(StringComparer.Ordinal);

    public VoiceMuteStore(string path)
    {
        _path = path;
        Load();
    }

    /// <summary>Заглушить. <paramref name="until"/> = null — до снятия вручную.</summary>
    public void Mute(string socialClubId, DateTime? until)
    {
        if (string.IsNullOrWhiteSpace(socialClubId) || socialClubId == "0") return;
        lock (_lock)
        {
            _muted[socialClubId] = until;
            Save();
        }
    }

    public bool Unmute(string socialClubId)
    {
        if (string.IsNullOrWhiteSpace(socialClubId)) return false;
        lock (_lock)
        {
            if (!_muted.Remove(socialClubId)) return false;
            Save();
            return true;
        }
    }

    /// <summary>Действует ли мут сейчас. Истёкший снимается сам.</summary>
    public bool IsMuted(string socialClubId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(socialClubId) || socialClubId == "0") return false;
        lock (_lock)
        {
            if (!_muted.TryGetValue(socialClubId, out var until)) return false;
            if (until is null) return true;
            if (nowUtc < until) return true;

            _muted.Remove(socialClubId);
            Save();
            return false;
        }
    }

    /// <summary>Когда закончится мут (null — бессрочно или мута нет).</summary>
    public DateTime? MutedUntil(string socialClubId)
    {
        lock (_lock)
        {
            return _muted.TryGetValue(socialClubId, out var until) ? until : null;
        }
    }

    public int Count
    {
        get { lock (_lock) { return _muted.Count; } }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var text = File.ReadAllText(_path);
            _muted = JsonSerializer.Deserialize<Dictionary<string, DateTime?>>(text)
                     ?? new Dictionary<string, DateTime?>(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            // Битый файл не должен мешать серверу стартовать: муты — не то,
            // ради чего стоит не пускать игроков вообще.
            CoreConsole.Warning($"[FloV:MP] voice-mutes.json не прочитан ({ex.Message}) — муты голоса сброшены.");
            _muted = new Dictionary<string, DateTime?>(StringComparer.Ordinal);
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(_muted,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            CoreConsole.Warning($"[FloV:MP] voice-mutes.json не сохранён: {ex.Message}");
        }
    }
}
