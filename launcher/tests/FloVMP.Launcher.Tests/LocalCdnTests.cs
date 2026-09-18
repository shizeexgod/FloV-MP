using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using FloVMP.Connect;
using Xunit;

namespace FloVMP.Launcher.Tests;

/// <summary>
/// Контракт native backup-манфиеста отличается от обычного CDN update.json.
/// Если вернуть 404 или общий манифест без <c>files</c>, закрытый launcher
/// завершает запуск до GTA с 0x30,033.
/// </summary>
public sealed class LocalCdnTests
{
    [Fact]
    public async Task BackupEndpoint_ReturnsNativeEmptyFilesManifest()
    {
        var port = ReserveLoopbackPort();
        var clientDir = Path.Combine(Path.GetTempPath(), "flovmp-cdn", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(clientDir);

        try
        {
            using var cdn = new LocalCdn(clientDir, port);
            cdn.Start();
            using var http = new HttpClient();
            var response = await http.GetAsync($"http://127.0.0.1:{port}//backup/update.json");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(document.RootElement.TryGetProperty("files", out var files));
            Assert.Equal(JsonValueKind.Array, files.ValueKind);
            Assert.Equal(0, files.GetArrayLength());
        }
        finally
        {
            try { Directory.Delete(clientDir, recursive: true); } catch { }
        }
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
