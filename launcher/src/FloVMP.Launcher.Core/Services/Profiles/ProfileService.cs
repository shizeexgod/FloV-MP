using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FloVMP.Launcher.Services.Cdn;
using FloVMP.Launcher.Services.Compat;

namespace FloVMP.Launcher.Services.Profiles;

public sealed class ProfileService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<ClientProfile> LoadFromDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return Array.Empty<ClientProfile>();

        var list = new List<ClientProfile>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            var profile = LoadFromFile(file);
            if (profile is not null) list.Add(profile);
        }
        return list;
    }

    public ClientProfile? LoadFromFile(string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<ClientProfile>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public async Task<ProfileValidationResult> EvaluateFolderAsync(
        IReadOnlyList<ClientProfile> profiles,
        string gtaFolder,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gtaFolder) || !Directory.Exists(gtaFolder))
            return new ProfileValidationResult(false, false, new[] { "Папка GTA V не существует или не указана." });

        var exeName = GtaLocator.FindGameExecutable(gtaFolder);
        if (exeName is null)
            return new ProfileValidationResult(false, false, new[] { "GTA V executable не найден (ожидался GTA5.exe или GTA5_Enhanced.exe)." });

        var exePath = Path.Combine(gtaFolder, exeName);
        var fv = FileVersionInfo.GetVersionInfo(exePath).FileVersion?.Trim() ?? "";
        var isEnhanced = string.Equals(exeName, "GTA5_Enhanced.exe", StringComparison.OrdinalIgnoreCase);
        var edition = isEnhanced ? GtaEdition.Enhanced : GtaEdition.Legacy;

        // Поиск подходящего профиля
        var candidate = profiles.FirstOrDefault(p =>
            p.Edition == edition &&
            string.Equals(p.GameExecutable, exeName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.GameFileVersion, fv, StringComparison.OrdinalIgnoreCase))
            ?? profiles.FirstOrDefault(p => p.Edition == edition);

        if (candidate is null)
            return new ProfileValidationResult(false, false, new[] { $"Не найден профиль для {edition} (версия {fv})." });

        var issues = new List<string>();
        var hashesValid = true;

        if (!string.IsNullOrWhiteSpace(candidate.GameSha256))
        {
            var actualExeHash = await ContentHasher.Sha256FileAsync(exePath, ct);
            if (!string.Equals(candidate.GameSha256, actualExeHash, StringComparison.OrdinalIgnoreCase))
            {
                hashesValid = false;
                issues.Add($"Не совпадает SHA-256 {exeName}: ожидался {candidate.GameSha256}, получен {actualExeHash}");
            }
        }

        var update1 = Path.Combine(gtaFolder, "update", "update.rpf");
        if (!string.IsNullOrWhiteSpace(candidate.UpdateRpfSha256))
        {
            if (!File.Exists(update1))
            {
                hashesValid = false;
                issues.Add("Отсутствует update/update.rpf");
            }
            else
            {
                var h1 = await ContentHasher.Sha256FileAsync(update1, ct);
                if (!string.Equals(candidate.UpdateRpfSha256, h1, StringComparison.OrdinalIgnoreCase))
                {
                    hashesValid = false;
                    issues.Add($"Не совпадает SHA-256 update.rpf: ожидался {candidate.UpdateRpfSha256}, получен {h1}");
                }
            }
        }

        var update2 = Path.Combine(gtaFolder, "update", "update2.rpf");
        if (!string.IsNullOrWhiteSpace(candidate.Update2RpfSha256))
        {
            if (!File.Exists(update2))
            {
                hashesValid = false;
                issues.Add("Отсутствует update/update2.rpf");
            }
            else
            {
                var h2 = await ContentHasher.Sha256FileAsync(update2, ct);
                if (!string.Equals(candidate.Update2RpfSha256, h2, StringComparison.OrdinalIgnoreCase))
                {
                    hashesValid = false;
                    issues.Add($"Не совпадает SHA-256 update2.rpf: ожидался {candidate.Update2RpfSha256}, получен {h2}");
                }
            }
        }

        return new ProfileValidationResult(
            IsMatch: true,
            AllHashesValid: hashesValid,
            Issues: issues,
            MatchedProfile: candidate);
    }
}
