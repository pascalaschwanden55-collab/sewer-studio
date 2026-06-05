using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SelfTrainingOrchestratorFrameTimeTests
{
    [Fact]
    public void AttachExtractedVideoFrame_preserves_linear_extraction_time_without_protocol_timestamp()
    {
        var entry = new GroundTruthEntry
        {
            MeterStart = 12.0,
            MeterEnd = 12.0,
            VsaCode = "BAB",
            Text = "Riss",
            IsStreckenschaden = false
        };

        var updated = SelfTrainingOrchestrator.AttachExtractedVideoFrame(
            entry,
            "frame.png",
            42.5);

        Assert.Equal("frame.png", updated.ExtractedFramePath);
        Assert.Equal(42.5, updated.ExtractedFrameTimeSeconds);
        Assert.Null(updated.Zeit);
    }

    [Fact]
    public void AttachExtractedVideoFrame_keeps_protocol_timestamp_for_reliability_check()
    {
        var entry = new GroundTruthEntry
        {
            MeterStart = 12.0,
            MeterEnd = 12.0,
            VsaCode = "BAB",
            Text = "Riss",
            IsStreckenschaden = false,
            Zeit = TimeSpan.FromSeconds(15)
        };

        var updated = SelfTrainingOrchestrator.AttachExtractedVideoFrame(
            entry,
            "frame.png",
            15.0);

        Assert.Equal("frame.png", updated.ExtractedFramePath);
        Assert.Equal(15.0, updated.ExtractedFrameTimeSeconds);
        Assert.Equal(TimeSpan.FromSeconds(15), updated.Zeit);
    }

    // ── ComputeMaxMeter: darf bei leerer Eintragsliste NICHT abstuerzen ──
    // (Regression: 'Sequence contains no elements' bei Sanierungs-PDF ohne
    //  erkannte Standard-Codes + vorhandenem Video.)

    [Fact]
    public void ComputeMaxMeter_returns_safe_default_for_empty_list()
    {
        Assert.Equal(100.0, SelfTrainingOrchestrator.ComputeMaxMeter(new List<GroundTruthEntry>()));
    }

    [Fact]
    public void ComputeMaxMeter_returns_safe_default_when_all_meters_zero()
    {
        var entries = new List<GroundTruthEntry>
        {
            new() { VsaCode = "BAB", Text = "x", MeterStart = 0, MeterEnd = 0, IsStreckenschaden = false },
        };

        Assert.Equal(100.0, SelfTrainingOrchestrator.ComputeMaxMeter(entries));
    }

    [Fact]
    public void ComputeMaxMeter_returns_largest_meter()
    {
        var entries = new List<GroundTruthEntry>
        {
            new() { VsaCode = "BAB", Text = "x", MeterStart = 3.0, MeterEnd = 5.0, IsStreckenschaden = false },
            new() { VsaCode = "BCD", Text = "y", MeterStart = 12.0, MeterEnd = 12.0, IsStreckenschaden = false },
        };

        Assert.Equal(12.0, SelfTrainingOrchestrator.ComputeMaxMeter(entries));
    }
}
