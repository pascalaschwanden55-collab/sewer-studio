using System;
using System.Linq;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection("EnvironmentVars")]
public class PipelineConfigTests
{
    [Fact]
    public void Load_NoEnvVars_ReturnsDefaults()
    {
        // Ensure env vars are not set (backup and restore)
        var backup = BackupEnvVars();
        try
        {
            ClearEnvVars();

            var config = PipelineConfig.Load();

            Assert.False(config.MultiModelEnabled);
            Assert.Equal(new Uri("http://localhost:8100"), config.SidecarUrl);
            Assert.Equal(PipelineMode.OllamaOnly, config.Mode);
            Assert.Equal(0.25, config.YoloConfidence);
            Assert.Equal(0.30, config.DinoBoxThreshold);
            Assert.Equal(0.25, config.DinoTextThreshold);
            Assert.Equal(300, config.SidecarTimeoutSec);
            Assert.Null(config.PipeDiameterMmOverride);
        }
        finally
        {
            RestoreEnvVars(backup);
        }
    }

    [Fact]
    public void Load_EnabledEnvVar_EnablesMultiModel()
    {
        var backup = BackupEnvVars();
        try
        {
            ClearEnvVars();
            Environment.SetEnvironmentVariable("SEWERSTUDIO_MULTIMODEL_ENABLED", "1");

            var config = PipelineConfig.Load();

            Assert.True(config.MultiModelEnabled);
        }
        finally
        {
            RestoreEnvVars(backup);
        }
    }

    [Theory]
    [InlineData("multimodel", PipelineMode.MultiModel)]
    [InlineData("multi", PipelineMode.MultiModel)]
    [InlineData("ollama", PipelineMode.OllamaOnly)]
    [InlineData("ollamaonly", PipelineMode.OllamaOnly)]
    [InlineData("auto", PipelineMode.Auto)]
    [InlineData("", PipelineMode.OllamaOnly)]
    [InlineData("unknown", PipelineMode.Auto)]
    public void Load_PipelineMode_ParsesCorrectly(string modeStr, PipelineMode expected)
    {
        var backup = BackupEnvVars();
        try
        {
            ClearEnvVars();
            Environment.SetEnvironmentVariable("SEWERSTUDIO_PIPELINE_MODE", modeStr);

            var config = PipelineConfig.Load();

            Assert.Equal(expected, config.Mode);
        }
        finally
        {
            RestoreEnvVars(backup);
        }
    }

    [Fact]
    public void Load_CustomThresholds_ParsesCorrectly()
    {
        var backup = BackupEnvVars();
        try
        {
            ClearEnvVars();
            Environment.SetEnvironmentVariable("SEWERSTUDIO_YOLO_CONFIDENCE", "0.5");
            Environment.SetEnvironmentVariable("SEWERSTUDIO_DINO_BOX_THRESHOLD", "0.4");
            Environment.SetEnvironmentVariable("SEWERSTUDIO_DINO_TEXT_THRESHOLD", "0.35");
            Environment.SetEnvironmentVariable("SEWERSTUDIO_PIPE_DIAMETER_MM", "400");

            var config = PipelineConfig.Load();

            Assert.Equal(0.5, config.YoloConfidence);
            Assert.Equal(0.4, config.DinoBoxThreshold);
            Assert.Equal(0.35, config.DinoTextThreshold);
            Assert.Equal(400, config.PipeDiameterMmOverride);
        }
        finally
        {
            RestoreEnvVars(backup);
        }
    }

    // ── Helpers ──

    private static readonly string[] EnvKeys =
    [
        "SEWERSTUDIO_MULTIMODEL_ENABLED",
        "SEWERSTUDIO_SIDECAR_URL",
        "SEWERSTUDIO_PIPELINE_MODE",
        "SEWERSTUDIO_YOLO_CONFIDENCE",
        "SEWERSTUDIO_DINO_BOX_THRESHOLD",
        "SEWERSTUDIO_DINO_TEXT_THRESHOLD",
        "SEWERSTUDIO_SIDECAR_TIMEOUT_SEC",
        "SEWERSTUDIO_PIPE_DIAMETER_MM",
    ];

    private static readonly string[] LegacyKeys = EnvKeys
        .Where(k => k.StartsWith("SEWERSTUDIO_", StringComparison.Ordinal))
        .Select(k => "AUSWERTUNGPRO_" + k["SEWERSTUDIO_".Length..])
        .ToArray();

    private static readonly string[] AllKeys = EnvKeys.Concat(LegacyKeys).ToArray();

    private static Dictionary<string, string?> BackupEnvVars()
    {
        var backup = new Dictionary<string, string?>();
        foreach (var key in AllKeys)
            backup[key] = Environment.GetEnvironmentVariable(key);
        return backup;
    }

    private static void ClearEnvVars()
    {
        foreach (var key in AllKeys)
            Environment.SetEnvironmentVariable(key, null);
    }

    private static void RestoreEnvVars(Dictionary<string, string?> backup)
    {
        foreach (var pair in backup)
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }
}
