using System;
using System.IO;
using FloVMP.Launcher.Native.Models;
using FloVMP.Launcher.Native.Services;
using Xunit;

namespace FloVMP.Launcher.Tests;

public sealed class UpscalerDeploymentServiceTests : IDisposable
{
    private readonly string _tempGtaDir;

    public UpscalerDeploymentServiceTests()
    {
        _tempGtaDir = Path.Combine(Path.GetTempPath(), "flovmp_test_gta_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempGtaDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempGtaDir))
                Directory.Delete(_tempGtaDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void GenerateIniConfig_ContainsAllSectionsAndNuiProtection()
    {
        var settings = new LauncherSettings
        {
            UpscalerMode = "dlss5_neural",
            UpscalerQuality = "quality",
            UpscalerSharpness = 65,
            UpscalerFrameGen = true,
            UpscalerNuiProtection = true
        };

        var gpu = new GpuInfo
        {
            Vendor = "NVIDIA",
            ModelName = "NVIDIA GeForce RTX 4060 Ti",
            VramMb = 8192,
            SupportsRtx = true,
            DriverVersion = "32.0.16.1088"
        };

        var ini = UpscalerDeploymentService.GenerateIniConfig(settings, gpu);

        Assert.Contains("[Upscaler]", ini);
        Assert.Contains("Mode=dlss5_neural", ini);
        Assert.Contains("QualityPreset=quality", ini);
        Assert.Contains("Sharpness=0.65", ini);
        Assert.Contains("FrameGeneration=true", ini);

        // NUI Protection
        Assert.Contains("[NUI_Protection]", ini);
        Assert.Contains("ProtectCEFOverlay=true", ini);
        Assert.Contains("DepthBufferMask=0.9995", ini);
        Assert.Contains("ExcludeAlphaLayers=true", ini);

        // Hardware
        Assert.Contains("[Hardware]", ini);
        Assert.Contains("GpuModel=NVIDIA GeForce RTX 4060 Ti", ini);
    }

    [Fact]
    public void Deploy_WhenModeIsNone_PerformsCleanVanillaState()
    {
        var settings = new LauncherSettings { UpscalerMode = "none" };
        var res = UpscalerDeploymentService.Deploy(_tempGtaDir, settings);

        Assert.True(res.Success);
        Assert.Equal("none", res.Mode);
        Assert.Empty(res.DeployedFiles);
        Assert.False(File.Exists(Path.Combine(_tempGtaDir, UpscalerDeploymentService.ConfigFileName)));
    }

    [Fact]
    public void Deploy_WhenModeIsDlss_CreatesIniAndMarker()
    {
        var settings = new LauncherSettings
        {
            UpscalerMode = "dlss5_neural",
            UpscalerQuality = "balanced",
            UpscalerNuiProtection = true
        };

        var gpu = new GpuInfo
        {
            Vendor = "NVIDIA",
            ModelName = "NVIDIA GeForce RTX 4060 Ti",
            SupportsRtx = true,
            VramMb = 8192
        };

        var res = UpscalerDeploymentService.Deploy(_tempGtaDir, settings, gpu);

        Assert.True(res.Success);
        Assert.Equal("dlss5_neural", res.Mode);
        Assert.Contains(UpscalerDeploymentService.ConfigFileName, res.DeployedFiles);
        Assert.Contains(UpscalerDeploymentService.MarkerFileName, res.DeployedFiles);

        var configFile = Path.Combine(_tempGtaDir, UpscalerDeploymentService.ConfigFileName);
        Assert.True(File.Exists(configFile));
        var content = File.ReadAllText(configFile);
        Assert.Contains("Mode=dlss5_neural", content);
        Assert.Contains("ProtectCEFOverlay=true", content);
    }

    [Fact]
    public void Cleanup_RemovesTransientFiles()
    {
        var settings = new LauncherSettings { UpscalerMode = "fsr3_framegen" };
        var gpu = new GpuInfo { Vendor = "AMD", SupportsRtx = false, VramMb = 8192 };

        UpscalerDeploymentService.Deploy(_tempGtaDir, settings, gpu);
        Assert.True(File.Exists(Path.Combine(_tempGtaDir, UpscalerDeploymentService.ConfigFileName)));

        var cleaned = UpscalerDeploymentService.Cleanup(_tempGtaDir);
        Assert.True(cleaned);
        Assert.False(File.Exists(Path.Combine(_tempGtaDir, UpscalerDeploymentService.ConfigFileName)));
        Assert.False(File.Exists(Path.Combine(_tempGtaDir, UpscalerDeploymentService.MarkerFileName)));
    }
}
