using System.Security.Cryptography;
using System.Text;
using FloVMP.LicenseAuthority;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class ReleasePublisherTests
{
    [Fact]
    public void Signed_release_is_accepted_and_changed_manifest_is_rejected()
    {
        var folder = NewFolder();
        try
        {
            using var key = RSA.Create(2048);
            CreateRelease(folder, key);
            Assert.Equal("1.0.10-beta", ReleasePublisher.Validate(folder, key));
            File.AppendAllText(Path.Combine(folder, "release-linux.txt"), "extra=untrusted\n");
            Assert.Throws<InvalidDataException>(() => ReleasePublisher.Validate(folder, key));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void Valid_signature_does_not_hide_modified_archive()
    {
        var folder = NewFolder();
        try
        {
            using var key = RSA.Create(2048);
            CreateRelease(folder, key);
            File.AppendAllText(Path.Combine(folder, "flovmp-server-1.0.10-beta-linux.tar.gz"), "tampered");
            Assert.Throws<InvalidDataException>(() => ReleasePublisher.Validate(folder, key));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void Different_validly_signed_versions_cannot_be_combined()
    {
        var folder = NewFolder();
        try
        {
            using var key = RSA.Create(2048);
            CreateRelease(folder, key);
            WritePlatform(folder, key, "windows", "1.0.11-beta");
            Assert.Throws<InvalidDataException>(() => ReleasePublisher.Validate(folder, key));
        }
        finally { Directory.Delete(folder, true); }
    }

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "flovmp-release-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void CreateRelease(string folder, RSA key)
    {
        WritePlatform(folder, key, "linux", "1.0.10-beta");
        WritePlatform(folder, key, "windows", "1.0.10-beta");
    }

    private static void WritePlatform(string folder, RSA key, string os, string version)
    {
        var file = $"flovmp-server-{version}-{os}" + (os == "linux" ? ".tar.gz" : ".zip");
        var archive = Encoding.UTF8.GetBytes("package for " + os);
        File.WriteAllBytes(Path.Combine(folder, file), archive);
        var manifest = $"format=1\nproduct=flovmp-server\nversion={version}\nos={os}\nfile={file}\nsha256={Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant()}\nsize={archive.Length}\n";
        var bytes = Encoding.UTF8.GetBytes(manifest);
        var path = Path.Combine(folder, $"release-{os}.txt");
        File.WriteAllBytes(path, bytes);
        File.WriteAllText(path + ".sig", Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) + "\n");
    }
}
