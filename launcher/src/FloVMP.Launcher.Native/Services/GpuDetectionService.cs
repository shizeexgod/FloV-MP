using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace FloVMP.Launcher.Native.Services;

/// <summary>
/// Информация о видеокарте пользователя и доступных технологиях масштабирования.
/// </summary>
public sealed class GpuInfo
{
    [JsonPropertyName("vendor")] public string Vendor { get; set; } = "Unknown";
    [JsonPropertyName("modelName")] public string ModelName { get; set; } = "Неизвестный видеоадаптер";
    [JsonPropertyName("vramMb")] public int VramMb { get; set; }
    [JsonPropertyName("driverVersion")] public string DriverVersion { get; set; } = "";
    [JsonPropertyName("supportsRtx")] public bool SupportsRtx { get; set; }
    [JsonPropertyName("supportsDlss")] public bool SupportsDlss { get; set; }
    [JsonPropertyName("supportsDlss5Neural")] public bool SupportsDlss5Neural { get; set; }
    [JsonPropertyName("supportsFsr3")] public bool SupportsFsr3 { get; set; } = true;
    [JsonPropertyName("supportsFrameGen")] public bool SupportsFrameGen { get; set; } = true;
    [JsonPropertyName("recommendedMode")] public string RecommendedMode { get; set; } = "none";
    [JsonPropertyName("recommendationReason")] public string RecommendationReason { get; set; } = "";
}

/// <summary>
/// Сервис аппаратного инспектирования графического ускорителя (GPU) пользователя.
/// Мгновенно читает конфигурацию без загрузки тяжёлых DX-рантаймов и определяет
/// доступность NVIDIA DLSS, Frame Generation и экспериментального Neural Reconstruction («DLSS 5»).
/// </summary>
public static class GpuDetectionService
{
    private const string DisplayClassGuid = @"{4d36e968-e325-11ce-bfc1-08002be10318}";

    /// <summary>
    /// Автоматическое определение установленной видеокарты через Windows Registry.
    /// </summary>
    public static GpuInfo Detect()
    {
        try
        {
            var gpus = new List<GpuInfo>();
            using var classKey = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Control\Class\{DisplayClassGuid}");

            if (classKey != null)
            {
                foreach (var subKeyName in classKey.GetSubKeyNames())
                {
                    if (subKeyName.Length != 4 || !int.TryParse(subKeyName, out _))
                        continue;

                    using var subKey = classKey.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var driverDesc = subKey.GetValue("DriverDesc") as string;
                    if (string.IsNullOrWhiteSpace(driverDesc)) continue;

                    var provider = subKey.GetValue("ProviderName") as string ?? "";
                    var driverVersion = subKey.GetValue("DriverVersion") as string ?? "";

                    long vramBytes = 0;
                    var qwMem = subKey.GetValue("HardwareInformation.qwMemorySize");
                    if (qwMem is long l) vramBytes = l;
                    else if (qwMem is int i) vramBytes = i;
                    else
                    {
                        var mem = subKey.GetValue("HardwareInformation.MemorySize");
                        if (mem is int m) vramBytes = (uint)m;
                        else if (mem is byte[] bytes && bytes.Length >= 4)
                            vramBytes = BitConverter.ToUInt32(bytes, 0);
                    }

                    var info = AnalyzeGpu(driverDesc, vramBytes, driverVersion, provider);
                    gpus.Add(info);
                }
            }

            if (gpus.Count > 0)
            {
                // При наличии нескольких видеокарт (встройка + дискретка) выбираем наиболее мощную дискретную
                gpus.Sort((a, b) =>
                {
                    if (a.SupportsRtx && !b.SupportsRtx) return -1;
                    if (!a.SupportsRtx && b.SupportsRtx) return 1;
                    return b.VramMb.CompareTo(a.VramMb);
                });
                return gpus[0];
            }
        }
        catch { }

        return new GpuInfo
        {
            Vendor = "Generic",
            ModelName = "Стандартный видеоадаптер",
            VramMb = 4096,
            SupportsRtx = false,
            SupportsDlss = false,
            SupportsDlss5Neural = false,
            SupportsFsr3 = true,
            SupportsFrameGen = true,
            RecommendedMode = "fsr3_framegen",
            RecommendationReason = "Универсальный профиль для максимальной стабильности."
        };
    }

    /// <summary>
    /// Анализирует модель GPU и формирует матрицу совместимости (чистая логика для тестирования).
    /// </summary>
    public static GpuInfo AnalyzeGpu(string name, long vramBytes, string driverVersion, string provider)
    {
        name ??= "";
        driverVersion ??= "";
        provider ??= "";

        var vramMb = (int)Math.Clamp(vramBytes / (1024 * 1024), 0, 131072);
        if (vramMb == 0) vramMb = 4096; // Фоллбек при неопределённом VRAM

        var upper = name.ToUpperInvariant();
        string vendor = "Unknown";

        if (upper.Contains("NVIDIA") || provider.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            vendor = "NVIDIA";
        else if (upper.Contains("AMD") || upper.Contains("RADEON") || provider.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase))
            vendor = "AMD";
        else if (upper.Contains("INTEL") || upper.Contains("ARC") || provider.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            vendor = "Intel";

        bool isRtx = vendor == "NVIDIA" && (upper.Contains("RTX") || upper.Contains("TITAN RTX"));
        bool isGtx = vendor == "NVIDIA" && upper.Contains("GTX");

        bool supportsDlss = isRtx;
        // Экспериментальный Neural DLSS 5 требует RTX и рекомендует от 6-8 ГБ VRAM
        bool supportsDlss5Neural = isRtx && vramMb >= 6000;

        string recommended;
        string reason;

        if (isRtx)
        {
            if (upper.Contains("40") || upper.Contains("50"))
            {
                recommended = "dlss_framegen";
                reason = "Ваша видеокарта серии RTX 40/50 поддерживает аппаратную генерацию кадров и DLSS 3.7. Также доступен экспериментальный DLSS 5.";
            }
            else
            {
                recommended = "dlss_framegen";
                reason = "Поддерживается аппаратное ускорение DLSS и Neural Reconstruction («DLSS 5»).";
            }
        }
        else if (vendor == "AMD")
        {
            recommended = "fsr3_framegen";
            reason = "Рекомендуется AMD FSR 3.1 + Frame Generation для прироста до +70% FPS.";
        }
        else if (isGtx)
        {
            recommended = "fsr3_framegen";
            reason = "Для карт GeForce GTX идеален FSR 3 с генерацией кадров для удвоения плавности.";
        }
        else
        {
            recommended = "fsr3_framegen";
            reason = "Универсальный FSR 3 обеспечивает максимальную частоту кадров на любом оборудовании.";
        }

        return new GpuInfo
        {
            Vendor = vendor,
            ModelName = name.Trim(),
            VramMb = vramMb,
            DriverVersion = driverVersion,
            SupportsRtx = isRtx,
            SupportsDlss = supportsDlss,
            SupportsDlss5Neural = supportsDlss5Neural,
            SupportsFsr3 = true,
            SupportsFrameGen = true,
            RecommendedMode = recommended,
            RecommendationReason = reason
        };
    }
}
