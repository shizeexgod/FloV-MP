using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FloVMP.Core.Mods;

/// <summary>Файл мода в списке: путь внутри папки mods игры, размер, SHA-256.</summary>
public sealed record ModFile(string Path, long Size, string Sha256);

/// <summary>
/// Список модов сервера (пункт 4 roadmap): что игрок должен иметь в папке
/// mods игры, чтобы видеть мир сервера (карта, машины, здания).
///
/// Владелец кладёт файлы в server/mods той же раскладкой, что у GTA\mods.
/// Сервер один раз считает SHA-256 каждого файла (дальше — кэш по размеру и
/// времени изменения: карта весит гигабайты) и отдаёт список клиенту. По
/// списку клиент докачивает только то, чего у него нет или что изменилось.
///
/// Раздаются только данные игры. Исполняемые файлы (.asi, .dll, .exe,
/// скрипты) в список не попадают никогда: иначе любой сервер мог бы
/// запустить свой код на компьютере игрока.
/// </summary>
public sealed class ModManifest
{
    /// <summary>Что можно раздавать: архивы и ресурсы движка RAGE, тексты и звук.</summary>
    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".rpf", ".ytd", ".ydr", ".ydd", ".yft", ".ybn", ".ymap", ".ytyp", ".ymt", ".ymf", ".ycd", ".ynv", ".ynd",
        ".ypt", ".ypdb", ".yld", ".ysc", ".awc", ".rel", ".gxt2", ".meta", ".xml", ".dat", ".dat151", ".dat54",
        ".dat4", ".dat22", ".dat10", ".nametable", ".txt", ".json", ".ide", ".ipl", ".cut", ".png", ".jpg",
    };

    /// <summary>Служебные файлы проводника и систем контроля версий — пропускаем молча.</summary>
    private static readonly HashSet<string> Junk = new(StringComparer.OrdinalIgnoreCase) { "thumbs.db", "desktop.ini", ".ds_store" };

    public IReadOnlyList<ModFile> Files { get; }
    public long TotalSize { get; }
    /// <summary>Отпечаток всего набора: совпал у клиента — моды те же, ничего не качать.</summary>
    public string Digest { get; }
    /// <summary>Что не попало в список и почему — для журнала владельцу.</summary>
    public IReadOnlyList<string> Skipped { get; }

    private readonly Dictionary<string, ModFile> _byPath;

    private ModManifest(List<ModFile> files, List<string> skipped)
    {
        files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        Files = files;
        Skipped = skipped;
        TotalSize = files.Sum(f => f.Size);
        _byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        Digest = ComputeDigest(files);
    }

    public static ModManifest Empty { get; } = new(new List<ModFile>(), new List<string>());

    public bool TryGet(string path, out ModFile file) => _byPath.TryGetValue(path, out file!);

    public static string ComputeDigest(IEnumerable<ModFile> files)
    {
        var sb = new StringBuilder();
        foreach (var f in files.OrderBy(f => f.Path, StringComparer.Ordinal))
            sb.Append(f.Path).Append('\t').Append(f.Size).Append('\t').Append(f.Sha256).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    /// <summary>
    /// Путь внутри mods допустим: относительный, прямые «/», без «..», без
    /// управляющих символов и двоеточий (диск, NTFS-потоки), не длиннее 240.
    /// Проверяет и сервер при сборке, и клиент при приёме списка.
    /// </summary>
    public static bool ValidPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 240 || path[0] == '/' || path.EndsWith('/')) return false;
        foreach (var ch in path)
            if (ch < 0x20 || ch == '\\' || ch == ':' || ch == '*' || ch == '?' || ch == '"' || ch == '<' || ch == '>' || ch == '|') return false;
        foreach (var part in path.Split('/'))
            if (part.Length == 0 || part == "." || part == ".." || part.EndsWith('.') || part.EndsWith(' ')) return false;
        return AllowedExtensions.Contains(System.IO.Path.GetExtension(path));
    }

    /// <summary>Собрать список по папке. Кэш хэшей (может быть null) обновляется.</summary>
    public static ModManifest Build(string root, ModHashCache? cache = null, CancellationToken stop = default)
    {
        var files = new List<ModFile>();
        var skipped = new List<string>();
        if (!Directory.Exists(root)) return new ModManifest(files, skipped);
        var full = System.IO.Path.GetFullPath(root);
        foreach (var file in Directory.EnumerateFiles(full, "*", new EnumerationOptions
                 { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint }))
        {
            stop.ThrowIfCancellationRequested();
            var rel = System.IO.Path.GetRelativePath(full, file).Replace('\\', '/');
            if (Junk.Contains(System.IO.Path.GetFileName(rel)) || rel.Split('/').Any(p => p.StartsWith('.'))) continue;
            if (!ValidPath(rel))
            {
                skipped.Add(AllowedExtensions.Contains(System.IO.Path.GetExtension(rel))
                    ? $"{rel}: недопустимое имя (двоеточие, «..», пробел или точка в конце, длиннее 240)"
                    : $"{rel}: такой тип файла не раздаётся (только данные игры — .rpf, .ytd, .ymap, .meta и т. п.)");
                continue;
            }
            var info = new FileInfo(file);
            var sha = cache?.Get(rel, info.Length, info.LastWriteTimeUtc.Ticks) ?? HashFile(file, stop);
            cache?.Put(rel, info.Length, info.LastWriteTimeUtc.Ticks, sha);
            files.Add(new ModFile(rel, info.Length, sha));
        }
        cache?.Retain(files.Select(f => f.Path));
        return new ModManifest(files, skipped);
    }

    private static string HashFile(string path, CancellationToken stop)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var buffer = new byte[1 << 20];
        int n;
        while ((n = fs.Read(buffer, 0, buffer.Length)) > 0)
        {
            stop.ThrowIfCancellationRequested();
            sha.TransformBlock(buffer, 0, n, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    public string ToJson() => JsonSerializer.Serialize(new
    {
        format = 1,
        digest = Digest,
        totalSize = TotalSize,
        files = Files.Select(f => new { path = f.Path, size = f.Size, sha256 = f.Sha256 }),
    });

    /// <summary>Разбор списка на стороне загрузчика (клиентские инструменты и тесты).</summary>
    public static ModManifest? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.GetProperty("format").GetInt32() != 1) return null;
            var files = new List<ModFile>();
            foreach (var f in r.GetProperty("files").EnumerateArray())
            {
                var path = f.GetProperty("path").GetString() ?? "";
                var sha = f.GetProperty("sha256").GetString() ?? "";
                var size = f.GetProperty("size").GetInt64();
                if (!ValidPath(path) || size < 0 || sha.Length != 64 || !sha.All(Uri.IsHexDigit)) return null;
                files.Add(new ModFile(path, size, sha.ToLowerInvariant()));
            }
            var m = new ModManifest(files, new List<string>());
            return m.Digest == r.GetProperty("digest").GetString() ? m : null;
        }
        catch { return null; }
    }
}

