using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace FloVMP.ServerHost;

/// <summary>
/// Движок сервера (модуль C#) передаёт пути в однобайтовой кодировке: из
/// папки с кириллицей в пути («C:\Проекты\...», «Рабочий стол») сервер падает
/// при загрузке ресурса — .NET не находит DLL по искажённому пути.
///
/// Решение без переноса файлов: ссылка-junction с латинским путём в
/// C:\ProgramData\FloVMP\run\&lt;id&gt;, указывающая на папку сервера. Сервер
/// запускается через неё. Прав администратора junction не требует.
/// </summary>
internal static class AsciiPath
{
    public static bool IsAscii(string path) => path.All(c => c < 128);

    /// <summary>Латинский путь к той же папке, либо null с причиной.</summary>
    public static string? Resolve(string root, out string? error)
    {
        error = null;
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (IsAscii(root)) return root;

        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FloVMP", "run");
        if (!IsAscii(baseDir))
        {
            error = "системная папка ProgramData содержит не-латинские символы";
            return null;
        }

        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToLowerInvariant())))[..12];
        var link = Path.Combine(baseDir, id);

        try
        {
            Directory.CreateDirectory(baseDir);
            if (Directory.Exists(link))
            {
                var target = new DirectoryInfo(link).LinkTarget;
                if (target is not null &&
                    string.Equals(Path.TrimEndingDirectorySeparator(target), root, StringComparison.OrdinalIgnoreCase))
                    return link;
                // Ссылка ведёт не туда (папку сервера перенесли) или это не ссылка.
                if (target is null)
                {
                    error = $"{link} занят обычной папкой";
                    return null;
                }
                Directory.Delete(link);
            }

            var psi = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("mklink");
            psi.ArgumentList.Add("/J");
            psi.ArgumentList.Add(link);
            psi.ArgumentList.Add(root);
            using var proc = Process.Start(psi)!;
            proc.WaitForExit(10_000);
            if (proc.ExitCode != 0 || !Directory.Exists(link))
            {
                error = "не удалось создать ссылку (mklink /J): " + proc.StandardError.ReadToEnd().Trim();
                return null;
            }
            return link;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }
}
