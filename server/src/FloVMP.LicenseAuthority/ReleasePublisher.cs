using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FloVMP.LicenseAuthority;

/// <summary>Проверка подписанного релиза и атомарное переключение раздачи.</summary>
public static class ReleasePublisher
{
    private static readonly Regex SafeVersion = new("^[0-9A-Za-z._-]+$", RegexOptions.CultureInvariant);

    [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
    private static extern int Rename([MarshalAs(UnmanagedType.LPUTF8Str)] string oldPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);

    public static string Validate(string folder)
    {
        using var key = RSA.Create();
        using var resource = typeof(ReleasePublisher).Assembly.GetManifestResourceStream("FloVMP.ReleaseSigningPublicKey")
            ?? throw new InvalidDataException("открытый ключ релизов не встроен в службу");
        using (var reader = new StreamReader(resource)) key.ImportFromPem(reader.ReadToEnd());
        return Validate(folder, key);
    }

    internal static string Validate(string folder, RSA key)
    {
        string? version = null;
        foreach (var os in new[] { "linux", "windows" })
        {
            var manifestPath = Path.Combine(folder, $"release-{os}.txt");
            var signaturePath = manifestPath + ".sig";
            var manifest = File.ReadAllBytes(manifestPath);
            byte[] signature;
            try { signature = Convert.FromBase64String(File.ReadAllText(signaturePath).Trim()); }
            catch (FormatException e) { throw new InvalidDataException($"неверный формат подписи {os}", e); }
            if (!key.VerifyData(manifest, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new InvalidDataException($"неверная подпись релиза {os}");

            var info = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(manifestPath))
            {
                var split = line.IndexOf('=');
                if (split <= 0 || !info.TryAdd(line[..split], line[(split + 1)..]))
                    throw new InvalidDataException($"неверная строка манифеста {os}");
            }
            if (info.GetValueOrDefault("format") != "1" || info.GetValueOrDefault("product") != "flovmp-server" ||
                info.GetValueOrDefault("os") != os)
                throw new InvalidDataException($"неверный тип релиза {os}");
            var candidate = info.GetValueOrDefault("version") ?? "";
            if (!SafeVersion.IsMatch(candidate) || candidate is "." or ".." ||
                (version is not null && version != candidate))
                throw new InvalidDataException("неверная или несовпадающая версия релиза");
            version = candidate;
            var file = info.GetValueOrDefault("file") ?? "";
            var expected = $"flovmp-server-{version}-{os}" + (os == "linux" ? ".tar.gz" : ".zip");
            if (file != expected) throw new InvalidDataException($"неверное имя архива {os}");
            var path = Path.Combine(folder, file);
            var digest = info.GetValueOrDefault("sha256") ?? "";
            if (!Regex.IsMatch(digest, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant) ||
                !long.TryParse(info.GetValueOrDefault("size"), out var size) || size <= 0 ||
                new FileInfo(path).Length != size)
                throw new InvalidDataException($"неверный размер или SHA-256 архива {os}");
            using var archive = File.OpenRead(path);
            var actual = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
            if (actual != digest) throw new InvalidDataException($"SHA-256 архива {os} не совпадает с подписанным манифестом");
        }
        return version!;
    }

    public static int Publish(string dataDirectory, string folder)
    {
        folder = Path.GetFullPath(folder);
        var version = Validate(folder); // до любых изменений текущего релиза
        var releases = Path.Combine(dataDirectory, "releases");
        Directory.CreateDirectory(releases);
        var dest = Path.Combine(releases, version + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dest);
        try
        {
            foreach (var os in new[] { "linux", "windows" })
            {
                var manifest = $"release-{os}.txt";
                var archive = $"flovmp-server-{version}-{os}" + (os == "linux" ? ".tar.gz" : ".zip");
                foreach (var name in new[] { manifest, manifest + ".sig", archive })
                    File.Copy(Path.Combine(folder, name), Path.Combine(dest, name));
            }
            var current = Path.Combine(releases, "current");
            var next = Path.Combine(releases, ".current-" + Guid.NewGuid().ToString("N"));
            Directory.CreateSymbolicLink(next, dest);
            try
            {
                if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("публикация предназначена для Linux VDS");
                // POSIX rename(2) заменяет ссылку на каталог одним атомарным шагом.
                if (Rename(next, current) != 0)
                    throw new IOException("не удалось переключить текущий релиз", new Win32Exception(Marshal.GetLastPInvokeError()));
            }
            finally { if (Directory.Exists(next)) Directory.Delete(next); }
        }
        catch
        {
            Directory.Delete(dest, recursive: true);
            throw;
        }
        Console.WriteLine($"опубликованы linux и windows {version}");
        return 0;
    }
}
