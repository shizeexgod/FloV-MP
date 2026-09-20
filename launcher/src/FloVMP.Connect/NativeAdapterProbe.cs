using System.Runtime.InteropServices;

namespace FloVMP.Connect;

public sealed record NativeAdapterProbeResult(
    bool Found,
    bool FingerprintValid,
    bool RuntimeBound,
    string Code,
    string Message);

/// <summary>
/// Loads a version-specific native adapter only for preflight. The adapter is
/// never treated as supported merely because the DLL exists: it must validate
/// the game and report that its runtime binding is active.
/// </summary>
public static class NativeAdapterProbe
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ValidateGameDelegate([MarshalAs(UnmanagedType.LPWStr)] string gameDirectory);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int IsRuntimeBoundDelegate();

    public static NativeAdapterProbeResult Probe(string? adapterPath, string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(adapterPath) || !File.Exists(adapterPath))
        {
            return new(false, false, false, "adapter-missing",
                "Native adapter DLL не найден.");
        }

        nint library = 0;
        try
        {
            library = NativeLibrary.Load(Path.GetFullPath(adapterPath));
            if (!NativeLibrary.TryGetExport(library, "FlovMpLegacy3889_ValidateGame", out var validatePtr) ||
                !NativeLibrary.TryGetExport(library, "FlovMpLegacy3889_IsRuntimeBound", out var boundPtr))
            {
                return new(true, false, false, "adapter-abi-invalid",
                    "Native adapter найден, но его ABI не содержит обязательные проверки.");
            }

            var validate = Marshal.GetDelegateForFunctionPointer<ValidateGameDelegate>(validatePtr);
            var isBound = Marshal.GetDelegateForFunctionPointer<IsRuntimeBoundDelegate>(boundPtr);
            var fingerprintValid = validate(gameDirectory) == 1;
            var runtimeBound = isBound() == 1;
            if (!fingerprintValid)
            {
                return new(true, false, runtimeBound, "adapter-game-mismatch",
                    "Native adapter отклонил файлы GTA: установленный профиль не совпадает с b3889.");
            }

            if (!runtimeBound)
            {
                return new(true, true, false, "adapter-runtime-not-bound",
                    "Native adapter проверяет b3889, но client runtime ещё не привязан к процессу GTA.");
            }

            return new(true, true, true, "adapter-bound",
                "Native adapter сообщил активную привязку client runtime.");
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            return new(true, false, false, "adapter-load-failed",
                $"Native adapter не удалось загрузить: {ex.Message}");
        }
        finally
        {
            if (library != 0) NativeLibrary.Free(library);
        }
    }
}
