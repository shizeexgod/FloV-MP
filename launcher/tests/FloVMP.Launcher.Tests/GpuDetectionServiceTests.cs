using FloVMP.Launcher.Native.Services;
using Xunit;

namespace FloVMP.Launcher.Tests;

public sealed class GpuDetectionServiceTests
{
    [Fact]
    public void AnalyzeGpu_Rtx4060_IdentifiesRtxAndRecommendsDlssFrameGen()
    {
        long vram = 8L * 1024 * 1024 * 1024; // 8 GB
        var info = GpuDetectionService.AnalyzeGpu("NVIDIA GeForce RTX 4060 Ti", vram, "32.0.16.1088", "NVIDIA");

        Assert.Equal("NVIDIA", info.Vendor);
        Assert.True(info.SupportsRtx);
        Assert.True(info.SupportsDlss);
        Assert.True(info.SupportsDlss5Neural);
        Assert.True(info.SupportsFrameGen);
        Assert.Equal("dlss_framegen", info.RecommendedMode);
        Assert.Contains("RTX", info.RecommendationReason);
    }

    [Fact]
    public void AnalyzeGpu_Rtx3080_IdentifiesRtxAndSupportsDlss5Neural()
    {
        long vram = 10L * 1024 * 1024 * 1024; // 10 GB
        var info = GpuDetectionService.AnalyzeGpu("NVIDIA GeForce RTX 3080", vram, "31.0.15.4633", "NVIDIA");

        Assert.Equal("NVIDIA", info.Vendor);
        Assert.True(info.SupportsRtx);
        Assert.True(info.SupportsDlss);
        Assert.True(info.SupportsDlss5Neural);
    }

    [Fact]
    public void AnalyzeGpu_Gtx1660_IdentifiesGtxAndRecommendsFsr3()
    {
        long vram = 6L * 1024 * 1024 * 1024; // 6 GB
        var info = GpuDetectionService.AnalyzeGpu("NVIDIA GeForce GTX 1660 Super", vram, "31.0.15.3623", "NVIDIA");

        Assert.Equal("NVIDIA", info.Vendor);
        Assert.False(info.SupportsRtx);
        Assert.False(info.SupportsDlss);
        Assert.False(info.SupportsDlss5Neural);
        Assert.True(info.SupportsFsr3);
        Assert.Equal("fsr3_framegen", info.RecommendedMode);
        Assert.Contains("GTX", info.RecommendationReason);
    }

    [Fact]
    public void AnalyzeGpu_AmdRadeon_IdentifiesAmdAndRecommendsFsr3()
    {
        long vram = 12L * 1024 * 1024 * 1024; // 12 GB
        var info = GpuDetectionService.AnalyzeGpu("AMD Radeon RX 6700 XT", vram, "23.12.1", "Advanced Micro Devices, Inc.");

        Assert.Equal("AMD", info.Vendor);
        Assert.False(info.SupportsRtx);
        Assert.False(info.SupportsDlss);
        Assert.False(info.SupportsDlss5Neural);
        Assert.True(info.SupportsFsr3);
        Assert.Equal("fsr3_framegen", info.RecommendedMode);
        Assert.Contains("AMD", info.RecommendationReason);
    }

    [Fact]
    public void AnalyzeGpu_IntelArc_IdentifiesIntel()
    {
        long vram = 16L * 1024 * 1024 * 1024; // 16 GB
        var info = GpuDetectionService.AnalyzeGpu("Intel(R) Arc(TM) A770 Graphics", vram, "31.0.101.4952", "Intel Corporation");

        Assert.Equal("Intel", info.Vendor);
        Assert.False(info.SupportsRtx);
        Assert.True(info.SupportsFsr3);
        Assert.Equal("fsr3_framegen", info.RecommendedMode);
    }

    [Fact]
    public void Detect_LiveSystem_ReturnsValidGpuInfo()
    {
        var info = GpuDetectionService.Detect();

        Assert.NotNull(info);
        Assert.False(string.IsNullOrWhiteSpace(info.Vendor));
        Assert.False(string.IsNullOrWhiteSpace(info.ModelName));
        Assert.True(info.VramMb > 0);
    }
}
