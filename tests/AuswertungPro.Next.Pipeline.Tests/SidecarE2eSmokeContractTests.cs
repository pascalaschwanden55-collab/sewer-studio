using System.Globalization;
using SidecarE2eSmoke;
using Xunit.Sdk;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SidecarE2eSmokeContractTests
{
    [Fact]
    public void FullPipelineOptions_SetzenSichereStandardwerte()
    {
        var options = SidecarSmokeOptions.Parse(
            ["--video", @"D:\Test\clip.mp4", "--full-pipeline", "--start-sidecar"]);

        Assert.True(options.FullPipeline);
        Assert.True(options.ShouldRunDino);
        Assert.True(options.ShouldRunSam);
        Assert.True(options.ShouldUseSamFallbackBox);
        Assert.True(options.StartSidecar);
        Assert.Equal(3, options.FrameCount);
        Assert.Equal(1.0, options.FrameStepSeconds);
        Assert.Equal(300, options.PipeDiameterMm);
    }

    [Fact]
    public void GoldenContractValidator_AkzeptiertVollstaendigePflichtpruefungen()
    {
        var report = CreateReport();
        report.Frames =
        [
            new FramePipelineReport(1, 0, 100, true, 1, 1, 1, 10, null),
            new FramePipelineReport(2, 1, 100, true, 0, 0, 0, 10, null),
            new FramePipelineReport(3, 2, 100, false, 0, 0, 0, 10, null),
        ];
        foreach (var name in RequiredChecks)
            report.AddCheck(name, true, "ok");

        var result = GoldenContractValidator.Validate(report, CreateContract(), "golden.json");

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Failures));
    }

    [Fact]
    public void GoldenContractValidator_LehntFehlendePruefungAb()
    {
        var report = CreateReport();
        report.Frames =
        [
            new FramePipelineReport(1, 0, 100, true, 0, 0, 0, 10, null),
            new FramePipelineReport(2, 1, 100, true, 0, 0, 0, 10, null),
            new FramePipelineReport(3, 2, 100, true, 0, 0, 0, 10, null),
        ];
        foreach (var name in RequiredChecks.Where(name => name != "sam"))
            report.AddCheck(name, true, "ok");

        var result = GoldenContractValidator.Validate(report, CreateContract(), "golden.json");

        Assert.False(result.Success);
        Assert.Contains(result.Failures, failure => failure.Contains("sam", StringComparison.Ordinal));
    }

    private static readonly string[] RequiredChecks =
    [
        "health",
        "video_frame_decode",
        "classify",
        "yolo",
        "dino",
        "sam",
        "quantification",
        "production_pipeline",
    ];

    private static SidecarSmokeReport CreateReport() => new()
    {
        Health = new HealthReport(true, true, 200, null, "ok", "1.2.0", 1, 32, []),
    };

    private static PipelineGoldenContract CreateContract() => new()
    {
        ContractVersion = 1,
        ExpectedSidecarVersion = "1.2.0",
        MinimumDecodedFrames = 3,
        MinimumSamMasks = 0,
        RequiredChecks = RequiredChecks,
    };
}

public sealed class MachineIntegrationFactAttribute : FactAttribute
{
    public MachineIntegrationFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("SEWERSTUDIO_RUN_MACHINE_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Maschinengebundener GPU-Test: SEWERSTUDIO_RUN_MACHINE_INTEGRATION=1 setzen.";
        }
    }
}

public sealed class SidecarRealVideoIntegrationTests
{
    [MachineIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task EchtesVideo_ErfuelltGoldenVertrag()
    {
        var video = Environment.GetEnvironmentVariable("SEWERSTUDIO_E2E_VIDEO");
        Assert.False(string.IsNullOrWhiteSpace(video), "SEWERSTUDIO_E2E_VIDEO fehlt.");
        var at = Environment.GetEnvironmentVariable("SEWERSTUDIO_E2E_VIDEO_AT") ?? "0";
        Assert.True(
            double.TryParse(at, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            "SEWERSTUDIO_E2E_VIDEO_AT ist ungueltig.");

        var options = SidecarSmokeOptions.Parse(
            ["--video", video!, "--at", at, "--full-pipeline", "--start-sidecar"]);
        Assert.True(options.IsValid(out var validationError), validationError);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSec));
        var report = await new SidecarSmokeRunner().RunAsync(options, cts.Token);

        Assert.True(
            report.Success,
            report.Error
            ?? string.Join(Environment.NewLine, report.GoldenValidation?.Failures ?? Array.Empty<string>()));
    }
}
