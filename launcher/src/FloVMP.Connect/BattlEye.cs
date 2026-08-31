using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace FloVMP.Connect;

/// <summary>
/// BattlEye + alt:V. GTA V Legacy (СЃРІРµР¶РёРµ Р±РёР»РґС‹) С‚СЂРµР±СѓРµС‚ BE РѕР±СЏР·Р°С‚РµР»СЊРЅРѕ.
/// РњС‹ РќРР§Р•Р“Рћ РЅРµ С‚СЂРѕРіР°РµРј РІ С„Р°Р№Р»Р°С… BE Рё РќР• РіР»СѓС€РёРј СЃР»СѓР¶Р±Сѓ (РїСЂРѕРІРµСЂРµРЅРѕ вЂ” РЅРµ
/// РїРѕРјРѕРіР°РµС‚: Р·Р°С‰РёС‚Р° РІ kernel-РґСЂР°Р№РІРµСЂРµ, Р° РїРѕР»РѕРјРєР° СЃР»СѓР¶Р±С‹ Р»РѕРјР°РµС‚ Р·Р°РїСѓСЃРє).
///
/// Р•РґРёРЅСЃС‚РІРµРЅРЅС‹Р№ РЅР°РґС‘Р¶РЅС‹Р№ СЃРїРѕСЃРѕР±: РѕС‚РєР»СЋС‡РёС‚СЊ BattlEye РІ РЅР°СЃС‚СЂРѕР№РєР°С…
/// Rockstar Games Launcher (РќР°СЃС‚СЂРѕР№РєРё в†’ Grand Theft Auto V в†’ BattlEye).
/// РўРѕРіРґР° GTA5.exe РЅРµ РїРµСЂРµР·Р°РїСѓСЃРєР°РµС‚ СЃРµР±СЏ РїРѕРґ BE Рё alt:V СѓСЃРїРµРІР°РµС‚ РїСЂРѕРїР°С‚С‡РёС‚СЊ.
///
/// Р—РґРµСЃСЊ вЂ” С‚РѕР»СЊРєРѕ СЃРѕРІРµС‚ РїРѕР»СЊР·РѕРІР°С‚РµР»СЋ + СЂР°Р·РѕРІРѕРµ РІРѕСЃСЃС‚Р°РЅРѕРІР»РµРЅРёРµ СЃР»СѓР¶Р±С‹
/// BEService, РµСЃР»Рё РµС‘ СЃР»РѕРјР°Р» РїСЂРѕС€Р»С‹Р№ (РѕС€РёР±РѕС‡РЅС‹Р№) Р·Р°С…РѕРґ РєРѕРЅРЅРµРєС‚РѕСЂР°.
/// </summary>
public static class BattlEye
{
    private const string Service = "BEService";

    public static bool IsPresent(string gtaDir) =>
        File.Exists(Path.Combine(gtaDir, "GTA5_BE.exe"));

    public static bool IsAdmin()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>Р’РµСЂРЅСѓС‚СЊ СЃР»СѓР¶Р±Сѓ BEService РІ Manual, РµСЃР»Рё РѕРЅР° РѕСЃС‚Р°Р»Р°СЃСЊ Disabled.</summary>
    [SupportedOSPlatform("windows")]
    public static void RepairServiceIfBroken()
    {
        try
        {
            var q = new ProcessStartInfo("sc.exe", $"qc {Service}")
            {
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
            };
            using var p = Process.Start(q);
            var outp = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(5000);
            if (outp.Contains("DISABLED", StringComparison.OrdinalIgnoreCase))
            {
                Run("sc.exe", $"config {Service} start= demand");
                Console.WriteLine("[be] СЃР»СѓР¶Р±Р° BEService РІРѕСЃСЃС‚Р°РЅРѕРІР»РµРЅР° (Manual)");
            }
        }
        catch { /* РЅРµ РєСЂРёС‚РёС‡РЅРѕ */ }
    }

        public static void Advise(string gtaDir)
    {
        if (!IsPresent(gtaDir)) return;
        Console.WriteLine("[connect] Напоминание: Убедитесь, что BattlEye отключен в Rockstar Launcher (если игра не запускается).");
    }

    private static void Run(string exe, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            });
            p?.WaitForExit(8000);
        }
        catch { }
    }
}

