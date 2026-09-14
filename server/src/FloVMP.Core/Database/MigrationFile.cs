using System.Security.Cryptography;
using System.Text;

namespace FloVMP.Core.Database;

/// <summary>
/// Разобранный файл миграции: <c>NNN_описание.sql</c>.
/// </summary>
public sealed class MigrationFile
{
    public string Name { get; }
    public int Version { get; }
    public string Sql { get; }
    public string Checksum { get; }
    public IReadOnlyList<string> Statements { get; }

    /// <summary>
    /// Коды ошибок MySQL/MariaDB, которые для этой миграции не считаются
    /// провалом (директива <c>-- flovmp:ignore-errors 1060,1061</c>).
    /// Нужны для баз, поднятых схемой вручную ДО появления системы миграций:
    /// там «колонка уже существует» — нормальное, ожидаемое состояние.
    /// </summary>
    public IReadOnlySet<int> IgnoredErrors { get; }

    private MigrationFile(string name, int version, string sql, string checksum,
                          IReadOnlyList<string> statements, IReadOnlySet<int> ignored)
    {
        Name = name;
        Version = version;
        Sql = sql;
        Checksum = checksum;
        Statements = statements;
        IgnoredErrors = ignored;
    }

    public static MigrationFile Parse(string fileName, string content)
    {
        // Перевод строк нормализуется ДО подсчёта контрольной суммы: репозиторий
        // может выехать с CRLF на Windows и с LF на Linux-VDS, а миграция при
        // этом одна и та же. Иначе каждый клиент видел бы ложный «дрейф схемы».
        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();

        var version = 0;
        var digits = new string(fileName.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length > 0) int.TryParse(digits, out version);

        var ignored = new HashSet<int>();
        foreach (var line in normalized.Split('\n'))
        {
            var idx = line.IndexOf("flovmp:ignore-errors", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            foreach (var part in line[(idx + "flovmp:ignore-errors".Length)..].Split(',', ' ', '\t'))
                if (int.TryParse(part.Trim(), out var code)) ignored.Add(code);
        }

        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        var statements = SqlScriptSplitter.Split(normalized);

        return new MigrationFile(fileName, version, normalized, checksum, statements, ignored);
    }

    public static MigrationFile Load(string path)
        => Parse(Path.GetFileName(path), File.ReadAllText(path));

    /// <summary>
    /// Все <c>*.sql</c> каталога в порядке номера, затем имени. Сортировка
    /// строго по числовому префиксу, а не лексикографически: иначе 010 ушла бы
    /// перед 9 и схема накатилась бы в неправильном порядке.
    /// </summary>
    public static IReadOnlyList<MigrationFile> LoadAll(string directory)
    {
        if (!Directory.Exists(directory)) return Array.Empty<MigrationFile>();
        return Directory.GetFiles(directory, "*.sql")
            .Select(Load)
            .OrderBy(m => m.Version)
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ToList();
    }
}
