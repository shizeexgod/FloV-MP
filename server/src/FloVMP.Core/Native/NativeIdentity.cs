using System.Security.Cryptography;

namespace FloVMP.Core.Native;

/// <summary>
/// Личность нативного клиента — пара ключей ECDSA P-256, созданная на его
/// компьютере (закрытый ключ не покидает машину, хранится под DPAPI).
///
/// Сервер при входе присылает случайный nonce, клиент его подписывает.
/// Поэтому ID игрока нельзя присвоить, просто узнав его: права
/// администратора и баны привязаны к ключу, а не к нику или числу,
/// которое клиент говорит о себе сам.
///
/// Публичный ключ — 64 байта X||Y (формат BCRYPT_ECCPUBLIC_BLOB без заголовка),
/// подпись — 64 байта r||s (IEEE P1363), как их отдаёт Windows CNG.
/// </summary>
public static class NativeIdentity
{
    /// <summary>Старший бит: ID нативных клиентов не пересекаются с числами Social Club.</summary>
    public const ulong IdentityFlag = 0x8000_0000_0000_0000UL;

    public static bool IsValidPublicKey(byte[] key)
    {
        if (key.Length != 64) return false;
        try
        {
            using var ecdsa = Import(key);
            return true;
        }
        catch (CryptographicException) { return false; }
    }

    public static bool Verify(byte[] publicKey, byte[] nonce, string signatureB64)
    {
        try
        {
            var signature = Convert.FromBase64String(signatureB64);
            if (signature.Length != 64) return false;
            using var ecdsa = Import(publicKey);
            return ecdsa.VerifyData(NativeProtocol.AuthMessage(nonce, publicKey), signature,
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (FormatException) { return false; }
        catch (CryptographicException) { return false; }
    }

    public static ulong IdFor(byte[] publicKey)
    {
        var hash = SHA256.HashData(publicKey);
        return IdentityFlag | (BitConverter.ToUInt64(hash, 0) & ~IdentityFlag);
    }

    public static bool IsNativeIdentity(ulong id) => (id & IdentityFlag) != 0;

    private static ECDsa Import(byte[] key)
    {
        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = key[..32], Y = key[32..] },
        };
        parameters.Validate();
        return ECDsa.Create(parameters);
    }
}
