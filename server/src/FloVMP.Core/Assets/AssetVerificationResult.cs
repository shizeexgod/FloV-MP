using System.Collections.Generic;

namespace FloVMP.Core.Assets;

public sealed record HashMismatch(string Path, string ExpectedSha256, string ActualSha256);

/// <summary>
/// Результат проверки целостности клиентских файлов по манифесту.
/// </summary>
public sealed class AssetVerificationResult
{
    public bool IsValid => MissingFiles.Count == 0 && HashMismatches.Count == 0;
    public List<string> MissingFiles { get; set; } = new();
    public List<HashMismatch> HashMismatches { get; set; } = new();
    public List<string> UntrackedFiles { get; set; } = new();

    public string GetSummary()
    {
        if (IsValid)
        {
            return $"Проверка пройдена успешно. Все файлы ({UntrackedFiles.Count} не отслеживаемых) соответствуют манифесту.";
        }

        return $"Ошибка целостности: Отсутствует файлов: {MissingFiles.Count}, Несовпадений хэша: {HashMismatches.Count}";
    }
}
