using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRuntimeSetupControllerTests
{
    [Fact]
    public async Task PrepareAsync_laed_runtime_daten_loggt_und_setzt_progress_max()
    {
        var cases = new List<TrainingCase>
        {
            new() { CaseId = "101.1-102.1" },
            new() { CaseId = "102.1-103.1" }
        };
        var samples = new List<TrainingSample>
        {
            new() { Signature = "sig-1" },
            new() { Signature = "" },
            new() { Signature = "sig-2" }
        };
        var calls = new List<string>();
        var settings = new TrainingCenterSettings { GpuConcurrency = 3 };
        var cfg = new AiRuntimeSettings(
            Enabled: true,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: "embed",
            FfmpegPath: "ffmpeg-custom",
            OllamaRequestTimeout: TimeSpan.FromMinutes(2),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 2048);

        var result = await TrainingBatchImportRuntimeSetupController.PrepareAsync(
            cases,
            loadConfig: () =>
            {
                calls.Add("load-config");
                return cfg;
            },
            loadSettingsAsync: () =>
            {
                calls.Add("load-settings");
                return Task.FromResult(settings);
            },
            createGenerator: (runtime, loadedSettings) =>
            {
                calls.Add($"create-generator:{runtime.VisionModel}:{loadedSettings.GpuConcurrency}");
                return "generator";
            },
            loadSamplesAsync: () =>
            {
                calls.Add("load-samples");
                return Task.FromResult(samples);
            },
            setProgressMax: value => calls.Add($"progress-max:{value}"),
            log: value => calls.Add($"log:{value}"));

        Assert.Same(cfg, result.Config);
        Assert.Same(settings, result.Settings);
        Assert.Equal("generator", result.Generator);
        Assert.Same(samples, result.AllSamples);
        Assert.Same(cases, result.CasesToProcess);
        Assert.IsType<TrainingBatchImportRunSummary>(result.RunSummary);
        Assert.Equal(new[] { "sig-1", "sig-2" }, result.ExistingSignatures.OrderBy(s => s));
        Assert.Equal(
            new[]
            {
                "load-config",
                "log:AI Config: Enabled=True, ffmpeg=ffmpeg-custom",
                "load-settings",
                "create-generator:vision:3",
                "load-samples",
                "log:Bestehende Samples: 3 (2 Signaturen)",
                "progress-max:2"
            },
            calls);
    }
}
