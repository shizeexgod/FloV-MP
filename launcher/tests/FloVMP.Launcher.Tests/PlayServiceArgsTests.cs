using System.Runtime.InteropServices;
using FloVMP.Launcher.Native.Services;
using Xunit;

namespace FloVMP.Launcher.Tests;

/// <summary>
/// Аргументы запуска коннектора. Раньше значения оборачивались в кавычки как
/// есть: путь или ник с «\» в конце экранировал кавычку, и аргументы съезжали.
/// Проверяем разбором самой Windows (CommandLineToArgvW), а не своими ожиданиями.
/// </summary>
public class PlayServiceArgsTests
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string cmdLine, out int numArgs);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private static string[] ParseByWindows(string args)
    {
        var ptr = CommandLineToArgvW("app.exe " + args, out var n);
        try
        {
            var result = new string[n - 1];
            for (var i = 1; i < n; i++)
                result[i - 1] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr, i * IntPtr.Size))!;
            return result;
        }
        finally { LocalFree(ptr); }
    }

    [Theory]
    [InlineData(@"C:\Games\Grand Theft Auto V\")]
    [InlineData(@"D:\GTA V")]
    [InlineData("Ник с пробелом")]
    [InlineData(@"nick\")]
    [InlineData("a\"b")]
    [InlineData(@"a\\""b\")]
    [InlineData("")]
    [InlineData("plain")]
    public void QuoteArg_RoundTripsThroughWindowsParser(string value)
    {
        if (!OperatingSystem.IsWindows()) return;

        var line = "--gta " + PlayService.QuoteArg(value) + " --nick " + PlayService.QuoteArg("Player");
        var parsed = ParseByWindows(line);

        Assert.Equal(new[] { "--gta", value, "--nick", "Player" }, parsed);
    }
}