/// <summary>
/// SHA-256 файлов модов между запусками (flovmp-data/mods-hashes.json): файл
/// с тем же размером и временем изменения не пересчитывается. Без этого
/// каждый старт сервера с картой на 10 ГБ читал бы все 10 ГБ.
/// </summary>
public sealed class ModHashCache
{
    private readonly Dictionary<string, (long Size, long Ticks, string Sha)> _map = new(StringComparer.Ordinal);
    private readonly string? _path;
    private bool _dirty;

    public ModHashCache(string? path)
    {
        _path = path;
        if (path is null || !File.Exists(path)) return;
        try
        {
            foreach (var e in JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path)) ?? new())
                if (e.Path is not null && e.Sha is { Length: 64 }) _map[e.Path] = (e.Size, e.Ticks, e.Sha);
        }
        catch { _map.Clear(); }   // испорченный кэш — просто пересчитаем
    }

    private sealed record Entry(string? Path, long Size, long Ticks, string? Sha);

    public string? Get(string path, long size, long ticks) =>
        _map.TryGetValue(path, out var e) && e.Size == size && e.Ticks == ticks ? e.Sha : null;

    public void Put(string path, long size, long ticks, string sha)
    {
        if (_map.TryGetValue(path, out var e) && e == (size, ticks, sha)) return;
        _map[path] = (size, ticks, sha);
        _dirty = true;
    }

    public void Retain(IEnumerable<string> paths)
    {
        var keep = new HashSet<string>(paths, StringComparer.Ordinal);
        foreach (var k in _map.Keys.Where(k => !keep.Contains(k)).ToList()) { _map.Remove(k); _dirty = true; }
    }

    public void Save()
    {
        if (_path is null || !_dirty) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(_map.Select(kv => new Entry(kv.Key, kv.Value.Size, kv.Value.Ticks, kv.Value.Sha))));
        File.Move(_path + ".tmp", _path, overwrite: true);
        _dirty = false;
    }
}
