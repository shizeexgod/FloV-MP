using System.Text.Json;
using FloVMP.Core.Native;

namespace FloVMP.Starter;

/// <summary>
/// Подтверждение лицензии для клиента игрока (LIC). Клиент FloV:MP при входе
/// проверяет подпись сервера лицензий, срок и то, что подключился к той самой
/// машине (IP в аренде), — иначе не заходит. Так ключ, слитый на чужой сервер,
/// копия папки сервера на другой машине или вырезанная из сервера проверка
/// лицензии не дают играть официальным клиентом: подделать подпись сервера
/// лицензий нельзя.
/// </summary>
public partial class StarterResource
{
    // ВАЖНО: игрокам уходит только подтверждение (attest) — в нём нет ключа
    // лицензии. Аренду (license.lease) с ключом клиентам не отдавать никогда:
    // любой игрок вытащил бы ключ сервера.
    private static string AttestPath(string leasePath) => Path.Combine(Path.GetDirectoryName(leasePath) ?? ".", "license.attest");

    private static void SaveAttestation(string leasePath, string payload, string signature)
    {
        try
        {
            var path = AttestPath(leasePath);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new { payload_b64 = payload, signature }));
            File.Move(path + ".tmp", path, true);
        }
        catch (Exception ex) { AltV.Net.Alt.LogWarning($"[FloV:MP] [License] не удалось сохранить подтверждение для клиентов: {ex.Message}"); }
    }

    /// <summary>Подтверждение для клиентов: свежая проверка или файл license.attest.</summary>
    private (string Payload, string Signature)? CurrentLease()
    {
        var remote = _licenseRemoteResult;
        if (remote is { Valid: true, AttestPayloadB64: { } p, AttestSignatureB64: { } s }) return (p, s);
        try
        {
            var path = AttestPath(FloVMP.Core.Licensing.LicenseLeaseCache.PathFor(FloVMP.Core.Licensing.LicenseFile.Locate()));
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var payload = doc.RootElement.GetProperty("payload_b64").GetString();
            var signature = doc.RootElement.GetProperty("signature").GetString();
            return payload is null || signature is null ? null : (payload, signature);
        }
        catch
        {
            return null;
        }
    }

    private void SendLicenseAttestation(NativeSession session)
    {
        if (CurrentLease() is { } lease) session.Send("LIC", lease.Payload, lease.Signature);
    }
}
