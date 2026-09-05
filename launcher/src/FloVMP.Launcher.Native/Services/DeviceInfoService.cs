using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace FloVMP.Launcher.Native.Services;

/// <summary>
/// Реальные данные о текущем устройстве для «Личного кабинета» (вкладки
/// «Устройства» и «История входов»). Ничего не выдумываем: только то, что
/// система отдаёт локально. Публичный IP и историю входов с других устройств
/// лаунчер сам знать не может — это ведётся на сервере, поэтому эти поля
/// возвращаются как есть (localIp — адрес в локальной сети) и UI честно
/// подписывает их «локальный адрес» / «полная история — на сервере».
/// </summary>
public static class DeviceInfoService
{
    public static object Collect()
    {
        return new
        {
            deviceId = StableDeviceId(),
            hostname = SafeHostName(),
            userName = Environment.UserName,
            os = RuntimeInformation.OSDescription,
            osArch = RuntimeInformation.OSArchitecture.ToString(),
            localIp = LocalIpv4() ?? "недоступен",
            bootTimeUtc = BootTimeUtc()?.ToString("o"),
            nowUtc = DateTime.UtcNow.ToString("o"),
        };
    }

    private static string SafeHostName()
    {
        try { return Dns.GetHostName(); }
        catch { return Environment.MachineName; }
    }

    /// <summary>
    /// Стабильный, но неперсональный идентификатор машины: SHA-256 от
    /// MachineGuid (или имени машины как запасного варианта), первые 12 hex.
    /// Нужен, чтобы отличать «это устройство» от чужих в списке, не светя
    /// сам GUID.
    /// </summary>
    private static string StableDeviceId()
    {
        string seed;
        try
        {
            using var key = RegistryKey
                .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            seed = key?.GetValue("MachineGuid") as string ?? Environment.MachineName;
        }
        catch { seed = Environment.MachineName; }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    private static string? LocalIpv4()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(addr.Address)) continue;
                    return addr.Address.ToString();
                }
            }
        }
        catch { }
        return null;
    }

    private static DateTime? BootTimeUtc()
    {
        try { return DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64); }
        catch { return null; }
    }
}
